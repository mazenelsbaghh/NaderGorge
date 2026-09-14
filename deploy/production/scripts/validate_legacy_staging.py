#!/usr/bin/env python3
"""Deep integrity and file-reference validation for the isolated legacy stage."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import subprocess
from pathlib import Path
from urllib.parse import unquote, urlsplit


CONTAINER = "massar-legacy-stage-166"
MIGRATION_ATTRIBUTE = re.compile(r'\[Migration\("([^"]+)"\)\]')
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
RESTORE_ID_PATTERN = re.compile(r"^legacy-restore-[0-9a-f]{32}$")
SUPPORTED_VIDEO_PROVIDERS = frozenset({"youtube", "vk", "bunny"})
EXTERNAL_PROVIDER_COLUMNS = frozenset({
    ("media_production_pipelines", "AssetFolderUrl"),
    ("teacher_profiles", "FacebookUrl"),
    ("teacher_profiles", "IntroVideoUrl"),
    ("teacher_profiles", "TelegramUrl"),
    ("teacher_profiles", "YouTubeUrl"),
    ("web_vitals_metrics", "PageUrl"),
})


def psql(query: str) -> str:
    completed = subprocess.run(
        [
            "docker", "exec", CONTAINER,
            "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
            "-U", "postgres", "-d", "massar_platform", "-c", query,
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    if completed.returncode:
        raise RuntimeError(completed.stderr.strip() or "staging SQL audit failed")
    return completed.stdout.strip()


def scalar(query: str) -> int:
    value = psql(query).splitlines()
    return int(value[-1]) if value else 0


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_restore_evidence(path: Path, backup: Path) -> tuple[dict[str, object], str]:
    expanded = path.expanduser().resolve()
    if expanded.is_symlink() or not expanded.is_file():
        raise RuntimeError("restore evidence must be a regular file")
    try:
        value = json.loads(expanded.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise RuntimeError("restore evidence is invalid") from exc
    source = value.get("sourceCapture")
    restore_id = value.get("restoreId")
    if (
        value.get("schemaVersion") != 1
        or value.get("status") != "success"
        or value.get("isolated") is not True
        or not isinstance(restore_id, str)
        or not RESTORE_ID_PATTERN.fullmatch(restore_id)
        or not isinstance(source, dict)
        or source.get("backupId") != value.get("backupId")
        or not isinstance(source.get("sourceHost"), str)
        or not source.get("sourceHost")
        or not isinstance(source.get("sourceUser"), str)
        or not source.get("sourceUser")
        or source.get("sourceMode")
        not in {"read-only", "frozen-writers", "frozen-writers-held"}
        or source.get("authoritativeSource") is not (
            source.get("sourceMode") == "frozen-writers-held"
        )
        or source.get("writersFrozenAtCompletion")
        is not source.get("authoritativeSource")
        or not isinstance(source.get("manifestSha256"), str)
        or not SHA256_PATTERN.fullmatch(str(source.get("manifestSha256")))
        or not isinstance(source.get("captureEvidenceSha256"), str)
        or not SHA256_PATTERN.fullmatch(str(source.get("captureEvidenceSha256")))
        or not isinstance(source.get("artifactSha256"), dict)
    ):
        raise RuntimeError("restore evidence does not prove a verified source capture")
    manifest = backup / "manifest.json"
    capture_evidence = backup / "capture-evidence.json"
    if (
        manifest.is_symlink()
        or capture_evidence.is_symlink()
        or not manifest.is_file()
        or not capture_evidence.is_file()
        or sha256_file(manifest) != source["manifestSha256"]
        or sha256_file(capture_evidence) != source["captureEvidenceSha256"]
    ):
        raise RuntimeError("restore evidence source digests do not match the backup")
    manifest_value = json.loads(manifest.read_text(encoding="utf-8"))
    entries = manifest_value.get("artifacts")
    if (
        manifest_value.get("backupId") != value.get("backupId")
        or not isinstance(entries, dict)
        or {
            name: entry.get("sha256")
            for name, entry in entries.items()
            if isinstance(entry, dict)
        }
        != source["artifactSha256"]
    ):
        raise RuntimeError("restore evidence artifact digests do not match the backup")
    inspected_id = subprocess.run(
        [
            "docker", "inspect", "--format",
            "{{index .Config.Labels \"massar.legacy.restore-id\"}}",
            CONTAINER,
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    if inspected_id.returncode or inspected_id.stdout.strip() != restore_id:
        raise RuntimeError("restore evidence does not identify the active staging container")
    return value, sha256_file(expanded)


def staging_file_tree_snapshot(backup: Path) -> tuple[int, str]:
    entries: list[dict[str, object]] = []
    roots = (
        ("assets", backup / "staging-files-assets"),
        ("protected", backup / "staging-files-protected"),
        ("appData", backup / "staging-files-app-data"),
    )
    for area, root in roots:
        for path in sorted(root.rglob("*")):
            if path.is_symlink():
                raise RuntimeError("staging file tree contains a symlink")
            if path.is_dir():
                continue
            if not path.is_file():
                raise RuntimeError("staging file tree contains a non-regular file")
            entries.append({
                "area": area,
                "path": path.relative_to(root).as_posix(),
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            })
    canonical = json.dumps(
        entries,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode()
    return len(entries), hashlib.sha256(canonical).hexdigest()


def orphan_rows() -> int:
    return scalar(r"""
CREATE TEMP TABLE orphan_audit (count bigint) ON COMMIT DROP;
DO $audit$
DECLARE foreign_key record; join_predicate text; non_null_predicate text; orphan_count bigint;
BEGIN
  FOR foreign_key IN
    SELECT conrelid, confrelid, conkey, confkey
    FROM pg_constraint WHERE contype='f' AND connamespace='public'::regnamespace
  LOOP
    SELECT
      string_agg(format('child.%I = parent.%I', child.attname, parent.attname), ' AND ' ORDER BY keys.ordinality),
      string_agg(format('child.%I IS NOT NULL', child.attname), ' AND ' ORDER BY keys.ordinality)
    INTO join_predicate, non_null_predicate
    FROM unnest(foreign_key.conkey, foreign_key.confkey)
      WITH ORDINALITY AS keys(child_number, parent_number, ordinality)
    JOIN pg_attribute child ON child.attrelid=foreign_key.conrelid AND child.attnum=keys.child_number
    JOIN pg_attribute parent ON parent.attrelid=foreign_key.confrelid AND parent.attnum=keys.parent_number;
    EXECUTE format(
      'SELECT count(*) FROM %s child WHERE (%s) AND NOT EXISTS (SELECT 1 FROM %s parent WHERE %s)',
      foreign_key.conrelid::regclass, non_null_predicate,
      foreign_key.confrelid::regclass, join_predicate
    ) INTO orphan_count;
    INSERT INTO orphan_audit VALUES (orphan_count);
  END LOOP;
END $audit$;
SELECT coalesce(sum(count),0) FROM orphan_audit;
""")


def duplicate_constrained_rows() -> int:
    return scalar(r"""
CREATE TEMP TABLE duplicate_audit (count bigint) ON COMMIT DROP;
DO $audit$
DECLARE item record; columns text; predicate text; duplicate_count bigint;
BEGIN
  FOR item IN
    SELECT conrelid, conkey FROM pg_constraint
    WHERE contype IN ('p','u') AND connamespace='public'::regnamespace
  LOOP
    SELECT
      string_agg(format('%I', attribute.attname), ', ' ORDER BY key.ordinality),
      string_agg(format('%I IS NOT NULL', attribute.attname), ' AND ' ORDER BY key.ordinality)
    INTO columns, predicate
    FROM unnest(item.conkey) WITH ORDINALITY AS key(number, ordinality)
    JOIN pg_attribute attribute ON attribute.attrelid=item.conrelid AND attribute.attnum=key.number;
    EXECUTE format(
      'SELECT coalesce(sum(rows - 1),0) FROM (SELECT count(*) rows FROM %s WHERE %s GROUP BY %s HAVING count(*)>1) value',
      item.conrelid::regclass, predicate, columns
    ) INTO duplicate_count;
    INSERT INTO duplicate_audit VALUES (duplicate_count);
  END LOOP;
END $audit$;
SELECT coalesce(sum(count),0) FROM duplicate_audit;
""")


def table_counts_snapshot() -> dict[str, int]:
    raw = psql(
        "select coalesce(json_object_agg(name,count order by name),'{}'::json) "
        "from (select c.relname name,"
        "(xpath('/row/count/text()',query_to_xml(format("
        "'select count(*) as count from %I',c.relname),false,true,''"
        ")))[1]::text::bigint count "
        "from pg_class c where c.relnamespace='public'::regnamespace "
        "and c.relkind in ('r','p')) value;"
    )
    try:
        parsed = json.loads(raw)
    except json.JSONDecodeError as exc:
        raise RuntimeError("staging table-count snapshot is invalid") from exc
    if not isinstance(parsed, dict) or any(
        not isinstance(name, str)
        or isinstance(count, bool)
        or not isinstance(count, int)
        or count < 0
        for name, count in parsed.items()
    ):
        raise RuntimeError("staging table-count snapshot is invalid")
    return dict(sorted(parsed.items()))


def local_reference_candidates(value: str, roots: list[Path]) -> list[Path]:
    parsed = urlsplit(value)
    if parsed.scheme and parsed.scheme not in {"http", "https"}:
        return []
    path = unquote(parsed.path or value).replace("\\", "/").lstrip("/")
    variants = {path}
    for prefix in (
        "assets/", "api/assets/", "uploads/", "wwwroot/",
        "app/App_Data/", "App_Data/", "protected/resources/",
    ):
        if path.startswith(prefix):
            variants.add(path[len(prefix):])
    return [root / variant for root in roots for variant in variants if variant]


def reference_columns() -> list[tuple[str, str]]:
    return [
        tuple(line.split("|", 1))
        for line in psql(
            "select table_name||'|'||column_name from information_schema.columns "
            "where table_schema='public' "
            "and data_type in ('text','character varying','character') "
            "and (column_name ~* '(url|uri|path)$' "
            "or column_name ~* '(file|image|audio|media|attachment|screenshot).*(url|path)') "
            "order by table_name,column_name;"
        ).splitlines()
        if "|" in line
    ]


def quoted_identifier(identifier: str) -> str:
    return '"' + identifier.replace('"', '""') + '"'


def reference_rows(table: str, column: str) -> list[tuple[str, bool]]:
    blocked = (
        'coalesce("IsBlocked",false)'
        if (table, column) == ("live_support_attachments", "StoragePath")
        else "false"
    )
    output = psql(
        "select json_build_object("
        f"'value',{quoted_identifier(column)},'blocked',{blocked})::text "
        f"from {quoted_identifier(table)} "
        f"where {quoted_identifier(column)} is not null "
        f"and btrim({quoted_identifier(column)})<>'';"
    )
    rows: list[tuple[str, bool]] = []
    for line in output.splitlines():
        reference = json.loads(line)
        rows.append((str(reference["value"]), reference["blocked"] is True))
    return rows


def is_external_provider_reference(source: tuple[str, str], value: str) -> bool:
    if source in EXTERNAL_PROVIDER_COLUMNS:
        return True
    parsed = urlsplit(value)
    if parsed.scheme not in {"", "http", "https"}:
        return True
    if parsed.scheme in {"http", "https"}:
        hostname = (parsed.hostname or "").lower()
        same_site = hostname == "massar-academy.net" or hostname.endswith(
            ".massar-academy.net"
        )
        local_path = any(
            token in parsed.path
            for token in ("/assets/", "/uploads/", "/api/assets/", "/protected/")
        )
        return not (same_site or local_path)
    return False


def file_reference_audit(assets: Path, protected: Path, app_data: Path) -> dict[str, object]:
    result = {
        "inventoryPolicy": "all-public-text-url-uri-path-columns",
        "discoveredColumnCount": 0,
        "columns": [],
        "totalReferences": 0,
        "externalProviderReferences": 0,
        "existingLocalReferences": 0,
        "missingLocalReferences": 0,
        "missingReferenceSha256": None,
        "missingBySource": {},
        "missingBasenameMatches": 0,
        "missingBlockedReferences": 0,
        "missingUnblockedReferences": 0,
    }
    roots = [assets, protected, app_data]
    missing: list[str] = []
    columns = reference_columns()
    result["discoveredColumnCount"] = len(columns)
    for table, column in columns:
        source = f"{table}.{column}"
        column_counts = {
            "source": source,
            "policy": (
                "external/provider"
                if (table, column) in EXTERNAL_PROVIDER_COLUMNS
                else "value-classified"
            ),
            "total": 0,
            "externalProvider": 0,
            "localExisting": 0,
            "localMissingBlocked": 0,
            "localMissingUnblocked": 0,
        }
        for value, blocked in reference_rows(table, column):
            result["totalReferences"] += 1
            column_counts["total"] += 1
            if is_external_provider_reference((table, column), value):
                result["externalProviderReferences"] += 1
                column_counts["externalProvider"] += 1
                continue
            candidates = local_reference_candidates(value, roots)
            if any(path.is_file() for path in candidates):
                result["existingLocalReferences"] += 1
                column_counts["localExisting"] += 1
                continue
            result["missingLocalReferences"] += 1
            missing.append(f"{source}|{value}")
            result["missingBySource"][source] = (
                result["missingBySource"].get(source, 0) + 1
            )
            if blocked:
                result["missingBlockedReferences"] += 1
                column_counts["localMissingBlocked"] += 1
            else:
                result["missingUnblockedReferences"] += 1
                column_counts["localMissingUnblocked"] += 1
            basename = Path(unquote(urlsplit(value).path or value)).name
            if basename and any(
                path.is_file() and path.name == basename
                for root in roots
                for path in root.rglob(basename)
            ):
                result["missingBasenameMatches"] += 1
        result["columns"].append(column_counts)
    if missing:
        result["missingReferenceSha256"] = hashlib.sha256(
            "\n".join(sorted(missing)).encode()
        ).hexdigest()
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--backup-dir", required=True, type=Path)
    parser.add_argument("--repository", required=True, type=Path)
    parser.add_argument("--restore-evidence", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    backup = args.backup_dir.expanduser().resolve()
    restore_evidence, restore_evidence_sha256 = load_restore_evidence(
        args.restore_evidence,
        backup,
    )
    app_data = backup / "staging-files-app-data"
    for required_root in (
        backup / "staging-files-assets",
        backup / "staging-files-protected",
        app_data,
    ):
        if not required_root.is_dir() or required_root.is_symlink():
            raise RuntimeError(
                "validated staging file roots from restore_legacy_staging.py are required"
            )
    migration_root = (
        args.repository.resolve()
        / "backend/src/NaderGorge.Infrastructure/Migrations"
    )
    model_migrations = sorted({
        match.group(1)
        for path in migration_root.glob("*.cs")
        for match in [MIGRATION_ATTRIBUTE.search(path.read_text(encoding="utf-8"))]
        if match
    })
    reset_counts = {}
    for table in (
        "VideoPlaybackSessions", "cluster_leases",
    ):
        if scalar(
            f"select count(*) from pg_tables where schemaname='public' and tablename={table!r};"
        ):
            reset_counts[table] = scalar(f'SELECT count(*) FROM "{table}";')
    roles = {
        line.split("|", 1)[0]: int(line.split("|", 1)[1])
        for line in psql(
            'select r."Name",count(*) from user_roles ur join roles r on r."Id"=ur."RoleId" group by r."Name" order by r."Name";'
        ).splitlines()
    }
    provider_counts = {
        line.split("|", 1)[0]: int(line.split("|", 1)[1])
        for line in psql(
            "select lower(trim(coalesce(\"Provider\",'missing'))),count(*) "
            'from lesson_videos group by lower(trim(coalesce("Provider",\'missing\'))) order by 1;'
        ).splitlines()
    }
    unsupported_provider_count = sum(
        count
        for provider, count in provider_counts.items()
        if provider not in SUPPORTED_VIDEO_PROVIDERS
    )
    database_migrations = [
        value
        for value in psql(
            'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";'
        ).splitlines()
        if value
    ]
    latest_database = database_migrations[-1] if database_migrations else None
    replay_risk_counts = {
        "pendingOutboxEvents": scalar(
            'select count(*) from outbox_events '
            'where "ProcessedAt" is null and not "IsDeadLetter";'
        ),
        "activePlaybackSessions": scalar(
            'select count(*) from "VideoPlaybackSessions";'
        ),
        "clusterLeases": scalar("select count(*) from cluster_leases;"),
    }
    table_counts = table_counts_snapshot()
    table_counts_sha256 = hashlib.sha256(
        json.dumps(
            table_counts,
            sort_keys=True,
            separators=(",", ":"),
        ).encode()
    ).hexdigest()
    staging_file_count, staging_file_tree_sha256 = staging_file_tree_snapshot(backup)
    payload = {
        "schemaVersion": 1,
        "backupId": restore_evidence["backupId"],
        "restoreId": restore_evidence["restoreId"],
        "restoreEvidenceSha256": restore_evidence_sha256,
        "sourceCapture": restore_evidence["sourceCapture"],
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "status": "success",
        "isolated": True,
        "latestModelMigration": model_migrations[-1] if model_migrations else None,
        "latestDatabaseMigration": latest_database,
        "migrationIds": database_migrations,
        "migrationModelCount": len(model_migrations),
        "migrationModelMatch": database_migrations == model_migrations,
        "orphanForeignKeyRowCount": orphan_rows(),
        "duplicateConstrainedKeyRowCount": duplicate_constrained_rows(),
        "invalidIndexCount": scalar("select count(*) from pg_index where not indisvalid;"),
        "unvalidatedConstraintCount": scalar(
            "select count(*) from pg_constraint where connamespace='public'::regnamespace and not convalidated;"
        ),
        "userCount": scalar("select count(*) from users;"),
        "userWithoutRoleCount": scalar(
            'select count(*) from users u where not exists (select 1 from user_roles ur where ur."UserId"=u."Id");'
        ),
        "duplicatePhoneCount": scalar(
            'select coalesce(sum(count-1),0) from (select count(*) from users group by "PhoneNumber" having count(*)>1) value;'
        ),
        "invalidPasswordHashCount": scalar(
            """select count(*) from users where coalesce("PasswordHash",'') !~ '^\\$2[aby]\\$';"""
        ),
        "roleAssignments": roles,
        "providerCounts": provider_counts,
        "unsupportedProviderCount": unsupported_provider_count,
        "resetTableCounts": reset_counts,
        "replayRiskCounts": replay_risk_counts,
        "tableCount": len(table_counts),
        "tableCounts": table_counts,
        "tableCountsSha256": table_counts_sha256,
        "stagingFileCount": staging_file_count,
        "stagingFileTreeSha256": staging_file_tree_sha256,
        "fileReferences": file_reference_audit(
            backup / "staging-files-assets",
            backup / "staging-files-protected",
            app_data,
        ),
    }
    critical = (
        int(not payload["migrationModelMatch"])
        + payload["orphanForeignKeyRowCount"]
        + payload["duplicateConstrainedKeyRowCount"]
        + payload["invalidIndexCount"]
        + payload["unvalidatedConstraintCount"]
        + payload["userWithoutRoleCount"]
        + payload["duplicatePhoneCount"]
        + payload["invalidPasswordHashCount"]
        + payload["unsupportedProviderCount"]
        + sum(reset_counts.values())
        + sum(replay_risk_counts.values())
        + payload["fileReferences"]["missingUnblockedReferences"]
    )
    payload["criticalFindingCount"] = critical
    payload["status"] = "success" if critical == 0 else "failed"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    args.output.chmod(0o640)
    print(json.dumps({
        "status": payload["status"],
        "criticalFindingCount": critical,
        "userCount": payload["userCount"],
        "missingLocalReferences": payload["fileReferences"]["missingLocalReferences"],
        "output": str(args.output),
    }))
    return 0 if critical == 0 else 6


if __name__ == "__main__":
    raise SystemExit(main())
