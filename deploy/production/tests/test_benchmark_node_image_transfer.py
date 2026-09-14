from __future__ import annotations

import hashlib
import subprocess
from pathlib import Path

import pytest

from benchmark_node_image_transfer import benchmark_receiver, create_benchmark_stage, remove_benchmark_stage
from clusterctl import Node
from release_contract import ReleaseManifest
from ssh_transport import SshTarget


class LocalCommandTransport:
    """Execute the remote filesystem boundary in a disposable local directory."""

    def run(self, target, command, **kwargs):
        return subprocess.run(command, check=True, text=True, capture_output=True)


TARGET = SshTarget("node-1", "192.0.2.1", "massar-ops")


def test_benchmark_cleanup_removes_only_its_owned_staging(tmp_path):
    transport = LocalCommandTransport()
    stage = tmp_path / "staging"
    identity = create_benchmark_stage(transport, TARGET, str(stage))
    (stage / "archive.tar").write_bytes(b"benchmark")

    remove_benchmark_stage(transport, TARGET, str(stage), identity)

    assert not stage.exists()


def test_existing_staging_is_preserved_when_benchmark_cannot_acquire_it(tmp_path):
    stage = tmp_path / "staging"
    stage.mkdir()
    archive = stage / "archive.tar"
    archive.write_bytes(b"another operation")

    with pytest.raises(subprocess.CalledProcessError):
        create_benchmark_stage(LocalCommandTransport(), TARGET, str(stage))

    assert archive.read_bytes() == b"another operation"


def test_replaced_staging_is_not_deleted_by_benchmark_cleanup(tmp_path):
    transport = LocalCommandTransport()
    stage = tmp_path / "staging"
    identity = create_benchmark_stage(transport, TARGET, str(stage))
    stage.rename(tmp_path / "original-staging")
    stage.mkdir()
    protected = stage / "another-operation.tar"
    protected.write_bytes(b"keep")

    with pytest.raises(subprocess.CalledProcessError):
        remove_benchmark_stage(transport, TARGET, str(stage), identity)

    assert protected.read_bytes() == b"keep"


def test_benchmark_transfers_image_archives_and_excludes_release_metadata(tmp_path):
    release = "src-" + hashlib.sha256(str(tmp_path).encode()).hexdigest()[:40]
    checksum = hashlib.sha256(b"archive").hexdigest()
    manifest = ReleaseManifest(path=tmp_path / "manifest.json", sha256="a" * 64,
        release_id=release, git_commit=None, source_state_sha256="b" * 64,
        provenance_type="source-build", images={"backend": "sha256:" + "c" * 64},
        release_files_sha256="d" * 64,
        artifacts={"backend": {"archiveSha256": checksum}, "release-files": {"sha256": "d" * 64}})

    class ArchiveTransport(LocalCommandTransport):
        def stream_remote_file(self, builder, source, receiver, destination):
            assert receiver.address == "10.77.0.11"
            Path(destination).write_bytes(b"archive")
            return {"image": Path(destination).stem, "sha256": checksum}

    node = Node("node-1", "node-1", "192.0.2.1", "10.77.0.11", ())
    receipts = benchmark_receiver(ArchiveTransport(), SshTarget("node-3", "192.0.2.3", "massar-ops"), node, manifest)

    assert receipts == [{"image": "backend", "sha256": checksum}]
    assert not Path(f"/tmp/massar-{release}").exists()
