#!/usr/bin/env python3
"""Age-bounded Docker image cleanup; application files and volumes are never removed."""
from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import fcntl
import json
import os
import re
import ssl
import stat
import subprocess
import sys
import urllib.request
from pathlib import Path

RELEASE = re.compile(r"(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
COMPONENTS = ("backend", "frontend", "worker", "migrator")
RELEASES = Path("/opt/massar/releases")
CURRENT = Path("/opt/massar/current")
BUILDS = Path("/var/lib/massar/builds")
SHARED = Path("/srv/massar-shared")
RESTORES = Path("/var/lib/massar/evidence/restore")
DOCKER = "/usr/bin/docker"


class CleanupError(RuntimeError):
    pass


class CleanupBusy(RuntimeError):
    pass


def command(arguments: list[str], environment: dict[str, str] | None = None) -> str:
    completed = subprocess.run(arguments, text=True, capture_output=True, timeout=120,
                               env={**os.environ, "LC_ALL": "C", **(environment or {})})
    if completed.returncode:
        raise CleanupError(f"Command failed: {Path(arguments[0]).name}")
    return completed.stdout.strip()


def read_json(path: Path) -> dict:
    if path.is_symlink() or not path.is_file():
        raise CleanupError(f"Missing or unsafe evidence: {path.name}")
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW), "r") as evidence_file:
        metadata = os.fstat(evidence_file.fileno())
        if metadata.st_uid != os.geteuid() or metadata.st_mode & 0o022:
            raise CleanupError(f"Untrusted evidence permissions: {path.name}")
        return json.load(evidence_file)


def fetch_json(url: str, context: ssl.SSLContext | None = None, host: str | None = None) -> dict:
    request = urllib.request.Request(url, headers={"Host": host} if host else {})
    with urllib.request.urlopen(request, context=context, timeout=10) as response:
        return json.loads(response.read(1024 * 1024))


def fresh_backup(path: Path, maximum_hours: int) -> None:
    evidence = read_json(path)
    age = dt.datetime.now().timestamp() - path.stat().st_mtime
    if evidence.get("status") != "success" or not 0 <= age < maximum_hours * 3600:
        raise CleanupError(f"Backup/restore evidence is stale or unsuccessful: {path.name}")


def database_backup_check() -> None:
    catalog = json.loads(command(["runuser", "-u", "postgres", "--", "pgbackrest",
                                  "--stanza=massar", "--repo=1", "--output=json", "info"]))
    stanzas = [stanza for stanza in catalog if stanza["name"] == "massar"]
    if len(stanzas) != 1:
        raise CleanupError("Database backup stanza is missing or ambiguous")
    stanza = stanzas[0]
    completed = [backup["timestamp"]["stop"] for backup in stanza.get("backup", [])
                 if backup.get("timestamp", {}).get("stop")]
    if stanza["status"]["code"] != 0 or not completed or not (
        0 <= dt.datetime.now().timestamp() - max(completed) < 30 * 3600
    ):
        raise CleanupError("Database backup repository is stale or unhealthy")


def restore_check() -> None:
    for name in ("massar-db-restore-scheduled.service", "massar-files-restore-test.service"):
        failed = subprocess.run(["systemctl", "is-failed", "--quiet", name], timeout=10)
        if failed.returncode == 0:
            raise CleanupError("An isolated restore test failed")
    # Restore evidence is producer-local: the primary owns DB drills and one
    # elected node owns file drills. Other nodes legitimately retain older copies.
    for name in ("database-latest.json", "files-latest.json"):
        path = RESTORES / name
        if path.exists() and read_json(path).get("status") != "success":
            raise CleanupError("An isolated restore has unsuccessful evidence")
    marker = SHARED / ".cluster-health/files-restore-test-last"
    metadata = marker.lstat()
    if not stat.S_ISREG(metadata.st_mode) or metadata.st_uid != os.geteuid() or metadata.st_mode & 0o022:
        raise CleanupError("Cluster file restore success marker is missing")
    age = dt.datetime.now().timestamp() - metadata.st_mtime
    if not 0 <= age < 35 * 86400:
        raise CleanupError("Cluster file restore success marker is stale")


def quorum_checks(configuration: dict) -> None:
    context = ssl.create_default_context(cafile="/etc/massar/pki/etcd/ca.crt")
    for address in configuration["overlayAddresses"]:
        if str(fetch_json(f"https://{address}:2379/health", context).get("health")).lower() != "true":
            raise CleanupError("etcd member is unhealthy")
    members = fetch_json("http://127.0.0.1:8008/cluster")["members"]
    leaders = [member for member in members if member["role"] in ("leader", "primary")]
    if len(members) != 3 or len(leaders) != 1 or any(
        member["state"] not in ("running", "streaming") for member in members
    ):
        raise CleanupError("PostgreSQL topology is unhealthy")
    redis_quorum(configuration["overlayAddresses"])


def redis_quorum(addresses: list[str]) -> None:
    environment = {"REDISCLI_AUTH": Path("/etc/massar/secrets/redis-password").read_text().strip()}
    observed_masters = []
    actual_masters = []
    for address in addresses:
        base = ["redis-cli", "--no-auth-warning", "--raw", "-h", address]
        quorum = command([*base, "-p", "26379", "SENTINEL", "ckquorum", "massar-redis"], environment)
        if not quorum.startswith("OK"):
            raise CleanupError("Redis Sentinel quorum is unavailable")
        observed_masters.append(command([*base, "-p", "26379", "SENTINEL",
                                        "get-master-addr-by-name", "massar-redis"], environment))
        replication = command([*base, "-p", "6379", "INFO", "replication"], environment)
        if "role:master" in replication.splitlines():
            actual_masters.append(address)
    if len(set(observed_masters)) != 1 or len(actual_masters) != 1 or (
        observed_masters[0].splitlines() != [actual_masters[0], "6379"]
    ):
        raise CleanupError("Redis master identity is inconsistent")


def preflight(configuration: dict) -> None:
    if Path("/etc/massar/cluster-id").read_text().strip() != "massar-production":
        raise CleanupError("Unexpected cluster")
    if Path("/etc/massar/node-id").read_text().strip() != configuration["nodeId"]:
        raise CleanupError("Unexpected node")
    if command(["findmnt", "-n", "-o", "FSTYPE", "--target", str(SHARED)]) != "fuse.glusterfs":
        raise CleanupError("Shared cluster lock requires the Gluster mount")
    command(["systemctl", "is-active", "--quiet", "chrony", "docker", "etcd", "patroni",
             "haproxy", "redis-server", "redis-sentinel", "glusterd"])
    quorum_checks(configuration)
    heal = command(["gluster", "volume", "heal", "massar-shared", "info", "summary"])
    counts = re.findall(r"Number of entries:\s*(\d+)", heal)
    if len(counts) != 3 or any(count != "0" for count in counts):
        raise CleanupError("Gluster healing has not completed")
    database_backup_check()
    fresh_backup(SHARED / ".cluster-health/file-backup-latest.json", 2)
    restore_check()
    command(["curl", "--fail", "--silent", "--max-time", "10", "-H", "Host: api.massar-academy.net",
             "http://127.0.0.1:8088/api/health/ready"])
    ready = fetch_json("http://127.0.0.1:8088/__node_ready", host="massar-academy.net")
    if ready.get("releaseId") != CURRENT.resolve(strict=True).name:
        raise CleanupError("Application readiness release identity differs")


@contextlib.contextmanager
def exclusive_lock(path: Path):
    descriptor = os.open(path, os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    try:
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_uid != os.geteuid() or metadata.st_nlink != 1:
            raise CleanupError("Unsafe cleanup lock")
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise CleanupBusy("Another maintenance operation holds the lock") from error
        yield
    finally:
        os.close(descriptor)


def retained_releases() -> tuple[str, set[str], set[str]]:
    current_root = CURRENT.resolve(strict=True)
    if not CURRENT.is_symlink() or current_root.parent != RELEASES:
        raise CleanupError("Current release pointer is unsafe")
    roots = [path for path in RELEASES.iterdir() if RELEASE.fullmatch(path.name)]
    if len(roots) < 2 or current_root not in roots:
        raise CleanupError("Current and rollback release roots must both be retained")
    image_ids: set[str] = set()
    for root in roots:
        if root.is_symlink() or not root.is_dir():
            raise CleanupError("Unsafe retained release root")
        manifest = read_json(root / "manifest.json")
        if manifest.get("releaseId") != root.name or manifest.get("status") != "success":
            raise CleanupError("Retained release manifest identity is invalid")
        images = manifest["images"]
        if set(images) != set(COMPONENTS) or not all(DIGEST.fullmatch(image) for image in images.values()):
            raise CleanupError("Retained release image identities are invalid")
        image_ids.update(images.values())
    return current_root.name, {root.name for root in roots}, image_ids


def refuse_active_changes() -> None:
    if list(BUILDS.glob("*/.remote-builder.lock")) or list(
        Path("/var/lib/massar/deploy-recovery").glob("*.json")
    ):
        raise CleanupBusy("A build or deployment needs exclusive access")


def image_rows() -> list[dict]:
    listing = command([DOCKER, "image", "ls", "--digests", "--no-trunc", "--format", "{{json .}}"])
    return [json.loads(line) for line in listing.splitlines()]


def used_image_ids() -> set[str]:
    containers = command([DOCKER, "ps", "-aq"]).split()
    if not containers:
        raise CleanupError("No containers found; refusing unattended cleanup")
    return set(command([DOCKER, "inspect", "--format", "{{.Image}}", *containers]).split())


def image_reference(image: dict) -> str:
    if image["Tag"] != "<none>":
        return image["Repository"] + ":" + image["Tag"]
    return image["Repository"] + "@" + image["Digest"]


def eligible_image(image: dict, repositories: set[str], cutoff: dt.datetime) -> bool:
    if image["Repository"] not in repositories or int(image["Containers"]) != 0:
        return False
    if not RELEASE.fullmatch(image["Tag"]) and not (
        image["Tag"] == "<none>" and DIGEST.fullmatch(image["Digest"])
    ):
        return False
    created = dt.datetime.strptime(" ".join(image["CreatedAt"].split()[:3]), "%Y-%m-%d %H:%M:%S %z")
    return created < cutoff


def cleanup_plan(images: list[dict], retention: tuple, configuration: dict, used_ids: set[str]) -> dict:
    current, release_ids, protected_ids = retention
    pending = {path.name for path in BUILDS.glob("*") if RELEASE.fullmatch(path.name)}
    protected_ids = protected_ids | used_ids
    repositories = {prefix + "/" + component for prefix in configuration["repositoryPrefixes"]
                    for component in COMPONENTS}
    cutoff = dt.datetime.now(dt.timezone.utc) - dt.timedelta(days=7)
    for image in images:
        if image["Tag"] in release_ids | pending or not eligible_image(image, repositories, cutoff):
            protected_ids.add(image["ID"])
    references = {image_reference(image): image["ID"] for image in images
                  if image["ID"] not in protected_ids}
    return {"currentReleaseId": current, "protectedReleaseIds": sorted(release_ids),
            "minimumAgeDays": 7, "references": references}


def remove_reference(reference: str, image_id: str) -> None:
    inspected = subprocess.run([DOCKER, "image", "inspect", reference, "--format", "{{.Id}}"],
                               capture_output=True, text=True, timeout=30)
    if inspected.returncode and "No such image" in inspected.stderr:
        return
    if inspected.returncode or inspected.stdout.strip() != image_id:
        raise CleanupError("Image reference changed after preview")
    command([DOCKER, "image", "rm", reference])


def apply_plan(plan: dict, configuration: dict) -> None:
    for reference, image_id in plan["references"].items():
        refuse_active_changes()
        retention = retained_releases()
        if retention[0] != plan["currentReleaseId"] or sorted(retention[1]) != plan["protectedReleaseIds"]:
            raise CleanupBusy("Release retention changed; cleanup stopped")
        fresh = cleanup_plan(image_rows(), retention, configuration, used_image_ids())
        if fresh["references"].get(reference) == image_id:
            remove_reference(reference, image_id)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args()


def configuration() -> dict:
    if os.geteuid() != 0:
        raise CleanupError("Scheduled cleanup must run as root")
    configuration_path = Path("/etc/massar/image-cleanup.json")
    metadata = configuration_path.stat()
    if metadata.st_uid != 0 or metadata.st_mode & 0o022:
        raise CleanupError("Cleanup policy must be root owned and not writable by others")
    return read_json(configuration_path)


def execute_cleanup(policy: dict, mode: str) -> None:
    refuse_active_changes()
    plan = cleanup_plan(image_rows(), retained_releases(), policy, used_image_ids())
    print(json.dumps({"status": "dry-run", "nodeId": policy["nodeId"], **plan}), flush=True)
    before = os.statvfs("/")
    if mode == "--yes":
        apply_plan(plan, policy)
        preflight(policy)
    after = os.statvfs("/")
    print(json.dumps({"status": "completed" if mode == "--yes" else "preview-completed",
                      "nodeId": policy["nodeId"], "referenceCount": len(plan["references"]),
                      "reclaimedBytes": max(0, (after.f_bavail - before.f_bavail) * after.f_frsize)}))


def main() -> int:
    requested = arguments()
    policy = configuration()
    preflight(policy)
    with exclusive_lock(SHARED / ".image-cleanup.lock"), exclusive_lock(
        Path("/run/massar-install-immutable-release.lock")
    ):
        execute_cleanup(policy, "--yes" if requested.yes else "--dry-run")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except CleanupBusy as error:
        print(json.dumps({"status": "skipped", "reason": str(error)}))
    except (CleanupError, OSError, ValueError, KeyError, subprocess.TimeoutExpired) as error:
        reason = str(error) if isinstance(error, CleanupError) else type(error).__name__
        print(json.dumps({"status": "failed", "reason": reason}), file=sys.stderr)
        raise SystemExit(1)
