#!/usr/bin/env python3
"""Strict SSH transport for production cluster commands."""

from __future__ import annotations

import os
import shlex
import stat
import subprocess
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Sequence


class SshTransportError(RuntimeError):
    pass


@dataclass(frozen=True)
class SshTarget:
    node_id: str
    address: str
    user: str


class StrictSshTransport:
    def __init__(self, known_hosts: Path, identity_file: Path, connect_timeout_seconds: int = 10):
        self.known_hosts = known_hosts.expanduser().resolve()
        self.identity_file = identity_file.expanduser().resolve()
        self.connect_timeout_seconds = connect_timeout_seconds
        for label, path in (("known_hosts", self.known_hosts), ("identity_file", self.identity_file)):
            if not path.is_file():
                raise SshTransportError(f"{label} does not exist: {path}")
        mode = self.identity_file.stat().st_mode & 0o777
        if mode & 0o077:
            raise SshTransportError("identity_file permissions must not allow group/other access")

    def base_args(self) -> list[str]:
        return [
            "ssh",
            "-C",
            "-o", "BatchMode=yes",
            "-o", "IdentitiesOnly=yes",
            "-o", "StrictHostKeyChecking=yes",
            "-o", f"UserKnownHostsFile={self.known_hosts}",
            "-o", f"ConnectTimeout={self.connect_timeout_seconds}",
            "-i", str(self.identity_file),
        ]

    def stream_directory(
        self,
        target: SshTarget,
        source: Path,
        destination: str,
        *,
        timeout_seconds: int = 600,
    ) -> None:
        """Stream a local source snapshot to a new remote directory.

        The archive exists only in pipes. This is intentionally for source
        snapshots, not release images, so an operator workstation never stores
        remote-build image archives.
        """
        local_source = _regular_directory(source)
        remote_destination = _remote_path(destination, label="stream destination")
        if not str(remote_destination).startswith("/tmp/massar-build-source-"):
            raise SshTransportError("stream destination must be a remote builder staging path")
        script = f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test ! -e {shlex.quote(str(remote_destination))}
install -d -m 0700 {shlex.quote(str(remote_destination))}
tar -xzf - --no-same-owner --no-same-permissions -C {shlex.quote(str(remote_destination))}
test -z "$(find {shlex.quote(str(remote_destination))} -type l -print -quit)"
"""
        self._stream(
            ["tar", "-C", str(local_source), "-czf", "-", "."],
            self._ssh_argv(target, ("bash", "-lc", script)),
            timeout_seconds=timeout_seconds,
            producer_label="local source stream",
            consumer_label=f"{target.node_id} source receiver",
        )

    def stream_remote_file(
        self,
        source_target: SshTarget,
        source: str,
        destination_target: SshTarget,
        destination: str,
        *,
        timeout_seconds: int = 1200,
    ) -> None:
        """Relay one remote regular file directly between production nodes.

        The relay is a pair of strict SSH processes joined by a pipe; it never
        writes the file to the operator workstation.
        """
        remote_source = _remote_path(source, label="remote source")
        remote_destination = _remote_path(destination, label="remote destination")
        if not remote_source.is_relative_to(PurePosixPath("/var/lib/massar/builds")):
            raise SshTransportError("remote source must be under the remote build root")
        if not str(remote_destination).startswith("/tmp/massar-"):
            raise SshTransportError("remote destination must be a release staging path")
        source_script = f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test -f {shlex.quote(str(remote_source))}
test ! -L {shlex.quote(str(remote_source))}
exec cat {shlex.quote(str(remote_source))}
"""
        destination_parent = remote_destination.parent
        destination_script = f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
install -d -m 0700 {shlex.quote(str(destination_parent))}
test ! -e {shlex.quote(str(remote_destination))}
( umask 077; cat > {shlex.quote(str(remote_destination))} )
test -f {shlex.quote(str(remote_destination))}
test ! -L {shlex.quote(str(remote_destination))}
"""
        self._stream(
            self._ssh_argv(source_target, ("bash", "-lc", source_script)),
            self._ssh_argv(destination_target, ("bash", "-lc", destination_script)),
            timeout_seconds=timeout_seconds,
            producer_label=f"{source_target.node_id} file sender",
            consumer_label=f"{destination_target.node_id} file receiver",
        )

    def _ssh_argv(self, target: SshTarget, remote_argv: Sequence[str]) -> list[str]:
        if not remote_argv:
            raise SshTransportError("remote command must not be empty")
        return [
            *self.base_args(),
            f"{target.user}@{target.address}",
            "--",
            shlex.join(list(remote_argv)),
        ]

    def _stream(
        self,
        producer_argv: Sequence[str],
        consumer_argv: Sequence[str],
        *,
        timeout_seconds: int,
        producer_label: str,
        consumer_label: str,
    ) -> None:
        if not isinstance(timeout_seconds, int) or isinstance(timeout_seconds, bool) or timeout_seconds <= 0:
            raise SshTransportError("stream timeout_seconds must be a positive integer")
        producer = subprocess.Popen(
            list(producer_argv), stdout=subprocess.PIPE, stderr=subprocess.PIPE
        )
        try:
            assert producer.stdout is not None
            consumer = subprocess.Popen(
                list(consumer_argv),
                stdin=producer.stdout,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.PIPE,
            )
        except BaseException:
            _terminate_process(producer)
            raise
        assert producer.stdout is not None
        producer.stdout.close()
        deadline = time.monotonic() + timeout_seconds
        try:
            _, consumer_stderr = consumer.communicate(
                timeout=_remaining_timeout(deadline)
            )
            producer.wait(timeout=_remaining_timeout(deadline))
        except subprocess.TimeoutExpired as exc:
            _terminate_process(consumer)
            _terminate_process(producer)
            raise SshTransportError(
                f"{producer_label} to {consumer_label} timed out"
            ) from exc
        producer_stderr = _read_process_stderr(producer)
        if consumer.returncode:
            raise SshTransportError(
                f"{consumer_label}: {_stderr_message(consumer_stderr, 'stream receiver failed')}"
            )
        if producer.returncode:
            raise SshTransportError(
                f"{producer_label}: {_stderr_message(producer_stderr, 'stream sender failed')}"
            )

    def run(
        self,
        target: SshTarget,
        remote_argv: Sequence[str],
        *,
        timeout_seconds: int = 60,
        check: bool = True,
    ) -> subprocess.CompletedProcess[str]:
        if not remote_argv:
            raise SshTransportError("remote command must not be empty")
        remote_command = shlex.join(list(remote_argv))
        completed = subprocess.run(
            [*self.base_args(), f"{target.user}@{target.address}", "--", remote_command],
            text=True,
            capture_output=True,
            timeout=timeout_seconds,
            check=False,
            env={**os.environ, "LC_ALL": "C"},
        )
        if check and completed.returncode != 0:
            stderr = completed.stderr.strip() or "remote command failed"
            raise SshTransportError(f"{target.node_id}: {stderr}")
        return completed

    def copy(
        self,
        target: SshTarget,
        source: Path,
        destination: str,
        *,
        timeout_seconds: int = 60,
    ) -> None:
        if not source.is_file():
            raise SshTransportError(f"copy source does not exist: {source}")
        completed = subprocess.run(
            [
                "scp",
                "-o", "BatchMode=yes",
                "-o", "IdentitiesOnly=yes",
                "-o", "StrictHostKeyChecking=yes",
                "-o", f"UserKnownHostsFile={self.known_hosts}",
                "-o", f"ConnectTimeout={self.connect_timeout_seconds}",
                "-i", str(self.identity_file),
                str(source),
                f"{target.user}@{target.address}:{destination}",
            ],
            text=True,
            capture_output=True,
            timeout=timeout_seconds,
            check=False,
            env={**os.environ, "LC_ALL": "C"},
        )
        if completed.returncode != 0:
            raise SshTransportError(
                f"{target.node_id}: {completed.stderr.strip() or 'secure copy failed'}"
            )

    def fetch(
        self,
        target: SshTarget,
        source: str,
        destination: Path,
        *,
        timeout_seconds: int = 60,
        max_bytes: int = 1024 * 1024,
    ) -> int:
        """Fetch one bounded regular file without overwriting a local path."""
        remote = PurePosixPath(source)
        if (
            not source
            or not remote.is_absolute()
            or remote.as_posix() != source
            or any(part in {"", ".", ".."} for part in remote.parts)
            or any(character not in "abcdefghijklmnopqrstuvwxyz"
                   "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/._-"
                   for character in source)
        ):
            raise SshTransportError("fetch source must be a normalized absolute path")
        if not isinstance(max_bytes, int) or isinstance(max_bytes, bool) or max_bytes <= 0:
            raise SshTransportError("fetch max_bytes must be a positive integer")

        requested = destination.expanduser()
        if requested.is_symlink() or requested.exists():
            raise SshTransportError("fetch destination must not exist or be a symlink")
        parent = requested.parent.resolve()
        parent.mkdir(parents=True, exist_ok=True)
        if not parent.is_dir():
            raise SshTransportError("fetch destination parent is not a directory")
        final = parent / requested.name
        if final.is_symlink() or final.exists():
            raise SshTransportError("fetch destination must not exist or be a symlink")

        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{final.name}.",
            suffix=".fetch",
            dir=parent,
        )
        os.close(descriptor)
        temporary = Path(temporary_name)
        try:
            completed = subprocess.run(
                [
                    "scp",
                    "-o", "BatchMode=yes",
                    "-o", "IdentitiesOnly=yes",
                    "-o", "StrictHostKeyChecking=yes",
                    "-o", f"UserKnownHostsFile={self.known_hosts}",
                    "-o", f"ConnectTimeout={self.connect_timeout_seconds}",
                    "-i", str(self.identity_file),
                    f"{target.user}@{target.address}:{source}",
                    str(temporary),
                ],
                text=True,
                capture_output=True,
                timeout=timeout_seconds,
                check=False,
                env={**os.environ, "LC_ALL": "C"},
            )
            if completed.returncode != 0:
                raise SshTransportError(
                    f"{target.node_id}: "
                    f"{completed.stderr.strip() or 'secure fetch failed'}"
                )
            metadata = temporary.lstat()
            if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
                raise SshTransportError("fetched object is not a single-link regular file")
            if metadata.st_size <= 0 or metadata.st_size > max_bytes:
                raise SshTransportError(
                    f"fetched file size must be between 1 and {max_bytes} bytes"
                )
            os.chmod(temporary, 0o640)
            with temporary.open("rb") as stream:
                os.fsync(stream.fileno())
            try:
                os.link(temporary, final, follow_symlinks=False)
            except FileExistsError as exc:
                raise SshTransportError(
                    "fetch destination appeared during transfer"
                ) from exc
            return metadata.st_size
        finally:
            temporary.unlink(missing_ok=True)


def _regular_directory(source: Path) -> Path:
    candidate = source.expanduser().resolve()
    if source.is_symlink() or not candidate.is_dir():
        raise SshTransportError("stream source must be a non-symlink directory")
    for path in candidate.rglob("*"):
        if path.is_symlink() or not path.is_file() and not path.is_dir():
            raise SshTransportError("stream source must contain only regular files and directories")
    return candidate


def _remote_path(value: str, *, label: str) -> PurePosixPath:
    if not isinstance(value, str) or not value:
        raise SshTransportError(f"{label} must be a normalized absolute path")
    path = PurePosixPath(value)
    allowed = set("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/._-")
    if (
        not path.is_absolute()
        or path.as_posix() != value
        or any(part in {"", ".", ".."} for part in path.parts)
        or any(character not in allowed for character in value)
    ):
        raise SshTransportError(f"{label} must be a normalized absolute path")
    return path


def _remaining_timeout(deadline: float) -> float:
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise subprocess.TimeoutExpired("stream", 0)
    return remaining


def _stderr_message(value: bytes | None, fallback: str) -> str:
    if not value:
        return fallback
    return value.decode("utf-8", errors="replace").strip() or fallback


def _read_process_stderr(process: subprocess.Popen[bytes]) -> bytes:
    if process.stderr is None:
        return b""
    return process.stderr.read()


def _terminate_process(process: subprocess.Popen[bytes]) -> None:
    if process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)


def scan_host_key(address: str, output: Path, *, port: int = 22) -> None:
    """Enrollment helper. A human must compare the fingerprint in provider console."""
    completed = subprocess.run(
        ["ssh-keyscan", "-T", "10", "-p", str(port), address],
        text=True,
        capture_output=True,
        timeout=15,
        check=False,
    )
    if completed.returncode != 0 or not completed.stdout.strip():
        raise SshTransportError(f"could not scan host key for {address}")
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("a", encoding="utf-8") as handle:
        handle.write(completed.stdout)
