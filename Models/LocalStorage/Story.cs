namespace AIStoryBuilders.Models.LocalStorage
{
    public class Story
    {
        public List<Locations> locations { get; set; }
        public List<Timelines> timelines { get; set; }
        public List<Character> characters { get; set; }
    }

    public class Locations
    {
        public string name { get; set; }
        public List<Descriptions> descriptions { get; set; }
    }

    public class Timelines
    {
        public string name { get; set; }
        public string description { get; set; }
        public string StartDate { get; set; }
        public string StopDate { get; set; }
    }

    public class Character
    {
        public string name { get; set; }
        public List<Descriptions> descriptions { get; set; }
    }

    public class Descriptions
    {
        public string description_type { get; set; }
        public List<string> _enum { get; set; }
        public string description { get; set; }
        public string timeline_name { get; set; }
        public string embedding { get; set; }
    }

    public class Chapter
    {
        public string chapter_name { get; set; }
        public string chapter_synopsis { get; set; }
        public string sequence { get; set; }
        public string embedding { get; set; }
        public List<Paragraphs> paragraphs { get; set; }
    }

    public class Paragraphs
    {
        public int sequence { get; set; }
        public string contents { get; set; }
        public string location_name { get; set; }
        public string timeline_name { get; set; }
        public string embedding { get; set; }
        public string character_names { get; set; }
    }
}