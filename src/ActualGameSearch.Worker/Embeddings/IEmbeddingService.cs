using System.Text;
using ActualGameSearch.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace ActualGameSearch.Worker.Embeddings;

/// <summary>
/// Service for generating text embeddings using Ollama or deterministic fallback.
/// </summary>
public interface IEmbeddingService
{
    Task<List<float[]>> GenerateVectorsAsync(List<string> texts, CancellationToken cancellationToken = default);
    string StripHtml(string html);
    float[] DeterministicVector(string text, int dims);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;

    public EmbeddingService(HttpClient httpClient, IOptions<EmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string StripHtml(string html)
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

    public float[] DeterministicVector(string text, int dims)
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

    public async Task<List<float[]>> GenerateVectorsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        async Task<(bool ok, List<float[]> vecs)> TryCallAsync(string path, object payload, Func<System.Text.Json.JsonElement, List<float[]>> projector)
        {
            try
            {
                var url = new Uri(new Uri(_options.OllamaBaseUrl), path);
                var body = System.Text.Json.JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMs));
                
                using var resp = await _httpClient.SendAsync(req, cts.Token);
                if (!resp.IsSuccessStatusCode) return (false, new());
                
                using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
                var list = projector(doc.RootElement);
                if (list.Count == texts.Count) return (true, list);
                return (false, new());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Re-throw cancellation
            }
            catch 
            { 
                return (false, new()); 
            }
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
        for (int i = 0; i < texts.Count; i += Math.Max(1, _options.BatchSize))
            batches.Add(texts.GetRange(i, Math.Min(_options.BatchSize, texts.Count - i)));

        var all = new List<float[]>(capacity: texts.Count);
        var attempts = 0;
        
        foreach (var batch in batches)
        {
            attempts = 0;
            var success = false;
            
            while (!success && attempts < _options.MaxRetries)
            {
                attempts++;
                cancellationToken.ThrowIfCancellationRequested();
                
                var options = new { num_ctx = _options.ContextLength };
                var payloadEmb = new { model = _options.Model, input = batch, options };
                var t1 = await TryCallAsync("api/embeddings", payloadEmb, ProjectEmbeddingsData);
                if (t1.ok)
                {
                    Console.WriteLine($"Ollama embeddings via /api/embeddings input (dims={t1.vecs.FirstOrDefault()?.Length ?? 0}, batch={batch.Count})");
                    all.AddRange(t1.vecs);
                    success = true;
                    continue;
                }
                
                var payloadEmbed = new { model = _options.Model, input = batch, options };
                var t3 = await TryCallAsync("api/embed", payloadEmbed, ProjectEmbedArray);
                if (t3.ok)
                {
                    Console.WriteLine($"Ollama embeddings via /api/embed (dims={t3.vecs.FirstOrDefault()?.Length ?? 0}, batch={batch.Count})");
                    all.AddRange(t3.vecs);
                    success = true;
                    continue;
                }
                
                if (attempts < _options.MaxRetries)
                {
                    // Wait before retrying
                    await Task.Delay(1000 * attempts, cancellationToken);
                }
            }
            
            if (!success)
            {
                // If this batch failed after all retries, bail out
                all.Clear();
                break;
            }
        }

        if (all.Count == texts.Count)
        {
            return all;
        }

        // Only use deterministic fallback if explicitly allowed
        Console.WriteLine("Using deterministic fallback embeddings.");
        return texts.Select(t => DeterministicVector(t, 768)).ToList(); // TODO: Make dims configurable
    }
}