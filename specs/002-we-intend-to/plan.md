# Implementation Plan: Actual Game Search (Hybrid Full‑Text + Semantic)

**Branch**: `002-we-intend-to` | **Date**: 2025-09-21 | **Spec**: `/workspaces/ActualGameSearch_V3/specs/002-we-intend-to/spec.md`
**Input**: Feature specification from `/specs/002-we-intend-to/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → OK
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → No unresolved markers in spec
3. Fill the Constitution Check section based on the content of the constitution document.
4. Evaluate Constitution Check section below
   → PASS; no violations
5. Execute Phase 0 → research.md
   → Completed (see research.md)
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, and agent guidance
   → Completed (see files in specs/002-we-intend-to)
7. Re-evaluate Constitution Check section
   → PASS; design adheres to principles
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
Build an open, ultra‑low‑cost, high‑accuracy game search that returns a large, grouped‑by‑game candidate pool for client‑side
re‑ranking. Users can apply rich filters (date, controller support, adult flag, minimum reviews, purchase‑origin ratio, game vs
DLC) and convergence criteria (≥N review matches and/or review+game match). Default client re‑rank weights begin 50/50 between
semantic and textual signals; users can tune locally without new server queries. ETL bounds review sampling at 10,000 per game
(ordered by helpfulness/recency) to be courteous to upstream APIs.

## Technical Context
**Language/Version**: C# / .NET 10 (+ .NET Aspire) for API and orchestration; Static frontend with HTML/CSS/JS (Bootstrap preferred)  
**Primary Dependencies**: Cosmos DB for NoSQL (DiskANN vectors for hybrid search), Ollama (Embedding Gemma, 768‑dim), Bootstrap  
**Storage**: Cosmos DB NoSQL (prod). For dev, emulator via Aspire; optional local store for prototyping.  
**Testing**: xUnit for unit/integration; contract tests for API (OpenAPI‑based). Perf smoke for search flows.  
**Target Platform**: GitHub Codespaces (Linux devcontainer) with Docker‑in‑Docker; deployable to Azure.  
**Project Type**: web (frontend + backend)  
**Performance Goals**: 
- Best‑effort initial results: P95 ≤ 3s (warm). Aim for a tiny preview subset ≤ 300ms when cached/cheap.  
- Candidate pool (up to cap) delivery target: P95 ≤ 1.5s (warm) when feasible; otherwise start an async high‑quality job.  
- Quality‑over‑latency policy: If forced to choose between ~300ms middling quality and a BLAST‑style asynchronous job of up to ~30s that yields markedly higher quality while keeping costs low, prefer the latter. Provide immediate preliminary results and update/replace with the high‑quality results upon job completion (polling or SSE).  
**Constraints**: Cost‑optimized, CPU‑first dev; public repository; AI‑driven solo development; use chat slash commands for governance phases.  
**Scale/Scope**: Seed corpus of released Steam games with up to 200 reviews per game for relevance; ETL sample cap 10,000 reviews per game for ratio metrics.

## Constitution Check
- Open Source & Public by Default: PASS — all artifacts public; no secrets in repo.  
- Solve It Right, Once: PASS — convergence filter and grouped payloads minimize hacks; durable design.  
- Pragmatic & Idiomatic Tech: PASS — .NET Aspire, Cosmos DB + DiskANN, and Ollama (Embedding Gemma).  
- Ultra‑Low‑Cost, High‑Accuracy: PASS — CPU‑friendly model, candidate cap controls, Cosmos DiskANN.  
- Test‑First, Observability, Simplicity: PASS — contract tests planned; logs/health/perf smoke included.

## Project Structure

### Documentation (this feature)
```
specs/002-we-intend-to/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/
```

**Structure Decision**: Web application (frontend + backend)

## Reality Check (as of 2025-09-23)
- Minimal API endpoints are mapped directly in `src/ActualGameSearch.Api/Program.cs` (no `Endpoints/` folder yet).
- Repositories: `CosmosGamesRepository` and `CosmosReviewsRepository` live under `src/ActualGameSearch.Api/Data/`; in-memory fallbacks also present.
- Embedding adapter: `TextEmbeddingService` under `src/ActualGameSearch.Core/Embeddings/` using Ollama HTTP; `NoopEmbeddingService` for tests.
- Tests: Contract and Integration tests exist and currently pass against the implemented endpoints with `{ ok, data.items }` payloads.

## Phase 0: Outline & Research
1) Unknowns extracted: 
- Normalization rules specifics (dates/locales/tags/platforms) → capture in data dictionary.  
- Exact Cosmos DB hybrid scoring knobs and DiskANN index settings → confirm from docs.  
- Candidate streaming/pagination tradeoffs with cap ranges (200–2,000).  
2) Research tasks dispatched:
- Best practices: Cosmos DB hybrid vector + text with DiskANN for NoSQL.  
- Best practices: Efficient client‑side re‑ranking and convergence signaling.  
- Patterns: ETL for Steam reviews with fair‑use (throttling/backoff) and dedup heuristics.  
3) Consolidated findings → see `research.md`.

**Output**: `research.md` with decisions, rationale, and alternatives.

## Phase 1: Design & Contracts
1) Entities from spec → see `data-model.md` (Game, Review, Candidate, FilterSet, ReRankWeights, QuerySession).  
2) API contracts → OpenAPI in `contracts/openapi.yaml` for:
- GET `/api/search` with filters and candidateCap; returns grouped candidates  
- GET `/api/similar/{gameId}` returns related candidates  
3) Contract tests → Scenarios documented alongside OpenAPI; failing tests to be created during implementation setup.  
4) Quickstart → `quickstart.md` for devcontainer, Aspire orchestration, and local run steps.  
5) Agent guidance → Keep Copilot context updated; adhere to constitution guardrails.

**Output**: `data-model.md`, `contracts/openapi.yaml`, `quickstart.md`.

## Phase 2: Task Planning Approach
- Generate tasks from contracts, data model, and quickstart; include perf smoke tests and convergence logic.  
- TDD: contract tests then implementation; models → services → API → UI; mark independent items [P] for parallel work.  
- Estimated: 25–30 tasks; `/tasks` will create `tasks.md`.

## Phase 3+: Future Implementation
- Phase 3: /tasks command generates `tasks.md`  
- Phase 4: Implement per tasks and constitution  
- Phase 5: Validate tests and perf targets

## Complexity Tracking
| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Progress Tracking
**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [ ] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [ ] Complexity deviations documented

---
*Based on Constitution v1.1.0 - See `.specify/memory/constitution.md`*
