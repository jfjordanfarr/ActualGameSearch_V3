using ActualGameSearch.Core.Embeddings;
using ActualGameSearch.Core.Models;
using ActualGameSearch.Core.Repositories;

namespace ActualGameSearch.Api.Data;

internal sealed class InMemoryGamesRepository : IGamesRepository
{
    private static readonly List<GameSummary> _sampleGames = new()
    {
        new("620", "Portal 2", new[] { "puzzle", "co-op", "platformer" }, 150000),
        new("570", "Dota 2", new[] { "moba", "strategy", "multiplayer" }, 95000),
        new("208730", "Moonstone: A Hard Days Knight", new[] { "action", "beat-em-up", "medieval" }, 1200),
        new("12210", "Grand Theft Auto IV", new[] { "open-world", "action", "crime" }, 75000),
        new("1091500", "Cyberpunk 2077", new[] { "rpg", "open-world", "futuristic" }, 125000)
    };

    public Task<IReadOnlyList<GameSummary>> SearchAsync(string query, int top, CancellationToken cancellationToken = default)
    {
        var results = _sampleGames
            .Where(g => g.GameTitle.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                       g.TagSummary.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(top)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<GameSummary>>(results);
    }

    public Task<IReadOnlyList<GameSummary>> HybridSearchAsync(string query, float[] queryVector, int top, CancellationToken cancellationToken = default)
    {
        // In memory mode: fall back to simple text search since we don't have real vectors
        return SearchAsync(query, top, cancellationToken);
    }
}

internal sealed class InMemoryReviewsRepository : IReviewsRepository
{
    private static readonly List<Candidate> _sampleReviews = new()
    {
        new("620", "Portal 2", "review1", 0.9, 0.85, 0.875, "Amazing puzzle game with great co-op mode", 
            new ReviewMeta(145, DateTimeOffset.Now.AddDays(-30))) { FullText = "Portal 2 is an amazing puzzle game that builds upon everything that made the original great. The single-player campaign is engaging with witty dialogue from GLaDOS, and the co-op mode adds a whole new dimension to the puzzle-solving experience. The level design is superb and the physics-based mechanics feel smooth and responsive. Highly recommended for anyone who enjoys thinking games." },
        
        new("570", "Dota 2", "review2", 0.8, 0.7, 0.75, "Great MOBA but very steep learning curve", 
            new ReviewMeta(89, DateTimeOffset.Now.AddDays(-15))) { FullText = "Dota 2 is undoubtedly one of the best MOBA games available, but it comes with a warning: the learning curve is incredibly steep. If you're willing to invest hundreds of hours to get good, you'll find an incredibly deep and rewarding strategic game. The community can be toxic at times, but the gameplay itself is fantastic." },
        
        new("208730", "Moonstone: A Hard Days Knight", "review3", 0.6, 0.65, 0.625, "Classic beat-em-up with medieval theme", 
            new ReviewMeta(23, DateTimeOffset.Now.AddDays(-60))) { FullText = "A nostalgic trip back to the classic beat-em-up games of the past. Moonstone has a unique medieval setting and some interesting RPG elements, though the graphics and gameplay feel quite dated by modern standards. Still worth playing for fans of the genre or those looking for some retro gaming fun." },
        
        new("12210", "Grand Theft Auto IV", "review4", 0.85, 0.8, 0.825, "Open world crime saga with great story", 
            new ReviewMeta(267, DateTimeOffset.Now.AddDays(-45))) { FullText = "GTA IV delivers an engaging crime story set in a believable recreation of New York City. The narrative is more serious and grounded compared to other GTA games, which works well with the darker tone. The driving physics take some getting used to, and the graphics are showing their age, but the overall experience is still compelling." },
        
        new("1091500", "Cyberpunk 2077", "review5", 0.7, 0.75, 0.725, "Ambitious RPG with technical issues but great atmosphere", 
            new ReviewMeta(156, DateTimeOffset.Now.AddDays(-10))) { FullText = "Cyberpunk 2077 is a game of contradictions. When it works, it's absolutely stunning - the world-building is incredible, the story is engaging, and the atmosphere is unmatched. However, even after patches, technical issues and bugs still persist. If you can look past the problems, there's a really good RPG underneath, but it still feels like it needed more development time." }
    };

    public Task<IReadOnlyList<Candidate>> VectorSearchAsync(float[] queryVector, int top, CancellationToken cancellationToken = default)
    {
        // In memory mode: fall back to simple text search since we don't have real vectors
        // We could implement a basic scoring algorithm here, but for now just return by combined score
        var results = _sampleReviews
            .OrderByDescending(r => r.CombinedScore)
            .Take(top)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<Candidate>>(results);
    }

    public Task<IReadOnlyList<Candidate>> TextSearchAsync(string query, int top, CancellationToken cancellationToken = default)
    {
        var results = _sampleReviews
            .Where(r => (r.GameTitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (r.Excerpt?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (r.FullText?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(r => r.CombinedScore)
            .Take(top)
            .ToList();
        
        return Task.FromResult<IReadOnlyList<Candidate>>(results);
    }
}

internal sealed class NoopEmbeddingService : ITextEmbeddingService
{
    private readonly int _dims;

    public NoopEmbeddingService(int dims = 768)
    {
        _dims = dims;
    }

    public Task<ReadOnlyMemory<float>> GenerateVectorAsync(string input, CancellationToken cancellationToken = default)
    {
        // Generate a deterministic but pseudo-random vector based on the input hash
        var hash = input.GetHashCode();
        var random = new Random(Math.Abs(hash));
        var vector = new float[_dims];
        
        for (int i = 0; i < _dims; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range [-1, 1]
        }
        
        return Task.FromResult<ReadOnlyMemory<float>>(vector.AsMemory());
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateVectorsAsync(IEnumerable<string> inputs, CancellationToken cancellationToken = default)
    {
        var results = new List<ReadOnlyMemory<float>>();
        foreach (var input in inputs)
        {
            results.Add(await GenerateVectorAsync(input, cancellationToken));
        }
        return results;
    }
}
