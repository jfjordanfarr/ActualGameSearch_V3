using Aspire.Hosting;
using Aspire.Hosting.Azure;

using System;
#pragma warning disable ASPIRECOSMOSDB001 // Opt-in to preview emulator for Data Explorer

// Provide sane defaults for the Aspire Dashboard in Codespaces
// If a dashboard is running, set its OTLP HTTP endpoint for exporters.
var otlp = Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL") ?? "http://127.0.0.1:18889";
// Also set standard OTEL endpoint env used by .NET OTLP exporters
Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", otlp);

var builder = DistributedApplication.CreateBuilder(args);

// Dashboard not wired programmatically in this version; exporters still respect OTLP env.

// Cosmos DB emulator with Data Explorer for DX
var cosmos = builder.AddAzureCosmosDB("cosmos-db").RunAsPreviewEmulator(emulator =>
{
	emulator.WithDataExplorer();
});

// Ollama embedding service as a managed container resource
// Exposes HTTP API on port 11434 inside the Aspire network
var ollama = builder.AddContainer("ollama", image: "ollama/ollama:latest")
					.WithHttpEndpoint(env: "http", port: 11434, targetPort: 11434)
					.WithEnvironment("OLLAMA_KEEP_ALIVE", "24h")
					.WithArgs("serve");

// Logical database and containers for games and reviews
var db = cosmos.AddCosmosDatabase("actualgames");
// Intentionally do not pre-provision containers here so the app can create them
// with the correct vector policy and indexes during bootstrap.

// Apps that will consume the resources
var api = builder.AddProject<Projects.ActualGameSearch_Api>("api")
					  .WithReference(cosmos)
					  .WithReference(db)
					  .WaitFor(db)
				 // Route Ollama endpoint into the API via environment using the discovered endpoint
				 .WithEnvironment("Ollama:Endpoint", ollama.GetEndpoint("http"))
				 .WithEnvironment("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL", otlp)
				 .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlp)
				 .WaitFor(ollama);

var worker = builder.AddProject<Projects.ActualGameSearch_Worker>("worker")
						  .WithReference(cosmos)
						  .WithReference(db)
						  .WaitFor(db)
					// Route Ollama endpoint into the Worker via environment using the discovered endpoint
					.WithEnvironment("Ollama:Endpoint", ollama.GetEndpoint("http"))
					.WithEnvironment("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL", otlp)
					.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlp)
					// Ensure data lake writes go to the repo-level AI-Agent-Workspace, not under the Worker project
					.WithEnvironment("DataLake:Root", "../../AI-Agent-Workspace/Artifacts/DataLake")
					.WaitFor(ollama);

// Optionally start the Worker in a specific mode by passing CLI args via env var
// Example: BRONZE_INGEST_ARGS="ingest bronze --sample=600 --reviews-cap-per-app=100 --news-tags=all --news-count=20 --concurrency=1"
var bronzeArgs = Environment.GetEnvironmentVariable("BRONZE_INGEST_ARGS");
if (!string.IsNullOrWhiteSpace(bronzeArgs))
{
	// Split on spaces; quoted values are uncommon for our flags. Adjust if needed.
	var workerArgs = bronzeArgs
		.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		.ToArray();
	worker.WithArgs(workerArgs);
}

builder.Build().Run();
