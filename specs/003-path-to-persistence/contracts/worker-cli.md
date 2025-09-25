# Worker CLI Contract: 003 – Path to Persistence

This feature primarily adds a background worker, not new HTTP endpoints. The contract here defines CLI flags and outputs.

## CLI Invocation
- executable: src/ActualGameSearch.Worker
- verbs:
  - ingest: fetch catalog, store pages, news, and reviews respecting caps
  - refine: transform Bronze → Silver
  - derive: compute candidate set (Gold)

## Common Flags
- --data-root=AI-Agent-Workspace/Artifacts/DataLake
- --concurrency=4 (max)
- --retry-backoff=exponential-jitter
- --resume (default true)
- --recrawl-store=7d
- --recrawl-news=7d
- --reviews-cap-per-app=10
- --mode=[full|popular|mid|longtail]
 - --news-tags=patchnotes|all (default: all; when 'patchnotes', also compute PatchNotesRatio)
 - --reviews-language=all --reviews-type=all --reviews-purchase=steam --reviews-per-page=100

## Outputs
- Exit code 0 on success; nonzero on failure.
- Writes manifests under each dataset/partition.
- Writes run.json summary with counts, durations, errors.

## File Layout Contract
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

## External Endpoints & Behaviors
- News: `ISteamNews/GetNewsForApp/v2` with optional `tags=patchnotes`; consider pinning version in requests.
- Reviews: `https://store.steampowered.com/appreviews/{appid}?json=1` with cursor pagination. Start with `cursor=*` and follow URL-encoded cursors until completion. Respect per-app review cap at Bronze.
- Workshop (optional Silver+): `IPublishedFileService/QueryFiles/v1` for UGC metrics.

## Sanitization & Metrics
- News contents sanitized from HTML/BBCode into `bodyClean` (Silver); raw preserved in Bronze.
- Derived metrics: patchNotesRatio, devResponseRate, avgDevResponseTimeHours, reviewUpdateVelocity; optional ugcMetrics.
