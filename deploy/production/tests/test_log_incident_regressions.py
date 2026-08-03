from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_websocket_logs_never_persist_signalr_query_tokens() -> None:
    nginx = (ROOT / "deploy/production/config/nginx/massar-node.conf.template").read_text()
    websocket = nginx.split("server_name ws.massar-academy.net;", 1)[1].split("\n}", 1)[0]
    safe_format = nginx.split("log_format massar_websocket_safe", 1)[1].split(";", 1)[0]

    assert "$request_method $uri $server_protocol" in safe_format
    assert "$request_uri" not in safe_format
    assert "$request " not in safe_format
    assert "access_log /var/log/nginx/access.log massar_websocket_safe;" in websocket
    assert "error_log /var/log/nginx/error.log crit;" in websocket


def test_read_only_frontends_have_a_writable_next_image_cache() -> None:
    compose = (ROOT / "deploy/production/compose/compose.app.yml").read_text()
    frontend_defaults = compose.split("x-frontend-defaults:", 1)[1].split("\nservices:", 1)[0]

    assert "read_only: true" in frontend_defaults
    assert "/app/.next/cache:size=256m,mode=1777" in frontend_defaults


def test_frontend_uses_memory_incremental_cache_instead_of_writing_server_files() -> None:
    config = (ROOT / "frontend/next.config.ts").read_text()
    handler = (ROOT / "frontend/cache-handler.cjs").read_text()

    assert "cacheHandler:" in config
    assert "cache-handler.cjs" in config
    assert "MAX_ENTRIES" in handler
    assert "entries.clear()" in handler


def test_telegram_download_destroys_the_client_update_loop() -> None:
    source = (ROOT / "worker/src/utils/audioExtractor.ts").read_text()

    assert "await client.destroy();" in source
    assert "await client.disconnect();" not in source


def test_backend_persists_data_protection_and_honors_forwarded_scheme_first() -> None:
    program = (ROOT / "backend/src/NaderGorge.API/Program.cs").read_text()

    assert "PersistKeysToFileSystem" in program
    assert "HttpsPort = 443" in program
    assert program.index("app.UseForwardedHeaders();") < program.index("UseHttpsRedirection()")
    assert '!context.Request.Path.StartsWithSegments("/api/v1/internal")' in program


def test_financial_constraint_allows_an_intentional_platform_loss() -> None:
    model = (ROOT / "backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs").read_text()
    constraint = model.split('HasCheckConstraint("CK_sales_financial_effect_amounts"', 1)[1].split(");", 1)[0]

    assert '\\"TeacherShareImpact\\" >= 0' in constraint
    assert '\\"PlatformShareImpact\\" >= 0' not in constraint


def test_frontend_options_probes_do_not_create_405_noise() -> None:
    nginx = (ROOT / "deploy/production/config/nginx/massar-node.conf.template").read_text()

    assert nginx.count("if ($request_method = OPTIONS) { return 204; }") == 5
