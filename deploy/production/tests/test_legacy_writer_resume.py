from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from types import SimpleNamespace
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "resume_legacy_writers",
    SCRIPTS / "resume_legacy_writers.py",
)
assert SPEC and SPEC.loader
resume = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(resume)


def capture_evidence(path: Path, **overrides: object) -> Path:
    payload = {
        "schemaVersion": 1,
        "status": "success",
        "backupId": "legacy-20260727T120000Z-deadbeef",
        "sourceHost": "192.0.2.10",
        "sourceUser": "root",
        "freezeRequested": True,
        "leaveWritersFrozenRequested": True,
        "writersRunningBeforeFreeze": ["massar_backend", "massar_worker"],
        "writersRestarted": [],
        "writersFrozenAtCompletion": True,
        "writerRecoveryComplete": False,
    }
    payload.update(overrides)
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


@pytest.mark.parametrize(
    "overrides",
    [
        {"backupId": "legacy-other-target"},
        {"sourceHost": "192.0.2.11"},
        {"writersRestarted": ["massar_backend"]},
        {"writersRunningBeforeFreeze": ["massar_backend", "unapproved"]},
        {"writersFrozenAtCompletion": False},
    ],
)
def test_resume_rejects_capture_or_target_mismatch(
    tmp_path: Path,
    overrides: dict[str, object],
) -> None:
    evidence = capture_evidence(tmp_path / "capture.json", **overrides)
    with pytest.raises(resume.WriterResumeError, match="exact writer recovery"):
        resume.load_resume_request(
            evidence,
            backup_id="legacy-20260727T120000Z-deadbeef",
            host="192.0.2.10",
            user="root",
        )


def test_resume_dry_run_attempts_no_ssh_and_evidence_is_one_time(
    tmp_path: Path,
) -> None:
    capture = capture_evidence(tmp_path / "capture.json")
    output = tmp_path / "resume-dry-run.json"
    command = [
        sys.executable,
        str(SCRIPTS / "resume_legacy_writers.py"),
        "--capture-evidence", str(capture),
        "--backup-id", "legacy-20260727T120000Z-deadbeef",
        "--host", "192.0.2.10",
        "--evidence-output", str(output),
        "--dry-run",
    ]

    first = subprocess.run(command, text=True, capture_output=True, check=False)
    second = subprocess.run(command, text=True, capture_output=True, check=False)

    assert first.returncode == 0, first.stderr
    payload = json.loads(output.read_text(encoding="utf-8"))
    assert payload["sshAttempted"] is False
    assert payload["retryAllowed"] is False
    assert payload["journalState"]["phase"] == "planned"
    assert output.stat().st_mode & 0o777 == 0o400
    assert second.returncode == 6
    assert "already exists" in second.stderr


class FakeJournalTransport:
    def __init__(self, failure: tuple[str, str] | None = None) -> None:
        self.owner = None
        self.phase = None
        self.started: list[str] = []
        self.running: set[str] = set()
        self.failure = failure

    def run(self, target, remote_argv, **kwargs):
        request = json.loads(remote_argv[-1])
        owner = request["owner"]
        action = request["action"]
        if self.owner is None:
            if action != "claim":
                raise RuntimeError("missing journal")
            self.owner = owner
            self.phase = "in-progress"
        elif self.owner != owner:
            return SimpleNamespace(returncode=43, stdout="", stderr="owner mismatch")
        if self.phase == "success":
            return SimpleNamespace(returncode=42, stdout="", stderr="already committed")
        if action == "start":
            writer = request["writer"]
            if writer in self.started:
                if writer not in self.running:
                    raise RuntimeError("journaled writer stopped")
            else:
                if writer in self.running:
                    return SimpleNamespace(
                        returncode=44,
                        stdout="",
                        stderr="running writer is not journaled",
                    )
                if self.failure == (writer, "before"):
                    self.failure = None
                    raise RuntimeError("crash before start")
                self.running.add(writer)
                self.started.append(writer)
                if self.failure == (writer, "after"):
                    self.failure = None
                    raise RuntimeError("network dropped after journal commit")
        elif action == "commit":
            if self.started != owner["writers"] or set(self.started) != self.running:
                raise RuntimeError("incomplete")
            self.phase = "success"
        return SimpleNamespace(
            returncode=0,
            stderr="",
            stdout=json.dumps({
                "phase": self.phase,
                "startedWriters": self.started,
            }),
        )


def owner() -> dict[str, object]:
    capture = {
        "backupId": "legacy-20260727T120000Z-deadbeef",
        "sourceHost": "192.0.2.10",
        "sourceUser": "root",
    }
    return resume.recovery_owner(
        capture,
        ("massar_backend", "massar_worker"),
    )


def test_retry_continues_after_crash_following_first_writer() -> None:
    transport = FakeJournalTransport(("massar_worker", "before"))
    recovery_owner = owner()
    with pytest.raises(RuntimeError, match="crash before") as interrupted:
        resume.resume_writers(transport, object(), recovery_owner)
    assert resume.failure_outcome(interrupted.value) == ("retryable", True)

    state = resume.resume_writers(transport, object(), recovery_owner)

    assert state["phase"] == "success"
    assert transport.started == ["massar_backend", "massar_worker"]


def test_retry_accepts_journaled_writer_after_lost_network_response() -> None:
    transport = FakeJournalTransport(("massar_backend", "after"))
    recovery_owner = owner()
    with pytest.raises(RuntimeError, match="network dropped") as interrupted:
        resume.resume_writers(transport, object(), recovery_owner)
    assert resume.failure_outcome(interrupted.value) == ("retryable", True)

    state = resume.resume_writers(transport, object(), recovery_owner)

    assert state["phase"] == "success"
    assert transport.started.count("massar_backend") == 1


def test_completed_recovery_rejects_reuse_and_mismatched_owner() -> None:
    recovery_owner = owner()
    completed = FakeJournalTransport()
    assert resume.resume_writers(completed, object(), recovery_owner)["phase"] == "success"
    with pytest.raises(resume.TerminalWriterResumeError, match="committed") as committed:
        resume.resume_writers(completed, object(), recovery_owner)
    assert resume.failure_outcome(committed.value) == ("blocked", False)

    mismatch = FakeJournalTransport(("massar_worker", "before"))
    with pytest.raises(RuntimeError):
        resume.resume_writers(mismatch, object(), recovery_owner)
    changed = {**recovery_owner, "sourceHost": "192.0.2.99"}
    with pytest.raises(resume.TerminalWriterResumeError, match="owner mismatch") as mismatch_error:
        resume.resume_writers(mismatch, object(), changed)
    assert resume.failure_outcome(mismatch_error.value) == ("blocked", False)


def test_running_writer_without_matching_journal_progress_is_rejected() -> None:
    transport = FakeJournalTransport()
    transport.running.add("massar_backend")

    with pytest.raises(resume.TerminalWriterResumeError, match="not journaled"):
        resume.resume_writers(transport, object(), owner())


def test_transport_interruption_is_retryable() -> None:
    interrupted = RetryableTransport()
    with pytest.raises(resume.RetryableWriterResumeError) as failure:
        resume.resume_writers(interrupted, object(), owner())
    assert resume.failure_outcome(failure.value) == ("retryable", True)


class RetryableTransport:
    def run(self, target, remote_argv, **kwargs):
        return SimpleNamespace(returncode=255, stdout="", stderr="connection lost")


def test_real_resume_uses_locked_atomic_remote_journal() -> None:
    source = (SCRIPTS / "resume_legacy_writers.py").read_text(encoding="utf-8")
    assert "/var/lib/massar/legacy-writer-recovery" in source
    assert "fcntl.LOCK_EX" in source
    assert "os.replace(temporary,journal)" in source
    assert '"startingWriter"' in source
    assert "running writer is not journaled by this operation" in source
    compile(resume.REMOTE_STATE_MACHINE, "<remote-writer-recovery>", "exec")
