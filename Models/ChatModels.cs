using System;
using System.Collections.Generic;

namespace AIStoryBuilders.Models
{
    public class ChatDisplayMessage
    {
        public string Role { get; set; } = "user"; // "user" | "assistant" | "system" | "tool"
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ConversationSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string StoryTitle { get; set; } = "";
        public List<ChatDisplayMessage> Messages { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MutationResult
    {
        public bool IsPreview { get; set; }
        public bool Success { get; set; }
        public string Summary { get; set; } = "";
        public List<string> AffectedFiles { get; set; } = new();
        public int EmbeddingsUpdated { get; set; }
        public bool GraphRefreshed { get; set; }
        public string Error { get; set; }

        // Structural-edit diagnostic fields (chapter / paragraph).
        public string BeforeExcerpt { get; set; }
        public string AfterExcerpt { get; set; }
        public string TargetKind { get; set; }   // "Chapter" | "Paragraph"
        public string TargetId { get; set; }     // e.g. "Chapter 3 / Paragraph 4"
    }

    public class ParagraphTextDto
    {
        public string Text { get; set; } = "";
        public string Location { get; set; } = "";
        public string Timeline { get; set; } = "";
        public List<string> Characters { get; set; } = new();
    }

    public class CharacterDto
    {
        public string Name { get; set; } = "";
        public List<AttributeDto> Attributes { get; set; } = new();
        public List<string> Timelines { get; set; } = new();
    }

    public class LocationDto
    {
        public string Name { get; set; } = "";
        public List<AttributeDto> Attributes { get; set; } = new();
    }

    public class TimelineDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
    }

    public class ChapterDto
    {
        public string Name { get; set; } = "";
        public int Sequence { get; set; }
        public string Synopsis { get; set; } = "";
        public int ParagraphCount { get; set; }
    }

    public class ParagraphDto
    {
        public string ChapterName { get; set; } = "";
        public int Sequence { get; set; }
        public string Content { get; set; } = "";
        public string LocationName { get; set; } = "";
        public string TimelineName { get; set; } = "";
        public List<string> Characters { get; set; } = new();
    }

    public class RelationshipDto
    {
        public string SourceName { get; set; } = "";
        public string TargetName { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class AppearanceDto
    {
        public string ChapterName { get; set; } = "";
        public int ParagraphSequence { get; set; }
        public string Content { get; set; } = "";
    }

    public class LocationUsageDto
    {
        public string LocationName { get; set; } = "";
        public string ChapterName { get; set; } = "";
        public int ParagraphSequence { get; set; }
    }

    public class InteractionDto
    {
        public string OtherCharacter { get; set; } = "";
        public int SharedParagraphs { get; set; }
    }

    public class OrphanDto
    {
        public string NodeId { get; set; } = "";
        public string Label { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public class ArcStepDto
    {
        public int ChapterSequence { get; set; }
        public int ParagraphSequence { get; set; }
        public string Content { get; set; } = "";
        public string LocationName { get; set; } = "";
    }

    public class LocationEventDto
    {
        public int ChapterSequence { get; set; }
        public int ParagraphSequence { get; set; }
        public List<string> Characters { get; set; } = new();
        public string Content { get; set; } = "";
    }

    public class GraphSummaryDto
    {
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public int CharacterCount { get; set; }
        public int LocationCount { get; set; }
        public int TimelineCount { get; set; }
        public int ChapterCount { get; set; }
        public int ParagraphCount { get; set; }
    }

    public class StoryDetailsDto
    {
        public string Title { get; set; } = "";
        public string Synopsis { get; set; } = "";
        public string Style { get; set; } = "";
        public string Theme { get; set; } = "";
        public int CharacterCount { get; set; }
        public int LocationCount { get; set; }
        public int TimelineCount { get; set; }
        public int ChapterCount { get; set; }
        public int ParagraphCount { get; set; }
    }

    public class AttributeDto
    {
        public string AttributeType { get; set; } = "";
        public string Description { get; set; } = "";
        public string TimelineName { get; set; } = "";
    }

    public class TimelineContextDto
    {
        public string TimelineName { get; set; } = "";
        public string Description { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public List<TimelineCharacterDto> Characters { get; set; } = new();
        public List<TimelineLocationDto> Locations { get; set; } = new();
        public List<TimelineEventDto> Events { get; set; } = new();
    }

    public class TimelineCharacterDto
    {
        public string Name { get; set; } = "";
        public List<AttributeDto> Attributes { get; set; } = new();
    }

    public class TimelineLocationDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class TimelineEventDto
    {
        public int ChapterSequence { get; set; }
        public int ParagraphSequence { get; set; }
        public string ChapterName { get; set; } = "";
        public List<string> Characters { get; set; } = new();
        public string LocationName { get; set; } = "";
        public string Content { get; set; } = "";
    }
}
