Repository documentation audit (2025-09-29)

Purpose
- Create a concise, living audit that ties Vision → Requirements → Architecture → Units.
- Keep it source-linked and practical for onboarding and day-to-day development.

Scope of this pass
- Seed a structured outline with initial, verified findings from the codebase and recent sessions.
- Call out open questions and next steps to converge on stable docs.

## Vision

- Mission: Ultra-low-cost, high-quality hybrid search for games; a model open-source project at actualgamesearch.com.
- Stack: .NET 8 (Aspire), Cosmos DB NoSQL + DiskANN, Ollama embeddings, deterministic ranker + optional client-side re-ranking.
- Stance: Correctness-first. No silent embedding chunking/mean pooling—opt-in only.
- Migration: Target .NET 8 now, keep forward-compatible with .NET 10 LTS.

## Requirements

### Functional
- Product search and reviews search backed by hybrid ranking (deterministic ranker + optional client-side re-ranking).
- Bronze ingestion pipeline for candidates, reviews, and news.
  - Bronze candidacy: include any game with ≥10 recommendations.
  - Associate up to 99 appids per canonical game.
- Embedding service available via a single, normalized endpoint (Ollama), model bootstrap handled automatically (show → pull → create).

### Non-functional
- Correctness-first embeddings:
  - Enforce configured context window (8192 for nomic-embed-8k) and fail fast when inputs exceed unless chunking is explicitly enabled.
- Resilience and pacing:
  - Steam API: bounded retries, backoff, and pacing to avoid 429s.
  - Embeddings: readiness polling and short warm-up after model create to avoid early 404s.
- Configuration:
  - Single-source configuration (AppHost) via environment; strongly-typed options in services.
  - Avoid magic strings; centralize configuration keys.
- Observability:
  - Aspire Dashboard enabled in dev via ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL.
- Cost: Favor low-cost defaults and predictable operations.

### Data policies
- Cosmos DB NoSQL used for game and review data; vector search with DiskANN.
- Data lake (bronze tier) organized under AI-Agent-Workspace/Artifacts/DataLake/bronze/.

## Architecture

### Components (by project)
- AppHost (Aspire): `src/ActualGameSearch.AppHost/`
  - Orchestrates services, wires configuration/env, pins the Ollama container version (observed 0.3.12 for reliable 8k context).
- API: `src/ActualGameSearch.Api/`
  - HTTP API + minimal UI (`wwwroot/`). Cosmos bootstrapping in `Infrastructure/CosmosBootstrapper.cs`. Repositories in `Data/`.
- Worker: `src/ActualGameSearch.Worker/`
  - Ingestion for Store/Reviews/News, embedding client/service, processing, probes, storage helpers, and configuration wiring.
- Core: `src/ActualGameSearch.Core/`
  - Models, primitives, ranking, and a text embedding abstraction used by API/Worker.
- Service Defaults: `src/ActualGameSearch.ServiceDefaults/`
  - Cross-cutting service defaults and extensions.

### External dependencies
- Cosmos DB NoSQL + DiskANN for vector search.
- Ollama for embeddings; custom model tag: `nomic-embed-8k` from `infrastructure/Modelfile.nomic-embed-8k` (PARAMETER num_ctx 8192).
- Steam Web API for source data.
- Aspire Dashboard for local observability.

### Configuration surface (selected)
- Worker configuration (see `src/ActualGameSearch.Worker/Configuration/`):
  - `ConfigurationKeys.cs` — centralized keys/constants.
  - `WorkerOptions.cs` and extensions for binding strongly-typed options.
  - Embeddings options and wiring in `EmbeddingConfigurationExtensions.cs`.
- Environment variables supplied by AppHost in dev; intent is single-source config with strong types.

### Known decisions and issues (current)
- Embeddings readiness: Calls immediately after model create can 404. Introduce readiness polling and a warm-up embed call.
- Version variance: Local host Ollama 0.12.3 tends to clamp to 2048 context; AppHost-pinned 0.3.12 yields true 8192 context. Prefer the pinned container for dev workflows.
- Cosmos initialization: Occasional NullReference exceptions observed during AppHost run—investigate bootstrap sequence and guards.

## Units (inventory with file anchors)

### Worker (ingestion and embeddings)
- Embeddings
  - `src/ActualGameSearch.Worker/Embeddings/EmbeddingClient.cs` — HTTP client to Ollama for embeddings.
  - `src/ActualGameSearch.Worker/Embeddings/EmbeddingUtils.cs` — endpoint probing, normalization, health checks; target to add readiness polling.
  - `src/ActualGameSearch.Worker/Embeddings/IEmbeddingClient.cs`, `IEmbeddingService.cs` — contracts.
- Ingestion
  - `src/ActualGameSearch.Worker/Ingestion/BronzeStoreIngestor.cs` — store ingestion with Bronze candidacy policy.
  - `src/ActualGameSearch.Worker/Ingestion/BronzeReviewIngestor.cs`, `BronzeNewsIngestor.cs` — reviews and news.
- Configuration
  - `src/ActualGameSearch.Worker/Configuration/ConfigurationExtensions.cs`
  - `src/ActualGameSearch.Worker/Configuration/EmbeddingConfigurationExtensions.cs`
  - `src/ActualGameSearch.Worker/Configuration/WorkerOptions.cs`
- Processing/Storage
  - `src/ActualGameSearch.Worker/Processing/ReviewSanitizer.cs`
  - `src/ActualGameSearch.Worker/Storage/*` (DataLake paths, manifests, run state).

### API
- `src/ActualGameSearch.Api/Infrastructure/CosmosBootstrapper.cs` — creates/ensures database/containers.
- `src/ActualGameSearch.Api/Data/*` — Games/Reviews repositories (Cosmos and in-memory flavors).
- `src/ActualGameSearch.Api/wwwroot/*` — minimal web UI (index/search pages, static JS/CSS).

### Core
- Models: `src/ActualGameSearch.Core/Models/*`
- Ranking: `src/ActualGameSearch.Core/Services/Ranking/HybridRanker.cs`
- Vector query helpers: `src/ActualGameSearch.Core/Services/CosmosVectorQueryHelper.cs`
- Embedding abstraction: `src/ActualGameSearch.Core/Embeddings/TextEmbeddingService.cs`

### AppHost and Service Defaults
- AppHost: `src/ActualGameSearch.AppHost/Program.cs`, project wiring and dev env.
- Service Defaults: `src/ActualGameSearch.ServiceDefaults/*`.

## Observed tree (top-level, gitignore-aware)

- Root folders: `.github/`, `.specify/`, `.vscode/`, `AI-Agent-Workspace/`, `infrastructure/`, `specs/`, `src/`, `tests/`.
- Notable data folders: `AI-Agent-Workspace/Artifacts/DataLake/bronze/` (data lake staging).
- Spec-kit present under `.specify/` with templates and scripts.

## Open questions
- API surface and query parameters for search endpoints (beyond the minimal UI) — document contract or OpenAPI if available.
- Finalize embedding endpoint normalization policy (`/api/embeddings` vs `/api/embed`) and timeouts.
- Cosmos bootstrap sequencing and idempotent guards to eliminate the NRE.

## Next steps
- Embed readiness and endpoint normalization
  - Add readiness polling and warm-up in `EmbeddingUtils` and/or `EmbeddingClient`.
  - Ensure Worker in dev targets the AppHost Ollama endpoint by default.
- Document configuration keys and environment variables explicitly (table in a follow-up doc).
- Architecture diagram: add a compact diagram under `AI-Agent-Workspace/Docs/`.
- Tests: add minimal tests around Bronze candidacy policy and embedding configuration binding.

---

Changelog
- 2025-09-29: Initial skeleton and inventory created from repo scan and recent session notes.
