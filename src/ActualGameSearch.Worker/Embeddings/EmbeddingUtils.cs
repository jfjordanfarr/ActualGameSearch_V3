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

    public static async Task<List<float[]>> GenerateVectorsAsync(HttpClient http, string ollamaEndpoint, string model, List<string> texts, int dims, bool allowDeterministicFallback, int numCtx = 2048, int maxBatch = 64)
    {
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
                if (list.Count == texts.Count) return (true, list);
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
        for (int i = 0; i < texts.Count; i += Math.Max(1, maxBatch))
            batches.Add(texts.GetRange(i, Math.Min(maxBatch, texts.Count - i)));

        var all = new List<float[]>(capacity: texts.Count);
        foreach (var batch in batches)
        {
            var options = new { num_ctx = numCtx };
            var payloadEmb = new { model, input = batch, options };
            var t1 = await TryCallAsync("api/embeddings", payloadEmb, ProjectEmbeddingsData);
            if (t1.ok)
            {
                Console.WriteLine($"Ollama embeddings via /api/embeddings input (dims={t1.vecs.FirstOrDefault()?.Length ?? 0}, batch={batch.Count})");
                all.AddRange(t1.vecs);
                continue;
            }
            var payloadEmbed = new { model, input = batch, options };
            var t3 = await TryCallAsync("api/embed", payloadEmbed, ProjectEmbedArray);
            if (t3.ok)
            {
                Console.WriteLine($"Ollama embeddings via /api/embed (dims={t3.vecs.FirstOrDefault()?.Length ?? 0}, batch={batch.Count})");
                all.AddRange(t3.vecs);
                continue;
            }
            // If neither endpoint worked for this batch, bail out
            all.Clear();
            break;
        }

        if (all.Count == texts.Count)
        {
            return all;
        }

        if (allowDeterministicFallback)
        {
            Console.WriteLine("Using deterministic fallback embeddings (allowed by config).");
            return texts.Select(t => DeterministicVector(t, dims)).ToList();
        }
        throw new InvalidOperationException("Failed to generate embeddings from Ollama and deterministic fallback is disabled.");
    }
}
