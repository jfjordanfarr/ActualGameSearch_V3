# Tasks: 003 – Path to Persistence

**Input**: Design documents from `/specs/003-path-to-persistence/`
**Prerequisites**: plan.md (present), research.md (planning), data-model.md (planning), contracts/ (N/A for this feature)

## Format: `[ID] [P?] Description`
- [P]: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Phase 3.1: Setup
- [ ] T001 Create local data lake root and partitions at `AI-Agent-Workspace/Artifacts/DataLake/{bronze,silver,gold}/`; ensure `.gitignore` excludes large artifacts; add `AI-Agent-Workspace/Artifacts/DataLake/README.md` describing layout and run IDs.
- [ ] T002 Add Worker options to `src/ActualGameSearch.Worker/appsettings.json`: `DataLake:Root` (default `AI-Agent-Workspace/Artifacts/DataLake`), `Ingestion:MaxConcurrency` (default 4), `Ingestion:ReviewCapBronze` (default 10), `Cadence:StorePages` (weekly), `Cadence:News` (weekly), and enable review delta policy.
- [ ] T003 [P] Create `src/ActualGameSearch.Worker/Storage/DataLakePaths.cs` for partitioned path construction (bronze/silver/gold, runId, yyyy-mm-dd, appid) and safe filename rules.
- [ ] T004 [P] Create `src/ActualGameSearch.Worker/Storage/RunStateTracker.cs` to persist/restore resumable checkpoints (random-without-replacement) per run.
- [ ] T005 [P] Create `src/ActualGameSearch.Worker/Storage/ManifestWriter.cs` to emit per-partition `manifest.json` with counts, durations, error classes, and input parameters for audit.

## Phase 3.2: Tests First (TDD) – MUST COMPLETE BEFORE 3.3
- [ ] T006 [P] Unit tests for DataLakePaths in `tests/ActualGameSearch.UnitTests/DataLakePathsTests.cs` (partition paths, runId, gzip/json, parquet/snappy naming).
- [ ] T007 [P] Unit tests for RunStateTracker in `tests/ActualGameSearch.UnitTests/RunStateTrackerTests.cs` (resume from last success, idempotency, random-without-replacement correctness).
- [ ] T008 [P] Unit tests for ReviewSanitizer in `tests/ActualGameSearch.UnitTests/ReviewSanitizerTests.cs` (strip PII fields, preserve review hyperlink fields).
- [ ] T009 Integration: Bronze review ingestion writes full review payloads with per-app cap in `tests/ActualGameSearch.IntegrationTests/BronzeReviewIngestionTests.cs`.
- [ ] T010 Integration: Concurrency gate enforces cap=4 with backoff in `tests/ActualGameSearch.IntegrationTests/ConcurrencyGateTests.cs`.
- [ ] T011 Integration: Resumable ingestion (interrupt + resume) without duplicate requests in `tests/ActualGameSearch.IntegrationTests/ResumeIngestionTests.cs`.
- [ ] T012 Integration: Silver classifies content type and flags duplicate/localized appids in `tests/ActualGameSearch.IntegrationTests/SilverRefinementTests.cs`.

## Phase 3.3: Core Implementation (ONLY after tests are failing)
- [ ] T013 [P] Implement polite Steam client with backoff in `src/ActualGameSearch.Worker/Services/SteamHttpClient.cs` (headers, retries, jitter, 429/5xx handling).
- [ ] T014 [P] Implement BronzeReviewIngestor capturing full review payloads (cap from settings) to `bronze/steam-reviews/{date}/{appid}/page_{n}.json.gz` in `src/ActualGameSearch.Worker/Ingestion/BronzeReviewIngestor.cs`.
- [ ] T015 [P] Implement BronzeStoreIngestor (appdetails) to `bronze/steam-appdetails/{date}/{appid}.json.gz` in `src/ActualGameSearch.Worker/Ingestion/BronzeStoreIngestor.cs`.
- [ ] T016 [P] Implement BronzeNewsIngestor (news/patch notes) to `bronze/steam-news/{date}/{appid}.json.gz` in `src/ActualGameSearch.Worker/Ingestion/BronzeNewsIngestor.cs`.
- [ ] T017 Implement IngestionCoordinator with random-without-replacement iterator, concurrency cap (4), resumable checkpoints, and per-source cadences in `src/ActualGameSearch.Worker/Ingestion/IngestionCoordinator.cs`.
- [ ] T018 Implement ReviewSanitizer per FR-013 in `src/ActualGameSearch.Worker/Processing/ReviewSanitizer.cs` (strip usernames/profiles, preserve review links).
- [ ] T019 Implement SilverRefiner to standardize fields (timestamps, languages), de-duplicate, annotate content type and duplicates; emit Parquet (Snappy) under `silver/` in `src/ActualGameSearch.Worker/Refinement/SilverRefiner.cs`.
- [ ] T020 Implement GoldDeriver to compute metrics, apply policy, and emit `gold/candidates.parquet` + `gold/candidates.csv` with `policy_version`, `run_id`, evidence refs in `src/ActualGameSearch.Worker/Refinement/GoldDeriver.cs`.
- [ ] T021 Wire CLI commands in `src/ActualGameSearch.Worker/Program.cs`: `ingest bronze|silver|gold` with flags `--sample|--full`, `--since`, `--until`, and config overrides; print run summary.

## Phase 3.4: Integration
- [ ] T022 Add strongly-typed options (`WorkerOptions`) for DataLake, Ingestion, Cadence in `src/ActualGameSearch.Worker/Models/WorkerOptions.cs` and register in DI.
- [ ] T023 Add run summary artifacts (counts, avoided calls, errors, throughput) to `ManifestWriter` and write to `AI-Agent-Workspace/Artifacts/DataLake/reports/{date}/run-summary.json`.
- [ ] T024 Document weekly recrawl and review delta strategy in `specs/003-path-to-persistence/quickstart.md` with example commands and expected outputs.
- [ ] T025 Add compliance/TOS note and throttling guidance to `AI-Agent-Workspace/Docs/steam-compliance.md` (avoid PII, preserve links, max concurrency 4, caching intent).
 - [x] T026 Add optional S3-compatible backup sync script (`AI-Agent-Workspace/Scripts/backup_rclone.example.sh`) and doc (`AI-Agent-Workspace/Docs/backups-and-egress.md`) covering R2/B2/S3/MinIO via rclone; include dry-run and size accounting examples. Default examples should use Cloudflare R2.
 - [ ] T027 Implement Worker CLI verbs: `export pack` and `import unpack` to create/restore portable tar.zst archives per run/partition with manifest + checksums; store under `AI-Agent-Workspace/Artifacts/DataLake/exports/`. (Helper scaffold drafted; wiring and zstd selection pending.)

## Phase 3.5: Polish
- [ ] T026 [P] Create exploratory notebook `AI-Agent-Workspace/Notebooks/DataLake_Exploration.ipynb` (Python + DuckDB) with cells for: review count distributions, recent activity histograms, patch cadence, and correlation sketch.
- [ ] T027 Performance test: measure ingest throughput with cap=4; add assertions in `tests/ActualGameSearch.IntegrationTests/ThroughputTests.cs` for basic floor (document expected ballpark, allow override).
- [ ] T028 [P] Add initial retention analysis and cost notes to `specs/003-path-to-persistence/research.md` (defer concrete defaults until data volume observed); cross-reference `AI-Agent-Workspace/Background/SteamSeeker-2023/*` findings.
- [ ] T029 [P] Update `specs/003-path-to-persistence/plan.md` Progress Tracking and note any deviations.
- [ ] T030 Remove duplication, ensure logs are structured, and refresh READMEs (`AI-Agent-Workspace/Artifacts/DataLake/README.md`, `specs/003-path-to-persistence/quickstart.md`).

## Dependencies
- Phase 3.2 tests (T006–T012) must be written and FAIL before Phase 3.3 implementation (T013–T021).
- T003 blocks T006 (paths), T004 blocks T011 (resume), T005 blocks T009 (manifest assertions).
- T013–T016 can run in parallel; T017 depends on them.
- T019 depends on Bronze artifacts; T020 depends on Silver outputs.
- T022 (options) before T021 (CLI wiring) finalization.
- Documentation tasks (T024–T025) after ingestion behaviors are validated.

## Parallel Execution Examples
```
# Group 1: Unit tests in parallel
Task: "T006 Unit tests for DataLakePaths"
Task: "T007 Unit tests for RunStateTracker"
Task: "T008 Unit tests for ReviewSanitizer"

# Group 2: Bronze ingestors in parallel
Task: "T014 BronzeReviewIngestor"
Task: "T015 BronzeStoreIngestor"
Task: "T016 BronzeNewsIngestor"
```

## Validation Checklist
- [ ] All unit/integration tests defined before implementation tasks
- [ ] DataLake paths and run state tested
- [ ] Concurrency cap (4) enforced and tested
- [ ] Bronze stores full review payloads with cap (default 10) and strips PII
- [ ] Silver annotates content type and duplicate/localized variants
- [ ] Gold emits candidates parquet + CSV with evidence refs and policy/run metadata
- [ ] Quickstart and compliance docs updated