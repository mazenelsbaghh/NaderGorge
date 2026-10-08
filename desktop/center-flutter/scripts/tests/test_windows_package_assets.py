"""Packaging regressions use small synthetic files, never installation records."""
from pathlib import Path
from contextlib import contextmanager
import struct
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import windows_package_assets as package
from app_build_metadata import read_app_version


PUBSPEC = """name: package_fixture
version: 1.2.3+4
# تعليق محفوظ
flutter:
  uses-material-design: true
  assets:
    - assets/logo.svg
    - assets/logo-light.svg
    - assets/admin_account.json
    - assets/installation_seed.json
    - assets/data_repair_fixture.json
  fonts:
    - family: Tajawal
      fonts:
        - asset: assets/fonts/Tajawal-Regular.ttf
        - asset: assets/fonts/Tajawal-Medium.ttf
          weight: 500
        - asset: assets/fonts/Tajawal-Bold.ttf
          weight: 700
""".replace("\n", "\r\n").encode("utf-8")


def encoded_manifest(paths):
    # Flutter StandardMessageCodec: Map<String, List<Map<String, String>>>.
    # Include a dpr double to cover the codec's absolute-position alignment.
    output = bytearray([13, len(paths)])

    def string(value):
        encoded = value.encode("utf-8")
        output.append(7)
        if len(encoded) < 254:
            output.append(len(encoded))
        else:
            output.extend(b"\xfe" + struct.pack("<H", len(encoded)))
        output.extend(encoded)

    for path in sorted(paths):
        string(path)
        output.extend([12, 1, 13, 2])
        string("asset")
        string(path)
        string("dpr")
        output.append(6)
        output.extend(b"\x00" * (-len(output) % 8))
        output.extend(struct.pack("<d", 1.0))
    return bytes(output)


@contextmanager
def active_build_lock(path):
    if sys.platform == "win32":
        import ctypes
        from ctypes import wintypes
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                     wintypes.LPVOID, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
        kernel.CreateFileW.restype = wintypes.HANDLE
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        handle = kernel.CreateFileW(str(path), 0xC0000000, 0, None, 4, 0x80, None)
        if handle == wintypes.HANDLE(-1).value:
            raise ctypes.WinError(ctypes.get_last_error())
        try:
            yield
        finally:
            kernel.CloseHandle(handle)
    else:
        import fcntl
        with path.open("a+b") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            yield


class WindowsPackageAssetsTest(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="massar-package-test-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.pubspec = self.root / "pubspec.yaml"
        self.pubspec.write_bytes(PUBSPEC)

    def test_client_without_private_inputs_restores_exact_host_manifest(self):
        # Regression: ClientOnly used to unconditionally open the host seed.
        for name in package.CLIENT_ASSETS:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"public fixture")
        package.begin(self.root, "client-build")
        staged = self.pubspec.read_text()
        self.assertNotIn("installation_seed", staged)
        self.assertNotIn("admin_account", staged)
        self.assertNotIn("data_repair", staged)
        for name in package.CLIENT_ASSETS:
            self.assertIn(name, staged)
        with self.assertRaises(RuntimeError):
            read_app_version(self.root)
        with self.assertRaises(RuntimeError):
            package.begin(self.root, "another-build")
        package.restore(self.root, "client-build")
        self.assertEqual(PUBSPEC, self.pubspec.read_bytes())
        self.assertEqual("1.2.3+4", read_app_version(self.root))
        self.assertFalse((self.root / package.JOURNAL_NAME).exists())

    def test_recovery_accepts_both_crash_points_and_is_idempotent(self):
        for already_restored in (False, True):
            with self.subTest(already_restored=already_restored):
                package.begin(self.root, "interrupted")
                if already_restored:
                    # Crash after restoration, before removing the journal.
                    self.pubspec.write_bytes(PUBSPEC)
                command = [sys.executable, str(Path(package.__file__)), "recover", "--root", str(self.root)]
                subprocess.run(command, check=True, capture_output=True)
                subprocess.run(command, check=True, capture_output=True)
                self.assertEqual(PUBSPEC, self.pubspec.read_bytes())
                self.assertFalse((self.root / package.JOURNAL_NAME).exists())

    def test_changed_manifest_or_wrong_owner_preserves_original_for_review(self):
        package.begin(self.root, "owner")
        journal = (self.root / package.JOURNAL_NAME).read_bytes()
        with self.assertRaises(ValueError):
            package.restore(self.root, "other-owner")
        edited = self.pubspec.read_bytes() + b"# user's concurrent edit\n"
        self.pubspec.write_bytes(edited)
        with self.assertRaises(RuntimeError):
            package.restore(self.root, "owner")
        self.assertEqual(edited, self.pubspec.read_bytes())
        self.assertEqual(journal, (self.root / package.JOURNAL_NAME).read_bytes())

    def test_unfamiliar_yaml_is_rejected_before_changing_any_file(self):
        variants = [
            PUBSPEC.replace(b"    - assets/logo.svg", b"    - assets/"),
            PUBSPEC.replace(b"    - assets/admin_account.json", b"    - path: assets/admin_account.json"),
            PUBSPEC.replace(b"assets/fonts/Tajawal-Bold.ttf", b"assets/private.ttf"),
            PUBSPEC.replace(b"    - assets/admin_account.json", b"    - assets/logo.svg"),
        ]
        for source in variants:
            with self.subTest(source=source):
                self.pubspec.write_bytes(source)
                with self.assertRaises(ValueError):
                    package.begin(self.root, "invalid")
                self.assertEqual(source, self.pubspec.read_bytes())
                self.assertFalse((self.root / package.JOURNAL_NAME).exists())

    def test_recovery_refuses_a_running_build_lock(self):
        package.begin(self.root, "owner")
        lock_path = self.root / package.LOCK_NAME
        with active_build_lock(lock_path):
            result = subprocess.run(
                [sys.executable, str(Path(package.__file__)), "recover", "--root", str(self.root)],
                capture_output=True)
            self.assertNotEqual(0, result.returncode)
            self.assertTrue(self.pubspec.read_bytes().startswith(package.MARKER))
        package.recover(self.root)
        self.assertEqual(PUBSPEC, self.pubspec.read_bytes())

    def test_bundle_files_and_manifest_must_both_contain_only_public_assets(self):
        assets = self.root / "data/flutter_assets"
        for name in package.CLIENT_ASSETS:
            path = assets / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"public fixture")
        manifest = assets / "AssetManifest.bin"
        manifest.write_bytes(encoded_manifest(package.CLIENT_ASSETS))
        package.verify_client_bundle(self.root)
        private = assets / "assets/installation_seed.json"
        private.write_bytes(b"{}")
        with self.assertRaises(RuntimeError):
            package.verify_client_bundle(self.root)
        private.unlink()
        manifest.write_bytes(encoded_manifest(package.CLIENT_ASSETS | {"assets/installation_seed.json"}))
        with self.assertRaises(RuntimeError):
            package.verify_client_bundle(self.root)
        manifest.write_bytes(encoded_manifest(package.CLIENT_ASSETS))
        (assets / "assets/logo.svg").unlink()
        with self.assertRaises(RuntimeError):
            package.verify_client_bundle(self.root)

    def test_manifest_codec_rejects_truncation_unknown_types_and_trailing_bytes(self):
        valid = encoded_manifest({"assets/" + "أ" * 150 + ".svg"})
        self.assertIn("assets/" + "أ" * 150 + ".svg", package.manifest_strings(valid))
        for invalid in (b"", valid[:-1], valid + b"x", b"\x0d\x01\x01", b"\x0d\xff\xff\xff\xff\xff"):
            with self.subTest(invalid=invalid):
                with self.assertRaises(ValueError):
                    package.manifest_strings(invalid)


if __name__ == "__main__":
    unittest.main()
