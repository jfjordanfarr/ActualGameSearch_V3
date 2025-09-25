Summary
- This PR lands the initial, working proof-of-concept for ActualGameSearch V3: a low-cost, high‑relevance hybrid search engine for games built on .NET Aspire, with Cosmos (vectors) readiness, Ollama embeddings, resilient ETL, and a clean API with a stable envelope. It also introduces a living spec and governance/provenance corpus so future changes stay aligned to reality.

What’s Included (High-Level)
- Application layout (`src/*`)
  - `ActualGameSearch.Api`: Minimal API with `Result<T>` envelope, static assets, in-memory fallback for degraded environments
  - `ActualGameSearch.Core`: Domain models, ranking, embeddings abstractions, repository interfaces
  - `ActualGameSearch.Worker`: ETL/seeding for authentic data (Steam), retries and non-fatal error handling, embedding ingestion
  - `ActualGameSearch.AppHost`: Aspire orchestration (dev bootstrap for Cosmos emulator + Ollama)
  - `ActualGameSearch.ServiceDefaults`: Cross‑cutting defaults (observability, configuration helpers)
- Endpoints (stable contract)
  - GET `/api/search/games` — params: `q`, `top`
  - GET `/api/search/reviews` — params: `q`, `top`, optional `fields=full`
  - GET `/api/search` — params: `q`, `top`, optional `fields=full`
  - All return envelope: `Result<T> { ok, data, error }`
  - Planned/Deprecated (documented only): tuning params, convergence, `candidateCap`, etc. remain marked deprecated in OpenAPI until implemented
- Search behavior
  - Hybrid philosophy: deterministic, lightweight full-text + semantic vectors; client-side re-ranking allowed
  - Emulator-aware fallback: if Cosmos emulator lacks required features (e.g., CASE WHEN with VectorDistance), we route to in-memory deterministic search
- Embeddings
  - Local embeddings via Ollama, with a reproducible setup for 8k context on `nomic-embed-8k` (custom Modelfile)
  - Abstractions for embedding service; deterministic fallback when embeddings unavailable
- ETL/Worker
  - Robust pipeline with non-fatal per-app failure handling (e.g., transient 5xx from Steam)
  - Clear logs, counters, and future hooks for retries/backoff and metrics
- Specs, Contracts, Docs, and Provenance
  - Living product spec and plan under `specs/002-we-intend-to/*` (including `contracts/openapi.yaml`)
  - Governance/provenance: raw and fact‑checked interleaved conversation histories Days 1–5
  - Infra/docs for reproducible embeddings setup and local dev

Key Files/Dirs
- API entry: `src/ActualGameSearch.Api/Program.cs`
- Core primitives and models: `src/ActualGameSearch.Core/*`
- Worker entry: `src/ActualGameSearch.Worker/Program.cs`
- Aspire host: `src/ActualGameSearch.AppHost/Program.cs`
- OpenAPI contract: `specs/002-we-intend-to/contracts/openapi.yaml`
- Specs and plan: `specs/002-we-intend-to/spec.md`, `plan.md`, `tasks.md`, `research.md`
- Infra for embeddings:
  - `infrastructure/Modelfile.nomic-embed-8k`
  - `infrastructure/setup-ollama-models.sh`
  - `infrastructure/README.md`
- Docs:
  - `AI-Agent-Workspace/Docs/ollama-context-fix.md`
  - `AI-Agent-Workspace/Docs/deployment-setup.md`
- Provenance (Days 1–5 summaries):
  - `AI-Agent-Workspace/Background/ConversationHistory/Summarized/SUMMARIZED_0{1..5}_2025-09-2{0..4}.md`

How To Run Locally (Dev)
- API only (in-memory fallback when Cosmos/Ollama absent)
  - `dotnet build src/ActualGameSearch.Api/ActualGameSearch.Api.csproj -c Debug`
  - `DOTNET_RUNNING_IN_TESTHOST=true ASPNETCORE_URLS=http://localhost:8080 dotnet run --project src/ActualGameSearch.Api/ActualGameSearch.Api.csproj`
- End-to-end via Aspire (brings up emulator/Ollama if configured)
  - `dotnet build src/ActualGameSearch.AppHost/ActualGameSearch.AppHost.csproj -c Debug`
  - `dotnet run --project src/ActualGameSearch.AppHost/ActualGameSearch.AppHost.csproj`
- Seed/ETL worker
  - `dotnet run --project src/ActualGameSearch.Worker/ActualGameSearch.Worker.csproj`
- Optional: prepare embeddings model (8k context)
  - `./infrastructure/setup-ollama-models.sh`
  - Verify model tag `nomic-embed-8k:latest` appears in `http://localhost:11434/api/tags`

Build, Test, and Health
- All projects build clean in Debug
- Unit, Integration, and Contract tests green
- Smoke: API endpoints reachable; in-memory fallback succeeds when emulator lacks vector/CASE support
- Worker runs to completion under transient upstream failures without crashing the whole job

Design Notes and Tradeoffs
- Relevance-first, cost-aware: deterministic ranking + optional client re-ranking
- Capability-aware fallback: in-memory path when Cosmos emulator lacks the needed SQL/vector features; avoids brittle emulator-specific workarounds
- Repeatability: custom 8k embedding model scripted via Modelfile + setup script; doc’d and idempotent
- Governance and provenance are first-class: interleaved, fact-checked histories + living spec/plan/tasks

Known Limitations / Deferred Items
- Emulator SQL limitations: CASE expressions with vector ops remain unsupported; full hybrid query semantics will be validated against real Azure Cosmos
- Similar games and advanced clustering: intentionally deferred; basic weighted similarity + client re‑ranking supported by design
- Embedding model assumptions: pinned to current model behavior via Modelfile; future upstream changes may require re‑pinning or validation
- Observability: metrics/tracing hooks exist via ServiceDefaults patterns; richer OTel dashboards and worker retry telemetry are planned

Review Guidance (very large PR)
- This is the initial POC landing; focus on:
  - Public API contract and envelope shape
  - Repo layout, boundaries, and abstractions (Core vs API vs Worker)
  - Fallback strategies and error handling in Worker and API
  - Reproducibility (infra + docs)
  - OpenAPI accuracy and deprecation markers matching code reality
- Non-blocking nits can be captured as follow-ups to keep momentum

Checklist
- [x] Builds clean (Debug)
- [x] Unit/Integration/Contract tests pass
- [x] API endpoints reachable; deterministic fallback verified
- [x] ETL resilient to transient upstream errors
- [x] Infra + docs for embeddings repeatability included
- [x] OpenAPI in sync with implemented endpoints; speculative params marked deprecated
- [x] Provenance summaries for Days 1–5 included

Next Steps (post-merge)
- Provision/point at real Azure Cosmos to validate full hybrid query semantics
- Add an embedding health probe endpoint (`/api/health/embedding`) and a contract test
- Add targeted integration test for worker retry/backoff (e.g., 502 then success)
- Begin “spec‑kit” driven branch from `main` for feature work and contract hardening