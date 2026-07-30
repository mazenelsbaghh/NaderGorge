from __future__ import annotations
import importlib.util, sys
from pathlib import Path
from types import SimpleNamespace
import pytest
ROOT=Path(__file__).resolve().parents[3]; SCRIPTS=ROOT/"deploy/production/scripts"
def load(name):
    spec=importlib.util.spec_from_file_location(name,SCRIPTS/f"{name}.py"); module=importlib.util.module_from_spec(spec); sys.modules[name]=module; spec.loader.exec_module(module); return module
load("ssh_transport"); load("release_images"); load("remote_build_release"); load("remote_distribution_plan"); load("remote_distribution_runner"); workflow=load("remote_builder_workflow")
RELEASE="src-"+"a"*40
def remote_plan(): return SimpleNamespace(workspace=Path(f"/var/lib/massar/builds/{RELEASE}"))
class Result:
    def __init__(self,state,code=0): self.stdout=state; self.returncode=code
class Transport:
    def __init__(self,state): self.state=state; self.fetched=[]
    def run(self,*args,**kwargs): return Result(self.state)
    def fetch(self,*args,**kwargs): self.fetched.append(args); args[2].write_text('{"cached":true}')
def test_existing_cache_manifest_is_fetched_without_source_stream(tmp_path):
    transport=Transport("present"); destination=tmp_path/"manifest.json"
    result=workflow.fetch_cached_builder_manifest(transport=transport,target=object(),remote=remote_plan(),destination=destination)
    assert result==destination and len(transport.fetched)==1
def test_absent_cache_does_not_fetch_or_create_source(tmp_path):
    transport=Transport("absent")
    assert workflow.fetch_cached_builder_manifest(transport=transport,target=object(),remote=remote_plan(),destination=tmp_path/"manifest.json") is None
    assert transport.fetched==[]
def test_unsafe_or_unknown_cache_probe_fails_closed(tmp_path):
    for state in ("unsafe","other"):
        with pytest.raises(workflow.RemoteBuilderWorkflowError): workflow.fetch_cached_builder_manifest(transport=Transport(state),target=object(),remote=remote_plan(),destination=tmp_path/f"{state}.json")

def test_cached_build_still_materializes_source_snapshot_for_release_bundle(tmp_path, monkeypatch):
    release = RELEASE
    remote = SimpleNamespace(
        release_id=release,
        source_state_sha256="a" * 64,
        builder_node_id="node-3",
        workspace=Path(f"/var/lib/massar/builds/{release}"),
        staging_source_root=Path(f"/tmp/massar-build-source-{release}"),
    )
    node = SimpleNamespace(id="node-3", public_address="192.0.2.3")
    inventory = SimpleNamespace(cluster={"ssh_user":"massar-ops"}, nodes=(node,))
    seen = {}

    monkeypatch.setattr(workflow, "create_remote_build_plan", lambda *_: remote)
    def snapshot(_repository, destination, _digest):
        destination.mkdir(parents=True)
        (destination / "deploy" / "production").mkdir(parents=True)
        seen["snapshot"] = destination
    monkeypatch.setattr(workflow, "create_source_snapshot", snapshot)
    def cached(*, destination, **_kwargs):
        destination.write_text("{}")
        return destination
    monkeypatch.setattr(workflow, "fetch_cached_builder_manifest", cached)
    plan = SimpleNamespace(images={})
    monkeypatch.setattr(workflow, "create_remote_distribution_plan", lambda *_: plan)
    def bundle(source, output):
        assert source == seen["snapshot"] and source.is_dir()
        archive = output / "release-files.tar.gz"
        archive.write_bytes(b"bundle")
        seen["bundle"] = True
        return archive
    monkeypatch.setattr(workflow, "create_release_bundle", bundle)
    monkeypatch.setattr(
        workflow,
        "create_release_manifest_v2",
        lambda *_args, **_kwargs: {"schemaVersion": 2, "digestParity": False},
    )
    class Runner:
        def __init__(self, **_kwargs): pass
        def run(self): return {"digestParity": True}
    monkeypatch.setattr(workflow, "RemoteDistributionRunner", Runner)
    monkeypatch.setattr(workflow, "publish_final_manifest", lambda *_args: None)

    result = workflow.run_remote_builder_workflow(
        repository=tmp_path,
        output=tmp_path / "output",
        inventory=inventory,
        transport=object(),
        provenance={"releaseId": release, "sourceStateSha256": "a" * 64},
        created_at="2026-07-28T00:00:00Z",
    )

    assert result["digestParity"] is True
    assert seen["bundle"] is True
