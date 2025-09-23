# ActualGameSearch Data Dictionary: Steam Ingestion and Normalization

This document captures the fields we ingest from Steam (store metadata and reviews), our normalized schema, and rules that govern data quality, language, dates, and provenance. It is versioned to support safe, explicit schema evolution.

## 1. Scope and Sources

- Source: `https://store.steampowered.com/api/appdetails?appids={appid}` (Store metadata)
- Source: `https://store.steampowered.com/appreviews/{appid}` (Cursor-paginated, `json=1`, `filter=recent`, `language=all`, `purchase_type=all`)
- Optional: ISteamNews for patch notes via `tags=patchnotes` (future, not yet ingested)

## 2. Normalized Containers

### 2.1 Games (Cosmos container: `games`)

- `id` (string): Steam `steam_appid` as string. Partition key.
- `title` (string): Steam `name`.
- `tagSummary` (string[]): Up to 8 from `genres` (fallback `categories` when missing).
- `reviewCount` (int): From `recommendations.total` when available, else 0.
- `price` (int|null): `price_overview.final` in minor units (Steam format) or null.
- `header` (string|null): `header_image` URL.
- `when` (string|null): `release_date.date` retained as string for display; do not sort on this.
- `gvector` (float32[d]): Embedding of `short_description + detailed_description (HTML stripped)`.
- `type` (string): Steam `type` (e.g., `game`, `dlc`). DLC are included as first-class items.

Notes:
- HTML in `detailed_description` is stripped server-side before embedding.
- Embeddings are generated via Ollama (`nomic-embed-text:v1.5`) and stored as float32 arrays; distance function is configured via `Cosmos:Vector:DistanceFunction`.

### 2.2 Reviews (Cosmos container: `reviews`)

- `id` (string): Internal GUID. Partition key.
- `gameId` (string): Steam `steam_appid` as string.
- `gameTitle` (string): Copied from game for convenience.
- `source` (string): Fixed `steam_review`.
- `lang` (string|null): Review language code from Steam; can be `null`.
- `fullText` (string): Full review text as returned by Steam (no BBCode expansion; see Steam quirks).
- `excerpt` (string): Truncated to 240 chars for preview.
- `helpfulVotes` (int): `votes_up`.
- `votesFunny` (int): `votes_funny`.
- `recommended` (bool): `voted_up`.
- `purchaseType` (string): one of `steam|gift|other` derived from `steam_purchase` and `received_for_free`.
- `steamReviewId` (string): `recommendationid`.
- `createdAt` (string, ISO 8601): From `timestamp_created` (Unix) converted to ISO-8601.
- `vector` (float32[d]): Embedding of `fullText`.

Ingestion rules:
- Language policy: `language=all` on the appreviews endpoint; we do NOT iterate per-language. Field `lang` is preserved when provided.
- Cursor-paginated up to `Seeding:MaxReviewsPerGame` (default 200). If zero reviews are returned and `Seeding:FailOnNoReviews=true`, ETL fails fast.
- Deterministic embedding fallback is disabled by default (`Embeddings:AllowDeterministicFallback=false`). When off, embedding failure is fatal.

### 2.3 Patch Notes (Cosmos container: `patchnotes`)

- `id` (string): Internal GUID. Partition key.
- `gameId` (string): Steam `steam_appid` as string.
- `gameTitle` (string): Copied from game for convenience.
- `title` (string): Patch note title.
- `publishedAt` (string, ISO 8601): Publication timestamp.
- `excerpt` (string): First 240 chars of cleaned content.
- `pvector` (float32[d]): Embedding of cleaned patch contents.

Notes:
- Source: `ISteamNews.GetNewsForApp` with `tags=patchnotes`. HTML/BBCode is stripped before embedding.
- Vector field path is configurable via `Cosmos:PatchVector:Path` (default `/pvector`).

## 3. Normalization Rules

- Dates: Unix seconds → ISO 8601 (`createdAt`). Store strings for interop; convert to `DateTimeOffset` in code.
- HTML: `detailed_description` is stripped before embedding; review `fullText` is preserved as-is (Steam BBCode may be present; downstream cleaning is acceptable for display but embeddings are robust to it).
- Languages: we request `language=all` to capture multilingual content. We do not downsample by language at ingestion.
- Purchase types: `steam_purchase=true` → `steam`; `received_for_free=true` → `gift`; else `other`.

## 4. Schema Evolution

- Version policy: Use additive changes by default. Breaking changes require a new field name or new container or a migration task.
- Configuration-driven paths: Vector field names are configurable (`Cosmos:Vector:Path`, `Cosmos:GamesVector:Path`). Avoid hardcoding in queries; use the helper for vector expressions.
- Backfill/Migration: For new fields, prefer lazy backfill (write-on-read or ETL re-run) over in-place mutation when feasible to control RU/s.
- API contracts: Expose core fields by default; add optional `fields` query param for heavy payloads (e.g., `fields=full` to include `fullText`).

## 5. Sampling and Validation Strategy

- Random sampling: Periodically select N appids uniformly at random across the catalog (or stratified by review count) and fetch pages until exhaustion cursors repeat, with `language=all`.
- Shape audit: Persist raw pages for sampled items to `AI-Agent-Workspace/Artifacts/steam-samples/{appid}/` to analyze field presence, language distribution, and Steam quirks.
- Metrics: Track ETL counters (apps processed, reviews ingested per app with language tags, patch notes ingested, embedding failures with stage tags). Emit via OpenTelemetry.
- Contracts: Maintain contract tests for API responses and smoke vector queries to catch accidental regressions.

## 6. Known Steam Quirks

- appreviews `language` supports `all` to return multilingual entries with a `language` field per review.
- Cursor pagination returns a `cursor` string; when it repeats, consider exhausted.
- Occasional empty `reviews` arrays with HTTP 200 should be treated as terminal for that app/page.
- Review text may include Steam BBCode; embeddings are tolerant. UI can apply presentation cleaning as needed.

---
Last updated: 2025-09-23