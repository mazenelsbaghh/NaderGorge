from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8")


def admin_ai_sources(root: str, suffixes: tuple[str, ...]) -> list[Path]:
    return [
        path
        for path in (ROOT / root).rglob("*")
        if path.is_file()
        and path.suffix in suffixes
        and ("adminai" in path.name.lower() or "admin-ai" in path.as_posix().lower())
    ]


def test_admin_ai_is_disabled_by_default_and_uses_an_independent_hmac_key():
    environment = read(".env.example")
    assert "ADMIN_AI_ENABLED=false" in environment
    assert "ADMIN_AI_HMAC_KEY=" in environment
    assert "AdminAI__Enabled: ${ADMIN_AI_ENABLED:-false}" in read("docker-compose.yml")


def test_frontend_admin_ai_does_not_persist_private_state_or_import_other_chat_features():
    sources = [
        path for path in admin_ai_sources("frontend/src", (".ts", ".tsx"))
        if "live-support" not in path.as_posix().lower()
        and ".test." not in path.name
        and ".spec." not in path.name
    ]
    assert sources
    combined = "\n".join(path.read_text(encoding="utf-8") for path in sources)
    assert "localStorage" not in combined
    assert "sessionStorage" not in combined
    assert "live-support-service" not in combined
    assert "chat-service" not in combined


def test_worker_admin_ai_has_no_database_or_unbounded_execution_authority():
    sources = admin_ai_sources("worker/src", (".ts",))
    assert sources
    combined = "\n".join(path.read_text(encoding="utf-8") for path in sources)
    prohibited = ("DB_CONNECTION_STRING", "node:pg", "from 'pg'", 'from "pg"', "executeSql", "rawSql")
    for marker in prohibited:
        assert marker not in combined


def test_backend_admin_ai_domain_is_separate_from_live_support_and_human_chat():
    sources = admin_ai_sources("backend/src", (".cs",))
    assert sources
    domain_sources = [path for path in sources if "NaderGorge.Domain" in str(path)]
    combined = "\n".join(path.read_text(encoding="utf-8") for path in domain_sources)
    assert "LiveSupport" not in combined
    assert "ChatRoom" not in combined
    assert "ChatMessage" not in combined


def test_realtime_contract_forbids_arbitrary_payload_content():
    contract = read("frontend/src/lib/admin-ai-agent-client-contract.ts")
    assert "payload" not in contract
    assert "ADMIN_AI_EVENT_TYPES" in contract


def test_admin_ai_docker_configuration_does_not_pass_database_credentials_as_feature_inputs():
    compose = read("docker-compose.yml")
    admin_ai_lines = [line.strip() for line in compose.splitlines() if "ADMIN_AI" in line or "AdminAI__" in line]
    assert admin_ai_lines
    assert all("DATABASE" not in line and "CONNECTION" not in line for line in admin_ai_lines)
