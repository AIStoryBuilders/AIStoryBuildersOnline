using System.Text.RegularExpressions;

namespace AIStoryBuilders.Services
{
    public static class TextSanitiser
    {
        public const int MaxEmbeddingChars = 1500;

        /// <summary>
        /// Strips invisible Unicode characters, normalises whitespace, and removes control characters.
        /// Returns the cleaned text and whether it was truncated to MaxEmbeddingChars.
        /// </summary>
        public static (string Cleaned, bool WasTruncated) Sanitise(string raw)
        {
            string text = Clean(raw);

            bool wasTruncated = false;
            if (text.Length > MaxEmbeddingChars)
            {
                text = text.Substring(0, MaxEmbeddingChars);
                wasTruncated = true;
            }

            return (text, wasTruncated);
        }

        /// <summary>
        /// Strips invisible Unicode characters, normalises whitespace, and removes control characters.
        /// Does NOT truncate. Use this for full-document cleaning.
        /// </summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw ?? "";

            string text = raw;

            // Remove zero-width spaces
            text = text.Replace("\u200B", "");
            text = text.Replace("\u200C", "");
            text = text.Replace("\u200D", "");
            text = text.Replace("\uFEFF", "");

            // Remove soft hyphens
            text = text.Replace("\u00AD", "");

            // Replace non-breaking spaces with regular spaces
            text = text.Replace("\u00A0", " ");

            // Remove other control characters (except newline/carriage return/tab)
            text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            // Normalise consecutive whitespace on same line
            text = Regex.Replace(text, @"[^\S\n]+", " ");

            // Normalise more than two consecutive blank lines to two
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }
    }
}
