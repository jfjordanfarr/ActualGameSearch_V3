using ActualGameSearch.Core.Models;

namespace ActualGameSearch.Core.Repositories;

public interface IGamesRepository
{
    // Text-first search over games metadata (title, tags, etc.)
    Task<IReadOnlyList<GameSummary>> SearchAsync(string query, int top, CancellationToken cancellationToken = default);

    // Hybrid search: blend vector similarity (game description embeddings) with title CONTAINS boost
    Task<IReadOnlyList<GameSummary>> HybridSearchAsync(string query, float[] queryVector, int top, CancellationToken cancellationToken = default);
}
