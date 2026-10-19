"""Native release delivery boundaries; synthetic files and fake GitHub only."""
import copy
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import windows_private_delivery as delivery


class GitHubFixture:
    """The external GitHub boundary, retaining its observable release state."""
    def __init__(self, identity):
        self.identity = identity
        self.private = True
        self.current = None
        self.tag_sha = identity.source_commit
        self.uploads = []
        self.list_visible = True

    def json(self, method, path, document=None):
        if method == "GET" and path == "":
            return {"private": self.private, "full_name": delivery.REPOSITORY}
        if method == "GET" and path == "/git/ref/tags/" + self.identity.tag:
            if self.tag_sha is None:
                raise delivery.GitHubError(404)
            return {"ref": "refs/tags/" + self.identity.tag, "object": {"type": "commit", "sha": self.tag_sha}}
        if method == "POST" and path == "/git/refs":
            self.tag_sha = document["sha"]
            return {"ref": document["ref"]}
        if method == "POST" and path == "/releases":
            self.current = dict(document, id=91, assets=[])
            return copy.deepcopy(self.current)
        if method == "GET" and self.current and path == "/releases/" + str(self.current["id"]):
            return copy.deepcopy(self.current)
        raise AssertionError("Unexpected external operation: " + method + " " + path)

    def release(self, tag):
        if tag != self.identity.tag:
            raise AssertionError("Different attempt or role requested")
        return copy.deepcopy(self.current) if self.list_visible else None

    def upload(self, release_id, payload):
        if release_id != self.current["id"]:
            raise AssertionError("Different release requested")
        asset = dict(payload.binding, id=100 + len(self.uploads), state="uploaded", digest="sha256:" + payload.binding["sha256"])
        self.current["assets"].append(asset)
        self.uploads.append(payload.content)
        return copy.deepcopy(asset)

    def require_hidden(self, path):
        if self.current["draft"] is not True:
            raise delivery.DeliveryError("Draft was published.")


class NativeDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.identity = delivery.BuildIdentity(390001, 2, "client", "a" * 40)

    def outputs(self, *, role="client", commit="a" * 40):
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, "w") as archive:
            archive.writestr("build-manifest.json", json.dumps({"role": role, "sourceCommit": commit, "buildId": "b" * 16}))
            archive.writestr("native.exe", b"MZsynthetic")
        path = self.root / ("massar-center-windows-client-run-390001-2-" + "b" * 16 + ".zip")
        path.write_bytes(buffer.getvalue())
        path.with_suffix(".zip.sha256").write_text(hashlib.sha256(buffer.getvalue()).hexdigest() + "\n")
        return delivery.local_assets(self.root, self.identity)

    def test_private_exact_attempt_release_delivers_the_original_zip_and_is_idempotent(self):
        payloads = self.outputs()
        api = GitHubFixture(self.identity)
        result = delivery.deliver(api, self.identity, payloads)
        self.assertEqual(result["tag"], "windows-native-390001-2-client")
        self.assertEqual(api.uploads, [payload.content for payload in payloads])
        self.assertEqual(json.loads(api.current["body"]), delivery.delivery_binding(self.identity, payloads))
        self.assertEqual(api.current["target_commitish"], self.identity.source_commit)
        delivery.deliver(api, self.identity, payloads)
        self.assertEqual(len(api.uploads), 2)

    def test_public_repository_delivers_only_an_unpublished_exact_run_draft(self):
        api = GitHubFixture(self.identity);api.private = False
        payloads = self.outputs()
        delivery.deliver(api, self.identity, payloads)
        self.assertTrue(api.current["draft"])
        self.assertEqual(api.uploads, [payload.content for payload in payloads])

    def test_missing_tag_is_bound_to_the_source_before_draft_delivery(self):
        api = GitHubFixture(self.identity);api.tag_sha = None
        delivery.deliver(api, self.identity, self.outputs())
        self.assertEqual(api.tag_sha, self.identity.source_commit)
        self.assertTrue(api.current["draft"])

    def test_new_draft_delivery_survives_release_list_index_delay(self):
        # Run 39183808133: the host draft existed by ID before listing saw it.
        api = GitHubFixture(self.identity);api.list_visible = False
        payloads = self.outputs()
        delivered = delivery.deliver(api, self.identity, payloads)
        self.assertEqual(delivered["releaseId"], api.current["id"])
        self.assertEqual(api.uploads, [payload.content for payload in payloads])

    def test_draft_lookup_paginates_and_rejects_ambiguous_tags(self):
        pages = [[{"tag_name": "other-" + str(n)} for n in range(100)], [{"id": 7, "tag_name": self.identity.tag}]]
        def read_page(path):
            return pages[int(path.rsplit("=", 1)[1]) - 1]
        self.assertEqual(delivery.draft_release(read_page, self.identity.tag)["id"], 7)
        pages[1].append(dict(pages[1][0], id=8))
        with self.assertRaises(delivery.DeliveryError):
            delivery.draft_release(read_page, self.identity.tag)

    def test_conflicting_release_or_assets_are_never_overwritten(self):
        payloads = self.outputs()
        for mutation in ("published", "body", "source", "tag", "asset-digest", "asset-size", "extra", "duplicate"):
            with self.subTest(mutation=mutation):
                api = GitHubFixture(self.identity)
                delivery.deliver(api, self.identity, payloads)
                if mutation == "published": api.current["draft"] = False
                elif mutation == "body": api.current["body"] = "{}"
                elif mutation == "source": api.current["target_commitish"] = "b" * 40
                elif mutation == "tag": api.tag_sha = "b" * 40
                elif mutation == "asset-digest": api.current["assets"][1]["digest"] = "sha256:" + "f" * 64
                elif mutation == "asset-size": api.current["assets"][0]["size"] += 1
                elif mutation == "extra": api.current["assets"].append(dict(api.current["assets"][0], name="private.db"))
                else: api.current["assets"].append(api.current["assets"][0])
                before = copy.deepcopy(api.current)
                with self.assertRaises(delivery.DeliveryError): delivery.deliver(api, self.identity, payloads)
                self.assertEqual(api.current, before)
                self.assertEqual(len(api.uploads), 2)

    def test_incomplete_release_with_wrong_existing_sidecar_does_not_upload_zip(self):
        payloads = self.outputs();api = GitHubFixture(self.identity)
        delivery.deliver(api, self.identity, payloads)
        api.current["assets"] = [dict(api.current["assets"][1], digest="sha256:" + "0" * 64)]
        api.uploads.clear()
        with self.assertRaises(delivery.DeliveryError): delivery.deliver(api, self.identity, payloads)
        self.assertEqual(api.uploads, [])

    def test_wrong_local_identity_checksum_and_ambiguous_files_are_rejected(self):
        for mutation in ("role", "source", "checksum", "extra-zip", "symlink", "oversize", "attempt-filename", "build-id"):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temporary:
                previous = self.root;self.root = Path(temporary)
                try:
                    if mutation in ("role", "source"):
                        with self.assertRaises(delivery.DeliveryError):
                            self.outputs(role="host" if mutation == "role" else "client", commit="b" * 40 if mutation == "source" else "a" * 40)
                        continue
                    self.outputs()
                    archive = self.root / ("massar-center-windows-client-run-390001-2-" + "b" * 16 + ".zip")
                    if mutation == "checksum": archive.with_suffix(".zip.sha256").write_text("f" * 64)
                    elif mutation == "extra-zip": (self.root / "other.zip").write_bytes(b"other")
                    elif mutation == "attempt-filename":
                        wrong = archive.with_name(archive.name.replace("390001-2-", "390001-1-"))
                        archive.rename(wrong)
                        archive.with_suffix(".zip.sha256").rename(wrong.with_suffix(".zip.sha256"))
                    elif mutation == "build-id":
                        wrong = archive.with_name(archive.name.replace("b" * 16, "c" * 16))
                        archive.rename(wrong)
                        archive.with_suffix(".zip.sha256").rename(wrong.with_suffix(".zip.sha256"))
                    elif mutation == "symlink":
                        archive.rename(self.root / "original.bin");archive.symlink_to(self.root / "original.bin")
                    else:
                        with archive.open("r+b") as stream: stream.truncate(delivery.MAX_ARCHIVE_BYTES + 1)
                    with self.assertRaises(delivery.DeliveryError): delivery.local_assets(self.root, self.identity)
                finally:
                    self.root = previous

    def test_environment_cannot_target_another_repository_attempt_or_role(self):
        environment = {"GITHUB_REPOSITORY": delivery.REPOSITORY, "MASSAR_WINDOWS_DELIVERY": delivery.DELIVERY_MARKER,
                       "GITHUB_RUN_ID": "390001", "GITHUB_RUN_ATTEMPT": "2", "PACKAGE_ROLE": "client", "GITHUB_SHA": "a" * 40}
        for key, wrong in (("GITHUB_REPOSITORY", "someone/public"), ("GITHUB_RUN_ID", "12/unsafe"),
                           ("GITHUB_RUN_ATTEMPT", "0"), ("PACKAGE_ROLE", "other"), ("GITHUB_SHA", "main"),
                           ("MASSAR_WINDOWS_DELIVERY", "")):
            with self.subTest(key=key), self.assertRaises(delivery.DeliveryError):
                delivery.identity_from_environment(dict(environment, **{key: wrong}))

    def test_redirects_are_not_followed_and_draft_visibility_checks_are_anonymous(self):
        received = []
        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                received.append((self.path, self.headers.get("Authorization")))
                self.send_response(404 if self.path == "/hidden" else 200 if self.path == "/public" else 302)
                if self.path not in ("/hidden", "/public"):
                    self.send_header("Location", self.server.redirect_target)
                self.end_headers()
            def log_message(self, *_): pass
        server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        server.redirect_target = "http://127.0.0.1:" + str(server.server_port) + "/leaked"
        worker = threading.Thread(target=server.serve_forever, daemon=True);worker.start()
        try:
            with patch.object(delivery, "API_ROOT", "http://127.0.0.1:" + str(server.server_port)):
                with self.assertRaises(delivery.GitHubError) as caught:
                    delivery.GitHubDelivery("synthetic-private-token").json("GET", "/metadata")
                self.assertEqual(caught.exception.status, 302)
                self.assertNotIn("synthetic-private-token", str(caught.exception))
                client = delivery.GitHubDelivery("synthetic-private-token")
                client.require_hidden("/hidden")
                for path in ("/public", "/redirect"):
                    with self.subTest(path=path), self.assertRaises(delivery.DeliveryError):
                        client.require_hidden(path)
                self.assertEqual(received, [("/metadata", "Bearer synthetic-private-token"), ("/hidden", None), ("/public", None), ("/redirect", None)])
        finally:
            server.shutdown();server.server_close();worker.join(timeout=2)


if __name__ == "__main__":
    unittest.main()
