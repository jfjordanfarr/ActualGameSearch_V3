using System.Net.Http;
using System.Text;
using System.Linq;

namespace ActualGameSearch.Core.Embeddings;

public interface ITextEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateVectorAsync(string input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateVectorsAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default);
}

public sealed class TextEmbeddingService : ITextEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly int _dims;
    private readonly bool _allowDeterministicFallback;

    public TextEmbeddingService(HttpClient http, string endpoint, string model, int dims = 768, bool allowDeterministicFallback = false)
    {
        _http = http;
        _endpoint = endpoint.EndsWith('/') ? endpoint : (endpoint + "/");
        _model = model;
        _dims = dims;
        _allowDeterministicFallback = allowDeterministicFallback;
    }

    public async Task<ReadOnlyMemory<float>> GenerateVectorAsync(string input, CancellationToken cancellationToken = default)
    {
        var list = await GenerateVectorsAsync(new[] { input }, cancellationToken);
        return list.Count > 0 ? list[0] : ReadOnlyMemory<float>.Empty;
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateVectorsAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default)
    {
        var items = inputs as IReadOnlyList<string> ?? inputs.ToList();

        async Task<(bool ok, List<float[]> vecs)> TryCallAsync(string path, object payload, Func<System.Text.Json.JsonElement, List<float[]>> projector)
        {
            try
            {
                var url = new Uri(new Uri(_endpoint), path);
                var body = System.Text.Json.JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return (false, new());
                using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var list = projector(doc.RootElement);
                if (list.Count == items.Count) return (true, list);
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

        var t1 = await TryCallAsync("api/embeddings", new { model = _model, input = items }, ProjectEmbeddingsData);
        if (t1.ok)
        {
            return t1.vecs.Select(v => new ReadOnlyMemory<float>(v)).ToList();
        }
        var t2 = await TryCallAsync("api/embeddings", new { model = _model, prompt = items }, ProjectEmbeddingsData);
        if (t2.ok)
        {
            return t2.vecs.Select(v => new ReadOnlyMemory<float>(v)).ToList();
        }
        var t3 = await TryCallAsync("api/embed", new { model = _model, input = items }, ProjectEmbedArray);
        if (t3.ok)
        {
            return t3.vecs.Select(v => new ReadOnlyMemory<float>(v)).ToList();
        }

        if (_allowDeterministicFallback)
        {
            var list = new List<ReadOnlyMemory<float>>(items.Count);
            foreach (var s in items)
            {
                list.Add(new ReadOnlyMemory<float>(DeterministicVector(s, _dims)));
            }
            return list;
        }
        throw new InvalidOperationException("Failed to generate embeddings from Ollama and deterministic fallback is disabled.");
    }

    private static float[] DeterministicVector(string text, int dims)
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
}
