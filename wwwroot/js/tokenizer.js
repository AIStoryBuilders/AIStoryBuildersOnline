/**
 * Lightweight WordPiece tokenizer for all-MiniLM-L6-v2.
 * Avoids pulling in the full @xenova/transformers library (~40 MB).
 *
 * Produces input_ids, attention_mask, and token_type_ids as BigInt64Array
 * tensors suitable for ONNX Runtime Web inference.
 */
export class WordPieceTokenizer {
    constructor() {
        /** @type {Map<string, number>|null} */
        this.vocab = null;
        /** @type {Map<number, string>|null} */
        this.idsToTokens = null;
        this.maxLength = 512;
        this.loaded = false;

        // Special token IDs
        this.padId = 0;       // [PAD]
        this.unkId = 100;     // [UNK]
        this.clsId = 101;     // [CLS]
        this.sepId = 102;     // [SEP]
    }

    /**
     * Load vocabulary from a URL (vocab.txt).
     * Each line is one token; line number = token ID.
     * @param {string} vocabUrl - URL to vocab.txt
     */
    async load(vocabUrl) {
        const response = await fetch(vocabUrl);
        if (!response.ok) {
            throw new Error(`Failed to load vocab from ${vocabUrl}: ${response.status}`);
        }
        const text = await response.text();
        const lines = text.split('\n');

        this.vocab = new Map();
        this.idsToTokens = new Map();

        for (let i = 0; i < lines.length; i++) {
            const token = lines[i].trimEnd('\r');
            if (token.length === 0 && i === lines.length - 1) continue; // trailing newline
            this.vocab.set(token, i);
            this.idsToTokens.set(i, token);
        }

        this.loaded = true;
    }

    /**
     * Tokenize input text into model-ready tensors.
     * @param {string} text - Input text
     * @returns {{ inputIds: BigInt64Array, attentionMask: BigInt64Array, tokenTypeIds: BigInt64Array }}
     */
    tokenize(text) {
        if (!this.loaded) {
            throw new Error('Tokenizer not loaded. Call load() first.');
        }

        // Step 1-4: BasicTokenizer
        const basicTokens = this._basicTokenize(text);

        // Step 5: WordPiece tokenization
        const wpTokens = [];
        for (const token of basicTokens) {
            const subTokens = this._wordPieceTokenize(token);
            wpTokens.push(...subTokens);
        }

        // Step 6: Add [CLS] and [SEP]
        // Step 7: Truncate (keep room for [CLS] and [SEP])
        const maxTokens = this.maxLength - 2;
        const truncated = wpTokens.slice(0, maxTokens);

        const tokenIds = [this.clsId, ...truncated, this.sepId];

        // Step 8: Pad to maxLength
        const inputIds = new BigInt64Array(this.maxLength);
        const attentionMask = new BigInt64Array(this.maxLength);
        const tokenTypeIds = new BigInt64Array(this.maxLength);

        for (let i = 0; i < this.maxLength; i++) {
            if (i < tokenIds.length) {
                inputIds[i] = BigInt(tokenIds[i]);
                attentionMask[i] = 1n;
            } else {
                inputIds[i] = BigInt(this.padId);
                attentionMask[i] = 0n;
            }
            tokenTypeIds[i] = 0n; // single-sentence input
        }

        return { inputIds, attentionMask, tokenTypeIds };
    }

    /**
     * BasicTokenizer: lowercase, strip accents, split on whitespace + punctuation.
     * @param {string} text
     * @returns {string[]}
     */
    _basicTokenize(text) {
        // Lowercase
        text = text.toLowerCase();

        // Strip accents via NFD normalization
        text = text.normalize('NFD').replace(/[\u0300-\u036f]/g, '');

        // Insert whitespace around punctuation and CJK characters
        let output = '';
        for (const ch of text) {
            const cp = ch.codePointAt(0);
            if (this._isPunctuation(cp)) {
                output += ' ' + ch + ' ';
            } else if (this._isCjk(cp)) {
                output += ' ' + ch + ' ';
            } else if (this._isWhitespace(cp)) {
                output += ' ';
            } else if (cp === 0 || cp === 0xfffd || this._isControl(cp)) {
                // Skip control characters
                continue;
            } else {
                output += ch;
            }
        }

        // Split on whitespace and filter empty
        return output.split(/\s+/).filter(t => t.length > 0);
    }

    /**
     * WordPiece tokenization: greedy longest-match-first.
     * @param {string} token
     * @returns {number[]} array of token IDs
     */
    _wordPieceTokenize(token) {
        if (token.length > 200) {
            // Very long tokens are unlikely to be in vocab
            return [this.unkId];
        }

        const ids = [];
        let start = 0;

        while (start < token.length) {
            let end = token.length;
            let found = false;

            while (start < end) {
                let substr = token.substring(start, end);
                if (start > 0) {
                    substr = '##' + substr;
                }

                if (this.vocab.has(substr)) {
                    ids.push(this.vocab.get(substr));
                    found = true;
                    break;
                }

                end--;
            }

            if (!found) {
                ids.push(this.unkId);
                break;
            }

            start = end;
        }

        return ids;
    }

    _isPunctuation(cp) {
        // ASCII punctuation ranges
        if ((cp >= 33 && cp <= 47) || (cp >= 58 && cp <= 64) ||
            (cp >= 91 && cp <= 96) || (cp >= 123 && cp <= 126)) {
            return true;
        }
        // Unicode general category P (punctuation)
        const ch = String.fromCodePoint(cp);
        return /\p{P}/u.test(ch);
    }

    _isCjk(cp) {
        // CJK Unified Ideographs and extensions
        return (cp >= 0x4E00 && cp <= 0x9FFF) ||
            (cp >= 0x3400 && cp <= 0x4DBF) ||
            (cp >= 0x20000 && cp <= 0x2A6DF) ||
            (cp >= 0x2A700 && cp <= 0x2B73F) ||
            (cp >= 0x2B740 && cp <= 0x2B81F) ||
            (cp >= 0x2B820 && cp <= 0x2CEAF) ||
            (cp >= 0xF900 && cp <= 0xFAFF) ||
            (cp >= 0x2F800 && cp <= 0x2FA1F);
    }

    _isWhitespace(cp) {
        if (cp === 32 || cp === 9 || cp === 10 || cp === 13) return true;
        const ch = String.fromCodePoint(cp);
        return /\p{Zs}/u.test(ch);
    }

    _isControl(cp) {
        if (cp === 9 || cp === 10 || cp === 13) return false; // tab, LF, CR are whitespace
        const ch = String.fromCodePoint(cp);
        return /\p{Cc}/u.test(ch);
    }
}
