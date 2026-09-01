from __future__ import annotations

import importlib.util
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MODULE_SPEC = importlib.util.spec_from_file_location(
    "build_app_env",
    ROOT / "deploy/production/scripts/build_app_env.py",
)
assert MODULE_SPEC and MODULE_SPEC.loader
build_app_env = importlib.util.module_from_spec(MODULE_SPEC)
MODULE_SPEC.loader.exec_module(build_app_env)


def test_messenger_pages_are_rendered_with_page_scoped_credentials(
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

    source = {"GEMINI_API_KEY": "gemini-test-key"}
    for page_number in range(1, 4):
        source.update(
            {
                f"FACEBOOK_MESSENGER_PAGE_{page_number}_ID": f"page-{page_number}",
                f"FACEBOOK_MESSENGER_PAGE_{page_number}_NAME": f"Page {page_number}",
                f"FACEBOOK_MESSENGER_PAGE_{page_number}_ACCESS_TOKEN": f"token-{page_number}",
                f"FACEBOOK_MESSENGER_PAGE_{page_number}_HUMAN_AGENT_ENABLED": "false",
            }
        )
    source.update(
        {
            "FACEBOOK_MESSENGER_VERIFY_TOKEN": "verify-token",
            "FACEBOOK_MESSENGER_APP_SECRET": "app-secret",
            "FACEBOOK_MESSENGER_API_VERSION": "v25.0",
        }
    )

    rendered = dict(
        line.split("=", 1) for line in build_app_env.render(source, secrets)
    )

    assert rendered["FacebookMessenger__VerifyToken"] == "verify-token"
    assert rendered["FacebookMessenger__AppSecret"] == "app-secret"
    assert rendered["FacebookMessenger__ApiVersion"] == "v25.0"
    for page_index in range(3):
        page_number = page_index + 1
        prefix = f"FacebookMessenger__Pages__{page_index}"
        assert rendered[f"{prefix}__PageId"] == f"page-{page_number}"
        assert rendered[f"{prefix}__DisplayName"] == f"Page {page_number}"
        assert rendered[f"{prefix}__AccessToken"] == f"token-{page_number}"
        assert rendered[f"{prefix}__HumanAgentEnabled"] == "false"
    assert rendered["AI_MEDIA_RELAY_SECRET"] == "ai-media-relay-secret"
