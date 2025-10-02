using System;
using System.Linq;
using System.IO;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Diagnostics.Metrics;
using ActualGameSearch.Worker.Embeddings;
using ActualGameSearch.Worker.Models;
using ActualGameSearch.Worker.Probes;
using ActualGameSearch.Worker.Configuration;
using Microsoft.Azure.Cosmos;
using ActualGameSearch.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ActualGameSearch.Core.Services;
using ActualGameSearch.Worker.Services;
using ActualGameSearch.Worker.Ingestion;
using ActualGameSearch.Worker.Storage;
using System.Text.Json;

namespace ActualGameSearch.Worker;

internal class Program
{
	private static async Task<int?> GetActualOllamaContextAsync(HttpClient http, string ollamaEndpoint, string model)
	{
		try
		{
			var showUrl = new Uri(new Uri(ollamaEndpoint), "api/show");
			var payload = System.Text.Json.JsonSerializer.Serialize(new { name = model });
			using var req = new HttpRequestMessage(HttpMethod.Post, showUrl)
			{
				Content = new StringContent(payload, Encoding.UTF8, "application/json")
			};
			using var resp = await http.SendAsync(req);
			if (!resp.IsSuccessStatusCode) return null;
			
			var jsonStr = await resp.Content.ReadAsStringAsync();
			using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
			
			// First check the actual runtime parameters (what Ollama is using)
			if (doc.RootElement.TryGetProperty("parameters", out var parameters) && 
				parameters.ValueKind == System.Text.Json.JsonValueKind.String)
			{
				var paramStr = parameters.GetString();
				if (!string.IsNullOrEmpty(paramStr))
				{
					// Look for "num_ctx XXXX" in parameter string
					var match = System.Text.RegularExpressions.Regex.Match(paramStr, @"num_ctx\s+(\d+)");
					if (match.Success && int.TryParse(match.Groups[1].Value, out var numCtx))
					{
						return numCtx;
					}
				}
			}
			
			// Fallback: check model_info for base model limitations (this is often hardcoded to 2048)
			if (doc.RootElement.TryGetProperty("model_info", out var modelInfo))
			{
				// Look for the actual model context limitation
				if (modelInfo.TryGetProperty("nomic-bert.context_length", out var contextProp) && 
					contextProp.TryGetInt32(out var actualContext))
				{
					return actualContext;
				}
				
				// Try other potential context fields as fallbacks
				var contextFields = new[] { "context_length", "n_ctx_train", "context_size", "max_context" };
				foreach (var field in contextFields)
				{
					if (modelInfo.TryGetProperty(field, out var prop) && prop.TryGetInt32(out var context))
						return context;
				}
			}
			
			return null;
		}
		catch 
		{
			return null;
		}
	}

	public static async Task Main(string[] args)
	{
	var builder = Host.CreateApplicationBuilder(args);
	builder.AddServiceDefaults();

	// Make configuration resilient to being run from the solution root by also loading
	// appsettings files from the output base directory (copied via csproj).
	var envName = builder.Environment.EnvironmentName ?? "Production";
	try
	{
		var baseDir = AppContext.BaseDirectory;
		var appsettingsPath = Path.Combine(baseDir, "appsettings.json");
		var envAppsettingsPath = Path.Combine(baseDir, $"appsettings.{envName}.json");
		builder.Configuration.AddJsonFile(appsettingsPath, optional: true, reloadOnChange: true);
		builder.Configuration.AddJsonFile(envAppsettingsPath, optional: true, reloadOnChange: true);
		// Re-apply environment variables and CLI so they override these late-added JSON files
		builder.Configuration.AddEnvironmentVariables();
		builder.Configuration.AddCommandLine(args);
	}
	catch { /* non-fatal */ }

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

	// Lightweight CLI overrides for Ollama without AppHost (e.g., --ollama-endpoint=..., --ollama-model=..., --emb-num-ctx=8192)
	foreach (var a in args)
	{
		if (a.StartsWith("--ollama-endpoint="))
			ollamaEndpoint = a.Substring("--ollama-endpoint=".Length);
		else if (a.StartsWith("--ollama-model="))
			ollamaModel = a.Substring("--ollama-model=".Length);
		else if (a.StartsWith("--emb-num-ctx=") && int.TryParse(a.Substring("--emb-num-ctx=".Length), out var cliNumCtx))
			builder.Configuration["Embeddings:NumCtx"] = cliNumCtx.ToString();
	}
	var maxReviewsPerGame = int.TryParse(builder.Configuration["Seeding:MaxReviewsPerGame"], out var mrg) ? mrg : 200;
	var failOnNoReviews = builder.Configuration.GetValue<bool>("Seeding:FailOnNoReviews", true);
	
	// Candidacy configuration for Bronze tier
	var candidacyOptions = Microsoft.Extensions.Options.Options.Create(new CandidacyOptions 
	{
		MinRecommendationsForInclusion = builder.Configuration.GetValue<int>("Candidacy:Bronze:MinRecommendationsForInclusion", 10),
		MinReviewsForEmbedding = builder.Configuration.GetValue<int>("Candidacy:Bronze:MinReviewsForEmbedding", 20),
		MaxAssociatedAppIds = builder.Configuration.GetValue<int>("Candidacy:Bronze:MaxAssociatedAppIds", 99)
	});
	var minReviewCount = int.TryParse(builder.Configuration["Seeding:MinReviewCount"], out var mrc) ? mrc : 10; // legacy, superseded by MinQualifiedReviews
	var minQualifiedReviews = int.TryParse(builder.Configuration["Seeding:MinQualifiedReviews"], out var mqr) ? mqr : 5;
	var minUniqueWordsPerReview = int.TryParse(builder.Configuration["Seeding:MinUniqueWordsPerReview"], out var muw) ? muw : 20;
	var requireSteamPurchase = builder.Configuration.GetValue<bool>("Seeding:RequireSteamPurchase", true);
	var allowDeterministicFallback = builder.Configuration.GetValue<bool>("Embeddings:AllowDeterministicFallback", false);
	var allowChunking = builder.Configuration.GetValue<bool>("Embeddings:AllowChunking", false);
	var embNumCtx = int.TryParse(builder.Configuration["Embeddings:NumCtx"], out var nc) ? nc : 2048;
	var embMaxBatch = int.TryParse(builder.Configuration["Embeddings:MaxBatch"], out var mb) ? mb : 64;
	var embHttpTimeoutSeconds = int.TryParse(builder.Configuration["Embeddings:HttpTimeoutSeconds"], out var ts) ? ts : 180;
	var samplingEnabled = builder.Configuration.GetValue<bool>("Sampling:Enabled", true);
	var samplingCount = int.TryParse(builder.Configuration["Sampling:Count"], out var sc) ? sc : 50;
	var patchIngestEnabled = builder.Configuration.GetValue<bool>("PatchNotes:Enabled", true);
	var maxPatchNotesPerGame = int.TryParse(builder.Configuration["PatchNotes:MaxPerGame"], out var mpn) ? mpn : 10;
	var requireCosmos = builder.Configuration.GetValue<bool>("Ingestion:RequireCosmos", true);

	// Services
	builder.Services.AddHttpClient();
	// Let Aspire wire CosmosClient via service discovery
	builder.AddAzureCosmosClient("cosmos-db");

		// Delay Cosmos client creation to runtime; we'll try to connect and otherwise fall back to writing JSON artifacts.

		// If a Cosmos connection string is provided, register a CosmosClient for downstream use
		if (!string.IsNullOrWhiteSpace(cosmosConn))
		{
			builder.Services.AddSingleton(new Microsoft.Azure.Cosmos.CosmosClient(cosmosConn));
		}

		using var host = builder.Build();

	// Use a named client tuned for long-running embedding calls
	var http = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
	var steamHttp = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("steam");
		// Safety: still clamp timeout based on configuration in case a host doesn't apply our ServiceDefaults
		http.Timeout = TimeSpan.FromSeconds(Math.Clamp(embHttpTimeoutSeconds, 30, 600));
		// Set a friendly User-Agent and Accept to avoid being blocked or served atypical responses
		try
		{
			http.DefaultRequestHeaders.UserAgent.ParseAdd("ActualGameSearch/1.0 (+https://actualgamesearch.com)");
			http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
		}
		catch { /* headers may be set already in some hosts */ }

		// Ensure the embedding model exists in the Ollama container (best-effort)
		try
		{
			var baseUri = new Uri(ollamaEndpoint);
			// 1) Check if model is present
			var showUrl = new Uri(baseUri, "api/show");
			using (var showReq = new HttpRequestMessage(HttpMethod.Post, showUrl))
			{
				var showBody = System.Text.Json.JsonSerializer.Serialize(new { name = ollamaModel });
				showReq.Content = new StringContent(showBody, Encoding.UTF8, "application/json");
				using var showResp = await http.SendAsync(showReq);
				Console.WriteLine($"Ollama /api/show for '{ollamaModel}' -> {(int)showResp.StatusCode} {showResp.StatusCode}");
				if (showResp.IsSuccessStatusCode)
				{
					// Model already available
					goto AfterModelEnsure;
				}
			}

			// 2) First ensure base model exists (needed for FROM clause)
			var basePullUrl = new Uri(baseUri, "api/pull");
			using (var basePullReq = new HttpRequestMessage(HttpMethod.Post, basePullUrl))
			{
				// Pin to a specific, known-good tag for reproducibility
				var basePullBody = System.Text.Json.JsonSerializer.Serialize(new { name = "nomic-embed-text:v1.5" });
				basePullReq.Content = new StringContent(basePullBody, Encoding.UTF8, "application/json");
				Console.WriteLine("Ollama /api/pull base 'nomic-embed-text:v1.5'...");
				using var basePullResp = await http.SendAsync(basePullReq);
				Console.WriteLine($"Ollama /api/pull -> {(int)basePullResp.StatusCode} {basePullResp.StatusCode}");
				// Continue regardless of success; create may still work
			}

			// 3) Create model via /api/create using inline Modelfile based on nomic-embed-text with desired context
			// Create from explicit v1.5 tag to avoid any retag drift
			var modelfile = $"FROM nomic-embed-text:v1.5\nPARAMETER num_ctx {embNumCtx}\n";
			var createUrl = new Uri(baseUri, "api/create");
			using (var createReq = new HttpRequestMessage(HttpMethod.Post, createUrl))
			{
				var createBody = System.Text.Json.JsonSerializer.Serialize(new { name = ollamaModel, modelfile });
				createReq.Content = new StringContent(createBody, Encoding.UTF8, "application/json");
				Console.WriteLine($"Ollama /api/create '{ollamaModel}' (num_ctx={embNumCtx})...");
				using var createResp = await http.SendAsync(createReq);
				Console.WriteLine($"Ollama /api/create -> {(int)createResp.StatusCode} {createResp.StatusCode}");
				// Best-effort: ignore non-success here; subsequent embedding calls will surface issues
			}

			// Give Ollama a brief moment to register the newly created model before probing
			await Task.Delay(TimeSpan.FromSeconds(1));

			AfterModelEnsure: ;
		}
		catch { /* ignore */ }

		// Proactive readiness check: avoid hammering Steam if embeddings are not actually available yet
		{
			Exception? last = null;
			// Log Ollama server version for debugging environment parity
			try
			{
				var verUrl = new Uri(new Uri(ollamaEndpoint), "api/version");
				using var verResp = await http.GetAsync(verUrl);
				if (verResp.IsSuccessStatusCode)
				{
					var ver = await verResp.Content.ReadAsStringAsync();
					Console.WriteLine($"Ollama server version: {ver}");
				}
			}
			catch { /* non-fatal */ }
			for (int attempt = 1; attempt <= 4; attempt++)
			{
				try
				{
					var health = await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, new List<string> { "healthcheck" }, dims, allowDeterministicFallback: false, numCtx: embNumCtx, maxBatch: 1, allowChunking: allowChunking);
					var hdim = health.FirstOrDefault()?.Length ?? 0;
					Console.WriteLine($"Ollama embedding health OK (dims={hdim}, model='{ollamaModel}', num_ctx={embNumCtx}) on attempt {attempt}.");
					
					// Validate actual context vs requested context to prevent silent semantic truncation
					var actualContext = await GetActualOllamaContextAsync(http, ollamaEndpoint, ollamaModel);
					Console.WriteLine($"DEBUG: actualContext={actualContext}, embNumCtx={embNumCtx}");
					if (actualContext.HasValue && actualContext.Value < embNumCtx)
					{
						Console.Error.WriteLine($"CONTEXT VALIDATION FAILED: Ollama model '{ollamaModel}' is using {actualContext.Value} context but {embNumCtx} was requested.");
						Console.Error.WriteLine($"This would silently truncate ~{100 * (embNumCtx - actualContext.Value) / embNumCtx:F0}% of semantic meaning in embeddings.");
						Console.Error.WriteLine($"Either fix the model context or reduce Embeddings:NumCtx to {actualContext.Value} to acknowledge the limitation.");
						Console.Error.WriteLine($"Aborting to prevent corrupted embeddings.");
						return;
					}
					
					Console.WriteLine($"Context validation passed. Proceeding with ingestion.");
					last = null;
					break;
				}
				catch (Exception ex)
				{
					last = ex;
					Console.Error.WriteLine($"Ollama embedding health check attempt {attempt} failed: {ex.Message}");
					if (attempt < 4) await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
				}
			}
			if (last is not null)
			{
				Console.Error.WriteLine($"Ollama embedding health check failed after retries. Aborting ingestion to avoid unnecessary Steam API traffic.\nEndpoint={ollamaEndpoint}, Model={ollamaModel}, NumCtx={embNumCtx}\nLast error: {last.Message}");
				return; // Do not proceed if embeddings are unavailable
			}
		}
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

		// If Cosmos is required for ingestion, abort early to avoid hammering upstream APIs.
		if (requireCosmos && cosmos is null)
		{
			Console.Error.WriteLine("Cosmos DB is not available and Ingestion:RequireCosmos=true. Aborting ingestion to avoid unnecessary upstream API traffic.");
			return;
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
			var dataRoot = ResolveDataRoot(builder.Configuration["DataLake:Root"]);
			var cap = int.TryParse(builder.Configuration["Ingestion:ReviewCapBronze"], out var rc) ? rc : 10;
			var runId = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
			var today = DateTime.UtcNow.Date;
			// Run-scoped log tee
			var logPath = DataLakePaths.Bronze.ConsoleLog(dataRoot, today, runId);
			Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
			using var logFile = File.CreateText(logPath);
			using var tee = new TeeTextWriter(Console.Out, logFile);
			Console.SetOut(tee);
			Console.SetError(tee);
			var mw = new ManifestWriter(dataRoot, runId, "bronze", new Dictionary<string, string>
			{
				["reviewCapBronze"] = cap.ToString(),
				["mode"] = "sample",
				["embeddingModel"] = ollamaModel,
				["embeddingNumCtx"] = embNumCtx.ToString(),
				["embeddingDims"] = dims.ToString()
			});
			mw.AddArtifact("consoleLog", logPath);
			mw.Start();
			var ingestor = new BronzeReviewIngestor(steam, dataRoot, cap);
			var storeIngestor = new BronzeStoreIngestor(steam, dataRoot, candidacyOptions);
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
			// Sanity report
			var sanityPath = await GenerateBronzeSanityReportAsync(dataRoot, today, runId);
			mw.AddArtifact("sanityReport", sanityPath);
			mw.Finish();
			await mw.SaveAsync();
			Console.WriteLine($"Bronze sample complete. Manifest: {DataLakePaths.Bronze.Manifest(dataRoot, runId)}");
			return;
		}

		// New: General Bronze ingestion (random-sampled) with flags
		// Usage:
		//   ingest bronze [--sample=250] [--reviews-cap-per-app=50] [--news-count=10] [--news-tags=patchnotes] [--concurrency=4] [--data-root=...]
		if (args.Length >= 2 && args[0] == "ingest" && args[1] == "bronze")
		{
			var dataRoot = ResolveDataRoot(builder.Configuration["DataLake:Root"]);
			int sampleCount = samplingCount; // default from config
			int reviewsCapPerApp = int.TryParse(builder.Configuration["Ingestion:ReviewCapBronze"], out var rcb) ? rcb : 50;
			int newsCount = int.TryParse(builder.Configuration["Ingestion:NewsCountBronze"], out var ncb) ? ncb : 10;
			string? newsTags = builder.Configuration["Ingestion:NewsTags"];
			// Default to 1 to reduce 429s; prefer explicit Ingestion:Concurrency, else fall back to Ingestion:MaxConcurrency, else 1
			int concurrency = 1;
			if (int.TryParse(builder.Configuration["Ingestion:Concurrency"], out var cc)) concurrency = cc;
			else if (int.TryParse(builder.Configuration["Ingestion:MaxConcurrency"], out var mcc)) concurrency = mcc;
			string? resumeRunId = null;

			// Parse simple --key=value flags
			foreach (var a in args.Skip(2))
			{
				if (!a.StartsWith("--")) continue;
				var idx = a.IndexOf('=');
				string key = idx > 2 ? a.Substring(2, idx - 2) : a[2..];
				string? val = idx > 0 && idx + 1 < a.Length ? a[(idx + 1)..] : null;
				if (string.IsNullOrWhiteSpace(key) || val is null) continue;
				switch (key)
				{
					case "data-root": dataRoot = val; break;
					case "sample": if (int.TryParse(val, out var sc2)) sampleCount = sc2; break;
					case "reviews-cap-per-app": if (int.TryParse(val, out var rc2)) reviewsCapPerApp = rc2; break;
					case "news-count": if (int.TryParse(val, out var nc2)) newsCount = nc2; break;
					case "news-tags": newsTags = (string.Equals(val, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(val, "none", StringComparison.OrdinalIgnoreCase)) ? null : val; break;
					case "concurrency": if (int.TryParse(val, out var c2)) concurrency = c2; break;
					case "resume": resumeRunId = val; break;
				}
			}

			var runId = !string.IsNullOrWhiteSpace(resumeRunId) ? resumeRunId! : $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
			var today = DateTime.UtcNow.Date;
			// Set up run-scoped console tee to file for auditability
			var logPath = DataLakePaths.Bronze.ConsoleLog(dataRoot, today, runId);
			Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
			using var logFile = File.CreateText(logPath);
			using var tee = new TeeTextWriter(Console.Out, logFile);
			Console.SetOut(tee);
			Console.SetError(tee);
			var steam = host.Services.GetRequiredService<ISteamClient>();
			var mw = new ManifestWriter(dataRoot, runId, "bronze", new Dictionary<string, string>
			{
				["mode"] = "random-sample",
				["sample"] = sampleCount.ToString(),
				["reviewsCapPerApp"] = reviewsCapPerApp.ToString(),
				["newsCount"] = newsCount.ToString(),
				["newsTags"] = newsTags ?? "<all>",
				["concurrency"] = Math.Clamp(concurrency, 1, 16).ToString(),
				["embeddingModel"] = ollamaModel,
				["embeddingNumCtx"] = embNumCtx.ToString(),
				["embeddingDims"] = dims.ToString()
			});
			mw.AddArtifact("consoleLog", logPath);
			mw.Start();

			// Build app sample
			int[] sampleApps;
			try
			{
				var listResp = await steamHttp.GetFromJsonAsync<SteamAppListResponse>("https://api.steampowered.com/ISteamApps/GetAppList/v2/");
				var all = listResp?.applist?.apps?.Select(a => a.appid).Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
				var rng = Random.Shared;
				var sample = all.OrderBy(_ => rng.Next()).Take(Math.Clamp(sampleCount, 3, 5000)).ToArray();
				int[] preferred = new[] { 620, 570, 440, 730, 292030, 271590, 1172470 };
				sampleApps = preferred.Concat(sample).Distinct().ToArray();
			}
			catch
			{
				sampleApps = new[] { 620, 570, 440 };
			}

			var stateDir = Path.Combine(dataRoot, "bronze", "runstate");
			var statePath = Path.Combine(stateDir, runId + ".json");
			var runState = new RunStateTracker(runId, statePath);
			var sem = new System.Threading.SemaphoreSlim(Math.Clamp(concurrency, 1, 16));
			var tasks = new List<Task>();
			var bronzeReviews = new BronzeReviewIngestor(steam, dataRoot, Math.Max(1, reviewsCapPerApp));
			var bronzeStore = new BronzeStoreIngestor(steam, dataRoot, candidacyOptions);
			var bronzeNews = new BronzeNewsIngestor(steam, dataRoot);

			var cts = new System.Threading.CancellationTokenSource();
			await foreach (var appId in runState.GetPendingShuffledAsync(sampleApps, cts.Token))
			{
				await sem.WaitAsync();
				tasks.Add(Task.Run(async () =>
				{
					try
					{
						await runState.MarkStartedAsync(appId);
						var rcount = await bronzeReviews.IngestReviewsAsync(appId, runId, today, cts.Token);
						mw.RecordItem($"reviews:{appId}", rcount);
						var scount = await bronzeStore.IngestStoreAsync(appId, runId, today, cts.Token);
						mw.RecordItem($"store:{appId}", scount);
						var ncount = await bronzeNews.IngestNewsAsync(appId, runId, today, count: newsCount, tags: newsTags, ct: cts.Token);
						mw.RecordItem($"news:{appId}", ncount);
						await runState.MarkSucceededAsync(appId);
					}
					catch (Exception ex)
					{
						mw.RecordError(ex);
					}
					finally
					{
						sem.Release();
					}
				}));
			}

			await Task.WhenAll(tasks);
			// Generate a sanity report artifact
			var sanityPath = await GenerateBronzeSanityReportAsync(dataRoot, today, runId);
			mw.AddArtifact("sanityReport", sanityPath);
			mw.Finish();
			await mw.SaveAsync();
			Console.WriteLine($"Bronze ingest complete. Manifest: {DataLakePaths.Bronze.Manifest(dataRoot, runId)}");
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

				// Prepare game description text; embeddings computed later only for sufficiently-reviewed games
				float[]? pendingGameVector = null;
				string? combinedDesc = null;
				var cleanDetailed = !string.IsNullOrWhiteSpace(dapp.detailed_description) ? EmbeddingUtils.StripHtml(dapp.detailed_description!) : string.Empty;
				combinedDesc = string.Join("\n\n", new[] { dapp.short_description, cleanDetailed }.Where(s => !string.IsNullOrWhiteSpace(s))!);

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
				try { vectors = await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, texts, dims, allowDeterministicFallback, embNumCtx, embMaxBatch, allowChunking); }
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
					// New: run embeddings for game descriptions only if there are at least 20 total fetched reviews
					if (pendingGameVector is null && !string.IsNullOrWhiteSpace(combinedDesc) && (dapp.recommendations?.total ?? 0) >= 20)
					{
						try
						{
							pendingGameVector = (await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, new List<string> { combinedDesc! }, dims, allowDeterministicFallback, embNumCtx, embMaxBatch, allowChunking)).FirstOrDefault();
						}
						catch { /* embedding failure is non-fatal for game doc */ }
					}
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
				var vec = (await EmbeddingUtils.GenerateVectorsAsync(http, ollamaEndpoint, ollamaModel, new List<string> { probe }, dims, allowDeterministicFallback, embNumCtx, embMaxBatch, allowChunking)).First();
			await VectorProbe.TryProbeAsync(reviews, dbName!, reviewsContainer!, distance, vec, formPref, fieldPath: vectorPath);
		}
	}

	private static string ResolveDataRoot(string? configured)
	{
		// Try to detect solution root by walking up from BaseDirectory
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		for (int i = 0; i < 8 && dir?.Parent is not null; i++)
		{
			if (File.Exists(Path.Combine(dir.FullName, "ActualGameSearch.sln")))
			{
				// If a configured path is provided and relative, resolve it against the solution root
				if (!string.IsNullOrWhiteSpace(configured))
				{
					var path = configured!;
					if (!Path.IsPathRooted(path))
					{
						path = Path.GetFullPath(Path.Combine(dir.FullName, path));
						Directory.CreateDirectory(path);
						return path;
					}
					// Configured path is absolute; use as-is
					Directory.CreateDirectory(path);
					return path;
				}
				// No configured path: use the default under the solution root
				var target = Path.Combine(dir.FullName, "AI-Agent-Workspace", "Artifacts", "DataLake");
				Directory.CreateDirectory(target);
				return target;
			}
			dir = dir.Parent;
		}
		// No solution file found. If configured, resolve relative to base directory; else fallback under process directory
		if (!string.IsNullOrWhiteSpace(configured))
		{
			var path = configured!;
			if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
			Directory.CreateDirectory(path);
			return path;
		}
		var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "AI-Agent-Workspace", "Artifacts", "DataLake"));
		Directory.CreateDirectory(fallback);
		return fallback;
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

	// Simple TextWriter that tees output to two writers (console + file)
	private sealed class TeeTextWriter : TextWriter
	{
		private readonly TextWriter _a;
		private readonly TextWriter _b;
		public TeeTextWriter(TextWriter a, TextWriter b)
		{
			_a = a; _b = b;
		}
		public override Encoding Encoding => _a.Encoding;
		public override void Write(char value) { _a.Write(value); _b.Write(value); }
		public override void Write(string? value) { _a.Write(value); _b.Write(value); }
		public override void WriteLine(string? value) { _a.WriteLine(value); _b.WriteLine(value); }
		public override void Flush() { try { _a.Flush(); } catch { } try { _b.Flush(); } catch { } }
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				try { _a.Flush(); } catch { }
				try { _b.Flush(); } catch { }
				try { _b.Dispose(); } catch { }
			}
			base.Dispose(disposing);
		}
	}

	private static async Task<string> GenerateBronzeSanityReportAsync(string dataRoot, DateTime today, string runId)
	{
		// Count files and totals; check for obviously empty outputs
		var report = new Dictionary<string, object?>();
		string dayRoot = Path.Combine(dataRoot, "bronze");
		string y = today.Year.ToString("D4"), m = today.Month.ToString("D2"), d = today.Day.ToString("D2");
		string reviewsDir = Path.Combine(dayRoot, "reviews", y, m, d, runId);
		string storeDir = Path.Combine(dayRoot, "store", y, m, d, runId);
		string newsDir = Path.Combine(dayRoot, "news", y, m, d, runId);

	int reviewFiles = Directory.Exists(reviewsDir) ? Directory.EnumerateFiles(reviewsDir, "*.json.gz", SearchOption.AllDirectories).Count() : 0;
	int storeFiles = Directory.Exists(storeDir) ? Directory.EnumerateFiles(storeDir, "*.json.gz", SearchOption.TopDirectoryOnly).Count() : 0;
	int newsFiles = Directory.Exists(newsDir) ? Directory.EnumerateFiles(newsDir, "*.json.gz", SearchOption.AllDirectories).Count() : 0;

		report["counts"] = new Dictionary<string, int>
		{
			["reviewsFiles"] = reviewFiles,
			["storeFiles"] = storeFiles,
			["newsFiles"] = newsFiles
		};

		var warnings = new List<string>();
		if (reviewFiles == 0) warnings.Add("No review pages were written; Steam throttling or selection may be too strict.");
		if (storeFiles == 0) warnings.Add("No store payloads were written; Bronze candidacy MinRecommendations may be too high.");
		if (newsFiles == 0) warnings.Add("No news payloads were written; tags/count settings may be too strict or apps had no news.");
		report["warnings"] = warnings;

		// Cross-artifact coverage: derive appIds present in each folder to detect gaps
		static int? ParseAppIdFromPath(string path)
		{
			// reviews: .../appid=12345/page=1.json.gz
			var name = Path.GetFileName(path);
			// Look one directory up for reviews/news where parent dir is appid=123
			var parent = new DirectoryInfo(Path.GetDirectoryName(path) ?? string.Empty).Name;
			if (parent.StartsWith("appid=", StringComparison.OrdinalIgnoreCase))
				parent = parent.Substring("appid=".Length);
			if (int.TryParse(parent, out var id1)) return id1;
			// store: filename appid=12345.json.gz
			var fn = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(name)); // strip .json.gz
			if (fn.StartsWith("appid=", StringComparison.OrdinalIgnoreCase))
			{
				var s = fn.Substring("appid=".Length);
				if (int.TryParse(s, out var id2)) return id2;
			}
			return null;
		}
		var reviewAppIds = Directory.Exists(reviewsDir) ? Directory.EnumerateFiles(reviewsDir, "*.json.gz", SearchOption.AllDirectories).Select(ParseAppIdFromPath).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet() : new HashSet<int>();
		var storeAppIds = Directory.Exists(storeDir) ? Directory.EnumerateFiles(storeDir, "*.json.gz", SearchOption.TopDirectoryOnly).Select(ParseAppIdFromPath).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet() : new HashSet<int>();
		var newsAppIds = Directory.Exists(newsDir) ? Directory.EnumerateFiles(newsDir, "*.json.gz", SearchOption.AllDirectories).Select(ParseAppIdFromPath).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet() : new HashSet<int>();

		var missingStore = reviewAppIds.Except(storeAppIds).Take(20).ToArray();
		var missingReviews = storeAppIds.Except(reviewAppIds).Take(20).ToArray();
		var missingNews = reviewAppIds.Except(newsAppIds).Take(20).ToArray();

		report["coverage"] = new Dictionary<string, object?>
		{
			["reviewOnlyApps_sample"] = missingStore,
			["storeOnlyApps_sample"] = missingReviews,
			["reviewNoNews_sample"] = missingNews
		};

		// Quick sample of one review file to estimate average review text length and language distribution
		string? sampleReviewFile = Directory.Exists(reviewsDir) ? Directory.EnumerateFiles(reviewsDir, "*.json.gz").FirstOrDefault() : null;
		if (sampleReviewFile is not null)
		{
			try
			{
				using var fs = File.OpenRead(sampleReviewFile);
				using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
				using var doc = await JsonDocument.ParseAsync(gz);
				if (doc.RootElement.TryGetProperty("reviews", out var arr) && arr.ValueKind == JsonValueKind.Array)
				{
					int n = 0; int totalLen = 0; var langs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
					foreach (var el in arr.EnumerateArray())
					{
						if (el.TryGetProperty("review", out var txt)) { totalLen += txt.GetString()?.Length ?? 0; n++; }
						if (el.TryGetProperty("language", out var lang))
						{
							var k = lang.GetString() ?? "unknown";
							langs[k] = langs.TryGetValue(k, out var c) ? c + 1 : 1;
						}
						if (n >= 50) break; // sample up to 50
					}
					if (n > 0)
					{
						report["sample"] = new Dictionary<string, object?>
						{
							["avgReviewLength"] = (int)Math.Round(totalLen / (double)n),
							["langsTop"] = langs.OrderByDescending(kv => kv.Value).Take(5).ToDictionary(kv => kv.Key, kv => kv.Value)
						};
					}
				}
			}
			catch { /* best-effort only */ }
		}

		var outPath = DataLakePaths.Bronze.SanityReport(dataRoot, today, runId);
		Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
		var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
		await File.WriteAllTextAsync(outPath, json);
		return outPath;
	}
}
