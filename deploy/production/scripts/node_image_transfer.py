#!/usr/bin/env python3
"""Transfer release archives over WireGuard using a receive-only SSH identity."""

from __future__ import annotations

import hashlib
import ipaddress
import json
import os
import re
import shlex
import signal
import stat
import subprocess
import sys
import time
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO


RELEASE_RE = re.compile(r"(?:git|src)-[0-9a-f]{40}")
SHA256_RE = re.compile(r"[0-9a-f]{64}")
IMAGES = ("backend", "frontend", "worker", "migrator")
MAX_ARCHIVE_BYTES = 16 * 1024**3
TRANSFER_TIMEOUT_SECONDS = 1200
CONFIG_NAME = ".config/massar-image-transfer.json"
IDENTITY_NAME = ".ssh/massar-image-transfer-ed25519"
HOSTS_NAME = ".ssh/massar-image-transfer-known-hosts"


class ImageTransferError(RuntimeError):
    pass


@dataclass(frozen=True)
class ArchiveReceipt:
    release: str
    image: str
    sha256: str
    size: int

    def __post_init__(self) -> None:
        if not RELEASE_RE.fullmatch(self.release) or self.image not in IMAGES:
            raise ImageTransferError("only immutable release image archives are accepted")
        if not SHA256_RE.fullmatch(self.sha256) or not 0 < self.size <= MAX_ARCHIVE_BYTES:
            raise ImageTransferError("archive checksum or byte count is invalid")


def configuration() -> dict:
    if Path("/etc/massar/cluster-id").read_text().strip() != "massar-production":
        raise ImageTransferError("image transfer requires the production cluster marker")
    config_path = Path.home() / CONFIG_NAME
    metadata = config_path.lstat()
    if not stat.S_ISREG(metadata.st_mode) or metadata.st_uid != os.getuid() or metadata.st_mode & 0o077:
        raise ImageTransferError("image transfer configuration must be a private owned file")
    return json.loads(config_path.read_text())


def receive_request(original_command: str) -> ArchiveReceipt:
    fields = shlex.split(original_command)
    if len(fields) != 5 or fields[0] != "receive" or not fields[4].isascii() or not fields[4].isdigit():
        raise ImageTransferError("this SSH identity can only receive release image archives")
    return ArchiveReceipt(fields[1], fields[2], fields[3], int(fields[4]))


def open_staging_directory(staging_root: Path, receipt: ArchiveReceipt) -> int:
    directory = staging_root / f"massar-{receipt.release}"
    descriptor = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    metadata = os.fstat(descriptor)
    space = os.fstatvfs(descriptor)
    if metadata.st_uid != os.getuid() or stat.S_IMODE(metadata.st_mode) != 0o700:
        os.close(descriptor)
        raise ImageTransferError("archive staging directory must be owned and mode 0700")
    if space.f_bavail * space.f_frsize < receipt.size + 8 * 1024**2:
        os.close(descriptor)
        raise ImageTransferError("insufficient free space for the archive")
    return descriptor


def copy_verified_archive(source: BinaryIO, destination: BinaryIO, receipt: ArchiveReceipt) -> None:
    checksum = hashlib.sha256()
    copied = 0
    while chunk := source.read(min(1024**2, receipt.size - copied + 1)):
        copied += len(chunk)
        if copied > receipt.size:
            raise ImageTransferError("archive exceeds its declared byte count")
        destination.write(chunk)
        checksum.update(chunk)
    if copied != receipt.size or checksum.hexdigest() != receipt.sha256:
        raise ImageTransferError("archive byte count or checksum did not match")
    destination.flush()
    os.fsync(destination.fileno())


def receive_archive(source: BinaryIO, receipt: ArchiveReceipt, staging_root: Path) -> None:
    directory_fd = open_staging_directory(staging_root, receipt)
    partial = f".{receipt.image}.{uuid.uuid4().hex}.partial"
    try:
        descriptor = os.open(partial, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=directory_fd)
        with os.fdopen(descriptor, "wb") as destination:
            copy_verified_archive(source, destination, receipt)
        # A hard link publishes verified bytes atomically without replacing an
        # existing file, including a symlink planted at the destination name.
        os.link(partial, f"{receipt.image}.tar", src_dir_fd=directory_fd, dst_dir_fd=directory_fd, follow_symlinks=False)
        os.fsync(directory_fd)
    finally:
        try:
            os.unlink(partial, dir_fd=directory_fd)
        except FileNotFoundError:
            pass
        os.close(directory_fd)


def require_wireguard_route(config: dict, target_id: str) -> str:
    if config["nodeId"] != "node-3" or target_id not in ("node-1", "node-2"):
        raise ImageTransferError("only node-3 can send to node-1 or node-2")
    destination = str(ipaddress.ip_address(config["targets"][target_id]))
    route = subprocess.run(["ip", "-j", "route", "get", destination], capture_output=True, text=True, check=True)
    routes = json.loads(route.stdout)
    if len(routes) != 1 or routes[0].get("dev") != "wg0" or routes[0].get("prefsrc") != config["builderAddress"]:
        raise ImageTransferError("image transfer requires the configured WireGuard route")
    return destination


def archive_source(release: str, image: str) -> Path:
    if not RELEASE_RE.fullmatch(release) or image not in IMAGES:
        raise ImageTransferError("invalid release archive source")
    source = Path("/var/lib/massar/builds") / release / "artifacts" / f"{image}.tar"
    if source.resolve(strict=True) != source or not source.is_file():
        raise ImageTransferError("release archive must be a regular file without symlinks")
    return source


def transfer_ssh_command(config: dict, target_id: str, receipt: ArchiveReceipt) -> list[str]:
    destination = require_wireguard_route(config, target_id)
    identity, hosts = (Path.home() / name for name in (IDENTITY_NAME, HOSTS_NAME))
    for path in (identity, hosts):
        metadata = path.lstat()
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_uid != os.getuid() or stat.S_IMODE(metadata.st_mode) != 0o600:
            raise ImageTransferError("transfer identity and host pins must be owned regular mode 0600 files")
    return [
        "ssh", "-F", "/dev/null", "-T", "-o", "BatchMode=yes", "-o", "IdentitiesOnly=yes",
        "-o", "StrictHostKeyChecking=yes", "-o", "GlobalKnownHostsFile=/dev/null",
        "-o", f"UserKnownHostsFile={hosts}", "-o", f"HostKeyAlias={target_id}",
        "-o", "ConnectTimeout=10", "-o", "ServerAliveInterval=15", "-o", "ServerAliveCountMax=4",
        "-o", "ForwardAgent=no", "-o", "ClearAllForwardings=yes", "-o", "UpdateHostKeys=no",
        "-b", config["builderAddress"], "-i", str(identity), f"massar-ops@{destination}",
        shlex.join(["receive", receipt.release, receipt.image, receipt.sha256, str(receipt.size)]),
    ]


def send_archive(config: dict, target_id: str, release: str, image: str) -> dict:
    source = archive_source(release, image)
    started = time.monotonic()
    with source.open("rb") as archive:
        receipt = ArchiveReceipt(release, image, hashlib.file_digest(archive, "sha256").hexdigest(), os.fstat(archive.fileno()).st_size)
        archive.seek(0)
        completed = subprocess.run(transfer_ssh_command(config, target_id, receipt), stdin=archive,
                                   capture_output=True, text=True, timeout=TRANSFER_TIMEOUT_SECONDS, check=True)
    received = json.loads(completed.stdout)
    if received != {"sha256": receipt.sha256, "bytes": receipt.size}:
        raise ImageTransferError("receiver did not acknowledge the verified archive")
    elapsed = time.monotonic() - started
    return {"sourceNode": "node-3", "targetNode": target_id, "image": image, "route": "wireguard",
            "bytes": receipt.size, "sha256": receipt.sha256, "elapsedSeconds": round(elapsed, 3),
            "mebibytesPerSecond": round(receipt.size / 1024**2 / elapsed, 2)}


def interrupt_transfer(signum: int, _frame: object) -> None:
    raise TimeoutError(f"image transfer interrupted by signal {signum}")


def main() -> None:
    config = configuration()
    for signum in (signal.SIGTERM, signal.SIGHUP, signal.SIGALRM):
        signal.signal(signum, interrupt_transfer)
    signal.alarm(TRANSFER_TIMEOUT_SECONDS + 30)
    if sys.argv[1:] == ["receive"] and config["nodeId"] in ("node-1", "node-2"):
        receipt = receive_request(os.environ.get("SSH_ORIGINAL_COMMAND", ""))
        receive_archive(sys.stdin.buffer, receipt, Path("/tmp"))
        print(json.dumps({"sha256": receipt.sha256, "bytes": receipt.size}))
    elif len(sys.argv) == 5 and sys.argv[1] == "send":
        print(json.dumps(send_archive(config, *sys.argv[2:])))
    else:
        raise ImageTransferError("unsupported image transfer command")


if __name__ == "__main__":
    try:
        main()
    except ImageTransferError as exc:
        print(f"node image transfer failed: {exc}", file=sys.stderr)
        raise SystemExit(6)
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        # SSH diagnostics can contain network identities; the operation name and
        # exception class suffice for the operator's bounded follow-up probe.
        print(f"node image transfer failed: {type(exc).__name__}", file=sys.stderr)
        raise SystemExit(6)
