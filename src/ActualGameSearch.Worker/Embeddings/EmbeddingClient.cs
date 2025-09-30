using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ActualGameSearch.Worker.Configuration;

namespace ActualGameSearch.Worker.Embeddings;

/// <summary>
/// Embedding client that calls Ollama HTTP API with deterministic fallback.
/// Uses raw HTTP calls for now but could be refactored to use Microsoft.Extensions.AI.
/// </summary>
public class EmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly bool _allowDeterministicFallback;
    private bool _modelEnsured;

    public EmbeddingClient(HttpClient httpClient, IOptions<EmbeddingOptions> options, bool allowDeterministicFallback = false)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _allowDeterministicFallback = allowDeterministicFallback;
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (!textList.Any()) return Array.Empty<float[]>();

        try
        {
            // Ensure target model exists in the Ollama instance (DCP container is ephemeral)
            if (!_modelEnsured)
            {
                await EnsureModelAsync(cancellationToken);
                _modelEnsured = true;
            }
            // Use the existing EmbeddingUtils logic but wrapped in a cleaner interface
            var vectors = await EmbeddingUtils.GenerateVectorsAsync(
                _httpClient,
                _options.OllamaBaseUrl,
                _options.Model,
                textList,
                768, // TODO: Make configurable
                _allowDeterministicFallback,
                _options.ContextLength,
                _options.BatchSize
            );

            return vectors.ToArray();
        }
        catch (Exception ex)
        {
            if (_allowDeterministicFallback)
            {
                Console.WriteLine($"Ollama embedding failed, using deterministic fallback: {ex.Message}");
                return textList.Select(t => GenerateDeterministicEmbedding(t)).ToArray();
            }
            throw;
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await GenerateEmbeddingsAsync(new[] { text }, cancellationToken);
        return results.FirstOrDefault() ?? GenerateDeterministicEmbedding(text);
    }

    public float[] GenerateDeterministicEmbedding(string text, int dimensions = 768)
    {
        return EmbeddingUtils.DeterministicVector(text, dimensions);
    }

    private async Task EnsureModelAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Normalize base URL
            var baseUrl = _options.OllamaBaseUrl?.TrimEnd('/') ?? "http://localhost:11434";

            // 1) Check if model is available via /api/show
            var showUrl = new Uri(new Uri(baseUrl + "/"), "api/show");
            using (var showReq = new HttpRequestMessage(HttpMethod.Post, showUrl))
            {
                var showBody = JsonSerializer.Serialize(new { name = _options.Model });
                showReq.Content = new StringContent(showBody, Encoding.UTF8, "application/json");
                using var showResp = await _httpClient.SendAsync(showReq, cancellationToken);
                if (showResp.IsSuccessStatusCode)
                {
                    // Model present
                    return;
                }
            }

            // 2) Create model via /api/create using an inlined Modelfile that extends nomic-embed-text
            var modelfile = $"FROM nomic-embed-text:latest\nPARAMETER num_ctx {_options.ContextLength}\n";
            var createUrl = new Uri(new Uri(baseUrl + "/"), "api/create");
            using var createReq = new HttpRequestMessage(HttpMethod.Post, createUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { name = _options.Model, modelfile }), Encoding.UTF8, "application/json")
            };
            Console.WriteLine($"Creating Ollama model '{_options.Model}' with num_ctx={_options.ContextLength}...");
            using var createResp = await _httpClient.SendAsync(createReq, cancellationToken);
            if (!createResp.IsSuccessStatusCode)
            {
                var err = await createResp.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to create model '{_options.Model}': {createResp.StatusCode} {err}");
            }
        }
        catch (Exception ex)
        {
            // Don't block startup on this; embedding call will fail later if needed
            Console.WriteLine($"Warning: EnsureModelAsync encountered an error: {ex.Message}");
        }
    }
}