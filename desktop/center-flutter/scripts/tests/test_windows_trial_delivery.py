"""Exact-run native delivery uses synthetic GitHub responses and native ZIPs."""
import copy
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import urllib.error

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import windows_trial as trial
import windows_trial_verify as verifier
from test_windows_trial import archive_bytes, client_fixture


class DeliveryFixture:
    def __init__(self):
        self.document, _, _ = client_fixture()
        self.document["branch"] = "codex/trial-native-delivery"
        private = {name: json.dumps({"fixture": name}).encode() for name in trial.PRIVATE_INSTALLER_ASSETS}
        self.document["privateInputs"] = {"files": {name: trial.digest(content) for name, content in private.items()}}
        self.run = {"id": 73, "run_attempt": 2, "workflow_id": 9, "html_url": "https://github.com/synthetic/run/73", "head_sha": self.document["commit"],
                    "head_branch": self.document["branch"], "event": "workflow_dispatch",
                    "name": trial.WORKFLOW, "status": "completed", "conclusion": "success"}
        self.repository = {"private": True}
        self.jobs = [{"name": f"windows ({role})", "status": "completed", "conclusion": "success"} for role in ("host", "client")]
        self.workflow = b"env:\n  MASSAR_WINDOWS_DELIVERY: github-release-assets-v1\n"
        self.releases, self.refs, self.evidence, self.payloads, self.artifacts = {}, {}, {}, {}, []
        for offset, role in enumerate(("host", "client"), 1):
            _, manifest, entries = client_fixture()
            manifest["role"] = role
            if role == "host":
                for name, content in private.items():
                    entries["data/flutter_assets/assets/" + name] = content
                for name in ("index.html", "app.js", "scanner.js", "style.css", "ZXING-LICENSE"):
                    entries["data/flutter_assets/assets/mobile-homework/" + name] = b"synthetic homework"
                manifest["seedSha256"] = self.document["privateInputs"]["files"]["installation_seed.json"]
            entries["build-manifest.json"] = json.dumps(manifest).encode()
            payload = archive_bytes(entries)
            name = f"massar-center-windows-{role}-run-73-2-{manifest['buildId']}.zip"
            checksum = (trial.digest(payload) + "\n").encode()
            declared, assets = [], []
            for asset_id, filename, content in ((offset * 10, name, payload), (offset * 10 + 1, name + ".sha256", checksum)):
                declared.append({"name": filename, "size": len(content), "sha256": trial.digest(content)})
                assets.append({"id": asset_id, "name": filename, "size": len(content), "digest": "sha256:" + trial.digest(content), "state": "uploaded"})
                self.payloads[asset_id] = content
            tag = f"windows-native-73-2-{role}"
            body = {"schemaVersion": 1, "transport": "github-release-assets", "repository": trial.REPO,
                    "runId": 73, "runAttempt": 2, "role": role, "sourceCommit": self.document["commit"], "assets": declared}
            self.releases[tag] = {"id": offset, "tag_name": tag, "target_commitish": self.document["commit"],
                                  "draft": False, "prerelease": True, "body": json.dumps(body), "assets": assets}
            self.refs[tag] = {"ref": "refs/tags/" + tag, "object": {"type": "commit", "sha": self.document["commit"]}}
            self.evidence[role] = {"role": role, "sourceCommit": self.document["commit"], "version": self.document["version"],
                                   "buildId": manifest["buildId"], "transport": "release-assets", "releaseId": offset, "releaseTag": tag,
                                   "artifactId": offset * 10, "assetName": name, "sha256": trial.digest(payload),
                                   "assetSha256": trial.digest(payload), "outerSha256": trial.digest(payload), "size": len(payload),
                                   "sidecarAssetId": offset * 10 + 1, "sidecarSize": len(checksum), "sidecarSha256": trial.digest(checksum)}
            outer = archive_bytes({name: payload, name + ".sha256": checksum})
            self.artifacts.append({"id": offset * 100, "name": f"massar-windows-{role}-73-2", "expired": False, "digest": "sha256:" + trial.digest(outer)})
            self.payloads["artifact-" + str(offset * 100)] = outer

    def api(self, path, _token):
        if path == "": value = self.repository
        elif path == "/actions/runs/73": value = self.run
        elif path == "/actions/runs/73/attempts/2/jobs?per_page=100": value = {"jobs": self.jobs}
        elif path == "/actions/runs/73/artifacts?per_page=100": value = {"artifacts": self.artifacts}
        elif path.startswith("/releases/tags/"): value = self.releases[path.removeprefix("/releases/tags/")]
        elif path.startswith("/git/ref/tags/"): value = self.refs[path.removeprefix("/git/ref/tags/")]
        else: raise AssertionError("Unexpected synthetic API path: " + path)
        return copy.deepcopy(value)

    def git(self, command, reference):
        if command != "show": raise AssertionError("Unexpected Git operation")
        if reference.endswith(":.github/workflows/center-windows-private.yml"): return self.workflow
        if ":desktop/center-flutter/assets/mobile-homework/" in reference: return b"synthetic homework"
        raise AssertionError("Unexpected synthetic Git object: " + reference)

    def open(self, request, timeout):
        if "/releases/assets/" in request.full_url:
            return io.BytesIO(self.payloads[int(request.full_url.rsplit("/", 1)[1])])
        if "/actions/artifacts/" in request.full_url:
            return io.BytesIO(self.payloads["artifact-" + request.full_url.split("/")[-2]])
        raise AssertionError("Unexpected synthetic download URL")


class NativeDeliveryTests(unittest.TestCase):
    def test_release_delivery_roundtrip_verifies_both_native_roles_and_retained_bindings(self):
        fixture = DeliveryFixture()
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            (output / "build-run.json").write_text(json.dumps(fixture.run))
            with (patch.object(verifier, "api", side_effect=fixture.api), patch.object(trial, "api", side_effect=fixture.api),
                  patch.object(verifier, "git", side_effect=fixture.git),
                  patch.object(verifier.urllib.request, "build_opener") as opener):
                opener.return_value.open.side_effect = fixture.open
                verifier.download_trial(fixture.document, output, "synthetic-token")
                result = json.loads((output / "verified-trial.json").read_text())
                self.assertEqual(result["runAttempt"], 2)
                self.assertEqual(len(result["releases"]), 2)
                for evidence in result["releases"]:
                    self.assertEqual(evidence["transport"], "release-assets")
                    self.assertEqual(Path(evidence["path"]).read_bytes(), fixture.payloads[evidence["artifactId"]])
                    verifier.verify_delivery_binding(fixture.document, fixture.run, evidence, "synthetic-token")

    def test_legacy_sealed_workflow_still_downloads_actions_wrappers(self):
        fixture = DeliveryFixture()
        fixture.workflow = b"permissions:\n  contents: read\n"
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            (output / "build-run.json").write_text(json.dumps(fixture.run))
            with (patch.object(verifier, "api", side_effect=fixture.api), patch.object(trial, "api", side_effect=fixture.api),
                  patch.object(verifier, "git", side_effect=fixture.git),
                  patch.object(verifier.urllib.request, "build_opener") as opener):
                opener.return_value.open.side_effect = fixture.open
                verifier.download_trial(fixture.document, output, "synthetic-token")
                result = json.loads((output / "verified-trial.json").read_text())
                for evidence in result["releases"]:
                    self.assertNotIn("releaseId", evidence)
                    self.assertEqual(evidence["artifactId"], 100 if evidence["role"] == "host" else 200)

    def test_stale_public_ambiguous_or_incomplete_release_proof_is_rejected(self):
        mutations = ("public", "failed-client", "rerun", "target", "tag-ref", "draft", "wrong-role", "old-attempt",
                     "extra-asset", "duplicate-id", "upload-pending", "digest", "size", "evidence-id", "unknown-marker")
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                fixture = DeliveryFixture()
                release = fixture.releases["windows-native-73-2-host"]
                body = json.loads(release["body"])
                evidence = fixture.evidence["host"]
                run = copy.deepcopy(fixture.run)
                if mutation == "public": fixture.repository["private"] = False
                elif mutation == "failed-client": fixture.jobs[1]["conclusion"] = "failure"
                elif mutation == "rerun": fixture.run["run_attempt"] = 3
                elif mutation == "target": release["target_commitish"] = "b" * 40
                elif mutation == "tag-ref": fixture.refs[release["tag_name"]]["object"]["sha"] = "b" * 40
                elif mutation == "draft": release["draft"] = True
                elif mutation == "wrong-role": body["role"] = "client"
                elif mutation == "old-attempt": body["runAttempt"] = 1
                elif mutation == "extra-asset": release["assets"].append(dict(release["assets"][0], id=999, name="unreviewed.zip"))
                elif mutation == "duplicate-id": release["assets"][1]["id"] = release["assets"][0]["id"]
                elif mutation == "upload-pending": release["assets"][0]["state"] = "starter"
                elif mutation == "digest": release["assets"][0]["digest"] = "sha256:" + "0" * 64
                elif mutation == "size": release["assets"][0]["size"] += 1
                elif mutation == "evidence-id": evidence["releaseId"] = 99
                elif mutation == "unknown-marker": fixture.workflow = b"env:\n  MASSAR_WINDOWS_DELIVERY: unreviewed-v2\n"
                release["body"] = json.dumps(body)
                with (patch.object(verifier, "api", side_effect=fixture.api), patch.object(verifier, "git", side_effect=fixture.git),
                      self.assertRaises(RuntimeError)):
                    verifier.verify_delivery_binding(fixture.document, run, evidence, "synthetic-token")

    def test_asset_redirect_never_forwards_bearer_and_rejects_untrusted_or_second_redirects(self):
        payload = b"synthetic bytes"
        asset = {"id": 7, "size": len(payload), "digest": "sha256:" + trial.digest(payload)}
        signed = "https://release-assets.githubusercontent.com/example?signature=synthetic"
        def redirected(request, timeout):
            if request.full_url.startswith("https://api.github.com/"):
                self.assertEqual(request.get_header("Authorization"), "Bearer synthetic-token")
                raise urllib.error.HTTPError(request.full_url, 302, "redirect", {"Location": signed}, None)
            self.assertIsNone(request.get_header("Authorization"))
            return io.BytesIO(payload)
        with patch.object(verifier.urllib.request, "build_opener") as opener:
            opener.return_value.open.side_effect = redirected
            self.assertEqual(verifier.download_release_asset(asset, "synthetic-token", maximum_bytes=100), payload)
        for location in ("http://release-assets.githubusercontent.com/file", "https://evil.example/file",
                         "https://token@release-assets.githubusercontent.com/file", "https://release-assets.githubusercontent.com:8443/file"):
            with self.subTest(location=location), patch.object(verifier.urllib.request, "build_opener") as opener:
                opener.return_value.open.side_effect = urllib.error.HTTPError("https://api.github.com/asset", 302, "redirect", {"Location": location}, None)
                with self.assertRaises(RuntimeError): verifier.download_release_asset(asset, "synthetic-token", maximum_bytes=100)
        with patch.object(verifier.urllib.request, "build_opener") as opener:
            opener.return_value.open.side_effect = [urllib.error.HTTPError("https://api.github.com/asset", 302, "redirect", {"Location": signed}, None),
                                                   urllib.error.HTTPError(signed, 302, "second redirect", {"Location": "https://evil.example"}, None)]
            with self.assertRaises(urllib.error.HTTPError): verifier.download_release_asset(asset, "synthetic-token", maximum_bytes=100)

    def test_download_digest_size_and_sidecar_each_bind_the_exact_zip(self):
        payload = b"synthetic bytes"
        asset = {"id": 7, "size": len(payload), "digest": "sha256:" + trial.digest(payload)}
        for downloaded in (payload + b"extra", b"x" * len(payload)):
            with self.subTest(downloaded=downloaded), patch.object(verifier.urllib.request, "build_opener") as opener:
                opener.return_value.open.return_value = io.BytesIO(downloaded)
                with self.assertRaises(RuntimeError): verifier.download_release_asset(asset, "synthetic-token", maximum_bytes=100)
        checksum = trial.digest(payload)
        for text in (checksum, checksum + "  trial.zip", checksum + " *trial.zip"):
            verifier.verify_release_sidecar(text.encode(), payload, "trial.zip")
        for text in ("0" * 64, checksum + "  other.zip", checksum + "\n" + checksum, checksum + " **trial.zip"):
            with self.subTest(sidecar=text), self.assertRaises(RuntimeError):
                verifier.verify_release_sidecar(text.encode(), payload, "trial.zip")


if __name__ == "__main__":
    unittest.main()
