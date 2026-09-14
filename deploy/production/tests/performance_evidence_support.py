from __future__ import annotations

import json
import subprocess
from pathlib import Path
from types import ModuleType


SOURCE_BINDING_KEYS = (
    "releaseId",
    "gitCommit",
    "sourceStateSha256",
    "dirtySourceSnapshot",
    "sourceDigestAlgorithm",
)


def initialize_repository(root: Path) -> Path:
    repository = root / "repository"
    repository.mkdir()
    subprocess.run(["git", "init", "-q", str(repository)], check=True)
    subprocess.run(
        ["git", "-C", str(repository), "config", "user.email", "test@example.invalid"],
        check=True,
    )
    subprocess.run(
        ["git", "-C", str(repository), "config", "user.name", "Performance Test"],
        check=True,
    )
    (repository / "application.txt").write_text("measured source\n", encoding="utf-8")
    subprocess.run(["git", "-C", str(repository), "add", "application.txt"], check=True)
    subprocess.run(["git", "-C", str(repository), "commit", "-qm", "initial"], check=True)
    return repository


def write_json(path: Path, value: object) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    return path


def source_binding(manifest: dict[str, object], manifest_bytes: bytes, sha256_bytes) -> dict[str, object]:
    return {
        **{key: manifest[key] for key in SOURCE_BINDING_KEYS},
        "manifestSha256": sha256_bytes(manifest_bytes),
    }


def resource(path: str, size: int) -> dict[str, object]:
    resource_type = Path(path).suffix.removeprefix(".")
    return {
        "path": path,
        "type": resource_type,
        "bytes": size,
        "gzipBytes": size,
        "brotliBytes": size,
    }


def summary(*resources: dict[str, object]) -> dict[str, object]:
    ordered = sorted(resources, key=lambda entry: str(entry["path"]))
    return {
        "resourceCount": len(ordered),
        "bytes": sum(int(entry["bytes"]) for entry in ordered),
        "gzipBytes": sum(int(entry["gzipBytes"]) for entry in ordered),
        "brotliBytes": sum(int(entry["brotliBytes"]) for entry in ordered),
        "resources": ordered,
    }


def route_evidence(binding: dict[str, object], build_id: str = "build-id-1") -> dict[str, object]:
    shared = resource("static/chunks/shared.js", 10)
    routes: dict[str, object] = {}
    for index, (name, pathname) in enumerate(
        (("login", "/login"), ("register", "/register"), ("student", "/student"), ("admin", "/admin")),
        start=1,
    ):
        initial = resource(f"static/chunks/{name}.js", index)
        deferred = resource(f"static/chunks/{name}.css", index + 10)
        routes[name] = {
            "pathname": pathname,
            "manifestKey": f"/{name}/page",
            "initial": summary(initial),
            "shared": summary(shared),
            "deferred": summary(deferred),
            "total": summary(initial, shared, deferred),
        }
    return {
        "schemaVersion": 1,
        "evidenceType": "route-resource-measurement",
        "generatedAt": "2026-08-24T00:00:00Z",
        "source": binding,
        "platform": {
            "operatingSystem": "test",
            "architecture": "test",
            "nodeVersion": "v22.13.0",
        },
        "measurement": {
            "source": "test production build",
            "productionBuildExecuted": True,
            "buildStartedAt": "2026-08-24T00:00:00Z",
            "buildId": build_id,
            "compression": {
                "gzipLevel": 9,
                "brotliQuality": 11,
                "note": "fixed",
            },
            "classification": {
                "shared": "shared",
                "initial": "initial",
                "deferred": "deferred",
            },
        },
        "shared": summary(shared),
        "routes": routes,
    }


def browser_evidence(
    binding: dict[str, object],
    build_id: str = "build-id-1",
    duplicate_count: int = 0,
) -> dict[str, object]:
    routes = {}
    for name, pathname in (("login", "/login"), ("register", "/register"), ("student", "/student")):
        samples = []
        for sequence in range(1, 21):
            samples.append(
                {
                    "sequence": sequence,
                    "warmNavigationMs": sequence * 10,
                    "eligibleReads": [
                        {
                            "identitySha256": "a" * 64,
                            "category": "api-read",
                            "count": duplicate_count + 1,
                        }
                    ],
                }
            )
        routes[name] = {"pathname": pathname, "samples": samples}
    return {
        "schemaVersion": 1,
        "evidenceType": "browser-performance-samples",
        "source": binding,
        "profile": {
            "name": "Pixel 5 / Android Chromium",
            "browserName": "chromium",
            "viewport": {"width": 393, "height": 851},
            "productionServer": True,
            "buildId": build_id,
        },
        "sampling": {
            "warmupCount": 3,
            "measuredCount": 20,
            "quietWindowMs": 250,
            "quietTimeoutMs": 2_000,
            "percentileMethod": "nearest-rank",
        },
        "routes": routes,
    }


def query_evidence(binding: dict[str, object], maximum_override: int | None = None) -> dict[str, object]:
    measurements = []
    for row_count in (1, 20, 100):
        measurements.append(
            {
                "rowCount": row_count,
                "dashboard": {"databaseCommands": 3, "returnedRows": row_count},
                "history": {"databaseCommands": 4, "returnedRows": row_count},
                "timeline": {"databaseCommands": 5, "returnedRows": row_count * 2},
            }
        )
    return {
        "schemaVersion": 1,
        "evidenceType": "live-support-query-budget",
        "source": binding,
        "database": {
            "databaseName": "massar_live_support_query_budget_disposable_test",
            "identitySha256": "b" * 64,
            "serverVersion": "16.4",
            "serverVersionNumber": 160004,
        },
        "rowCounts": [1, 20, 100],
        "measurements": measurements,
        "workflows": {
            "live-support-admin": {
                "maximumDatabaseCommandsObserved": 5 if maximum_override is None else maximum_override,
            }
        },
    }


def create_raw_evidence(
    assembler: ModuleType,
    repository: Path,
    *,
    duplicate_count: int = 0,
) -> tuple[Path, Path]:
    raw_root = repository / "artifacts/performance-167/final/raw"
    manifest_path = raw_root / assembler.SOURCE_MANIFEST_NAME
    manifest = assembler.seal_source(repository, manifest_path)
    manifest_bytes = manifest_path.read_bytes()
    binding = source_binding(manifest, manifest_bytes, assembler.sha256_bytes)
    write_json(raw_root / assembler.ROUTE_EVIDENCE_NAME, route_evidence(binding))
    write_json(
        raw_root / assembler.BROWSER_EVIDENCE_NAME,
        browser_evidence(binding, duplicate_count=duplicate_count),
    )
    write_json(raw_root / assembler.QUERY_EVIDENCE_NAME, query_evidence(binding))
    candidate_path = repository / "artifacts/performance-167/final/frontend-routes.json"
    assembler.write_candidate(repository, raw_root, candidate_path)
    return raw_root, candidate_path
