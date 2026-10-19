"""Verify both artifacts of one exact private Windows trial run before delivery."""
import io
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import urllib.error
import urllib.parse
import urllib.request
import zipfile

from windows_trial import REPO, WORKFLOW, api, digest, git, save_json, selected_run

PUBLIC_ASSETS = {"logo.svg", "logo-light.svg", "fonts/Tajawal-Regular.ttf", "fonts/Tajawal-Medium.ttf", "fonts/Tajawal-Bold.ttf"}
NATIVE_EXECUTABLES = {"massar-exam-room.exe", "massar-exam-portal-proxy.exe", "massar-exam-network.exe", "hotspot/Massar.Hotspot.exe", "report-pdf/massar-report-pdf.exe"}
RUNTIME_NAMES = {"network.json", "leases.json", "heartbeat.json", "adapter-backup.json", "identity.json", "devices.json", "lan-settings.json", "lan-pending-command.json", "last-opened-build.json", "data-repair-status.json", "portal-mode.json", "capport-public.json", "center.sqlite.lock", "omada-controller.enc", ".env"}
RELEASE_DELIVERY_MARKER = "github-release-assets-v1"
DRAFT_DELIVERY_MARKER = "github-draft-release-assets-v1"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, url):
        return None


def delivery_transport(document):
    workflow = git("show", document["commit"] + ":.github/workflows/center-windows-private.yml").decode("utf-8")
    markers = re.findall(r"(?m)^[ \t]*MASSAR_WINDOWS_DELIVERY:[ \t]*([a-z0-9-]+)[ \t]*$", workflow)
    if "MASSAR_WINDOWS_DELIVERY" not in workflow:
        return "actions-artifact"
    if markers == [DRAFT_DELIVERY_MARKER]:
        return "draft-release-assets"
    if markers != [RELEASE_DELIVERY_MARKER]:
        raise RuntimeError("Unknown or ambiguous sealed Windows delivery transport.")
    return "release-assets"


def _verified_run(document, run, token):
    if api("", token).get("private") is not True and delivery_transport(document) != "draft-release-assets":
        raise RuntimeError("Native Windows delivery must remain in the private repository.")
    actual = api(f"/actions/runs/{run['id']}", token)
    keys = ("id", "run_attempt", "workflow_id", "head_sha", "head_branch", "event", "name", "status", "conclusion")
    if (any(actual.get(key) != run.get(key) for key in keys)
            or actual.get("head_sha") != document["commit"] or actual.get("head_branch") != document["branch"]
            or actual.get("event") != "workflow_dispatch" or actual.get("name") != WORKFLOW
            or actual.get("status") != "completed" or actual.get("conclusion") != "success"):
        raise RuntimeError("The exact native Windows run attempt has not succeeded or changed.")
    jobs = api(f"/actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs?per_page=100", token)["jobs"]
    if (len(jobs) != 2 or {job["name"] for job in jobs} != {"windows (host)", "windows (client)"}
            or any(job["status"] != "completed" or job["conclusion"] != "success" for job in jobs)):
        raise RuntimeError("Both Windows roles must pass in the same build attempt.")


def _actions_delivery(run, role, token):
    artifacts = api(f"/actions/runs/{run['id']}/artifacts?per_page=100", token)["artifacts"]
    name = f"massar-windows-{role}-{run['id']}-{run['run_attempt']}"
    matches = [artifact for artifact in artifacts if artifact["name"] == name and not artifact["expired"]]
    if len(matches) != 1:
        raise RuntimeError("Missing or ambiguous exact-attempt trial artifact.")
    return matches[0]


def _release_delivery(document, run, role, token):
    tag = f"windows-native-{run['id']}-{run['run_attempt']}-{role}"
    draft = delivery_transport(document) == "draft-release-assets"
    if draft:
        from windows_private_delivery import draft_release
        release = draft_release(lambda path: api(path, token), tag)
        if release is None:
            raise RuntimeError("Exact-run draft release is missing.")
    else:
        release = api("/releases/tags/" + tag, token)
    reference = api("/git/ref/tags/" + tag, token)
    if (type(release.get("id")) is not int or release["id"] < 1 or release.get("tag_name") != tag
            or release.get("target_commitish") != document["commit"] or release.get("draft") is not draft
            or release.get("prerelease") is not True
            or reference.get("ref") != "refs/tags/" + tag
            or reference.get("object", {}).get("type") != "commit"
            or reference["object"].get("sha") != document["commit"]):
        raise RuntimeError("Native release tag or target differs from the exact source commit.")
    receipt = json.loads(release.get("body") or "null")
    expected = {"schemaVersion": 1, "transport": "github-draft-release-assets" if draft else "github-release-assets", "repository": REPO,
                "runId": run["id"], "runAttempt": run["run_attempt"], "role": role,
                "sourceCommit": document["commit"]}
    if (not isinstance(receipt, dict) or set(receipt) != set(expected) | {"assets"}
            or any(type(receipt.get(key)) is not int for key in ("schemaVersion", "runId", "runAttempt"))
            or any(receipt.get(key) != value for key, value in expected.items())):
        raise RuntimeError("Native release receipt differs from the exact successful run and role.")
    declared = receipt["assets"]
    if not isinstance(declared, list) or len(declared) != 2 or any(not isinstance(a, dict) for a in declared):
        raise RuntimeError("Native release must declare exactly one ZIP and checksum sidecar.")
    prefix = f"massar-center-windows-{role}-run-{run['id']}-{run['run_attempt']}-"
    archives = [a for a in declared if re.fullmatch(re.escape(prefix) + r"[a-f0-9]{16}\.zip", str(a.get("name", "")))]
    if len(archives) != 1:
        raise RuntimeError("Native release ZIP name differs from this exact role and run attempt.")
    archive = archives[0]
    sidecars = [a for a in declared if a.get("name") == archive["name"] + ".sha256"]
    if len(sidecars) != 1:
        raise RuntimeError("Native release checksum sidecar name differs from the ZIP.")
    assets = release.get("assets", [])
    if (not isinstance(assets, list) or len(assets) != 2
            or {a.get("name") for a in assets} != {a["name"] for a in declared}
            or len({a.get("id") for a in assets}) != 2):
        raise RuntimeError("Native release contains unexpected or ambiguous assets.")
    selected = []
    for metadata in (archive, sidecars[0]):
        limit = 512 * 1024 * 1024 if metadata is archive else 4096
        if (set(metadata) != {"name", "size", "sha256"} or type(metadata.get("size")) is not int
                or not 0 < metadata["size"] <= limit
                or re.fullmatch(r"[a-f0-9]{64}", str(metadata.get("sha256", ""))) is None):
            raise RuntimeError("Native release asset declaration is invalid.")
        asset = next(a for a in assets if a["name"] == metadata["name"])
        if (type(asset.get("id")) is not int or asset["id"] < 1 or asset.get("state") != "uploaded"
                or asset.get("size") != metadata["size"] or asset.get("digest") != "sha256:" + metadata["sha256"]):
            raise RuntimeError("Native release asset identity, upload state or digest differs from its receipt.")
        selected.append(asset)
    return {"release": release, "zip": selected[0], "sidecar": selected[1]}


def verify_delivery_binding(document, run, evidence, token):
    """Recheck retained delivery evidence before promoting the exact native bytes."""
    _verified_run(document, run, token)
    transport = delivery_transport(document)
    role = evidence.get("role")
    if role not in ("host", "client"):
        raise RuntimeError("Unexpected native delivery role.")
    if transport == "actions-artifact":
        artifact = _actions_delivery(run, role, token)
        if (evidence.get("transport", transport) != transport or artifact["id"] != evidence.get("artifactId")
                or artifact.get("digest") != "sha256:" + evidence["outerSha256"]):
            raise RuntimeError("Actions artifact differs from the retained exact-attempt evidence.")
        return
    delivery = _release_delivery(document, run, role, token)
    release, archive, sidecar = delivery["release"], delivery["zip"], delivery["sidecar"]
    expected = {"transport": transport, "sourceCommit": document["commit"], "version": document["version"],
                "releaseId": release["id"], "releaseTag": release["tag_name"],
                "artifactId": archive["id"], "assetName": archive["name"], "size": archive["size"],
                "sha256": archive["digest"][7:], "assetSha256": archive["digest"][7:],
                "outerSha256": archive["digest"][7:], "sidecarAssetId": sidecar["id"],
                "sidecarSize": sidecar["size"], "sidecarSha256": sidecar["digest"][7:]}
    if any(evidence.get(key) != value for key, value in expected.items()):
        raise RuntimeError("Release asset differs from the retained exact-attempt evidence.")
    if archive["name"] != f"massar-center-windows-{role}-run-{run['id']}-{run['run_attempt']}-{evidence.get('buildId')}.zip":
        raise RuntimeError("Retained native build identity differs from its release asset name.")


def download_release_asset(asset, token, *, maximum_bytes):
    request = urllib.request.Request(f"https://api.github.com/repos/{REPO}/releases/assets/{asset['id']}",
        headers={"Authorization": "Bearer " + token, "Accept": "application/octet-stream", "User-Agent": "Massar-Windows-Trial"})
    opener = urllib.request.build_opener(NoRedirect)
    try:
        response = opener.open(request, timeout=45)
    except urllib.error.HTTPError as error:
        if error.code not in (302, 303, 307, 308):
            raise
        location = error.headers.get("Location", "")
        error.close()
        parsed = urllib.parse.urlsplit(location)
        if (parsed.scheme != "https" or parsed.hostname not in ("release-assets.githubusercontent.com", "objects.githubusercontent.com")
                or parsed.username or parsed.password or parsed.port not in (None, 443) or parsed.fragment):
            raise RuntimeError("Unsafe native release asset redirect.")
        response = opener.open(urllib.request.Request(location, headers={"User-Agent": "Massar-Windows-Trial"}), timeout=120)
    with response:
        content = response.read(maximum_bytes + 1)
    if len(content) > maximum_bytes or len(content) != asset["size"] or asset["digest"] != "sha256:" + digest(content):
        raise RuntimeError("Native release asset bytes differ from the exact declared size or digest.")
    return content


def verify_release_sidecar(content, payload, name):
    text = content.decode("utf-8-sig").strip()
    if re.fullmatch(r"[a-fA-F0-9]{64}(?:[ \t]+\*?[^\r\n]+)?", text) is None:
        raise RuntimeError("Native release checksum sidecar must contain one checksum line.")
    parts = text.split(maxsplit=1)
    if parts[0].lower() != digest(payload) or (len(parts) > 1 and parts[1].removeprefix("*") != name):
        raise RuntimeError("Native release checksum sidecar does not match the exact ZIP.")


def download_artifact(artifact, token):
    request = urllib.request.Request(f"https://api.github.com/repos/{REPO}/actions/artifacts/{artifact['id']}/zip", headers={"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json", "User-Agent": "Massar-Windows-Trial"})
    try:
        response = urllib.request.build_opener(NoRedirect).open(request, timeout=45)
    except urllib.error.HTTPError as error:
        if error.code not in (302, 303, 307, 308):
            raise
        location = error.headers["Location"]
        parsed = urllib.parse.urlparse(location)
        if parsed.scheme != "https" or not parsed.hostname or parsed.username:
            raise RuntimeError("Unsafe artifact redirect.")
        response = urllib.request.urlopen(urllib.request.Request(location, headers={"User-Agent": "Massar-Windows-Trial"}), timeout=120)
    with response:
        content = response.read(512 * 1024 * 1024 + 1)
    if len(content) > 512 * 1024 * 1024:
        raise RuntimeError("Unexpectedly large trial artifact.")
    if artifact.get("digest") != "sha256:" + digest(content):
        raise RuntimeError("Downloaded artifact checksum mismatch.")
    return content


def unique_suffix(names, suffix):
    matches = [name for name in names if name == suffix or name.endswith("/" + suffix)]
    if len(matches) != 1:
        raise RuntimeError("Expected one archive entry: " + suffix)
    return matches[0]


def unwrap_artifact(content):
    with zipfile.ZipFile(io.BytesIO(content)) as wrapper:
        names = wrapper.namelist()
        if wrapper.testzip() is not None or len(names) != len(set(names)):
            raise RuntimeError("Corrupt or duplicate artifact entries.")
        archives = [name for name in names if name.endswith(".zip")]
        sidecars = [name for name in names if name.endswith(".sha256")]
        if len(archives) != 1 or len(sidecars) != 1:
            raise RuntimeError("Expected one Windows ZIP and checksum sidecar.")
        payload = wrapper.read(archives[0])
        sidecar = wrapper.read(sidecars[0]).decode("utf-8-sig").split()
        if not sidecar or sidecar[0].lower() != digest(payload):
            raise RuntimeError("Windows ZIP checksum differs from CI sidecar.")
        return payload


def assert_archive_safe(archive):
    names = archive.namelist()
    if len(names) > 10000 or sum(member.file_size for member in archive.infolist()) > 1024 * 1024 * 1024:
        raise RuntimeError("Unexpectedly large unpacked Windows archive.")
    if len(names) != len(set(names)) or archive.testzip() is not None:
        raise RuntimeError("Duplicate or corrupt Windows archive entries.")
    for member in archive.infolist():
        name = member.filename
        if "\\" in name or ":" in name or name.startswith("/") or ".." in PurePosixPath(name).parts or ((member.external_attr >> 16) & 0o170000) == 0o120000:
            raise RuntimeError("Unsafe Windows archive path.")
        if member.is_dir():
            continue
        base = PurePosixPath(name).name.lower()
        if any(base == private or base.startswith(private + ".") for private in RUNTIME_NAMES) or base.endswith((".pem", ".key", ".pfx")) or re.search(r"\.(sqlite3?|db)(-wal|-shm|-journal)?$", base):
            raise RuntimeError("Private runtime metadata is present in the Windows ZIP.")
        with archive.open(member) as stream:
            if stream.read(16) == b"SQLite format 3\0":
                raise RuntimeError("Runtime database hidden under another filename.")
        if base.endswith(".json") and re.search(rb'"format"\s*:\s*"massar-center-backup"', archive.read(member)):
            raise RuntimeError("A runtime backup is present in the Windows ZIP.")


def assert_native_runtime(archive, manifest):
    runtime = manifest.get("examRuntime", {})
    flags = ("windowsGoTestsPassed", "windowsCredentialReconnectTestsPassed", "windowsCliSmokePassed", "syntheticPdfAndZipPassed")
    if runtime.get("schemaVersion") != 1 or any(runtime.get(flag) is not True for flag in flags) or runtime.get("includesRuntimeDatabase") is not False or runtime.get("physicalWindowsVerified") is not False or set(runtime.get("networkModes", [])) != {"legacy", "https"}:
        raise RuntimeError("Native Windows verification is incomplete.")
    hashes = runtime.get("executables", {})
    if set(hashes) != NATIVE_EXECUTABLES:
        raise RuntimeError("Unexpected native runtime executables.")
    names = archive.namelist()
    for name, expected in hashes.items():
        content = archive.read(unique_suffix(names, name))
        if not content.startswith(b"MZ") or digest(content) != expected:
            raise RuntimeError("Native executable differs from the tested binary.")
    for name in ("hotspot/Massar.Hotspot.runtimeconfig.json", "hotspot/coreclr.dll", "hotspot/Microsoft.Windows.SDK.NET.dll", "Windows-exam-setup.txt"):
        unique_suffix(names, name)
    if not any("report-pdf/_internal/" in name for name in names):
        raise RuntimeError("PDF worker dependencies are missing.")


def assert_assets(archive, manifest, document):
    marker = "data/flutter_assets/assets/"
    if any(marker in name and not name.startswith(marker) for name in archive.namelist()):
        raise RuntimeError("Unexpected nested or duplicate application asset roots.")
    assets = {name.split(marker, 1)[1]: name for name in archive.namelist() if marker in name and not name.endswith("/")}
    if manifest["role"] == "client":
        if set(assets) != PUBLIC_ASSETS or manifest.get("seedId") is not None or manifest.get("students") != 0 or manifest.get("groups") != 0 or manifest.get("seedSha256") is not None:
            raise RuntimeError("Client contains missing/publicly unapproved assets or host data.")
        return
    private_inputs = document["privateInputs"]["files"]
    if {name for name in assets if name.endswith(".json")} != set(private_inputs):
        raise RuntimeError("Host private asset set differs from reviewed inputs.")
    for name, expected in private_inputs.items():
        if digest(archive.read(assets[name])) != expected:
            raise RuntimeError("Host private asset differs from reviewed bytes.")
    if manifest.get("seedSha256") != private_inputs["installation_seed.json"]:
        raise RuntimeError("Host seed manifest digest mismatch.")
    for name in ("index.html", "app.js", "scanner.js", "style.css", "ZXING-LICENSE"):
        relative = "mobile-homework/" + name
        reviewed = git("show", document["commit"] + ":desktop/center-flutter/assets/" + relative)
        if archive.read(assets[relative]).replace(b"\r\n", b"\n") != reviewed.replace(b"\r\n", b"\n"):
            raise RuntimeError("Mobile homework asset differs from sealed source.")


def verify_archive(payload, document, role):
    with zipfile.ZipFile(io.BytesIO(payload)) as archive:
        assert_archive_safe(archive)
        names = archive.namelist()
        manifest = json.loads(archive.read(unique_suffix(names, "build-manifest.json")).decode("utf-8-sig"))
        if manifest.get("role") != role or manifest.get("appVersion") != document["version"] or manifest.get("sourceCommit") != document["commit"] or re.fullmatch(r"[a-f0-9]{16}", manifest.get("buildId", "")) is None:
            raise RuntimeError("Trial role, version or source identity mismatch.")
        if digest(archive.read(unique_suffix(names, "data/app.so"))) != manifest["aotSha256"]:
            raise RuntimeError("Trial AOT differs from its build manifest.")
        for name in ("massar_center.exe", "massar-lan-host.exe", "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll"):
            unique_suffix(names, name)
        assert_native_runtime(archive, manifest)
        assert_assets(archive, manifest, document)
        return manifest


def save_archive(path, content):
    if path.exists():
        if path.stat().st_size != len(content) or digest(path.read_bytes()) != digest(content):
            raise RuntimeError("A different trial bundle already exists; preserved it.")
        return
    if shutil.disk_usage(path.parent).free < len(content) + 32 * 1024 * 1024:
        raise RuntimeError("Insufficient free disk space for the verified trial archive.")
    with path.open("xb") as stream:
        stream.write(content)
    path.chmod(0o600)
    if path.stat().st_size != len(content) or digest(path.read_bytes()) != digest(content):
        raise RuntimeError("Saved trial archive failed read-back verification.")


def download_trial(document, output, token):
    run = selected_run(document, output, token)
    if run is None or run["status"] != "completed" or run["conclusion"] != "success":
        raise RuntimeError("The exact trial build has not succeeded.")
    _verified_run(document, run, token)
    transport = delivery_transport(document)
    releases = []
    for role in ("host", "client"):
        if transport in ("release-assets", "draft-release-assets"):
            delivery = _release_delivery(document, run, role, token)
            release, archive, sidecar = delivery["release"], delivery["zip"], delivery["sidecar"]
            payload = download_release_asset(archive, token, maximum_bytes=512 * 1024 * 1024)
            checksum = download_release_asset(sidecar, token, maximum_bytes=4096)
            verify_release_sidecar(checksum, payload, archive["name"])
            identity = {"transport": transport, "releaseId": release["id"], "releaseTag": release["tag_name"],
                        "artifactId": archive["id"], "assetName": archive["name"], "assetSha256": digest(payload),
                        "outerSha256": digest(payload), "sidecarAssetId": sidecar["id"],
                        "sidecarSize": len(checksum), "sidecarSha256": digest(checksum)}
        else:
            artifact = _actions_delivery(run, role, token)
            outer = download_artifact(artifact, token)
            payload = unwrap_artifact(outer)
            identity = {"artifactId": artifact["id"], "outerSha256": digest(outer)}
        manifest = verify_archive(payload, document, role)
        if transport in ("release-assets", "draft-release-assets") and archive["name"] != f"massar-center-windows-{role}-run-{run['id']}-{run['run_attempt']}-{manifest['buildId']}.zip":
            raise RuntimeError("Native release asset name differs from its verified packaged build identity.")
        path = output / f"massar-windows-{role}-trial-{document['version'].replace('+', '-')}.zip"
        save_archive(path, payload)
        evidence = {"role": role, "version": document["version"], "sourceCommit": document["commit"], "buildId": manifest["buildId"], "sha256": digest(payload), "size": len(payload), "path": str(path), **identity, "nativeChecksPassed": True, "physicalWindowsVerified": False}
        verify_delivery_binding(document, run, evidence, token)
        save_json(output / (role + "-verification.json"), evidence)
        releases.append(evidence)
        print(role + " trial verified and saved", flush=True)
    save_json(output / "verified-trial.json", {"status": "verified_private_trial", "runId": run["id"], "runAttempt": run["run_attempt"], "releases": releases, "productionPromoted": False, "physicalWindowsVerified": False})
