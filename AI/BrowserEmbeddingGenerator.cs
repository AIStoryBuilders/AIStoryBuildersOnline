using Microsoft.JSInterop;
using AIStoryBuilders.Model;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Generates text embeddings locally in the browser using
    /// ONNX Runtime Web (all-MiniLM-L6-v2 model).
    /// Produces 384-dimensional normalized vectors.
    /// </summary>
    public class BrowserEmbeddingGenerator
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly LogService _logService;
        private bool _initialized = false;

        /// <summary>
        /// The output dimensionality of the embedding model.
        /// </summary>
        public const int VectorDimension = 384;

        public bool IsInitialized => _initialized;

        public BrowserEmbeddingGenerator(
            IJSRuntime jsRuntime,
            LogService logService)
        {
            _jsRuntime = jsRuntime;
            _logService = logService;
        }

        /// <summary>
        /// Initializes the ONNX model and tokenizer in the browser.
        /// Safe to call multiple times — subsequent calls are no-ops
        /// (guarded on the JS side).
        /// First call downloads ~23 MB model + ~226 KB vocab.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                await _jsRuntime.InvokeVoidAsync(
                    "EmbeddingEngine.initialize");
                _initialized = true;

                await _logService.WriteToLogAsync(
                    "BrowserEmbeddingGenerator: ONNX model initialized.");
            }
            catch (Exception ex)
            {
                await _logService.WriteToLogAsync(
                    $"BrowserEmbeddingGenerator: Init failed — {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generate a 384-d normalized embedding for a single text.
        /// Automatically initializes the model on first call.
        /// </summary>
        /// <param name="text">The text to embed.</param>
        /// <returns>A 384-element float array.</returns>
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return new float[VectorDimension]; // zero vector
            }

            try
            {
                // JS returns number[] which deserializes to float[]
                var embedding = await _jsRuntime
                    .InvokeAsync<float[]>(
                        "EmbeddingEngine.generateEmbedding",
                        text);

                return embedding;
            }
            catch (Exception ex)
            {
                await _logService.WriteToLogAsync(
                    $"BrowserEmbeddingGenerator: Embedding failed — {ex.Message}");
                return new float[VectorDimension]; // fallback zero vector
            }
        }

        /// <summary>
        /// Generate embeddings for multiple texts.
        /// Processes sequentially to avoid overwhelming the browser thread.
        /// </summary>
        public async Task<float[][]> GenerateEmbeddingsAsync(string[] texts)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                var results = await _jsRuntime
                    .InvokeAsync<float[][]>(
                        "EmbeddingEngine.generateEmbeddings",
                        (object)texts);

                return results;
            }
            catch (Exception ex)
            {
                await _logService.WriteToLogAsync(
                    $"BrowserEmbeddingGenerator: Batch embedding failed — {ex.Message}");
                // Return zero vectors as fallback
                return texts.Select(_ => new float[VectorDimension]).ToArray();
            }
        }
    }
}
