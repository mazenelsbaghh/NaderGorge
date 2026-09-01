from __future__ import annotations

import importlib.util
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[3]
MODULE_SPEC = importlib.util.spec_from_file_location(
    "build_app_env_bunny_legacy",
    ROOT / "deploy/production/scripts/build_app_env.py",
)
assert MODULE_SPEC and MODULE_SPEC.loader
build_app_env = importlib.util.module_from_spec(MODULE_SPEC)
MODULE_SPEC.loader.exec_module(build_app_env)


def yaml_block(source: str, header: str, next_header: str) -> str:
    match = re.search(
        rf"^{re.escape(header)}\n(.*?)(?=^{re.escape(next_header)}|\Z)",
        source,
        flags=re.MULTILINE | re.DOTALL,
    )
    assert match, f"could not find YAML block: {header}"
    return match.group(1)


def test_legacy_bunny_credentials_are_rendered_for_backend_migration_without_value_changes(
    tmp_path: Path,
) -> None:
    secrets = tmp_path / "secrets"
    secrets.mkdir()
    for name in (
        "postgres-app",
        "redis",
        "ai-callback",
        "ai-media-relay",
        "jwt",
        "api-callback",
        "worker-admin",
        "parent-signing",
    ):
        (secrets / name).write_text(f"{name}-secret", encoding="utf-8")

    source = {
        "GEMINI_API_KEY": "gemini-test-key",
        "BUNNY_STREAM_LIBRARY_ID": "740733",
        "BUNNY_STREAM_API_KEY": "bunny-test-key",
        "BUNNY_STREAM_TUS_UPLOAD_EXPIRY_MINUTES": "90",
    }

    rendered = dict(
        line.split("=", 1) for line in build_app_env.render(source, secrets)
    )

    assert rendered["BUNNY_STREAM_LIBRARY_ID"] == "740733"
    assert rendered["BUNNY_STREAM_API_KEY"] == "bunny-test-key"
    assert rendered["BunnyStream__LibraryId"] == "740733"
    assert rendered["BunnyStream__ApiKey"] == "bunny-test-key"
    assert rendered["BunnyStream__TusUploadExpiryMinutes"] == "90"
    assert rendered["AI_MEDIA_RELAY_SECRET"] == "ai-media-relay-secret"


def test_bunny_analysis_security_keys_remain_raw_for_compose_scoping(
    tmp_path: Path,
) -> None:
    """Compose injects this secret only into the backend configuration."""
    secrets = tmp_path / "secrets"
    secrets.mkdir()
    for name in (
        "postgres-app",
        "redis",
        "ai-callback",
        "ai-media-relay",
        "jwt",
        "api-callback",
        "worker-admin",
        "parent-signing",
    ):
        (secrets / name).write_text(f"{name}-secret", encoding="utf-8")

    rendered = dict(
        line.split("=", 1)
        for line in build_app_env.render(
            {
                "GEMINI_API_KEY": "gemini-test-key",
                "BUNNY_ANALYSIS_CDN_TOKEN_SECURITY_KEYS_JSON": '{"740733":"cdn-key"}',
                "BUNNY_ANALYSIS_PLAYER_TOKEN_SECURITY_KEYS_JSON": '{"740733":"player-key"}',
            },
            secrets,
        )
    )

    assert rendered["BUNNY_ANALYSIS_CDN_TOKEN_SECURITY_KEYS_JSON"] == '{"740733":"cdn-key"}'
    assert rendered["BUNNY_ANALYSIS_PLAYER_TOKEN_SECURITY_KEYS_JSON"] == '{"740733":"player-key"}'
    assert "BunnyAnalysis__CdnTokenSecurityKeysJson" not in rendered
    assert "BunnyAnalysis__PlayerTokenSecurityKeysJson" not in rendered


def test_production_compose_scopes_bunny_and_relay_secrets_to_backend_and_worker() -> None:
    source = (ROOT / "deploy/production/compose/compose.app.yml").read_text(encoding="utf-8")
    frontend = yaml_block(source, "x-frontend-internal-api: &frontend-internal-api", "services:")
    worker = yaml_block(source, "  worker:", "  landing:")
    gateway = yaml_block(source, "  gateway:", "networks:")
    backend = yaml_block(source, "  backend:", "  worker:")

    for block in (frontend, worker, gateway):
        assert 'BUNNY_STREAM_API_KEY: ""' in block
        assert 'BunnyStream__ApiKey: ""' in block
        assert 'BUNNY_ANALYSIS_CDN_TOKEN_SECURITY_KEYS_JSON: ""' in block
        assert 'BUNNY_ANALYSIS_PLAYER_TOKEN_SECURITY_KEYS_JSON: ""' in block

    assert 'AI_MEDIA_RELAY_SECRET: ""' in frontend
    assert 'AiMediaRelay__Secret: ""' in frontend
    assert 'AI_MEDIA_RELAY_SECRET: ${AI_MEDIA_RELAY_SECRET:?AI_MEDIA_RELAY_SECRET is required}' in worker
    assert 'AiMediaRelay__Secret: ""' in worker
    assert 'AI_MEDIA_RELAY_SECRET: ""' in gateway
    assert 'AiMediaRelay__Secret: ""' in gateway
    assert 'AiMediaRelay__Secret: ${AI_MEDIA_RELAY_SECRET:?AI_MEDIA_RELAY_SECRET is required}' in backend
