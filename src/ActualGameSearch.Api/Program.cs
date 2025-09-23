using ActualGameSearch.ServiceDefaults;
using ActualGameSearch.Core.Primitives;
using ActualGameSearch.Core.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;
using ActualGameSearch.Core.Embeddings;
using ActualGameSearch.Core.Repositories;
using ActualGameSearch.Api.Data;
using ActualGameSearch.Api.Infrastructure;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Determine if we have Cosmos connection info (AppHost/discovery) or we're in test mode
bool Has(string key) => !string.IsNullOrWhiteSpace(builder.Configuration[key]);
var forceInMemory = builder.Configuration.GetValue<bool>("UseInMemory");
var isTestHost = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_TESTHOST"), "true", StringComparison.OrdinalIgnoreCase);
var isTestEnv = string.Equals(builder.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase);
var hasCosmos = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("cosmos-db"))
	|| Has("Aspire:Microsoft:Azure:Cosmos:ConnectionString")
	|| Has("Aspire:Microsoft:Azure:Cosmos:AccountEndpoint")
	|| Has("Aspire:Microsoft:Azure:Cosmos:cosmos-db:ConnectionString")
	|| Has("Aspire:Microsoft:Azure:Cosmos:cosmos-db:AccountEndpoint");
// Always force in-memory mode under tests (either env name or test host flag)
if (isTestEnv || isTestHost) forceInMemory = true;
if (forceInMemory) hasCosmos = false;

if (hasCosmos)
{
	// Embeddings via Ollama in normal mode
	var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434/";
	var ollamaModel = builder.Configuration["Ollama:Model"] ?? "nomic-embed-text:v1.5";
	builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel));
	builder.Services.AddSingleton<ITextEmbeddingService, TextEmbeddingService>();

	// Cosmos client via Aspire
	builder.AddAzureCosmosClient("cosmos-db");
	builder.Services.AddScoped<IGamesRepository, CosmosGamesRepository>();
	builder.Services.AddScoped<IReviewsRepository, CosmosReviewsRepository>();
	builder.Services.AddHostedService<CosmosBootstrapper>();
}
else
{
	// Test/fallback mode: stub services to allow API to start without Cosmos/Ollama
	builder.Services.AddSingleton<ITextEmbeddingService, NoopEmbeddingService>();
	builder.Services.AddSingleton<IGamesRepository, InMemoryGamesRepository>();
	builder.Services.AddSingleton<IReviewsRepository, InMemoryReviewsRepository>();
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Games search (text)
app.MapGet("/api/search/games", async (string? q, int? top, IGamesRepository gamesRepo, CancellationToken ct) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var limit = top is int t && t > 0 && t <= 50 ? t : 10;
	var items = await gamesRepo.SearchAsync(q, limit, ct);
	return Results.Ok(Result<GamesSearchResponse>.Success(new GamesSearchResponse(items)));
});

// Reviews search (semantic/vector-first)
app.MapGet("/api/search/reviews", async (string? q, int? top, ITextEmbeddingService embed, IReviewsRepository reviewsRepo, CancellationToken ct) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var limit = top is int t && t > 0 && t <= 50 ? t : 10;
	var vec = await embed.GenerateVectorAsync(q, ct);
	var items = await reviewsRepo.VectorSearchAsync(vec.ToArray(), limit, ct);
	return Results.Ok(Result<ReviewsSearchResponse>.Success(new ReviewsSearchResponse(items)));
});

// Grouped search (by game, using semantic review matches)
app.MapGet("/api/search", async (string? q, int? top, ITextEmbeddingService embed, IReviewsRepository reviewsRepo, CancellationToken ct) =>
{
	if (string.IsNullOrWhiteSpace(q))
	{
		return Results.BadRequest(Result<object>.Fail("missing_q", "Query parameter 'q' is required."));
	}
	var groupLimit = top is int t && t > 0 && t <= 25 ? t : 10;
	var vec = await embed.GenerateVectorAsync(q, ct);
	// Fetch a larger candidate set to group effectively
	var flat = await reviewsRepo.VectorSearchAsync(vec.ToArray(), Math.Max(groupLimit * 5, 30), ct);
	// Group by game
	var grouped = flat
		.GroupBy(c => (c.GameId, c.GameTitle))
		.Select(g => new
		{
			Key = g.Key,
			MaxScore = g.Max(c => c.CombinedScore),
			Items = g.OrderByDescending(c => c.CombinedScore).Take(3).ToList(),
			Count = g.Count()
		})
		.OrderByDescending(x => x.MaxScore)
		.Take(groupLimit)
		.Select(x => new GameCandidates(
			GameId: x.Key.GameId,
			GameTitle: x.Key.GameTitle,
			TagSummary: Array.Empty<string>(),
			ReviewCount: x.Count,
			PurchaseOriginRatio: new PurchaseOriginRatio(0, 0, 0, x.Count),
			Candidates: x.Items
		))
		.ToList();

	var payload = new GroupedSearchResponse(Guid.NewGuid().ToString("n"), grouped);
	return Results.Ok(Result<GroupedSearchResponse>.Success(payload));
});

app.Run();

public partial class Program;
