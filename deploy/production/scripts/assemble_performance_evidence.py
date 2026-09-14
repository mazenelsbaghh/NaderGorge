#!/usr/bin/env python3
"""Seal source provenance and assemble reproducible performance evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import stat
import sys
import uuid
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Mapping, Sequence

from release_images import SOURCE_EXCLUDED_PARTS, source_state


ROOT = Path(__file__).resolve().parents[3]
FINAL_ROOT = ROOT / "artifacts/performance-167/final"
RAW_ROOT = FINAL_ROOT / "raw"
SOURCE_MANIFEST_NAME = "source-manifest.json"
ROUTE_EVIDENCE_NAME = "route-resources.json"
BROWSER_EVIDENCE_NAME = "browser-samples.json"
QUERY_EVIDENCE_NAME = "live-support-query.json"
DEFAULT_CANDIDATE = FINAL_ROOT / "frontend-routes.json"
SOURCE_DIGEST_ALGORITHM = "massar-release-snapshot-sha256-v2"
HEX_40 = re.compile(r"^[0-9a-f]{40}$")
HEX_64 = re.compile(r"^[0-9a-f]{64}$")
SAFE_BUILD_ID = re.compile(r"^[A-Za-z0-9_-]{8,128}$")
SAFE_PLATFORM_TOKEN = re.compile(r"^[A-Za-z0-9._ /()-]{1,128}$")
SAFE_DATABASE_NAME = re.compile(
    r"^massar_live_support_query_budget_disposable_[a-z0-9_]{1,64}$"
)
EXPECTED_ROUTES = {
    "login": "/login",
    "register": "/register",
    "student": "/student",
    "admin": "/admin",
}
MEASURED_BROWSER_ROUTES = {
    "login": "/login",
    "register": "/register",
    "student": "/student",
}
SOURCE_BINDING_FIELDS = {
    "releaseId",
    "gitCommit",
    "sourceStateSha256",
    "dirtySourceSnapshot",
    "sourceDigestAlgorithm",
    "manifestSha256",
}


class PerformanceEvidenceError(RuntimeError):
    """Raised when raw performance evidence cannot be trusted."""


def sha256_bytes(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def _reject_json_constant(constant: str) -> None:
    raise PerformanceEvidenceError(f"JSON contains unsupported numeric constant: {constant}")


def _unique_json_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    parsed_object: dict[str, object] = {}
    for key, field_value in pairs:
        if key in parsed_object:
            raise PerformanceEvidenceError(f"JSON contains duplicate field: {key}")
        parsed_object[key] = field_value
    return parsed_object


def parse_json(content: bytes, label: str) -> dict[str, object]:
    try:
        parsed_object = json.loads(
            content.decode("utf-8"),
            object_pairs_hook=_unique_json_object,
            parse_constant=_reject_json_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise PerformanceEvidenceError(f"{label} must be valid UTF-8 JSON") from exc
    if not isinstance(parsed_object, dict):
        raise PerformanceEvidenceError(f"{label} must contain a JSON object")
    return parsed_object


def exact_fields(candidate: Mapping[str, object], expected: set[str], label: str) -> None:
    actual = set(candidate)
    if actual != expected:
        missing = sorted(expected - actual)
        unexpected = sorted(actual - expected)
        raise PerformanceEvidenceError(
            f"{label} fields are invalid; missing={missing}, unexpected={unexpected}"
        )


def required_mapping(candidate: object, label: str) -> Mapping[str, object]:
    if not isinstance(candidate, dict):
        raise PerformanceEvidenceError(f"{label} must be an object")
    return candidate


def required_list(candidate: object, label: str) -> list[object]:
    if not isinstance(candidate, list):
        raise PerformanceEvidenceError(f"{label} must be an array")
    return candidate


def required_string(candidate: object, label: str) -> str:
    if not isinstance(candidate, str) or not candidate:
        raise PerformanceEvidenceError(f"{label} must be a non-empty string")
    return candidate


def required_integer(candidate: object, label: str, minimum: int = 0) -> int:
    if isinstance(candidate, bool) or not isinstance(candidate, int) or candidate < minimum:
        raise PerformanceEvidenceError(f"{label} must be an integer >= {minimum}")
    return candidate


def required_number(candidate: object, label: str) -> float | int:
    if isinstance(candidate, bool) or not isinstance(candidate, (int, float)):
        raise PerformanceEvidenceError(f"{label} must be a finite non-negative number")
    if not math.isfinite(candidate) or candidate < 0:
        raise PerformanceEvidenceError(f"{label} must be a finite non-negative number")
    return candidate


def safe_relative_path(candidate: object, label: str) -> str:
    relative = PurePosixPath(required_string(candidate, label))
    if relative.is_absolute() or ".." in relative.parts or not relative.parts:
        raise PerformanceEvidenceError(f"{label} must be a safe relative path")
    return relative.as_posix()


def _absolute(path: Path) -> Path:
    return Path(os.path.abspath(path.expanduser()))


def _require_directory(path: Path, label: str) -> Path:
    absolute = _absolute(path)
    try:
        mode = absolute.lstat().st_mode
    except FileNotFoundError as exc:
        raise PerformanceEvidenceError(f"{label} must exist") from exc
    if not stat.S_ISDIR(mode) or stat.S_ISLNK(mode):
        raise PerformanceEvidenceError(f"{label} must be a regular non-symlink directory")
    return absolute


def require_regular_file(path: Path, label: str) -> Path:
    absolute = _absolute(path)
    try:
        mode = absolute.lstat().st_mode
    except FileNotFoundError as exc:
        raise PerformanceEvidenceError(f"{label} must be a regular non-symlink file") from exc
    if not stat.S_ISREG(mode) or stat.S_ISLNK(mode):
        raise PerformanceEvidenceError(f"{label} must be a regular non-symlink file")
    return absolute


def require_file_beneath(path: Path, root: Path, label: str) -> Path:
    root_absolute = _require_directory(root, "raw evidence root")
    file_absolute = _absolute(path)
    try:
        relative = file_absolute.relative_to(root_absolute)
    except ValueError as exc:
        raise PerformanceEvidenceError(f"{label} must remain beneath the raw evidence root") from exc
    current = root_absolute
    for part in relative.parts[:-1]:
        current = _require_directory(current / part, f"{label} parent")
    return require_regular_file(file_absolute, label)


def ensure_directory_beneath(repository: Path, directory: Path) -> Path:
    repository_root = _require_directory(repository, "repository root")
    requested = _absolute(directory)
    try:
        relative = requested.relative_to(repository_root)
    except ValueError as exc:
        raise PerformanceEvidenceError("evidence directory must remain beneath the repository") from exc
    current = repository_root
    for part in relative.parts:
        current = current / part
        try:
            mode = current.lstat().st_mode
        except FileNotFoundError:
            current.mkdir(mode=0o700)
            mode = current.lstat().st_mode
        if not stat.S_ISDIR(mode) or stat.S_ISLNK(mode):
            raise PerformanceEvidenceError("evidence directory chain must not contain symlinks")
    return requested


def fixed_raw_root(repository: Path) -> Path:
    return _absolute(repository) / "artifacts/performance-167/final/raw"


def _require_fixed_path(actual: Path, expected: Path, label: str) -> None:
    if _absolute(actual) != _absolute(expected):
        raise PerformanceEvidenceError(f"{label} must be {expected}")


def write_create_new_atomic(path: Path, content: bytes) -> None:
    destination = _absolute(path)
    parent = _require_directory(destination.parent, "evidence output parent")
    temporary = parent / f".{destination.name}.{os.getpid()}.{uuid.uuid4().hex}.tmp"
    descriptor = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        try:
            os.link(temporary, destination, follow_symlinks=False)
        except FileExistsError as exc:
            raise PerformanceEvidenceError(
                f"refusing to overwrite existing evidence: {destination}"
            ) from exc
        require_regular_file(destination, "evidence output")
        directory_descriptor = os.open(parent, os.O_RDONLY)
        try:
            os.fsync(directory_descriptor)
        finally:
            os.close(directory_descriptor)
    finally:
        temporary.unlink(missing_ok=True)


def canonical_json(evidence: Mapping[str, object]) -> bytes:
    return (json.dumps(evidence, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _source_entry(raw_entry: object, index: int) -> dict[str, object]:
    entry = required_mapping(raw_entry, f"sourcePaths[{index}]")
    exact_fields(
        entry,
        {"path", "status", "classification", "sizeBytes", "sha256"},
        f"sourcePaths[{index}]",
    )
    relative = safe_relative_path(entry["path"], f"sourcePaths[{index}].path")
    _validate_source_path(relative)
    classification = required_string(
        entry["classification"], f"sourcePaths[{index}].classification"
    )
    if classification == "artifact":
        raise PerformanceEvidenceError("artifact classifications must not be included in sourcePaths")
    digest = required_string(entry["sha256"], f"sourcePaths[{index}].sha256")
    if not HEX_64.fullmatch(digest):
        raise PerformanceEvidenceError(f"sourcePaths[{index}].sha256 is invalid")
    return {
        "path": relative,
        "status": _source_status(entry["status"], index),
        "classification": classification,
        "sizeBytes": required_integer(
            entry["sizeBytes"], f"sourcePaths[{index}].sizeBytes"
        ),
        "sha256": digest,
    }


def _validate_source_path(relative: str) -> None:
    parts = PurePosixPath(relative).parts
    if parts[0] == "artifacts":
        raise PerformanceEvidenceError("artifact files must not be included in sourcePaths")
    if any(part in SOURCE_EXCLUDED_PARTS for part in parts):
        raise PerformanceEvidenceError(f"sourcePaths contains excluded build output: {relative}")


def _source_status(raw_status: object, index: int) -> str:
    status_value = required_string(raw_status, f"sourcePaths[{index}].status")
    if status_value not in {"tracked", "modified", "added", "untracked"}:
        raise PerformanceEvidenceError(f"sourcePaths[{index}].status is invalid")
    return status_value


def source_digest(entries: Sequence[Mapping[str, object]]) -> str:
    digest = hashlib.sha256()
    for entry in entries:
        digest.update(str(entry["path"]).encode("utf-8", errors="surrogateescape"))
        digest.update(b"\0")
        digest.update(str(entry["sha256"]).encode("ascii"))
        digest.update(b"\0")
    return digest.hexdigest()


def _validate_release_identity(manifest: Mapping[str, object]) -> None:
    git_commit = required_string(manifest["gitCommit"], "source manifest gitCommit")
    source_sha256 = required_string(
        manifest["sourceStateSha256"], "source manifest sourceStateSha256"
    )
    if not HEX_40.fullmatch(git_commit) or not HEX_64.fullmatch(source_sha256):
        raise PerformanceEvidenceError("source manifest Git or source digest is invalid")
    if not isinstance(manifest["dirtySourceSnapshot"], bool):
        raise PerformanceEvidenceError("source manifest dirtySourceSnapshot must be boolean")
    expected_release = (
        f"src-{source_sha256[:40]}"
        if manifest["dirtySourceSnapshot"]
        else f"git-{git_commit}"
    )
    if manifest["releaseId"] != expected_release:
        raise PerformanceEvidenceError("source manifest release identity is inconsistent")


def validate_source_manifest(
    manifest: Mapping[str, object],
    current_state: Mapping[str, object],
) -> dict[str, object]:
    _validate_source_manifest_header(manifest)
    entries = _source_entries(manifest["sourcePaths"])
    if source_digest(entries) != manifest["sourceStateSha256"]:
        raise PerformanceEvidenceError("sourcePaths do not match sourceStateSha256")
    deleted = _deleted_source_paths(manifest["deletedSourcePaths"])
    dirty = any(entry["status"] != "tracked" for entry in entries) or bool(deleted)
    if manifest["dirtySourceSnapshot"] is not dirty:
        raise PerformanceEvidenceError("source manifest dirtySourceSnapshot is inconsistent")
    _validate_current_source(entries, deleted, manifest, current_state)
    return dict(manifest)


def _validate_source_manifest_header(manifest: Mapping[str, object]) -> None:
    exact_fields(
        manifest,
        {
            "schemaVersion",
            "releaseId",
            "gitCommit",
            "sourceStateSha256",
            "dirtySourceSnapshot",
            "sourceDigestAlgorithm",
            "sourcePaths",
            "deletedSourcePaths",
        },
        "source manifest",
    )
    if manifest["schemaVersion"] != 2:
        raise PerformanceEvidenceError("source manifest schemaVersion must be 2")
    if manifest["sourceDigestAlgorithm"] != SOURCE_DIGEST_ALGORITHM:
        raise PerformanceEvidenceError("source manifest digest algorithm is unsupported")
    _validate_release_identity(manifest)


def _source_entries(raw_entries: object) -> list[dict[str, object]]:
    entries = [
        _source_entry(entry, index)
        for index, entry in enumerate(required_list(raw_entries, "sourcePaths"))
    ]
    paths = [str(entry["path"]) for entry in entries]
    if paths != sorted(set(paths)):
        raise PerformanceEvidenceError("sourcePaths must be sorted and unique")
    return entries


def _deleted_source_paths(raw_paths: object) -> list[str]:
    deleted = [
        safe_relative_path(path, "deletedSourcePaths entry")
        for path in required_list(raw_paths, "deletedSourcePaths")
    ]
    if deleted != sorted(set(deleted)) or any(path.startswith("artifacts/") for path in deleted):
        raise PerformanceEvidenceError("deletedSourcePaths must be sorted, unique, and artifact-free")
    return deleted


def _entry_projection(entries: Iterable[Mapping[str, object]]) -> list[tuple[object, ...]]:
    return [
        (
            entry.get("path"),
            entry.get("classification"),
            entry.get("sizeBytes"),
            entry.get("sha256"),
        )
        for entry in entries
    ]


def _validate_current_source(
    entries: Sequence[Mapping[str, object]],
    deleted: Sequence[str],
    manifest: Mapping[str, object],
    current_state: Mapping[str, object],
) -> None:
    # Evidence artifacts may be committed after measurement, so the measured
    # commit is recorded while the artifact-free digest and inventory stay authoritative.
    if current_state.get("sourceDigestAlgorithm") != SOURCE_DIGEST_ALGORITHM:
        raise PerformanceEvidenceError("current source uses an unsupported digest algorithm")
    if current_state.get("sourceStateSha256") != manifest["sourceStateSha256"]:
        raise PerformanceEvidenceError("source changed after performance measurement")
    current_entries = required_list(current_state.get("sourcePaths"), "current sourcePaths")
    if _entry_projection(entries) != _entry_projection(
        required_mapping(entry, "current source entry") for entry in current_entries
    ):
        raise PerformanceEvidenceError("source path inventory changed after measurement")
    if list(current_state.get("deletedSourcePaths", [])) != list(deleted):
        raise PerformanceEvidenceError("deleted source inventory changed after measurement")


def seal_source(repository: Path, output: Path) -> dict[str, object]:
    _require_fixed_path(
        output,
        fixed_raw_root(repository) / SOURCE_MANIFEST_NAME,
        "source manifest output",
    )
    state = source_state(_absolute(repository))
    manifest = {"schemaVersion": 2, **state}
    validate_source_manifest(manifest, state)
    ensure_directory_beneath(repository, output.parent)
    write_create_new_atomic(output, canonical_json(manifest))
    return manifest


def _source_binding(
    raw_binding: object,
    manifest: Mapping[str, object],
    manifest_sha256: str,
    label: str,
) -> dict[str, object]:
    binding = required_mapping(raw_binding, label)
    exact_fields(binding, SOURCE_BINDING_FIELDS, label)
    expected = {
        "releaseId": manifest["releaseId"],
        "gitCommit": manifest["gitCommit"],
        "sourceStateSha256": manifest["sourceStateSha256"],
        "dirtySourceSnapshot": manifest["dirtySourceSnapshot"],
        "sourceDigestAlgorithm": manifest["sourceDigestAlgorithm"],
        "manifestSha256": manifest_sha256,
    }
    if binding != expected:
        raise PerformanceEvidenceError(f"{label} does not match the authoritative source manifest")
    return dict(binding)


def _resource(raw_resource: object, label: str) -> dict[str, object]:
    resource = required_mapping(raw_resource, label)
    exact_fields(resource, {"path", "type", "bytes", "gzipBytes", "brotliBytes"}, label)
    relative = safe_relative_path(resource["path"], f"{label}.path")
    if not relative.startswith("static/chunks/"):
        raise PerformanceEvidenceError(f"{label}.path must reference a production chunk")
    suffix = PurePosixPath(relative).suffix.removeprefix(".")
    resource_type = required_string(resource["type"], f"{label}.type")
    if suffix not in {"js", "css"} or resource_type != suffix:
        raise PerformanceEvidenceError(f"{label} must reference a JS or CSS resource")
    return {
        "path": relative,
        "type": resource_type,
        "bytes": required_integer(resource["bytes"], f"{label}.bytes"),
        "gzipBytes": required_integer(resource["gzipBytes"], f"{label}.gzipBytes"),
        "brotliBytes": required_integer(resource["brotliBytes"], f"{label}.brotliBytes"),
    }


def _resource_summary(raw_summary: object, label: str) -> dict[str, object]:
    summary = required_mapping(raw_summary, label)
    exact_fields(
        summary,
        {"resourceCount", "bytes", "gzipBytes", "brotliBytes", "resources"},
        label,
    )
    resources = [
        _resource(resource, f"{label}.resources[{index}]")
        for index, resource in enumerate(required_list(summary["resources"], f"{label}.resources"))
    ]
    paths = [str(resource["path"]) for resource in resources]
    if paths != sorted(set(paths)):
        raise PerformanceEvidenceError(f"{label}.resources must be sorted and unique")
    computed = {
        "resourceCount": len(resources),
        "bytes": sum(int(resource["bytes"]) for resource in resources),
        "gzipBytes": sum(int(resource["gzipBytes"]) for resource in resources),
        "brotliBytes": sum(int(resource["brotliBytes"]) for resource in resources),
        "resources": resources,
    }
    if dict(summary) != computed:
        raise PerformanceEvidenceError(f"{label} aggregate does not match its resource rows")
    return computed


def _route_resource_evidence(
    raw_route: object,
    name: str,
    expected_pathname: str,
) -> dict[str, object]:
    route = required_mapping(raw_route, f"route evidence {name}")
    exact_fields(
        route,
        {"pathname", "manifestKey", "initial", "shared", "deferred", "total"},
        f"route evidence {name}",
    )
    if route["pathname"] != expected_pathname:
        raise PerformanceEvidenceError(f"route evidence {name} pathname is invalid")
    required_string(route["manifestKey"], f"route evidence {name}.manifestKey")
    summaries = {
        bucket: _resource_summary(route[bucket], f"route evidence {name}.{bucket}")
        for bucket in ("initial", "shared", "deferred", "total")
    }
    expected_total = _merged_resources(
        summaries["initial"], summaries["shared"], summaries["deferred"]
    )
    if summaries["total"] != expected_total:
        raise PerformanceEvidenceError(f"route evidence {name}.total is inconsistent")
    return {
        "pathname": expected_pathname,
        "initial": summaries["initial"],
        "shared": summaries["shared"],
        "deferred": summaries["deferred"],
    }


def _merged_resources(*summaries: Mapping[str, object]) -> dict[str, object]:
    resources_by_path: dict[str, dict[str, object]] = {}
    for summary in summaries:
        for raw_resource in required_list(summary["resources"], "resource summary rows"):
            resource = required_mapping(raw_resource, "resource summary row")
            relative = str(resource["path"])
            prior = resources_by_path.setdefault(relative, dict(resource))
            if prior != resource:
                raise PerformanceEvidenceError(f"resource metadata conflicts for {relative}")
    resources = [resources_by_path[path] for path in sorted(resources_by_path)]
    return {
        "resourceCount": len(resources),
        "bytes": sum(int(resource["bytes"]) for resource in resources),
        "gzipBytes": sum(int(resource["gzipBytes"]) for resource in resources),
        "brotliBytes": sum(int(resource["brotliBytes"]) for resource in resources),
        "resources": resources,
    }


def _validate_route_measurement(raw_measurement: object) -> dict[str, object]:
    measurement = required_mapping(raw_measurement, "route measurement")
    exact_fields(
        measurement,
        {
            "source",
            "productionBuildExecuted",
            "buildStartedAt",
            "buildId",
            "compression",
            "classification",
        },
        "route measurement",
    )
    if measurement["productionBuildExecuted"] is not True:
        raise PerformanceEvidenceError("route evidence must come from a production build")
    build_id = required_string(measurement["buildId"], "route measurement.buildId")
    if not SAFE_BUILD_ID.fullmatch(build_id):
        raise PerformanceEvidenceError("route measurement.buildId is invalid")
    required_string(measurement["buildStartedAt"], "route measurement.buildStartedAt")
    compression = required_mapping(measurement["compression"], "route compression")
    exact_fields(compression, {"gzipLevel", "brotliQuality", "note"}, "route compression")
    if compression.get("gzipLevel") != 9 or compression.get("brotliQuality") != 11:
        raise PerformanceEvidenceError("route compression settings are not reproducible")
    required_string(compression["note"], "route compression.note")
    classification = required_mapping(measurement["classification"], "route classification")
    exact_fields(classification, {"shared", "initial", "deferred"}, "route classification")
    for bucket in ("shared", "initial", "deferred"):
        required_string(classification[bucket], f"route classification.{bucket}")
    return dict(measurement)


def _validate_platform(raw_platform: object) -> None:
    platform = required_mapping(raw_platform, "route platform")
    exact_fields(
        platform,
        {"operatingSystem", "architecture", "nodeVersion"},
        "route platform",
    )
    for field in ("operatingSystem", "architecture", "nodeVersion"):
        token = required_string(platform[field], f"route platform.{field}")
        if not SAFE_PLATFORM_TOKEN.fullmatch(token):
            raise PerformanceEvidenceError(f"route platform.{field} is invalid")


def route_metrics(
    raw: Mapping[str, object],
    manifest: Mapping[str, object],
    manifest_sha256: str,
) -> tuple[dict[str, dict[str, object]], dict[str, object], dict[str, object]]:
    exact_fields(
        raw,
        {
            "schemaVersion",
            "evidenceType",
            "generatedAt",
            "source",
            "platform",
            "measurement",
            "shared",
            "routes",
        },
        "route resource evidence",
    )
    if raw["schemaVersion"] != 1 or raw["evidenceType"] != "route-resource-measurement":
        raise PerformanceEvidenceError("route resource evidence schema is unsupported")
    source = _source_binding(raw["source"], manifest, manifest_sha256, "route source")
    _validate_platform(raw["platform"])
    measurement = _validate_route_measurement(raw["measurement"])
    shared = _resource_summary(raw["shared"], "route shared resources")
    metrics = _fixed_route_metrics(raw["routes"])
    if any(route["shared"] != shared for route in metrics.values()):
        raise PerformanceEvidenceError("route shared resource summaries are inconsistent")
    return metrics, measurement, source


def _fixed_route_metrics(raw_routes: object) -> dict[str, dict[str, object]]:
    routes = required_mapping(raw_routes, "route resources")
    if set(routes) != set(EXPECTED_ROUTES):
        raise PerformanceEvidenceError("route resource evidence must contain the fixed route set")
    return {
        name: _route_resource_evidence(routes[name], name, pathname)
        for name, pathname in EXPECTED_ROUTES.items()
    }


def _browser_profile(raw_profile: object) -> dict[str, object]:
    profile = required_mapping(raw_profile, "browser profile")
    exact_fields(
        profile,
        {"name", "browserName", "viewport", "productionServer", "buildId"},
        "browser profile",
    )
    if profile["browserName"] != "chromium" or profile["productionServer"] is not True:
        raise PerformanceEvidenceError("browser evidence must use Chromium and a production server")
    build_id = required_string(profile["buildId"], "browser profile.buildId")
    if not SAFE_BUILD_ID.fullmatch(build_id):
        raise PerformanceEvidenceError("browser profile.buildId is invalid")
    viewport = required_mapping(profile["viewport"], "browser viewport")
    exact_fields(viewport, {"width", "height"}, "browser viewport")
    required_integer(viewport["width"], "browser viewport.width", 1)
    required_integer(viewport["height"], "browser viewport.height", 1)
    return dict(profile)


def _browser_sampling(raw_sampling: object) -> dict[str, object]:
    sampling = required_mapping(raw_sampling, "browser sampling")
    exact_fields(
        sampling,
        {
            "warmupCount",
            "measuredCount",
            "quietWindowMs",
            "quietTimeoutMs",
            "percentileMethod",
        },
        "browser sampling",
    )
    expected = {
        "warmupCount": 3,
        "measuredCount": 20,
        "quietWindowMs": 250,
        "quietTimeoutMs": 2_000,
        "percentileMethod": "nearest-rank",
    }
    if sampling != expected:
        raise PerformanceEvidenceError("browser sampling does not match the fixed protocol")
    return dict(sampling)


def _eligible_reads(raw_reads: object, label: str) -> list[dict[str, object]]:
    reads: list[dict[str, object]] = []
    for index, raw_read in enumerate(required_list(raw_reads, label)):
        read_label = f"{label}[{index}]"
        read = required_mapping(raw_read, read_label)
        exact_fields(read, {"identitySha256", "category", "count"}, read_label)
        identity = required_string(read["identitySha256"], f"{read_label}.identitySha256")
        if not HEX_64.fullmatch(identity):
            raise PerformanceEvidenceError(f"{read_label}.identitySha256 is invalid")
        if read["category"] not in {"api-read", "rsc-read"}:
            raise PerformanceEvidenceError(f"{read_label}.category is invalid")
        reads.append(
            {
                "identitySha256": identity,
                "category": read["category"],
                "count": required_integer(read["count"], f"{read_label}.count", 1),
            }
        )
    identities = [
        (str(read["category"]), str(read["identitySha256"])) for read in reads
    ]
    if identities != sorted(set(identities)):
        raise PerformanceEvidenceError(f"{label} identities must be sorted and unique")
    return reads


def _nearest_rank(values: Sequence[float | int], percentile: float) -> float | int:
    if not values:
        raise PerformanceEvidenceError("percentile requires at least one measurement")
    ordered = sorted(values)
    return ordered[math.ceil(percentile * len(ordered)) - 1]


def _browser_route_metrics(raw_route: object, name: str, pathname: str) -> dict[str, object]:
    route = required_mapping(raw_route, f"browser route {name}")
    exact_fields(route, {"pathname", "samples"}, f"browser route {name}")
    if route["pathname"] != pathname:
        raise PerformanceEvidenceError(f"browser route {name} pathname is invalid")
    samples = required_list(route["samples"], f"browser route {name}.samples")
    if len(samples) != 20:
        raise PerformanceEvidenceError(f"browser route {name} must contain 20 measured samples")
    navigation: list[float | int] = []
    duplicate_reads: list[int] = []
    for index, raw_sample in enumerate(samples, start=1):
        label = f"browser route {name}.samples[{index - 1}]"
        sample = required_mapping(raw_sample, label)
        exact_fields(sample, {"sequence", "warmNavigationMs", "eligibleReads"}, label)
        if sample["sequence"] != index:
            raise PerformanceEvidenceError(f"{label}.sequence must be {index}")
        navigation.append(required_number(sample["warmNavigationMs"], f"{label}.warmNavigationMs"))
        reads = _eligible_reads(sample["eligibleReads"], f"{label}.eligibleReads")
        duplicate_reads.append(sum(int(read["count"]) - 1 for read in reads))
    return {
        "requests": {
            "duplicateEligibleReads": max(duplicate_reads),
            "sampleCount": len(samples),
        },
        "navigation": {
            "warmP75Ms": _nearest_rank(navigation, 0.75),
            "sampleCount": len(samples),
            "percentileMethod": "nearest-rank",
        },
    }


def browser_metrics(
    raw: Mapping[str, object],
    manifest: Mapping[str, object],
    manifest_sha256: str,
) -> tuple[dict[str, dict[str, object]], dict[str, object], dict[str, object], dict[str, object]]:
    exact_fields(
        raw,
        {"schemaVersion", "evidenceType", "source", "profile", "sampling", "routes"},
        "browser evidence",
    )
    if raw["schemaVersion"] != 1 or raw["evidenceType"] != "browser-performance-samples":
        raise PerformanceEvidenceError("browser evidence schema is unsupported")
    source = _source_binding(raw["source"], manifest, manifest_sha256, "browser source")
    profile = _browser_profile(raw["profile"])
    sampling = _browser_sampling(raw["sampling"])
    routes = required_mapping(raw["routes"], "browser routes")
    if set(routes) != set(MEASURED_BROWSER_ROUTES):
        raise PerformanceEvidenceError("browser evidence must contain the fixed route set")
    metrics = {
        name: _browser_route_metrics(routes[name], name, pathname)
        for name, pathname in MEASURED_BROWSER_ROUTES.items()
    }
    return metrics, profile, sampling, source


def _query_observation(raw_observation: object, label: str) -> dict[str, int]:
    observation = required_mapping(raw_observation, label)
    exact_fields(observation, {"databaseCommands", "returnedRows"}, label)
    return {
        "databaseCommands": required_integer(
            observation["databaseCommands"], f"{label}.databaseCommands", 1
        ),
        "returnedRows": required_integer(observation["returnedRows"], f"{label}.returnedRows"),
    }


def _query_measurements(raw_measurement_rows: object) -> tuple[list[dict[str, object]], int]:
    raw_measurements = required_list(raw_measurement_rows, "query measurements")
    if len(raw_measurements) != 3:
        raise PerformanceEvidenceError("query evidence must contain three representative measurements")
    measurements: list[dict[str, object]] = []
    observed: list[int] = []
    for index, raw_measurement in enumerate(raw_measurements):
        label = f"query measurements[{index}]"
        measurement = required_mapping(raw_measurement, label)
        exact_fields(measurement, {"rowCount", "dashboard", "history", "timeline"}, label)
        projected: dict[str, object] = {
            "rowCount": required_integer(measurement["rowCount"], f"{label}.rowCount", 1)
        }
        for workflow in ("dashboard", "history", "timeline"):
            observation = _query_observation(measurement[workflow], f"{label}.{workflow}")
            projected[workflow] = observation
            observed.append(observation["databaseCommands"])
        measurements.append(projected)
    if [measurement["rowCount"] for measurement in measurements] != [1, 20, 100]:
        raise PerformanceEvidenceError("query row counts must be exactly [1, 20, 100]")
    return measurements, max(observed)


def query_metrics(
    raw: Mapping[str, object],
    manifest: Mapping[str, object],
    manifest_sha256: str,
) -> tuple[dict[str, object], dict[str, object]]:
    source = _query_source(raw, manifest, manifest_sha256)
    if raw["rowCounts"] != [1, 20, 100]:
        raise PerformanceEvidenceError("query rowCounts must be exactly [1, 20, 100]")
    measurements, maximum = _query_measurements(raw["measurements"])
    _validate_query_database(raw["database"])
    _validate_query_maximum(raw["workflows"], maximum)
    return {
        "maximumDatabaseCommandsObserved": maximum,
        "representativeRowCounts": [1, 20, 100],
        "measurementCount": len(measurements),
    }, source


def _query_source(
    raw: Mapping[str, object],
    manifest: Mapping[str, object],
    manifest_sha256: str,
) -> dict[str, object]:
    exact_fields(
        raw,
        {
            "schemaVersion",
            "evidenceType",
            "source",
            "database",
            "rowCounts",
            "measurements",
            "workflows",
        },
        "live-support query evidence",
    )
    if raw["schemaVersion"] != 1 or raw["evidenceType"] != "live-support-query-budget":
        raise PerformanceEvidenceError("live-support query evidence schema is unsupported")
    return _source_binding(raw["source"], manifest, manifest_sha256, "query source")


def _validate_query_database(raw_database: object) -> None:
    database = required_mapping(raw_database, "query database")
    exact_fields(
        database,
        {"databaseName", "identitySha256", "serverVersion", "serverVersionNumber"},
        "query database",
    )
    database_name = required_string(database["databaseName"], "query database.databaseName")
    if not SAFE_DATABASE_NAME.fullmatch(database_name):
        raise PerformanceEvidenceError("query evidence did not use the disposable database contract")
    identity = required_string(database["identitySha256"], "query database.identitySha256")
    if not HEX_64.fullmatch(identity):
        raise PerformanceEvidenceError("query database.identitySha256 is invalid")
    required_string(database["serverVersion"], "query database.serverVersion")
    required_integer(database["serverVersionNumber"], "query database.serverVersionNumber", 1)


def _validate_query_maximum(raw_workflows: object, maximum: int) -> None:
    workflows = required_mapping(raw_workflows, "query workflows")
    exact_fields(workflows, {"live-support-admin"}, "query workflows")
    live_support = required_mapping(workflows["live-support-admin"], "live-support workflow")
    exact_fields(live_support, {"maximumDatabaseCommandsObserved"}, "live-support workflow")
    if live_support["maximumDatabaseCommandsObserved"] != maximum:
        raise PerformanceEvidenceError("query workflow maximum was not derived from measurements")


def _candidate_summary(summary: Mapping[str, object]) -> dict[str, object]:
    return {
        "resourceCount": summary["resourceCount"],
        "bytes": summary["bytes"],
        "gzipBytes": summary["gzipBytes"],
        "brotliBytes": summary["brotliBytes"],
    }


def _raw_descriptor(repository: Path, path: Path, content: bytes) -> dict[str, str]:
    return {
        "path": _absolute(path).relative_to(_absolute(repository)).as_posix(),
        "sha256": sha256_bytes(content),
    }


def _candidate_source(
    source: Mapping[str, object],
) -> dict[str, object]:
    return {
        "releaseId": source["releaseId"],
        "measuredGitCommit": source["gitCommit"],
        "sourceStateSha256": source["sourceStateSha256"],
        "dirtySourceSnapshot": source["dirtySourceSnapshot"],
        "sourceDigestAlgorithm": source["sourceDigestAlgorithm"],
        "manifestSha256": source["manifestSha256"],
        "commitBinding": "measured-commit-source-digest-authoritative",
    }


def _raw_paths(raw_root: Path) -> dict[str, Path]:
    return {
        "sourceManifest": raw_root / SOURCE_MANIFEST_NAME,
        "routeResources": raw_root / ROUTE_EVIDENCE_NAME,
        "browserSamples": raw_root / BROWSER_EVIDENCE_NAME,
        "liveSupportQuery": raw_root / QUERY_EVIDENCE_NAME,
    }


def _raw_contents(raw_root: Path, raw_paths: Mapping[str, Path]) -> dict[str, bytes]:
    return {
        name: require_file_beneath(path, raw_root, name).read_bytes()
        for name, path in raw_paths.items()
    }


def _assembled_routes(
    routes: Mapping[str, Mapping[str, object]],
    browser: Mapping[str, Mapping[str, object]],
) -> dict[str, object]:
    return {
        name: {
            "pathname": route["pathname"],
            "initial": _candidate_summary(required_mapping(route["initial"], "initial")),
            "shared": _candidate_summary(required_mapping(route["shared"], "shared")),
            "deferred": _candidate_summary(required_mapping(route["deferred"], "deferred")),
            **browser.get(name, {}),
        }
        for name, route in routes.items()
    }


def assemble_candidate(repository: Path, raw_root: Path) -> dict[str, object]:
    _require_fixed_path(raw_root, fixed_raw_root(repository), "raw evidence root")
    raw_paths = _raw_paths(raw_root)
    contents = _raw_contents(raw_root, raw_paths)
    manifest = parse_json(contents["sourceManifest"], "source manifest")
    validate_source_manifest(manifest, source_state(_absolute(repository)))
    manifest_sha256 = sha256_bytes(contents["sourceManifest"])
    route_raw = parse_json(contents["routeResources"], "route resource evidence")
    browser_raw = parse_json(contents["browserSamples"], "browser evidence")
    query_raw = parse_json(contents["liveSupportQuery"], "live-support query evidence")
    routes, route_measurement, route_source = route_metrics(
        route_raw, manifest, manifest_sha256
    )
    browser, browser_profile, browser_sampling, browser_source = browser_metrics(
        browser_raw, manifest, manifest_sha256
    )
    query, query_source = query_metrics(query_raw, manifest, manifest_sha256)
    if route_source != browser_source or route_source != query_source:
        raise PerformanceEvidenceError("raw evidence source bindings are inconsistent")
    if route_measurement["buildId"] != browser_profile["buildId"]:
        raise PerformanceEvidenceError("route and browser evidence used different production builds")
    return {
        "schemaVersion": 2,
        "evidenceType": "assembled-performance-candidate",
        "source": _candidate_source(route_source),
        "rawEvidence": {
            name: _raw_descriptor(repository, raw_paths[name], contents[name])
            for name in raw_paths
        },
        "measurement": {
            "productionBuildExecuted": True,
            "buildId": route_measurement["buildId"],
            "compression": route_measurement["compression"],
            "browserProfile": browser_profile,
            "browserSampling": browser_sampling,
        },
        "routes": _assembled_routes(routes, browser),
        "workflows": {"live-support-admin": query},
    }


def write_candidate(repository: Path, raw_root: Path, output: Path) -> dict[str, object]:
    _require_fixed_path(
        output,
        _absolute(repository) / "artifacts/performance-167/final/frontend-routes.json",
        "performance candidate output",
    )
    candidate = assemble_candidate(repository, raw_root)
    ensure_directory_beneath(repository, output.parent)
    write_create_new_atomic(output, canonical_json(candidate))
    return candidate


def validate_candidate(repository: Path, raw_root: Path, candidate_path: Path) -> dict[str, object]:
    _require_fixed_path(
        candidate_path,
        _absolute(repository) / "artifacts/performance-167/final/frontend-routes.json",
        "performance candidate",
    )
    candidate_file = require_regular_file(candidate_path, "performance candidate")
    candidate = parse_json(candidate_file.read_bytes(), "performance candidate")
    expected = assemble_candidate(repository, raw_root)
    if candidate != expected:
        raise PerformanceEvidenceError(
            "performance candidate does not match recomputed raw evidence"
        )
    return candidate


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    seal = subparsers.add_parser("seal-source")
    seal.add_argument("--repo", type=Path, default=ROOT)
    seal.add_argument("--output", type=Path, default=RAW_ROOT / SOURCE_MANIFEST_NAME)
    assemble = subparsers.add_parser("assemble")
    assemble.add_argument("--repo", type=Path, default=ROOT)
    assemble.add_argument("--raw-root", type=Path, default=RAW_ROOT)
    assemble.add_argument("--output", type=Path, default=DEFAULT_CANDIDATE)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        if args.command == "seal-source":
            manifest = seal_source(args.repo, args.output)
            summary = {
                "status": "sealed",
                "sourceStateSha256": manifest["sourceStateSha256"],
                "output": str(_absolute(args.output)),
            }
        else:
            candidate = write_candidate(args.repo, args.raw_root, args.output)
            summary = {
                "status": "assembled",
                "sourceStateSha256": candidate["source"]["sourceStateSha256"],
                "output": str(_absolute(args.output)),
            }
        print(json.dumps(summary, sort_keys=True))
        return 0
    except (OSError, PerformanceEvidenceError, RuntimeError) as exc:
        print(f"performance evidence blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
