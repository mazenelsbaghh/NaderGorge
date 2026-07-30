#!/usr/bin/env python3
"""Reviewed remote-builder release workflow; local disk receives no image tar."""
from __future__ import annotations
import json, os, shutil, uuid
from pathlib import Path
from typing import Any, Mapping
from remote_build_release import create_remote_build_plan
from remote_distribution_plan import create_remote_distribution_plan
from remote_distribution_runner import RemoteDistributionRunner, builder_executor_command
from release_images import create_release_bundle, create_source_snapshot, publish_final_manifest, write_json_atomic
from ssh_transport import SshTarget

class RemoteBuilderWorkflowError(RuntimeError): pass
def _target(inventory, node): return SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"])
def _read(path):
    try: value=json.loads(path.read_text(encoding="utf-8"))
    except (OSError,json.JSONDecodeError) as exc: raise RemoteBuilderWorkflowError("remote builder manifest is invalid") from exc
    if not isinstance(value,dict): raise RemoteBuilderWorkflowError("remote builder manifest is invalid")
    return value
def fetch_cached_builder_manifest(*, transport, target, remote, destination: Path) -> Path | None:
    manifest = remote.workspace / "builder-manifest.json"
    probe = transport.run(target,("bash","-lc",f"if ! test -e {manifest}; then printf absent; elif test -f {manifest} && ! test -L {manifest}; then printf present; else printf unsafe; fi"),timeout_seconds=30,check=False)
    state = probe.stdout.strip()
    if probe.returncode != 0 or state == "unsafe": raise RemoteBuilderWorkflowError("remote builder cache manifest is unsafe")
    if state == "absent": return None
    if state != "present": raise RemoteBuilderWorkflowError("remote builder cache manifest probe is invalid")
    transport.fetch(target,str(manifest),destination,timeout_seconds=120,max_bytes=1024*1024)
    return destination
def run_remote_builder_workflow(*, repository: Path, output: Path, inventory: object, transport: object, provenance: Mapping[str,Any], created_at: str) -> dict[str,Any]:
    if output.exists() or output.is_symlink(): raise RemoteBuilderWorkflowError("remote release output already exists")
    remote=create_remote_build_plan(inventory,provenance); nodes=tuple(inventory.nodes); builder=next(node for node in nodes if node.id==remote.builder_node_id)
    temporary=output.parent/f".{remote.release_id}.{uuid.uuid4().hex}.remote"; snapshot=output.parent/f".{remote.release_id}.{uuid.uuid4().hex}.source"
    try:
        temporary.mkdir(mode=0o700,parents=True)
        builder_manifest=temporary/"builder-manifest.json"
        # The release bundle is assembled locally from the verified source
        # snapshot even when the OCI build is already cached remotely.  A
        # cache hit must skip only the upload/build, not silently turn the
        # deployment bundle into an empty archive because `snapshot` was
        # never materialised.
        create_source_snapshot(repository,snapshot,remote.source_state_sha256)
        cached = fetch_cached_builder_manifest(transport=transport,target=_target(inventory,builder),remote=remote,destination=builder_manifest)
        if cached is None:
            transport.stream_directory(_target(inventory,builder),snapshot,str(remote.staging_source_root))
            transport.run(_target(inventory,builder),builder_executor_command(remote),timeout_seconds=3600)
            transport.fetch(_target(inventory,builder),str(remote.workspace/"builder-manifest.json"),builder_manifest,timeout_seconds=120,max_bytes=1024*1024)
            transport.fetch(_target(inventory,builder),str(remote.workspace/"build-evidence.json"),temporary/"build-evidence.json",timeout_seconds=120,max_bytes=1024*1024)
        plan=create_remote_distribution_plan(inventory,_read(builder_manifest)); create_release_bundle(snapshot,temporary)
        initial={"schemaVersion":1,**dict(provenance),"createdAt":created_at,"platform":"linux/amd64","images":dict(plan.images),"status":"success","nodeCount":3,"digestParity":False}
        manifest=temporary/"manifest.json"; write_json_atomic(manifest,initial)
        final={**initial,**RemoteDistributionRunner(inventory=inventory,transport=transport,plan=plan,bundle=temporary/"release-files.tar.gz",manifest=manifest).run()}
        write_json_atomic(manifest,final); publish_final_manifest(temporary,remote.release_id,nodes,inventory.cluster["ssh_user"],transport)
        os.rename(temporary,output); return final
    finally:
        shutil.rmtree(snapshot,ignore_errors=True); shutil.rmtree(temporary,ignore_errors=True)
