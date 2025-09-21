using ActualGameSearch.ServiceDefaults;
using ActualGameSearch.Core.Primitives;
using ActualGameSearch.Core.Models;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Temporary placeholders to support frontend wiring; real implementations will follow tasks
app.MapGet("/api/search/games", (string? q) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var payload = new GamesSearchResponse(Array.Empty<GameSummary>());
	return Results.Ok(Result<GamesSearchResponse>.Success(payload));
});

app.MapGet("/api/search/reviews", (string? q) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var payload = new ReviewsSearchResponse(Array.Empty<Candidate>());
	return Results.Ok(Result<ReviewsSearchResponse>.Success(payload));
});

app.MapGet("/api/search", (string? q) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var payload = new GroupedSearchResponse(Guid.NewGuid().ToString("n"), Array.Empty<GameCandidates>());
	return Results.Ok(Result<GroupedSearchResponse>.Success(payload));
});

app.Run();

public partial class Program;
