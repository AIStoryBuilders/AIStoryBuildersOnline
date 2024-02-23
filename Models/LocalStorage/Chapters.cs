namespace AIStoryBuilders.Models.LocalStorage
{
    public class Chapters
    {
        public List<Chapter> chapter { get; set; }
    }
    public class Chapter
    {
        public string chapter_name { get; set; }
        public string chapter_synopsis { get; set; }
        public List<Paragraphs> paragraphs { get; set; }
    }

    public class Paragraphs
    {
        public int sequence { get; set; }
        public string contents { get; set; }
        public string location_name { get; set; }
        public string timeline_name { get; set; }
        public List<string> character_names { get; set; }
    }
}
