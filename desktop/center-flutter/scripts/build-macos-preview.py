#!/usr/bin/env python3
"""Build a local Apple Silicon trial with Flutter assets and Apple CLT."""
import argparse
import base64
import hashlib
import json
import os
import plistlib
import re
import shutil
import subprocess
from pathlib import Path

from app_build_metadata import read_app_version


ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "build" / "macos-preview"
FRAMEWORKS = BUILD / "frameworks"
DIST = ROOT / "dist"
APP_NAMES = {"host": "Massar Center Mac Code Host Base.app",
             "client": "Massar Center Mac Code Client Base.app"}
BUNDLE_IDENTIFIERS = {"host": "com.massar.lantrial.primary.fresh20261002",
                      "client": "com.massar.lantrial.secondary.fresh20261002"}
RUNTIME_METADATA = {"identity.json", "devices.json", "lan-settings.json",
                    "lan-pending-command.json", "last-opened-build.json",
                    "center.sqlite.lock", "data-repair-status.json"}


def run(*args):
    subprocess.run([str(arg) for arg in args], cwd=ROOT, check=True)


def reject_runtime_data(app):
    for file in app.rglob("*"):
        if not file.is_file():
            continue
        if any(file.name == name or file.name.startswith(name + ".") for name in RUNTIME_METADATA):
            raise RuntimeError("Base app contains private runtime metadata.")
        with file.open("rb") as source:
            if re.search(r"\.(sqlite3?|db)(-wal|-shm|-journal)?$", file.name, re.I) or source.read(16) == b"SQLite format 3\x00":
                raise RuntimeError("Base app contains a database.")
            if file.suffix.lower() == ".json":
                source.seek(0)
                try:
                    document = json.load(source)
                except (ValueError, UnicodeError):
                    continue
                if isinstance(document, dict) and document.get("format") == "massar-center-backup":
                    raise RuntimeError("Base app contains a private runtime backup.")


def framework_info(name, destination):
    with (destination / "Info.plist").open("wb") as output:
        plistlib.dump({
            "CFBundleExecutable": name,
            "CFBundleIdentifier": f"com.massar.preview.{name}",
            "CFBundleName": name,
            "CFBundlePackageType": "FMWK",
            "CFBundleVersion": "1",
            "CFBundleShortVersionString": "1.0.0",
        }, output)


def source_fingerprint():
    fingerprint = hashlib.sha256()
    sources = sorted((ROOT / "lib").rglob("*.dart"))
    sources += [ROOT / "macos" / "PreviewRunner.swift", ROOT / "pubspec.lock", ROOT / "pubspec.yaml"]
    sources += sorted((ROOT / "assets").rglob("*"))
    sources = [source for source in sources if source.is_file()]
    lan_root = ROOT.parent / "center-lan"
    sources += sorted(lan_root.glob("*.go")) + [lan_root / "go.mod"]
    for source in sources:
        fingerprint.update(os.path.relpath(source, ROOT).encode())
        fingerprint.update(b"\0")
        fingerprint.update(source.read_bytes())
    return fingerprint.hexdigest()


def build_identifier(mode="host"):
    return hashlib.sha256((source_fingerprint() + "\0MASSAR_CLIENT_ONLY=" +
                           ("true" if mode == "client" else "false")).encode()).hexdigest()[:16]


def dart_defines(mode, build_id):
    return [f"MASSAR_BUILD_ID={build_id}",
            "MASSAR_CLIENT_ONLY=" + ("true" if mode == "client" else "false"),
            f"MASSAR_APP_VERSION={read_app_version(ROOT)}"]


def aot_identity(app):
    binary = app / "Contents/Frameworks/App.framework/App"
    output = subprocess.check_output(["xcrun", "dwarfdump", "--uuid", str(binary)], text=True)
    matches = re.findall(r"^UUID: ([A-Fa-f0-9-]{36}) \(([^)]+)\)", output, re.M)
    if len(matches) != 1 or matches[0][1] != "arm64":
        raise RuntimeError("The compiled Dart AOT image must have an arm64 UUID.")
    return {"MassarAotUUIDs": {"arm64": matches[0][0].upper()},
            "MassarAotSha256": hashlib.sha256(binary.read_bytes()).hexdigest()}


def strip_client_private_assets(app):
    info = plistlib.loads((app / "Contents/Info.plist").read_bytes())
    if info.get("MassarClientOnly") is not True or "MASSAR_CLIENT_ONLY=true" not in info.get("MassarDartDefines", []):
        raise RuntimeError("Refusing to remove the admin asset from an unverified client build.")
    for asset in (asset for name in ("admin_account.json", "installation_seed.json", "data_repair_20261002.json", "data_repair_20261004.json", "academic_import_20261004.json", "cairo_academic_import_20261004.json", "cairo_codes_20261004.json", "gec_codes_20261004.json", "gec_duplicate_codes_20261004.json") for asset in app.rglob(name)):
        if asset.parts[-3:] != ("flutter_assets", "assets", asset.name) or app.resolve() not in asset.resolve().parents:
            raise RuntimeError("Unexpected private asset location; refusing to remove it.")
        asset.unlink()


def native_source(mode):
    title = "مسار — أساس جهاز الموظف" if mode == "client" else "مسار — أساس الجهاز الرئيسي"
    template = (ROOT / "macos/PreviewRunner.swift").read_text(encoding="utf-8")
    source, count = re.subn(r'^window\.title\s*=\s*".*"$',
        lambda _: "window.title = " + json.dumps(title, ensure_ascii=False), template, flags=re.M)
    if count != 1:
        raise RuntimeError("Expected one native window title in PreviewRunner.swift.")
    generated = BUILD / "base-launchers" / mode / "PreviewRunner.swift"
    generated.parent.mkdir(parents=True, exist_ok=True)
    generated.write_text(source, encoding="utf-8")
    return generated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client-only", action="store_true",
                        help="Compile a dedicated remote-only Dart client; default builds the host.")
    parser.add_argument("--app-name", help="Fresh .app filename inside dist (existing output is refused).")
    parser.add_argument("--no-archive", action="store_true", help="Build the base app without a ZIP.")
    parser.add_argument("--sign-identity", default="-", help="codesign identity name/fingerprint.")
    options = parser.parse_args()
    mode = "client" if options.client_only else "host"
    app_name = options.app_name or APP_NAMES[mode]
    if Path(app_name).name != app_name or not app_name.endswith(".app"):
        raise SystemExit("--app-name must be a single .app filename.")
    app = DIST / app_name
    archive = DIST / f"massar-center-macos-code-{mode}-base-arm64.zip"
    if app.exists() or (not options.no_archive and archive.exists()):
        raise SystemExit("Refusing to replace an existing base app or archive.")
    flutter = shutil.which("flutter")
    if flutter is None:
        raise SystemExit("Flutter is required.")
    build_id = build_identifier(mode)
    app_version = read_app_version(ROOT)
    version_name, version_number = app_version.split("+")
    source_hash = source_fingerprint()
    compile_defines = dart_defines(mode, build_id)
    defines = ",".join(base64.b64encode(define.encode()).decode() for define in compile_defines)
    print(f"Trial build identifier: {build_id}", flush=True)
    run(flutter, "--verbose", "assemble", f"--output={FRAMEWORKS}", f"--dart-define={defines}",
        "-dTargetPlatform=darwin", "-dTargetFile=lib/main.dart",
        "-dBuildMode=release", "-dDarwinArchs=arm64", "-dTreeShakeIcons=false",
        "release_macos_bundle_flutter_assets")
    sdk = subprocess.check_output(
        ["xcrun", "--show-sdk-path"], text=True).strip()
    plugins = json.loads((ROOT / ".flutter-plugins-dependencies").read_text())
    paths = {entry["name"]: Path(entry["path"])
             for entry in plugins["plugins"]["macos"]}
    flags = ["-swift-version", "5", "-target", "arm64-apple-macosx11.0",
             "-sdk", sdk, "-F", str(FRAMEWORKS), "-framework", "FlutterMacOS"]
    selector = FRAMEWORKS / "file_selector_macos.framework"
    selector.mkdir(parents=True, exist_ok=True)
    selector_sources = sorted((paths["file_selector_macos"] / "macos" /
        "file_selector_macos" / "Sources" / "file_selector_macos").glob("*.swift"))
    run("xcrun", "swiftc", *flags, "-emit-library", "-emit-module",
        "-module-name", "file_selector_macos", "-emit-module-path",
        BUILD / "file_selector_macos.swiftmodule", *selector_sources,
        "-Xlinker", "-install_name", "-Xlinker",
        "@rpath/file_selector_macos.framework/file_selector_macos",
        "-o", selector / "file_selector_macos")
    framework_info("file_selector_macos", selector)
    printing = FRAMEWORKS / "printing.framework"
    printing.mkdir(parents=True, exist_ok=True)
    headers = printing / "Headers"
    headers.mkdir(exist_ok=True)
    printing_sources = sorted((paths["printing"] / "macos" / "Classes").glob("*.swift"))
    # Generate the Objective-C header before compiling the plugin's C-FFI bridge.
    run("xcrun", "swiftc", *flags, "-emit-module", "-module-name", "printing",
        "-emit-module-path", BUILD / "printing.swiftmodule",
        "-emit-objc-header-path", headers / "printing-Swift.h", *printing_sources)
    bridge = BUILD / "PrintingPlugin.o"
    run("xcrun", "clang", "-c", "-fobjc-arc", "-target", "arm64-apple-macosx11.0",
        "-isysroot", sdk, "-F", FRAMEWORKS,
        "-include", "Cocoa/Cocoa.h", "-include", "FlutterMacOS/FlutterMacOS.h",
        paths["printing"] / "macos" / "Classes" / "PrintingPlugin.m", "-o", bridge)
    run("xcrun", "swiftc", *flags, "-emit-library", "-module-name", "printing",
        *printing_sources, bridge, "-Xlinker", "-install_name", "-Xlinker",
        "@rpath/printing.framework/printing", "-o", printing / "printing")
    framework_info("printing", printing)
    contents = app / "Contents"
    (contents / "MacOS").mkdir(parents=True)
    embedded = contents / "Frameworks"
    embedded.mkdir()
    for source in FRAMEWORKS.glob("*.framework"):
        shutil.copytree(source, embedded / source.name, symlinks=True)
    native_frameworks = list((ROOT / "build" / "native_assets" / "macos").glob("*.framework"))
    if not native_frameworks:
        raise SystemExit("Native SQLite/Objective-C assets are missing; refusing to package.")
    for source in native_frameworks:
        shutil.copytree(source, embedded / source.name, symlinks=True)
    resources = contents / "Resources"
    resources.mkdir(exist_ok=True)
    shutil.copy2(ROOT / "assets" / "AppIcon.icns", resources / "AppIcon.icns")
    executable = contents / "MacOS" / "MassarCenter"
    subprocess.run(["go", "build", "-trimpath", "-ldflags=-s -w", "-o",
        str(contents / "MacOS" / "massar-lan-host"), "."],
        cwd=ROOT.parent / "center-lan", check=True,
        env={**os.environ, "GOOS": "darwin", "GOARCH": "arm64"})
    run("xcrun", "swiftc", *flags, "-I", BUILD,
        "-framework", "file_selector_macos", "-framework", "printing",
        native_source(mode), "-module-name", f"MassarCodeBase{mode.title()}",
        "-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks",
        "-o", executable)
    if source_fingerprint() != source_hash:
        raise SystemExit("Source changed during compilation; refusing to label a mixed build.")
    with (contents / "Info.plist").open("wb") as output:
        plistlib.dump({
            "CFBundleExecutable": "MassarCenter",
            "CFBundleIconFile": "AppIcon.icns",
            "CFBundleIdentifier": BUNDLE_IDENTIFIERS[mode],
            "MassarClientOnly": options.client_only,
            "MassarBuildIdentifier": build_id,
            "MassarAppVersion": app_version,
            "MassarSourceFingerprint": source_hash,
            "MassarDartDefines": compile_defines,
            "CFBundleName": "مسار | نادر جورج — تجربة ماك",
            "CFBundleDisplayName": "مسار | نادر جورج",
            "CFBundlePackageType": "APPL",
            "CFBundleVersion": version_number,
            "CFBundleShortVersionString": version_name.split("-")[0],
            "LSMinimumSystemVersion": "11.0",
            "NSPrincipalClass": "NSApplication",
            "NSHighResolutionCapable": True,
            "NSLocalNetworkUsageDescription": "ربط أجهزة السنتر عبر نفس الراوتر بدون إنترنت.",
        }, output)
    if options.client_only:
        strip_client_private_assets(app)
    reject_runtime_data(app)
    for framework in embedded.glob("*.framework"):
        run("codesign", "--force", "--sign", options.sign_identity, "--timestamp=none", framework)
    info_path = contents / "Info.plist"
    info = plistlib.loads(info_path.read_bytes())
    info.update(aot_identity(app))
    info_path.write_bytes(plistlib.dumps(info))
    run("codesign", "--force", "--sign", options.sign_identity, "--timestamp=none", contents / "MacOS" / "massar-lan-host")
    run("codesign", "--force", "--sign", options.sign_identity, "--timestamp=none", app)
    run("codesign", "--verify", "--deep", "--strict", app)
    if not options.no_archive:
        run("ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", app, archive)
    print(f"Built {mode} base: {app}")


if __name__ == "__main__":
    main()
