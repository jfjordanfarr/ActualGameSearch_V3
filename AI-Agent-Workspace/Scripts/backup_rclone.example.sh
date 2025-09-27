#!/usr/bin/env bash
set -euo pipefail

# backup_rclone.example.sh — Mirror the local data lake to Cloudflare R2 (or any S3-compatible remote)
#
# Prereqs:
#   - rclone installed and in PATH
#   - An rclone remote configured for R2 (e.g., name: r2) pointing at your account endpoint
#   - R2 bucket created (e.g., ags-datalake)
#
# Usage examples:
#   # Using a named rclone remote
#   DRY_RUN=1 ./backup_rclone.example.sh r2 ags-datalake
#   ./backup_rclone.example.sh r2 ags-datalake
#   ./backup_rclone.example.sh r2 ags-datalake --include "/bronze/**" --exclude "/gold/**"
#
#   # Using ephemeral (no-config) R2 remote built from env vars (ideal with Codespaces secrets)
#   # Required env vars: R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET
#   EPHEMERAL=1 DRY_RUN=1 ./backup_rclone.example.sh
#
# Notes:
#   - This script is conservative by default. Set DRY_RUN=1 to preview changes.
#   - It syncs the local lake root to the remote bucket. Use filters to narrow scope.

REMOTE_NAME="${1:-}"
BUCKET_NAME="${2:-}"
if [[ -n "${REMOTE_NAME}" && -n "${BUCKET_NAME}" ]]; then
  shift 2 || true
fi

if ! command -v rclone >/dev/null 2>&1; then
  echo "ERROR: rclone not found in PATH. Install rclone first: https://rclone.org/install/" >&2
  exit 1
fi

EPHEMERAL_MODE="${EPHEMERAL:-0}"
if [[ -z "${REMOTE_NAME}" || -z "${BUCKET_NAME}" ]]; then
  if [[ "${EPHEMERAL_MODE}" != "1" ]]; then
    echo "Usage: $0 <remote_name> <bucket_name> [rclone-filters...]" >&2
    echo "Or, set EPHEMERAL=1 and provide R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET via env vars." >&2
    exit 2
  fi
fi

# Workspace-relative lake root
LAKE_ROOT="AI-Agent-Workspace/Artifacts/DataLake"
if [[ ! -d "${LAKE_ROOT}" ]]; then
  echo "ERROR: Data lake root not found at ${LAKE_ROOT}. Run an ingest or create the directories first." >&2
  exit 3
fi

if [[ "${EPHEMERAL_MODE}" == "1" ]]; then
  : "${R2_ACCOUNT_ID:?R2_ACCOUNT_ID not set}"
  : "${R2_ACCESS_KEY_ID:?R2_ACCESS_KEY_ID not set}"
  : "${R2_SECRET_ACCESS_KEY:?R2_SECRET_ACCESS_KEY not set}"
  R2_BUCKET_PATH="${R2_BUCKET:-${BUCKET_NAME:-}}"
  if [[ -z "${R2_BUCKET_PATH}" ]]; then
    echo "ERROR: R2_BUCKET not set and no bucket_name arg provided." >&2
    exit 4
  fi
  # Build an ephemeral s3 backend remote without writing a config file.
  # NOTE: Do NOT echo the full string to avoid leaking secrets.
  # rclone expects endpoint without scheme for some providers; it will add https itself.
  # Use env_auth=true so credentials come from environment (not CLI), avoiding any chance of logging secrets.
  DEST=":s3,provider=Cloudflare,env_auth=true,endpoint=${R2_ACCOUNT_ID}.r2.cloudflarestorage.com:${R2_BUCKET_PATH}"
else
  DEST="${REMOTE_NAME}:${BUCKET_NAME}"
fi

# Respect DRY_RUN env toggle
RCLONE_FLAGS=("--progress" "--fast-list" "--checksum")
if [[ "${DRY_RUN:-0}" == "1" ]]; then
  RCLONE_FLAGS+=("--dry-run")
fi

# Pass-through extra include/exclude filters, etc.
EXTRA_FILTERS=("$@")

# Show size summary before sync (safe)
echo "Local lake size summary:" && du -sh "${LAKE_ROOT}"/* || true

# Perform sync without echoing secrets. We intentionally avoid 'set -x'.
if [[ "${EPHEMERAL_MODE}" == "1" ]]; then
  AWS_ACCESS_KEY_ID="${R2_ACCESS_KEY_ID}" \
  AWS_SECRET_ACCESS_KEY="${R2_SECRET_ACCESS_KEY}" \
  rclone sync "${LAKE_ROOT}" "${DEST}" "${RCLONE_FLAGS[@]}" "${EXTRA_FILTERS[@]}"
else
  rclone sync "${LAKE_ROOT}" "${DEST}" "${RCLONE_FLAGS[@]}" "${EXTRA_FILTERS[@]}"
fi

echo "Done. To restore a subtree for testing, you can run:"
if [[ "${EPHEMERAL_MODE}" == "1" ]]; then
  SAFE_REMOTE="cf-r2://${R2_ACCOUNT_ID}/${R2_BUCKET_PATH}"
else
  SAFE_REMOTE="${REMOTE_NAME}:${BUCKET_NAME}"
fi
echo "  rclone copy ${SAFE_REMOTE}/exports ${LAKE_ROOT}/exports.restore --progress"
