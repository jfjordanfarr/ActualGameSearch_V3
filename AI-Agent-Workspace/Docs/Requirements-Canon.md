Requirements Canon (living source of truth)

Purpose
- Consolidate user-defined intent into a single, citable document backed by sources (conversation summaries and spec-kit).
- Keep it concise, versionable, and organized for day-to-day engineering.

Sources
- Conversation summaries:
  - SUMMARIZED_0001_2025-09-20.md, SUMMARIZED_0002_2025-09-21.md, SUMMARIZED_0003_2025-09-22.md,
  - SUMMARIZED_0005_2025-09-24.md, SUMMARIZED_0007_2025_09_24.md, SUMMARIZED_0008_2025_09_26.md,
  - SUMMARIZED_0009_2025_09_28.md
- Spec-kit:
  - specs/002-we-intend-to/{spec.md, plan.md, tasks.md}
  - specs/003-path-to-persistence/{spec.md, plan.md, tasks.md, data-model.md}
  - AI-Agent-Workspace/Docs/backups-and-egress.md

## Vision
- Ultra-low-cost, high-quality hybrid search for games with client-side re-ranking; open-source reference at actualgamesearch.com.
- Stack: .NET 8 Aspire, Cosmos DB NoSQL + DiskANN, Ollama embeddings (nomic-embed-text, 768-dim), deterministic ranker; forward-compatible with .NET 10.
- Correctness-first: no silent chunking or deterministic fallbacks; fail fast unless explicitly opted in.  
  (Sources: SUMMARIZED_0001; SUMMARIZED_0009 Parts 11–14; 002/spec.md Overview)

## High-level outcomes (what “good” looks like)
- Visitors describe a “dream game” and get ranked candidates with short evidence and sliders to adjust weights; client re-ranking without new server calls.  
  (Sources: 002/spec.md FRs; SUMMARIZED_0001)
- Ingestion persists authentic Steam data (store, reviews, news) once (Bronze), refines to Silver, derives Gold candidates with provenance and metrics.  
  (Sources: 003/spec.md; 003/plan.md; SUMMARIZED_0007/0008)
- Embeddings stable and reproducible: ensure-model then readiness, consistent 8k context in AppHost dev runs.  
  (Sources: SUMMARIZED_0009 Parts 5–14)

## Functional requirements (condensed)
- Search API
  - Accept prompt; return candidate set for client re-ranking; grouped-by-game results; evidence snippets (1–3).
  - Support local re-ranking via sliders (semantic vs textual, positivity, richness, playtime).  
  (Sources: 002/spec.md FR-001..FR-032; tests in Contract/Integration)
- Filters & convergence
  - Server-side filters: release date, controller support, adult flag, min reviews, purchase-origin ratio, content type.  
  - Convergence modes: min matching reviews and/or review+game-description; singleton games optionally included.  
  (Sources: 002/spec.md FR-020..FR-028)
- Ingestion (Bronze → Silver → Gold)
  - Bronze candidacy: include any app with ≥10 recommendations (configurable).  
  - Reviews: store full payload in Bronze with small per-app cap (default 10); Gold promotes to higher cap (e.g., 200).  
  - Associated appids: up to 99 related appids per true game (≤100 records per game).  
  - Concurrency: hard cap 4; backoff; random-without-replacement; resumable.  
  - Cadence: weekly store/news; delta reviews.  
  (Sources: 003/spec.md FR-019..FR-023, FR-028..FR-034; data-model.md)
- Data ownership & portability
  - Local data lake is canonical; S3-compatible backup target optional (R2-first).  
  - Export/import tar.zst archives; manifests with checksums.  
  (Sources: 003/spec.md FR-024..FR-028; backups-and-egress.md)

## Non-functional requirements (condensed)
- Correctness-first embeddings
  - Enforce configured context (target 8192 in AppHost dev); fail if exceeded unless operator enables chunking.  
  - Normalize embedding endpoint(s); ensure-model (show → pull → create), readiness poll + warm-up.  
  (Sources: SUMMARIZED_0009; infrastructure/Modelfile.nomic-embed-8k)
- Reliability & etiquette
  - Steam: Retry-After-aware backoff, cursor-sticky pagination, single connection default.  
  - Worker resilience: non-fatal per-app errors, surfaced diagnostics; resume from checkpoints.  
  (Sources: SUMMARIZED_0008; 003/spec.md)
- Configuration
  - Single-source via AppHost env in dev; bind strongly-typed options; avoid magic strings.  
  (Sources: copilot-instructions; SUMMARIZED_0009)
- Observability
  - Aspire dashboard; structured logs; minimal health probes (embedding health includes dims/model/num_ctx).  
  (Sources: SUMMARIZED_0008/0009)

## Policies and invariants
- Do not silently chunk or mean-pool by default; make it an explicit, opt-in mode.  
  (Sources: SUMMARIZED_0009 Part 11)
- Ask Steam once; local lake is the source of truth; cloud indices are derivatives.  
  (Sources: SUMMARIZED_0007/0008; 003/spec.md)
- Enforce politeness (429s should be rare; if they occur, backoff and do not introduce holes).  
  (Sources: SUMMARIZED_0008)
- Bronze candidacy uses recommendations.total (configurable min, default 10); up to 99 related appids per true game.  
  (Sources: SUMMARIZED_0009 Part 1; 003/spec.md FR-029)

## User stories (selected)
- As a visitor, I can describe my desired game and adjust sliders to refine results without extra server calls.  
  (Sources: 002/spec.md User Scenarios)
- As a maintainer, I can run Bronze ingestion that is resumable, polite to Steam, and produces manifests and raw JSON.gz under a predictable layout.  
  (Sources: 003/spec.md Acceptance Scenarios)
- As an operator, I can verify embeddings health quickly (dims=768, model tag, num_ctx) and know whether the 8k model is active.  
  (Sources: SUMMARIZED_0009 Completion Checklist)

## Gaps and follow-ups
- Document search API parameter surface (beyond minimal UI) with examples; ensure OpenAPI reflects convergence and filters fully.  
  (Sources: 002/spec Reality Check)
- Add embedding readiness/warm-up implementation and tests; normalize on one endpoint path in Worker.  
  (Sources: SUMMARIZED_0009)
- Address Cosmos init NRE(s) observed during AppHost runs before scaling Bronze.  
  (Sources: SUMMARIZED_0009 Part 14)

Changelog
- 2025-09-29: Initial consolidation from conversation summaries and spec-kit documents.
