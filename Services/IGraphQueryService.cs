using AIStoryBuilders.Models;
using System.Collections.Generic;

namespace AIStoryBuilders.Services
{
    public interface IGraphQueryService
    {
        CharacterDto GetCharacter(string name);
        List<CharacterDto> ListCharacters();
        LocationDto GetLocation(string name);
        List<LocationDto> ListLocations();
        TimelineDto GetTimeline(string name);
        List<TimelineDto> ListTimelines();
        ChapterDto GetChapter(string title);
        List<ChapterDto> ListChapters();
        ParagraphDto GetParagraph(string chapter, int index);
        IReadOnlyList<ParagraphDto> ListParagraphs(string chapter);
        ParagraphTextDto GetParagraphText(string chapter, int index);
        List<RelationshipDto> GetRelationships(string name);
        List<AppearanceDto> GetAppearances(string characterName);
        List<LocationUsageDto> GetLocationUsage(string locationName);
        List<InteractionDto> GetInteractions(string characterName);
        List<OrphanDto> FindOrphans();
        List<ArcStepDto> GetCharacterArc(string characterName);
        List<LocationEventDto> GetLocationEvents(string locationName);
        GraphSummaryDto GetGraphSummary();
        StoryDetailsDto GetStoryDetails();
        List<AttributeDto> ListAttributes(string parentType, string parentName);
        TimelineContextDto GetTimelineContext(string timelineName, int chapterSequence, int paragraphSequence);
    }
}
