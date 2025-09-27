# Feature Specification: 003 – Path to Persistence

**Feature Branch**: `003-path-to-persistence`  
**Created**: 2025-09-25  
**Status**: Draft  
**Input**: User description: "Build a persistence-first, storewide ingestion and refinement effort for Steam app data so we only fetch once, keep raw evidence, and empirically decide cutoffs (legitimate titles) before production indexing."

## Execution Flow (main)
```
1. Parse user description from Input
   → Done
2. Extract key concepts from description
   → Actors: project maintainer, background ingestion/refinement jobs, reviewers of datasets
   → Actions: capture once, persist raw, refine iteratively, derive candidate set, measure thresholds empirically
   → Data: Steam catalog, store pages, reviews, patch notes; run metadata; threshold policies; candidate list
   → Constraints: minimize external API calls, reproducibility, local-first, path to durable cloud storage
3. For each unclear aspect
   → Mark with [NEEDS CLARIFICATION]
4. Fill User Scenarios & Testing
   → Added below
5. Generate Functional Requirements
   → Added below (testable, business-focused)
6. Identify Key Entities
   → Added below
7. Review Checklist
   → Present below
8. Return: SUCCESS (spec ready for planning)
```

---

## ⚡ Quick Guidelines
- Focus on WHAT the system must achieve for maintainers/users and WHY (reduce cost, increase durability, enable empirical selection of legitimate titles)
- Avoid implementation-specific details (no specific databases, libraries, or code structures mandated)
- Write for stakeholders who care about durability, cost, auditability, and decision quality

---

## Clarifications

### Session 2025-09-25
- Q: What qualifies for Bronze inclusion and how should reviews be captured? → A: Include any app with at least one published review; capture full review payloads in Bronze while capping stored reviews per app to a small configurable maximum (default 10) to control local storage.
- Q: Should non-game content (DLC, demos, soundtracks, tools, workshop) be excluded? → A: Do not exclude by type at Bronze if reviewed; capture all reviewed appids and classify/annotate content type during Silver to allow inclusion (e.g., DLC, workshop) in downstream relevance and recommendations.
- Q: What concurrency and iteration strategy should ingestion follow? → A: Strict cap of max 4 parallel outbound requests with backoff; prefer random-without-replacement iteration over large appid sets; runs must be resumable from last success checkpoints.
- Q: What are the default recrawl cadences? → A: Weekly recrawl of store pages and news/patch notes; reviews fetched as deltas by page/cursor policy; all cadences are configurable.
- Q: What storage layout and formats should be used locally? → A: Data lake root under `AI-Agent-Workspace/Artifacts/DataLake` with Bronze JSON (gzip) and Silver/Gold Parquet (Snappy) plus a manifest; aim for an Iceberg-compatible path in the future but do not mandate it now.

## User Scenarios & Testing (mandatory)

### Primary User Story
As the project maintainer, I want to capture Steam game data once and retain the raw evidence so that I can refine, audit, and empirically determine legitimacy thresholds without repeatedly calling external APIs or losing provenance.

### Acceptance Scenarios
1. Given no prior data, when I execute the storewide ingestion, then the system persists timestamped raw responses (catalog, store pages, reviews, patch notes) with run metadata, and a summary report is produced.
2. Given raw data exists, when I run refinement, then the system produces curated, queryable datasets with standardized fields, de-duplication, and basic quality indicators, along with a summary of record counts and any discarded records with reasons.
3. Given refinement outputs and a threshold policy, when I derive the candidate set, then the system outputs a reproducible list of “legitimate titles” with an accompanying rationale (metrics snapshot) and an audit trail linking back to raw sources.
4. Given partial prior runs or interruptions, when I re-run ingestion/refinement, then the operation is resumable and does not duplicate work or exceed rate limits beyond defined allowances.
5. Given repeated execution on a short interval, when ingestion is invoked again, then the system detects already-seen inputs and avoids unnecessary external requests while still capturing allowed deltas.
6. Given a title is selected as a Gold candidate and only Bronze-capped reviews (e.g., 10) exist locally, when I run the derive flow with tiered capture enabled, then the system performs a targeted delta to extend reviews for that app up to the configured Gold cap (e.g., 200) without duplicating prior pages and then computes metrics/embeddings for derivation.

### Edge Cases
- External API instability (rate limits, schema drift, partial pages). The system must fail gracefully, record context, and allow resume.
- Extremely large or sparse applications (very high/low review volumes). The system must not time out unbounded; it should use bounded pages/runs.
- Content type differences (games vs DLC/demos/non-games). The system must provide filters and reasons for exclusion.
- Clock/timezone inconsistencies across sources. The system must normalize timestamps at refinement time.
- Data retention vs cost constraints. The system must make retention configurable while preserving auditability of decisions.
 - Duplicate or localized variants of titles across multiple appids (region/language-specific releases). The system should detect and annotate potential duplicates during Silver for de-dup/reconciliation analysis.

## Requirements (mandatory)

### Functional Requirements
- FR-001: The system MUST support a one-time storewide catalog pass to enumerate app identifiers and core descriptors used for downstream targeting.
- FR-002: The system MUST persist raw external responses immutably with timestamped partitions and run metadata to enable full reproducibility and audit trails.
- FR-003: The system MUST provide a resumable, idempotent ingestion behavior that avoids redundant external requests for already-captured items within configurable windows.
- FR-004: The system MUST produce a human-readable summary after each run (counts, durations, error classes, skipped items, newly added items).
- FR-005: The system MUST offer a refinement stage that standardizes fields (e.g., dates, categories, languages), de-duplicates records, and flags inconsistencies without discarding source evidence.
- FR-006: The system MUST expose data selection thresholds as configuration (e.g., minimum review counts, recency/activity windows, evidence weights) and allow recording the exact policy used per run.
- FR-007: The system MUST generate a “candidate titles” output that lists qualifying apps with accompanying metrics and references to the evidence that justified inclusion.
- FR-008: The system MUST support sampling modes (e.g., top/popular, mid-tier, long-tail) to enable fast exploratory analysis before full-scale runs.
- FR-009: The system MUST record performance/health metrics sufficient to compare strategies across runs (e.g., items processed, external calls avoided, errors, throughput), without mandating a specific telemetry stack.
- FR-010: The system MUST document the storage layout conventions (partitions, naming, run IDs) and the schemas for refined/candidate outputs in plain prose artifacts that ship with the repo.
- FR-011: The system MUST provide a basic audit mechanism to trace any refined/candidate record back to its raw sources and the policy used to include/exclude it.
- FR-012: The system MUST be usable entirely in a local developer environment by default, with a clearly-defined path to durable object storage in a hosted environment.
- FR-013: The system MUST avoid storing PII (e.g., usernames, profiles) and MUST comply with platform terms; preserve review hyperlinks for provenance while stripping PII; store full review text in Bronze subject to a configurable per-app cap.
- FR-014: The system MUST make retention policies configurable (how long to keep raw, refined, and candidate artifacts) and MUST support space-aware cleanup that keeps audit essentials. Defaults will be defined after cost/size analysis; defer default values to planning.
- FR-015: The system MUST provide guardrails to prevent accidental deletion of raw evidence when clearing refined/candidate outputs.
- FR-016: The system SHOULD provide convenience notebooks and/or reports enabling empirical threshold discovery over refined datasets (distributions, correlations, outlier detection) without mandating a specific analytics engine.
- FR-017: The system SHOULD provide test fixtures and small public sample slices to allow CI validation without bundling large datasets.
 - FR-018: Bronze inclusion MUST include any appid with at least one published review; do not exclude DLC/demos/workshop at Bronze—content types are annotated in Silver for downstream filtering and recommendations.
 - FR-019: Bronze reviews MUST capture full review payloads; a configurable limit (default 10) caps the number of stored review documents per app to control local storage footprint.
 - FR-020: Ingestion MUST enforce a hard cap of 4 concurrent outbound requests with exponential backoff, random-without-replacement iteration over appids, and resumable checkpoints.
 - FR-021: Default recrawl cadences MUST be weekly for store pages and news/patch notes and delta-based for reviews (by page/cursor); cadences must be configurable.
 - FR-022: The local data lake root MUST be `AI-Agent-Workspace/Artifacts/DataLake`; Bronze stored as gzip-compressed JSON; Silver/Gold stored as Parquet with Snappy compression and an accompanying manifest; an Iceberg-compatible path is a future objective but not a hard requirement.
 - FR-023: The system MUST support tiered review capture: Bronze uses a small per-app cap by default for breadth, and Gold derivation MUST be able to trigger targeted review deltas to extend specific appids up to a higher cap before computing metrics/embeddings, without re-fetching already captured pages.

#### Scale Targets and Content Scope (Bronze→Gold)
- FR-029: Bronze/Silver scale targets per game SHOULD allow up to 2,000 reviews, 2,000 news items, and 2,000 workshop items captured, subject to practical API/page-size limits and runtime budgets; actual caps are configurable per run.
- FR-030: Gold selection per game SHOULD target up to 200 reviews, 200 news items, and 200 workshop items, chosen by rank combining semantic richness (unique token count), community helpfulness, hours played (when available), and recency.
- FR-031: Bronze news capture MUST NOT exclude by tag by default; tag filters (e.g., patchnotes) are optional flags. Silver MUST classify news into patch/update vs. marketing/other for downstream filtering.
- FR-032: Bronze SHOULD include Steam Workshop content (UGC) via `IPublishedFileService/QueryFiles/v1` with configurable limits and stored as raw JSON.gz under `bronze/workshop/...` with manifests.

#### Operability
- FR-033: Long-running Bronze ingestion MUST support resume across process restarts via persistent run-state checkpoints keyed by runId; iteration order remains random-without-replacement for pending items.

#### Data Ownership & Portability
- FR-024: Canonical Source of Truth. The canonical store for Steam data MUST be the local filesystem data lake (Bronze/Silver/Gold + manifests). Any cloud database or index (e.g., Cosmos, vector stores) is derivative and MUST be reconstructable from the filesystem alone.
- FR-025: Vendor-Neutral Backups. The system MUST support an optional S3-compatible backup target configured via environment/JSON options without code changes. Preferred primary is Cloudflare R2 (for $0 public egress and S3-compatibility), but any S3-compatible target (e.g., Backblaze B2, MinIO, AWS S3) is acceptable.
- FR-026: Export/Import. The Worker CLI MUST expose portable export/import: “export pack” (create tar.zst archives per partition/run with manifest + checksums + license/provenance note) and “import unpack” (restore archives to the data lake layout) without network access.
- FR-027: Egress-Aware Mirroring. Backups MUST be retrievable via standard tools (e.g., aws s3, rclone) and include byte-size/accounting in manifests to reason about egress/cost. Provide a simple “dry-run” report mode.
- FR-028: Git Scope. The repository MUST exclude large artifacts from Git. Tiny, safe, curated samples MAY be tracked via Git LFS strictly for demos; production datasets MUST NOT rely on Git/LFS as a primary store.

### Key Entities (data-oriented)
- Run: A single execution instance (ingestion or refinement) capturing timing, parameters, and outcomes.
- Partition: A time-based or run-based subdivision of storage (used to organize raw and refined artifacts).
- Raw Artifact: Source evidence captured as-is from external systems, immutable once written.
- Refined Dataset: Standardized, de-duplicated, and query-friendly view derived from raw artifacts.
- Threshold Policy: A set of criteria used to select or score titles for candidacy.
- Candidate Set: The resulting list of titles proposed for production indexing, with supporting metrics and references to evidence.
- Audit Record: Linkage that explains how a refined/candidate record was produced from raw sources and under which policy.

---

## Review & Acceptance Checklist

### Content Quality
- [x] No mandatory implementation details (specific technologies) are required to understand the feature’s value
- [x] Focused on user value (durability, cost efficiency, evidence-based selection)
- [x] Written for non-technical stakeholders, emphasizing outcomes and controls
- [x] All mandatory sections completed

### Requirement Completeness
- [ ] No [NEEDS CLARIFICATION] markers remain (to be resolved in planning)
- [x] Requirements are testable and unambiguous at a business level
- [x] Success criteria are measurable (counts, avoidance, reproducibility, traceability)
- [x] Scope is bounded (ingest → refine → candidate; local-first, path to hosted)
- [x] Dependencies and assumptions identified (external API, storage capacity, policy constraints)

---

## Execution Status

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [ ] Review checklist passed (pending clarifications)
