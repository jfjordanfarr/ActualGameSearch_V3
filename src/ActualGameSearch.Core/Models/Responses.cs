namespace ActualGameSearch.Core.Models;

public sealed record GamesSearchResponse(IReadOnlyList<GameSummary> Items);

public sealed record ReviewsSearchResponse(IReadOnlyList<Candidate> Items);

public sealed record GroupedSearchResponse(string QueryId, IReadOnlyList<GameCandidates> Items);
