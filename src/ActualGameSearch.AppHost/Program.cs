using Aspire.Hosting;
using Aspire.Hosting.Azure;

using System;
#pragma warning disable ASPIRECOSMOSDB001 // Opt-in to preview emulator for Data Explorer

// Provide sane defaults for the Aspire Dashboard in Codespaces
// Remove environment variable settings for the Aspire Dashboard
// Environment.SetEnvironmentVariable("ASPNETCORE_URLS", Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://127.0.0.1:18888");
// Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL", Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL") ?? "http://127.0.0.1:18889");

var builder = DistributedApplication.CreateBuilder(args);

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
				 // Route Ollama endpoint into the API via configuration
				 .WithEnvironment("Ollama:Endpoint", "http://ollama:11434/")
				 .WaitFor(ollama);

var worker = builder.AddProject<Projects.ActualGameSearch_Worker>("worker")
						  .WithReference(cosmos)
						  .WithReference(db)
						  .WaitFor(db)
					// Route Ollama endpoint into the worker via configuration
					.WithEnvironment("Ollama:Endpoint", "http://ollama:11434/")
					.WaitFor(ollama);

builder.Build().Run();
