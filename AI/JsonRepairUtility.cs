using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Zero-cost, deterministic regex pipeline for repairing malformed LLM JSON output.
    /// </summary>
    public static class JsonRepairUtility
    {
        /// <summary>
        /// Extracts and repairs JSON from raw LLM output that may contain markdown fences,
        /// trailing commas, unescaped newlines, etc.
        /// </summary>
        public static string ExtractAndRepair(string rawLlmOutput)
        {
            if (string.IsNullOrWhiteSpace(rawLlmOutput))
                return rawLlmOutput;

            string json = rawLlmOutput;

            // Step 1: Strip Markdown code fences
            json = StripMarkdownFences(json);

            // Step 2: Isolate JSON block (find matching outermost { } or [ ])
            json = IsolateJsonBlock(json);

            // Step 3: Fix trailing commas before } or ]
            json = FixTrailingCommas(json);

            // Step 4: Fix unescaped newlines inside string values
            json = FixUnescapedNewlines(json);

            // Step 5: Validate — attempt to parse
            try
            {
                JToken.Parse(json);
                return json;
            }
            catch
            {
                // Return best-effort string
                return json;
            }
        }

        private static string StripMarkdownFences(string input)
        {
            // Remove ```json ... ``` or ``` ... ```
            var fencePattern = new Regex(@"```(?:json|JSON)?\s*\n?(.*?)\n?\s*```", RegexOptions.Singleline);
            var match = fencePattern.Match(input);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return input.Trim();
        }

        private static string IsolateJsonBlock(string input)
        {
            // Find the first { or [ and last } or ]
            int firstBrace = input.IndexOf('{');
            int firstBracket = input.IndexOf('[');

            int startIndex = -1;
            char openChar = '{';
            char closeChar = '}';

            if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
            {
                startIndex = firstBrace;
                openChar = '{';
                closeChar = '}';
            }
            else if (firstBracket >= 0)
            {
                startIndex = firstBracket;
                openChar = '[';
                closeChar = ']';
            }

            if (startIndex < 0)
                return input;

            // Find matching close
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = startIndex; i < input.Length; i++)
            {
                char c = input[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == openChar)
                        depth++;
                    else if (c == closeChar)
                    {
                        depth--;
                        if (depth == 0)
                            return input.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }

            // If we couldn't find matching close, return from start to end
            return input.Substring(startIndex);
        }

        private static string FixTrailingCommas(string input)
        {
            // Remove commas before } or ]
            return Regex.Replace(input, @",\s*([}\]])", "$1");
        }

        private static string FixUnescapedNewlines(string input)
        {
            // Fix unescaped newlines inside JSON string values
            // This is a simplified approach: replace literal newlines between quotes
            var result = new System.Text.StringBuilder();
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (escaped)
                {
                    result.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    result.Append(c);
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    result.Append(c);
                    continue;
                }

                if (inString && (c == '\n' || c == '\r'))
                {
                    if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n')
                    {
                        result.Append("\\n");
                        i++; // Skip the \n after \r
                    }
                    else
                    {
                        result.Append("\\n");
                    }
                    continue;
                }

                result.Append(c);
            }

            return result.ToString();
        }
    }
}
