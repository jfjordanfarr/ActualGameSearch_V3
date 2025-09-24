# Quickstart (Dev)

Date: 2025‑09‑21 | Branch: `002-we-intend-to`

## Prereqs
- Docker enabled in Codespaces devcontainer
- .NET 10 SDK (provided in devcontainer)
- Ollama installed in container (optional for local embedding prototype)

## Run Backend (Dev)
The solution is already scaffolded with `.NET Aspire` projects: AppHost, Api, Core, Worker, ServiceDefaults.

Option A: Run tests only

```
dotnet test
```

Option B: Build and run the API directly (in-memory mode)

```
dotnet build src/ActualGameSearch.Api/ActualGameSearch.Api.csproj -c Debug
DOTNET_RUNNING_IN_TESTHOST=true ASPNETCORE_URLS=http://localhost:8080 dotnet run --project src/ActualGameSearch.Api/ActualGameSearch.Api.csproj
```

Option C: Run via Aspire AppHost (starts Cosmos emulator and Ollama)

```
dotnet build src/ActualGameSearch.AppHost/ActualGameSearch.AppHost.csproj -c Debug
dotnet run --project src/ActualGameSearch.AppHost/ActualGameSearch.AppHost.csproj
```

API endpoints:
- `GET /api/search/games?q=...&top=10`
- `GET /api/search/reviews?q=...&top=10[&fields=full]`
- `GET /api/search?q=...&top=10[&fields=full]`

## Run Frontend
Static site is served from `src/ActualGameSearch.Api/wwwroot/` when running the API.

## Data
- Cosmos DB (prod) or emulator via Aspire for dev
- Seed minimal dataset with synthetic games/reviews for local testing

## Next Steps
- Use `/tasks` to generate `tasks.md` and begin implementation
- Follow `data-model.md` and `contracts/openapi.yaml`
