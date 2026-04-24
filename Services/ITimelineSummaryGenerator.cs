using AIStoryBuilders.Models;

namespace AIStoryBuilders.Services
{
    public interface ITimelineSummaryGenerator
    {
        string GenerateSummary(TimelineContextDto context, int maxWords = 800);
    }
}
