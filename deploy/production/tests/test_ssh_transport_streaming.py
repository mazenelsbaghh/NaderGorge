from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "deploy/production/scripts/ssh_transport.py"
SPEC = importlib.util.spec_from_file_location("ssh_transport", MODULE_PATH)
assert SPEC and SPEC.loader
transport_module = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = transport_module
SPEC.loader.exec_module(transport_module)


class Pipe:
    def close(self) -> None:
        pass


class Stderr:
    def __init__(self, value: bytes) -> None:
        self.value = value

    def read(self) -> bytes:
        return self.value


class Process:
    def __init__(
        self,
        *,
        returncode: int = 0,
        stderr: bytes = b"",
        timeout: bool = False,
    ) -> None:
        self.returncode: int | None = returncode
        self.stdout = Pipe()
        self.stderr = Stderr(stderr)
        self.timeout = timeout
        self.terminated = False
        self.killed = False

    def communicate(self, *, timeout: float):
        if self.timeout:
            raise subprocess.TimeoutExpired("ssh", timeout)
        return b"", self.stderr.read()

    def wait(self, *, timeout: float) -> int:
        assert self.returncode is not None
        return self.returncode

    def poll(self) -> int | None:
        return None if not self.terminated else self.returncode

    def terminate(self) -> None:
        self.terminated = True
        self.returncode = -15

    def kill(self) -> None:
        self.killed = True
        self.returncode = -9


def strict_transport(monkeypatch: pytest.MonkeyPatch, tmp_path: Path):
    known_hosts = tmp_path / "known_hosts"
    identity = tmp_path / "id_ed25519"
    known_hosts.write_text("pinned\n")
    identity.write_text("private\n")
    identity.chmod(0o600)
    return transport_module.StrictSshTransport(known_hosts, identity)


def source_snapshot(tmp_path: Path) -> Path:
    source = tmp_path / "source"
    source.mkdir()
    (source / "tracked.txt").write_text("snapshot\n")
    return source


def target(node_id: str, address: str):
    return transport_module.SshTarget(node_id, address, "massar-ops")


def test_stream_directory_uses_strict_ssh_and_never_creates_a_local_archive(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    transport = strict_transport(monkeypatch, tmp_path)
    calls: list[tuple[list[str], dict]] = []
    processes = [Process(), Process()]

    def popen(argv, **kwargs):
        calls.append((list(argv), kwargs))
        return processes.pop(0)

    monkeypatch.setattr(transport_module.subprocess, "Popen", popen)
    source = source_snapshot(tmp_path)
    before = {path.relative_to(tmp_path) for path in tmp_path.rglob("*")}

    transport.stream_directory(
        target("node-3", "192.0.2.3"),
        source,
        "/tmp/massar-build-source-src-" + "a" * 40,
    )

    assert calls[0][0] == [
        "env",
        "COPYFILE_DISABLE=1",
        "tar",
        "-C",
        str(source),
        "-czf",
        "-",
        ".",
    ]
    ssh_argv = calls[1][0]
    assert ssh_argv[:3] == ["ssh", "-C", "-o"]
    assert "StrictHostKeyChecking=yes" in ssh_argv
    assert f"UserKnownHostsFile={tmp_path / 'known_hosts'}" in ssh_argv
    assert str(tmp_path / "id_ed25519") in ssh_argv
    assert "massar-ops@192.0.2.3" in ssh_argv
    assert "massar-production" in ssh_argv[-1]
    assert "tar -xzf -" in ssh_argv[-1]
    after = {path.relative_to(tmp_path) for path in tmp_path.rglob("*")}
    assert after == before


def test_20260912_image_transfer_runs_on_builder_without_relaying_bytes_through_workstation(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    transport = strict_transport(monkeypatch, tmp_path)
    calls: list[tuple[list[str], dict]] = []
    receipt = {"route": "wireguard", "sourceNode": "node-3", "targetNode": "node-1", "image": "backend"}

    def run(argv, **kwargs):
        calls.append((list(argv), kwargs))
        return subprocess.CompletedProcess(argv, 0, json.dumps(receipt), "")

    monkeypatch.setattr(transport_module.subprocess, "run", run)
    monkeypatch.setattr(transport_module.subprocess, "Popen", lambda *args, **kwargs: pytest.fail("local image pipe forbidden"))
    release = "src-" + "a" * 40
    observed = transport.stream_remote_file(
        target("node-3", "192.0.2.3"),
        f"/var/lib/massar/builds/{release}/artifacts/backend.tar",
        target("node-1", "10.77.0.11"),
        f"/tmp/massar-{release}/backend.tar",
    )

    assert observed == receipt
    assert len(calls) == 1
    assert "StrictHostKeyChecking=yes" in calls[0][0]
    assert "massar-ops@192.0.2.3" in calls[0][0]
    assert "massar-node-image-transfer.py send node-1" in calls[0][0][-1]
    assert "stdin" not in calls[0][1] and "input" not in calls[0][1]
    assert not list(tmp_path.glob("*.tar"))


@pytest.mark.parametrize(("source", "destination", "receiver"), [
    ("/etc/passwd", "/tmp/massar-file", "10.77.0.11"),
    (f"/var/lib/massar/builds/src-{'a' * 40}/artifacts/backend.tar", f"/tmp/massar-src-{'b' * 40}/backend.tar", "10.77.0.11"),
    (f"/var/lib/massar/builds/src-{'a' * 40}/artifacts/backend.tar", f"/tmp/massar-src-{'a' * 40}/backend.tar", "8.8.8.8"),
])
def test_direct_transfer_rejects_unsafe_paths_and_public_receivers_before_ssh(monkeypatch, tmp_path, source, destination, receiver):
    transport = strict_transport(monkeypatch, tmp_path)
    monkeypatch.setattr(subprocess, "run", lambda *args, **kwargs: pytest.fail("SSH must not run"))

    with pytest.raises(transport_module.SshTransportError):
        transport.stream_remote_file(target("node-3", "192.0.2.3"), source, target("node-1", receiver), destination)


def test_stream_propagates_remote_receiver_failure(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    transport = strict_transport(monkeypatch, tmp_path)
    processes = [Process(), Process(returncode=23, stderr=b"destination denied\n")]
    monkeypatch.setattr(transport_module.subprocess, "Popen", lambda *args, **kwargs: processes.pop(0))

    with pytest.raises(transport_module.SshTransportError, match="destination denied"):
        transport.stream_directory(
            target("node-3", "192.0.2.3"),
            source_snapshot(tmp_path),
            "/tmp/massar-build-source-src-" + "a" * 40,
        )


def test_stream_timeout_terminates_both_sides_and_propagates_timeout(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    transport = strict_transport(monkeypatch, tmp_path)
    producer = Process()
    consumer = Process(timeout=True)
    processes = [producer, consumer]
    monkeypatch.setattr(transport_module.subprocess, "Popen", lambda *args, **kwargs: processes.pop(0))

    with pytest.raises(transport_module.SshTransportError, match="timed out"):
        transport.stream_directory(
            target("node-3", "192.0.2.3"),
            source_snapshot(tmp_path),
            "/tmp/massar-build-source-src-" + "a" * 40,
            timeout_seconds=1,
        )
    assert producer.terminated
    assert consumer.terminated


def test_stream_rejects_unsafe_remote_paths_before_process_creation(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    transport = strict_transport(monkeypatch, tmp_path)
    monkeypatch.setattr(
        transport_module.subprocess,
        "Popen",
        lambda *args, **kwargs: pytest.fail("stream process must not start"),
    )
    with pytest.raises(transport_module.SshTransportError, match="normalized absolute path"):
        transport.stream_directory(
            target("node-3", "192.0.2.3"), source_snapshot(tmp_path), "/var/lib/massar/../escape"
        )
