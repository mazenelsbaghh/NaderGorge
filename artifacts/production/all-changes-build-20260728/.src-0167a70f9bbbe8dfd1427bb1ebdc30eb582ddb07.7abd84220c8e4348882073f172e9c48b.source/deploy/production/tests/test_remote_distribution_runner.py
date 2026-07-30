from __future__ import annotations
import importlib.util, sys
from pathlib import Path
from types import SimpleNamespace
import pytest

ROOT=Path(__file__).resolve().parents[3]; SCRIPTS=ROOT/"deploy/production/scripts"
def load(name):
    spec=importlib.util.spec_from_file_location(name,SCRIPTS/f"{name}.py"); module=importlib.util.module_from_spec(spec); sys.modules[name]=module; spec.loader.exec_module(module); return module
load("ssh_transport"); load("release_images"); load("remote_build_release"); planner=load("remote_distribution_plan"); runner_module=load("remote_distribution_runner")
RELEASE="src-"+"a"*40
def inventory():
    return SimpleNamespace(cluster={"ssh_user":"massar-ops"},nodes=tuple(SimpleNamespace(id=node,public_address=f"192.0.2.{i}",roles=("builder",) if node=="node-3" else ()) for i,node in enumerate(planner.NODE_IDS,1)))
def plan():
    return planner.create_remote_distribution_plan(inventory(),{"schemaVersion":1,"status":"success","clusterId":"massar-production","builderNodeId":"node-3","releaseId":RELEASE,"sourceStateSha256":"a"*64,"platform":"linux/amd64","images":{n:f"sha256:{i:064x}" for i,n in enumerate(planner.IMAGES,1)},"artifacts":{n:{"filename":f"{n}.tar","sha256":f"{i:064x}"} for i,n in enumerate(planner.IMAGES,1)}})
class Transport:
    def __init__(self,fail=None): self.calls=[]; self.fail=fail
    def run(self,target,command,**kwargs):
        self.calls.append((target.node_id,command[-1]))
        if self.fail and self.fail in command[-1]: raise RuntimeError("tampered archive")
    def copy(self,*args,**kwargs): self.calls.append((args[0].node_id,"copy"))
    def stream_remote_file(self,*args,**kwargs): self.calls.append((args[2].node_id,"stream"))
def runner(tmp,transport):
    bundle=tmp/"bundle"; manifest=tmp/"manifest"; bundle.write_bytes(b"x"); manifest.write_bytes(b"x")
    return runner_module.RemoteDistributionRunner(inventory=inventory(),transport=transport,plan=plan(),bundle=bundle,manifest=manifest)
def test_verifies_all_images_before_install(tmp_path):
    transport=Transport(); final=runner(tmp_path,transport).run(); assert final["digestParity"] is True
    for node in planner.NODE_IDS:
        rows=[text for seen,text in transport.calls if seen==node]; install=next(i for i,text in enumerate(rows) if "install-release" in text); verified=[text for text in rows[:install] if "sha256sum -c -" in text]; assert len(verified)==4
        if node=="node-3": assert all("docker load" not in text for text in verified)
        else: assert all("docker load" in text for text in verified)

def test_builder_verifies_its_cached_images_in_place_without_self_ssh_relay(tmp_path):
    transport=Transport(); runner(tmp_path,transport).run()
    assert not any(node=="node-3" and text=="stream" for node,text in transport.calls)
    node3_rows=[text for node,text in transport.calls if node=="node-3"]
    direct_checks=[text for text in node3_rows if "/var/lib/massar/builds/" in text and "sha256sum -c -" in text]
    assert len(direct_checks)==4
    assert all("docker load" not in text and "docker image inspect" in text for text in direct_checks)
def test_tamper_stops_partial_run_before_install(tmp_path):
    transport=Transport("worker.tar")
    with pytest.raises(RuntimeError,match="tampered"): runner(tmp_path,transport).run()
    assert not any("install-release" in text for _,text in transport.calls); assert not any(node=="node-2" for node,_ in transport.calls)
def test_executor_command_is_node3_cache_pinned():
    command=runner_module.builder_executor_command(plan()); assert command[:2]==("sudo","/usr/local/sbin/massar-remote-builder"); assert f"/var/lib/massar/builds/{RELEASE}" in command; assert command[-1]=="--yes"
