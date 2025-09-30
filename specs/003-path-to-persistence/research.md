# Research: 003 – Path to Persistence

Date: 2025-09-25
Spec: /workspaces/ActualGameSearch_V3/specs/003-path-to-persistence/spec.md

## Decisions

1) Parquet Writer for .NET
- Decision: Use Parquet.NET for writing Silver/Gold datasets.
- Rationale: Pure C#, easy API, active maintenance, supports Snappy.
- Alternatives: ParquetSharp (C++ bindings) – higher perf but more complex; Apache Arrow via third-party – heavier dep.

2) Checkpointing Strategy
- Decision: File-based JSON checkpoints per run/partition (e.g., `checkpoints/{stage}/{runId}.json`).
- Rationale: Simple, visible, easy to diff; good for local-first.
- Alternatives: SQLite/LowDB – adds runtime deps; stateful queues – unnecessary.

3) Manifest Format
- Decision: JSON manifests per dataset partition capturing counts, byte sizes, checksums, source lineage (run ids, source paths).
- Rationale: Human-readable, easy to validate in tests.
- Alternatives: Hive-style tables – overkill now; full Iceberg – future path.

4) Backoff/Retry
- Decision: Exponential backoff with jitter. Prefer Polly if we add a dependency; otherwise implement a minimal backoff util.
- Rationale: Standard pattern; aligns with FR-020 caps.
- Alternatives: Fixed delay – poorer performance under contention.

5) Sampling Modes
- Decision: Support sampling via CLI flags: `--mode=popular|mid|longtail` with seed appid lists from small curated JSONs.
- Rationale: Enables quick smoke runs before full-scale.
- Alternatives: Random global sample – requires full catalog first.

6) Retention Defaults
- Decision: Expose retention config knobs but do not fix defaults yet; document sizing after first empirical runs.
- Rationale: Matches spec: defaults deferred to planning with cost/size analysis.
- Alternatives: Arbitrary defaults – risk churn.

7) Steam News & Patch Notes Detection
- Decision: Use ISteamNews/GetNewsForApp (pin version v2 or v0002) and prefer undocumented `tags=patchnotes` filter to isolate patch notes.
- Rationale: Patch notes ratio (patchnotes/total announcements) is a high-signal maintenance indicator; `tags` is discoverable via ISteamWebAPIUtil/GetSupportedAPIList.
- Alternatives: Heuristic title/content classification without tags – higher false positives; RSS parsing – divergent structure.

8) Review Corpus Access & Dynamics
- Decision: Use Storefront `https://store.steampowered.com/appreviews/{appid}?json=1` with cursor-based pagination (`cursor=*` then follow URL-encoded cursors) and `num_per_page=100`, `language=all`, `review_type=all`, `purchase_type=steam` for Bronze capture (subject to per-app cap).
- Rationale: Rich fields include `developer_response`, `timestamp_dev_responded`, `timestamp_created`, `timestamp_updated`, `comment_count`, and author playtime stats; supports derivation of responsiveness and sentiment-change metrics.
- Alternatives: Steam Web API review summary endpoints – insufficient granularity.

8.1) Multilingual Text Handling & Cleaning
- Decision: Apply multilingual-safe cleaning at Silver (preserve original at Bronze). Strip HTML/BBCode, links, newlines, excessive repetition, backslashes; avoid filters that drop non-Latin scripts.
- Rationale: SteamSeeker-2023 showed non-Latin content suffers from naïve alpha-only regex; preserving multi-language integrity is essential for worldwide coverage.

8.2) Review Filters (Silver normalization stage)
- Decision: Configurable filters replicated from SteamSeeker experience: unique_word_count ≥ 20; received_for_free == false. Naughty-word filtering deferred to analysis, not default pipeline.
- Rationale: Improves signal quality for embeddings and derived metrics while keeping Bronze evidence intact.

8.3) Time-Weighted Resonance & Geometric Means
- Decision: Compute time_weighted_resonance = resonance_score / log_base_365(max(days_since_review, 1)); compute geometric means (clip to ≥1) for word_count, unique_word_count, resonance_score, author.playtime_forever, author.num_games_owned, author.num_reviews.
- Rationale: Encodes recency and stabilizes heavy-tailed distributions.

8.4) Tiered Review Capture Policy
- Decision: Keep Bronze default cap at 10 reviews/app for breadth. When an app is promoted as a Gold candidate, run a targeted delta to extend its Bronze reviews up to a higher cap (e.g., 200) before embeddings and metric derivation.
- Rationale: Balances storage cost and fidelity while honoring “fetch once” via resumable deltas.

9) Workshop/UGC Signals (Optional, Silver+)
- Decision: For apps with Workshop, query IPublishedFileService/QueryFiles (cursor-based) to compute UGC velocity, maintenance rate, and unique author count.
- Rationale: Strong long-term health indicator; complements reviews/news signals.
- Alternatives: Ignore UGC – leaves information advantage on the table.

10) News Content Sanitization
- Decision: Sanitize `appnews.newsitems[].contents` (HTML + Steam BBCode) using a robust parser; `maxlength=4294967295` can sometimes reduce markup but is not sufficient. Persist `bodyRaw` in Bronze; create `bodyClean` in Silver.
- Rationale: Clean text improves semantic analysis; keeps raw for provenance.
- Alternatives: Store only raw – hurts later analysis; store only cleaned – loses provenance.

11) App Enumeration Source
- Decision: Use the official Steam app list endpoint for storewide catalog enumeration; persist as `bronze/steam-catalog/{date}/apps.json.gz` with a manifest entry.
- Rationale: Single authoritative list; enables random-without-replacement iteration and aligns with “ask Steam once.”
- Alternatives: Seed lists or previously seen appids only – incomplete; requires bootstrap.

12) Tiered Review Extension Default Stage
- Decision: Perform targeted review deltas at Silver by default to extend selected appids to a higher cap for analysis; Gold MAY also extend if needed.
- Rationale: Balances earlier analytical fidelity with storage discipline; Bronze remains breadth-first.
- Alternatives: Only extend at Gold – delays insight; extend at Bronze – increases storage prematurely.

13) News Taxonomy Discovery
- Decision: Start with a discovery census (tags/body features) and classify to a coarse taxonomy (patch/update vs marketing/other); iterate as distributions are observed.
- Rationale: Unknown tag/value space; avoid premature filtering at Bronze.
- Alternatives: Hardcode taxonomy now – risks drift and misclassification.

## Open Questions (tracked)
- Cloud path (Azure ADLS vs R2) remains a later decision; local-first suffices now.
- Review delta fetch practical cursor limits: verify on first prototype.
 - UGC metrics collection cadence: on-demand vs periodic (weekly) for cost control.

## References
- Constitution v1.2.0
- Prior Steam Seeker 2023 notes (AI-Agent-Workspace/Background/SteamSeeker-2023)
 - Gemini Deep Research Report 01: Steam API Analysis for Product Viability Signals
