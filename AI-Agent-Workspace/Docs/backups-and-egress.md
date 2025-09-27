# Backups, Egress, and Data Ownership

This project treats the local filesystem data lake (`AI-Agent-Workspace/Artifacts/DataLake/`) as the canonical store. Cloud databases and indices (e.g., Cosmos, vector stores) are derivatives and can be reconstructed from the filesystem.

## Goals
- Avoid vendor lock-in: use portable formats (JSON.gz, Parquet Snappy) and a neutral layout.
- Keep exit paths cheap: use standard tools (rclone/aws cli) and S3-compatible targets.
- Support offline archiving: `export pack` and `import unpack` create portable tar.zst bundles per run/partition with manifests and checksums.

## Primary Target: Cloudflare R2 (S3-Compatible)
- Why R2 first: zero egress fees to the public Internet, S3-compatible API, durable and cost-effective storage, and tight DNS/Pages integration under the same Cloudflare account as actualgamesearch.com.
- Typical flow here: keep Bronze/Silver (and a copy of Gold) in R2 as the canonical remote lake; use Azure Cosmos as the serving layer for Gold (derived candidates + embeddings). If Cosmos needs to be rebuilt, read from R2 and rehydrate.

Other targets (optional secondaries):
- Backblaze B2
- AWS S3
- Self-hosted MinIO

## Suggested Tooling
- rclone (preferred for cross-provider sync)
- aws-cli (optional)

### rclone remote for Cloudflare R2
1) Create an R2 bucket, e.g., `ags-datalake`.
2) Create an API token with appropriate permissions (Object Read/Write, List, Create) scoped to the bucket.
3) Configure rclone:

```
rclone config
# new remote → name: r2
# storage: s3
# provider: Cloudflare
# env_auth: false
# access_key_id: <R2 access key>
# secret_access_key: <R2 secret>
# endpoint: https://<accountid>.r2.cloudflarestorage.com
# region: auto (or leave blank)
```

Once configured:
- Sync up: `rclone sync --progress ./AI-Agent-Workspace/Artifacts/DataLake r2:ags-datalake`
- Dry-run: `rclone sync --dry-run ./AI-Agent-Workspace/Artifacts/DataLake r2:ags-datalake`

## Example Patterns (Conceptual)
- Dry-run to estimate transferred objects/bytes:
  - `rclone sync --dry-run --progress ./AI-Agent-Workspace/Artifacts/DataLake r2:ags-datalake`
- Size accounting (local):
  - `du -sh ./AI-Agent-Workspace/Artifacts/DataLake/*`
- Pull-back (test restore):
  - `rclone copy r2:ags-datalake/exports ./AI-Agent-Workspace/Artifacts/DataLake/exports.restore`

## Secrets and CI-friendly usage

Avoid putting secrets in files. Prefer environment variables managed by Codespaces or GitHub Environments:

- GitHub Codespaces Secrets (recommended for local dev):
  - Add the following Codespaces secrets in the repo/org settings:
    - R2_ACCOUNT_ID
    - R2_ACCESS_KEY_ID
    - R2_SECRET_ACCESS_KEY
    - R2_BUCKET (for example: actualgamesearch-datalake)
  - These become env vars inside the Codespace automatically, and are not written to disk or the repo.

- Ephemeral rclone remote via env vars (no rclone config file):
  - Use the provided script with EPHEMERAL=1 to build an in-memory S3 backend for R2:
    - `EPHEMERAL=1 DRY_RUN=1 ./AI-Agent-Workspace/Scripts/backup_rclone.example.sh`
  - Required env vars: R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, and R2_BUCKET.
  - This avoids storing secrets in rclone.conf and keeps logs clean.

- GitHub Actions (later):
  - Store the same variables as Actions secrets and inject them into a build job if you want scheduled mirrors.

Security notes:
- Do not echo secrets; avoid set -x when commands include credentials.
- Use bucket-scoped credentials with least privilege (Object Read/Write/List) rather than account-wide keys.

## Notes
- Cloud cost posture:
  - R2 egress: $0 to the public Internet (you still pay request ops and storage); ideal for multi-cloud pipelines.
  - Azure ingress (Blob/Cosmos): free. You pay Cosmos RUs to write/import Gold documents. This makes “R2-as-lake, Azure-as-serving” cost-efficient and portable.
- Do not store large production datasets in Git or Git LFS. Curated small samples are acceptable for demos only.
- Backups should include the `manifests/` and `exports/` subtrees to enable partial/point-in-time restoration.
- Consider encryption-at-rest on the remote (provider or rclone-side) if needed.
