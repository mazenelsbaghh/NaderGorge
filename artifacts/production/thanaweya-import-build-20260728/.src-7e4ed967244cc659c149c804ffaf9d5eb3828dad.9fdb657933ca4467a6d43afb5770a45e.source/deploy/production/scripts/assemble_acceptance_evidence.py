#!/usr/bin/env python3
"""Assemble an atomic canonical pre-DNS evidence directory from raw JSON."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import shutil
import sys
import tempfile
from pathlib import Path

from acceptance_schema import (
    EVIDENCE_NAMES,
    SchemaError,
    load_schema,
    validate_evidence,
)
from accept_production import evaluate_values


class AssemblyError(RuntimeError):
    pass


def load_source_map(path: Path) -> dict[str, Path]:
    if not path.is_file() or path.is_symlink():
        raise AssemblyError("source map must be a regular JSON file")
    try:
        source_document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise AssemblyError("source map is not valid JSON") from exc
    if (
        not isinstance(source_document, dict)
        or set(source_document) != {"schemaVersion", "sources"}
    ):
        raise AssemblyError("source map must contain only schemaVersion and sources")
    if (
        source_document["schemaVersion"] != 1
        or not isinstance(source_document["sources"], dict)
    ):
        raise AssemblyError("source map has an invalid schemaVersion or sources object")
    if set(source_document["sources"]) != set(EVIDENCE_NAMES):
        missing = sorted(
            set(EVIDENCE_NAMES) - set(source_document["sources"])
        )
        extra = sorted(
            set(source_document["sources"]) - set(EVIDENCE_NAMES)
        )
        raise AssemblyError(f"source map mismatch missing={missing} extra={extra}")
    source_paths: dict[str, Path] = {}
    for name in EVIDENCE_NAMES:
        raw_path = source_document["sources"][name]
        if not isinstance(raw_path, str) or not raw_path:
            raise AssemblyError(f"{name} source path must be a non-empty string")
        candidate = Path(raw_path)
        source_paths[name] = (
            candidate if candidate.is_absolute() else path.parent / candidate
        )
    return source_paths


def canonicalize(name: str, source: Path) -> dict:
    if not source.is_file() or source.is_symlink():
        raise AssemblyError(f"{name} source must be a regular file")
    try:
        raw = source.read_bytes()
        raw_evidence = json.loads(raw)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise AssemblyError(f"{name} source is not valid JSON") from exc
    if not isinstance(raw_evidence, dict):
        raise AssemblyError(f"{name} source must contain a JSON object")
    properties = load_schema(name).get("properties", {})
    canonical = {
        key: raw_evidence[key]
        for key in properties
        if key != "sourceEvidenceSha256" and key in raw_evidence
    }
    canonical["sourceEvidenceSha256"] = hashlib.sha256(raw).hexdigest()
    try:
        validate_evidence(name, canonical)
    except SchemaError as exc:
        raise AssemblyError(f"{name} violates canonical schema: {exc}") from exc
    return canonical


def assemble(
    source_map: Path,
    output_dir: Path,
    now: dt.datetime | None = None,
) -> dict[str, str]:
    if output_dir.exists() or output_dir.is_symlink():
        raise AssemblyError("output directory already exists")
    sources = load_source_map(source_map)
    canonical = {
        name: canonicalize(name, sources[name])
        for name in EVIDENCE_NAMES
    }
    reasons = evaluate_values(
        canonical,
        [],
        now or dt.datetime.now(dt.timezone.utc),
    )
    if reasons:
        raise AssemblyError(
            "acceptance evidence rejected: " + ",".join(reasons)
        )

    output_dir.parent.mkdir(parents=True, exist_ok=True)
    temporary = Path(tempfile.mkdtemp(
        prefix=f".{output_dir.name}.",
        dir=output_dir.parent,
    ))
    digests: dict[str, str] = {}
    try:
        for name in EVIDENCE_NAMES:
            encoded = (
                json.dumps(canonical[name], indent=2, sort_keys=True) + "\n"
            ).encode("utf-8")
            destination = temporary / name
            destination.write_bytes(encoded)
            os.chmod(destination, 0o640)
            digests[name] = hashlib.sha256(encoded).hexdigest()
        os.chmod(temporary, 0o750)
        os.replace(temporary, output_dir)
    finally:
        if temporary.exists():
            shutil.rmtree(temporary)
    return digests


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-map", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    try:
        digests = assemble(args.source_map, args.output_dir)
    except (AssemblyError, OSError, SchemaError, ValueError) as exc:
        print(f"acceptance assembly blocked: {exc}", file=sys.stderr)
        return 6
    print(json.dumps({
        "status": "success",
        "fileCount": len(digests),
        "output": str(args.output_dir),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
