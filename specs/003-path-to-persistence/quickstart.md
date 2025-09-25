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

## Expected Layout
See `contracts/worker-cli.md` for exact file paths and manifest locations.

## Smoke Checks
- Verify manifests exist and list files with nonzero row counts
- Ensure review JSON includes full text and that per-app review count ≤ cap
- Confirm silver parquet files can be read by DuckDB
