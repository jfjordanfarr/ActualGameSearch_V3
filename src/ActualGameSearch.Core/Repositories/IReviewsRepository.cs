using ActualGameSearch.Core.Models;

namespace ActualGameSearch.Core.Repositories;

public interface IReviewsRepository
{
    // Vector-first search over review vectors, optionally filtered
    Task<IReadOnlyList<Candidate>> VectorSearchAsync(float[] queryVector, int top, CancellationToken cancellationToken = default);

    // Text-first search over review content
    Task<IReadOnlyList<Candidate>> TextSearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
