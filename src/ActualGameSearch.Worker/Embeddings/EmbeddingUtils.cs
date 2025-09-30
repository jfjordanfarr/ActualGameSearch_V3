using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ActualGameSearch.Worker.Embeddings;

public static class EmbeddingUtils
{
    public static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var arr = new char[html.Length];
        int idx = 0; bool inside = false;
        foreach (var ch in html)
        {
            if (ch == '<') { inside = true; continue; }
            if (ch == '>') { inside = false; continue; }
            if (!inside) arr[idx++] = ch;
        }
        return new string(arr, 0, idx).Replace("\n", " ").Replace("\r", " ").Trim();
    }

    public static float[] DeterministicVector(string text, int dims)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var vec = new float[dims];
        for (int i = 0; i < bytes.Length; i++)
        {
            vec[i % dims] += (bytes[i] % 23) / 23.0f;
        }
        var norm = MathF.Sqrt(vec.Sum(v => v * v));
        if (norm > 0)
        {
            for (int i = 0; i < vec.Length; i++) vec[i] /= norm;
        }
        return vec;
    }

    public static async Task<List<float[]>> GenerateVectorsAsync(HttpClient http, string ollamaEndpoint, string model, List<string> texts, int dims, bool allowDeterministicFallback, int numCtx = 2048, int maxBatch = 64, bool allowChunking = false)
    {
        // Helper: mean-pool multiple vectors
        static float[] MeanPool(IReadOnlyList<float[]> parts, int dims)
        {
            var acc = new float[dims];
            if (parts.Count == 0) return acc;
            foreach (var v in parts)
            {
                for (int i = 0; i < dims && i < v.Length; i++) acc[i] += v[i];
            }
            var inv = 1.0f / parts.Count;
            for (int i = 0; i < dims; i++) acc[i] *= inv;
            return acc;
        }

        // Enforce correctness by default: if inputs exceed estimated context, fail unless chunking is explicitly allowed.
        // Heuristic mapping ~4 chars per token avoids a tokenizer dependency.
        int approxCharsPerToken = 4;
        int maxChars = Math.Max(256, numCtx * approxCharsPerToken);
        var expandedTexts = new List<(int originalIndex, string piece)>();
        var pieceBoundaries = new List<(int start, int count)>();
        if (!allowChunking)
        {
            for (int idx = 0; idx < texts.Count; idx++)
            {
                var t = texts[idx] ?? string.Empty;
                if (t.Length > maxChars)
                {
                    throw new InvalidOperationException($"Input text at index {idx} exceeds allowed context length (chars={t.Length} > maxApproxChars={maxChars}). Increase Embeddings:NumCtx or enable Embeddings:AllowChunking explicitly.");
                }
                pieceBoundaries.Add((expandedTexts.Count, 1));
                expandedTexts.Add((idx, t));
            }
        }
        else
        {
            for (int idx = 0; idx < texts.Count; idx++)
            {
                var t = texts[idx] ?? string.Empty;
                if (t.Length <= maxChars)
                {
                    pieceBoundaries.Add((expandedTexts.Count, 1));
                    expandedTexts.Add((idx, t));
                    continue;
                }
                int start = 0;
                int pieces = 0;
                while (start < t.Length)
                {
                    int len = Math.Min(maxChars, t.Length - start);
                    expandedTexts.Add((idx, t.Substring(start, len)));
                    start += len;
                    pieces++;
                }
                pieceBoundaries.Add((expandedTexts.Count - pieces, pieces));
            }
        }

        // Work over the expanded piece list, but keep original ordering via indices.
        var pieceTexts = expandedTexts.Select(e => e.piece).ToList();

        async Task<(bool ok, List<float[]> vecs)> TryCallAsync(string path, object payload, Func<System.Text.Json.JsonElement, List<float[]>> projector)
        {
            try
            {
                var url = new Uri(new Uri(ollamaEndpoint), path);
                var body = System.Text.Json.JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                using var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return (false, new());
                using var stream = await resp.Content.ReadAsStreamAsync();
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
                var list = projector(doc.RootElement);
                if (list.Count == ((payload as dynamic).input as System.Collections.ICollection)?.Count) return (true, list);
                return (false, new());
            }
            catch { return (false, new()); }
        }

        static List<float[]> ProjectEmbeddingsData(System.Text.Json.JsonElement root)
        {
            var list = new List<float[]>();
            if (root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("embedding", out var emb) && emb.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        list.Add(emb.EnumerateArray().Select(x => x.GetSingle()).ToArray());
                    }
                }
            }
            return list;
        }

        static List<float[]> ProjectEmbedArray(System.Text.Json.JsonElement root)
        {
            var list = new List<float[]>();
            if (root.TryGetProperty("embeddings", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var emb in arr.EnumerateArray())
                {
                    if (emb.ValueKind == System.Text.Json.JsonValueKind.Array)
                        list.Add(emb.EnumerateArray().Select(x => x.GetSingle()).ToArray());
                }
            }
            return list;
        }

        // Batch inputs to avoid extremely large single calls
        var batches = new List<List<string>>();
        for (int i = 0; i < pieceTexts.Count; i += Math.Max(1, maxBatch))
            batches.Add(pieceTexts.GetRange(i, Math.Min(maxBatch, pieceTexts.Count - i)));

        var allPieces = new List<float[]>(capacity: pieceTexts.Count);
        foreach (var batch in batches)
        {
            var options = new { num_ctx = numCtx };
            // Try the more reliable endpoint first
            var payloadEmbed = new { model, input = batch, options };
            var t3 = await TryCallAsync("api/embed", payloadEmbed, ProjectEmbedArray);
            if (t3.ok)
            {
                Console.WriteLine($"Ollama embeddings via /api/embed (dims={t3.vecs.FirstOrDefault()?.Length ?? 0}, batch={batch.Count})");
                allPieces.AddRange(t3.vecs);
                continue;
            }
            // Then try legacy /api/embeddings which may be absent on some builds
            var payloadEmb = new { model, input = batch, options };
            var t1 = await TryCallAsync("api/embeddings", payloadEmb, ProjectEmbeddingsData);
            if (t1.ok)
            {
                Console.WriteLine($"Ollama embeddings via /api/embeddings input (dims={t1.vecs.FirstOrDefault()?.Length ?? 0}, batch={batch.Count})");
                allPieces.AddRange(t1.vecs);
                continue;
            }

            // Fallback: OpenAI-compatible path (supported by Ollama when the compatibility API is enabled)
            var payloadV1 = new { model, input = batch };
            var tV1 = await TryCallAsync("v1/embeddings", payloadV1, ProjectEmbeddingsData);
            if (tV1.ok)
            {
                Console.WriteLine($"Ollama embeddings via /v1/embeddings (dims={tV1.vecs.FirstOrDefault()?.Length ?? 0}, batch={batch.Count})");
                allPieces.AddRange(tV1.vecs);
                continue;
            }
            // If neither endpoint worked for this batch, bail out
            allPieces.Clear();
            break;
        }

        // Recompose piece vectors back to one per original input via mean-pooling
        if (allPieces.Count == pieceTexts.Count)
        {
            var result = new List<float[]>(texts.Count);
            int cursor = 0;
            foreach (var (start, count) in pieceBoundaries)
            {
                if (count == 1)
                {
                    result.Add(allPieces[cursor++]);
                }
                else
                {
                    var parts = new List<float[]>(count);
                    for (int i = 0; i < count; i++) parts.Add(allPieces[cursor++]);
                    result.Add(MeanPool(parts, dims));
                }
            }
            if (result.Count == texts.Count) return result;
        }

        if (allowDeterministicFallback)
        {
            Console.WriteLine("Using deterministic fallback embeddings (allowed by config).");
            return texts.Select(t => DeterministicVector(t, dims)).ToList();
        }
        throw new InvalidOperationException("Failed to generate embeddings from Ollama and deterministic fallback is disabled.");
    }
}
