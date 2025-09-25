
# Implementation Plan: 003 – Path to Persistence

Branch: 003-path-to-persistence | Date: 2025-09-25 | Spec: /workspaces/ActualGameSearch_V3/specs/003-path-to-persistence/spec.md
Input: Feature specification from `/specs/003-path-to-persistence/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → Found and loaded: /workspaces/ActualGameSearch_V3/specs/003-path-to-persistence/spec.md
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → No explicit [NEEDS CLARIFICATION] markers remain; retention default values are deferred but acceptable for planning
   → Project Type detected: single (backend services + worker)
3. Fill the Constitution Check section based on the content of the constitution document.
4. Evaluate Constitution Check section below
   → No violations requiring ERROR; see notes
   → Initial Constitution Check: PASS
5. Execute Phase 0 → research.md
   → Completed (below; file created)
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific file
   → Completed (below; files created)
7. Re-evaluate Constitution Check section
   → Post-Design Constitution Check: PASS
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

IMPORTANT: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md (already present; will be verified/iterated)
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
Goal: Capture Steam data once (Bronze), refine into clean, queryable datasets (Silver), and derive a reproducible candidate set (Gold) for indexing—minimizing external calls, preserving provenance, and enabling empirical thresholding. Incorporate concrete signals: PatchNotesRatio from ISteamNews (tags=patchnotes), developer responsiveness and review update velocity from appreviews, and optional UGC velocity/maintenance from Workshop.

Approach: Implement a local-first medallion data lake under `AI-Agent-Workspace/Artifacts/DataLake` using gzip JSON for Bronze and Parquet (Snappy) + manifest for Silver/Gold. A .NET Worker orchestrates ingestion with strict concurrency (max 4), exponential backoff, resumable checkpoints, and configurable cadences (weekly store/news; delta reviews). Reviews are stored with full text in Bronze but capped per app (default 10). Silver annotates content types (games/DLC/demos/workshop), normalizes fields, and deduplicates; Gold emits a candidate list with metrics and citations to raw evidence.
Additionally, Silver introduces `NewsItemRefined` with sanitized `bodyClean` and `isPatchNotes`, and `RefinedGame` accumulates derived metrics (patchNotesRatio, devResponseRate, avgDevResponseTimeHours, reviewUpdateVelocity, ugcMetrics?).

SteamSeeker 2023 infusions (evidence-based metrics and workflow):
- Multilingual-safe text cleaning for reviews/store text (strip HTML/BBCode/links/newlines; avoid alpha-only filters to preserve non-Latin scripts).
- Review-level filters (applied at Silver during normalization, not at Bronze acquisition):
   - unique_word_count >= 20 (configurable), received_for_free == false (configurable), optional naughty-word filters deferred to analysis.
   - Compute per-app review_counts and allow Gold policies to require thresholds (e.g., >= 20) when cap allows.
- Derived review metrics (Silver): positivity_rating (up ratio), geometric means: word_count, unique_word_count, resonance_score, playtime_forever, num_games_owned, author_num_reviews.
- Time-weighted resonance: resonance_score / log_base_365(max(days_since_review, 1)) for recency emphasis.
- Game-level embedding derivation (Gold): weighted average of top-K review embeddings by time-weighted resonance, blended with a small metadata embedding weight (default 95% reviews, 5% metadata); K default up to 200 when available.
- Tiered capture policy: Bronze defaults to 10 reviews/app for breadth; upon promotion to Gold, extend Bronze for those appids up to a higher cap (e.g., 200) via delta fetch, then compute embeddings/metrics—still “fetch once, enrich later.”

## Technical Context
**Language/Version**: C# 12 (.NET 8.0)
**Primary Dependencies**: .NET Aspire (orchestration), System.Net.Http, Polly (retry/backoff) [planned], Parquet.NET (Parquet writer) [planned], Newtonsoft.Json/System.Text.Json
**Storage**: Local filesystem data lake; Bronze: JSON (.json.gz), Silver/Gold: Parquet (Snappy); manifests as JSON; future-compatible with Iceberg layout (not mandated)
**Testing**: xUnit via `dotnet test`; unit + integration tests under `tests/ActualGameSearch.*`
**Target Platform**: Linux dev container (Ubuntu 24.04) and cross-platform .NET
**Project Type**: single (src/* projects; Worker for ETL, Api unchanged)
**Performance Goals**: Respect max 4 concurrent outbound requests; avoid redundant calls via checkpoints; ingest long-tail over hours; weekly maintenance feasible on a developer laptop
**Constraints**: No PII persisted; preserve review links; adhere to platform TOS; retention configurable; local-first with a path to durable object storage later
**Scale/Scope**: All Steam appids with ≥1 review included at Bronze; review capture capped per app (default 10) with tiered extension for promoted Gold candidates (e.g., up to 200); datasets in the tens of thousands of titles with mixed content types

## Constitution Check
Gate: Must pass before Phase 0 research. Re-check after Phase 1 design.

Findings (v1.2.0):
- Open Source & Public by Default: All docs artefacts generated in-repo; no secrets.
- Solve It Right, Once: Durable data lake + resumable idempotent ingestion; avoids re-fetching.
- Pragmatic & Idiomatic: .NET 8 + Aspire; Cosmos/Ollama unaffected by this feature initially.
- Ultra‑Low‑Cost, High‑Accuracy: Local-first storage; review cap limits footprint; candidate derivation supports quality.
- Test‑First & Simplicity: Plan includes contract/unit/integration tests; minimal external deps.
- Evidence-Based Documentation: research.md/data-model.md/quickstart.md capture decisions and how to verify.

Risks/Deviations: None requiring ERROR. Worker may be wired to Aspire AppHost in a later step (acceptable sequencing).

## Project Structure

### Documentation (this feature)
```
specs/003-path-to-persistence/
├── plan.md              # This file (/plan output)
├── research.md          # Phase 0 output (/plan)
├── data-model.md        # Phase 1 output (/plan)
├── quickstart.md        # Phase 1 output (/plan)
├── contracts/           # Phase 1 output (/plan)
└── tasks.md             # Phase 2 output (/tasks – already present)
```

### Source Code (repository root)
```
src/
├── ActualGameSearch.Worker/      # ETL jobs (new files for this feature)
├── ActualGameSearch.Api/         # unchanged by this feature
├── ActualGameSearch.Core/        # domain models and abstractions
└── ActualGameSearch.ServiceDefaults/

tests/
├── ActualGameSearch.UnitTests/
├── ActualGameSearch.IntegrationTests/
└── ActualGameSearch.ContractTests/
```

Structure Decision: Option 1 (single repo with src/* + tests/*). No separate frontend work here.

## Phase 0: Outline & Research
Unknowns resolved and decisions recorded in research.md:
- Parquet writer for .NET: choose Parquet.NET for simplicity and pure C#; ParquetSharp kept as alternative.
- Checkpointing: file-based JSON checkpoints per partition/run for simplicity; later optional SQLite.
- Manifest format: JSON with checksums, counts, byte sizes, and lineage (run ids and source paths).
- Backoff: Exponential backoff with jitter using Polly (if added) or custom minimal logic.
- Sampling modes: small seeds (popular/mid/long-tail) supported via CLI flags.
- Retention defaults: remain configurable; not hard-coded in planning (documented as TBD values).
- SteamSeeker-derived metrics and multilingual handling to be incorporated in Silver/Gold computations; tiered review capture introduced to reconcile storage cost vs metric fidelity.

Output: research.md created with decisions, rationale, alternatives.

## Phase 1: Design & Contracts
Design outputs created:
1) data-model.md: Entities and schemas (Run, RawArtifact, Review, StorePage, NewsItem, RefinedGame, Candidate, Manifests) with key fields and relationships.
2) contracts/: No new public HTTP endpoints in this feature. Added worker CLI contract and file layout contract documentation.
3) quickstart.md: How to run the worker locally (post-implementation), directory layout, and smoke checks.
4) Agent context: Will be updated via `.specify/scripts/bash/update-agent-context.sh copilot`.

Appendix: SteamSeeker-derived metric definitions (to guide implementation tests)
- positivity_rating = count(voted_up)/count(all)
- geometric means: use gmean over clipped-to-≥1 values for: word_count, unique_word_count, resonance_score, author.playtime_forever, author.num_games_owned, author.num_reviews
- time_weighted_resonance(review) = resonance_score / log_365(max(days_since_review, 1))
- game_embedding(review+metadata): average of review embeddings weighted by time_weighted_resonance, then blend with metadata embedding: E_game = average(E_reviews, weights=w_i) ⊕ 0.05·E_metadata (defaults: weights normalized; metadata weight configurable)

Re-check Constitution: Post-Design PASS.

## Phase 2: Task Planning Approach
Task Generation Strategy:
- Derive tasks from data-model.md, contracts, and quickstart.
- Emphasize TDD: unit tests for path helpers, checkpointing, manifests; integration tests for a tiny sample run.
- Keep parallelizable tasks for independent files (marked [P]).

Ordering Strategy:
- Tests → implementation; models → services → wiring.

Estimated Output: ~25–30 tasks. Note: tasks.md already exists and aligns with this approach; we will iterate via /verify as needed.

## Phase 3+: Future Implementation
Out of scope for /plan. Implementation will target the Worker with tests first, then wiring to Aspire.

## Complexity Tracking
No constitutional deviations requiring justification at this time.

## Progress Tracking
Phase Status:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

Gate Status:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved (defaults for retention remain configurable by design)
- [x] Complexity deviations documented (N/A)

---
Based on Constitution v1.2.0 - See `.specify/memory/constitution.md`
