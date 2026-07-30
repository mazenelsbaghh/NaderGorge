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


load("remote_build_release")
planner = load("remote_distribution_plan")
RELEASE = "src-" + "a" * 40


def inventory():
    return SimpleNamespace(nodes=tuple(
        SimpleNamespace(id=node_id, roles=("app", "builder") if node_id == "node-3" else ("app",))
        for node_id in ("node-1", "node-2", "node-3")
    ))


def builder_manifest():
    return {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": "massar-production",
        "builderNodeId": "node-3",
        "releaseId": RELEASE,
        "sourceStateSha256": "a" * 64,
        "platform": "linux/amd64",
        "images": {name: f"sha256:{index:064x}" for index, name in enumerate(planner.IMAGES, 1)},
        "artifacts": {name: {"filename": f"{name}.tar", "sha256": f"{index:064x}"} for index, name in enumerate(planner.IMAGES, 1)},
    }


def verification(plan):
    return {
        node_id: {
            transfer.image: {
                "archiveSha256": transfer.archive_sha256,
                "imageDigest": transfer.image_digest,
            }
            for transfer in plan.transfers_for_node(node_id)
        }
        for node_id in planner.NODE_IDS
    }


def test_plan_maps_builder_cache_to_all_nodes_with_remote_only_paths() -> None:
    plan = planner.create_remote_distribution_plan(inventory(), builder_manifest())
    assert len(plan.transfers) == 12
    assert all(item.source_node_id == "node-3" for item in plan.transfers)
    assert all(str(item.source_path).startswith(f"/var/lib/massar/builds/{RELEASE}/artifacts/") for item in plan.transfers)
    assert all(str(item.target_path).startswith(f"/tmp/massar-{RELEASE}/") for item in plan.transfers)
    assert all(not hasattr(item.source_path, "write_bytes") for item in plan.transfers)


def test_plan_partial_resume_only_returns_unverified_approved_nodes() -> None:
    plan = planner.create_remote_distribution_plan(inventory(), builder_manifest())
    assert plan.remaining_nodes({"node-3"}) == ("node-1", "node-2")
    with pytest.raises(planner.RemoteDistributionError, match="unapproved"):
        plan.remaining_nodes({"node-9"})


def test_final_manifest_is_blocked_by_tampered_node_verification() -> None:
    plan = planner.create_remote_distribution_plan(inventory(), builder_manifest())
    observed = verification(plan)
    observed["node-2"]["worker"]["archiveSha256"] = "0" * 64
    with pytest.raises(planner.RemoteDistributionError, match="node-2 did not verify worker"):
        plan.final_manifest(observed, "f" * 64)


def test_final_manifest_requires_all_nodes_and_exact_archive_image_proofs() -> None:
    plan = planner.create_remote_distribution_plan(inventory(), builder_manifest())
    observed = verification(plan)
    final = plan.final_manifest(observed, "f" * 64)
    assert final["digestParity"] is True
    assert set(final["distribution"]) == set(planner.NODE_IDS)
    assert all(value == {"status": "verified", "releaseFilesSha256": "f" * 64} for value in final["distribution"].values())
    del observed["node-1"]
    with pytest.raises(planner.RemoteDistributionError, match="all three nodes"):
        plan.final_manifest(observed, "f" * 64)


def test_plan_rejects_tampered_builder_artifact_manifest() -> None:
    manifest = builder_manifest()
    manifest["artifacts"]["backend"]["filename"] = "other.tar"
    with pytest.raises(planner.RemoteDistributionError, match="artifact is invalid"):
        planner.create_remote_distribution_plan(inventory(), manifest)
