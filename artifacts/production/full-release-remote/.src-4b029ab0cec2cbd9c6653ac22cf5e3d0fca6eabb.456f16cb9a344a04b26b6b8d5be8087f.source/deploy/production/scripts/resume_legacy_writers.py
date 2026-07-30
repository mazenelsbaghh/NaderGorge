#!/usr/bin/env python3
"""Resume writers from one authoritative capture through a resumable journal."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import sys
from pathlib import Path

from ssh_transport import SshTarget, StrictSshTransport


APPROVED_WRITERS = frozenset({"massar_backend", "massar_worker"})
BACKUP_ID = re.compile(r"^legacy-[A-Za-z0-9][A-Za-z0-9._-]{7,119}$")
REMOTE_JOURNAL_ROOT = "/var/lib/massar/legacy-writer-recovery"
REMOTE_STATE_MACHINE = r"""
import fcntl,json,os,pathlib,stat,subprocess,sys
request=json.loads(sys.argv[1]); owner=request["owner"]; action=request["action"]
root=pathlib.Path("/var/lib/massar/legacy-writer-recovery")
def fail(code,message):
    print(message,file=sys.stderr); raise SystemExit(code)
if root.is_symlink(): fail(45,"recovery journal root is a symlink")
root.mkdir(mode=0o700,parents=True,exist_ok=True); os.chmod(root,0o700)
metadata=root.stat()
if not stat.S_ISDIR(metadata.st_mode) or metadata.st_uid!=0:
    fail(45,"recovery journal root is not a root-owned directory")
stem=owner["backupId"]; journal=root/f"{stem}.json"; lock=root/f"{stem}.lock"
if journal.is_symlink() or lock.is_symlink(): fail(45,"recovery journal path is a symlink")
def write(state):
    temporary=journal.with_suffix(".json.tmp")
    with temporary.open("w",encoding="utf-8") as stream:
        json.dump(state,stream,sort_keys=True,separators=(",",":")); stream.write("\n")
        stream.flush(); os.fsync(stream.fileno())
    os.chmod(temporary,0o600); os.replace(temporary,journal)
    descriptor=os.open(root,os.O_RDONLY|os.O_DIRECTORY); os.fsync(descriptor); os.close(descriptor)
def running(writer):
    value=subprocess.run(["docker","inspect","--format","{{.State.Running}}",writer],
                         text=True,capture_output=True,check=True).stdout.strip()
    if value not in ("true","false"): raise SystemExit("invalid writer state")
    return value=="true"
with lock.open("a+",encoding="utf-8") as lock_stream:
    os.chmod(lock,0o600); fcntl.flock(lock_stream,fcntl.LOCK_EX)
    state=json.loads(journal.read_text(encoding="utf-8")) if journal.exists() else None
    if state is None:
        if action!="claim": raise SystemExit("recovery journal is missing")
        state={"schemaVersion":1,"owner":owner,"phase":"in-progress",
               "startingWriter":None,"startedWriters":[]}
        write(state)
    elif state.get("schemaVersion")!=1 or state.get("owner")!=owner:
        fail(43,"recovery journal owner mismatch")
    if state["phase"]=="success": fail(42,"writer recovery already committed")
    if state["phase"]!="in-progress": raise SystemExit("invalid recovery journal phase")
    if action=="start":
        writer=request["writer"]
        if writer not in owner["writers"]: raise SystemExit("writer is outside recovery owner")
        if writer in state["startedWriters"]:
            if not running(writer): raise SystemExit("journaled writer is no longer running")
        else:
            if state.get("startingWriter") not in (None,writer):
                raise SystemExit("another writer transition is incomplete")
            if state.get("startingWriter") is None:
                if running(writer): fail(44,"running writer is not journaled by this operation")
                state["startingWriter"]=writer; write(state)
            elif running(writer):
                state["startedWriters"].append(writer)
                state["startingWriter"]=None; write(state)
                print(json.dumps({"phase":state["phase"],"startedWriters":state["startedWriters"]},
                                 sort_keys=True,separators=(",",":")))
                raise SystemExit(0)
            subprocess.run(["docker","start",writer],check=True,capture_output=True,text=True)
            if not running(writer): raise SystemExit("writer did not start")
            state["startedWriters"].append(writer)
            state["startingWriter"]=None; write(state)
    elif action=="commit":
        if state.get("startingWriter") is not None or state["startedWriters"]!=owner["writers"]:
            raise SystemExit("writer recovery is incomplete")
        if not all(running(writer) for writer in owner["writers"]):
            raise SystemExit("writer verification failed")
        state["phase"]="success"; write(state)
    elif action!="claim":
        raise SystemExit("unsupported recovery action")
    print(json.dumps({"phase":state["phase"],"startedWriters":state["startedWriters"]},
                     sort_keys=True,separators=(",",":")))
"""


class WriterResumeError(RuntimeError):
    """Raised when frozen legacy writers cannot be safely resumed."""


class TerminalWriterResumeError(WriterResumeError):
    """Raised when retry is forbidden by durable remote state."""


class RetryableWriterResumeError(WriterResumeError):
    """Raised when the same operation may safely retry."""


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def load_resume_request(
    capture_path: Path,
    *,
    backup_id: str,
    host: str,
    user: str,
) -> tuple[dict[str, object], tuple[str, ...]]:
    path = capture_path.expanduser().resolve()
    if capture_path.expanduser().is_symlink() or not path.is_file():
        raise WriterResumeError("capture evidence must be a regular non-symlink file")
    try:
        capture = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise WriterResumeError("capture evidence is invalid") from exc
    writers = capture.get("writersRunningBeforeFreeze")
    if (
        not BACKUP_ID.fullmatch(backup_id)
        or capture.get("schemaVersion") != 1
        or capture.get("status") != "success"
        or capture.get("backupId") != backup_id
        or capture.get("sourceHost") != host
        or capture.get("sourceUser") != user
        or capture.get("freezeRequested") is not True
        or capture.get("leaveWritersFrozenRequested") is not True
        or capture.get("writersFrozenAtCompletion") is not True
        or capture.get("writerRecoveryComplete") is not False
        or capture.get("writersRestarted") != []
        or not isinstance(writers, list)
        or not writers
        or "massar_backend" not in writers
        or len(writers) != len(set(writers))
        or any(writer not in APPROVED_WRITERS for writer in writers)
    ):
        raise WriterResumeError(
            "capture evidence does not authorize this exact writer recovery target"
        )
    return capture, tuple(writers)


def recovery_owner(capture: dict[str, object], writers: tuple[str, ...]) -> dict[str, object]:
    identity = {
        "backupId": capture["backupId"],
        "sourceHost": capture["sourceHost"],
        "sourceUser": capture["sourceUser"],
        "writers": list(writers),
    }
    identity["operationId"] = hashlib.sha256(
        json.dumps(identity, sort_keys=True, separators=(",", ":")).encode()
    ).hexdigest()
    return identity


def write_once(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o400)
    with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
        json.dump(payload, stream, indent=2, sort_keys=True)
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())


def remote_transition(
    transport: StrictSshTransport,
    target: SshTarget,
    owner: dict[str, object],
    action: str,
    writer: str | None = None,
) -> dict[str, object]:
    request = {"owner": owner, "action": action}
    if writer is not None:
        request["writer"] = writer
    completed = transport.run(
        target,
        (
            "sudo", "python3", "-c", REMOTE_STATE_MACHINE,
            json.dumps(request, sort_keys=True, separators=(",", ":")),
        ),
        timeout_seconds=90,
        check=False,
    )
    if completed.returncode:
        message = completed.stderr.strip() or "remote recovery transition failed"
        if completed.returncode in {42, 43, 44, 45}:
            raise TerminalWriterResumeError(message)
        raise RetryableWriterResumeError(message)
    try:
        result = json.loads(completed.stdout)
    except json.JSONDecodeError as exc:
        raise WriterResumeError("remote recovery returned invalid journal state") from exc
    if (
        result.get("phase") not in {"in-progress", "success"}
        or not isinstance(result.get("startedWriters"), list)
    ):
        raise WriterResumeError("remote recovery returned invalid journal state")
    return result


def failure_outcome(error: Exception) -> tuple[str, bool]:
    if isinstance(error, TerminalWriterResumeError):
        return "blocked", False
    return "retryable", True


def resume_writers(
    transport: StrictSshTransport,
    target: SshTarget,
    owner: dict[str, object],
) -> dict[str, object]:
    state = remote_transition(transport, target, owner, "claim")
    for writer in owner["writers"]:
        state = remote_transition(transport, target, owner, "start", str(writer))
    return remote_transition(transport, target, owner, "commit")


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser(description=__doc__)
    value.add_argument("--capture-evidence", required=True, type=Path)
    value.add_argument("--backup-id", required=True)
    value.add_argument("--host", required=True)
    value.add_argument("--user", default="root")
    value.add_argument("--known-hosts", type=Path)
    value.add_argument("--identity", type=Path)
    value.add_argument("--evidence-output", required=True, type=Path)
    mode = value.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return value


def main() -> int:
    args = parser().parse_args()
    started_at = utc_now()
    try:
        capture, writers = load_resume_request(
            args.capture_evidence,
            backup_id=args.backup_id,
            host=args.host,
            user=args.user,
        )
        output = args.evidence_output.expanduser().resolve()
        if output.exists() or output.is_symlink():
            raise WriterResumeError("attempt evidence output already exists")
        owner = recovery_owner(capture, writers)
        status = "dry-run"
        journal_state: dict[str, object] = {
            "phase": "planned",
            "startedWriters": [],
        }
        recovery_error: Exception | None = None
        if args.yes:
            if not args.known_hosts or not args.identity:
                raise WriterResumeError("real recovery requires known-hosts and identity")
            try:
                transport = StrictSshTransport(args.known_hosts, args.identity)
                journal_state = resume_writers(
                    transport,
                    SshTarget("legacy-writer-recovery", args.host, args.user),
                    owner,
                )
                status = "success"
            except (RuntimeError, OSError, ValueError) as exc:
                status, _ = failure_outcome(exc)
                recovery_error = exc
        evidence = {
            "schemaVersion": 1,
            "status": status,
            "operationId": owner["operationId"],
            "backupId": capture["backupId"],
            "sourceHost": capture["sourceHost"],
            "sourceUser": capture["sourceUser"],
            "writers": list(writers),
            "journalState": journal_state,
            "startedAt": started_at,
            "completedAt": utc_now(),
            "sshAttempted": args.yes,
            "retryAllowed": failure_outcome(recovery_error)[1] if recovery_error else False,
            "reason": None if recovery_error is None else str(recovery_error)[:500],
        }
        write_once(output, evidence)
        if recovery_error is not None:
            if status == "blocked":
                raise WriterResumeError(
                    "writer recovery is terminally blocked by remote journal state"
                )
            raise WriterResumeError(
                "writer recovery attempt interrupted; retry with a new evidence output"
            )
        print(json.dumps({"status": status, "evidence": str(output)}))
        return 0
    except (WriterResumeError, OSError, ValueError) as exc:
        print(f"legacy writer recovery blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
