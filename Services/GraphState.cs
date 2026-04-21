using AIStoryBuilders.Models;

namespace AIStoryBuilders.Services
{
    /// <summary>
    /// Process-wide cache for the currently loaded story graph.
    /// Single-user Blazor WASM: acceptable as static.
    /// </summary>
    public static class GraphState
    {
        public static StoryGraph Current { get; set; }
        public static Story CurrentStory { get; set; }
        public static bool IsDirty { get; set; } = true;

        public static void MarkDirty()
        {
            IsDirty = true;
        }

        public static void Clear()
        {
            Current = null;
            CurrentStory = null;
            IsDirty = true;
        }
    }
}
