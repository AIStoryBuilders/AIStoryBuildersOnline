/**
 * Local browser embedding engine using ONNX Runtime Web.
 * Loads the all-MiniLM-L6-v2 model (quantized) and produces
 * 384-dimensional normalized embedding vectors.
 */
import { WordPieceTokenizer } from './tokenizer.js';

let session = null;
let tokenizer = null;
let initialized = false;
let ortModule = null;

/**
 * Initialize ONNX Runtime and load the model + tokenizer.
 * Called once on first use. Subsequent calls are no-ops.
 */
export async function initializeEmbeddingModel() {
    if (initialized) return;

    // 1. Load ONNX Runtime Web (WASM backend)
    ortModule = await import(
        'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.21.0/dist/ort.min.mjs'
    );

    // Configure WASM paths
    ortModule.env.wasm.wasmPaths =
        'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.21.0/dist/';

    // 2. Load the ONNX model
    session = await ortModule.InferenceSession.create(
        './models/all-MiniLM-L6-v2/model_quantized.onnx',
        { executionProviders: ['wasm'] }
    );

    // 3. Load the tokenizer vocabulary
    tokenizer = new WordPieceTokenizer();
    await tokenizer.load('./models/all-MiniLM-L6-v2/vocab.txt');

    initialized = true;
}

/**
 * Generate a 384-d embedding for the given text.
 * @param {string} text - Input text to embed
 * @returns {Promise<number[]>} - 384-dimensional normalized embedding vector
 */
export async function generateEmbedding(text) {
    await initializeEmbeddingModel();

    const ort = ortModule;

    // Tokenize
    const { inputIds, attentionMask, tokenTypeIds } = tokenizer.tokenize(text);

    // Derive sequence length from the tokenizer to stay in sync
    const seqLen = tokenizer.maxLength;

    // Create tensors [1, seqLen] (batch size = 1)
    const inputIdsTensor = new ort.Tensor('int64', inputIds, [1, seqLen]);
    const attentionMaskTensor = new ort.Tensor('int64', attentionMask, [1, seqLen]);
    const tokenTypeIdsTensor = new ort.Tensor('int64', tokenTypeIds, [1, seqLen]);

    // Run inference
    const feeds = {
        input_ids: inputIdsTensor,
        attention_mask: attentionMaskTensor,
        token_type_ids: tokenTypeIdsTensor
    };

    const results = await session.run(feeds);

    // Extract last_hidden_state: shape [1, seqLen, 384]
    const lastHiddenState = results['last_hidden_state'].data; // Float32Array
    const hiddenDim = 384;

    // Mean pooling (masked)
    const embedding = new Float32Array(hiddenDim);
    let maskSum = 0;

    for (let i = 0; i < seqLen; i++) {
        const mask = Number(attentionMask[i]); // 0 or 1
        maskSum += mask;
        for (let j = 0; j < hiddenDim; j++) {
            embedding[j] += lastHiddenState[i * hiddenDim + j] * mask;
        }
    }

    // Divide by mask sum
    if (maskSum > 0) {
        for (let j = 0; j < hiddenDim; j++) {
            embedding[j] /= maskSum;
        }
    }

    // L2 normalize
    let norm = 0;
    for (let j = 0; j < hiddenDim; j++) {
        norm += embedding[j] * embedding[j];
    }
    norm = Math.sqrt(norm);
    if (norm > 0) {
        for (let j = 0; j < hiddenDim; j++) {
            embedding[j] /= norm;
        }
    }

    // Return as a plain number array (for JS interop)
    return Array.from(embedding);
}

/**
 * Generate embeddings for multiple texts in sequence.
 * @param {string[]} texts - Array of input texts
 * @returns {Promise<number[][]>} - Array of 384-d embedding vectors
 */
export async function generateEmbeddings(texts) {
    const results = [];
    for (const text of texts) {
        results.push(await generateEmbedding(text));
    }
    return results;
}
