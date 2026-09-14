#!/usr/bin/env python3
"""Fixed root-only executor for a staged Cloudflare Tunnel token.

This program intentionally accepts no arguments. The token arrives only in a
fixed, massar-ops-owned staging file, is opened without following links, and
is copied into a root-only file before the dedicated service starts.
"""

from __future__ import annotations

import hashlib
import os
import pwd
import stat
import subprocess
import sys
import time
from pathlib import Path


STAGED_TOKEN = Path("/tmp/massar-cloudflared-token")
ROOT_DIRECTORY = Path("/etc/massar-cloudflared-token")
TOKEN_PATH = ROOT_DIRECTORY / "token"
CONFIG_SOURCE = Path("/usr/local/lib/massar-cloudflared-token/config.yml")
CONFIG_PATH = ROOT_DIRECTORY / "config.yml"
UNIT_SOURCE = Path("/usr/local/lib/massar-cloudflared-token/massar-cloudflared-token.service")
UNIT_PATH = Path("/etc/systemd/system/massar-cloudflared-token.service")
SERVICE = "massar-cloudflared-token"


class ExecutorError(RuntimeError):
    pass


def read_staged_token() -> bytes:
    expected_uid = pwd.getpwnam("massar-ops").pw_uid
    descriptor = os.open(STAGED_TOKEN, os.O_RDONLY | os.O_NOFOLLOW)
    try:
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode):
            raise ExecutorError("staged token is not a regular file")
        if metadata.st_uid != expected_uid or metadata.st_mode & 0o077:
            raise ExecutorError("staged token ownership or mode is unsafe")
        value = os.read(descriptor, 16 * 1024).strip()
        if not value:
            raise ExecutorError("staged token is empty")
        if os.read(descriptor, 1):
            raise ExecutorError("staged token exceeds the maximum size")
        return value + b"\n"
    finally:
        os.close(descriptor)


def require_root_asset(path: Path) -> None:
    if not path.is_file() or path.is_symlink():
        raise ExecutorError(f"required root asset is invalid: {path.name}")
    metadata = path.stat()
    if metadata.st_uid != 0 or metadata.st_mode & 0o022:
        raise ExecutorError(f"required root asset ownership or mode is unsafe: {path.name}")


def write_token(value: bytes) -> None:
    ROOT_DIRECTORY.mkdir(mode=0o700, exist_ok=True)
    os.chown(ROOT_DIRECTORY, 0, 0)
    os.chmod(ROOT_DIRECTORY, 0o700)
    temporary = ROOT_DIRECTORY / ".token.new"
    descriptor = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_TRUNC | os.O_NOFOLLOW, 0o600)
    try:
        offset = 0
        while offset < len(value):
            offset += os.write(descriptor, value[offset:])
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    os.chown(temporary, 0, 0)
    os.chmod(temporary, 0o600)
    os.replace(temporary, TOKEN_PATH)


def run(*command: str) -> None:
    subprocess.run(command, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True)


def wait_for_metrics() -> None:
    for _ in range(30):
        result = subprocess.run(
            ("/usr/bin/curl", "--fail", "--silent", "http://127.0.0.1:2010/ready"),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        if result.returncode == 0:
            return
        time.sleep(1)
    raise ExecutorError("cloudflared metrics endpoint did not become ready")


def install() -> None:
    if os.geteuid() != 0:
        raise ExecutorError("executor must run as root")
    if Path("/etc/massar/cluster-id").read_text(encoding="utf-8").strip() != "massar-production":
        raise ExecutorError("unexpected cluster marker")
    if not Path("/usr/bin/docker").is_file():
        raise ExecutorError("docker is not installed at the reviewed path")
    subprocess.run(("/usr/bin/docker", "pull", "cloudflare/cloudflared:latest"), check=True)
    help_text = subprocess.run(
        (
            "/usr/bin/docker", "run", "--rm", "--network", "none",
            "cloudflare/cloudflared:latest", "tunnel", "run", "--help",
        ),
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    if "--token-file" not in help_text:
        raise ExecutorError("installed cloudflared lacks --token-file support")
    require_root_asset(CONFIG_SOURCE)
    require_root_asset(UNIT_SOURCE)
    token = read_staged_token()
    try:
        write_token(token)
        subprocess.run(("/usr/bin/install", "-m", "0644", "-o", "root", "-g", "root", str(CONFIG_SOURCE), str(CONFIG_PATH)), check=True)
        subprocess.run(("/usr/bin/install", "-m", "0644", "-o", "root", "-g", "root", str(UNIT_SOURCE), str(UNIT_PATH)), check=True)
        run("/usr/bin/systemctl", "daemon-reload")
        run("/usr/bin/systemctl", "enable", "--now", SERVICE)
        run("/usr/bin/systemctl", "is-active", "--quiet", SERVICE)
        wait_for_metrics()
    finally:
        STAGED_TOKEN.unlink(missing_ok=True)


if __name__ == "__main__":
    try:
        install()
    except (ExecutorError, OSError, subprocess.CalledProcessError) as error:
        print(f"Cloudflare token connector blocked: {error}", file=sys.stderr)
        raise SystemExit(6)
