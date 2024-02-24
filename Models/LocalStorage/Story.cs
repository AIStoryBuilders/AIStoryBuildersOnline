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
        public List<string> descriptions { get; set; }
    }

    public class Timelines
    {
        public string name { get; set; }
        public string description { get; set; }
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
}