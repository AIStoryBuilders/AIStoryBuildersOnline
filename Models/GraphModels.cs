using System.Collections.Generic;

namespace AIStoryBuilders.Models
{
    public enum NodeType
    {
        Character,
        Location,
        Timeline,
        Chapter,
        Paragraph,
        Attribute
    }

    public class GraphNode
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public NodeType Type { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    public class GraphEdge
    {
        public string Id { get; set; } = "";
        public string SourceId { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string Label { get; set; } = "";
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    public class StoryGraph
    {
        public string StoryTitle { get; set; } = "";
        public List<GraphNode> Nodes { get; set; } = new();
        public List<GraphEdge> Edges { get; set; } = new();
    }

    public class GraphManifest
    {
        public string StoryTitle { get; set; } = "";
        public System.DateTime CreatedDate { get; set; }
        public string Version { get; set; } = "1.0";
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
    }

    public class GraphMetadata
    {
        public string Title { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Theme { get; set; } = "";
        public string Synopsis { get; set; } = "";
        public int CharacterCount { get; set; }
        public int LocationCount { get; set; }
        public int TimelineCount { get; set; }
        public int ChapterCount { get; set; }
        public int ParagraphCount { get; set; }
    }
}
