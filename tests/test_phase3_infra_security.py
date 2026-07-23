from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def test_worker_dockerfile_runs_as_non_root():
    dockerfile = read("worker/Dockerfile")
    assert "USER worker" in dockerfile
    assert "useradd" in dockerfile


def test_root_compose_worker_uses_ready_and_no_host_port():
    compose = read("docker-compose.yml")
    assert "http://localhost:3001/ready" in compose
    worker_section = compose.split("  worker:", 1)[1].split("  # Public Landing Surface", 1)[0]
    assert "\n    ports:" not in worker_section


def test_protected_resources_volume_is_shared_only_for_internal_nginx():
    compose = read("docker-compose.yml")
    assert "massar_protected_resources:/app/App_Data/protected/resources" in compose
    assert "massar_protected_resources:/var/www/assets/protected/resources:ro" in compose
    assert "massar_protected_resources:" in compose


def test_redis_hardening_is_configured():
    compose = read("docker-compose.yml")
    assert "--requirepass" in compose
    assert "--appendonly" in compose
    assert "--maxmemory-policy" in compose
    assert "redis-cli -a" in compose


def test_nginx_protected_assets_are_internal_and_not_wildcard_cors():
    nginx = read("docker/nginx/massar.conf")
    secured = nginx.split("location /secured-assets/", 1)[1].split("location /", 1)[0]
    assert "internal;" in secured
    assert 'Access-Control-Allow-Origin "*"' not in secured
    assert "$massar_cors_origin" in secured
    assets = nginx.split("server_name assets.massar-academy.net;", 1)[1]
    assert "location ^~ /protected/" in assets
    assert "location ^~ /uploads/resources/" in assets


def test_tls_contract_is_documented():
    docs = read("docs/verification-contract.md")
    assert "Production TLS and Protected Assets Contract" in docs
    assert "X-Forwarded-Proto" in docs


if __name__ == "__main__":
    for name, func in sorted(globals().items()):
        if name.startswith("test_"):
            func()
            print(f"PASS {name}")
