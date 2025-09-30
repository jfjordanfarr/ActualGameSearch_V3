using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ActualGameSearch.Worker.Embeddings;

/// <summary>
/// Service for generating text embeddings with fallback support.
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// Generate embeddings for multiple texts.
    /// </summary>
    /// <param name="texts">Input texts to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of embeddings, one per input text</returns>
    Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embedding for a single text.
    /// </summary>
    /// <param name="text">Input text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a deterministic embedding for a text (fallback method).
    /// </summary>
    /// <param name="text">Input text</param>
    /// <param name="dimensions">Number of dimensions</param>
    /// <returns>Deterministic embedding vector</returns>
    float[] GenerateDeterministicEmbedding(string text, int dimensions = 768);
}