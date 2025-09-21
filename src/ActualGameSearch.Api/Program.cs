using ActualGameSearch.ServiceDefaults;
using ActualGameSearch.Core.Primitives;

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
	var payload = new { items = Array.Empty<object>() };
	return Results.Ok(Result<object>.Success(payload));
});

app.MapGet("/api/search/reviews", (string? q) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var payload = new { items = Array.Empty<object>() };
	return Results.Ok(Result<object>.Success(payload));
});

app.MapGet("/api/search", (string? q) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var payload = new { queryId = Guid.NewGuid().ToString("n"), items = Array.Empty<object>() };
	return Results.Ok(Result<object>.Success(payload));
});

app.Run();

public partial class Program;
