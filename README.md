# ActualGameSearch V3

A spec-driven rebuild of Semantic Game Search using modern .NET and Azure primitives, with Spec Kit for workflow and MCP for on-demand docs.

## Vision
Deliver a fast, multilingual, explainable game search that feels like recommendations from people you trust.

- Core stack: `.NET 10` + `.NET Aspire` for orchestrated microservices
- Data: `Azure Cosmos DB for NoSQL` with vector (DiskANN) + full-text search
- Embeddings: `Ollama` (local) using `Embedding Gemma` (768 dim) for cost-effective, reproducible embeddings
- ETL: `Azure Functions` (Timer/Queue triggered) to ingest and transform Steam data
- Dev experience: Dev container + Spec Kit + MCP (Microsoft Docs, Context7)

## Repo status
This repo was initialized with Spec Kit.
- New folders: `.specify/` (templates, scripts, memory) and `.github/` (automation)
- Background materials preserved under `Background/SteamSeeker-2023/`
- Prior minimal README was replaced with this one.

## How we work (Spec Kit)
Use the slash commands to drive work:
- `/constitution` – update principles in `.specify/memory/constitution.md`
- `/specify` – write feature specs with acceptance criteria
- `/plan` – break specs into tasks
- `/tasks` – generate actionable tasks
- `/implement` – implement with tight loops and validation

You can run the CLI yourself:
- `uvx --from git+https://github.com/github/spec-kit.git specify check` – validate env

## MCP servers (Docs-on-demand)
Installed and available in Codespaces:
- Microsoft Docs MCP – query official Microsoft Learn docs
- Context7 MCP – pull framework/library docs and examples

Use the VS Code MCP sidebar to query during design/implementation.

## Initial implementation plan (high-level)
1) Scaffold Aspire solution
- Solution: `src/ActualGameSearch.sln`
- Projects: `Gateway`, `API.Search`, `Workers.ETL`, `Shared` (contracts/models)

2) Provision Cosmos resources (local emulator first)
- Collections: `games` (vector+FTS fields), `reviews` (vector)
- Index: DiskANN for vectors; full-text for titles/descriptions

3) Embedding pipeline
- Service to call local `Ollama` (Gemma) for text → vector
- Deterministic batching; backpressure via Storage Queues

4) ETL functions
- Timers to refresh Steam metadata/reviews (respecting terms)
- Map-reduce: review-level vectors → game-level weighted vector

5) Search API
- Hybrid search (vector + lexical), re-ranking with proximity + resonance
- Explain endpoint returns top matching reviews and factors

6) Minimal UI
- Next steps: lightweight Razor/Blazor or static frontend to test queries

## Next steps
- Commit Spec Kit scaffolding and background docs
- `/constitution` to capture principles and architectural invariants
- `/specify` the “Search API v1” and “ETL v1” features
- Scaffold Aspire solution and wire local Cosmos + Ollama

## Development prerequisites
- GitHub Codespaces with Docker
- `uv/uvx` installed (already done by bootstrap)
- Local `Ollama` (Codespaces variant or container) for embeddings
- Azure CLI (later for provisioning) and Cosmos emulator/local dev connection

## License
MIT License
