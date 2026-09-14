from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]


def load(name: str, relative: str):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


bootstrap_access = load("bootstrap_access", "deploy/production/scripts/bootstrap_access.py")
configure_firewall = load("configure_firewall", "deploy/production/scripts/configure_firewall.py")
configure_wireguard = load("configure_wireguard", "deploy/production/scripts/configure_wireguard.py")
release_images = load("release_images", "deploy/production/scripts/release_images.py")


def inventory_nodes() -> list[dict[str, str]]:
    return json.loads((ROOT / "deploy/production/inventory/production.yml").read_text())["nodes"]


def test_operator_bootstrap_accepts_only_ed25519_and_has_rescue_sudo() -> None:
    script = bootstrap_access.render_operator_bootstrap("ssh-ed25519 AAAATEST operator")
    assert "massar-ops" in script
    assert "visudo -cf" in script
    assert "PasswordAuthentication" not in script
    with pytest.raises(ValueError):
        bootstrap_access.render_operator_bootstrap("ssh-rsa AAAATEST")


def test_password_login_disable_is_separate_and_validated() -> None:
    script = bootstrap_access.render_disable_routine_password_login()
    assert "PasswordAuthentication no" in script
    assert "PermitRootLogin prohibit-password" in script
    assert "sshd -t" in script


def test_wireguard_renders_two_peers_and_no_private_value() -> None:
    nodes = inventory_nodes()
    rendered = configure_wireguard.render_wireguard(
        "node-1",
        nodes,
        {"node-1": "pub1", "node-2": "pub2", "node-3": "pub3"},
    )
    assert rendered.count("[Peer]") == 2
    assert "10.77.0.11/32" in rendered
    assert "191.218.161.78:51820" in rendered
    assert "PrivateKey = <read-at-install:" in rendered


def test_firewall_requires_explicit_ipv4_operator_network() -> None:
    template = ROOT / "deploy/production/config/firewall/massar-production.nft"
    rendered = configure_firewall.render_firewall(template, "203.0.113.7/32")
    assert "203.0.113.7/32" in rendered
    assert "tcp dport 22" in rendered
    assert "iifname \"wg0\"" in rendered
    with pytest.raises(ValueError):
        configure_firewall.render_firewall(template, "2001:db8::/64")


def test_release_manifest_requires_all_exact_sha256_digests() -> None:
    valid = {name: f"sha256:{'a' * 64}" for name in release_images.IMAGES}
    release_images.verify_manifest(valid)
    with pytest.raises(ValueError):
        release_images.verify_manifest({"backend": f"sha256:{'a' * 64}"})
