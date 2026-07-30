from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
MODULE_PATH = SCRIPTS / "remote_build_release.py"
SPEC = importlib.util.spec_from_file_location("remote_build_release", MODULE_PATH)
assert SPEC and SPEC.loader
remote_build = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = remote_build
SPEC.loader.exec_module(remote_build)


RELEASE = "src-" + "a" * 40
PROVENANCE = {
    "releaseId": RELEASE,
    "sourceStateSha256": "b" * 64,
}


def inventory(*, builder_on: str = "node-3", extra_builder: bool = False):
    nodes = []
    for node_id in ("node-1", "node-2", "node-3"):
        roles = ["app", "ingress", "etcd", "postgres", "redis", "sentinel"]
        if node_id == "node-3":
            roles.append("file-arbiter")
        elif node_id == "node-1":
            roles.append("file-data-primary")
        else:
            roles.append("file-data-standby")
        if node_id == builder_on or (extra_builder and node_id == "node-1"):
            roles.append("builder")
        nodes.append(SimpleNamespace(id=node_id, roles=tuple(roles)))
    return SimpleNamespace(nodes=tuple(nodes))


def test_plan_pins_builder_and_never_describes_operator_image_archives() -> None:
    plan = remote_build.create_remote_build_plan(inventory(), PROVENANCE)

    assert plan.builder_node_id == "node-3"
    assert str(plan.workspace) == f"/var/lib/massar/builds/{RELEASE}"
    assert plan.as_dict()["operatorImageArchives"] == "forbidden"
    assert plan.image_archives == {
        name: plan.artifact_root / f"{name}.tar" for name in remote_build.IMAGES
    }


@pytest.mark.parametrize(
    ("builder_on", "extra_builder"),
    [("node-1", False), ("node-3", True), ("none", False)],
)
def test_plan_refuses_missing_ambiguous_or_wrong_builder(
    builder_on: str,
    extra_builder: bool,
) -> None:
    with pytest.raises(remote_build.RemoteBuildContractError, match="exactly one builder"):
        remote_build.create_remote_build_plan(
            inventory(builder_on=builder_on, extra_builder=extra_builder), PROVENANCE
        )


def test_plan_refuses_builder_on_a_data_brick() -> None:
    candidate = inventory()
    candidate.nodes[2].roles = (*candidate.nodes[2].roles, "file-data-primary")
    with pytest.raises(remote_build.RemoteBuildContractError, match="data-brick"):
        remote_build.create_remote_build_plan(candidate, PROVENANCE)


@pytest.mark.parametrize("field", ["releaseId", "sourceStateSha256"])
def test_plan_refuses_invalid_immutable_provenance(field: str) -> None:
    malformed = {**PROVENANCE, field: "not-a-verified-value"}
    with pytest.raises(remote_build.RemoteBuildContractError, match="invalid"):
        remote_build.create_remote_build_plan(inventory(), malformed)


def test_remote_build_contract_has_no_process_or_local_filesystem_execution() -> None:
    source = MODULE_PATH.read_text(encoding="utf-8")
    assert "subprocess" not in source
    assert ".write_" not in source
    assert "docker" not in source.lower()
