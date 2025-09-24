namespace ActualGameSearch.Core.Models;

public sealed record ReRankWeights(double Semantic = 0.5, double Text = 0.5);

public sealed record ConvergenceFilters(int? MinReviewMatches = null, bool? RequireGameAndReview = null);
