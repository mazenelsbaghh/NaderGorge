"""Deliver tested native ZIPs to a private, exact-run GitHub release.

This transport does not use Actions artifact storage. It neither replaces an
existing conflicting asset nor publishes to a public repository.
"""
import argparse
from dataclasses import dataclass
import hashlib
import io
import json
import os
from pathlib import Path
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile

REPOSITORY = "mazenelsbaghh/massar-center-desktop"
DELIVERY_MARKER = "github-release-assets-v1"
API_ROOT = "https://api.github.com/repos/" + REPOSITORY
UPLOAD_ROOT = "https://uploads.github.com/repos/" + REPOSITORY
MAX_ARCHIVE_BYTES = 512 * 1024 * 1024


class DeliveryError(RuntimeError):
    pass


class GitHubError(DeliveryError):
    def __init__(self, status):
        self.status = status
        super().__init__("Private delivery API rejected the request (HTTP " + str(status) + ").")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, url):
        return None


@dataclass(frozen=True)
class BuildIdentity:
    run_id: int
    run_attempt: int
    role: str
    source_commit: str

    @property
    def tag(self):
        return f"windows-native-{self.run_id}-{self.run_attempt}-{self.role}"


@dataclass(frozen=True)
class AssetPayload:
    name: str
    content: bytes

    @property
    def binding(self):
        return {"name": self.name, "size": len(self.content),
                "sha256": hashlib.sha256(self.content).hexdigest()}


class GitHubDelivery:
    def __init__(self, token):
        if not token or any(character.isspace() for character in token):
            raise DeliveryError("A valid workflow token is required.")
        self._token = token
        self._opener = urllib.request.build_opener(NoRedirect)

    def _response(self, request):
        try:
            with self._opener.open(request, timeout=180) as response:
                content = response.read(2 * 1024 * 1024 + 1)
        except urllib.error.HTTPError as error:
            error.close()
            raise GitHubError(error.code) from None
        if len(content) > 2 * 1024 * 1024:
            raise DeliveryError("Private delivery metadata exceeded the size limit.")
        return json.loads(content)

    def _headers(self):
        return {"Authorization": "Bearer " + self._token,
                "Accept": "application/vnd.github+json", "User-Agent": "Massar-Windows-Private-Delivery",
                "X-GitHub-Api-Version": "2022-11-28"}

    def json(self, method, path, document=None):
        headers = dict(self._headers(), **{"Content-Type": "application/json"})
        body = None if document is None else json.dumps(document).encode()
        return self._response(urllib.request.Request(API_ROOT + path, data=body, headers=headers, method=method))

    def release(self, tag):
        try:
            return self.json("GET", "/releases/tags/" + urllib.parse.quote(tag, safe=""))
        except GitHubError as error:
            if error.status == 404:
                return None
            raise

    def upload(self, release_id, payload):
        query = urllib.parse.urlencode({"name": payload.name})
        headers = dict(self._headers(), **{"Content-Type": "application/octet-stream"})
        url = UPLOAD_ROOT + f"/releases/{release_id}/assets?" + query
        return self._response(urllib.request.Request(url, data=payload.content, headers=headers, method="POST"))


def identity_from_environment(environment):
    if environment.get("GITHUB_REPOSITORY") != REPOSITORY:
        raise DeliveryError("Native delivery is restricted to the reviewed private repository.")
    if environment.get("MASSAR_WINDOWS_DELIVERY") != DELIVERY_MARKER:
        raise DeliveryError("Native delivery transport marker is missing.")
    for name in ("GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT"):
        if not re.fullmatch(r"[1-9][0-9]{0,19}", environment.get(name, "")):
            raise DeliveryError("Native delivery requires an exact run and attempt.")
    role = environment.get("PACKAGE_ROLE")
    commit = environment.get("GITHUB_SHA", "")
    if role not in ("host", "client") or not re.fullmatch(r"[a-f0-9]{40}", commit):
        raise DeliveryError("Native delivery requires an exact role and source SHA.")
    return BuildIdentity(int(environment["GITHUB_RUN_ID"]), int(environment["GITHUB_RUN_ATTEMPT"]), role, commit)


def local_assets(directory, identity):
    archives = sorted(directory.glob("*.zip"))
    sidecars = sorted(directory.glob("*.sha256"))
    if len(archives) != 1 or len(sidecars) != 1:
        raise DeliveryError("Expected exactly one native ZIP and one checksum sidecar.")
    for path, limit in ((archives[0], MAX_ARCHIVE_BYTES), (sidecars[0], 4096)):
        if (path.is_symlink() or not path.is_file() or path.stat().st_size <= 0
                or path.stat().st_size > limit or not re.fullmatch(r"[A-Za-z0-9_.-]+", path.name)):
            raise DeliveryError("Native delivery requires bounded regular output files.")
    if sidecars[0].name != archives[0].name + ".sha256":
        raise DeliveryError("Native checksum sidecar does not belong to the ZIP.")
    filename = re.fullmatch(
        rf"massar-center-windows-{identity.role}-run-{identity.run_id}-{identity.run_attempt}-([a-f0-9]{{16}})\.zip",
        archives[0].name,
    )
    if filename is None:
        raise DeliveryError("Native ZIP filename belongs to a different run, attempt, or role.")
    payloads = [AssetPayload(path.name, path.read_bytes()) for path in (archives[0], sidecars[0])]
    if any(len(payload.content) > limit for payload, limit in zip(payloads, (MAX_ARCHIVE_BYTES, 4096))):
        raise DeliveryError("Native output changed beyond the size limit.")
    checksum = payloads[1].content.decode("utf-8-sig").split()
    if (len(checksum) not in (1, 2) or checksum[0].lower() != payloads[0].binding["sha256"]
            or len(checksum) == 2 and checksum[1].lstrip("*") != payloads[0].name):
        raise DeliveryError("Native checksum does not match the delivered ZIP.")
    with zipfile.ZipFile(io.BytesIO(payloads[0].content)) as archive:
        manifests = [entry for entry in archive.infolist()
                     if entry.filename == "build-manifest.json" or entry.filename.endswith("/build-manifest.json")]
        if len(manifests) != 1 or manifests[0].file_size > 1024 * 1024:
            raise DeliveryError("Expected one bounded native build manifest.")
        manifest = json.loads(archive.read(manifests[0]).decode("utf-8-sig"))
    if (manifest.get("sourceCommit") != identity.source_commit or manifest.get("role") != identity.role
            or manifest.get("buildId") != filename.group(1)):
        raise DeliveryError("Native output belongs to a different source or role.")
    return payloads


def require_private_repository(api):
    repository = api.json("GET", "")
    if repository.get("private") is not True or repository.get("full_name") != REPOSITORY:
        raise DeliveryError("Native outputs must remain in the reviewed private repository.")


def delivery_binding(identity, payloads):
    return {"schemaVersion": 1, "transport": "github-release-assets", "repository": REPOSITORY,
            "runId": identity.run_id, "runAttempt": identity.run_attempt, "role": identity.role,
            "sourceCommit": identity.source_commit, "assets": [payload.binding for payload in payloads]}


def require_release(release, identity, binding):
    if (type(release.get("id")) is not int or release["id"] <= 0
            or release.get("tag_name") != identity.tag or release.get("target_commitish") != identity.source_commit
            or release.get("draft") is not False or release.get("prerelease") is not True
            or json.loads(release.get("body", "")) != binding):
        raise DeliveryError("Existing native release conflicts with the exact build binding.")


def require_tag(api, identity):
    reference = api.json("GET", "/git/ref/tags/" + identity.tag)
    if (reference.get("ref") != "refs/tags/" + identity.tag
            or reference.get("object", {}).get("type") != "commit"
            or reference["object"].get("sha") != identity.source_commit):
        raise DeliveryError("Native release tag does not point to the exact source commit.")


def require_asset(asset, binding):
    if (type(asset.get("id")) is not int or asset["id"] <= 0 or asset.get("state") != "uploaded"
            or asset.get("name") != binding["name"] or asset.get("size") != binding["size"]
            or asset.get("digest") != "sha256:" + binding["sha256"]):
        raise DeliveryError("Native release asset conflicts with verified local bytes.")


def deliver(api, identity, payloads):
    require_private_repository(api)
    binding = delivery_binding(identity, payloads)
    release = api.release(identity.tag)
    if release is None:
        release = api.json("POST", "/releases", {
            "tag_name": identity.tag, "target_commitish": identity.source_commit,
            "name": identity.tag, "body": json.dumps(binding, separators=(",", ":")),
            "draft": False, "prerelease": True, "make_latest": "false"})
    require_release(release, identity, binding)
    require_tag(api, identity)
    existing = release.get("assets", [])
    expected_names = {payload.name for payload in payloads}
    if len(existing) != len({asset["name"] for asset in existing}) or any(asset["name"] not in expected_names for asset in existing):
        raise DeliveryError("Unexpected assets exist on the exact native release.")
    for asset in existing:
        require_asset(asset, next(payload.binding for payload in payloads if payload.name == asset["name"]))
    for payload in payloads:
        matching = [asset for asset in existing if asset["name"] == payload.name]
        if matching:
            require_asset(matching[0], payload.binding)
        else:
            require_private_repository(api)
            require_tag(api, identity)
            require_asset(api.upload(release["id"], payload), payload.binding)
    require_private_repository(api)
    require_tag(api, identity)
    final = api.release(identity.tag)
    require_release(final, identity, binding)
    assets = final.get("assets", [])
    if len(assets) != 2 or {asset["name"] for asset in assets} != expected_names:
        raise DeliveryError("Native release does not contain exactly the ZIP and checksum.")
    for payload in payloads:
        require_asset(next(asset for asset in assets if asset["name"] == payload.name), payload.binding)
    return {"tag": identity.tag, "releaseId": final["id"], "assets": assets}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dist", required=True, type=Path)
    args = parser.parse_args()
    identity = identity_from_environment(os.environ)
    payloads = local_assets(args.dist, identity)
    delivered = deliver(GitHubDelivery(os.environ.get("GITHUB_TOKEN", "")), identity, payloads)
    print(json.dumps({"delivery": DELIVERY_MARKER, "role": identity.role, "tag": delivered["tag"],
                      "releaseId": delivered["releaseId"], "assetIds": [asset["id"] for asset in delivered["assets"]]}))


if __name__ == "__main__":
    try:
        main()
    except DeliveryError as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
    except (OSError, ValueError, zipfile.BadZipFile):
        print("Private native delivery failed; no conflicting asset was overwritten. Inspect the exact build run.", file=sys.stderr)
        raise SystemExit(1)
