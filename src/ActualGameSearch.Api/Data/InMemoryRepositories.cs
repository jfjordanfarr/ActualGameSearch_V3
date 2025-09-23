using ActualGameSearch.Core.Embeddings;
using ActualGameSearch.Core.Models;
using ActualGameSearch.Core.Repositories;

namespace ActualGameSearch.Api.Data;

internal sealed class InMemoryGamesRepository : IGamesRepository
{
    public Task<IReadOnlyList<GameSummary>> SearchAsync(string query, int top, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GameSummary>>(Array.Empty<GameSummary>());

    public Task<IReadOnlyList<GameSummary>> HybridSearchAsync(string query, float[] queryVector, int top, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GameSummary>>(Array.Empty<GameSummary>());
}

internal sealed class InMemoryReviewsRepository : IReviewsRepository
{
    public Task<IReadOnlyList<Candidate>> TextSearchAsync(string query, int top, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Candidate>>(Array.Empty<Candidate>());

    public Task<IReadOnlyList<Candidate>> VectorSearchAsync(float[] queryVector, int top, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Candidate>>(Array.Empty<Candidate>());
}

internal sealed class NoopEmbeddingService : ITextEmbeddingService
{
    public Task<ReadOnlyMemory<float>> GenerateVectorAsync(string input, CancellationToken cancellationToken = default)
        => Task.FromResult(ReadOnlyMemory<float>.Empty);

    public Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateVectorsAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>(Array.Empty<ReadOnlyMemory<float>>());
}
