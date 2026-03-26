namespace AIStoryBuilders.Models
{
    /// <summary>
    /// Cache entry for storing model lists in Blazored.LocalStorage.
    /// </summary>
    public class ModelCacheEntry
    {
        public List<string> Models { get; set; }
        public DateTime CachedAt { get; set; }
    }
}
