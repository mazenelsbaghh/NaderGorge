from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from types import SimpleNamespace


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
installer = load("install_cloudflare_token_connector_helper")


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


def test_helper_bootstrap_installs_only_fixed_root_executor_and_sudoers():
    transport = Transport()
    installer.install(inventory(), transport)
    assert [command[0].node_id for command in transport.commands] == ["node-3", "node-2", "node-1"]
    script = transport.commands[0][1][-1]
    assert "visudo -cf" in script
    assert "root:root:755" in script
    assert "sudo -n -l /usr/local/sbin/massar-cloudflared-token-install" in script
    assert "/etc/sudoers.d/massar-cloudflared-token" in script
    assert "cloudflared-token-install --" not in script


def test_sudoers_permits_only_the_argument_free_fixed_executor():
    sudoers = installer.SUDOERS.read_text(encoding="utf-8").strip()
    assert sudoers == "massar-ops ALL=(root) NOPASSWD: /usr/local/sbin/massar-cloudflared-token-install"


def test_executor_reads_token_from_fixed_file_without_arguments():
    source = (SCRIPTS / "cloudflare_token_connector_executor.py").read_text(encoding="utf-8")
    assert "STAGED_TOKEN = Path(\"/tmp/massar-cloudflared-token\")" in source
    assert "def install() -> None:" in source
    assert "sys.argv" not in source
    assert "def arguments" not in source
