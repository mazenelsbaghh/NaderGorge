from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import subprocess
import sys
from pathlib import Path

import pytest


SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
SPEC = importlib.util.spec_from_file_location("node_image_transfer", SCRIPTS / "node_image_transfer.py")
transfer = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = transfer
SPEC.loader.exec_module(transfer)
RELEASE = "git-" + "a" * 40
ARCHIVE = b"release archive bytes\x00" * 100


@pytest.fixture
def staged_archive(tmp_path):
    directory = tmp_path / f"massar-{RELEASE}"
    directory.mkdir(mode=0o700)
    receipt = transfer.ArchiveReceipt(RELEASE, "backend", hashlib.sha256(ARCHIVE).hexdigest(), len(ARCHIVE))
    return directory, receipt


def test_verified_archive_is_published_with_private_permissions(tmp_path, staged_archive):
    directory, receipt = staged_archive

    transfer.receive_archive(io.BytesIO(ARCHIVE), receipt, tmp_path)

    archive = directory / "backend.tar"
    assert archive.read_bytes() == ARCHIVE
    assert archive.stat().st_mode & 0o777 == 0o600
    assert list(directory.iterdir()) == [archive]


@pytest.mark.parametrize("corrupt_archive", [ARCHIVE[:-1], ARCHIVE + b"extra", b"x" * len(ARCHIVE)])
def test_incomplete_oversized_or_corrupt_transfer_leaves_no_archive(tmp_path, staged_archive, corrupt_archive):
    directory, receipt = staged_archive

    with pytest.raises(transfer.ImageTransferError):
        transfer.receive_archive(io.BytesIO(corrupt_archive), receipt, tmp_path)

    assert list(directory.iterdir()) == []


@pytest.mark.parametrize("existing_kind", ["file", "symlink"])
def test_receiver_never_overwrites_an_existing_destination(tmp_path, staged_archive, existing_kind):
    directory, receipt = staged_archive
    protected = tmp_path / "protected"
    protected.write_bytes(b"keep")
    archive = directory / "backend.tar"
    if existing_kind == "symlink":
        archive.symlink_to(protected)
    else:
        archive.write_bytes(b"keep")

    with pytest.raises(FileExistsError):
        transfer.receive_archive(io.BytesIO(ARCHIVE), receipt, tmp_path)

    assert archive.read_bytes() == protected.read_bytes() == b"keep"
    assert list(directory.iterdir()) == [archive]


@pytest.mark.parametrize("unsafe_stage", ["symlink", "writable", "missing"])
def test_receiver_rejects_unprepared_or_unsafe_staging(tmp_path, staged_archive, unsafe_stage):
    directory, receipt = staged_archive
    if unsafe_stage == "writable":
        directory.chmod(0o777)
    else:
        directory.rmdir()
        if unsafe_stage == "symlink":
            outside = tmp_path / "outside"
            outside.mkdir(mode=0o700)
            directory.symlink_to(outside, target_is_directory=True)

    with pytest.raises((OSError, transfer.ImageTransferError)):
        transfer.receive_archive(io.BytesIO(ARCHIVE), receipt, tmp_path)

    assert not list(tmp_path.rglob("*.tar"))


def test_interrupted_receive_cleans_its_partial_file(tmp_path, staged_archive):
    directory, receipt = staged_archive

    class InterruptedStream(io.BytesIO):
        def read(self, size=-1):
            if self.tell():
                raise TimeoutError("connection interrupted")
            return super().read(1)

    with pytest.raises(TimeoutError):
        transfer.receive_archive(InterruptedStream(ARCHIVE), receipt, tmp_path)

    assert list(directory.iterdir()) == []


@pytest.mark.parametrize("command", [
    "id", "bash -c id", "scp -t /tmp/file", "receive",
    f"receive {RELEASE} backend {'a' * 64} 0",
    f"receive {RELEASE} backend {'a' * 64} {transfer.MAX_ARCHIVE_BYTES + 1}",
    f"receive {RELEASE}/../../escape backend {'a' * 64} 1",
    f"receive {RELEASE} ../authorized_keys {'a' * 64} 1",
    f"receive {RELEASE} backend {'a' * 64} 1;id",
])
def test_transfer_identity_rejects_shell_commands_and_invalid_archive_requests(command):
    with pytest.raises((transfer.ImageTransferError, ValueError)):
        transfer.receive_request(command)


def test_valid_forced_command_preserves_the_expected_checksum_and_size(staged_archive):
    _, receipt = staged_archive
    command = f"receive {receipt.release} {receipt.image} {receipt.sha256} {receipt.size}"

    assert transfer.receive_request(command) == receipt


@pytest.mark.parametrize("route", [
    [{"dev": "eth0", "prefsrc": "10.77.0.13"}],
    [{"dev": "wg0", "prefsrc": "10.77.0.99"}], [],
])
def test_sender_refuses_a_route_outside_the_configured_wireguard_link(monkeypatch, route):
    config = {"nodeId": "node-3", "builderAddress": "10.77.0.13", "targets": {"node-1": "10.77.0.11"}}
    monkeypatch.setattr(subprocess, "run", lambda *args, **kwargs: subprocess.CompletedProcess(args, 0, json.dumps(route)))

    with pytest.raises(transfer.ImageTransferError, match="WireGuard route"):
        transfer.require_wireguard_route(config, "node-1")


def test_sender_uses_only_the_dedicated_identity_and_pinned_internal_receiver(monkeypatch, tmp_path, staged_archive):
    _, receipt = staged_archive
    monkeypatch.setenv("HOME", str(tmp_path))
    (tmp_path / ".ssh").mkdir()
    for name in (transfer.IDENTITY_NAME, transfer.HOSTS_NAME):
        path = tmp_path / name
        path.write_text("test fixture")
        path.chmod(0o600)
    config = {"nodeId": "node-3", "builderAddress": "10.77.0.13", "targets": {"node-1": "10.77.0.11"}}
    route = [{"dev": "wg0", "prefsrc": "10.77.0.13"}]
    monkeypatch.setattr(subprocess, "run", lambda *args, **kwargs: subprocess.CompletedProcess(args, 0, json.dumps(route)))

    command = transfer.transfer_ssh_command(config, "node-1", receipt)

    assert "StrictHostKeyChecking=yes" in command
    assert "HostKeyAlias=node-1" in command
    assert "ForwardAgent=no" in command
    assert "ClearAllForwardings=yes" in command
    assert str(tmp_path / transfer.IDENTITY_NAME) in command
    assert f"UserKnownHostsFile={tmp_path / transfer.HOSTS_NAME}" in command
    assert "massar-ops@10.77.0.11" in command
    assert command[command.index("-b") + 1] == "10.77.0.13"
    assert transfer.receive_request(command[-1]) == receipt
