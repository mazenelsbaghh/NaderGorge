from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"


def load(name: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


load("ssh_transport")
load("release_images")
load("remote_build_release")
load("remote_distribution_plan")
load("remote_distribution_runner")
load("remote_builder_workflow")
load("clusterctl")
installer = load("install_cloudflare_token_connector")


def inventory():
    return SimpleNamespace(
        cluster={"ssh_user": "massar-ops"},
        nodes=tuple(
            SimpleNamespace(id=f"node-{index}", public_address=f"192.0.2.{index}")
            for index in (1, 2, 3)
        ),
    )


class Transport:
    def __init__(self):
        self.copies: list[tuple] = []
        self.commands: list[tuple] = []

    def copy(self, *args, **kwargs):
        self.copies.append(args)

    def run(self, *args, **kwargs):
        self.commands.append(args)


def token_file(tmp_path: Path) -> Path:
    path = tmp_path / "cloudflare-token"
    path.write_text("not-a-real-token\n", encoding="utf-8")
    path.chmod(0o600)
    return path


def test_token_connector_rolls_out_sequentially_without_embedding_token(tmp_path: Path):
    token = token_file(tmp_path)
    transport = Transport()
    installer.install(inventory(), transport, token)
    nodes = [command[0].node_id for command in transport.commands]
    assert nodes == ["node-3", "node-3", "node-2", "node-2", "node-1", "node-1"]
    assert [copy[0].node_id for copy in transport.copies] == ["node-3", "node-2", "node-1"]
    commands = "\n".join(command[1][-1] for command in transport.commands)
    assert "not-a-real-token" not in commands
    assert "/tmp/massar-cloudflared-token" in commands
    assert "sudo -n /usr/local/sbin/massar-cloudflared-token-install" in commands


def test_token_connector_rejects_permissive_token_file(tmp_path: Path):
    token = token_file(tmp_path)
    token.chmod(0o644)
    with pytest.raises(installer.TokenConnectorError, match="0600"):
        installer.validate_token_file(token)


def test_token_connector_assets_never_include_token_arguments():
    unit = installer.UNIT.read_text(encoding="utf-8")
    config = "\n".join(
        line for line in installer.CONFIG.read_text(encoding="utf-8").splitlines()
        if not line.lstrip().startswith("#")
    )
    assert "--token-file /etc/massar-cloudflared-token/token" in unit
    assert "cloudflare/cloudflared:latest" in unit
    assert "TUNNEL_TOKEN=" not in unit
    assert "credentials-file:" not in config
    assert "tunnel:" not in config
