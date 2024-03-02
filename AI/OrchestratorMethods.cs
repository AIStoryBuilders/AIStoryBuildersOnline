using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using AIStoryBuilders.Services;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Files;
using OpenAI.FineTuning;
using OpenAI.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        public event EventHandler<ReadTextEventArgs> ReadTextEvent;
        public SettingsService SettingsService { get; set; }
        public LogService LogService { get; set; }
        public DatabaseService DatabaseService { get; set; }
        public string Summary { get; set; }

        public List<(string, float)> similarities = new List<(string, float)>();

        public Dictionary<string, string> AIStoryBuildersMemory = new Dictionary<string, string>();
        public HttpClient HttpClient { get; set; }

        // Constructor
        public OrchestratorMethods(SettingsService _SettingsService, LogService _LogService, DatabaseService _DatabaseService, HttpClient _HttpClient)
        {
            SettingsService = _SettingsService;
            LogService = _LogService;
            DatabaseService = _DatabaseService;
            HttpClient = _HttpClient;
        }

        // Memory and Vectors

        #region public async Task<string> GetVectorEmbedding(string EmbeddingContent, bool Combine)
        public async Task<string> GetVectorEmbedding(string EmbeddingContent, bool Combine)
        {
            // **** Call OpenAI and get embeddings for the memory text
            // Create an instance of the OpenAI client
            var api = new OpenAIClient(new OpenAIAuthentication(SettingsService.ApiKey), client: HttpClient);
            // Get the model details
            var model = await api.ModelsEndpoint.GetModelDetailsAsync("text-embedding-ada-002");
            // Get embeddings for the text
            var embeddings = await api.EmbeddingsEndpoint.CreateEmbeddingAsync(EmbeddingContent, model);
            // Get embeddings as an array of floats
            var EmbeddingVectors = embeddings.Data[0].Embedding.Select(d => (float)d).ToArray();
            // Loop through the embeddings
            List<VectorData> AllVectors = new List<VectorData>();
            for (int i = 0; i < EmbeddingVectors.Length; i++)
            {
                var embeddingVector = new VectorData
                {
                    VectorValue = EmbeddingVectors[i]
                };
                AllVectors.Add(embeddingVector);
            }
            // Convert the floats to a single string
            var VectorsToSave = "[" + string.Join(",", AllVectors.Select(x => x.VectorValue)) + "]";

            if (Combine)
            {
                return EmbeddingContent + "|" + VectorsToSave;
            }
            else
            {
                return VectorsToSave;
            }
        }
        #endregion

        #region public async Task<string> GetVectorEmbeddingAsFloats(string EmbeddingContent)
        public async Task<float[]> GetVectorEmbeddingAsFloats(string EmbeddingContent)
        {
            // **** Call OpenAI and get embeddings for the memory text
            // Create an instance of the OpenAI client
            var api = new OpenAIClient(new OpenAIAuthentication(SettingsService.ApiKey), client: HttpClient);
            // Get the model details
            var model = await api.ModelsEndpoint.GetModelDetailsAsync("text-embedding-ada-002");
            // Get embeddings for the text
            var embeddings = await api.EmbeddingsEndpoint.CreateEmbeddingAsync(EmbeddingContent, model);
            // Get embeddings as an array of floats
            var EmbeddingVectors = embeddings.Data[0].Embedding.Select(d => (float)d).ToArray();

            return EmbeddingVectors;
        }
        #endregion

        // Utility Methods

        #region public float CosineSimilarity(float[] vector1, float[] vector2)
        public float CosineSimilarity(float[] vector1, float[] vector2)
        {
            // Initialize variables for dot product and
            // magnitudes of the vectors
            float dotProduct = 0;
            float magnitude1 = 0;
            float magnitude2 = 0;

            // Iterate through the vectors and calculate
            // the dot product and magnitudes
            for (int i = 0; i < vector1?.Length; i++)
            {
                // Calculate dot product
                dotProduct += vector1[i] * vector2[i];

                // Calculate squared magnitude of vector1
                magnitude1 += vector1[i] * vector1[i];

                // Calculate squared magnitude of vector2
                magnitude2 += vector2[i] * vector2[i];
            }

            // Take the square root of the squared magnitudes
            // to obtain actual magnitudes
            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            // Calculate and return cosine similarity by dividing
            // dot product by the product of magnitudes
            return dotProduct / (magnitude1 * magnitude2);
        }
        #endregion

        #region private string CombineAndSortLists(string paramExistingList, string paramNewList)
        private string CombineAndSortLists(string paramExistingList, string paramNewList)
        {
            // Split the lists into an arrays
            string[] ExistingListArray = paramExistingList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] NewListArray = paramNewList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Combine the lists
            string[] CombinedListArray = ExistingListArray.Concat(NewListArray).ToArray();

            // Remove duplicates
            CombinedListArray = CombinedListArray.Distinct().ToArray();

            // Sort the array
            Array.Sort(CombinedListArray);

            // Combine the array into a string
            string CombinedList = string.Join("\n", CombinedListArray);

            return CombinedList;
        }
        #endregion

        #region public static string TrimToMaxWords(string input, int maxWords = 500)
        public static string TrimToMaxWords(string input, int maxWords = 500)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] words = input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length <= maxWords)
                return input;

            return string.Join(" ", words.Take(maxWords));
        }
        #endregion

        #region public bool IsValidFolderName(string folderName)
        public bool IsValidFolderName(string folderName)
        {
            string invalidChars = @"\/:*?""<>|";
            Regex containsABadCharacter = new Regex("[" + Regex.Escape(invalidChars) + "]");
            if (containsABadCharacter.IsMatch(folderName))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion

        #region public static string TrimInnerSpaces(string input)
        public static string TrimInnerSpaces(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return Regex.Replace(input, @"\s{2,}", " ");
        }
        #endregion

        #region public string SanitizeFileName(string input)
        public string SanitizeFileName(string input)
        {
            // Remove the | character
            input = input.Replace("|", "");

            return input;
        }
        #endregion

        #region public static List<string> ParseStringToList(string input)
        public static List<string> ParseStringToList(string input)
        {
            // Remove the brackets and split the string by comma
            string[] items = Regex.Replace(input, @"[\[\]]", "").Split(',');

            // Convert the array to a List<string> and return
            return new List<string>(items);
        }
        #endregion

        #region public class ReadTextEventArgs : EventArgs
        public class ReadTextEventArgs : EventArgs
        {
            public string Message { get; set; }
            public int DisplayLength { get; set; }

            public ReadTextEventArgs(string message, int display_length)
            {
                Message = message;
                DisplayLength = display_length;
            }
        }
        #endregion

        #region public string ConvertDateToLongDateString(DateTime paramDate)
        public static string ConvertDateToLongDateString(DateTime? paramDate)
        {
            string response = "";

            if(paramDate.HasValue)
            {
                response = paramDate.Value.ToShortDateString() + " " + paramDate.Value.ToShortTimeString();
            }

            return response;
        }
        #endregion
    }
}