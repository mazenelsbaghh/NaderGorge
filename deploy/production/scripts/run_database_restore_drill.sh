#!/usr/bin/env bash
set -euo pipefail

# The timer is enabled on all nodes for availability, but exactly one Patroni
# member is primary. Only that member creates the probe and performs the
# isolated PITR drill.
if ! curl --fail --silent http://127.0.0.1:8008/primary >/dev/null; then
  printf 'database restore drill skipped: this node is not the Patroni primary\n'
  exit 0
fi

/usr/local/sbin/massar-prepare-pitr-probe
exec /usr/local/lib/massar/restore_database_sample.sh
