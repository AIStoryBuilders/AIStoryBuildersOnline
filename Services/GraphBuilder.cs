using AIStoryBuilders.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIStoryBuilders.Services
{
    public class GraphBuilder : IGraphBuilder
    {
        public StoryGraph Build(Story story)
        {
            var graph = new StoryGraph { StoryTitle = story?.Title ?? "" };
            if (story == null) return graph;

            var nodesById = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
            var edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Phase 1 - Nodes
            foreach (var ch in story.Character ?? new List<Character>())
            {
                if (!IsValidEntity(ch?.CharacterName)) continue;
                AddNode(nodesById, MakeId(NodeType.Character, ch.CharacterName), ch.CharacterName, NodeType.Character);

                // Attributes from CharacterBackground
                int seq = 0;
                foreach (var bg in ch.CharacterBackground ?? new List<CharacterBackground>())
                {
                    if (string.IsNullOrWhiteSpace(bg?.Description)) continue;
                    string attrType = string.IsNullOrWhiteSpace(bg.Type) ? "Fact" : bg.Type;
                    string attrId = $"attribute:character:{Normalize(ch.CharacterName)}:{Normalize(attrType)}:{seq}";
                    var attrNode = AddNode(nodesById, attrId, bg.Description, NodeType.Attribute);
                    attrNode.Properties["AttributeType"] = attrType;
                    if (bg.Timeline != null && IsValidEntity(bg.Timeline.TimelineName))
                        attrNode.Properties["TimelineName"] = bg.Timeline.TimelineName;

                    AddEdge(graph, edgeIds, MakeId(NodeType.Character, ch.CharacterName), attrId, "HAS_ATTRIBUTE");

                    if (bg.Timeline != null && IsValidEntity(bg.Timeline.TimelineName))
                    {
                        AddNode(nodesById, MakeId(NodeType.Timeline, bg.Timeline.TimelineName), bg.Timeline.TimelineName, NodeType.Timeline);
                        AddEdge(graph, edgeIds, attrId, MakeId(NodeType.Timeline, bg.Timeline.TimelineName), "IN_TIMELINE");
                    }
                    seq++;
                }
            }

            foreach (var loc in story.Location ?? new List<Location>())
            {
                if (!IsValidEntity(loc?.LocationName)) continue;
                AddNode(nodesById, MakeId(NodeType.Location, loc.LocationName), loc.LocationName, NodeType.Location);

                int seq = 0;
                foreach (var desc in loc.LocationDescription ?? new List<LocationDescription>())
                {
                    if (string.IsNullOrWhiteSpace(desc?.Description)) continue;
                    string attrId = $"attribute:location:{Normalize(loc.LocationName)}:description:{seq}";
                    var attrNode = AddNode(nodesById, attrId, desc.Description, NodeType.Attribute);
                    attrNode.Properties["AttributeType"] = "Description";
                    if (desc.Timeline != null && IsValidEntity(desc.Timeline.TimelineName))
                        attrNode.Properties["TimelineName"] = desc.Timeline.TimelineName;

                    AddEdge(graph, edgeIds, MakeId(NodeType.Location, loc.LocationName), attrId, "HAS_ATTRIBUTE");

                    if (desc.Timeline != null && IsValidEntity(desc.Timeline.TimelineName))
                    {
                        AddNode(nodesById, MakeId(NodeType.Timeline, desc.Timeline.TimelineName), desc.Timeline.TimelineName, NodeType.Timeline);
                        AddEdge(graph, edgeIds, attrId, MakeId(NodeType.Timeline, desc.Timeline.TimelineName), "IN_TIMELINE");
                    }
                    seq++;
                }
            }

            foreach (var tl in story.Timeline ?? new List<Timeline>())
            {
                if (!IsValidEntity(tl?.TimelineName)) continue;
                var node = AddNode(nodesById, MakeId(NodeType.Timeline, tl.TimelineName), tl.TimelineName, NodeType.Timeline);
                if (!string.IsNullOrWhiteSpace(tl.TimelineDescription)) node.Properties["Description"] = tl.TimelineDescription;
                if (tl.StartDate.HasValue) node.Properties["StartDate"] = tl.StartDate.Value.ToString("o");
                if (tl.StopDate.HasValue) node.Properties["EndDate"] = tl.StopDate.Value.ToString("o");
            }

            foreach (var ch in story.Chapter ?? new List<Chapter>())
            {
                if (!IsValidEntity(ch?.ChapterName)) continue;
                var cNode = AddNode(nodesById, MakeId(NodeType.Chapter, ch.ChapterName), ch.ChapterName, NodeType.Chapter);
                cNode.Properties["Sequence"] = ch.Sequence.ToString();
                if (!string.IsNullOrWhiteSpace(ch.Synopsis)) cNode.Properties["Synopsis"] = ch.Synopsis;
            }

            var knownCharacterNames = (story.Character ?? new List<Character>())
                .Where(c => IsValidEntity(c?.CharacterName))
                .Select(c => c.CharacterName)
                .ToList();

            // Phase 2 - Edges
            foreach (var chap in story.Chapter ?? new List<Chapter>())
            {
                if (!IsValidEntity(chap?.ChapterName)) continue;
                string chapId = MakeId(NodeType.Chapter, chap.ChapterName);
                var chapLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var chapTimelines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in chap.Paragraph ?? new List<Paragraph>())
                {
                    string pId = $"paragraph:{Normalize(chap.ChapterName)}:p{p.Sequence}";
                    var pNode = AddNode(nodesById, pId, $"{chap.ChapterName} P{p.Sequence}", NodeType.Paragraph);
                    pNode.Properties["Sequence"] = p.Sequence.ToString();
                    pNode.Properties["ChapterName"] = chap.ChapterName;
                    if (!string.IsNullOrWhiteSpace(p.ParagraphContent)) pNode.Properties["Content"] = p.ParagraphContent;
                    if (p.Location != null && IsValidEntity(p.Location.LocationName)) pNode.Properties["LocationName"] = p.Location.LocationName;
                    if (p.Timeline != null && IsValidEntity(p.Timeline.TimelineName)) pNode.Properties["TimelineName"] = p.Timeline.TimelineName;

                    AddEdge(graph, edgeIds, chapId, pId, "CONTAINS");

                    // Characters referenced
                    var resolvedCharacters = new List<string>();
                    foreach (var pc in p.Characters ?? new List<Character>())
                    {
                        if (!IsValidEntity(pc?.CharacterName)) continue;
                        string resolved = ResolveCharacterName(pc.CharacterName, knownCharacterNames);
                        AddNode(nodesById, MakeId(NodeType.Character, resolved), resolved, NodeType.Character);
                        resolvedCharacters.Add(resolved);
                    }
                    resolvedCharacters = resolvedCharacters.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    foreach (var c in resolvedCharacters)
                    {
                        string cId = MakeId(NodeType.Character, c);
                        AddEdge(graph, edgeIds, cId, pId, "MENTIONED_IN");
                        AddEdge(graph, edgeIds, cId, chapId, "APPEARS_IN");

                        if (p.Location != null && IsValidEntity(p.Location.LocationName))
                        {
                            string lId = MakeId(NodeType.Location, p.Location.LocationName);
                            AddNode(nodesById, lId, p.Location.LocationName, NodeType.Location);
                            AddEdge(graph, edgeIds, cId, lId, "SEEN_AT");
                        }
                    }

                    // Pairwise interactions (deterministic)
                    var sorted = resolvedCharacters.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        for (int j = i + 1; j < sorted.Count; j++)
                        {
                            AddEdge(graph, edgeIds,
                                MakeId(NodeType.Character, sorted[i]),
                                MakeId(NodeType.Character, sorted[j]),
                                "INTERACTS_WITH");
                        }
                    }

                    if (p.Location != null && IsValidEntity(p.Location.LocationName)) chapLocations.Add(p.Location.LocationName);
                    if (p.Timeline != null && IsValidEntity(p.Timeline.TimelineName)) chapTimelines.Add(p.Timeline.TimelineName);
                }

                foreach (var locName in chapLocations)
                {
                    string lId = MakeId(NodeType.Location, locName);
                    AddNode(nodesById, lId, locName, NodeType.Location);
                    AddEdge(graph, edgeIds, lId, chapId, "SETTING_OF");
                }
                foreach (var tlName in chapTimelines)
                {
                    string tId = MakeId(NodeType.Timeline, tlName);
                    AddNode(nodesById, tId, tlName, NodeType.Timeline);
                    AddEdge(graph, edgeIds, tId, chapId, "COVERS");
                }
            }

            // ACTIVE_ON from CharacterBackground timelines
            foreach (var ch in story.Character ?? new List<Character>())
            {
                if (!IsValidEntity(ch?.CharacterName)) continue;
                var timelineNames = (ch.CharacterBackground ?? new List<CharacterBackground>())
                    .Where(b => b.Timeline != null && IsValidEntity(b.Timeline.TimelineName))
                    .Select(b => b.Timeline.TimelineName)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var tn in timelineNames)
                {
                    AddNode(nodesById, MakeId(NodeType.Timeline, tn), tn, NodeType.Timeline);
                    AddEdge(graph, edgeIds,
                        MakeId(NodeType.Character, ch.CharacterName),
                        MakeId(NodeType.Timeline, tn),
                        "ACTIVE_ON");
                }
            }

            graph.Nodes = nodesById.Values.ToList();
            return graph;
        }

        private static bool IsValidEntity(string s)
            => !string.IsNullOrWhiteSpace(s) && !string.Equals(s.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase);

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var cleaned = System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"[\s_\-]+", " ");
            return cleaned.ToLowerInvariant();
        }

        private static string MakeId(NodeType type, string label)
        {
            string prefix = type switch
            {
                NodeType.Character => "character",
                NodeType.Location => "location",
                NodeType.Timeline => "timeline",
                NodeType.Chapter => "chapter",
                _ => type.ToString().ToLowerInvariant()
            };
            return $"{prefix}:{Normalize(label)}";
        }

        private static GraphNode AddNode(Dictionary<string, GraphNode> nodes, string id, string label, NodeType type)
        {
            if (nodes.TryGetValue(id, out var existing)) return existing;
            var node = new GraphNode { Id = id, Label = label, Type = type };
            nodes[id] = node;
            return node;
        }

        private static void AddEdge(StoryGraph graph, HashSet<string> ids, string source, string target, string label)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return;
            string id = $"{source}--{label}--{target}";
            if (!ids.Add(id)) return;
            graph.Edges.Add(new GraphEdge { Id = id, SourceId = source, TargetId = target, Label = label });
        }

        private static string ResolveCharacterName(string raw, List<string> known)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            string trimmed = raw.Trim();
            var exact = known.FirstOrDefault(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            var sub = known.FirstOrDefault(k =>
                k.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
            if (sub != null) return sub;
            var fuzzy = known.FirstOrDefault(k => Levenshtein(k.ToLowerInvariant(), trimmed.ToLowerInvariant()) <= 2);
            return fuzzy ?? trimmed;
        }

        private static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            return d[a.Length, b.Length];
        }
    }
}
