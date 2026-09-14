#!/usr/bin/env bash
set -euo pipefail

source /etc/massar/backup/files.env
export RESTIC_REPOSITORY AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY AWS_DEFAULT_REGION RESTIC_PASSWORD_FILE

if ! restic cat config >/dev/null 2>&1; then
  restic init
fi

install -d -m 0750 -o root -g massar /var/lib/massar/evidence/backup
if [[ ! -f /srv/massar-shared/.backup-restore-sentinel ]]; then
  nonce="$(date -u '+%Y%m%dT%H%M%SZ')-$RANDOM"
  printf '%s\n' "$nonce" > /srv/massar-shared/.backup-restore-sentinel
  sha256sum /srv/massar-shared/.backup-restore-sentinel \
    > /srv/massar-shared/.backup-restore-sentinel.sha256
fi

restic cat config >/dev/null
