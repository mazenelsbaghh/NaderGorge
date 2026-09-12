from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

from clusterctl import Inventory, Node
from install_node_image_transfer import install_script, pinned_receiver_hosts, receiver_authorization


def test_20260912_generated_installer_keeps_authorized_key_newlines_valid_python():
    script = install_script("/tmp/massar-image-transfer-1234567890", "a" * 64, "b" * 64)

    compile(script, "remote-image-transfer-installation", "exec")


def test_receiver_authorization_is_restricted_to_builder_and_forced_receiver_command():
    public_key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIFixture comment"

    authorization = receiver_authorization(public_key, "10.77.0.13")

    assert authorization.startswith('restrict,from="10.77.0.13",command="/usr/bin/python3 -I ')
    assert 'massar-node-image-transfer.py receive" ssh-ed25519' in authorization
    assert authorization.endswith("massar-node-image-transfer")


def inventory() -> Inventory:
    nodes = tuple(Node(f"node-{i}", f"node-{i}", f"192.0.2.{i}", f"10.77.0.{i}", ()) for i in (1, 2, 3))
    return Inventory(Path("inventory.json"), {"ssh_user": "massar-ops"}, nodes, {})


def test_internal_host_pins_are_derived_from_operator_trust_not_network_keyscan(tmp_path):
    key = tmp_path / "host_key"
    subprocess.run(["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-f", str(key)], check=True)
    public_key = " ".join(key.with_suffix(".pub").read_text().split()[:2])
    hosts = tmp_path / "known_hosts"
    hosts.write_text(f"192.0.2.1 {public_key}\n192.0.2.2 {public_key}\n")
    subprocess.run(["ssh-keygen", "-H", "-f", str(hosts)], check=True, capture_output=True)

    assert pinned_receiver_hosts(inventory(), hosts) == f"node-1 {public_key}\nnode-2 {public_key}\n"


def test_missing_operator_host_pin_refuses_installation(tmp_path):
    hosts = tmp_path / "known_hosts"
    hosts.write_text("")

    with pytest.raises(subprocess.CalledProcessError):
        pinned_receiver_hosts(inventory(), hosts)
