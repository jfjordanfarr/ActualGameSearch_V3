using ActualGameSearch.Core.Models;

namespace ActualGameSearch.Core.Repositories;

public interface IGamesRepository
{
    // Text-first search over games metadata (title, tags, etc.)
    Task<IReadOnlyList<GameSummary>> SearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
