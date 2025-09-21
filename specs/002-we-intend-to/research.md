# Research: Hybrid Game Search (Cosmos DB + DiskANN + Client Re‑rank)

Date: 2025‑09‑21 | Branch: `002-we-intend-to`

## Goals
- High‑accuracy, low‑cost hybrid search for games using both full‑text and vector similarity.
- Deliver a large grouped candidate pool to the client for local re‑ranking, respecting a tunable cap (200–2,000).
- Enable convergence filtering by review and game signals.

## Key Findings

### 1) Cosmos DB NoSQL hybrid search with DiskANN
- DiskANN indexes in Cosmos enable sub‑second vector similarity without GPU; combine with full‑text.
- Common tuning knobs: vector dimensionality (e.g., 768 for Gemma embeddings), efConstruction, maxDegree; query‑time efSearch.
- Hybrid strategy: run vector top‑K to a high K (e.g., 5–10× candidate cap) and intersect/union with text top‑K, scoring via weighted sum (50/50 default) or learned linear weights.
- Cost controls: narrow indexed fields; pre‑filter with strict booleans (adult flag, controller support) before vector search where possible; cache frequent queries.

### 2) Client‑side re‑ranking
- Send grouped candidates (by `gameId`) with per‑game candidate slices (reviews + summary fields) to enable local tuning.
- Include feature vectors and textual scores optionally (hashed/quantized) to avoid re‑querying for small client adjustments.
- Expose defaults via `ReRankWeights { semantic: 0.5, text: 0.5 }`; keep deterministic ordering when equal.

### 3) Convergence filtering and integrity
- Two indicators: (a) pileups (≥N review matches near each other by semantic score), (b) game‑level match combined with at least 1 review match.
- ETL ensures review normalization, deduplication, and provenance tracking; preserve mapping to original source with fair‑use throttles.

### 4) ETL standards
- Normalize fields: dates (UTC ISO 8601), languages (BCP 47), platforms (enum), tags (lower‑snake), prices (micros, currency code), regions.
- Dedup via content hash + authorID + time bucket heuristic; mark suspected dupes with confidence.
- Respect sample caps: up to 10,000 reviews per game for ratio metrics; per‑query relevance uses smaller working sets (e.g., 200 recent/helpful).

## Alternatives Considered
- Postgres pgvector: great developer ergonomics, but Cosmos integrates better with Azure and DiskANN at scale; chosen for cost/runtime simplicity.
- GPU embedding models: skipped for dev; Ollama + Gemma embeddings are CPU‑friendly and adequate.

## Risks and Mitigations
- Risk: High RU cost for hybrid. Mitigation: Pre‑filters, caching, capped candidates, narrow projections.
- Risk: Data quality drifts. Mitigation: Data dictionary, ETL checks, contract tests.
- Risk: Over‑tight convergence hides niche matches. Mitigation: make thresholds user‑tunable and transparent.

## Next
- Lock vector dim to embedding model (Gemma 2B Embeddings ~768). Document index config in `data-model.md`.
- Prototype hybrid query in a small sandbox with synthetic data.
