#!/usr/bin/env python3
"""Idempotent host foundation bootstrap orchestration."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from ssh_transport import SshTarget, StrictSshTransport


CLUSTER_ID = "massar-production"


FOUNDATION_SCRIPT = r"""
set -euo pipefail
if test -f /etc/massar/cluster-id && test "$(cat /etc/massar/cluster-id)" != "massar-production"; then
  echo "host belongs to another cluster" >&2
  exit 5
fi
sudo /usr/bin/install -d -m 0750 -o root -g massar /etc/massar /etc/massar/secrets
sudo /usr/bin/install -d -m 0755 /opt/massar/releases /opt/massar/current /var/lib/massar/evidence
printf '%s\n' massar-production | sudo /usr/bin/tee /etc/massar/cluster-id >/dev/null
sudo /usr/bin/chmod 0644 /etc/massar/cluster-id
sudo /usr/bin/systemctl enable --now chrony
sudo /usr/bin/timedatectl set-timezone Africa/Cairo
"""


def bootstrap_foundation(transport: StrictSshTransport, target: SshTarget, *, dry_run: bool) -> str:
    if dry_run:
        return FOUNDATION_SCRIPT
    return transport.run(target, ["bash", "-lc", FOUNDATION_SCRIPT], timeout_seconds=180).stdout
