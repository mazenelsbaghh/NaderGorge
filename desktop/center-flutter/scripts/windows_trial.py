"""Prepare and build private Windows trials without touching production releases.

Only `seal` writes Git objects; only `dispatch` pushes the named private trial
branch and starts CI. There is deliberately no production publication command.
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import urllib.parse
import urllib.request
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[3]
REPO = "mazenelsbaghh/massar-center-desktop"
WORKFLOW = "Massar Windows private build"
WORKFLOW_FILE = "center-windows-private.yml"
SOURCE_PREFIXES = ("desktop/center-flutter/", "desktop/center-lan/", "exam-room/")
sys.path.insert(0, str(ROOT / "deploy/production/scripts"))
from source_manifest import sensitive_path_reason, secret_content_reason_bytes
from release_images import committed_source_entries
from windows_package_assets import HISTORICAL_PRIVATE_ASSETS, PRIVATE_INSTALLER_ASSETS, GRADE_REPAIR_ASSET, ATTENDANCE_REPAIR_ASSET, JABER_ATTENDANCE_REPAIR_ASSET, SHOBRA_ATTENDANCE_REPAIR_ASSET, HARAM_ATTENDANCE_REPAIR_ASSET

GENERATED_PARTS = {"build", "dist", "node_modules", ".dart_tool", "__pycache__", ".pytest_cache", "obj", "bin", "ephemeral"}
LOCAL_ONLY_SOURCES = {"desktop/center-flutter/scripts/recover-windows-exam-setup.ps1"}
NATIVE_SOURCE_ROOTS = (
    ("desktop/center-flutter/lib", ".dart"),
    ("desktop/center-flutter/test", ".dart"),
    ("desktop/center-flutter/integration_test", ".dart"),
    ("desktop/center-lan", ".go"),
    ("exam-room", ".go"),
    ("exam-room/cmd/portal-proxy", ".html"),
    ("exam-room/frontend/src", ".jsx"),
    ("exam-room/frontend/src", ".js"),
    ("exam-room/windows-hotspot", ".cs"),
)
TRIAL_BROWSER_TESTS = {
    "exam-room/tests/center-wired-room.cjs",
    "exam-room/tests/center-embedded-ui.cjs",
    "exam-room/tests/whatsapp-center-bindings.cjs",
}


def digest(content):
    return hashlib.sha256(content).hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path, document):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n")
    temporary.chmod(0o600)
    os.replace(temporary, path)


def git(*arguments, env=None, content=None):
    completed = subprocess.run(
        ["git", "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=false", *arguments],
        cwd=ROOT, env=env, input=content, capture_output=True, check=False,
    )
    if completed.returncode:
        raise RuntimeError("Git operation failed: " + arguments[0])
    return completed.stdout


def source_path(name):
    path = Path(name)
    if path.is_absolute() or ".." in path.parts or path.as_posix() != name:
        raise RuntimeError("Unsafe source path.")
    if not name.startswith(SOURCE_PREFIXES) and name != ".github/workflows/" + WORKFLOW_FILE:
        raise RuntimeError("Trial source is restricted to the desktop/exam build scope.")
    if name in LOCAL_ONLY_SOURCES:
        raise RuntimeError("Device-specific incident recovery must remain local.")
    if "private-import" in path.parts:
        raise RuntimeError("Private data-repair work must remain local.")
    if "assets/" in name and name.endswith(".json"):
        raise RuntimeError("Private installer JSON must not enter the source export.")
    if path.name.lower().startswith("omada-controller.enc"):
        raise RuntimeError("Saved Omada credentials must not enter the source export.")
    if any(part in GENERATED_PARTS for part in path.parts):
        raise RuntimeError("Generated build content must not enter the source export.")
    if sensitive_path_reason(name):
        raise RuntimeError("Sensitive source path rejected: " + name)
    return path


def source_entry(name):
    path = ROOT / source_path(name)
    if not path.is_file() or path.is_symlink() or not path.resolve().is_relative_to(ROOT):
        raise RuntimeError("Source must be an existing regular file: " + name)
    content = path.read_bytes()
    if secret_content_reason_bytes(content, name)[0]:
        raise RuntimeError("Source content failed privacy review: " + name)
    return {"path": name, "sha256": digest(content)}


def native_source_paths():
    # Local excludes may hide an entire native project. New build inputs still
    # belong to the reviewed source; only generated directories are pruned.
    result = {name for name in TRIAL_BROWSER_TESTS if (ROOT / name).is_file()}
    for prefix, extension in NATIVE_SOURCE_ROOTS:
        for directory, children, files in os.walk(ROOT / prefix):
            children[:] = [name for name in children if name not in GENERATED_PARTS and name not in {".git", "private-import"}]
            for name in files:
                if Path(name).suffix == extension:
                    result.add((Path(directory) / name).relative_to(ROOT).as_posix())
    return result


def changed_source_paths(parent):
    roots = [*SOURCE_PREFIXES, ".github/workflows/" + WORKFLOW_FILE]
    baseline = {}
    for row in git("ls-tree", "-r", "-z", parent, "--", *roots).split(b"\0"):
        if row:
            metadata, name = row.split(b"\t", 1)
            baseline[name.decode()] = metadata.decode().split()[2]
    working = {name.decode() for name in git("ls-files", "--cached", "--others", "--exclude-standard", "-z", "--", *roots).split(b"\0") if name}
    working.update(native_source_paths())
    changed = []
    for name in sorted(working | set(baseline)):
        if name in LOCAL_ONLY_SOURCES:
            if name in baseline:
                raise RuntimeError("Reviewed parent contains device-specific recovery; remove it through an explicit source review.")
            continue
        if any(part in GENERATED_PARTS for part in Path(name).parts) or ("assets/" in name and name.endswith(".json")):
            continue
        path = ROOT / source_path(name)
        if not path.is_file():
            raise RuntimeError("Source deletion needs explicit review before export: " + name)
        content = path.read_bytes()
        blob = hashlib.sha1(b"blob " + str(len(content)).encode() + b"\0" + content).hexdigest()
        if baseline.get(name) != blob:
            source_entry(name)
            changed.append(name)
    return changed


def assert_scope_complete(document):
    omitted = set(changed_source_paths(document["parent"])) - {entry["path"] for entry in document["files"]}
    if omitted:
        raise RuntimeError("Changed/new build source omitted from allowlist: " + ", ".join(sorted(omitted)))


def private_archive(payload, expected):
    """Read a closed root-only archive without extracting untrusted paths."""
    with zipfile.ZipFile(io.BytesIO(payload)) as archive:
        names = archive.namelist()
        if len(names) != len(expected) or set(names) != set(expected):
            raise RuntimeError("Unexpected private packaging archive contents.")
        if any(entry.file_size > 64 * 1024 * 1024 or (entry.external_attr >> 16) & 0o170000 == 0o120000
               for entry in archive.infolist()):
            raise RuntimeError("Private packaging entries must be bounded regular JSON files.")
        values = {name: archive.read(name) for name in names}
        if any(digest(content) != expected[name] for name, content in values.items()):
            raise RuntimeError("Private asset digest mismatch.")
        for content in values.values():
            if not isinstance(json.loads(content), dict):
                raise RuntimeError("Private assets require JSON objects.")
        return values


def private_upload_inputs(directory, manifest, expected_names):
    files = manifest.get("files")
    if not isinstance(files, dict) or set(files) != expected_names or any(
            not isinstance(value, str) or not re.fullmatch(r"[a-f0-9]{64}", value) for value in files.values()):
        raise RuntimeError("Unexpected private asset manifest.")
    asset = read_json(directory / "private-assets-upload.json")
    package = directory / "private-packaging-assets.zip"
    payload = package.read_bytes()
    if len(payload) > 256 * 1024 * 1024 or digest(payload) != manifest.get("sha256") or asset.get("sha256") != manifest["sha256"]:
        raise RuntimeError("Private asset upload is not bound to the verified packaging archive.")
    if type(asset.get("id")) is not int or asset["id"] <= 0:
        raise RuntimeError("Private packaging requires a verified uploaded asset identity.")
    private_archive(payload, files)
    return {"files": files, "assetId": asset["id"], "sha256": manifest["sha256"], "size": len(payload)}


def baseline_inputs(directory):
    published = read_json(directory / "published.json")
    manifest = read_json(directory / "private-packaging-manifest.json")
    inputs = private_upload_inputs(directory, manifest, HISTORICAL_PRIVATE_ASSETS)
    release = next(item for item in published["releases"] if item["role"] == "host")
    archive = directory / Path(release["downloadPath"]).name
    payload = archive.read_bytes()
    if published.get("verifiedPrivateDownloads") is not True or digest(payload) != release["sha256"] or len(payload) != release["size"]:
        raise RuntimeError("Private inputs require the previously verified host ZIP.")
    with zipfile.ZipFile(io.BytesIO(payload)) as bundle:
        for name, expected in inputs["files"].items():
            matches = [entry for entry in bundle.namelist() if entry.endswith("data/flutter_assets/assets/" + name)]
            if len(matches) != 1 or digest(bundle.read(matches[0])) != expected:
                raise RuntimeError("Historical private asset digest mismatch.")
    return inputs


def verified_attendance_review(review, asset_name, inputs):
    if not isinstance(review, dict) or review.get("status") != "verified" or review.get("ownerAuthorized") is not True:
        raise RuntimeError("Attendance repair requires completed owner-authorized verification.")
    if review.get("repairSha256") != inputs["files"][asset_name] or any(
            not isinstance(review.get(field), str) or not re.fullmatch(r"[a-f0-9]{64}", review[field])
            for field in ("sourceSha256", "sourceBackupSha256")):
        raise RuntimeError("Attendance repair review does not match the private inputs.")
    return review


def private_inputs(baseline, overlay=None):
    # Historical verification always happens first, even when an overlay is supplied.
    historical = baseline_inputs(baseline)
    if overlay is None:
        return historical
    root = (ROOT / "artifacts/private").resolve()
    directory = overlay.absolute()
    if not directory.resolve().is_relative_to(root) or directory.resolve() != directory:
        raise RuntimeError("Private overlay must be a canonical folder under artifacts/private.")
    names = {"private-packaging-assets.zip", "private-packaging-manifest.json", "private-assets-upload.json", "grade-repair-review.json"}
    if not directory.is_dir() or any(not (directory / name).is_file() or (directory / name).is_symlink() for name in names):
        raise RuntimeError("Private overlay requires regular reviewed packaging files.")
    manifest = read_json(directory / "private-packaging-manifest.json")
    if manifest.get("schemaVersion") != 1 or manifest.get("privateOnly") is not True or manifest.get("baselineSha256") != historical["sha256"]:
        raise RuntimeError("Private overlay is not bound to the historical baseline.")
    inputs = private_upload_inputs(directory, manifest, PRIVATE_INSTALLER_ASSETS)
    if any(inputs["files"][name] != value for name, value in historical["files"].items()):
        raise RuntimeError("Private overlay may only add reviewed repairs; historical assets are immutable.")
    review_path = directory / "grade-repair-review.json"
    if manifest.get("reviewManifest") != review_path.name or manifest.get("reviewManifestSha256") != digest(review_path.read_bytes()):
        raise RuntimeError("Private overlay review manifest digest mismatch.")
    review = read_json(review_path)
    if review.get("status") != "verified" or review.get("ownerAuthorized") is not True:
        raise RuntimeError("Private grade repair requires completed owner-authorized verification.")
    if not isinstance(review.get("sourceBackupSha256"), str) or not re.fullmatch(r"[a-f0-9]{64}", review["sourceBackupSha256"]):
        raise RuntimeError("Private grade repair requires a reviewed source backup digest.")
    if review.get("installationSeedSha256") != historical["files"]["installation_seed.json"] or review.get("repairSha256") != inputs["files"][GRADE_REPAIR_ASSET]:
        raise RuntimeError("Private grade repair review does not match the installer inputs.")
    attendance = verified_attendance_review(review.get("attendanceRepair"), ATTENDANCE_REPAIR_ASSET, inputs)
    jaber = verified_attendance_review(review.get("jaberAttendanceRepair"), JABER_ATTENDANCE_REPAIR_ASSET, inputs)
    shobra = verified_attendance_review(review.get("shobraAttendanceRepair"), SHOBRA_ATTENDANCE_REPAIR_ASSET, inputs)
    haram = verified_attendance_review(review.get("haramAttendanceRepair"), HARAM_ATTENDANCE_REPAIR_ASSET, inputs)
    inputs.update({"baselineSha256": historical["sha256"], "reviewManifestSha256": manifest["reviewManifestSha256"],
                   "sourceBackupSha256": review["sourceBackupSha256"], "repairSha256": review["repairSha256"],
                   "attendanceRepair": attendance, "jaberAttendanceRepair": jaber,
                   "shobraAttendanceRepair": shobra, "haramAttendanceRepair": haram})
    return inputs


def validate_trial_identity(version, branch):
    if not re.fullmatch(r"\d{1,6}\.\d{1,6}\.\d{1,6}\+\d{1,10}", version):
        raise RuntimeError("Trial version requires an explicit numeric build.")
    if not re.fullmatch(r"codex/trial-[a-z0-9]+(?:-[a-z0-9]+)*", branch):
        raise RuntimeError("Only a named codex/trial- branch may be pushed.")


def prepare(args):
    validate_trial_identity(args.version, args.branch)
    if not re.fullmatch(r"[a-f0-9]{40}", args.parent) or git("cat-file", "-t", args.parent).strip() != b"commit":
        raise RuntimeError("Use an exact reviewed source parent already present locally.")
    names = read_json(args.scope)
    if not isinstance(names, list) or not names or len(names) != len(set(names)):
        raise RuntimeError("An explicit unique source allowlist is required.")
    if "desktop/center-flutter/pubspec.yaml" not in names:
        raise RuntimeError("The source allowlist must include the trial version.")
    versions = re.findall(r"^version:\s*(\S+)", (ROOT / "desktop/center-flutter/pubspec.yaml").read_text(), re.M)
    if versions != [args.version]:
        raise RuntimeError("The trial version differs from pubspec.yaml.")
    document = {"schemaVersion": 1, "channel": "private-trial", "repository": REPO,
                "parent": args.parent, "version": args.version, "branch": args.branch,
                "files": [source_entry(name) for name in sorted(names)],
                "privateInputs": private_inputs(args.baseline, getattr(args, "private_overlay", None)), "productionPromoted": False}
    assert_scope_complete(document)
    if (args.output / "source-candidate.json").exists():
        raise RuntimeError("This trial is already sealed; use a fresh trial directory for changed source.")
    save_json(args.output / "source-inventory.json", document)
    print(json.dumps({"prepared": True, "sourceFiles": len(names), "version": args.version}))


def assert_source_unchanged(document):
    validate_trial_identity(document["version"], document["branch"])
    if document["repository"] != REPO or document["channel"] != "private-trial":
        raise RuntimeError("Unexpected trial destination.")
    for entry in document["files"]:
        if source_entry(entry["path"]) != entry:
            raise RuntimeError("Reviewed source changed: " + entry["path"])
    assert_scope_complete(document)


def assert_sealed_commit(document):
    if git("rev-parse", document["commit"] + "^").decode().strip() != document["parent"]:
        raise RuntimeError("Trial commit no longer matches the reviewed parent.")
    for entry in document["files"]:
        if digest(git("show", document["commit"] + ":" + entry["path"])) != entry["sha256"]:
            raise RuntimeError("Sealed Git source differs from reviewed source: " + entry["path"])
    actual = {name.decode() for name in git("diff", "--name-only", "-z", document["parent"], document["commit"]).split(b"\0") if name}
    if actual != set(document["changedPaths"]):
        raise RuntimeError("Sealed Git change inventory differs from its reviewed record.")


def seal(args):
    if not args.verified:
        raise RuntimeError("Seal requires --verified after source review and relevant tests.")
    document = read_json(args.output / "source-inventory.json")
    assert_source_unchanged(document)
    candidate_path = args.output / "source-candidate.json"
    if candidate_path.exists():
        raise RuntimeError("Trial source is already sealed.")
    index_path = Path(git("rev-parse", "--git-path", "index").decode().strip())
    if not index_path.is_absolute():
        index_path = ROOT / index_path
    index_before = index_path.read_bytes() if index_path.exists() else None
    trial_index = args.output / ("trial-" + uuid.uuid4().hex + ".index")
    environment = dict(os.environ, GIT_INDEX_FILE=str(trial_index.resolve()))
    git("read-tree", document["parent"], env=environment)
    for entry in document["files"]:
        content = (ROOT / entry["path"]).read_bytes()
        if digest(content) != entry["sha256"]:
            raise RuntimeError("Source changed while sealing the trial.")
        blob = git("hash-object", "-w", "--stdin", content=content).decode().strip()
        previous = git("ls-tree", document["parent"], "--", entry["path"]).decode()
        mode = previous.split()[0] if previous else "100644"
        git("update-index", "--add", "--cacheinfo", mode, blob, entry["path"], env=environment)
    tree = git("write-tree", env=environment).decode().strip()
    commit = git("commit-tree", tree, "-p", document["parent"], content=b"Build private Windows trial without production promotion\n").decode().strip()
    actual = {name.decode() for name in git("diff", "--name-only", "-z", document["parent"], commit).split(b"\0") if name}
    if not actual.issubset({entry["path"] for entry in document["files"]}):
        raise RuntimeError("Unexpected source in trial candidate.")
    complete = {name.decode() for name in git("ls-tree", "-r", "--name-only", "-z", commit).split(b"\0") if name}
    if complete != {str(entry["path"]) for entry in committed_source_entries(ROOT, commit)}:
        raise RuntimeError("Private or excluded content in source tree.")
    git("diff", "--check", document["parent"], commit)
    if (index_path.read_bytes() if index_path.exists() else None) != index_before:
        raise RuntimeError("The working index changed concurrently; review it before dispatch.")
    document.update({"commit": commit, "changedPaths": sorted(actual)})
    save_json(candidate_path, document)
    print(json.dumps({"sealed": True, "commit": commit, "branch": document["branch"]}))


def credential():
    completed = subprocess.run(["git", "credential", "fill"], input=b"protocol=https\nhost=github.com\n\n", capture_output=True, check=True)
    fields = dict(line.split("=", 1) for line in completed.stdout.decode().splitlines() if "=" in line)
    if not fields.get("password"):
        raise RuntimeError("GitHub credential helper is unavailable.")
    return fields["password"]


def api(path, token, body=None):
    request = urllib.request.Request("https://api.github.com/repos/" + REPO + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers={"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json",
                 "Content-Type": "application/json", "User-Agent": "Massar-Windows-Trial"})
    with urllib.request.urlopen(request, timeout=45) as response:
        content = response.read()
        return json.loads(content) if content else None


def matching_runs(document, token):
    query = urllib.parse.urlencode({"branch": document["branch"], "event": "workflow_dispatch", "per_page": 100})
    runs = api("/actions/workflows/" + WORKFLOW_FILE + "/runs?" + query, token)["workflow_runs"]
    return [run for run in runs if run["head_sha"] == document["commit"] and run["head_branch"] == document["branch"] and run["name"] == WORKFLOW]


def selected_run(document, output, token):
    selected_path = output / "build-run.json"
    if selected_path.exists():
        selected = read_json(selected_path)
        run = api("/actions/runs/" + str(selected["id"]), token)
        if run["run_attempt"] != selected["run_attempt"]:
            raise RuntimeError("Run attempt changed; review the rerun before selecting it.")
    else:
        runs = matching_runs(document, token)
        if not runs:
            return None
        if len(runs) != 1:
            raise RuntimeError("Multiple trial runs exist; select one explicitly after review.")
        run = runs[0]
    if run["head_sha"] != document["commit"] or run["head_branch"] != document["branch"] or run["event"] != "workflow_dispatch" or run["name"] != WORKFLOW:
        raise RuntimeError("Run is not bound to the sealed trial source.")
    save_json(selected_path, {key: run[key] for key in ("id", "run_attempt", "workflow_id", "head_sha", "head_branch", "event", "name", "status", "conclusion", "html_url")})
    return run


def dispatch(document, output, token):
    assert_source_unchanged(document)
    assert_sealed_commit(document)
    from windows_trial_verify import delivery_transport
    repository = api("", token)
    if repository["private"] is not True and delivery_transport(document) != "draft-release-assets":
        raise RuntimeError("A public build requires the protected draft-delivery workflow.")
    inputs = document["privateInputs"]
    asset = api("/releases/assets/" + str(inputs["assetId"]), token)
    if asset["size"] != inputs["size"] or asset.get("digest") != "sha256:" + inputs["sha256"]:
        raise RuntimeError("Private packaging asset changed before dispatch.")
    if matching_runs(document, token):
        print("Existing trial run reused; no duplicate dispatch.")
        return
    if (output / "dispatch.json").exists():
        raise RuntimeError("Dispatch was already requested; inspect status instead of resubmitting.")
    reference = "refs/heads/" + document["branch"]
    existing = git("ls-remote", "https://github.com/" + REPO + ".git", reference).decode().strip()
    if existing and existing.split()[0] != document["commit"]:
        raise RuntimeError("Trial branch exists with a different commit; never force-push it.")
    if not existing:
        git("push", "https://github.com/" + REPO + ".git", document["commit"] + ":" + reference)
    # Persist intent first: an uncertain HTTP result must not start duplicate builds.
    save_json(output / "dispatch.json", {"commit": document["commit"], "branch": document["branch"], "requested": True})
    api("/actions/workflows/" + WORKFLOW_FILE + "/dispatches", token, {"ref": document["branch"], "inputs": {"private_assets_id": str(inputs["assetId"]), "private_assets_sha256": inputs["sha256"]}})
    print("Private host/client trial build dispatched; production unchanged.")


def inspect_build(document, output, token):
    run = selected_run(document, output, token)
    if run is None:
        print("The dispatched trial run is not visible yet.")
        return
    jobs = api(f"/actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs?per_page=100", token)["jobs"]
    print(json.dumps({"id": run["id"], "attempt": run["run_attempt"], "status": run["status"], "conclusion": run["conclusion"], "jobs": [{"name": job["name"], "status": job["status"], "conclusion": job["conclusion"]} for job in jobs]}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("scan", "prepare", "seal", "dispatch", "status", "download"))
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--scope", type=Path)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--private-overlay", type=Path)
    parser.add_argument("--parent")
    parser.add_argument("--version")
    parser.add_argument("--branch")
    parser.add_argument("--verified", action="store_true")
    args = parser.parse_args()
    args.output = args.output.resolve()
    if not args.output.is_relative_to(ROOT / "artifacts/private"):
        parser.error("Trial evidence belongs under canonical artifacts/private.")
    if args.action == "scan":
        if not args.parent or not re.fullmatch(r"[a-f0-9]{40}", args.parent):
            parser.error("scan requires an exact --parent SHA")
        paths = changed_source_paths(args.parent)
        save_json(args.output / "source-paths.json", paths)
        print(json.dumps({"changedSourceFiles": len(paths), "allowlist": str(args.output / "source-paths.json")}))
    elif args.action == "prepare":
        if any(value is None for value in (args.scope, args.baseline, args.parent, args.version, args.branch)):
            parser.error("prepare requires --scope, --baseline, --parent, --version and --branch")
        prepare(args)
    elif args.action == "seal":
        seal(args)
    else:
        document = read_json(args.output / "source-candidate.json")
        token = credential()
        if args.action == "dispatch":
            dispatch(document, args.output, token)
        elif args.action == "download":
            from windows_trial_verify import download_trial
            download_trial(document, args.output, token)
        else:
            inspect_build(document, args.output, token)


if __name__ == "__main__":
    main()
