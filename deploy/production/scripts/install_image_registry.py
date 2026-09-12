#!/usr/bin/env python3
"""Install the pinned node-3 registry and stable per-node mTLS credentials."""
from __future__ import annotations

import argparse
import hashlib
import ipaddress
import json
import os
import shlex
import subprocess
import tempfile
import uuid
from pathlib import Path

from clusterctl import load_inventory, operator_transport, target
from remote_build_release import select_builder

REGISTRY_IMAGE = "registry@sha256:7518da9b12dd746278282a729dee2e65eabdeb449db4d0b28d46ef6e90308f58"
SECRET_ROOT = Path.home() / ".config/massar/image-registry"


def openssl(*arguments: str) -> None:
    subprocess.run(["openssl", *arguments], check=True, capture_output=True)


def issue_certificate(root: Path, name: str, extensions: str) -> None:
    openssl("req", "-new", "-newkey", "rsa:3072", "-nodes", "-subj", f"/CN=massar-{name}",
            "-keyout", str(root / f"{name}.key"), "-out", str(root / f"{name}.csr"))
    extension_file = root / f"{name}.ext"
    extension_file.write_text(extensions)
    openssl("x509", "-req", "-in", str(root / f"{name}.csr"), "-CA", str(root / "ca.crt"),
            "-CAkey", str(root / "ca.key"), "-CAcreateserial", "-days", "825", "-sha256",
            "-extfile", str(extension_file), "-out", str(root / f"{name}.crt"))
    os.chmod(root / f"{name}.key", 0o600)


def prepare_certificates(root: Path, address: str) -> None:
    required = {"ca.crt", "ca.key", "server.crt", "server.key"}
    required.update(f"{node}.{suffix}" for node in ("node-1", "node-2", "node-3") for suffix in ("crt", "key"))
    if root.exists():
        if root.is_symlink() or root.stat().st_mode & 0o077 or not all((root / name).is_file() for name in required):
            raise RuntimeError("registry credentials are incomplete or insecure; refusing automatic rotation")
        for name in required:
            path = root / name
            if path.is_symlink() or (name.endswith(".key") and path.stat().st_mode & 0o077):
                raise RuntimeError("registry credential permissions are unsafe")
        openssl("x509", "-in", str(root / "server.crt"), "-noout", "-checkip", address)
        return
    root.mkdir(mode=0o700, parents=True)
    openssl("req", "-x509", "-newkey", "rsa:3072", "-nodes", "-days", "3650", "-sha256",
            "-subj", "/CN=Massar private image registry CA", "-addext", "basicConstraints=critical,CA:TRUE",
            "-addext", "keyUsage=critical,keyCertSign,cRLSign",
            "-keyout", str(root / "ca.key"), "-out", str(root / "ca.crt"))
    os.chmod(root / "ca.key", 0o600)
    issue_certificate(root, "server", f"subjectAltName=IP:{address}\nextendedKeyUsage=serverAuth\n")
    for node in ("node-1", "node-2", "node-3"):
        issue_certificate(root, node, "extendedKeyUsage=clientAuth\n")


def registry_config(endpoint: str) -> dict:
    return {
        "version": 0.1, "log": {"level": "warn", "accesslog": {"disabled": True}},
        "storage": {"filesystem": {"rootdirectory": "/var/lib/registry"}},
        "http": {"addr": endpoint, "tls": {
            "certificate": "/certs/server.crt", "key": "/certs/server.key",
            "clientcas": ["/certs/ca.crt"], "clientauth": "require-and-verify-client-cert",
        }},
    }


def registry_unit() -> str:
    return f"""[Unit]
Description=Massar private image registry over WireGuard
Requires=docker.service
After=docker.service network-online.target wg-quick@wg0.service
[Service]
Restart=always
RestartSec=5
ExecStartPre=-/usr/bin/docker rm -f massar-image-registry
ExecStart=/usr/bin/docker run --rm --name massar-image-registry --network host --user 0:0 --read-only --cap-drop ALL --security-opt no-new-privileges --log-opt max-size=10m --log-opt max-file=3 --env OTEL_TRACES_EXPORTER=none --mount type=bind,src=/etc/massar/registry,dst=/certs,readonly --mount type=bind,src=/var/lib/massar/image-registry,dst=/var/lib/registry --entrypoint /bin/registry {REGISTRY_IMAGE} serve /certs/config.json
ExecStop=/usr/bin/docker stop -t 30 massar-image-registry
TimeoutStartSec=90
[Install]
WantedBy=multi-user.target
"""


def install_file(transport, host, source: Path, destination: str) -> None:
    staged = f"/tmp/massar-registry-{source.name}"
    transport.copy(host, source, staged, timeout_seconds=120)
    checksum = hashlib.sha256(source.read_bytes()).hexdigest()
    script = (
        "set -euo pipefail; "
        f"trap 'rm -f {shlex.quote(staged)}' EXIT; "
        f"printf '%s  %s\\n' '{checksum}' '{staged}' | sha256sum -c - >/dev/null; "
        f"sudo /usr/bin/install -d -m 0700 -o root -g root {shlex.quote(str(Path(destination).parent))}; "
    )
    if source.suffix in {".key", ".crt"}:
        script += f"test ! -L {shlex.quote(destination)}; "
    mode = "0600" if source.suffix == ".key" else "0644"
    script += f"sudo /usr/bin/install -m {mode} -o root -g root {shlex.quote(staged)} {shlex.quote(destination)}"
    transport.run(host, ("bash", "-lc", script), timeout_seconds=120)


def repair_ca_usage(root: Path) -> None:
    """Repair the original CA certificate without changing its signing key."""
    marker = root / "ca-usage-repair.json"
    if marker.exists():
        recorded = json.loads(marker.read_text())
        if hashlib.sha256((root / "ca.crt").read_bytes()).hexdigest() != recorded["repairedCaSha256"]:
            pending = root / "ca-repaired.crt"
            if not pending.is_file() or hashlib.sha256(pending.read_bytes()).hexdigest() != recorded["repairedCaSha256"]:
                raise RuntimeError("CA repair state does not match its verified certificate")
            pending.replace(root / "ca.crt")
        return
    old_digest = hashlib.sha256((root / "ca.crt").read_bytes()).hexdigest()
    repaired = root / "ca-repaired.crt"
    openssl("req", "-x509", "-key", str(root / "ca.key"), "-days", "3650", "-sha256",
            "-subj", "/CN=Massar private image registry CA", "-addext", "basicConstraints=critical,CA:TRUE",
            "-addext", "keyUsage=critical,keyCertSign,cRLSign", "-out", str(repaired))
    for name in ("server", "node-1", "node-2", "node-3"):
        openssl("verify", "-x509_strict", "-CAfile", str(repaired), str(root / f"{name}.crt"))
    marker.write_text(json.dumps({"previousCaSha256": old_digest, "repairedCaSha256": hashlib.sha256(repaired.read_bytes()).hexdigest()}))
    repaired.replace(root / "ca.crt")


def install(inventory, transport, trusted_previous_ca: str | None = None) -> None:
    builder = select_builder(inventory)
    address = str(ipaddress.ip_address(builder.overlay_address))
    endpoint = f"{address}:5443"
    prepare_certificates(SECRET_ROOT, address)
    with tempfile.TemporaryDirectory(prefix="massar-registry-config-") as temporary:
        directory = Path(temporary)
        config = directory / "image-registry.json"
        registry_identity = {"endpoint": endpoint, "caSha256": hashlib.sha256((SECRET_ROOT / "ca.crt").read_bytes()).hexdigest()}
        config.write_text(json.dumps(registry_identity))
        # Validate every existing trust root before modifying any node.
        for node in inventory.nodes:
            observed = transport.run(target(inventory, node), ("bash", "-lc", "if test -f /etc/massar/image-registry.json; then cat /etc/massar/image-registry.json; else printf absent; fi"), timeout_seconds=30)
            if observed.stdout.strip() != "absent":
                previous = json.loads(observed.stdout)
                allowed = previous == registry_identity or (trusted_previous_ca is not None and previous == {"endpoint": endpoint, "caSha256": trusted_previous_ca})
                if not allowed:
                    raise RuntimeError("registry trust differs from the saved credentials; refusing rotation")
        for node in inventory.nodes:
            host = target(inventory, node)
            certs = f"/etc/docker/certs.d/{endpoint}"
            for filename, destination in (("ca.crt", "ca.crt"), (f"{node.id}.crt", "client.cert"), (f"{node.id}.key", "client.key")):
                install_file(transport, host, SECRET_ROOT / filename, f"{certs}/{destination}")
            # Preserve /etc/massar's existing traversability for other service readers.
            staged = "/tmp/massar-image-registry.json"
            transport.copy(host, config, staged, timeout_seconds=60)
            transport.run(host, ("sudo", "/usr/bin/install", "-m", "0644", "-o", "root", "-g", "root", staged, "/etc/massar/image-registry.json"), timeout_seconds=60)
            transport.run(host, ("rm", "-f", staged), timeout_seconds=30)
        host = target(inventory, builder)
        for filename in ("ca.crt", "server.crt", "server.key"):
            install_file(transport, host, SECRET_ROOT / filename, f"/etc/massar/registry/{filename}")
        service_config = directory / "config.json"
        service_config.write_text(json.dumps(registry_config(endpoint)))
        install_file(transport, host, service_config, "/etc/massar/registry/config.json")
        unit = directory / "massar-image-registry.service"
        unit.write_text(registry_unit())
        staged_unit = "/tmp/massar-image-registry.service"
        transport.copy(host, unit, staged_unit, timeout_seconds=60)
        transport.run(host, ("sudo", "/usr/bin/docker", "pull", REGISTRY_IMAGE), timeout_seconds=300)
        transport.run(host, ("sudo", "/usr/bin/install", "-d", "-m", "0700", "/var/lib/massar/image-registry"), timeout_seconds=60)
        transport.run(host, ("sudo", "/usr/bin/install", "-m", "0644", staged_unit, "/etc/systemd/system/massar-image-registry.service"), timeout_seconds=60)
        transport.run(host, ("rm", "-f", staged_unit), timeout_seconds=30)
        transport.run(host, ("sudo", "/usr/bin/systemctl", "daemon-reload"), timeout_seconds=60)
        transport.run(host, ("sudo", "/usr/bin/systemctl", "enable", "--now", "massar-image-registry"), timeout_seconds=90)
        if trusted_previous_ca is not None:
            transport.run(host, ("sudo", "/usr/bin/systemctl", "restart", "massar-image-registry"), timeout_seconds=90)


def probe_registry(inventory, transport) -> None:
    endpoint = f"{select_builder(inventory).overlay_address}:5443"
    for node in inventory.nodes:
        host = target(inventory, node)
        staging = f"/tmp/massar-registry-probe-{uuid.uuid4().hex}"
        transport.run(host, ("install", "-d", "-m", "0700", staging), timeout_seconds=30)
        try:
            for filename in ("ca.crt", f"{node.id}.crt", f"{node.id}.key"):
                transport.copy(host, SECRET_ROOT / filename, f"{staging}/{filename}", timeout_seconds=60)
            arguments = ("curl", "--fail", "--silent", "--show-error", "--max-time", "10", "--cacert", f"{staging}/ca.crt")
            authenticated = transport.run(host, (*arguments, "--retry", "5", "--retry-connrefused", "--cert", f"{staging}/{node.id}.crt", "--key", f"{staging}/{node.id}.key", f"https://{endpoint}/v2/"), timeout_seconds=60)
            if authenticated.stdout.strip() != "{}":
                raise RuntimeError(f"{node.id}: registry did not return its API health response")
            anonymous = transport.run(host, (*arguments, f"https://{endpoint}/v2/"), timeout_seconds=20, check=False)
            if anonymous.returncode == 0:
                raise RuntimeError("registry accepted a client without an authenticated certificate")
            print(json.dumps({"node": node.id, "registryReachable": True, "anonymousRejected": True}))
        finally:
            transport.run(host, ("rm", "-rf", "--", staging), timeout_seconds=30)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, default=Path("deploy/production/inventory/production.yml"))
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--dry-run", action="store_true")
    action.add_argument("--yes", action="store_true")
    action.add_argument("--probe", action="store_true")
    parser.add_argument("--repair-ca-usage", action="store_true")
    args = parser.parse_args()
    inventory = load_inventory(args.inventory, require_operator_files=True)
    builder = select_builder(inventory)
    if args.probe:
        probe_registry(inventory, operator_transport(inventory))
        return
    if args.dry_run:
        print(json.dumps({"status": "dry-run", "repairCaKeyUsage": args.repair_ca_usage, "registryNode": builder.id, "endpoint": f"{builder.overlay_address}:5443", "image": REGISTRY_IMAGE,
                          "steps": ["stable-external-mtls-certificates", "install-client-trust-all-nodes", "pull-pinned-registry-image", "start-private-registry-only"], "applicationRestarts": False}))
        return
    trusted_previous_ca = None
    if args.repair_ca_usage:
        repair_ca_usage(SECRET_ROOT)
        trusted_previous_ca = json.loads((SECRET_ROOT / "ca-usage-repair.json").read_text())["previousCaSha256"]
    install(inventory, operator_transport(inventory), trusted_previous_ca)
    probe_registry(inventory, operator_transport(inventory))
    print(json.dumps({"status": "success", "registryNode": builder.id}))


if __name__ == "__main__":
    main()
