#!/usr/bin/env python3
"""Production PostgreSQL schema and forbidden-bootstrap audit."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlsplit


FORBIDDEN_IDS = (
    "d36c2e35-512c-497b-b8c7-43df9ac3b123",
    "c4b82937-293e-48a3-a002-decf9a1efab8",
    "b4b82937-293e-48a3-a002-decf9a1efab8",
    "d9b8a342-990a-4286-905e-fdebb2e3895e",
)
ROOT = Path(__file__).resolve().parents[3]


def connection_environment() -> dict[str, str]:
    connection = os.environ.get("DATABASE_URL")
    if connection:
        parsed = urlsplit(connection)
        if parsed.scheme not in {"postgres", "postgresql"}:
            raise RuntimeError("DATABASE_URL must be a PostgreSQL URL")
        if not parsed.hostname or not parsed.path.strip("/") or not parsed.username:
            raise RuntimeError("DATABASE_URL is incomplete")
        query_options = parse_qs(parsed.query)
        values = {
            "PGHOST": parsed.hostname,
            "PGPORT": str(parsed.port or 5432),
            "PGDATABASE": parsed.path.lstrip("/"),
            "PGUSER": unquote(parsed.username),
            "PGPASSWORD": unquote(parsed.password or ""),
        }
        if "sslmode" in query_options:
            values["PGSSLMODE"] = query_options["sslmode"][-1]
        return values

    dotnet = os.environ.get("ConnectionStrings__DefaultConnection")
    if not dotnet:
        raise RuntimeError("DATABASE_URL or ConnectionStrings__DefaultConnection reference is required")
    pairs = {}
    for item in dotnet.split(";"):
        if not item.strip():
            continue
        key, separator, value = item.partition("=")
        if not separator:
            raise RuntimeError("DefaultConnection contains an invalid segment")
        pairs[key.strip().lower()] = value.strip()
    required = ("host", "database", "username")
    if any(not pairs.get(key) for key in required):
        raise RuntimeError("DefaultConnection is incomplete")
    return {
        "PGHOST": pairs["host"],
        "PGPORT": pairs.get("port", "5432"),
        "PGDATABASE": pairs["database"],
        "PGUSER": pairs["username"],
        "PGPASSWORD": pairs.get("password", ""),
        **({"PGSSLMODE": pairs["ssl mode"]} if pairs.get("ssl mode") else {}),
    }


def psql(query: str) -> str:
    process_environment = {
        **os.environ,
        **connection_environment(),
        "PGCONNECT_TIMEOUT": "10",
    }
    executable = os.environ.get("PSQL_BIN") or shutil.which("psql")
    if not executable:
        homebrew = Path("/opt/homebrew/opt/libpq/bin/psql")
        executable = str(homebrew) if homebrew.is_file() else None
    if not executable:
        raise RuntimeError("psql client is required for the database audit")
    completed = subprocess.run(
        [
            executable,
            "--no-psqlrc",
            "--set",
            "ON_ERROR_STOP=1",
            "--tuples-only",
            "--no-align",
            "--command",
            query,
        ],
        text=True,
        capture_output=True,
        check=False,
        env=process_environment,
    )
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr.strip() or "psql audit failed")
    return completed.stdout.strip()


def scalar(query: str) -> int:
    lines = psql(query).splitlines()
    return int(lines[-1]) if lines else 0


def fingerprint(query: str) -> str:
    return hashlib.sha256(psql(query).encode("utf-8")).hexdigest()


def orphan_foreign_key_rows() -> int:
    return scalar(r"""
        CREATE TEMP TABLE massar_orphan_audit ("Count" bigint) ON COMMIT DROP;
        DO $audit$
        DECLARE
            foreign_key record;
            join_predicate text;
            non_null_predicate text;
            orphan_count bigint;
        BEGIN
            FOR foreign_key IN
                SELECT oid, conrelid, confrelid, conkey, confkey
                FROM pg_constraint
                WHERE contype='f' AND connamespace='public'::regnamespace
            LOOP
                SELECT
                    string_agg(format('child.%I = parent.%I', child.attname, parent.attname), ' AND ' ORDER BY key_pair.ordinality),
                    string_agg(format('child.%I IS NOT NULL', child.attname), ' AND ' ORDER BY key_pair.ordinality)
                INTO join_predicate, non_null_predicate
                FROM unnest(foreign_key.conkey, foreign_key.confkey)
                    WITH ORDINALITY AS key_pair(child_number, parent_number, ordinality)
                JOIN pg_attribute child
                  ON child.attrelid=foreign_key.conrelid AND child.attnum=key_pair.child_number
                JOIN pg_attribute parent
                  ON parent.attrelid=foreign_key.confrelid AND parent.attnum=key_pair.parent_number;

                EXECUTE format(
                    'SELECT count(*) FROM %s child WHERE (%s) AND NOT EXISTS (SELECT 1 FROM %s parent WHERE %s)',
                    foreign_key.conrelid::regclass,
                    non_null_predicate,
                    foreign_key.confrelid::regclass,
                    join_predicate
                ) INTO orphan_count;
                INSERT INTO massar_orphan_audit VALUES (orphan_count);
            END LOOP;
        END
        $audit$;
        SELECT coalesce(sum("Count"), 0) FROM massar_orphan_audit;
    """)


def duplicate_constrained_key_rows() -> int:
    return scalar(r"""
        CREATE TEMP TABLE massar_duplicate_key_audit ("Count" bigint) ON COMMIT DROP;
        DO $audit$
        DECLARE
            key_constraint record;
            key_columns text;
            non_null_predicate text;
            duplicate_count bigint;
        BEGIN
            FOR key_constraint IN
                SELECT conrelid, conkey
                FROM pg_constraint
                WHERE contype IN ('p','u') AND connamespace='public'::regnamespace
            LOOP
                SELECT
                    string_agg(format('%I', attribute.attname), ', ' ORDER BY key_column.ordinality),
                    string_agg(format('%I IS NOT NULL', attribute.attname), ' AND ' ORDER BY key_column.ordinality)
                INTO key_columns, non_null_predicate
                FROM unnest(key_constraint.conkey)
                    WITH ORDINALITY AS key_column(attribute_number, ordinality)
                JOIN pg_attribute attribute
                  ON attribute.attrelid=key_constraint.conrelid
                 AND attribute.attnum=key_column.attribute_number;

                EXECUTE format(
                    'SELECT coalesce(sum(row_count - 1), 0) FROM (SELECT count(*) AS row_count FROM %s WHERE %s GROUP BY %s HAVING count(*) > 1) duplicates',
                    key_constraint.conrelid::regclass,
                    non_null_predicate,
                    key_columns
                ) INTO duplicate_count;
                INSERT INTO massar_duplicate_key_audit VALUES (duplicate_count);
            END LOOP;
        END
        $audit$;
        SELECT coalesce(sum("Count"), 0) FROM massar_duplicate_key_audit;
    """)


def latest_model_migration() -> str:
    migrations = sorted(
        path.stem
        for path in (
            ROOT / "backend/src/NaderGorge.Infrastructure/Migrations"
        ).glob("[0-9]*_*.cs")
        if not path.name.endswith(".Designer.cs")
    )
    if not migrations:
        raise RuntimeError("no EF migrations were found in the repository")
    return migrations[-1]


def collect() -> dict[str, object]:
    migration_ids = [line for line in psql('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";').splitlines() if line]
    model_migration = latest_model_migration()
    relations = scalar("SELECT count(*) FROM pg_class WHERE relkind IN ('r','p') AND relnamespace = 'public'::regnamespace;")
    columns = scalar("SELECT count(*) FROM information_schema.columns WHERE table_schema='public';")
    indexes = scalar("SELECT count(*) FROM pg_indexes WHERE schemaname='public';")
    constraints = scalar("SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace;")
    primary_keys = scalar("SELECT count(*) FROM pg_constraint WHERE contype='p' AND connamespace='public'::regnamespace;")
    foreign_keys = scalar("SELECT count(*) FROM pg_constraint WHERE contype='f' AND connamespace='public'::regnamespace;")
    check_constraints = scalar("SELECT count(*) FROM pg_constraint WHERE contype='c' AND connamespace='public'::regnamespace;")
    unique_constraints = scalar("SELECT count(*) FROM pg_constraint WHERE contype='u' AND connamespace='public'::regnamespace;")
    invalid_indexes = scalar("SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid WHERE c.relnamespace='public'::regnamespace AND NOT i.indisvalid;")
    unvalidated_constraints = scalar("SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND NOT convalidated;")
    tables_without_pk = scalar("""
        SELECT count(*) FROM pg_class table_class
        WHERE table_class.relkind IN ('r','p')
          AND table_class.relnamespace='public'::regnamespace
          AND table_class.relname <> '__EFMigrationsHistory'
          AND NOT EXISTS (
              SELECT 1 FROM pg_constraint constraint_row
              WHERE constraint_row.conrelid=table_class.oid
                AND constraint_row.contype='p'
          );
    """)
    duplicate_indexes = scalar("""
        SELECT count(*) FROM (
            SELECT indrelid, indkey, indexprs, indpred, count(*)
            FROM pg_index
            GROUP BY indrelid, indkey, indexprs, indpred
            HAVING count(*) > 1
        ) duplicate;
    """)
    ownership_mismatches = scalar("""
        SELECT count(*) FROM pg_class relation
        WHERE relation.relnamespace='public'::regnamespace
          AND relation.relkind IN ('r','p','S')
          AND pg_get_userbyid(relation.relowner) <>
              pg_get_userbyid((SELECT datdba FROM pg_database WHERE datname=current_database()));
    """)
    forbidden_users = scalar(
        "SELECT count(*) FROM users WHERE \"Id\"::text IN "
        f"({','.join(repr(value) for value in FORBIDDEN_IDS[:2])}) "
        "OR \"PhoneNumber\" IN ('__legacy_teacher__');"
    )
    forbidden_profiles = scalar(
        f"SELECT count(*) FROM teacher_profiles WHERE \"Id\"::text={FORBIDDEN_IDS[2]!r};"
    )
    forbidden_subjects = scalar(
        f"SELECT count(*) FROM subjects WHERE \"Id\"::text={FORBIDDEN_IDS[3]!r} OR \"NormalizedName\"='__legacy_subject__';"
    )
    orphan_rows = orphan_foreign_key_rows()
    duplicate_key_rows = duplicate_constrained_key_rows()
    column_fingerprint = fingerprint("""
        SELECT table_name || '|' || ordinal_position || '|' || column_name || '|' ||
               data_type || '|' || is_nullable || '|' || coalesce(column_default, '')
        FROM information_schema.columns
        WHERE table_schema='public'
        ORDER BY table_name, ordinal_position;
    """)
    index_fingerprint = fingerprint("""
        SELECT schemaname || '|' || tablename || '|' || indexname || '|' || indexdef
        FROM pg_indexes
        WHERE schemaname='public'
        ORDER BY tablename, indexname;
    """)
    migration_mismatch = int(not migration_ids or migration_ids[-1] != model_migration)
    critical = (
        invalid_indexes
        + unvalidated_constraints
        + tables_without_pk
        + duplicate_indexes
        + ownership_mismatches
        + forbidden_users
        + forbidden_profiles
        + forbidden_subjects
        + orphan_rows
        + duplicate_key_rows
        + migration_mismatch
    )
    return {
        "migrationIds": migration_ids,
        "expectedModelMigration": model_migration,
        "migrationModelMatch": migration_mismatch == 0,
        "relationCount": relations,
        "columnCount": columns,
        "indexCount": indexes,
        "constraintCount": constraints,
        "primaryKeyCount": primary_keys,
        "foreignKeyCount": foreign_keys,
        "checkConstraintCount": check_constraints,
        "uniqueConstraintCount": unique_constraints,
        "invalidIndexCount": invalid_indexes,
        "unvalidatedConstraintCount": unvalidated_constraints,
        "tableWithoutPrimaryKeyCount": tables_without_pk,
        "duplicateIndexDefinitionCount": duplicate_indexes,
        "ownershipMismatchCount": ownership_mismatches,
        "orphanForeignKeyRowCount": orphan_rows,
        "duplicateConstrainedKeyRowCount": duplicate_key_rows,
        "columnDefinitionSha256": column_fingerprint,
        "indexDefinitionSha256": index_fingerprint,
        "extensions": [line for line in psql(
            "SELECT extname FROM pg_extension ORDER BY extname;"
        ).splitlines() if line],
        "forbiddenBootstrapRowCount": forbidden_users + forbidden_profiles + forbidden_subjects,
        "criticalFindings": critical,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = collect()
    except RuntimeError as exc:
        print(f"database audit failed: {exc}", file=sys.stderr)
        return 6
    text = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
    else:
        print(text, end="")
    return 0 if result["criticalFindings"] == 0 else 6


if __name__ == "__main__":
    raise SystemExit(main())
