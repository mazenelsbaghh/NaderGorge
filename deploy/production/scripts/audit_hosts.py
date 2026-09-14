#!/usr/bin/env python3
"""Read-only host preflight with normalized JSON output."""

from __future__ import annotations

import json
from typing import Any

from ssh_transport import SshTarget, StrictSshTransport


AUDIT_SCRIPT = r"""
set -eu
python3 - <<'PY'
import json, os, pathlib, shutil, socket, subprocess

def command(argv):
    try:
        return subprocess.check_output(argv, text=True, stderr=subprocess.DEVNULL).strip()
    except Exception:
        return ""

stat = os.statvfs("/")
payload = {
    "hostname": socket.gethostname(),
    "os": command(["bash", "-lc", ". /etc/os-release && printf '%s %s' \"$ID\" \"$VERSION_ID\""]),
    "kernel": command(["uname", "-r"]),
    "architecture": command(["uname", "-m"]),
    "cpuCount": os.cpu_count(),
    "memoryBytes": int(command(["awk", "/MemTotal/{print $2*1024}", "/proc/meminfo"]) or 0),
    "rootBytes": stat.f_blocks * stat.f_frsize,
    "rootFreeBytes": stat.f_bavail * stat.f_frsize,
    "rootFreeInodes": stat.f_favail,
    "docker": command(["docker", "--version"]) if shutil.which("docker") else "",
    "compose": command(["docker", "compose", "version"]) if shutil.which("docker") else "",
    "clock": command(["timedatectl", "show", "-p", "NTPSynchronized", "--value"]),
    "interfaces": command(["ip", "-brief", "address"]),
    "listeners": command(["ss", "-lntupH"]),
    "clusterMarker": pathlib.Path("/etc/massar/cluster-id").read_text().strip()
        if pathlib.Path("/etc/massar/cluster-id").is_file() else None,
}
print(json.dumps(payload))
PY
"""


def audit_node(transport: StrictSshTransport, target: SshTarget) -> dict[str, Any]:
    completed = transport.run(target, ["bash", "-lc", AUDIT_SCRIPT])
    payload = json.loads(completed.stdout)
    payload["nodeId"] = target.node_id
    return payload


def validate_clean_host(payload: dict[str, Any], expected_cluster: str = "massar-production") -> list[str]:
    findings: list[str] = []
    if payload.get("architecture") != "x86_64":
        findings.append("unsupported architecture")
    if (payload.get("cpuCount") or 0) < 4:
        findings.append("fewer than 4 CPUs")
    if (payload.get("memoryBytes") or 0) < 16 * 1024**3:
        findings.append("less than 16 GiB RAM")
    if (payload.get("rootFreeBytes") or 0) < 100 * 1024**3:
        findings.append("less than 100 GiB free disk")
    marker = payload.get("clusterMarker")
    if marker not in (None, expected_cluster):
        findings.append("host belongs to another cluster")
    return findings
