using AIStoryBuilders.AI;
using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using System.Text;
using System.Text.RegularExpressions;
using Xceed.Document.NET;
using Xceed.Words.NET;
using UglyToad.PdfPig;
using Paragraph = AIStoryBuilders.Models.Paragraph;

namespace AIStoryBuilders.Services
{
    public class ManuscriptParsingService
    {
        private readonly OrchestratorMethods _orchestrator;
        private readonly LogService _logService;

        public ManuscriptParsingService(
            OrchestratorMethods orchestrator,
            LogService logService)
        {
            _orchestrator = orchestrator;
            _logService = logService;
        }

        /// <summary>
        /// Parses a manuscript file stream into a fully structured Story object.
        /// </summary>
        public async Task<Story> ParseManuscriptAsync(
            Stream fileStream,
            string fileName,
            IProgress<int> progress,
            IProgress<string> statusProgress)
        {
            // Phase 1: Text Extraction
            statusProgress?.Report("Extracting text...");
            progress?.Report(5);

            string rawText = await ExtractTextFromFile(fileStream, fileName);
            string cleanText = TextSanitiser.Clean(rawText);

            if (string.IsNullOrWhiteSpace(cleanText))
                throw new InvalidOperationException("The file appears to be empty or could not be read.");

            await _logService.WriteToLogAsync($"ManuscriptParsing: Extracted {cleanText.Length} characters from {fileName}");
            progress?.Report(10);

            // Phase 2: Structure Splitting
            statusProgress?.Report("Detecting chapter boundaries...");

            // Try fast regex-based detection first (handles "Chapter 1", "CHAPTER ONE", etc.)
            var chapters = RegexSplitIntoChapters(cleanText);
            await _logService.WriteToLogAsync($"ManuscriptParsing: Regex detected {chapters.Count} chapters");

            if (chapters.Count <= 1)
            {
                // Regex found no explicit headings — fall back to LLM-based detection
                statusProgress?.Report("No chapter headings found, using AI detection...");
                var sentences = SplitIntoSentences(cleanText);
                await _logService.WriteToLogAsync($"ManuscriptParsing: Split into {sentences.Count} sentences");
                var chunks = ChunkSentences(sentences, 100);

                List<ChapterBoundary> boundaries;
                try
                {
                    boundaries = await DetectChapterBoundaries(chunks, statusProgress);
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync($"ManuscriptParsing: Chapter detection failed: {ex.Message}. Using single chapter fallback.");
                    boundaries = new List<ChapterBoundary>();
                }

                chapters = SplitTextIntoChapters(cleanText, sentences, boundaries);
            }

            await _logService.WriteToLogAsync($"ManuscriptParsing: Identified {chapters.Count} chapters");
            progress?.Report(25);
            progress?.Report(30);

            statusProgress?.Report("Splitting chapters into paragraphs...");
            foreach (var ch in chapters)
            {
                ch.Paragraphs = SplitIntoParagraphs(ch.RawText);
            }
            progress?.Report(35);

            // Phase 3: AI Entity Extraction
            int chapterIndex = 0;
            int totalChapters = chapters.Count;

            foreach (var ch in chapters)
            {
                chapterIndex++;
                int progressBase = 35 + (int)(45.0 * chapterIndex / totalChapters);

                statusProgress?.Report($"Summarizing chapter {chapterIndex} of {totalChapters}...");

                try
                {
                    ch.Summary = await _orchestrator.SummarizeChapterAsync(ch.RawText);
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync($"ManuscriptParsing: Summary failed for {ch.Title}: {ex.Message}");
                    ch.Summary = "";
                }

                try
                {
                    ch.Synopsis = await _orchestrator.ExtractBeatsAsync(ch.Summary);
                }
                catch
                {
                    ch.Synopsis = ch.Summary;
                }

                statusProgress?.Report($"Extracting characters from chapter {chapterIndex}...");
                try
                {
                    ch.Characters = await _orchestrator.ExtractCharactersFromSummaryAsync(ch.Summary, ch.Title);
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync($"ManuscriptParsing: Character extraction failed for {ch.Title}: {ex.Message}");
                    ch.Characters = new List<ParsedCharacter>();
                }

                statusProgress?.Report($"Extracting locations from chapter {chapterIndex}...");
                try
                {
                    ch.Locations = await _orchestrator.ExtractLocationsFromSummaryAsync(ch.Summary, ch.Title);
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync($"ManuscriptParsing: Location extraction failed for {ch.Title}: {ex.Message}");
                    ch.Locations = new List<ParsedLocation>();
                }

                statusProgress?.Report($"Extracting timelines from chapter {chapterIndex}...");
                try
                {
                    ch.Timelines = await _orchestrator.ExtractTimelinesFromSummaryAsync(ch.Summary, ch.Title, chapterIndex);
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync($"ManuscriptParsing: Timeline extraction failed for {ch.Title}: {ex.Message}");
                    ch.Timelines = new List<ParsedTimeline>();
                }

                statusProgress?.Report($"Associating entities for chapter {chapterIndex}...");
                try
                {
                    ch.ParagraphAssociations = await _orchestrator.AssociateParagraphEntitiesAsync(
                        ch.Paragraphs, ch.Characters, ch.Locations, ch.Timelines);
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync($"ManuscriptParsing: Entity association failed for {ch.Title}: {ex.Message}");
                    ch.ParagraphAssociations = new List<ParagraphAssociation>();
                }

                progress?.Report(progressBase);

                // Yield to UI
                await Task.Delay(1);
            }

            progress?.Report(82);

            // Phase 4: Story Assembly
            statusProgress?.Report("Assembling story...");
            var story = AssembleStory(fileName, chapters);

            progress?.Report(95);
            statusProgress?.Report("Story assembly complete.");

            return story;
        }

        #region Phase 1: Text Extraction

        private async Task<string> ExtractTextFromFile(Stream stream, string fileName)
        {
            string extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";

            switch (extension)
            {
                case ".docx":
                    return ExtractDocx(stream);
                case ".pdf":
                    return ExtractPdf(stream);
                case ".txt":
                case ".md":
                    return await ExtractPlainText(stream);
                default:
                    throw new InvalidOperationException(
                        $"Unsupported file type: '{extension}'. Supported types: .docx, .pdf, .txt, .md");
            }
        }

        private static string ExtractDocx(Stream stream)
        {
            // Copy to MemoryStream since DocX may need a seekable stream
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;

            using var doc = DocX.Load(ms);
            var sb = new StringBuilder();
            foreach (var para in doc.Paragraphs)
            {
                sb.AppendLine(para.Text);
            }
            return sb.ToString();
        }

        private static string ExtractPdf(Stream stream)
        {
            // PdfPig needs a byte array or seekable stream
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            using var document = PdfDocument.Open(bytes);
            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return sb.ToString();
        }

        private static async Task<string> ExtractPlainText(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        #endregion

        #region Phase 2: Sentence Splitting and Chunking

        private static List<string> SplitIntoSentences(string text)
        {
            // Split on sentence-ending punctuation followed by whitespace and an uppercase letter
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+(?=[A-Z])");
            return sentences
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static List<List<string>> ChunkSentences(List<string> sentences, int chunkSize)
        {
            var chunks = new List<List<string>>();
            for (int i = 0; i < sentences.Count; i += chunkSize)
            {
                chunks.Add(sentences.Skip(i).Take(chunkSize).ToList());
            }
            return chunks;
        }

        /// <summary>
        /// Fast, deterministic chapter detection using regex patterns.
        /// Handles: "Chapter 1", "Chapter One", "CHAPTER I", "Chapter 1: Title",
        /// "Chapter 1 - Title", and similar variations on their own line.
        /// </summary>
        private static List<ParsedChapter> RegexSplitIntoChapters(string fullText)
        {
            // Match lines that look like chapter headings.
            // Pattern: line starts (after optional whitespace) with "Chapter" followed by
            // a number, roman numeral, or written number, optionally followed by a colon/dash and title.
            var chapterPattern = new Regex(
                @"^[ \t]*(?<heading>Chapter\s+(?:\d+|[IVXLCDM]+|One|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|" +
                @"Eleven|Twelve|Thirteen|Fourteen|Fifteen|Sixteen|Seventeen|Eighteen|Nineteen|Twenty" +
                @"(?:[- ](?:One|Two|Three|Four|Five|Six|Seven|Eight|Nine))?|" +
                @"Thirty(?:[- ](?:One|Two|Three|Four|Five|Six|Seven|Eight|Nine))?))" +
                @"(?:\s*[:\-–—]\s*[^\n]*)?[ \t]*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            var matches = chapterPattern.Matches(fullText);

            if (matches.Count < 2)
            {
                // No reliable chapter headings found (or only one) — return single chapter
                var single = new List<ParsedChapter>();
                single.Add(new ParsedChapter
                {
                    Title = "Chapter 1",
                    RawText = fullText.Trim()
                });
                return single;
            }

            var chapters = new List<ParsedChapter>();

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                string heading = match.Value.Trim();

                // Chapter text starts after the heading line
                int contentStart = match.Index + match.Length;

                // Chapter text ends at the next heading (or end of document)
                int contentEnd = (i + 1 < matches.Count) ? matches[i + 1].Index : fullText.Length;

                string chapterText = fullText.Substring(contentStart, contentEnd - contentStart).Trim();

                chapters.Add(new ParsedChapter
                {
                    Title = heading,
                    RawText = chapterText
                });
            }

            // If there is text before the first chapter heading, prepend it to the first chapter
            if (matches[0].Index > 0)
            {
                string preamble = fullText.Substring(0, matches[0].Index).Trim();
                if (!string.IsNullOrWhiteSpace(preamble) && chapters.Count > 0)
                {
                    chapters[0].RawText = preamble + "\n\n" + chapters[0].RawText;
                }
            }

            return chapters;
        }

        private async Task<List<ChapterBoundary>> DetectChapterBoundaries(
            List<List<string>> chunks, IProgress<string> statusProgress)
        {
            var allBoundaries = new List<ChapterBoundary>();

            for (int i = 0; i < chunks.Count; i++)
            {
                statusProgress?.Report($"Detecting chapters in chunk {i + 1} of {chunks.Count}...");
                string chunkText = string.Join(" ", chunks[i]);

                var boundaries = await _orchestrator.ParseChaptersAsync(chunkText);
                if (boundaries != null)
                {
                    allBoundaries.AddRange(boundaries);
                }

                await Task.Delay(1);
            }

            return allBoundaries;
        }

        private static List<ParsedChapter> SplitTextIntoChapters(
            string fullText, List<string> sentences, List<ChapterBoundary> boundaries)
        {
            var chapters = new List<ParsedChapter>();

            if (boundaries == null || boundaries.Count == 0)
            {
                // No chapters detected — treat entire text as one chapter
                chapters.Add(new ParsedChapter
                {
                    Title = "Chapter 1",
                    RawText = StripChapterHeading(fullText, "Chapter 1")
                });
                return chapters;
            }

            // Find the positions of each boundary in the full text
            var boundaryPositions = new List<(string Title, int Position)>();
            foreach (var b in boundaries)
            {
                if (string.IsNullOrWhiteSpace(b.FirstSentence))
                    continue;

                int pos = fullText.IndexOf(b.FirstSentence, StringComparison.Ordinal);
                if (pos < 0)
                {
                    // Try partial match (first 50 chars)
                    string partial = b.FirstSentence.Length > 50 ? b.FirstSentence.Substring(0, 50) : b.FirstSentence;
                    pos = fullText.IndexOf(partial, StringComparison.Ordinal);
                }

                if (pos >= 0)
                {
                    boundaryPositions.Add((b.Title ?? $"Chapter {boundaryPositions.Count + 1}", pos));
                }
            }

            if (boundaryPositions.Count == 0)
            {
                chapters.Add(new ParsedChapter
                {
                    Title = "Chapter 1",
                    RawText = StripChapterHeading(fullText, "Chapter 1")
                });
                return chapters;
            }

            // Sort by position
            boundaryPositions = boundaryPositions.OrderBy(bp => bp.Position).ToList();

            // Extract text between boundaries
            for (int i = 0; i < boundaryPositions.Count; i++)
            {
                int start = boundaryPositions[i].Position;
                int end = (i + 1 < boundaryPositions.Count) ? boundaryPositions[i + 1].Position : fullText.Length;
                string chapterText = fullText.Substring(start, end - start).Trim();

                chapters.Add(new ParsedChapter
                {
                    Title = boundaryPositions[i].Title,
                    RawText = StripChapterHeading(chapterText, boundaryPositions[i].Title)
                });
            }

            // If there's content before the first boundary, prepend it to first chapter
            if (boundaryPositions[0].Position > 0)
            {
                string preamble = fullText.Substring(0, boundaryPositions[0].Position).Trim();
                if (!string.IsNullOrWhiteSpace(preamble) && chapters.Count > 0)
                {
                    chapters[0].RawText = preamble + "\n\n" + chapters[0].RawText;
                }
            }

            return chapters;
        }

        private static string StripChapterHeading(string text, string chapterTitle)
        {
            if (string.IsNullOrWhiteSpace(chapterTitle))
                return text;

            // Remove the chapter title from the beginning of the text
            string pattern = @"^\s*" + Regex.Escape(chapterTitle) + @"\s*\n?";
            text = Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase);

            // Also try common patterns like "Chapter 1: Title"
            text = Regex.Replace(text, @"^\s*Chapter\s+\d+[:\.\s]*[^\n]*\n?", "", RegexOptions.IgnoreCase);

            return text.TrimStart();
        }

        #endregion

        #region Phase 2b: Paragraph Splitting

        private List<string> SplitIntoParagraphs(string chapterText)
        {
            // Split on blank lines
            var rawParagraphs = Regex.Split(chapterText, @"\n\s*\n")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            var result = new List<string>();

            foreach (var para in rawParagraphs)
            {
                // Collapse internal line breaks into single spaces
                string collapsed = Regex.Replace(para, @"\s*\n\s*", " ").Trim();

                // Check for oversized paragraphs (over 500 words)
                int wordCount = collapsed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount > 500)
                {
                    // Heuristic split for oversized paragraphs
                    var subParagraphs = HeuristicSplit(collapsed, 250);
                    result.AddRange(subParagraphs);
                }
                else
                {
                    result.Add(collapsed);
                }
            }

            return result;
        }

        private static List<string> HeuristicSplit(string text, int targetWordCount)
        {
            var words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            var current = new StringBuilder();
            int count = 0;

            foreach (var word in words)
            {
                current.Append(word);
                current.Append(' ');
                count++;

                if (count >= targetWordCount)
                {
                    // Try to break at a sentence boundary
                    string segment = current.ToString().Trim();
                    int lastSentenceEnd = FindLastSentenceEnd(segment);
                    if (lastSentenceEnd > segment.Length / 2)
                    {
                        result.Add(segment.Substring(0, lastSentenceEnd + 1).Trim());
                        string remainder = segment.Substring(lastSentenceEnd + 1).Trim();
                        current.Clear();
                        current.Append(remainder);
                        current.Append(' ');
                        count = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    }
                    else if (count >= targetWordCount * 2)
                    {
                        // Force break
                        result.Add(segment);
                        current.Clear();
                        count = 0;
                    }
                }
            }

            if (current.Length > 0)
            {
                string remaining = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(remaining))
                    result.Add(remaining);
            }

            return result;
        }

        private static int FindLastSentenceEnd(string text)
        {
            int lastPeriod = text.LastIndexOf(". ");
            int lastExclamation = text.LastIndexOf("! ");
            int lastQuestion = text.LastIndexOf("? ");

            return Math.Max(lastPeriod, Math.Max(lastExclamation, lastQuestion));
        }

        #endregion

        #region Phase 4: Story Assembly

        private Story AssembleStory(string fileName, List<ParsedChapter> parsedChapters)
        {
            string storyTitle = Path.GetFileNameWithoutExtension(fileName);
            // Sanitize the title
            storyTitle = Regex.Replace(storyTitle, @"[\\/:*?""<>|]", "").Trim();
            if (string.IsNullOrWhiteSpace(storyTitle))
                storyTitle = "Imported Story";

            var story = new Story
            {
                Title = storyTitle,
                Style = "",
                Theme = "You are a software program that creates prose for novels.",
                Synopsis = "",
            };

            // Merge all characters across chapters (deduplicate by name)
            var allCharacters = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase);
            var allLocations = new Dictionary<string, Location>(StringComparer.OrdinalIgnoreCase);
            var allTimelines = new Dictionary<string, Timeline>(StringComparer.OrdinalIgnoreCase);

            int chapterSequence = 1;
            foreach (var pc in parsedChapters)
            {
                // Build characters
                if (pc.Characters != null)
                {
                    foreach (var parsedChar in pc.Characters)
                    {
                        if (string.IsNullOrWhiteSpace(parsedChar.Name))
                            continue;

                        string charName = parsedChar.Name.Trim();

                        if (!allCharacters.TryGetValue(charName, out var existingChar))
                        {
                            existingChar = new Character
                            {
                                CharacterName = charName,
                                CharacterBackground = new List<CharacterBackground>()
                            };
                            allCharacters[charName] = existingChar;
                        }

                        if (!string.IsNullOrWhiteSpace(parsedChar.Backstory))
                        {
                            existingChar.CharacterBackground.Add(new CharacterBackground
                            {
                                Type = "History",
                                Description = parsedChar.Backstory,
                                Timeline = new Timeline { TimelineName = pc.Title }
                            });
                        }
                    }
                }

                // Build locations
                if (pc.Locations != null)
                {
                    foreach (var parsedLoc in pc.Locations)
                    {
                        if (string.IsNullOrWhiteSpace(parsedLoc.Name))
                            continue;

                        string locName = parsedLoc.Name.Trim();

                        if (!allLocations.TryGetValue(locName, out var existingLoc))
                        {
                            existingLoc = new Location
                            {
                                LocationName = locName,
                                LocationDescription = new List<LocationDescription>()
                            };
                            allLocations[locName] = existingLoc;
                        }

                        if (!string.IsNullOrWhiteSpace(parsedLoc.Description))
                        {
                            existingLoc.LocationDescription.Add(new LocationDescription
                            {
                                Description = parsedLoc.Description,
                                Timeline = new Timeline { TimelineName = pc.Title }
                            });
                        }
                    }
                }

                // Build timelines
                if (pc.Timelines != null)
                {
                    foreach (var parsedTl in pc.Timelines)
                    {
                        if (string.IsNullOrWhiteSpace(parsedTl.Name))
                            continue;

                        string tlName = parsedTl.Name.Trim();

                        if (!allTimelines.ContainsKey(tlName))
                        {
                            allTimelines[tlName] = new Timeline
                            {
                                TimelineName = tlName,
                                TimelineDescription = parsedTl.Description ?? ""
                            };
                        }
                    }
                }

                // Build chapter
                var chapter = new Chapter
                {
                    ChapterName = $"Chapter{chapterSequence}",
                    Synopsis = pc.Synopsis ?? "",
                    Sequence = chapterSequence,
                    Story = story,
                    Paragraph = new List<Paragraph>()
                };

                int paraSequence = 1;
                if (pc.Paragraphs != null)
                {
                    foreach (var paraText in pc.Paragraphs)
                    {
                        var paragraph = new Paragraph
                        {
                            Sequence = paraSequence,
                            ParagraphContent = paraText,
                            Location = new Location { LocationName = "" },
                            Timeline = new Timeline { TimelineName = "" },
                            Characters = new List<Character>()
                        };

                        // Apply entity associations if available
                        if (pc.ParagraphAssociations != null)
                        {
                            var assoc = pc.ParagraphAssociations
                                .FirstOrDefault(a => a.Index == paraSequence - 1);

                            if (assoc != null)
                            {
                                if (!string.IsNullOrWhiteSpace(assoc.Location))
                                    paragraph.Location = new Location { LocationName = assoc.Location };

                                if (!string.IsNullOrWhiteSpace(assoc.Timeline))
                                    paragraph.Timeline = new Timeline { TimelineName = assoc.Timeline };

                                if (assoc.Characters != null)
                                {
                                    paragraph.Characters = assoc.Characters
                                        .Where(c => !string.IsNullOrWhiteSpace(c))
                                        .Select(c => new Character { CharacterName = c.Trim() })
                                        .ToList();
                                }
                            }
                        }

                        chapter.Paragraph.Add(paragraph);
                        paraSequence++;
                    }
                }

                story.Chapter.Add(chapter);
                chapterSequence++;
            }

            story.Character = allCharacters.Values.ToList();
            story.Location = allLocations.Values.ToList();
            story.Timeline = allTimelines.Values.ToList();

            // Generate synopsis from first chapter summary
            if (parsedChapters.Count > 0 && !string.IsNullOrWhiteSpace(parsedChapters[0].Summary))
            {
                story.Synopsis = OrchestratorMethods.TrimToMaxWords(parsedChapters[0].Summary, 200);
            }

            return story;
        }

        #endregion

        #region Internal Models

        public class ChapterBoundary
        {
            public string Title { get; set; }
            public string FirstSentence { get; set; }
        }

        public class ParsedChapter
        {
            public string Title { get; set; }
            public string RawText { get; set; }
            public string Summary { get; set; }
            public string Synopsis { get; set; }
            public List<string> Paragraphs { get; set; } = new List<string>();
            public List<ParsedCharacter> Characters { get; set; } = new List<ParsedCharacter>();
            public List<ParsedLocation> Locations { get; set; } = new List<ParsedLocation>();
            public List<ParsedTimeline> Timelines { get; set; } = new List<ParsedTimeline>();
            public List<ParagraphAssociation> ParagraphAssociations { get; set; } = new List<ParagraphAssociation>();
        }

        public class ParsedCharacter
        {
            public string Name { get; set; }
            public string Backstory { get; set; }
        }

        public class ParsedLocation
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public class ParsedTimeline
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public class ParagraphAssociation
        {
            public int Index { get; set; }
            public string Location { get; set; }
            public string Timeline { get; set; }
            public List<string> Characters { get; set; }
        }

        #endregion
    }
}
