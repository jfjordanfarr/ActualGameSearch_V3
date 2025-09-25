using System;
using System.Linq;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Diagnostics.Metrics;
using ActualGameSearch.Worker.Embeddings;
using ActualGameSearch.Worker.Models;
using ActualGameSearch.Worker.Probes;
using Microsoft.Azure.Cosmos;
using ActualGameSearch.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ActualGameSearch.Core.Services;
using ActualGameSearch.Worker.Services;
using ActualGameSearch.Worker.Ingestion;
using ActualGameSearch.Worker.Storage;

namespace ActualGameSearch.Worker;

internal class Program
{
	public static async Task Main(string[] args)
	{
	var builder = Host.CreateApplicationBuilder(args);
	builder.AddServiceDefaults();

	// Register Steam client for DI
	builder.Services.AddSingleton<ISteamClient, SteamHttpClient>();

		// Config defaults (can be overridden via appsettings or env)
		var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434/";
		if (!ollamaEndpoint.EndsWith('/')) ollamaEndpoint += "/";
		var ollamaModel = builder.Configuration["Ollama:Model"] ?? "nomic-embed-text:v1.5";
		var cosmosConn = builder.Configuration.GetConnectionString("cosmos-db");
		var dbName = builder.Configuration["Cosmos:Database"] ?? "actualgames";
		var gamesContainer = builder.Configuration["Cosmos:GamesContainer"] ?? "games";
		var reviewsContainer = builder.Configuration["Cosmos:ReviewsContainer"] ?? "reviews";
		var patchNotesContainer = builder.Configuration["Cosmos:PatchNotesContainer"] ?? "patchnotes";
	var vectorPath = builder.Configuration["Cosmos:Vector:Path"] ?? "/vector";
	var gamesVectorPath = builder.Configuration["Cosmos:GamesVector:Path"] ?? "/gvector";
	var patchVectorPath = builder.Configuration["Cosmos:PatchVector:Path"] ?? "/pvector";
		var dims = int.TryParse(builder.Configuration["Cosmos:Vector:Dimensions"], out var d) ? d : 768;
	var distance = (builder.Configuration["Cosmos:Vector:DistanceFunction"] ?? "cosine").ToLowerInvariant();
	var indexType = (builder.Configuration["Cosmos:Vector:IndexType"] ?? "diskann").ToLowerInvariant();
	var formPref = CosmosVectorQueryHelper.ParsePreference(builder.Configuration["Cosmos:Vector:DistanceForm"]);
	var maxReviewsPerGame = int.TryParse(builder.Configuration["Seeding:MaxReviewsPerGame"], out var mrg) ? mrg : 200;
	var failOnNoReviews = builder.Configuration.GetValue<bool>("Seeding:FailOnNoReviews", true);
	var minReviewCount = int.TryParse(builder.Configuration["Seeding:MinReviewCount"], out var mrc) ? mrc : 10; // legacy, superseded by MinQualifiedReviews
	var minQualifiedReviews = int.TryParse(builder.Configuration["Seeding:MinQualifiedReviews"], out var mqr) ? mqr : 5;
	var minUniqueWordsPerReview = int.TryParse(builder.Configuration["Seeding:MinUniqueWordsPerReview"], out var muw) ? muw : 20;
	var requireSteamPurchase = builder.Configuration.GetValue<bool>("Seeding:RequireSteamPurchase", true);
	var allowDeterministicFallback = builder.Configuration.GetValue<bool>("Embeddings:AllowDeterministicFallback", false);
	var embNumCtx = int.TryParse(builder.Configuration["Embeddings:NumCtx"], out var nc) ? nc : 2048;
	var embMaxBatch = int.TryParse(builder.Configuration["Embeddings:MaxBatch"], out var mb) ? mb : 64;
	var embHttpTimeoutSeconds = int.TryParse(builder.Configuration["Embeddings:HttpTimeoutSeconds"], out var ts) ? ts : 180;
	var samplingEnabled = builder.Configuration.GetValue<bool>("Sampling:Enabled", true);
	var samplingCount = int.TryParse(builder.Configuration["Sampling:Count"], out var sc) ? sc : 50;
	var patchIngestEnabled = builder.Configuration.GetValue<bool>("PatchNotes:Enabled", true);
	var maxPatchNotesPerGame = int.TryParse(builder.Configuration["PatchNotes:MaxPerGame"], out var mpn) ? mpn : 10;

	// Services
	builder.Services.AddHttpClient();
	// Let Aspire wire CosmosClient via service discovery
	builder.AddAzureCosmosClient("cosmos-db");

		// Delay Cosmos client creation to runtime; we'll try to connect and otherwise fall back to writing JSON artifacts.

		using var host = builder.Build();

	var http = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
	var steamHttp = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("steam");
		http.Timeout = TimeSpan.FromSeconds(Math.Clamp(embHttpTimeoutSeconds, 30, 600));
		// Set a friendly User-Agent and Accept to avoid being blocked or served atypical responses
		try
		{
			http.DefaultRequestHeaders.UserAgent.ParseAdd("ActualGameSearch/1.0 (+https://actualgamesearch.com)");
			http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
		}
		catch { /* headers may be set already in some hosts */ }

		// Attempt to pre-pull the embedding model in the Ollama container (best-effort)
		try
		{
				var pullUrl = new Uri(new Uri(ollamaEndpoint), "api/pull");
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
		Container? patchnotes = null;
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
					// Ensure games container exists WITH VECTOR POLICY for game embeddings
					var gameEmbeddings = new List<Microsoft.Azure.Cosmos.Embedding>
					{
						new Microsoft.Azure.Cosmos.Embedding
						{
							Path = gamesVectorPath,
							DataType = VectorDataType.Float32,
							DistanceFunction = distance == "euclidean" ? DistanceFunction.Euclidean :
											   (distance == "dotproduct" || distance == "dot_product") ? DistanceFunction.DotProduct :
											   DistanceFunction.Cosine,
							Dimensions = dims
						}
					};

					var gamesProps = new ContainerProperties(gamesContainer, "/id")
					{
						VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new System.Collections.ObjectModel.Collection<Microsoft.Azure.Cosmos.Embedding>(gameEmbeddings)),
						IndexingPolicy = new IndexingPolicy
						{
							IncludedPaths = { new IncludedPath { Path = "/*" } },
							ExcludedPaths = { new ExcludedPath { Path = $"{gamesVectorPath}/*" } },
							VectorIndexes =
							{
								new VectorIndexPath
								{
									Path = gamesVectorPath,
									Type = indexType == "flat" ? VectorIndexType.Flat :
										   (indexType == "quantizedflat" || indexType == "quantized_flat") ? VectorIndexType.QuantizedFlat :
										   VectorIndexType.DiskANN
								}
							}
						}
					};

					games = (await db.CreateContainerIfNotExistsAsync(gamesProps)).Container;
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

		// Ensure patchnotes container exists WITH VECTOR POLICY
		if (cosmos is not null)
		{
			try
			{
				var pembeddings = new List<Microsoft.Azure.Cosmos.Embedding>
				{
					new Microsoft.Azure.Cosmos.Embedding
					{
						Path = patchVectorPath,
						DataType = VectorDataType.Float32,
						DistanceFunction = distance == "euclidean" ? DistanceFunction.Euclidean :
										   (distance == "dotproduct" || distance == "dot_product") ? DistanceFunction.DotProduct :
										   DistanceFunction.Cosine,
						Dimensions = dims
					}
				};

				var pprops = new ContainerProperties(patchNotesContainer, "/id")
				{
					VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new System.Collections.ObjectModel.Collection<Microsoft.Azure.Cosmos.Embedding>(pembeddings)),
					IndexingPolicy = new IndexingPolicy
					{
						IncludedPaths = { new IncludedPath { Path = "/*" } },
						ExcludedPaths = { new ExcludedPath { Path = $"{patchVectorPath}/*" } },
						VectorIndexes =
						{
							new VectorIndexPath
							{
								Path = patchVectorPath,
								Type = indexType == "flat" ? VectorIndexType.Flat :
									   (indexType == "quantizedflat" || indexType == "quantized_flat") ? VectorIndexType.QuantizedFlat :
									   VectorIndexType.DiskANN
							}
						}
					}
				};

				patchnotes = (await db!.CreateContainerIfNotExistsAsync(pprops)).Container;
			}
			catch (Exception)
			{
				patchnotes = cosmos.GetContainer(dbName ?? "actualgames", patchNotesContainer);
			}
		}

		// Minimal bronze sample ingestion path (quick demo) before metrics setup
		if (args.Length >= 2 && args[0] == "ingest" && args[1] == "bronze-reviews-sample")
		{
			var steam = host.Services.GetRequiredService<ISteamClient>();
			var dataRoot = builder.Configuration["DataLake:Root"] ?? "AI-Agent-Workspace/Artifacts/DataLake";
			var cap = int.TryParse(builder.Configuration["Ingestion:ReviewCapBronze"], out var rc) ? rc : 10;
			var runId = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
			var today = DateTime.UtcNow.Date;
			var mw = new ManifestWriter(dataRoot, runId, "bronze", new Dictionary<string, string>
			{
				["reviewCapBronze"] = cap.ToString(),
				["mode"] = "sample"
			});
			mw.Start();
			var ingestor = new BronzeReviewIngestor(steam, dataRoot, cap);
			var storeIngestor = new BronzeStoreIngestor(steam, dataRoot);
			var newsIngestor = new BronzeNewsIngestor(steam, dataRoot);
			int[] sampleApps = new[] { 620, 570, 440 };
			foreach (var appId in sampleApps)
			{
				try
				{
					var written = await ingestor.IngestReviewsAsync(appId, runId, today);
					mw.RecordItem($"reviews:{appId}", written);

					var storeWritten = await storeIngestor.IngestStoreAsync(appId, runId, today);
					mw.RecordItem($"store:{appId}", storeWritten);

					var newsWritten = await newsIngestor.IngestNewsAsync(appId, runId, today, count: 10, tags: "patchnotes");
					mw.RecordItem($"news:{appId}", newsWritten);
				}
				catch (Exception ex)
				{
					mw.RecordError(ex);
				}
			}
			mw.Finish();
			await mw.SaveAsync();
			Console.WriteLine($"Bronze sample complete. Manifest: {DataLakePaths.Bronze.Manifest(dataRoot, runId)}");
			return;
		}

		// Establish metrics
		var meter = new Meter("ActualGameSearch.Worker", "1.0.0");
		var appsProcessed = meter.CreateCounter<long>("etl.apps_processed");
		var reviewsIngested = meter.CreateCounter<long>("etl.reviews_ingested");
		var patchnotesIngested = meter.CreateCounter<long>("etl.patchnotes_ingested");
		var embeddingFailures = meter.CreateCounter<long>("etl.embedding_failures");
		var appErrors = meter.CreateCounter<long>("etl.app_errors");

		// Select Steam app IDs (random sample or defaults)
		int[] appIds;
		if (samplingEnabled)
		{
			try
			{
				var listResp = await steamHttp.GetFromJsonAsync<SteamAppListResponse>("https://api.steampowered.com/ISteamApps/GetAppList/v2/");
				var all = listResp?.applist?.apps?.Select(a => a.appid).Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
				var rng = Random.Shared;
				var sample = all.OrderBy(_ => rng.Next()).Take(Math.Clamp(samplingCount, 3, 1000)).ToArray();
				// Prepend a small set of popular app IDs to ensure we ingest some data even if random sample is sparse
				int[] preferred = new[] { 620, 570, 440, 730, 292030, 271590, 1172470 };
				appIds = preferred.Concat(sample).Distinct().ToArray();
			}
			catch
			{
				appIds = new[] { 620, 570, 440 };
			}
		}
		else
		{
			appIds = new[] { 620, 570, 440 }; // small default set
		}
	var seededGames = new List<Dictionary<string, object?>>();
	var seededReviews = new List<Dictionary<string, object?>>();

		foreach (var appId in appIds)
		{
			try
			{
				// Fetch app details (resilient): skip app if Steam API fails temporarily
				var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=en";
				using var resp = await steamHttp.GetAsync(url);
				if (!resp.IsSuccessStatusCode) 
				{
					Console.Error.WriteLine($"Skipping app {appId} – Steam appdetails API returned HTTP {(int)resp.StatusCode}. This may be temporary.");
					continue; // Skip this app instead of crashing
				}
				var rawDetails = await resp.Content.ReadAsStringAsync();
				Dictionary<string, AppDetailsResponse>? payload = null;
				try { payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, AppDetailsResponse>>(rawDetails); }
				catch (Exception jex)
				{
					Console.Error.WriteLine($"Failed to parse appdetails for app {appId}. Raw: {Truncate(rawDetails, 600)}\n{jex}");
					continue; // skip this app, do not fail the whole run
				}
				if (payload is null || !payload.TryGetValue(appId.ToString(), out var entry) || entry is null || entry.data is null || entry.IsSuccess == false)
				{
					Console.Error.WriteLine($"Skipping app {appId} – appdetails unsuccessful or missing data. Raw: {Truncate(rawDetails, 300)}");
					continue;
				}
				var dapp = entry.data;

				var gameId = dapp.steam_appid?.ToString() ?? appId.ToString();
				var tagsGenres = (dapp.genres ?? Array.Empty<SimpleNamed>()).Select(g => g.name ?? string.Empty);
				var tagsCats = (dapp.categories ?? Array.Empty<SimpleNamed>()).Select(c => c.name ?? string.Empty);
				var tagSummary = tagsGenres.Where(s => !string.IsNullOrWhiteSpace(s)).Take(8).ToList();
				if (tagSummary.Count == 0)
				{
					// Fallback to categories if genres missing
					tagSummary = tagsCats.Where(s => !string.IsNullOrWhiteSpace(s)).Take(8).ToList();
				}

				var type = dapp.type ?? "game";
				// Keep Steam's reported total for metadata only; do not gate on this (it's often missing for DLC/older titles).
				var appReviewCount = dapp.recommendations?.total ?? 0;

				var gameDoc = new Dictionary<string, object?>
				{
					["id"] = gameId,
					["title"] = dapp.name ?? $"App {appId}",
					["tagSummary"] = tagSummary.ToArray(),
					["reviewCount"] = appReviewCount,
					["price"] = dapp.price_overview?.final,
					["header"] = dapp.header_image,
					["when"] = dapp.release_date?.date,
					["type"] = type
				};

				// Precompute game embedding but DEFER game upsert until at least one review is persisted
				float[]? pendingGameVector = null;
				if (games is not null)
				{
					var cleanDetailed = !string.IsNullOrWhiteSpace(dapp.detailed_description) ? EmbeddingUtils.StripHtml(dapp.detailed_description!) : string.Empty;
					var combinedDesc = string.Join("\n\n", new[] { dapp.short_description, cleanDetailed }.Where(s => !string.IsNullOrWhiteSpace(s))!);
					if (!string.IsNullOrWhiteSpace(combinedDesc))
					{
						pendingGameVector = (await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, new List<string> { combinedDesc }, dims, allowDeterministicFallback, embNumCtx, embMaxBatch)).First();
					}
				}

				// Strict: fetch real reviews; then filter to qualified ones before embedding/persisting
				var realReviews = await TryFetchSteamReviewsPaginatedAsync(steamHttp, appId, maxReviewsPerGame);
				// Require a minimum total number of real reviews overall (independent of quality filter)
				if (realReviews.Count < Math.Max(minReviewCount, 1))
				{
					Console.WriteLine($"Skipping app {appId} ('{dapp.name ?? "unknown"}') – total fetched reviews {realReviews.Count} < minimum overall {minReviewCount}.");
					continue;
				}
				// Apply quality gating
				var qualified = realReviews
					.Where(r => (!requireSteamPurchase || r.steamPurchase) && IsTextQualified(r.text, minUniqueWordsPerReview))
					.ToList();
				if (qualified.Count < Math.Max(minQualifiedReviews, 1))
				{
					Console.WriteLine($"Skipping app {appId} ('{dapp.name ?? "unknown"}') – qualified reviews {qualified.Count} < threshold {minQualifiedReviews} (minUniqueWords={minUniqueWordsPerReview}, requireSteamPurchase={requireSteamPurchase}).");
					continue;
				}

				// Prefer the most helpful and substantial reviews up to maxReviewsPerGame
				var picked = qualified
					.OrderByDescending(r => r.votesUp)
					.ThenByDescending(r => r.votesFunny)
					.ThenByDescending(r => r.text?.Length ?? 0)
					.Take(Math.Clamp(maxReviewsPerGame, 1, 1000))
					.ToList();

				var texts = picked.Select(r => r.text).ToList();
				List<float[]> vectors;
				try { vectors = await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, texts, dims, allowDeterministicFallback, embNumCtx, embMaxBatch); }
				catch { embeddingFailures.Add(1, new KeyValuePair<string, object?>[] { new("stage", "reviews") }); throw; }

				var reviewsWritten = 0;
				for (int i = 0; i < texts.Count; i++)
				{
					var reviewId = Guid.NewGuid().ToString("n");
					var vec = vectors[i].ToArray();
					var r = picked[i];
					var created = r.createdAt.ToString("O");
					var reviewDoc = new Dictionary<string, object?>
					{
						["id"] = reviewId,
						["gameId"] = gameId,
						["gameTitle"] = gameDoc["title"],
						["source"] = "steam_review",
						["lang"] = r.lang,
						["fullText"] = texts[i],
						["excerpt"] = texts[i].Length > 240 ? texts[i][..240] : texts[i],
						["helpfulVotes"] = r.votesUp,
						["votesFunny"] = r.votesFunny,
						["recommended"] = r.votedUp,
						["purchaseType"] = r.steamPurchase ? "steam" : (r.receivedForFree ? "gift" : "other"),
						["steamReviewId"] = r.id,
						["createdAt"] = created,
						["vector"] = vec,
					};
					if (reviews is not null)
					{
						await reviews.UpsertItemAsync(reviewDoc, new PartitionKey(reviewId));
						reviewsWritten++;
					}
					else
					{
						// keep a trimmed copy for artifacts
						seededReviews.Add(new Dictionary<string, object?>
						{
							["id"] = reviewId,
							["gameId"] = gameId,
							["gameTitle"] = gameDoc["title"],
							["excerpt"] = texts[i],
							["helpfulVotes"] = r.votesUp,
							["createdAt"] = created,
							["vectorDims"] = vec.Length
						});
					}
					reviewsIngested.Add(1, new KeyValuePair<string, object?>[] { new("lang", r.lang ?? "unknown") });
				}

				// Only persist the game and patch notes if we wrote at least the qualified threshold
				if (reviewsWritten >= minQualifiedReviews && games is not null)
				{
					if (pendingGameVector is not null)
					{
						gameDoc[gamesVectorPath.TrimStart('/')] = pendingGameVector.ToArray();
					}
					// Reflect actual qualified count
					gameDoc["fetchedReviewCount"] = reviewsWritten;
					await games.UpsertItemAsync(gameDoc, new PartitionKey(gameId));
					seededGames.Add(gameDoc);
					appsProcessed.Add(1);
				}

				// Patch notes ingestion (optional)
				if (patchIngestEnabled && patchnotes is not null && reviewsWritten >= minQualifiedReviews)
				{
					try
					{
						var purl = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={appId}&count={maxPatchNotesPerGame}&tags=patchnotes";
						using var presp = await steamHttp.GetAsync(purl);
						if (presp.IsSuccessStatusCode)
						{
							var pd = await presp.Content.ReadFromJsonAsync<SteamNewsResponse>();
							var items = pd?.appnews?.newsitems ?? Array.Empty<SteamNewsItem>();
							if (items.Length > 0)
							{
								var ptexts = items.Select(i => EmbeddingUtils.StripHtml(i.contents ?? string.Empty)).ToList();
								List<float[]> pvecs;
								try { pvecs = await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, ptexts, dims, allowDeterministicFallback); }
								catch { embeddingFailures.Add(1, new KeyValuePair<string, object?>[] { new("stage", "patchnotes") }); throw; }
								for (int i = 0; i < items.Length && i < maxPatchNotesPerGame; i++)
								{
									var it = items[i];
									var pid = Guid.NewGuid().ToString("n");
									var published = DateTimeOffset.FromUnixTimeSeconds(it.date).ToString("O");
									var pdoc = new Dictionary<string, object?>
									{
										["id"] = pid,
										["gameId"] = gameId,
										["gameTitle"] = gameDoc["title"],
										["title"] = it.title,
										["publishedAt"] = published,
										["excerpt"] = ptexts[i].Length > 240 ? ptexts[i][..240] : ptexts[i],
										[patchVectorPath.TrimStart('/')] = pvecs[i].ToArray()
									};
									await patchnotes.UpsertItemAsync(pdoc, new PartitionKey(pid));
									patchnotesIngested.Add(1);
								}
							}
						}
					}
					catch { /* ignore patch failures per app; non-fatal */ }
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"ETL failed for app {appId}: {ex.Message}");
				// Don't crash the entire ETL process - just skip this app and continue
				appErrors.Add(1, new KeyValuePair<string, object?>[] { 
					new("app_id", appId),
					new("error_type", ex.GetType().Name),
					new("stage", "app_processing")
				});
				continue; // Skip this app but continue with the next one
			}
		}

		Console.WriteLine($"Seeded {seededGames.Count} games and corresponding reviews.");
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
			var probe = "co-op puzzle with portals and witty writing";
				var vec = (await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, new List<string> { probe }, dims, allowDeterministicFallback, embNumCtx, embMaxBatch)).First();
			await VectorProbe.TryProbeAsync(reviews, dbName!, reviewsContainer!, distance, vec, formPref, fieldPath: vectorPath);
		}
	}

	// Intentionally removed any snippet fallback; strict ETL requires real reviews.

	// Legacy single-page fetch (unused); keep for reference but map to full shape if used
	private static async Task<List<SteamUserReview>> TryFetchSteamReviewsAsync(HttpClient http, int appId, int max)
	{
		try
		{
			// Fetch ALL languages in a single pass (multilingual); do not iterate per-language
			var url = $"https://store.steampowered.com/appreviews/{appId}?json=1&filter=recent&language=all&purchase_type=all&num_per_page={max}";
			using var resp = await http.GetAsync(url);
			if (!resp.IsSuccessStatusCode) return new();
			var payload = await resp.Content.ReadFromJsonAsync<SteamReviewsResponse>();
			if (payload?.reviews == null || payload.reviews.Length == 0) return new();
			var outList = new List<SteamUserReview>();
			foreach (var r in payload.reviews.Take(max))
			{
				if (string.IsNullOrWhiteSpace(r.review)) continue;
				var created = DateTimeOffset.FromUnixTimeSeconds(r.timestamp_created);
				string? weighted = null;
				if (r.weighted_vote_score.HasValue)
				{
					var el = r.weighted_vote_score.Value;
					if (el.ValueKind == System.Text.Json.JsonValueKind.String) weighted = el.GetString();
					else if (el.ValueKind == System.Text.Json.JsonValueKind.Number) weighted = el.GetDouble().ToString("G");
				}
				outList.Add(new SteamUserReview(
					id: r.recommendationid ?? Guid.NewGuid().ToString("n"),
					text: r.review,
					votesUp: r.votes_up,
					votesFunny: r.votes_funny,
					createdAt: created,
					lang: r.language,
					votedUp: r.voted_up,
					steamPurchase: r.steam_purchase,
					receivedForFree: r.received_for_free,
					weightedVoteScore: weighted
				));
			}
			return outList;
		}
		catch
		{
			return new();
		}
	}

	// New: paginate reviews using Steam cursor until reaching max or pages exhausted
	private static async Task<List<SteamUserReview>> TryFetchSteamReviewsPaginatedAsync(HttpClient http, int appId, int maxTotal)
	{
		// Steam's 'filter' supports 'recent' or 'updated' (not 'all'). We'll try both if needed.
		var filtersToTry = new[] { "recent", "updated" };
		foreach (var filter in filtersToTry)
		{
			var results = new List<SteamUserReview>();
			string cursor = "*"; // initial cursor
			int pageSize = Math.Clamp(maxTotal >= 100 ? 100 : maxTotal, 10, 100);
			int pages = 0;
			while (results.Count < maxTotal && pages < 20)
			{
				// Multilingual in a single pass; broaden timeframe via day_range.
					var url =
					$"https://store.steampowered.com/appreviews/{appId}?json=1" +
					$"&filter={filter}" +
					$"&language=all" +
					$"&review_type=all" +
					$"&purchase_type=steam" +
					$"&exclude_inappropriate_content=0" +
						$"&day_range=0" +
					$"&num_per_page={pageSize}" +
					$"&cursor={Uri.EscapeDataString(cursor)}";
				using var resp = await http.GetAsync(url);
				if (!resp.IsSuccessStatusCode)
				{
					throw new HttpRequestException($"Steam appreviews returned status {(int)resp.StatusCode} for app {appId} (filter={filter}).");
				}
				var raw = await resp.Content.ReadAsStringAsync();
				SteamReviewsResponse? payload;
				try
				{
					payload = System.Text.Json.JsonSerializer.Deserialize<SteamReviewsResponse>(raw);
				}
				catch (Exception jex)
				{
					throw new InvalidOperationException($"Failed to parse Steam reviews JSON for app {appId} (filter={filter}). Raw: {Truncate(raw, 800)}", jex);
				}
				if (payload is null)
				{
					throw new InvalidOperationException($"Steam reviews payload was null for app {appId} (filter={filter}). Raw: {Truncate(raw, 400)}");
				}
				if (payload.success != 1)
				{
					throw new InvalidOperationException($"Steam reviews 'success' != 1 for app {appId} (filter={filter}). Raw: {Truncate(raw, 400)}");
				}
				if (payload.reviews == null || payload.reviews.Length == 0)
				{
					Console.Error.WriteLine($"Steam returned 0 reviews (HTTP 200) for app {appId} (filter={filter}, page={pages}, cursor={cursor}). Raw: {Truncate(raw, 400)}");
					break; // no more for this filter
				}
				Console.WriteLine($"Fetched {payload.reviews.Length} reviews for app {appId} (filter={filter}, page={pages+1}).");
				foreach (var r in payload.reviews)
				{
					if (string.IsNullOrWhiteSpace(r.review)) continue;
					var created = DateTimeOffset.FromUnixTimeSeconds(r.timestamp_created);
					string? weighted = null;
					if (r.weighted_vote_score.HasValue)
					{
						var el = r.weighted_vote_score.Value;
						if (el.ValueKind == System.Text.Json.JsonValueKind.String) weighted = el.GetString();
						else if (el.ValueKind == System.Text.Json.JsonValueKind.Number) weighted = el.GetDouble().ToString("G");
					}
					results.Add(new SteamUserReview(
						id: r.recommendationid ?? Guid.NewGuid().ToString("n"),
						text: r.review,
						votesUp: r.votes_up,
						votesFunny: r.votes_funny,
						createdAt: created,
						lang: r.language,
						votedUp: r.voted_up,
						steamPurchase: r.steam_purchase,
						receivedForFree: r.received_for_free,
						weightedVoteScore: weighted
					));
					if (results.Count >= maxTotal) break;
				}
				if (results.Count >= maxTotal) return results; // enough
				if (string.IsNullOrEmpty(payload.cursor) || payload.cursor == cursor) break;
				cursor = payload.cursor;
				pages++;
			}
			if (results.Count > 0) return results; // success with this filter
			// else try next filter
		}
		return new();
	}

	private static string Truncate(string? s, int max)
	{
		if (string.IsNullOrEmpty(s)) return string.Empty;
		if (s!.Length <= max) return s;
		return s.Substring(0, Math.Max(0, max)) + "…";
	}

	// Steam DTOs and embedding helpers moved to separate files.

		// Text quality heuristics
		private static bool IsTextQualified(string? text, int minUniqueWords)
		{
			if (string.IsNullOrWhiteSpace(text)) return false;
			var words = Tokenize(text!);
			if (words.Count == 0) return false;
			return words.Count >= minUniqueWords;
		}

		private static HashSet<string> Tokenize(string text)
		{
			var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var sb = new StringBuilder();
			void Flush()
			{
				if (sb.Length == 0) return;
				var w = sb.ToString();
				if (w.Length >= 2 && w.All(ch => char.IsLetter(ch) || ch == '\'' || ch == '-'))
				{
					// filter trivial stop-like tokens
					if (!StopWords.Contains(w)) set.Add(w);
				}
				sb.Clear();
			}
			foreach (var ch in text)
			{
				if (char.IsLetter(ch) || ch == '\'' || ch == '-') sb.Append(char.ToLowerInvariant(ch));
				else Flush();
			}
			Flush();
			return set;
		}

		private static readonly HashSet<string> StopWords = new(new[]
		{
			"a","an","the","and","or","but","if","then","so","of","on","in","to","for","with","at","by","from","up","down","out","over","under",
			"is","am","are","was","were","be","been","being","it's","its","this","that","these","those","as","it","i","you","he","she","they","we","me","him","her","them","us",
			"my","your","his","hers","their","our","mine","yours","theirs","ours","do","does","did","done","not","no","yes","y","n","gg","ok","okay","cool","nice","good","bad"
		}, StringComparer.OrdinalIgnoreCase);
}
