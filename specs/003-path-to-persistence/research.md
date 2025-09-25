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

## Open Questions (tracked)
- Cloud path (Azure ADLS vs R2) remains a later decision; local-first suffices now.
- Review delta fetch practical cursor limits: verify on first prototype.

## References
- Constitution v1.2.0
- Prior Steam Seeker 2023 notes (AI-Agent-Workspace/Background/SteamSeeker-2023)
