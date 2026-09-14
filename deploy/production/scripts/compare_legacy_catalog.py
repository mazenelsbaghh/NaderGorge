#!/usr/bin/env python3
"""Produce a PII-safe legacy-to-Production reconciliation decision report."""

from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path


RESET_ON_IMPORT = {
    "VideoPlaybackSessions",
    "cluster_leases",
}
DURABLE_PRESERVE = {
    "refresh_tokens",
    "devices",
    "ParentDeviceTokens",
    "outbox_events",
    "web_vitals_metrics",
    "hr_idempotency_records",
}
SENSITIVE_TOKENS = ("audit", "log", "message", "payment", "balance", "payroll")
REFERENCE_TABLES = {
    "roles",
    "subjects",
    "video_types",
    "PlatformSettings",
    "academic_subject_eligibilities",
    "student_facing_academic_scopes",
}
CONTENT_TABLES = {
    "teacher_profiles",
    "teacher_accounts",
    "teacher_subjects",
    "teacher_photos",
    "packages",
    "terms",
    "content_sections",
    "lessons",
    "lesson_videos",
    "video_chapters",
    "lesson_resources",
    "exams",
    "exam_questions",
    "question_bank_items",
    "question_options",
    "homeworks",
    "homework_questions",
    "shared_teacher_packages",
    "shared_teacher_package_teachers",
    "shared_teacher_package_items",
    "public_exam_products",
}


def classification(table: str) -> str:
    lowered = table.lower()
    if table in RESET_ON_IMPORT:
        return "RESET_ON_IMPORT"
    if table in DURABLE_PRESERVE:
        return "DURABLE_PRESERVE"
    if any(token in lowered for token in SENSITIVE_TOKENS):
        return "SENSITIVE_PRESERVE_REVIEW"
    if table in REFERENCE_TABLES:
        return "REFERENCE_REVIEW"
    if table in CONTENT_TABLES:
        return "CONTENT_REVIEW"
    return "RELATIONAL_DATA_REVIEW"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--legacy", required=True, type=Path)
    parser.add_argument("--production", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    legacy = json.loads(args.legacy.read_text(encoding="utf-8"))
    production = json.loads(args.production.read_text(encoding="utf-8"))
    legacy_databases = [
        value for value in legacy.get("databases", [])
        if value.get("accessible") and value.get("database") == "massar_platform"
    ]
    if len(legacy_databases) != 1:
        raise ValueError("expected exactly one accessible legacy massar_platform database")
    legacy_database = legacy_databases[0]
    old_counts = legacy_database["tableCounts"]
    new_counts = production["tableCounts"]
    rows = []
    for table in sorted(set(old_counts) | set(new_counts)):
        old = int(old_counts.get(table, 0))
        new = int(new_counts.get(table, 0))
        rows.append({
            "table": table,
            "legacyRows": old,
            "productionRows": new,
            "delta": old - new,
            "classification": classification(table),
            "candidateAction": (
                "NONE"
                if old == 0
                else "RESET"
                if classification(table) == "RESET_ON_IMPORT"
                else "PRESERVE"
                if classification(table) == "DURABLE_PRESERVE"
                else "MIGRATE_IN_STAGING"
            ),
        })
    missing_migrations = [
        migration
        for migration in production.get("migrationIds", [])
        if migration not in {
            value
            for value in legacy_database.get("migrationIds", [])
        }
    ]
    payload = {
        "schemaVersion": 1,
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "status": "STAGING_POLICY_APPROVED",
        "policyDecision": "PRESERVE_ALL_DURABLE_STATE",
        "policyVersion": 2,
        "directProductionImportAllowed": False,
        "legacyMigrationCount": legacy_database.get("migrationCount"),
        "legacyLatestMigration": legacy_database.get("latestMigration"),
        "productionMigrationCount": production.get("migrationCount"),
        "productionLatestMigration": production.get("latestMigration"),
        "schemaMatch": legacy_database.get("schemaSha256") == production.get("schemaSha256"),
        "productionMigrationsMissingFromLegacy": missing_migrations,
        "tables": rows,
        "summary": {
            kind: sum(1 for row in rows if row["classification"] == kind and row["legacyRows"] > 0)
            for kind in (
                "RESET_ON_IMPORT",
                "DURABLE_PRESERVE",
                "REFERENCE_REVIEW",
                "CONTENT_REVIEW",
                "SENSITIVE_PRESERVE_REVIEW",
                "RELATIONAL_DATA_REVIEW",
            )
        },
        "requiredPath": [
            "encrypted source backup",
            "isolated PostgreSQL 16 staging restore",
            "apply current migrations in staging",
            "preserve all durable auth, device, outbox, telemetry and HR idempotency state",
            "reset only VideoPlaybackSessions and cluster_leases",
            "require pending outbox events to equal zero before candidate build",
            "foreign-key, uniqueness, login and file checks",
            "fresh Production backup",
            "bounded maintenance import with rollback evidence",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    args.output.chmod(0o640)
    print(json.dumps({
        "status": payload["status"],
        "schemaMatch": payload["schemaMatch"],
        "tableCount": len(rows),
        "output": str(args.output),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
