from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
NGINX_CONFIG = ROOT / "deploy/production/config/nginx/massar-node.conf.template"


def _location_blocks(source: str, prefix: str) -> list[str]:
    blocks: list[str] = []
    cursor = 0
    while True:
        start = source.find(prefix, cursor)
        if start < 0:
            return blocks
        opening = source.find("{", start)
        depth = 0
        for index in range(opening, len(source)):
            if source[index] == "{":
                depth += 1
            elif source[index] == "}":
                depth -= 1
                if depth == 0:
                    blocks.append(source[start : index + 1])
                    cursor = index + 1
                    break
        else:
            raise AssertionError(f"unterminated nginx location beginning with {prefix!r}")


def _max_age(block: str) -> int:
    match = re.search(r"Cache-Control\s+\"[^\"]*max-age=(\d+)[^\"]*\"", block)
    assert match, "location must emit an explicit bounded Cache-Control max-age"
    return int(match.group(1))


def test_hashed_next_assets_are_immutable_for_every_frontend_surface() -> None:
    source = NGINX_CONFIG.read_text(encoding="utf-8")
    blocks = _location_blocks(source, "location ^~ /_next/static/")

    assert len(blocks) == 5
    for block in blocks:
        assert _max_age(block) >= 31_536_000
        assert "immutable" in block
        assert "proxy_pass http://" in block


def test_mutable_public_assets_are_bounded_and_revalidatable() -> None:
    source = NGINX_CONFIG.read_text(encoding="utf-8")
    assets_server = source.split("server_name assets.massar-academy.net;", 1)[1]
    public_block = _location_blocks(assets_server, "location /")[0]

    assert 0 < _max_age(public_block) <= 86_400
    assert "immutable" not in public_block
    assert "etag off;" not in public_block
    assert "must-revalidate" in public_block
    assert "no-transform" in public_block


def test_private_asset_denials_cannot_inherit_public_cacheability() -> None:
    source = NGINX_CONFIG.read_text(encoding="utf-8")
    assets_server = source.split("server_name assets.massar-academy.net;", 1)[1]

    for path in ("/protected/", "/private/", "/.tmp/"):
        denial = _location_blocks(assets_server, f"location ^~ {path}")[0]
        assert "return 404;" in denial
        assert "public" not in denial
