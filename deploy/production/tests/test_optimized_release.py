from __future__ import annotations

import json
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import optimized_builder as builder
import registry_distribution as distribution
import release_contract
from install_image_registry import prepare_certificates, registry_config, registry_unit, repair_ca_usage, openssl
from test_release_contract import release_manifest_v2, RELEASE


@pytest.fixture
def source(tmp_path):
    for component in ("backend", "frontend", "worker"):
        context = tmp_path / component
        context.mkdir()
        (context / "Dockerfile").write_text("FROM base:1 AS build\nCOPY . .\n")
        (context / "source.txt").write_text("original")
    policy = tmp_path / "deploy/production/scripts/optimized_builder.py"
    policy.parent.mkdir(parents=True)
    policy.write_text("reviewed-build-policy")
    return tmp_path


@pytest.mark.parametrize(("changed", "affected"), [
    ("frontend/source.txt", {"frontend"}),
    ("worker/source.txt", {"worker"}),
    ("backend/source.txt", {"backend", "migrator"}),
    ("deploy/production/scripts/optimized_builder.py", set(builder.IMAGES)),
])
def test_changed_and_deleted_inputs_only_invalidate_dependent_images(source, changed, affected):
    bases = {"base:1": "sha256:" + "1" * 64}
    before = {image: builder.input_digest(source, image, bases) for image in builder.IMAGES}
    path = source / changed
    path.write_text("changed")
    after = {image: builder.input_digest(source, image, bases) for image in builder.IMAGES}
    assert {image for image in before if before[image] != after[image]} == affected
    path.unlink()
    deleted = {image: builder.input_digest(source, image, bases) for image in builder.IMAGES}
    assert {image for image in before if before[image] != deleted[image]} == affected


def test_base_digest_changes_invalidate_build_cache(source):
    assert builder.input_digest(source, "worker", {"base:1": "old"}) != builder.input_digest(source, "worker", {"base:1": "new"})


def test_registry_artifacts_preserve_release_contract_and_reject_digest_mismatch(tmp_path):
    path = release_manifest_v2(tmp_path / "manifest.json")
    manifest = json.loads(path.read_text())
    for image in builder.IMAGES:
        manifest["artifacts"][image] = {"imageDigest": manifest["images"][image], "registryDigest": "sha256:" + "c" * 64, "inputSha256": "d" * 64}
    path.write_text(json.dumps(manifest))
    assert release_contract.load_release_manifest(path, manifest["releaseId"]).artifacts["backend"]["registryDigest"] == "sha256:" + "c" * 64
    manifest["artifacts"]["backend"]["imageDigest"] = "sha256:" + "f" * 64
    path.write_text(json.dumps(manifest))
    with pytest.raises(release_contract.ReleaseContractError, match="registry artifact parity"):
        release_contract.load_release_manifest(path, manifest["releaseId"])


def registry_builder():
    nodes = tuple(SimpleNamespace(id=f"node-{n}", overlay_address=f"10.77.0.{10+n}", public_address=f"192.0.2.{n}") for n in (1, 2, 3))
    inventory = SimpleNamespace(nodes=nodes, cluster={"ssh_user": "massar-ops"})
    provenance = {"releaseId": RELEASE, "sourceStateSha256": "a" * 64}
    manifest = {"schemaVersion": 2, "status": "success", "clusterId": "massar-production", "builderNodeId": "node-3", "platform": "linux/amd64", "registryEndpoint": "10.77.0.13:5443", **provenance}
    manifest["images"] = {image: "sha256:" + str(n) * 64 for n, image in enumerate(builder.IMAGES, 1)}
    manifest["artifacts"] = {image: {"imageDigest": identity, "registryDigest": "sha256:" + "f" * 64, "inputSha256": "a" * 64, "disposition": "reused", "elapsedSeconds": 1.0} for image, identity in manifest["images"].items()}
    return inventory, provenance, manifest


@pytest.mark.parametrize("field", ["registryEndpoint", "releaseId", "sourceStateSha256"])
def test_registry_distribution_rejects_wrong_origin_before_ssh(field):
    inventory, provenance, manifest = registry_builder()
    manifest[field] = "untrusted"
    with pytest.raises(ValueError, match="provenance"):
        distribution.validate_builder(manifest, inventory, provenance)


def test_registry_distribution_only_seals_after_all_nodes_verify(tmp_path):
    inventory, provenance, manifest = registry_builder()
    distribution.validate_builder(manifest, inventory, provenance)
    (tmp_path / "release-files.tar.gz").write_bytes(b"reviewed bundle")
    (tmp_path / "manifest.json").write_text(json.dumps(manifest))
    class Transport:
        def __init__(self, fail_node=None):
            self.fail_node, self.nodes = fail_node, set()
        def run(self, target, command, **kwargs):
            self.nodes.add(target.node_id)
            if target.node_id == self.fail_node:
                raise RuntimeError("digest mismatch")
            return SimpleNamespace(returncode=0)
    passing = Transport()
    assert distribution.RegistryDistributionRunner(inventory, passing, manifest, tmp_path).run()["digestParity"] is True
    assert passing.nodes == {"node-1", "node-2", "node-3"}
    with pytest.raises(RuntimeError, match="digest mismatch"):
        distribution.RegistryDistributionRunner(inventory, Transport("node-2"), manifest, tmp_path).run()


def test_mtls_credentials_are_stable_and_incomplete_state_is_not_rotated(tmp_path):
    root = tmp_path / "certificates"
    prepare_certificates(root, "10.77.0.13")
    original = (root / "ca.key").read_bytes()
    prepare_certificates(root, "10.77.0.13")
    assert (root / "ca.key").read_bytes() == original
    repair_ca_usage(root)
    assert (root / "ca.key").read_bytes() == original
    openssl("verify", "-x509_strict", "-CAfile", str(root / "ca.crt"), str(root / "server.crt"))
    (root / "node-2.key").unlink()
    with pytest.raises(RuntimeError, match="refusing automatic rotation"):
        prepare_certificates(root, "10.77.0.13")
    assert (root / "ca.key").read_bytes() == original
    config = registry_config("10.77.0.13:5443")
    assert config["http"]["addr"] == "10.77.0.13:5443"
    assert config["http"]["tls"]["clientauth"] == "require-and-verify-client-cert"
    assert "--network host" in registry_unit() and "--publish" not in registry_unit()


def test_registry_index_binds_docker_identity_to_verified_platform(monkeypatch):
    # Only the HTTPS boundary is replaced; index selection and identity binding are real.
    root_digest, child_digest, config_digest = ("sha256:" + c * 64 for c in "abc")
    def document(_endpoint, _image, reference):
        if reference == root_digest:
            return root_digest, {"manifests": [{"digest": child_digest, "platform": {"os": "linux", "architecture": "amd64"}}]}
        return child_digest, {"config": {"digest": config_digest}}
    monkeypatch.setattr(builder, "registry_document", document)
    observed, identities = builder.registry_manifest("10.77.0.13:5443", "backend", root_digest)
    assert observed == root_digest
    assert identities == {root_digest, child_digest, config_digest}
