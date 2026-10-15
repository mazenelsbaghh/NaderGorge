"""Trial release boundaries use synthetic ZIPs and temporary output files only."""
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import windows_trial as trial
import windows_trial_verify as verifier


def archive_bytes(entries):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, content in entries.items():
            archive.writestr(name, content)
    return buffer.getvalue()


def client_fixture():
    source = {"version": "1.4.0+21", "commit": "a" * 40}
    runtime = {"schemaVersion": 1, "networkModes": ["legacy", "https"], "includesRuntimeDatabase": False, "physicalWindowsVerified": False}
    for name in ("windowsGoTestsPassed", "windowsCredentialReconnectTestsPassed", "windowsCliSmokePassed", "syntheticPdfAndZipPassed"):
        runtime[name] = True
    entries = {name: b"MZsynthetic" for name in verifier.NATIVE_EXECUTABLES}
    runtime["executables"] = {name: trial.digest(content) for name, content in entries.items()}
    for name in ("massar_center.exe", "massar-lan-host.exe", "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll", "hotspot/coreclr.dll", "hotspot/Microsoft.Windows.SDK.NET.dll", "Windows-exam-setup.txt", "report-pdf/_internal/dependency.dll"):
        entries[name] = b"synthetic"
    entries["hotspot/Massar.Hotspot.runtimeconfig.json"] = b"{}"
    entries["data/app.so"] = b"synthetic-aot"
    for name in verifier.PUBLIC_ASSETS:
        entries["data/flutter_assets/assets/" + name] = b"public"
    manifest = {"role": "client", "appVersion": source["version"], "sourceCommit": source["commit"], "buildId": "b" * 16, "aotSha256": trial.digest(entries["data/app.so"]), "seedId": None, "seedSha256": None, "students": 0, "groups": 0, "examRuntime": runtime}
    entries["build-manifest.json"] = json.dumps(manifest).encode()
    return source, manifest, entries


def private_fixture(directory, *, overlay=False, baseline_sha=None):
    directory.mkdir(parents=True)
    names = trial.PRIVATE_INSTALLER_ASSETS if overlay else trial.HISTORICAL_PRIVATE_ASSETS
    contents = {name: json.dumps({"fixture": name}).encode() for name in names}
    files = {name: trial.digest(value) for name, value in contents.items()}
    payload = archive_bytes(contents)
    manifest = {"schemaVersion": 1, "privateOnly": True, "sha256": trial.digest(payload), "files": files}
    if overlay:
        review = {"status": "verified", "ownerAuthorized": True, "sourceBackupSha256": "a" * 64,
                  "installationSeedSha256": files["installation_seed.json"], "repairSha256": files[trial.GRADE_REPAIR_ASSET]}
        review_bytes = json.dumps(review).encode()
        (directory / "grade-repair-review.json").write_bytes(review_bytes)
        manifest.update({"baselineSha256": baseline_sha, "reviewManifest": "grade-repair-review.json", "reviewManifestSha256": trial.digest(review_bytes)})
    else:
        host = archive_bytes({"data/flutter_assets/assets/" + name: value for name, value in contents.items()})
        (directory / "host.zip").write_bytes(host)
        (directory / "published.json").write_text(json.dumps({"verifiedPrivateDownloads": True, "releases": [{"role": "host", "downloadPath": "host.zip", "sha256": trial.digest(host), "size": len(host)}]}))
    (directory / "private-packaging-assets.zip").write_bytes(payload)
    (directory / "private-packaging-manifest.json").write_text(json.dumps(manifest))
    (directory / "private-assets-upload.json").write_text(json.dumps({"id": 123, "sha256": manifest["sha256"]}))
    return manifest


def rebind_overlay(directory, entries=None, review_changes=None):
    manifest_path = directory / "private-packaging-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    if entries is not None:
        payload = archive_bytes(entries)
        (directory / "private-packaging-assets.zip").write_bytes(payload)
        manifest.update({"sha256": trial.digest(payload), "files": {name: trial.digest(value) for name, value in entries.items()}})
        (directory / "private-assets-upload.json").write_text(json.dumps({"id": 123, "sha256": manifest["sha256"]}))
    if review_changes is not None:
        path = directory / "grade-repair-review.json"
        review = json.loads(path.read_text())
        review.update(review_changes)
        path.write_text(json.dumps(review))
        manifest["reviewManifestSha256"] = trial.digest(path.read_bytes())
    manifest_path.write_text(json.dumps(manifest))


class WindowsTrialTests(unittest.TestCase):
    def test_private_overlay_adds_only_verified_repair_and_binds_all_inputs(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            baseline = root / "artifacts/private/baseline"
            historical = private_fixture(baseline)
            overlay = root / "artifacts/private/overlay"
            manifest = private_fixture(overlay, overlay=True, baseline_sha=historical["sha256"])
            before = {path.name: path.read_bytes() for path in baseline.iterdir()}
            with patch.object(trial, "ROOT", root):
                result = trial.private_inputs(baseline, overlay)
                self.assertEqual(13, len(result["files"]))
                self.assertEqual(manifest["files"], result["files"])
                self.assertEqual(historical["sha256"], result["baselineSha256"])
                self.assertEqual("a" * 64, result["sourceBackupSha256"])
                self.assertEqual(historical["files"]["installation_seed.json"], result["files"]["installation_seed.json"])
                self.assertEqual(12, len(trial.private_inputs(baseline)["files"]))
            self.assertEqual(before, {path.name: path.read_bytes() for path in baseline.iterdir()})

    def test_private_overlay_rejects_tampering_unreviewed_seed_and_unsafe_paths(self):
        for mutation in ("zip_tamper", "missing_repair", "seed", "other_history", "unreviewed", "source_hash", "review_digest", "repair_review", "baseline", "outside", "symlink", "zip_path"):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary).resolve()
                baseline = root / "artifacts/private/baseline"
                historical = private_fixture(baseline)
                overlay = root / "artifacts/private/overlay"
                private_fixture(overlay, overlay=True, baseline_sha=historical["sha256"])
                with zipfile.ZipFile(overlay / "private-packaging-assets.zip") as archive:
                    entries = {name: archive.read(name) for name in archive.namelist()}
                if mutation == "zip_tamper":
                    (overlay / "private-packaging-assets.zip").write_bytes(b"tampered")
                elif mutation == "missing_repair":
                    del entries[trial.GRADE_REPAIR_ASSET]
                    rebind_overlay(overlay, entries)
                elif mutation in ("seed", "other_history"):
                    name = "installation_seed.json" if mutation == "seed" else "data_repair_20261002.json"
                    entries[name] = b'{"changed":true}'
                    rebind_overlay(overlay, entries, {"installationSeedSha256": trial.digest(entries["installation_seed.json"])})
                elif mutation in ("unreviewed", "source_hash", "repair_review"):
                    changes = {"ownerAuthorized": False} if mutation == "unreviewed" else {"sourceBackupSha256": "unknown"} if mutation == "source_hash" else {"repairSha256": "b" * 64}
                    rebind_overlay(overlay, review_changes=changes)
                elif mutation == "review_digest":
                    (overlay / "grade-repair-review.json").write_text('{}')
                elif mutation == "baseline":
                    (baseline / "host.zip").write_bytes(b"tampered historical host")
                elif mutation == "outside":
                    outside = root / "outside"
                    overlay.rename(outside)
                    overlay = outside
                elif mutation == "symlink":
                    link = root / "artifacts/private/linked"
                    link.symlink_to(overlay, target_is_directory=True)
                    overlay = link
                elif mutation == "zip_path":
                    entries["../hidden.json"] = entries.pop(trial.GRADE_REPAIR_ASSET)
                    rebind_overlay(overlay, entries)
                with patch.object(trial, "ROOT", root), self.assertRaises(RuntimeError):
                    trial.private_inputs(baseline, overlay)

    def test_private_archive_rejects_duplicate_entries_even_with_expected_names(self):
        buffer = io.BytesIO()
        import warnings
        with warnings.catch_warnings(), zipfile.ZipFile(buffer, "w") as archive:
            warnings.simplefilter("ignore", UserWarning)
            archive.writestr("installation_seed.json", b"{}")
            archive.writestr("installation_seed.json", b"{}")
        with self.assertRaises(RuntimeError):
            trial.private_archive(buffer.getvalue(), {"installation_seed.json": trial.digest(b"{}")})

    def test_trial_identity_rejects_production_destinations(self):
        trial.validate_trial_identity("1.4.0+21", "codex/trial-omada-live-attendance-20261014")
        for branch in ("main", "codex/production", "codex/release/new", "codex/trial-safe:main", "codex/trial-safe\nmain"):
            with self.subTest(branch=branch), self.assertRaises(RuntimeError):
                trial.validate_trial_identity("1.4.0+21", branch)

    def test_source_allowlist_rejects_private_generated_and_out_of_scope_inputs(self):
        for name in ("desktop/center-flutter/assets/admin_account.json", "artifacts/private/student.json", "desktop/center-flutter/build/private.dart", "desktop/center-flutter/private-import/repair.dart", "desktop/center-flutter/omada-controller.enc", "desktop/center-flutter/scripts/recover-windows-exam-setup.ps1", "desktop/center-flutter/../secret.dart", "backend/source.cs", "/absolute/path"):
            with self.subTest(name=name), self.assertRaises(RuntimeError):
                trial.source_path(name)

    def test_git_excludes_cannot_hide_native_inputs_or_wired_browser_test(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            expected = ["desktop/center-flutter/lib/new_feature.dart", "exam-room/center_monitor.go", "exam-room/center_monitor_test.go", "exam-room/tests/center-wired-room.cjs", "exam-room/frontend/src/CenterWhatsAppReports.jsx", "exam-room/frontend/src/templateFields.js", "exam-room/windows-hotspot/HomeworkHotspot.cs"]
            for name in expected + ["exam-room/build/generated.go", "exam-room/windows-hotspot/obj/generated.cs", "desktop/center-flutter/.dart_tool/generated.dart", "desktop/center-flutter/private-import/repair.dart"]:
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("package main\n" if path.suffix == ".go" else "void main() {}\n")
            # Git is the external discovery boundary: an ignored project does
            # not appear in either tracked or unignored-file output.
            with patch.object(trial, "ROOT", root), patch.object(trial, "git", return_value=b""):
                self.assertEqual(sorted(expected), trial.changed_source_paths("a" * 40))

    def test_complete_client_roundtrip_and_saved_archive_are_exact(self):
        source, manifest, entries = client_fixture()
        payload = archive_bytes(entries)
        self.assertEqual(manifest, verifier.verify_archive(payload, source, "client"))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trial.zip"
            verifier.save_archive(path, payload)
            self.assertEqual(payload, path.read_bytes())
            verifier.save_archive(path, payload)
            with self.assertRaises(RuntimeError):
                verifier.save_archive(path, payload + b"different")
            self.assertEqual(payload, path.read_bytes())

    def test_client_rejects_private_data_and_misidentified_binaries(self):
        for mutation in ("private_asset", "nested_assets", "seed", "role", "version", "source", "aot", "native", "native_tests"):
            with self.subTest(mutation=mutation):
                source, manifest, entries = client_fixture()
                if mutation == "private_asset":
                    entries["data/flutter_assets/assets/admin_account.json"] = b"{}"
                elif mutation == "nested_assets":
                    entries["nested/data/flutter_assets/assets/logo.svg"] = b"private hidden duplicate"
                elif mutation == "seed":
                    manifest["seedId"] = "synthetic-seed"
                elif mutation == "role":
                    manifest["role"] = "host"
                elif mutation == "version":
                    manifest["appVersion"] = "1.3.3+20"
                elif mutation == "source":
                    manifest["sourceCommit"] = "c" * 40
                elif mutation == "aot":
                    entries["data/app.so"] = b"tampered"
                elif mutation == "native":
                    entries["massar-exam-network.exe"] = b"MZtampered"
                elif mutation == "native_tests":
                    manifest["examRuntime"]["windowsGoTestsPassed"] = False
                entries["build-manifest.json"] = json.dumps(manifest).encode()
                with self.assertRaises(RuntimeError):
                    verifier.verify_archive(archive_bytes(entries), source, "client")

    def test_host_rejects_changed_or_missing_private_inputs(self):
        document = {"privateInputs": {"files": {"installation_seed.json": trial.digest(b"expected")}}}
        for content in (b"changed", None):
            with self.subTest(content=content):
                entries = {} if content is None else {"data/flutter_assets/assets/installation_seed.json": content}
                with zipfile.ZipFile(io.BytesIO(archive_bytes(entries))) as archive, self.assertRaises(RuntimeError):
                    verifier.assert_assets(archive, {"role": "host"}, document)

    def test_archive_rejects_runtime_data_and_unsafe_paths(self):
        for name, content in (("database.bin", b"SQLite format 3\0more"), ("network.json.tmp", b"{}"), ("omada-controller.enc", b"protected credentials"), ("user.db-wal", b"sqlite"), ("backup.json", b'{"format":"massar-center-backup"}'), ("../file", b"bad"), ("C:/file", b"bad"), ("folder\\file", b"bad")):
            with self.subTest(name=name), zipfile.ZipFile(io.BytesIO(archive_bytes({name: content}))) as archive, self.assertRaises(RuntimeError):
                verifier.assert_archive_safe(archive)

    def test_artifact_sidecar_must_exactly_match_zip_digest(self):
        payload = b"synthetic zip"
        valid = {"trial.zip": payload, "trial.zip.sha256": (trial.digest(payload) + "  trial.zip").encode()}
        self.assertEqual(payload, verifier.unwrap_artifact(archive_bytes(valid)))
        valid["trial.zip.sha256"] = ("0" * 64 + "\n" + trial.digest(payload)).encode()
        with self.assertRaises(RuntimeError):
            verifier.unwrap_artifact(archive_bytes(valid))


if __name__ == "__main__":
    unittest.main()
