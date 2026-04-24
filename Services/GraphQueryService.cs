using AIStoryBuilders.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIStoryBuilders.Services
{
    public class GraphQueryService : IGraphQueryService
    {
        private StoryGraph Graph => GraphState.Current;

        private GraphNode FindByLabel(string label, NodeType type)
        {
            if (Graph == null || string.IsNullOrWhiteSpace(label)) return null;
            return Graph.Nodes.FirstOrDefault(n => n.Type == type &&
                string.Equals(n.Label, label, StringComparison.OrdinalIgnoreCase));
        }

        public CharacterDto GetCharacter(string name)
        {
            var node = FindByLabel(name, NodeType.Character);
            if (node == null) return null;
            return new CharacterDto
            {
                Name = node.Label,
                Attributes = ListAttributes("character", node.Label),
                Timelines = (Graph?.Edges ?? new List<GraphEdge>())
                    .Where(e => e.SourceId == node.Id && e.Label == "ACTIVE_ON")
                    .Select(e => Graph.Nodes.FirstOrDefault(n => n.Id == e.TargetId)?.Label)
                    .Where(s => !string.IsNullOrEmpty(s)).ToList()
            };
        }

        public List<CharacterDto> ListCharacters()
        {
            if (Graph == null) return new List<CharacterDto>();
            return Graph.Nodes.Where(n => n.Type == NodeType.Character)
                .Select(n => GetCharacter(n.Label)).Where(x => x != null).ToList();
        }

        public LocationDto GetLocation(string name)
        {
            var node = FindByLabel(name, NodeType.Location);
            if (node == null) return null;
            return new LocationDto
            {
                Name = node.Label,
                Attributes = ListAttributes("location", node.Label)
            };
        }

        public List<LocationDto> ListLocations()
        {
            if (Graph == null) return new List<LocationDto>();
            return Graph.Nodes.Where(n => n.Type == NodeType.Location)
                .Select(n => GetLocation(n.Label)).Where(x => x != null).ToList();
        }

        public TimelineDto GetTimeline(string name)
        {
            var node = FindByLabel(name, NodeType.Timeline);
            if (node == null) return null;
            return new TimelineDto
            {
                Name = node.Label,
                Description = node.Properties.TryGetValue("Description", out var d) ? d : "",
                StartDate = node.Properties.TryGetValue("StartDate", out var s) ? s : "",
                EndDate = node.Properties.TryGetValue("EndDate", out var e) ? e : ""
            };
        }

        public List<TimelineDto> ListTimelines()
        {
            if (Graph == null) return new List<TimelineDto>();
            return Graph.Nodes.Where(n => n.Type == NodeType.Timeline)
                .Select(n => GetTimeline(n.Label)).Where(x => x != null).ToList();
        }

        public ChapterDto GetChapter(string title)
        {
            var node = FindByLabel(title, NodeType.Chapter);
            if (node == null) return null;
            int seq = 0;
            if (node.Properties.TryGetValue("Sequence", out var s)) int.TryParse(s, out seq);
            int pCount = Graph.Edges.Count(e => e.SourceId == node.Id && e.Label == "CONTAINS");
            return new ChapterDto
            {
                Name = node.Label,
                Sequence = seq,
                Synopsis = node.Properties.TryGetValue("Synopsis", out var syn) ? syn : "",
                ParagraphCount = pCount
            };
        }

        public List<ChapterDto> ListChapters()
        {
            if (Graph == null) return new List<ChapterDto>();
            return Graph.Nodes.Where(n => n.Type == NodeType.Chapter)
                .Select(n => GetChapter(n.Label)).Where(x => x != null)
                .OrderBy(x => x.Sequence).ToList();
        }

        public ParagraphDto GetParagraph(string chapter, int index)
        {
            if (Graph == null) return null;
            var resolvedChapter = ResolveChapterName(chapter);
            if (resolvedChapter == null) return null;
            var p = Graph.Nodes.FirstOrDefault(n => n.Type == NodeType.Paragraph &&
                n.Properties.TryGetValue("ChapterName", out var cn) &&
                string.Equals(cn, resolvedChapter, StringComparison.OrdinalIgnoreCase) &&
                n.Properties.TryGetValue("Sequence", out var seq) && seq == index.ToString());
            if (p == null) return null;
            return ToParagraphDto(p);
        }

        public IReadOnlyList<ParagraphDto> ListParagraphs(string chapter)
        {
            if (Graph == null || string.IsNullOrWhiteSpace(chapter)) return new List<ParagraphDto>();
            var resolvedChapter = ResolveChapterName(chapter);
            if (resolvedChapter == null) return new List<ParagraphDto>();
            return Graph.Nodes
                .Where(n => n.Type == NodeType.Paragraph &&
                    n.Properties.TryGetValue("ChapterName", out var cn) &&
                    string.Equals(cn, resolvedChapter, StringComparison.OrdinalIgnoreCase))
                .Select(ToParagraphDto)
                .OrderBy(p => p.Sequence)
                .ToList();
        }

        /// <summary>
        /// Resolves a chapter identifier supplied by a caller (typically the chat
        /// assistant) to the actual chapter node label stored in the graph.
        /// Falls back to matching by sequence number when the caller uses a
        /// generic form like "Chapter 1" and no chapter with that exact label
        /// exists.
        /// </summary>
        private string ResolveChapterName(string chapter)
        {
            if (Graph == null || string.IsNullOrWhiteSpace(chapter)) return null;

            var chapterNodes = Graph.Nodes.Where(n => n.Type == NodeType.Chapter).ToList();

            var exact = chapterNodes.FirstOrDefault(n =>
                string.Equals(n.Label, chapter, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.Label;

            // Fallback: "Chapter N" → match by Sequence == N
            var m = System.Text.RegularExpressions.Regex.Match(
                chapter.Trim(),
                @"^(?:chapter\s*)?(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var seqNum))
            {
                var bySeq = chapterNodes.FirstOrDefault(n =>
                    n.Properties.TryGetValue("Sequence", out var s) &&
                    int.TryParse(s, out var i) && i == seqNum);
                if (bySeq != null) return bySeq.Label;
            }

            return null;
        }

        public ParagraphTextDto GetParagraphText(string chapter, int index)
        {
            var dto = GetParagraph(chapter, index);
            if (dto == null) return null;
            return new ParagraphTextDto
            {
                Text = dto.Content,
                Location = dto.LocationName,
                Timeline = dto.TimelineName,
                Characters = dto.Characters ?? new List<string>()
            };
        }

        private ParagraphDto ToParagraphDto(GraphNode p)
        {
            int seq = 0;
            if (p.Properties.TryGetValue("Sequence", out var s)) int.TryParse(s, out seq);
            var chars = Graph.Edges.Where(e => e.TargetId == p.Id && e.Label == "MENTIONED_IN")
                .Select(e => Graph.Nodes.FirstOrDefault(n => n.Id == e.SourceId)?.Label)
                .Where(x => !string.IsNullOrEmpty(x)).ToList();
            return new ParagraphDto
            {
                ChapterName = p.Properties.TryGetValue("ChapterName", out var cn) ? cn : "",
                Sequence = seq,
                Content = p.Properties.TryGetValue("Content", out var c) ? c : "",
                LocationName = p.Properties.TryGetValue("LocationName", out var ln) ? ln : "",
                TimelineName = p.Properties.TryGetValue("TimelineName", out var tn) ? tn : "",
                Characters = chars
            };
        }

        public List<RelationshipDto> GetRelationships(string name)
        {
            if (Graph == null) return new List<RelationshipDto>();
            var node = Graph.Nodes.FirstOrDefault(n =>
                string.Equals(n.Label, name, StringComparison.OrdinalIgnoreCase));
            if (node == null) return new List<RelationshipDto>();
            var list = new List<RelationshipDto>();
            foreach (var e in Graph.Edges)
            {
                if (e.SourceId == node.Id)
                {
                    var t = Graph.Nodes.FirstOrDefault(n => n.Id == e.TargetId);
                    if (t != null) list.Add(new RelationshipDto { SourceName = node.Label, TargetName = t.Label, Label = e.Label });
                }
                else if (e.TargetId == node.Id)
                {
                    var s = Graph.Nodes.FirstOrDefault(n => n.Id == e.SourceId);
                    if (s != null) list.Add(new RelationshipDto { SourceName = s.Label, TargetName = node.Label, Label = e.Label });
                }
            }
            return list;
        }

        public List<AppearanceDto> GetAppearances(string characterName)
        {
            if (Graph == null) return new List<AppearanceDto>();
            var c = FindByLabel(characterName, NodeType.Character);
            if (c == null) return new List<AppearanceDto>();
            var paraIds = Graph.Edges.Where(e => e.SourceId == c.Id && e.Label == "MENTIONED_IN")
                .Select(e => e.TargetId).ToHashSet();
            return Graph.Nodes.Where(n => n.Type == NodeType.Paragraph && paraIds.Contains(n.Id))
                .Select(n => new AppearanceDto
                {
                    ChapterName = n.Properties.TryGetValue("ChapterName", out var cn) ? cn : "",
                    ParagraphSequence = n.Properties.TryGetValue("Sequence", out var s) && int.TryParse(s, out var i) ? i : 0,
                    Content = n.Properties.TryGetValue("Content", out var ct) ? ct : ""
                }).ToList();
        }

        public List<LocationUsageDto> GetLocationUsage(string locationName)
        {
            if (Graph == null) return new List<LocationUsageDto>();
            var paragraphs = Graph.Nodes.Where(n => n.Type == NodeType.Paragraph &&
                n.Properties.TryGetValue("LocationName", out var ln) &&
                string.Equals(ln, locationName, StringComparison.OrdinalIgnoreCase));
            return paragraphs.Select(p => new LocationUsageDto
            {
                LocationName = locationName,
                ChapterName = p.Properties.TryGetValue("ChapterName", out var cn) ? cn : "",
                ParagraphSequence = p.Properties.TryGetValue("Sequence", out var s) && int.TryParse(s, out var i) ? i : 0
            }).ToList();
        }

        public List<InteractionDto> GetInteractions(string characterName)
        {
            if (Graph == null) return new List<InteractionDto>();
            var c = FindByLabel(characterName, NodeType.Character);
            if (c == null) return new List<InteractionDto>();
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Graph.Edges.Where(e => e.Label == "INTERACTS_WITH" && (e.SourceId == c.Id || e.TargetId == c.Id)))
            {
                var otherId = e.SourceId == c.Id ? e.TargetId : e.SourceId;
                var other = Graph.Nodes.FirstOrDefault(n => n.Id == otherId);
                if (other == null) continue;
                if (!dict.ContainsKey(other.Label)) dict[other.Label] = 0;
                dict[other.Label]++;
            }
            return dict.Select(kv => new InteractionDto { OtherCharacter = kv.Key, SharedParagraphs = kv.Value }).ToList();
        }

        public List<OrphanDto> FindOrphans()
        {
            if (Graph == null) return new List<OrphanDto>();
            var referenced = new HashSet<string>(Graph.Edges.Select(e => e.SourceId)
                .Concat(Graph.Edges.Select(e => e.TargetId)));
            return Graph.Nodes.Where(n => !referenced.Contains(n.Id))
                .Select(n => new OrphanDto { NodeId = n.Id, Label = n.Label, Type = n.Type.ToString() })
                .ToList();
        }

        public List<ArcStepDto> GetCharacterArc(string characterName)
        {
            if (Graph == null) return new List<ArcStepDto>();
            var c = FindByLabel(characterName, NodeType.Character);
            if (c == null) return new List<ArcStepDto>();
            var paraIds = Graph.Edges.Where(e => e.SourceId == c.Id && e.Label == "MENTIONED_IN")
                .Select(e => e.TargetId).ToHashSet();
            var arc = new List<ArcStepDto>();
            foreach (var p in Graph.Nodes.Where(n => n.Type == NodeType.Paragraph && paraIds.Contains(n.Id)))
            {
                int seq = 0;
                if (p.Properties.TryGetValue("Sequence", out var s)) int.TryParse(s, out seq);
                int chSeq = 0;
                var chName = p.Properties.TryGetValue("ChapterName", out var cn) ? cn : "";
                var chNode = FindByLabel(chName, NodeType.Chapter);
                if (chNode != null && chNode.Properties.TryGetValue("Sequence", out var cs)) int.TryParse(cs, out chSeq);
                arc.Add(new ArcStepDto
                {
                    ChapterSequence = chSeq,
                    ParagraphSequence = seq,
                    Content = p.Properties.TryGetValue("Content", out var ct) ? ct : "",
                    LocationName = p.Properties.TryGetValue("LocationName", out var ln) ? ln : ""
                });
            }
            return arc.OrderBy(a => a.ChapterSequence).ThenBy(a => a.ParagraphSequence).ToList();
        }

        public List<LocationEventDto> GetLocationEvents(string locationName)
        {
            if (Graph == null) return new List<LocationEventDto>();
            var events = new List<LocationEventDto>();
            var paragraphs = Graph.Nodes.Where(n => n.Type == NodeType.Paragraph &&
                n.Properties.TryGetValue("LocationName", out var ln) &&
                string.Equals(ln, locationName, StringComparison.OrdinalIgnoreCase));
            foreach (var p in paragraphs)
            {
                int seq = 0;
                if (p.Properties.TryGetValue("Sequence", out var s)) int.TryParse(s, out seq);
                int chSeq = 0;
                var chName = p.Properties.TryGetValue("ChapterName", out var cn) ? cn : "";
                var chNode = FindByLabel(chName, NodeType.Chapter);
                if (chNode != null && chNode.Properties.TryGetValue("Sequence", out var cs)) int.TryParse(cs, out chSeq);
                var chars = Graph.Edges.Where(e => e.TargetId == p.Id && e.Label == "MENTIONED_IN")
                    .Select(e => Graph.Nodes.FirstOrDefault(n => n.Id == e.SourceId)?.Label)
                    .Where(x => !string.IsNullOrEmpty(x)).ToList();
                events.Add(new LocationEventDto
                {
                    ChapterSequence = chSeq,
                    ParagraphSequence = seq,
                    Characters = chars,
                    Content = p.Properties.TryGetValue("Content", out var ct) ? ct : ""
                });
            }
            return events.OrderBy(e => e.ChapterSequence).ThenBy(e => e.ParagraphSequence).ToList();
        }

        public GraphSummaryDto GetGraphSummary()
        {
            if (Graph == null) return new GraphSummaryDto();
            return new GraphSummaryDto
            {
                NodeCount = Graph.Nodes.Count,
                EdgeCount = Graph.Edges.Count,
                CharacterCount = Graph.Nodes.Count(n => n.Type == NodeType.Character),
                LocationCount = Graph.Nodes.Count(n => n.Type == NodeType.Location),
                TimelineCount = Graph.Nodes.Count(n => n.Type == NodeType.Timeline),
                ChapterCount = Graph.Nodes.Count(n => n.Type == NodeType.Chapter),
                ParagraphCount = Graph.Nodes.Count(n => n.Type == NodeType.Paragraph)
            };
        }

        public StoryDetailsDto GetStoryDetails()
        {
            var s = GraphState.CurrentStory;
            var sum = GetGraphSummary();
            return new StoryDetailsDto
            {
                Title = s?.Title ?? Graph?.StoryTitle ?? "",
                Synopsis = s?.Synopsis ?? "",
                Style = s?.Style ?? "",
                Theme = s?.Theme ?? "",
                CharacterCount = sum.CharacterCount,
                LocationCount = sum.LocationCount,
                TimelineCount = sum.TimelineCount,
                ChapterCount = sum.ChapterCount,
                ParagraphCount = sum.ParagraphCount
            };
        }

        public List<AttributeDto> ListAttributes(string parentType, string parentName)
        {
            if (Graph == null) return new List<AttributeDto>();
            NodeType parentTypeEnum = parentType?.ToLowerInvariant() switch
            {
                "character" => NodeType.Character,
                "location" => NodeType.Location,
                _ => NodeType.Character
            };
            var parent = FindByLabel(parentName, parentTypeEnum);
            if (parent == null) return new List<AttributeDto>();
            var attrIds = Graph.Edges.Where(e => e.SourceId == parent.Id && e.Label == "HAS_ATTRIBUTE")
                .Select(e => e.TargetId).ToHashSet();
            return Graph.Nodes.Where(n => n.Type == NodeType.Attribute && attrIds.Contains(n.Id))
                .Select(n => new AttributeDto
                {
                    AttributeType = n.Properties.TryGetValue("AttributeType", out var at) ? at : "",
                    Description = n.Label,
                    TimelineName = n.Properties.TryGetValue("TimelineName", out var tn) ? tn : ""
                }).ToList();
        }

        public TimelineContextDto GetTimelineContext(string timelineName, int chapterSequence, int paragraphSequence)
        {
            var dto = new TimelineContextDto { TimelineName = timelineName };
            if (Graph == null || string.IsNullOrWhiteSpace(timelineName)) return dto;
            var tn = FindByLabel(timelineName, NodeType.Timeline);
            if (tn != null)
            {
                dto.Description = tn.Properties.TryGetValue("Description", out var d) ? d : "";
                dto.StartDate = tn.Properties.TryGetValue("StartDate", out var s) ? s : "";
                dto.EndDate = tn.Properties.TryGetValue("EndDate", out var e) ? e : "";
            }

            // Chars active on timeline
            var activeOnIds = (tn == null) ? new HashSet<string>() :
                Graph.Edges.Where(e => e.TargetId == tn.Id && e.Label == "ACTIVE_ON").Select(e => e.SourceId).ToHashSet();
            foreach (var c in Graph.Nodes.Where(n => n.Type == NodeType.Character && activeOnIds.Contains(n.Id)))
            {
                var attrs = ListAttributes("character", c.Label)
                    .Where(a => string.IsNullOrEmpty(a.TimelineName) ||
                                string.Equals(a.TimelineName, timelineName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                dto.Characters.Add(new TimelineCharacterDto { Name = c.Label, Attributes = attrs });
            }

            // Locations in timeline: those used in paragraphs with this timeline
            var tlParagraphs = Graph.Nodes.Where(n => n.Type == NodeType.Paragraph &&
                n.Properties.TryGetValue("TimelineName", out var ptn) &&
                string.Equals(ptn, timelineName, StringComparison.OrdinalIgnoreCase)).ToList();

            var locNames = tlParagraphs
                .Select(p => p.Properties.TryGetValue("LocationName", out var ln) ? ln : null)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var ln in locNames)
            {
                var locNode = FindByLabel(ln, NodeType.Location);
                var locAttrs = ListAttributes("location", ln);
                dto.Locations.Add(new TimelineLocationDto
                {
                    Name = ln,
                    Description = locAttrs.FirstOrDefault()?.Description ?? ""
                });
            }

            // Events: paragraphs on this timeline with (chSeq, pSeq) < (chapterSequence, paragraphSequence)
            foreach (var p in tlParagraphs)
            {
                int pSeq = 0;
                if (p.Properties.TryGetValue("Sequence", out var s)) int.TryParse(s, out pSeq);
                var chName = p.Properties.TryGetValue("ChapterName", out var cn) ? cn : "";
                int chSeq = 0;
                var chNode = FindByLabel(chName, NodeType.Chapter);
                if (chNode != null && chNode.Properties.TryGetValue("Sequence", out var cs)) int.TryParse(cs, out chSeq);

                // strict "before current paragraph" on the same timeline
                if (chSeq > chapterSequence) continue;
                if (chSeq == chapterSequence && pSeq >= paragraphSequence) continue;

                var chars = Graph.Edges.Where(e => e.TargetId == p.Id && e.Label == "MENTIONED_IN")
                    .Select(e => Graph.Nodes.FirstOrDefault(n => n.Id == e.SourceId)?.Label)
                    .Where(x => !string.IsNullOrEmpty(x)).ToList();

                dto.Events.Add(new TimelineEventDto
                {
                    ChapterSequence = chSeq,
                    ParagraphSequence = pSeq,
                    ChapterName = chName,
                    Characters = chars,
                    LocationName = p.Properties.TryGetValue("LocationName", out var lnm) ? lnm : "",
                    Content = p.Properties.TryGetValue("Content", out var ct) ? ct : ""
                });
            }
            dto.Events = dto.Events.OrderBy(e => e.ChapterSequence).ThenBy(e => e.ParagraphSequence).ToList();
            return dto;
        }
    }
}
