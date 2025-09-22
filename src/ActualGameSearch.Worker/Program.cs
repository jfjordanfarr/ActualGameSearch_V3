using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Azure.Cosmos;
using ActualGameSearch.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ActualGameSearch.Worker;

internal class Program
{
	public static async Task Main(string[] args)
	{
	var builder = Host.CreateApplicationBuilder(args);
	builder.AddServiceDefaults();

		// Config defaults (can be overridden via appsettings or env)
		var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434/";
		var ollamaModel = builder.Configuration["Ollama:Model"] ?? "nomic-embed-text:v1.5";
		var cosmosConn = builder.Configuration.GetConnectionString("cosmos-db");
		var dbName = builder.Configuration["Cosmos:Database"] ?? "actualgames";
		var gamesContainer = builder.Configuration["Cosmos:GamesContainer"] ?? "games";
		var reviewsContainer = builder.Configuration["Cosmos:ReviewsContainer"] ?? "reviews";
		var vectorPath = builder.Configuration["Cosmos:Vector:Path"] ?? "/vector";
		var dims = int.TryParse(builder.Configuration["Cosmos:Vector:Dimensions"], out var d) ? d : 768;
	var distance = (builder.Configuration["Cosmos:Vector:DistanceFunction"] ?? "cosine").ToLowerInvariant();
	var indexType = (builder.Configuration["Cosmos:Vector:IndexType"] ?? "diskann").ToLowerInvariant();

	// Services
	builder.Services.AddHttpClient();
	// Let Aspire wire CosmosClient via service discovery
	builder.AddAzureCosmosClient("cosmos-db");

		// Delay Cosmos client creation to runtime; we'll try to connect and otherwise fall back to writing JSON artifacts.

		using var host = builder.Build();

		var http = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient();

		// Attempt to pre-pull the embedding model in the Ollama container (best-effort)
		try
		{
			var pullUrl = new Uri(new Uri(ollamaEndpoint), "/api/pull");
			var body = System.Text.Json.JsonSerializer.Serialize(new { name = ollamaModel });
			using var req = new HttpRequestMessage(HttpMethod.Post, pullUrl)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json")
			};
			using var resp = await http.SendAsync(req);
			// ignore status; container may already have the model
		}
		catch { /* ignore */ }
	// embedding via local function using Ollama HTTP when available
		CosmosClient? cosmos = null;
		try { cosmos = host.Services.GetService<CosmosClient>(); }
		catch { /* leave null to fall back to artifacts */ }

		// DB and containers
		Container? games = null;
		Container? reviews = null;
		Database? db = null;
		if (cosmos is not null)
		{
			var attempts = 0;
			while (attempts < 12 && db is null)
			{
				attempts++;
				try
				{
					db = (await cosmos.CreateDatabaseIfNotExistsAsync(dbName)).Database;
					games = (await db.CreateContainerIfNotExistsAsync(new ContainerProperties(gamesContainer, "/id"))).Container;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Cosmos DB init attempt {attempts} failed (non-fatal): {ex.Message}");
					await Task.Delay(TimeSpan.FromSeconds(Math.Min(8, attempts * 2)), CancellationToken.None);
				}
			}
			if (db is null)
			{
				// Give up and fall back to artifacts
				cosmos = null;
			}
		}

		// Ensure reviews container exists WITH VECTOR POLICY
		if (cosmos is not null)
		{
			try
			{
			var embeddings = new List<Microsoft.Azure.Cosmos.Embedding>
			{
				new Microsoft.Azure.Cosmos.Embedding
				{
					Path = vectorPath,
					DataType = VectorDataType.Float32,
					DistanceFunction = distance == "euclidean" ? DistanceFunction.Euclidean :
									   (distance == "dotproduct" || distance == "dot_product") ? DistanceFunction.DotProduct :
									   DistanceFunction.Cosine,
					Dimensions = dims
				}
			};

			var props = new ContainerProperties(reviewsContainer, "/id")
			{
				VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new System.Collections.ObjectModel.Collection<Microsoft.Azure.Cosmos.Embedding>(embeddings)),
				IndexingPolicy = new IndexingPolicy
				{
					IncludedPaths = { new IncludedPath { Path = "/*" } },
					ExcludedPaths = { new ExcludedPath { Path = $"{vectorPath}/*" } },
					VectorIndexes =
					{
						new VectorIndexPath
						{
							Path = vectorPath,
							Type = indexType == "flat" ? VectorIndexType.Flat :
								   (indexType == "quantizedflat" || indexType == "quantized_flat") ? VectorIndexType.QuantizedFlat :
								   VectorIndexType.DiskANN
						}
					}
				}
			};

				reviews = (await db!.CreateContainerIfNotExistsAsync(props)).Container;
			}
			catch (Exception)
			{
				// Fallback to existing container if available
				reviews = cosmos.GetContainer(dbName ?? "actualgames", reviewsContainer);
			}
		}

		// Select a few Steam app IDs
		var appIds = new[] { 620, 570, 440 }; // Portal 2, Dota 2, Team Fortress 2
	var seededGames = new List<dynamic>();
	var seededReviews = new List<Dictionary<string, object?>>();

		foreach (var appId in appIds)
		{
			var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=en";
			try
			{
				using var resp = await http.GetAsync(url);
				if (!resp.IsSuccessStatusCode) continue;
				var payload = await resp.Content.ReadFromJsonAsync<Dictionary<string, AppDetailsResponse>>();
				if (payload is null || !payload.TryGetValue(appId.ToString(), out var entry) || entry is null || entry.data is null) continue;
				var dapp = entry.data;

				var gameDoc = new
				{
					id = dapp.steam_appid?.ToString() ?? appId.ToString(),
					title = dapp.name ?? $"App {appId}",
					tagSummary = (dapp.genres ?? Array.Empty<SimpleNamed>()).Select(g => g.name ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).Take(8).ToArray(),
					reviewCount = dapp.recommendations?.total ?? 0,
					price = dapp.price_overview?.final,
					header = dapp.header_image,
					when = dapp.release_date?.date
				};
				if (games is not null)
				{
					await games.UpsertItemAsync(gameDoc, new PartitionKey(gameDoc.id));
				}
				seededGames.Add(gameDoc);


				// Prefer real user reviews when available; fallback to review-like snippets from the app description
				var realReviews = await TryFetchSteamReviewsAsync(http, appId, max: 3);
				var texts = realReviews.Count > 0 ? realReviews.Select(r => r.text).ToList() : BuildReviewLikeSnippets(dapp);
				if (texts.Count == 0) continue;

				var vectors = await GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, texts, dims);

				for (int i = 0; i < texts.Count; i++)
				{
					var reviewId = Guid.NewGuid().ToString("n");
					var vec = vectors[i].ToArray();
					var helpful = realReviews.Count > 0 ? realReviews[i].votesUp : 0;
					var created = realReviews.Count > 0 ? realReviews[i].createdAt.ToString("O") : DateTimeOffset.UtcNow.ToString("O");
					var reviewDoc = new Dictionary<string, object?>
					{
						["id"] = reviewId,
						["gameId"] = gameDoc.id,
						["gameTitle"] = gameDoc.title,
						["excerpt"] = texts[i],
						["helpfulVotes"] = helpful,
						["createdAt"] = created,
						["vector"] = vec,
					};
					if (reviews is not null)
					{
						await reviews.UpsertItemAsync(reviewDoc, new PartitionKey(reviewId));
					}
					else
					{
						// keep a trimmed copy for artifacts
						seededReviews.Add(new Dictionary<string, object?>
						{
							["id"] = reviewId,
							["gameId"] = gameDoc.id,
							["gameTitle"] = gameDoc.title,
							["excerpt"] = texts[i],
							["helpfulVotes"] = helpful,
							["createdAt"] = created,
							["vectorDims"] = vec.Length
						});
					}
				}
			}
			catch
			{
				// ignore network hiccups; continue
			}
		}

		Console.WriteLine($"Seeded {seededGames.Count} games and corresponding review snippets.");
		if (cosmos is null)
		{
			// Write artifacts to disk
			var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AI-Agent-Workspace", "Artifacts");
			Directory.CreateDirectory(outDir);
			var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
			await File.WriteAllTextAsync(Path.Combine(outDir, $"games-{stamp}.json"), System.Text.Json.JsonSerializer.Serialize(seededGames));
			if (seededReviews.Count > 0)
			{
				await File.WriteAllTextAsync(Path.Combine(outDir, $"reviews-{stamp}.json"), System.Text.Json.JsonSerializer.Serialize(seededReviews));
			}
			Console.WriteLine($"Wrote games seed to {outDir}");
		}

		// Smoke vector search
	if (seededGames.Count > 0 && reviews is not null)
		{
			try
			{
				var probe = "co-op puzzle with portals and witty writing";
				var vec = (await GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, new List<string> { probe }, dims)).First();
				// Cosmos Emulator currently supports 2-arg VectorDistance(vector, vector) when a vector policy defines the metric.
				var sql = "SELECT TOP 5 c.id, c.gameTitle, VectorDistance(c.vector, @e) AS sim FROM c ORDER BY VectorDistance(c.vector, @e)";
				var q = new QueryDefinition(sql).WithParameter("@e", vec);
				using var it = reviews.GetItemQueryIterator<dynamic>(q);
				Console.WriteLine("Top 5 semantic matches for probe:");
				while (it.HasMoreResults)
				{
					var page = await it.ReadNextAsync();
					foreach (var doc in page)
					{
						Console.WriteLine($"  {doc.gameTitle}  sim={doc.sim}");
					}
				}
			}
			catch (CosmosException ex)
			{
				Console.WriteLine($"Vector query failed: {ex.Message}. Ensure the reviews container has a vector policy and index.");
			}
		}
	}

	// Minimal DTOs for Steam response (subset)
	private record AppDetailsResponse(bool success, SteamAppDetails? data);
	private record SteamAppDetails(
		int? steam_appid,
		string? name,
		string? short_description,
		string? detailed_description,
		PriceOverview? price_overview,
		ReleaseDate? release_date,
		SimpleNamed[]? categories,
		SimpleNamed[]? genres,
		SteamRecommendations? recommendations,
		string? header_image
	);

	private record SteamRecommendations(int? total);
	private record PriceOverview(int? final, string? currency);
	private record ReleaseDate(string? date, bool? coming_soon);
	private record SimpleNamed(int? id, string? name, string? description);

	private static List<string> BuildReviewLikeSnippets(SteamAppDetails d)
	{
		var snippets = new List<string>();
		if (!string.IsNullOrWhiteSpace(d.short_description)) snippets.Add(d.short_description!);
		if (!string.IsNullOrWhiteSpace(d.detailed_description))
		{
			var cleaned = StripHtml(d.detailed_description!);
			if (cleaned.Length > 200) cleaned = cleaned[..200];
			snippets.Add(cleaned);
		}
		return snippets.Distinct().Take(2).ToList();
	}

	private static async Task<List<SteamUserReview>> TryFetchSteamReviewsAsync(HttpClient http, int appId, int max)
	{
		try
		{
			var url = $"https://store.steampowered.com/appreviews/{appId}?json=1&filter=recent&language=english&purchase_type=all&num_per_page={max}";
			using var resp = await http.GetAsync(url);
			if (!resp.IsSuccessStatusCode) return new();
			var payload = await resp.Content.ReadFromJsonAsync<SteamReviewsResponse>();
			if (payload?.reviews == null || payload.reviews.Length == 0) return new();
			var outList = new List<SteamUserReview>();
			foreach (var r in payload.reviews.Take(max))
			{
				if (string.IsNullOrWhiteSpace(r.review)) continue;
				var created = DateTimeOffset.FromUnixTimeSeconds(r.timestamp_created);
				outList.Add(new SteamUserReview(r.recommendationid ?? Guid.NewGuid().ToString("n"), r.review, r.votes_up, created));
			}
			return outList;
		}
		catch
		{
			return new();
		}
	}

	private record SteamReviewsResponse(string? success, SteamReviewItem[] reviews);
	private record SteamReviewItem(string? recommendationid, string review, int votes_up, int votes_funny, long timestamp_created);
	private record SteamUserReview(string id, string text, int votesUp, DateTimeOffset createdAt);
	private static string StripHtml(string html)
	{
		var arr = new char[html.Length];
		int idx = 0; bool inside = false;
		foreach (var ch in html)
		{
			if (ch == '<') { inside = true; continue; }
			if (ch == '>') { inside = false; continue; }
			if (!inside) arr[idx++] = ch;
		}
		return new string(arr, 0, idx).Replace("\n", " ").Replace("\r", " ").Trim();
	}

	private static float[] DeterministicVector(string text, int dims)
	{
		// Simple non-crypto hash-based embedding to allow offline runs
		var bytes = Encoding.UTF8.GetBytes(text);
		var vec = new float[dims];
		for (int i = 0; i < bytes.Length; i++)
		{
			vec[i % dims] += (bytes[i] % 23) / 23.0f;
		}
		// L2 normalize
		var norm = MathF.Sqrt(vec.Sum(v => v * v));
		if (norm > 0)
		{
			for (int i = 0; i < vec.Length; i++) vec[i] /= norm;
		}
		return vec;
	}

	private static async Task<List<float[]>> GenerateVectorsAsync(HttpClient http, string ollamaEndpoint, string model, List<string> texts, int dims)
	{
		// Try multiple compatible Ollama endpoints/shapes
		async Task<(bool ok, List<float[]> vecs)> TryCallAsync(string path, object payload, Func<System.Text.Json.JsonElement, List<float[]>> projector)
		{
			try
			{
				var url = new Uri(new Uri(ollamaEndpoint), path);
				var body = System.Text.Json.JsonSerializer.Serialize(payload);
				using var req = new HttpRequestMessage(HttpMethod.Post, url)
				{
					Content = new StringContent(body, Encoding.UTF8, "application/json")
				};
				using var resp = await http.SendAsync(req);
				if (!resp.IsSuccessStatusCode) return (false, new());
				using var stream = await resp.Content.ReadAsStreamAsync();
				using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
				var list = projector(doc.RootElement);
				if (list.Count == texts.Count) return (true, list);
				return (false, new());
			}
			catch { return (false, new()); }
		}

		// Projectors for different API shapes
		static List<float[]> ProjectEmbeddingsData(System.Text.Json.JsonElement root)
		{
			var list = new List<float[]>();
			if (root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Array)
			{
				foreach (var item in data.EnumerateArray())
				{
					if (item.TryGetProperty("embedding", out var emb) && emb.ValueKind == System.Text.Json.JsonValueKind.Array)
					{
						list.Add(emb.EnumerateArray().Select(x => x.GetSingle()).ToArray());
					}
				}
			}
			return list;
		}

		static List<float[]> ProjectEmbedArray(System.Text.Json.JsonElement root)
		{
			// Some endpoints return { "embeddings": [[...], [...]] }
			var list = new List<float[]>();
			if (root.TryGetProperty("embeddings", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
			{
				foreach (var emb in arr.EnumerateArray())
				{
					if (emb.ValueKind == System.Text.Json.JsonValueKind.Array)
						list.Add(emb.EnumerateArray().Select(x => x.GetSingle()).ToArray());
				}
			}
			return list;
		}

		// 1) Ollama /api/embeddings with "input"
		var t1 = await TryCallAsync("/api/embeddings", new { model, input = texts }, ProjectEmbeddingsData);
		if (t1.ok)
		{
			Console.WriteLine($"Ollama embeddings via /api/embeddings input (dims={t1.vecs.FirstOrDefault()?.Length ?? 0})");
			return t1.vecs;
		}
		// 2) Ollama /api/embeddings with "prompt" (per model docs)
		var t2 = await TryCallAsync("/api/embeddings", new { model, prompt = texts }, ProjectEmbeddingsData);
		if (t2.ok)
		{
			Console.WriteLine($"Ollama embeddings via /api/embeddings prompt (dims={t2.vecs.FirstOrDefault()?.Length ?? 0})");
			return t2.vecs;
		}
		// 3) Ollama /api/embed with "input"
		var t3 = await TryCallAsync("/api/embed", new { model, input = texts }, ProjectEmbedArray);
		if (t3.ok)
		{
			Console.WriteLine($"Ollama embeddings via /api/embed (dims={t3.vecs.FirstOrDefault()?.Length ?? 0})");
			return t3.vecs;
		}

		// Fallback deterministic embeddings
		Console.WriteLine("Using deterministic fallback embeddings.");
		return texts.Select(t => DeterministicVector(t, dims)).ToList();
	}
}
