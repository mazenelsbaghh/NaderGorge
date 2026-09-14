#!/usr/bin/env python3
"""Evaluate release-bound production evidence and emit a signed GO/NO-GO decision."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import hmac
import json
import os
import re
import sys
from pathlib import Path

from acceptance_schema import EVIDENCE_NAMES, SchemaError, validate_evidence


RELEASE_ID = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$")
REQUIRED = EVIDENCE_NAMES
PRE_DNS_CHAOS_SCENARIOS = {
    "ingress", "app", "postgres", "redis", "files", "worker",
}
FRESHNESS_SECONDS = {
    "cluster-health.json": 300,
    "database-restore.json": 31 * 24 * 60 * 60,
    "file-restore.json": 31 * 24 * 60 * 60,
    "load.json": 24 * 60 * 60,
    "chaos.json": 24 * 60 * 60,
    "security.json": 24 * 60 * 60,
    "automated-tests.json": 24 * 60 * 60,
    "manual-qa.json": 24 * 60 * 60,
}


def parse_utc(value: object) -> dt.datetime | None:
    if not isinstance(value, str):
        return None
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    return parsed.astimezone(dt.timezone.utc)


def number(value: object) -> float | None:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return None
    result = float(value)
    return result if result == result and abs(result) != float("inf") else None


def validate_contract(
    name: str,
    value: dict,
    release_id: str,
    now: dt.datetime,
) -> list[str]:
    reasons: list[str] = []
    if value.get("schemaVersion") != 1:
        reasons.append(f"invalid-contract:{name}:schemaVersion")
    if value.get("status") not in {"success", "failed"}:
        reasons.append(f"invalid-contract:{name}:status")
    if name != "release.json" and value.get("releaseId") != release_id:
        reasons.append(f"release-mismatch:{name}")

    timestamp_field = "createdAt" if name == "release.json" else "capturedAt"
    observed_at = parse_utc(value.get(timestamp_field))
    if observed_at is None or observed_at > now + dt.timedelta(minutes=5):
        reasons.append(f"invalid-contract:{name}:{timestamp_field}")
    elif (
        name in FRESHNESS_SECONDS
        and (now - observed_at).total_seconds() > FRESHNESS_SECONDS[name]
    ):
        reasons.append(f"stale:{name}")

    if name in {"security.json", "manual-qa.json"} and value.get("scope") != "pre-dns":
        reasons.append(f"invalid-contract:{name}:scope")
    if name == "automated-tests.json" and value.get("criticalFindingCount") != 0:
        reasons.append("automated-test-findings")
    return reasons


def read_evidence(
    evidence_root: Path,
) -> tuple[dict[str, dict], list[str], dict[str, str]]:
    reasons: list[str] = []
    evidence: dict[str, dict] = {}
    digests: dict[str, str] = {}
    for name in REQUIRED:
        path = evidence_root / name
        if not path.is_file():
            reasons.append(f"missing:{name}")
            continue
        if path.is_symlink():
            reasons.append(f"invalid:{name}")
            continue
        try:
            raw = path.read_bytes()
            value = json.loads(raw)
            if not isinstance(value, dict):
                raise ValueError(f"{name} must contain a JSON object")
            validate_evidence(name, value)
            evidence[name] = value
            digests[name] = hashlib.sha256(raw).hexdigest()
        except (
            OSError,
            UnicodeDecodeError,
            ValueError,
            SchemaError,
            json.JSONDecodeError,
        ):
            reasons.append(f"invalid:{name}")
    return evidence, reasons, digests


def evaluate_values(
    evidence: dict[str, dict],
    reasons: list[str],
    now: dt.datetime,
) -> list[str]:
    for name, value in evidence.items():
        if value.get("status") != "success":
            reasons.append(f"failed:{name}")
    release = evidence.get("release.json", {})
    release_id = release.get("releaseId")
    if not isinstance(release_id, str) or not RELEASE_ID.fullmatch(release_id):
        reasons.append("invalid-contract:release.json:releaseId")
        release_id = ""
    for name, value in evidence.items():
        reasons.extend(validate_contract(name, value, release_id, now))
    if release and (release.get("digestParity") is not True or release.get("nodeCount") != 3):
        reasons.append("release-digest-parity")
    health = evidence.get("cluster-health.json", {})
    if health and (
        health.get("healthyNodeCount") != 3
        or health.get("postgresWriterCount") != 1
        or health.get("redisMasterCount") != 1
        or health.get("glusterSplitBrainCount") != 0
    ):
        reasons.append("cluster-quorum-or-role")
    db_backup = evidence.get("database-backup.json", {})
    if db_backup:
        observed_at = parse_utc(db_backup.get("capturedAt"))
        reported_age = number(db_backup.get("walArchiveAgeSeconds"))
        if (
            reported_age is None
            or reported_age < 0
            or observed_at is None
            or reported_age + max(0, (now - observed_at).total_seconds()) > 300
        ):
            reasons.append("database-wal-older-than-five-minutes")
    file_backup = evidence.get("file-backup.json", {})
    if file_backup:
        observed_at = parse_utc(file_backup.get("capturedAt"))
        reported_age = number(file_backup.get("snapshotAgeSeconds"))
        if (
            reported_age is None
            or reported_age < 0
            or observed_at is None
            or reported_age + max(0, (now - observed_at).total_seconds()) > 3600
        ):
            reasons.append("file-snapshot-older-than-one-hour")
    for name in ("database-restore.json", "file-restore.json"):
        value = evidence.get(name, {})
        if value and (value.get("isolated") is not True or value.get("checksumVerified") is not True):
            reasons.append(f"unverified:{name}")
    load_evidence = evidence.get("load.json", {})
    load_duration = number(load_evidence.get("durationSeconds"))
    load_multiplier = number(load_evidence.get("baselineMultiplier"))
    load_error_rate = number(load_evidence.get("errorRate"))
    load_p95 = number(load_evidence.get("p95Milliseconds"))
    load_p99 = number(load_evidence.get("p99Milliseconds"))
    load_dropped_rate = number(load_evidence.get("droppedIterationRate"))
    if load_evidence and (
        load_duration is None
        or load_duration < 1800
        or load_multiplier is None
        or load_multiplier < 2
        or load_error_rate is None
        or load_error_rate >= 0.01
        or load_p95 is None
        or load_p95 >= 1000
        or load_p99 is None
        or load_p99 >= 2000
        or load_dropped_rate is None
        or load_dropped_rate >= 0.005
        or load_evidence.get("healthyNodeCount") != 3
    ):
        reasons.append("load-gate")
    security = evidence.get("security.json", {})
    if security and (
        security.get("directOriginDenied") is not True
        or security.get("internalPortsClosed") is not True
        or security.get("wrongHostDenied") is not True
    ):
        reasons.append("security-exposure-gate")
    chaos = evidence.get("chaos.json", {})
    if chaos:
        passed = chaos.get("passedScenarios")
        if (
            not isinstance(passed, list)
            or any(not isinstance(value, str) for value in passed)
            or set(passed) != PRE_DNS_CHAOS_SCENARIOS
        ):
            reasons.append("chaos-coverage")
    return sorted(set(reasons))


def evaluate(evidence_root: Path, now: dt.datetime | None = None) -> list[str]:
    evidence, reasons, _ = read_evidence(evidence_root)
    return evaluate_values(
        evidence,
        reasons,
        now or dt.datetime.now(dt.timezone.utc),
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence-root", required=True, type=Path)
    parser.add_argument("--signing-key-file", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not args.signing_key_file.is_file() or args.signing_key_file.stat().st_mode & 0o077:
        print("acceptance blocked: signing key must exist with mode 0600", file=sys.stderr)
        return 6
    key = args.signing_key_file.read_bytes().strip()
    if len(key) < 32:
        print("acceptance blocked: signing key is too short", file=sys.stderr)
        return 6
    evidence, read_reasons, evidence_digests = read_evidence(args.evidence_root)
    reasons = evaluate_values(
        evidence,
        read_reasons,
        dt.datetime.now(dt.timezone.utc),
    )
    payload = {
        "schemaVersion": 1,
        "decision": "GO" if not reasons else "NO-GO",
        "evaluatedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "reasons": reasons,
        "evidenceDigests": evidence_digests,
    }
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()
    payload["signature"] = hmac.new(key, canonical, hashlib.sha256).hexdigest()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    os.chmod(temporary, 0o640)
    os.replace(temporary, args.output)
    print(json.dumps({"decision": payload["decision"], "reasonCount": len(reasons)}))
    return 0 if not reasons else 6


if __name__ == "__main__":
    raise SystemExit(main())
