#!/usr/bin/env bash
set -euo pipefail

source /etc/massar/backup/files.env
export RESTIC_REPOSITORY AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY AWS_DEFAULT_REGION RESTIC_PASSWORD_FILE

exec 9>/srv/massar-shared/.restic-backup.lock
if ! flock --nonblock 9; then
  printf 'file backup prune skipped: another cluster node owns the backup lock\n'
  exit 0
fi

nice -n 15 ionice -c 3 \
  restic forget --host massar-cluster --keep-within 30d --prune
