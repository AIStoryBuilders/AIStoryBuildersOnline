using AIStoryBuilders.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AIStoryBuilders.Services
{
    public class TimelineSummaryGenerator : ITimelineSummaryGenerator
    {
        public string GenerateSummary(TimelineContextDto context, int maxWords = 800)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.TimelineName)) return "";
            if ((context.Characters == null || context.Characters.Count == 0) &&
                (context.Locations == null || context.Locations.Count == 0) &&
                (context.Events == null || context.Events.Count == 0))
            {
                return "";
            }

            var sb = new StringBuilder();
            string dateRange = "";
            if (!string.IsNullOrEmpty(context.StartDate) || !string.IsNullOrEmpty(context.EndDate))
            {
                dateRange = $" ({context.StartDate} to {context.EndDate})";
            }
            sb.AppendLine($"Timeline: {context.TimelineName}{dateRange}");
            if (!string.IsNullOrWhiteSpace(context.Description)) sb.AppendLine(context.Description);

            if (context.Characters?.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Characters active in this timeline:");
                foreach (var c in context.Characters)
                {
                    var attrText = string.Join("; ", c.Attributes?.Take(3).Select(a =>
                        string.IsNullOrEmpty(a.AttributeType) ? a.Description : $"{a.AttributeType}: {a.Description}")
                        ?? Enumerable.Empty<string>());
                    sb.AppendLine($"- {c.Name}: {attrText}");
                }
            }

            if (context.Locations?.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Locations in this timeline:");
                foreach (var l in context.Locations)
                {
                    sb.AppendLine($"- {l.Name}: {l.Description}");
                }
            }

            if (context.Events?.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Events (chronological):");
                var lines = new List<string>();
                foreach (var e in context.Events)
                {
                    string who = e.Characters != null && e.Characters.Count > 0 ? string.Join(", ", e.Characters) : "";
                    string where = string.IsNullOrEmpty(e.LocationName) ? "" : $" at {e.LocationName}";
                    string what = string.IsNullOrEmpty(e.Content) ? "" : ": " + Truncate(e.Content, 160);
                    lines.Add($"- Chapter {e.ChapterSequence}, P{e.ParagraphSequence}: {who}{where}{what}");
                }

                // Apply word budget; drop oldest first
                var currentWords = CountWords(sb.ToString());
                var kept = new LinkedList<string>(lines);
                int dropped = 0;
                while (kept.Count > 0)
                {
                    int totalWords = currentWords + kept.Sum(s => CountWords(s));
                    if (totalWords <= maxWords) break;
                    kept.RemoveFirst();
                    dropped++;
                }

                if (dropped > 0) sb.AppendLine($"- ... and {dropped} earlier events");
                foreach (var line in kept) sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static int CountWords(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return s.Split(new[] { ' ', '\t', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
