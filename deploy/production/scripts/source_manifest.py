#!/usr/bin/env python3
"""Seal and verify the complete changed workspace without exposing secrets."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import uuid
from collections import Counter
from pathlib import Path, PurePosixPath
from typing import Iterable


SCHEMA_VERSION = 2
DIGEST_ALGORITHM = "massar-complete-workspace-sha256-v2"
MAX_SECRET_SCAN_BYTES = 8 * 1024 * 1024
SENSITIVE_DIRECTORY_NAMES = frozenset(
    {"secret", "secrets", ".secrets", "credential", "credentials"}
)
SENSITIVE_FILE_NAMES = frozenset(
    {
        "id_dsa",
        "id_ecdsa",
        "id_ed25519",
        "id_rsa",
        "service-account.json",
        "google-application-credentials.json",
    }
)
SENSITIVE_SUFFIXES = frozenset(
    {".key", ".kdbx", ".keystore", ".p12", ".pem", ".pfx"}
)
BINARY_SUFFIXES = frozenset(
    {
        ".7z", ".aab", ".apk", ".avi", ".bin", ".bmp", ".db", ".dll", ".dylib",
        ".eot", ".exe", ".gif", ".gpg", ".gz", ".ico", ".jar", ".jpeg", ".jpg", ".mov",
        ".mp3", ".mp4", ".o", ".otf", ".pdf", ".png", ".so", ".tar", ".tiff",
        ".ttf", ".war", ".webm", ".webp", ".woff", ".woff2", ".zip",
    }
)
SECRET_PATTERNS = (
    ("private-key", re.compile(rb"-----BEGIN (?:OPENSSH|RSA|EC|DSA) PRIVATE KEY-----")),
    ("aws-access-key", re.compile(rb"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b")),
    ("github-token", re.compile(rb"\bgh[pousr]_[A-Za-z0-9]{30,255}\b")),
    ("google-api-key", re.compile(rb"\bAIza[0-9A-Za-z_-]{35}\b")),
    ("slack-token", re.compile(rb"\bxox[baprs]-[A-Za-z0-9-]{10,255}\b")),
    ("stripe-live-key", re.compile(rb"\bsk_live_[0-9A-Za-z]{20,255}\b")),
)


class ManifestError(RuntimeError):
    """Base error for source-manifest failures."""


class ManifestSafetyError(ManifestError):
    """Raised when an inventory item cannot be safely published."""


class WorkspaceDeltaError(ManifestError):
    """Raised when the workspace no longer matches a sealed manifest."""


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git_bytes(repo: Path, *arguments: str) -> bytes:
    try:
        return subprocess.check_output(
            ["git", "-C", str(repo), *arguments],
            stderr=subprocess.PIPE,
        )
    except subprocess.CalledProcessError as exc:
        raise ManifestError("unable to inspect Git workspace") from exc


def repository_root(repo: Path) -> Path:
    value = git_bytes(repo, "rev-parse", "--show-toplevel").decode(
        "utf-8", errors="strict"
    ).strip()
    root = Path(value).resolve()
    if not root.is_dir():
        raise ManifestError("Git workspace root is unavailable")
    return root


def normalize_relative_path(value: str) -> str:
    path = PurePosixPath(value)
    if path.is_absolute() or not path.parts or ".." in path.parts:
        raise ManifestSafetyError("inventory contains an unsafe relative path")
    return path.as_posix()


def tracked_changes(repo: Path) -> dict[str, str]:
    fields = git_bytes(
        repo,
        "diff",
        "--name-status",
        "-z",
        "--find-renames",
        "HEAD",
        "--",
    ).split(b"\0")
    changes: dict[str, str] = {}
    index = 0
    while index < len(fields) and fields[index]:
        code = fields[index].decode("ascii", errors="strict")
        index += 1
        if index >= len(fields) or not fields[index]:
            raise ManifestError("Git returned an incomplete tracked change")
        old_path = normalize_relative_path(
            fields[index].decode("utf-8", errors="surrogateescape")
        )
        index += 1
        kind = code[:1]
        if kind in {"R", "C"}:
            if index >= len(fields) or not fields[index]:
                raise ManifestError("Git returned an incomplete rename")
            new_path = normalize_relative_path(
                fields[index].decode("utf-8", errors="surrogateescape")
            )
            index += 1
            if kind == "R":
                changes[old_path] = "deleted"
            changes[new_path] = "added"
        elif kind == "D":
            changes[old_path] = "deleted"
        elif kind == "A":
            changes[old_path] = "added"
        elif kind in {"M", "T", "U"}:
            changes[old_path] = "modified"
        else:
            raise ManifestError(f"unsupported Git change type: {kind}")
    return changes


def tracked_files(repo: Path) -> tuple[str, ...]:
    values = git_bytes(repo, "ls-files", "--cached", "-z", "--").split(b"\0")
    return tuple(
        normalize_relative_path(value.decode("utf-8", errors="surrogateescape"))
        for value in values
        if value
    )


def untracked_files(repo: Path) -> tuple[str, ...]:
    values = git_bytes(
        repo,
        "ls-files",
        "--others",
        "--exclude-standard",
        "-z",
        "--",
    ).split(b"\0")
    return tuple(
        normalize_relative_path(value.decode("utf-8", errors="surrogateescape"))
        for value in values
        if value
    )


def classify_path(relative: str) -> str:
    path = PurePosixPath(relative)
    parts = tuple(part.lower() for part in path.parts)
    suffix = path.suffix.lower()
    if parts[0] == "artifacts":
        return "artifact"
    if parts[0] in {"specs"}:
        return "specification"
    if parts[0] in {"docs"} or suffix in {".md", ".mdx", ".rst"}:
        return "documentation"
    if "test" in parts or "tests" in parts or path.name.lower().startswith("test_"):
        return "test"
    if parts[0] in {"deploy", "docker"}:
        return "infrastructure"
    if parts[0] in {"backend", "frontend", "worker", "mobile"} and suffix in {
        ".cs", ".css", ".html", ".js", ".jsx", ".mjs", ".mts", ".py", ".sh",
        ".ts", ".tsx",
    }:
        return "source"
    if path.name.lower() in {
        "dockerfile", "makefile", "package-lock.json", "package.json",
        "tsconfig.json",
    } or suffix in {".json", ".toml", ".yaml", ".yml"}:
        return "configuration"
    if parts[0] in {".agents", ".github", ".specify", "scripts"}:
        return "tooling"
    return "other"


def sensitive_path_reason(relative: str) -> str | None:
    path = PurePosixPath(relative)
    lowered_parts = tuple(part.lower() for part in path.parts)
    name = path.name.lower()
    if name.startswith(".env") and name not in {".env.example", ".env.sample"}:
        return "environment-secret-file"
    if any(part in SENSITIVE_DIRECTORY_NAMES for part in lowered_parts):
        return "sensitive-directory"
    if name in SENSITIVE_FILE_NAMES:
        return "private-credential-file"
    if path.suffix.lower() in SENSITIVE_SUFFIXES:
        return "private-key-or-keystore"
    return None


def is_android_firebase_client_config(relative: str) -> bool:
    parts = PurePosixPath(relative).parts
    return (
        len(parts) == 4
        and parts[0] == "mobile"
        and parts[2] == "app"
        and parts[3] == "google-services.json"
    )


def secret_content_reason(
    path: Path,
    relative: str,
) -> tuple[str | None, bool]:
    size = path.stat().st_size
    suffix = path.suffix.lower()
    if suffix in BINARY_SUFFIXES:
        return None, False
    if size > MAX_SECRET_SCAN_BYTES:
        if classify_path(relative) == "artifact":
            return None, False
        raise ManifestSafetyError(
            f"{path.name}: text-or-unknown file exceeds safe secret-scan limit"
        )
    content = path.read_bytes()
    if b"\0" in content[:8192]:
        return None, False
    for label, pattern in SECRET_PATTERNS:
        if label == "google-api-key" and is_android_firebase_client_config(relative):
            continue
        if pattern.search(content):
            return label, True
    return None, True


def previous_sha256(repo: Path, relative: str) -> str | None:
    try:
        value = git_bytes(repo, "show", f"HEAD:{relative}")
    except ManifestError:
        return None
    return sha256_bytes(value)


def gitlink_index_commit(repo: Path, relative: str) -> str | None:
    stage = git_bytes(repo, "ls-files", "--stage", "-z", "--", relative)
    if not stage:
        return None
    metadata, separator, listed_path = stage.partition(b"\t")
    fields = metadata.split()
    decoded_path = listed_path.rstrip(b"\0").decode(
        "utf-8", errors="surrogateescape"
    )
    if (
        separator != b"\t"
        or decoded_path != relative
        or len(fields) != 3
        or fields[0] != b"160000"
    ):
        return None
    return fields[1].decode("ascii", errors="strict")


def gitlink_entry(
    repo: Path,
    relative: str,
    status_name: str,
    index_commit: str,
) -> dict[str, object]:
    path = repo / relative
    if not path.is_dir():
        raise ManifestSafetyError(f"{relative}: uninitialized-gitlink")
    if git_bytes(path, "status", "--porcelain", "-z"):
        raise ManifestSafetyError(f"{relative}: dirty-gitlink")
    current_commit = git_bytes(path, "rev-parse", "HEAD").decode("ascii").strip()
    if not re.fullmatch(r"[0-9a-f]{40,64}", current_commit):
        raise ManifestSafetyError(f"{relative}: invalid-gitlink-commit")
    return {
        "path": relative,
        "status": status_name,
        "classification": classify_path(relative),
        "entryType": "gitlink",
        "gitlinkCommit": current_commit,
        "sizeBytes": None,
        "sha256": sha256_bytes(current_commit.encode("ascii")),
        "previousSha256": sha256_bytes(index_commit.encode("ascii")),
    }


def inventory_entry(
    repo: Path,
    relative: str,
    status_name: str,
) -> tuple[dict[str, object], bool]:
    reason = sensitive_path_reason(relative)
    if reason:
        raise ManifestSafetyError(f"{relative}: {reason}")
    path = repo / relative
    if status_name == "deleted":
        prior_hash = previous_sha256(repo, relative)
        return (
            {
                "path": relative,
                "status": status_name,
                "classification": classify_path(relative),
                "sizeBytes": None,
                "sha256": None,
                "previousSha256": prior_hash,
            },
            False,
        )
    try:
        mode = path.lstat().st_mode
    except FileNotFoundError as exc:
        raise ManifestError(f"inventory file disappeared: {relative}") from exc
    if stat.S_ISDIR(mode):
        index_commit = gitlink_index_commit(repo, relative)
        if index_commit is not None:
            return gitlink_entry(
                repo,
                relative,
                status_name,
                index_commit,
            ), False
    if not stat.S_ISREG(mode):
        raise ManifestSafetyError(f"{relative}: non-regular-file")
    prior_hash = (
        previous_sha256(repo, relative)
        if status_name == "modified"
        else None
    )
    secret_reason, scanned = secret_content_reason(path, relative)
    if secret_reason:
        raise ManifestSafetyError(f"{relative}: {secret_reason}")
    return (
        {
            "path": relative,
            "status": status_name,
            "classification": classify_path(relative),
            "sizeBytes": path.stat().st_size,
            "sha256": sha256_file(path),
            "previousSha256": prior_hash,
        },
        scanned,
    )


def canonical_workspace_digest(
    git_head: str,
    entries: list[dict[str, object]],
    excluded_paths: tuple[str, ...],
) -> str:
    payload = {
        "algorithm": DIGEST_ALGORITHM,
        "gitHead": git_head,
        "entries": entries,
        "excludedPaths": list(excluded_paths),
    }
    encoded = json.dumps(
        payload,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return sha256_bytes(encoded)


def build_manifest(
    repo: Path,
    *,
    excluded_paths: Iterable[str] = (),
) -> dict[str, object]:
    root = repository_root(repo)
    exclusions = tuple(sorted({normalize_relative_path(path) for path in excluded_paths}))
    excluded = set(exclusions)
    changes = {
        relative: "tracked"
        for relative in tracked_files(root)
    }
    changes.update(tracked_changes(root))
    for relative in untracked_files(root):
        changes.setdefault(relative, "untracked")
    for relative in excluded:
        changes.pop(relative, None)

    entries: list[dict[str, object]] = []
    scanned_text = 0
    skipped_binary = 0
    for relative in sorted(changes):
        entry, scanned = inventory_entry(root, relative, changes[relative])
        entries.append(entry)
        if entry["status"] != "deleted":
            scanned_text += int(scanned)
            skipped_binary += int(not scanned)

    git_head = git_bytes(root, "rev-parse", "HEAD").decode("ascii").strip()
    counts = Counter(str(entry["status"]) for entry in entries)
    counts["total"] = len(entries)
    return {
        "schemaVersion": SCHEMA_VERSION,
        "scope": "complete-releasable-workspace",
        "digestAlgorithm": DIGEST_ALGORITHM,
        "generatedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace(
            "+00:00", "Z"
        ),
        "gitHead": git_head,
        "workspaceDigest": canonical_workspace_digest(
            git_head, entries, exclusions
        ),
        "excludedPaths": list(exclusions),
        "counts": {key: counts[key] for key in sorted(counts)},
        "entries": entries,
        "secretAudit": {
            "status": "passed",
            "textFilesScanned": scanned_text,
            "binaryFilesSkipped": skipped_binary,
            "detectors": [label for label, _ in SECRET_PATTERNS],
        },
    }


def relative_output(root: Path, output: Path) -> tuple[str, ...]:
    resolved = output.expanduser().resolve()
    try:
        relative = resolved.relative_to(root).as_posix()
    except ValueError:
        return ()
    return (normalize_relative_path(relative),)


def write_json_atomic(path: Path, value: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    try:
        temporary.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def seal_manifest(repo: Path, output: Path) -> dict[str, object]:
    root = repository_root(repo)
    manifest = build_manifest(
        root,
        excluded_paths=relative_output(root, output),
    )
    write_json_atomic(output.expanduser().resolve(), manifest)
    return manifest


def load_manifest(path: Path) -> dict[str, object]:
    resolved = path.expanduser()
    if resolved.is_symlink() or not resolved.is_file():
        raise ManifestError("sealed manifest must be a regular file")
    try:
        value = json.loads(resolved.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ManifestError("sealed manifest is not valid UTF-8 JSON") from exc
    required = {
        "schemaVersion", "scope", "digestAlgorithm", "generatedAt", "gitHead",
        "workspaceDigest", "excludedPaths", "counts", "entries", "secretAudit",
    }
    if not isinstance(value, dict) or set(value) != required:
        raise ManifestError("sealed manifest fields do not match the contract")
    if value["schemaVersion"] != SCHEMA_VERSION:
        raise ManifestError("sealed manifest schema version is unsupported")
    if (
        value["scope"] != "complete-releasable-workspace"
        or value["digestAlgorithm"] != DIGEST_ALGORITHM
    ):
        raise ManifestError("sealed manifest source scope is unsupported")
    if not isinstance(value["entries"], list) or not isinstance(
        value["excludedPaths"], list
    ):
        raise ManifestError("sealed manifest inventory is invalid")
    return value


def changed_paths(
    expected_entries: list[dict[str, object]],
    actual_entries: list[dict[str, object]],
) -> list[str]:
    expected = {str(entry["path"]): entry for entry in expected_entries}
    actual = {str(entry["path"]): entry for entry in actual_entries}
    return sorted(
        path
        for path in expected.keys() | actual.keys()
        if expected.get(path) != actual.get(path)
    )


def verify_manifest(repo: Path, manifest_path: Path) -> dict[str, object]:
    sealed = load_manifest(manifest_path)
    exclusions = tuple(str(path) for path in sealed["excludedPaths"])
    current = build_manifest(repo, excluded_paths=exclusions)
    if (
        sealed["workspaceDigest"] != current["workspaceDigest"]
        or sealed["gitHead"] != current["gitHead"]
    ):
        delta = changed_paths(sealed["entries"], current["entries"])
        visible = ", ".join(delta[:20]) if delta else "Git HEAD"
        if len(delta) > 20:
            visible += f", and {len(delta) - 20} more path(s)"
        raise WorkspaceDeltaError(
            f"workspace changed after seal; candidate invalidated: {visible}"
        )
    return current


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Create or verify a complete changed-workspace manifest."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)
    for command in ("create", "verify"):
        subparser = subparsers.add_parser(command)
        subparser.add_argument("--repo", type=Path, default=Path.cwd())
        if command == "create":
            subparser.add_argument("--output", type=Path, required=True)
        else:
            subparser.add_argument("--manifest", type=Path, required=True)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if args.command == "create":
        manifest = seal_manifest(args.repo, args.output)
    else:
        manifest = verify_manifest(args.repo, args.manifest)
    print(
        json.dumps(
            {
                "status": "success",
                "workspaceDigest": manifest["workspaceDigest"],
                "counts": manifest["counts"],
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ManifestError as exc:
        print(f"source manifest blocked: {exc}", file=sys.stderr)
        raise SystemExit(6)
