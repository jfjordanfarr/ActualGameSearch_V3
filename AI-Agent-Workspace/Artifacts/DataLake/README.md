# Data Lake Layout (Local)

This directory is the local root for the ingestion pipeline (003 – Path to Persistence).

Structure:
- bronze/
  - reviews/{yyyy}/{MM}/{dd}/{runId}/appid={id}/page={n}.json.gz
  - store/{yyyy}/{MM}/{dd}/{runId}/appid={id}.json.gz
  - news/{yyyy}/{MM}/{dd}/{runId}/appid={id}/page={n}.json.gz
  - catalog/{yyyy}/{MM}/{dd}/{runId}/catalog.json.gz
  - manifests/{runId}.manifest.json
- silver/
  - games/partition={yyyyMMdd}/{file}.parquet
  - manifests/{runId}.manifest.json
- gold/
  - candidates/partition={yyyyMMdd}/{file}.parquet
  - manifests/{runId}.manifest.json

Notes:
- Run IDs are opaque, unique identifiers for a pipeline execution (e.g., run-20250314-abc123).
- Bronze preserves raw, provenance-rich payloads (JSON.gz). Silver and Gold emit normalized Parquet.
- Large files here are ignored by Git via the parent .gitignore.
