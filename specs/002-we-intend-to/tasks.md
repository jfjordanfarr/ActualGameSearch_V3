# Tasks: Actual Game Search (Hybrid Full‑Text + Semantic)

**Input**: Design documents from `/specs/002-we-intend-to/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)
```
1. Load plan.md from feature directory → Extract tech stack, libraries, structure
2. Load optional design docs → data-model.md, contracts/, research.md, quickstart.md
3. Generate tasks by category (Setup → Tests → Core → Integration → Polish)
4. Apply task rules (TDD first, parallelization [P] across different files)
5. Number tasks sequentially; add dependency notes and parallel examples
```

## Path conventions (idiomatic .NET Aspire)
- Single solution `ActualGameSearch.sln` with projects under `src/`:
  - `src/ActualGameSearch.AppHost/` (orchestrator)
  - `src/ActualGameSearch.Api/` (ASP.NET Core Minimal API + serves static wwwroot)
  - `src/ActualGameSearch.Core/` (models, services, adapters)
  - `src/ActualGameSearch.Worker/` (ETL placeholder)
  - `src/ActualGameSearch.ServiceDefaults/` (.ServiceDefaults shared config)
- Tests under `tests/`:
  - `tests/ActualGameSearch.ContractTests/`
  - `tests/ActualGameSearch.IntegrationTests/`
  - `tests/ActualGameSearch.UnitTests/`
- API base URL `http://localhost:8080`

---

## Phase 3.1: Setup
- [x] T001 Create solution and projects (Aspire idioms)
  - `ActualGameSearch.sln` with projects: `ActualGameSearch.AppHost`, `ActualGameSearch.Api`, `ActualGameSearch.Core`, `ActualGameSearch.Worker`, `ActualGameSearch.ServiceDefaults`
  - Add test projects: `ActualGameSearch.ContractTests`, `ActualGameSearch.IntegrationTests`, `ActualGameSearch.UnitTests`
  - Files/paths: `src/*`, `tests/*`
  - Dependencies: none
- [ ] T002 Configure AppHost orchestrator with resources
  - Add Cosmos DB emulator resource; add database/containers (games, reviews)
  - Add Ollama container (Embedding Gemma), private, persistent model volume
  - Reference resources from `Api` and `Worker` via `.WithReference(...)`
  - File: `src/ActualGameSearch.AppHost/Program.cs`
  - Dependencies: T001
- [x] T003 [P] Configure ServiceDefaults and tooling
  - `src/ActualGameSearch.ServiceDefaults/` with `AddServiceDefaults()`; enable OpenTelemetry, service discovery, resilience
  - Add `Directory.Build.props`, nullable, analyzers, central package mgmt
  - Dependencies: T001
- [x] T004 [P] Initialize Api with static frontend
  - Minimal `wwwroot/index.html`, `wwwroot/app.js`, `wwwroot/styles.css` (Bootstrap)
  - Configure static files + minimal pipeline in `Program.cs`
  - Files: `src/ActualGameSearch.Api/`
  - Dependencies: T001

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
- [x] T005 Update OpenAPI for cheap preview (games‑first)
  - Extend `contracts/openapi.yaml` with:
    - GET `/api/search/games` → games‑only hybrid results
    - GET `/api/search/reviews` → reviews‑only hybrid results
    - Keep existing grouped endpoint `GET /api/search`
  - Validate query params mirror shared `FilterSet` (caps, controller, adultOnly, convergence)
  - File: `specs/002-we-intend-to/contracts/openapi.yaml`
  - Dependencies: none
- [x] T006 [P] Contract test GET /api/search/games
  - Implement in `tests/ActualGameSearch.ContractTests/GamesSearchContractTests.cs`
  - Validate `q`, caps, filters; response schema for games array with lightweight fields
  - Dependencies: T005, T003
- [x] T007 [P] Contract test GET /api/search/reviews
  - Implement in `tests/ActualGameSearch.ContractTests/ReviewsSearchContractTests.cs`
  - Validate `q`, caps, filters, convergence params
  - Dependencies: T005, T003
- [x] T008 [P] Contract test GET /api/search (grouped)
  - Implement in `tests/ActualGameSearch.ContractTests/SearchContractTests.cs`
  - Validate grouped payload shape (game + candidates[])
  - Dependencies: T005, T003
- [ ] T009 [P] Integration test: cheap preview flow (client merge)
  - Simulate client fetching `/api/search/games` and `/api/search/reviews` concurrently; ensure merge/re‑rank client‑side
  - Implement in `tests/ActualGameSearch.IntegrationTests/CheapPreviewFlowTests.cs`
  - Dependencies: T003
- [ ] T010 [P] Integration test: convergence filters
  - Assert `minReviewMatches` and `requireGameAndReview` behavior in reviews‑only path and grouped path
  - Implement in `tests/ActualGameSearch.IntegrationTests/ConvergenceTests.cs`
  - Dependencies: T003

## Phase 3.3: Core Implementation (ONLY after tests are failing)
- [ ] T011 [P] Models: Game, Review, Candidate, FilterSet, ReRankWeights, QuerySession
  - Mirror `data-model.md` shapes (C# records)
  - File: `src/ActualGameSearch.Core/Models/*.cs`
  - Dependencies: T006–T010 (tests exist and fail)
- [ ] T012 [P] Embeddings via Microsoft.Extensions.AI
  - Implement `OllamaEmbeddingGenerator` adapter as `IEmbeddingGenerator<string, float[]>`
  - Register in DI; prefer MS.EAI abstractions instead of custom `IEmbedder`
  - File: `src/ActualGameSearch.Core/Embeddings/OllamaEmbeddingGenerator.cs`
  - Dependencies: T011
- [ ] T013 Ranking service (hybrid combine)
  - `HybridRanker` computes `combinedScore = wS*semantic + wT*text`, tie‑breakers
  - Text score from Cosmos text ranking; semantic from vector similarity
  - File: `src/ActualGameSearch.Core/Services/Ranking/HybridRanker.cs`
  - Dependencies: T011–T012
- [ ] T014 Data access adapters (Cosmos)
  - Games and Reviews repositories; vector query helpers for DiskANN
  - Initialize containers with indexing policy (DiskANN on vectors, FTS on text)
  - Files: `src/ActualGameSearch.Core/Adapters/Cosmos/*.cs`
  - Dependencies: T011
- [ ] T015 SearchService: games‑only hybrid
  - Pre‑filters; run vector+text against games (use `Game.vector`); cap results and project lightweight fields
  - File: `src/ActualGameSearch.Core/Services/Search/GamesSearchService.cs`
  - Dependencies: T011–T014
- [ ] T016 SearchService: reviews‑only hybrid
  - Pre‑filters; run vector+text against reviews; apply convergence logic; cap candidates
  - File: `src/ActualGameSearch.Core/Services/Search/ReviewsSearchService.cs`
  - Dependencies: T011–T014
- [ ] T017 SearchService: grouped orchestration
  - Optionally stitch games+reviews into grouped payload for `/api/search`
  - File: `src/ActualGameSearch.Core/Services/Search/GroupedSearchService.cs`
  - Dependencies: T015–T016
- [ ] T018 API: GET /api/search/games
  - Minimal API endpoint mapping to `GamesSearchService`
  - File: `src/ActualGameSearch.Api/Endpoints/GamesSearchEndpoints.cs`
  - Dependencies: T015
- [ ] T019 API: GET /api/search/reviews
  - Minimal API endpoint mapping to `ReviewsSearchService`
  - File: `src/ActualGameSearch.Api/Endpoints/ReviewsSearchEndpoints.cs`
  - Dependencies: T016
- [ ] T020 API: GET /api/search (grouped)
  - Minimal API endpoint mapping to `GroupedSearchService`
  - File: `src/ActualGameSearch.Api/Endpoints/SearchEndpoints.cs`
  - Dependencies: T017

## Phase 3.4: Integration
- [ ] T021 AppHost wiring and environment
  - WithReference to `cosmos` and `ollama` for Api and Worker; private exposure for Ollama
  - Ensure connection strings/URLs flow via Aspire service binding
  - File: `src/ActualGameSearch.AppHost/Program.cs`
  - Dependencies: T018–T020
- [ ] T022 Worker ETL scaffolding
  - Normalize/dedup/provenance per research; seed synthetic dataset
  - Consume `IEmbeddingGenerator` to compute vectors for games and reviews
  - File: `src/ActualGameSearch.Worker/Program.cs`
  - Dependencies: T012, T014
- [ ] T023 Frontend client merge and re‑rank
  - `wwwroot/app.js`: fetch `/api/search/games` and `/api/search/reviews` concurrently; client re‑rank controls (default 0.5/0.5)
  - Files: `src/ActualGameSearch.Api/wwwroot/*`
  - Dependencies: T004, T018–T020
- [ ] T024 Logging, health, and metrics
  - Add basic request logging, `/health`, minimal metrics hooks
  - Files: `src/ActualGameSearch.Api/Program.cs`, `src/ActualGameSearch.Api/Health/HealthEndpoints.cs`
  - Dependencies: T018–T020

## Phase 3.5: Polish
- [ ] T025 [P] Unit tests for ranking and convergence
  - Validate weight adjustments, tie‑breakers, and convergence
  - File: `tests/ActualGameSearch.UnitTests/RankingTests.cs`
  - Dependencies: T013–T017
- [ ] T026 [P] Performance smoke tests
  - Target: Best‑effort initial preview ≤ 3s; try tiny preview ≤ 300ms when cached/cheap
  - File: `tests/ActualGameSearch.IntegrationTests/SearchPerfTests.cs`
  - Dependencies: T018–T020
- [ ] T027 [P] Update docs and data dictionary
  - Create `docs/data-dictionary.md` capturing normalization/dedup and fields
  - Update feature `quickstart.md` usage with test dataset and preview flow
  - Files: `/workspaces/ActualGameSearch_V3/docs/data-dictionary.md`, `specs/002-we-intend-to/quickstart.md`
  - Dependencies: none
- [ ] T028 Cleanup and duplication removal
  - Ensure analyzers pass; remove dead code; finalize comments
  - Files: src + tests touched across tasks
  - Dependencies: T021–T027

## Dependencies
- Setup (T001–T004) before Tests (T006–T010)
- OpenAPI update (T005) before contract tests (T006–T008)
- Models (T011) before Services (T012–T017)
- Services before Endpoints (T018–T020)
- Endpoints before Integration (T021–T024)
- Everything before Polish (T025–T028)

## Parallel Execution Examples
```
# Example 1: Kick off contract tests in parallel after OpenAPI update
Task: T006 Contract test GET /api/search/games
Task: T007 Contract test GET /api/search/reviews
Task: T008 Contract test GET /api/search (grouped)

# Example 2: Core models/services in parallel (independent files)
Task: T011 Models (Core/Models/*.cs)
Task: T012 IEmbeddingGenerator adapter (Core/Embeddings/*.cs)

# Example 3: Polish in parallel
Task: T025 Unit tests for ranking/convergence
Task: T026 Performance smoke tests
Task: T027 Docs/data dictionary
```

## Validation Checklist
- [ ] Contracts updated and all endpoints have tests (T005–T008)
- [ ] All entities have model tasks (T011)
- [ ] All tests come before implementation (T006–T010 precede T011–T020)
- [ ] Parallel tasks only touch independent files
- [ ] Each task specifies exact file paths
- [ ] No task modifies the same file as another [P] task

---

## Status Snapshot (2025-09-23)
- Passing tests: Contract tests for `/api/search/games` and `/api/search/reviews` and Integration tests for preview/convergence assertions are green.
- Implemented now: Minimal endpoints in `Program.cs`, in-memory repos for tests, Cosmos repos for real mode, embedding service, result envelope.
- Gaps vs plan:
  - Convergence query params exist in contracts but are not yet honored by API/services; mark as planned.
  - Endpoint classes (`Endpoints/*.cs`) are not yet split; planned refactor later (T018–T020 remain pending, but functionality exists inline).
  - Grouped search currently groups review candidates by game using vector search only; hybrid with games/reviews merge is planned.
