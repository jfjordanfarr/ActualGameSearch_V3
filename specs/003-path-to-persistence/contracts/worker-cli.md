# Worker CLI Contract: 003 – Path to Persistence

This feature primarily adds a background worker, not new HTTP endpoints. The contract here defines CLI flags and outputs.

## CLI Invocation
- executable: src/ActualGameSearch.Worker
- verbs:
  - ingest: fetch catalog, store pages, news, and reviews respecting caps
    - bronze: random-sampled Bronze ingestion with flags below
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
  - for Bronze random run: default 50 (configurable)
- --mode=[full|popular|mid|longtail]
- --news-tags=<tag>|all (default: all; pass a specific tag like 'patchnotes' to filter)
- --news-count=10 (Bronze news items per app; default 10)
- --sample=250 (Bronze app sample size)
- --concurrency=4 (Bronze parallelism)
- --resume=run-YYYYMMDD-HHMMSS (resume an existing Bronze run; uses runstate at bronze/runstate/{runId}.json)
 - --reviews-language=all --reviews-type=all --reviews-purchase=steam --reviews-per-page=100
 - --silver-min-unique-words=20 (applied in normalization, not Bronze capture)
 - --silver-exclude-free-received=true
 - --gold-review-cap-per-app=200 (extend cap for promoted candidates via delta fetch)
 - --gold-embedding-metadata-weight=0.05 (blend weight for metadata vs review embeddings)

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
- Tiered capture: After derive step selects Gold candidates, perform targeted review deltas for those appids up to `--gold-review-cap-per-app` prior to computing embeddings/metrics.
- Workshop (optional Silver+): `IPublishedFileService/QueryFiles/v1` for UGC metrics.

## Sanitization & Metrics
- News contents sanitized from HTML/BBCode into `bodyClean` (Silver); raw preserved in Bronze.
- Derived metrics: patchNotesRatio, devResponseRate, avgDevResponseTimeHours, reviewUpdateVelocity; optional ugcMetrics.
