# Quickstart: 003 – Path to Persistence

This guide explains how to run the ingestion/refinement pipeline locally after implementation.

## Prerequisites
- .NET 8 SDK installed in dev container (already present)
- No external services required to produce local files

## Steps
1) Build the solution
2) Run a tiny ingest sample (popular seed) with concurrency cap 4
3) Inspect bronze outputs and manifest
4) Run refine to produce silver parquet
5) Run derive to compute candidates

### Non-trivial Bronze sample (random)
- Run a larger Bronze ingestion (collect all news now; filter later in Silver):
	- worker ingest bronze --sample=250 --reviews-cap-per-app=50 --news-tags=all --news-count=10 --concurrency=4
- After completion, consider mirroring the lake to R2 (dry-run first):
	- EPHEMERAL=1 DRY_RUN=1 ./AI-Agent-Workspace/Scripts/backup_rclone.example.sh

## Expected Layout
See `contracts/worker-cli.md` for exact file paths and manifest locations.

## Smoke Checks
- Verify manifests exist and list files with nonzero row counts
- Ensure review JSON includes full text and that per-app review count ≤ cap
- Confirm silver parquet files can be read by DuckDB
