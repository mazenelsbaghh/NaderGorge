from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"


def source(name: str) -> str:
    return (SCRIPTS / name).read_text(encoding="utf-8")


def test_pgbackrest_backup_records_the_only_new_real_label() -> None:
    backup = source("backup_database.sh")
    assert "massar-pgbackrest-before" in backup
    assert "massar-pgbackrest-after" in backup
    assert 'if label not in before' in backup
    assert "len(created)!=1" in backup
    assert '"backupLabel":backup_label' in backup
    assert '"repositoryInfoSha256":repository_info_sha256' in backup
    assert '"clusterId":"massar-production"' in backup
    assert 'database-$backup_label.json' in backup
    assert 'ln -- "$temporary" "$identity_evidence"' in backup


def test_database_restore_requires_and_restores_the_exact_backup_label() -> None:
    restore = source("restore_database_sample.sh")
    assert 'readonly BACKUP_EVIDENCE="/var/lib/massar/evidence/backup/database-latest.json"' in restore
    assert 'set(value)!=required' in restore
    assert '--set="$backup_label"' in restore
    assert '"backupEvidenceSha256":backup_evidence_sha256' in restore
    assert '"productionTarget":False' in restore
    assert 'database-$backup_label.json' in restore
    assert '"$backup_evidence_sha256"' in restore
    assert 'identity_evidence="$EVIDENCE_DIR/database-$backup_label.json"' in restore
    assert "cleanup\ntrap - EXIT\ndestroyed_at=" in restore


def test_restic_backup_records_full_snapshot_id_from_json_summary() -> None:
    backup = source("backup_files.sh")
    assert "restic backup \\" in backup
    assert "--json" in backup
    assert 'value.get("message_type")=="summary"' in backup
    assert 're.fullmatch(r"[0-9a-f]{64}",snapshot)' in backup
    assert '"snapshotId":snapshot_id' in backup
    assert '"backupSummarySha256":backup_summary_sha256' in backup
    assert 'file-backup-$snapshot_id.json' in backup
    assert 'ln -- "$temporary" "$identity_evidence"' in backup


def test_file_restore_uses_evidence_snapshot_not_latest() -> None:
    restore = source("restore_files_sample.sh")
    assert 'readonly BACKUP_EVIDENCE="/srv/massar-shared/.cluster-health/file-backup-latest.json"' in restore
    assert 'restic snapshots --json "$snapshot_id"' in restore
    assert 'restic restore "$snapshot_id"' in restore
    assert "restic restore latest" not in restore
    assert '"backupEvidenceSha256":backup_evidence_sha256' in restore
    assert '"snapshotMetadataSha256":snapshot_metadata_sha256' in restore
    assert 'file-backup-$snapshot_id.json' in restore
    assert 'identity_evidence="$evidence_dir/files-$snapshot_id.json"' in restore
    assert "cleanup\ntrap - EXIT\ndestroyed_at=" in restore


def test_all_identity_evidence_is_release_cluster_and_time_bound() -> None:
    for name in (
        "backup_database.sh",
        "restore_database_sample.sh",
        "backup_files.sh",
        "restore_files_sample.sh",
    ):
        script = source(name)
        assert '"clusterId":"massar-production"' in script
        assert '"releaseId":release_id' in script
        assert '"startedAt":started' in script
        assert '"completedAt":completed' in script
        assert '"capturedAt":captured' in script
