using Aspire.Hosting;
using Aspire.Hosting.Azure;

#pragma warning disable ASPIRECOSMOSDB001 // Opt-in to preview emulator for Data Explorer

var builder = DistributedApplication.CreateBuilder(args);

// Cosmos DB emulator with Data Explorer for DX
var cosmos = builder.AddAzureCosmosDB("cosmos-db").RunAsPreviewEmulator(emulator =>
{
	emulator.WithDataExplorer();
});

// Logical database and containers for games and reviews
var db = cosmos.AddCosmosDatabase("actualgames");
var games = db.AddContainer("games", "/id");
var reviews = db.AddContainer("reviews", "/id");

// Apps that will consume the resources
var api = builder.AddProject<Projects.ActualGameSearch_Api>("api")
				 .WithReference(cosmos);

var worker = builder.AddProject<Projects.ActualGameSearch_Worker>("worker")
					.WithReference(cosmos);

builder.Build().Run();
