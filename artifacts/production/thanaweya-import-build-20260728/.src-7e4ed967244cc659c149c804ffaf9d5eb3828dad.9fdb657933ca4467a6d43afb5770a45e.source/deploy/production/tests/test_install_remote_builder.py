from __future__ import annotations
import importlib.util, sys
from pathlib import Path
from types import SimpleNamespace
import pytest
ROOT=Path(__file__).resolve().parents[3]; SCRIPTS=ROOT/"deploy/production/scripts"
def load(name):
    spec=importlib.util.spec_from_file_location(name,SCRIPTS/f"{name}.py"); module=importlib.util.module_from_spec(spec); sys.modules[name]=module; spec.loader.exec_module(module); return module
load("ssh_transport"); load("release_images"); load("remote_build_release"); load("remote_distribution_plan"); load("remote_distribution_runner"); load("remote_builder_workflow"); load("clusterctl"); installer=load("install_remote_builder")
def inventory(builder="node-3"):
    return SimpleNamespace(cluster={"ssh_user":"massar-ops"},nodes=tuple(SimpleNamespace(id=f"node-{i}",public_address=f"192.0.2.{i}",roles=("builder",) if f"node-{i}"==builder else ()) for i in (1,2,3)))
class Transport:
    def __init__(self): self.copies=[]; self.commands=[]
    def copy(self,*args,**kwargs): self.copies.append(args)
    def run(self,*args,**kwargs): self.commands.append(args)
def test_installer_targets_node3_and_installs_only_helper_and_sudoers():
    transport=Transport(); installer.install(inventory(),transport)
    assert len(transport.copies)==2 and all(args[0].node_id=="node-3" for args in transport.copies)
    script=transport.commands[0][1][-1]; assert "visudo -cf" in script and "root:root:755" in script and "trap 'rm -f" in script
    assert "if ! test -e /etc/massar/node-id" in script and "root:root:644" in script and "$(cat /etc/massar/node-id)\" = node-3" in script
    assert "/usr/sbin/visudo -cf /tmp/massar-remote-builder.sudoers" in script
    assert "sudo /usr/sbin/visudo" not in script
    assert "stat -c '%U:%G:%a' /etc/sudoers.d" not in script
    assert "sudo -n -l /usr/local/sbin/massar-remote-builder" in script
    assert "backup" not in script and "secret" not in script
def test_installer_refuses_any_non_node3_builder():
    with pytest.raises(installer.RemoteBuilderInstallError,match="node-3") : installer.target(inventory("node-1"))
def test_dry_run_does_not_create_transport(monkeypatch,tmp_path):
    path=tmp_path/"inventory.json"; path.write_text((ROOT/"deploy/production/inventory/production.yml").read_text())
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE","/dry-run-known-hosts")
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE","/dry-run-identity")
    monkeypatch.setattr(sys,"argv",["install_remote_builder.py","--inventory",str(path),"--known-hosts","/missing","--identity","/missing","--node","node-3","--dry-run"])
    assert installer.main()==0
