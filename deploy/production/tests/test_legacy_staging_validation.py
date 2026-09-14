from __future__ import annotations

import importlib.util
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
PATH = ROOT / "deploy/production/scripts/validate_legacy_staging.py"
SPEC = importlib.util.spec_from_file_location("validate_legacy_staging", PATH)
assert SPEC and SPEC.loader
validation = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(validation)


def test_staging_validation_covers_data_integrity_security_and_files() -> None:
    source = (
        ROOT / "deploy/production/scripts/validate_legacy_staging.py"
    ).read_text(encoding="utf-8")
    for check in (
        "orphanForeignKeyRowCount",
        "duplicateConstrainedKeyRowCount",
        "migrationModelMatch",
        "migrationIds",
        "invalidIndexCount",
        "unvalidatedConstraintCount",
        "duplicatePhoneCount",
        "invalidPasswordHashCount",
        "resetTableCounts",
        "roleAssignments",
        "providerCounts",
        "unsupportedProviderCount",
        "userWithoutRoleCount",
        "replayRiskCounts",
        "tableCountsSha256",
        "stagingFileTreeSha256",
        "restoreEvidenceSha256",
        "sourceCapture",
        "missingLocalReferences",
        "missingReferenceSha256",
        "discoveredColumnCount",
        "externalProviderReferences",
        "all-public-text-url-uri-path-columns",
    ):
        assert check in source
    assert "is_symlink" in source
    assert "PasswordHash" in source
    assert "select * from users" not in source.lower()


def test_reference_classification_separates_provider_and_local_paths() -> None:
    assert validation.is_external_provider_reference(
        ("teacher_profiles", "FacebookUrl"),
        "https://massar-academy.net/profile",
    )
    assert validation.is_external_provider_reference(
        ("lesson_resources", "FileUrl"),
        "https://drive.google.com/file/123",
    )
    assert not validation.is_external_provider_reference(
        ("lesson_resources", "FileUrl"),
        "https://assets.massar-academy.net/assets/book.pdf",
    )
    assert not validation.is_external_provider_reference(
        ("task_comments", "AttachmentUrl"),
        "uploads/tasks/proof.png",
    )
