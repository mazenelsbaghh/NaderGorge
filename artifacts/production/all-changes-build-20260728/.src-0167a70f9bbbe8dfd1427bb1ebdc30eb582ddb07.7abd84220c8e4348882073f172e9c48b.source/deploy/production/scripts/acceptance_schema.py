#!/usr/bin/env python3
"""Dependency-free runtime validation for canonical pre-DNS evidence."""

from __future__ import annotations

import datetime as dt
import json
import math
import re
from pathlib import Path
from typing import Any


EVIDENCE_NAMES = (
    "release.json",
    "cluster-health.json",
    "database-backup.json",
    "database-restore.json",
    "file-backup.json",
    "file-restore.json",
    "load.json",
    "chaos.json",
    "security.json",
    "automated-tests.json",
    "manual-qa.json",
)
SCHEMA_ROOT = (
    Path(__file__).resolve().parents[1] / "evidence/schemas/acceptance"
)
SCHEMA_PATHS = {name: SCHEMA_ROOT / name for name in EVIDENCE_NAMES}


class SchemaError(ValueError):
    pass


def load_schema(name: str) -> dict[str, Any]:
    if name not in SCHEMA_PATHS:
        raise SchemaError(f"no acceptance schema registered for {name}")
    try:
        schema_document = json.loads(
            SCHEMA_PATHS[name].read_text(encoding="utf-8")
        )
    except (OSError, json.JSONDecodeError) as exc:
        raise SchemaError(f"cannot load schema for {name}") from exc
    if not isinstance(schema_document, dict):
        raise SchemaError(f"schema for {name} must be an object")
    return schema_document


def _is_type(instance: object, expected: str) -> bool:
    if expected == "object":
        return isinstance(instance, dict)
    if expected == "array":
        return isinstance(instance, list)
    if expected == "string":
        return isinstance(instance, str)
    if expected == "boolean":
        return isinstance(instance, bool)
    if expected == "integer":
        return isinstance(instance, int) and not isinstance(instance, bool)
    if expected == "number":
        return (
            isinstance(instance, (int, float))
            and not isinstance(instance, bool)
            and math.isfinite(float(instance))
        )
    if expected == "null":
        return instance is None
    raise SchemaError(f"unsupported schema type {expected}")


def _validate_format(instance: str, expected: str, path: str) -> None:
    if expected != "date-time":
        raise SchemaError(f"{path}: unsupported format {expected}")
    try:
        parsed = dt.datetime.fromisoformat(instance.replace("Z", "+00:00"))
    except ValueError as exc:
        raise SchemaError(f"{path}: invalid date-time") from exc
    if parsed.tzinfo is None:
        raise SchemaError(f"{path}: date-time must include a timezone")


def _validate_object(
    instance: dict[str, object],
    schema: dict[str, Any],
    path: str,
) -> None:
    properties = schema.get("properties", {})
    for required in schema.get("required", []):
        if required not in instance:
            raise SchemaError(f"{path}.{required}: required property missing")
    additional = schema.get("additionalProperties", True)
    for key, child in instance.items():
        if key in properties:
            validate(child, properties[key], f"{path}.{key}")
        elif additional is False:
            raise SchemaError(f"{path}.{key}: additional property forbidden")
        elif isinstance(additional, dict):
            validate(child, additional, f"{path}.{key}")
    if "minProperties" in schema and len(instance) < schema["minProperties"]:
        raise SchemaError(f"{path}: too few properties")
    if "maxProperties" in schema and len(instance) > schema["maxProperties"]:
        raise SchemaError(f"{path}: too many properties")


def _validate_array(
    instance: list[object],
    schema: dict[str, Any],
    path: str,
) -> None:
    if "minItems" in schema and len(instance) < schema["minItems"]:
        raise SchemaError(f"{path}: too few items")
    if "maxItems" in schema and len(instance) > schema["maxItems"]:
        raise SchemaError(f"{path}: too many items")
    if schema.get("uniqueItems"):
        serialized = [json.dumps(entry, sort_keys=True) for entry in instance]
        if len(serialized) != len(set(serialized)):
            raise SchemaError(f"{path}: duplicate items")
    if "items" in schema:
        for index, entry in enumerate(instance):
            validate(entry, schema["items"], f"{path}[{index}]")


def _validate_string(
    instance: str,
    schema: dict[str, Any],
    path: str,
) -> None:
    if len(instance) < schema.get("minLength", 0):
        raise SchemaError(f"{path}: string is too short")
    if "pattern" in schema and not re.fullmatch(schema["pattern"], instance):
        raise SchemaError(f"{path}: string does not match pattern")
    if "format" in schema:
        _validate_format(instance, schema["format"], path)


def _validate_number(
    instance: int | float,
    schema: dict[str, Any],
    path: str,
) -> None:
    numeric = float(instance)
    if "minimum" in schema and numeric < schema["minimum"]:
        raise SchemaError(f"{path}: below minimum")
    if "maximum" in schema and numeric > schema["maximum"]:
        raise SchemaError(f"{path}: above maximum")
    if "exclusiveMinimum" in schema and numeric <= schema["exclusiveMinimum"]:
        raise SchemaError(f"{path}: below exclusive minimum")
    if "exclusiveMaximum" in schema and numeric >= schema["exclusiveMaximum"]:
        raise SchemaError(f"{path}: above exclusive maximum")


def validate(instance: object, schema: dict[str, Any], path: str = "$") -> None:
    if "const" in schema and instance != schema["const"]:
        raise SchemaError(f"{path}: expected constant {schema['const']!r}")
    if "enum" in schema and instance not in schema["enum"]:
        raise SchemaError(f"{path}: value is outside the allowed enum")
    expected_type = schema.get("type")
    if expected_type is not None:
        candidates = (
            expected_type if isinstance(expected_type, list) else [expected_type]
        )
        if not any(_is_type(instance, candidate) for candidate in candidates):
            raise SchemaError(f"{path}: invalid type")

    if isinstance(instance, dict):
        _validate_object(instance, schema, path)
    elif isinstance(instance, list):
        _validate_array(instance, schema, path)
    elif isinstance(instance, str):
        _validate_string(instance, schema, path)
    elif _is_type(instance, "number"):
        _validate_number(instance, schema, path)


def validate_evidence(name: str, evidence: object) -> None:
    validate(evidence, load_schema(name), f"${name}")
