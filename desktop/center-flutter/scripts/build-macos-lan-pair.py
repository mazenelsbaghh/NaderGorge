#!/usr/bin/env python3
"""Compile distinct native launchers and package isolated Apple Silicon trials."""
import argparse
import hashlib
import json
import plistlib
import re
import shutil
import subprocess
import zipfile
from pathlib import Path

from app_build_metadata import read_app_version

ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / "dist"
BUILD = ROOT / "build" / "macos-preview"
PREPARED = BUILD / "lan-code-pair"
EDITION = "fresh20261002"
BUNDLE_IDENTIFIERS = {
    "primary": "com.massar.lantrial.primary.fresh20261002",
    "secondary": "com.massar.lantrial.secondary.fresh20261002",
}
LAN_SETTINGS_FILES = {"identity.json", "devices.json", "lan-settings.json",
                      "lan-pending-command.json", "last-opened-build.json",
                      "center.sqlite.lock", "data-repair-status.json"}
SQLITE_SUFFIXES = (".sqlite", ".sqlite-wal", ".sqlite-shm", ".sqlite-journal",
                   ".sqlite3", ".sqlite3-wal", ".sqlite3-shm",
                   ".db", ".db-wal", ".db-shm", ".db-journal")
ROLES = {
    "primary": ("Massar Mac 1 Primary Trial", "مسار — ماك ١ الرئيسي"),
    "secondary": ("Massar Mac 2 Connected Trial", "مسار — ماك ٢ المتصل"),
}
GUIDE = """مسار على جهازين ماك — النسخة الرئيسية ونسخة الموظف بالرمز
Apple Silicon (سلسلة M)

الجهاز الرئيسي يحتفظ ببيانات السنتر. نسخة الموظف لا تنشئ أو تفتح قاعدة
طلبة محلية؛ تحفظ إعدادات الربط فقط وتستخدم بيانات الرئيسي عبر نفس الراوتر.
لا تنقل قواعد البيانات بين الجهازين.

الجهاز الأول — Massar Mac 1 Primary Trial:
١. افتح نسخة الرئيسي وسجل دخول المدير.
٢. افتح «ربط الأجهزة»، اختر «الجهاز الرئيسي»، ثم ابدأ السيرفر.
٣. اترك التطبيق مفتوحًا؛ سيظهر اسم الجهاز وبصمته وكود الربط.
٤. استخدم بيانات التجربة الموجودة أو جهّز مجموعة وحصة وطالبًا للتجربة.

الجهاز الثاني — Massar Mac 2 Connected Trial:
١. وصّل الجهازين بنفس الراوتر، ثم افتح نسخة الموظف؛ لا تحتاج إنترنت.
٢. تظهر «جهاز متصل — البيانات على الرئيسي» ويبدأ البحث تلقائيًا.
٣. قارن اسم الجهاز وبصمته الظاهرين مع الجهاز الأول. إذا ظهر أكثر من
   جهاز، اختر الرئيسي الصحيح قبل الاتصال.
٤. اكتب الكود في «كود الربط — ستة أرقام»، ثم اضغط Enter أو
   «اتصال بالجهاز الرئيسي».
٥. بعد الربط سجل دخول الموظف بحساب موجود على الرئيسي.

المرات التالية: يُحفظ الربط وتعود النسخة للرئيسي تلقائيًا دون كود جديد.
قد تحتاج تسجيل دخول الموظف مجددًا بعد إعادة تشغيل الرئيسي. عند انقطاع
الاتصال يتوقف التسجيل والتحصيل، ويستأنف الاتصال حين يعود الرئيسي.

إذا لم يظهر الرئيسي: تأكد أن سيرفره مفتوح. راجع إذن الشبكة المحلية في
إعدادات النظام، ثم افتح «خيارات إضافية إذا لم يظهر الجهاز» للربط اليدوي.
انسخ بيانات الربط من الرئيسي والصقها على الثاني ثم أدخل الكود بعد مراجعة
الهوية. لا تثق بجهاز جديد لمجرد أن اسمَه مشابه.

جرّب حضور طالب من الثاني ومراجعة سجله وتقفيلة الحصة على الأول.
حاول حضور الطالب نفسه على الجهازين: يظهر تنبيه دون دفع جديد.
أغلق الرئيسي وتأكد أن التسجيل يتوقف على الثاني، ثم راجع عودة الاتصال.

تظل إعدادات الربط والبيانات السابقة محفوظة؛ النسخة الجديدة لا تحذفها.
عند التحديث استبدل التطبيق الخاص بنفس الجهاز؛ لا تحذف مجلد بياناته ولا
تُعد الربط. تحديث الرئيسي والمتصل يحتفظ ببيانات السنتر والربط المحفوظ.
بيانات الطلاب والمجموعات المرفقة تُستخدم فقط عند أول تثبيت حقيقي دون
قاعدة بيانات موجودة؛ التحديث لا يعيد زرعها ولا يصفّر الحصص أو المدفوعات.
أغلق نسخة السيرفر السابقة بنفسك قبل تشغيل الجديد. هذه الحزمة لسلسلة M
فقط. نجاح تجهيز الحزمة لا يغني عن اختبار الاتصال الفعلي على جهازين.
"""


def run(*args):
    subprocess.run([str(arg) for arg in args], cwd=ROOT, check=True)


def native_uuids(executable):
    output = subprocess.check_output(
        ["xcrun", "dwarfdump", "--uuid", str(executable)], text=True)
    matches = re.findall(r"^UUID: ([A-Fa-f0-9-]{36}) \(([^)]+)\)", output, re.M)
    uuids = {arch: uuid.upper() for uuid, arch in matches}
    if set(uuids) != {"arm64"} or len(matches) != 1:
        raise RuntimeError("A trial launcher must have one arm64 LC_UUID.")
    return uuids


def main_executable(bundle):
    info = plistlib.loads((bundle / "Contents/Info.plist").read_bytes())
    name = info.get("CFBundleExecutable", "")
    if not name or Path(name).name != name:
        raise RuntimeError("Invalid bundle main executable name.")
    return bundle / "Contents/MacOS" / name


def aot_identity(bundle):
    binary = bundle / "Contents/Frameworks/App.framework/App"
    resolved = binary.resolve()
    if bundle.resolve() not in resolved.parents:
        raise RuntimeError("The Dart AOT image must be contained in its own bundle.")
    return {"uuids": native_uuids(binary),
            "sha256": hashlib.sha256(binary.read_bytes()).hexdigest(),
            "relativePath": str(resolved.relative_to(bundle.resolve()))}


def build_metadata(bundle, mode):
    info = plistlib.loads((bundle / "Contents/Info.plist").read_bytes())
    expected_client = mode == "client"
    expected_id = BUNDLE_IDENTIFIERS["secondary" if expected_client else "primary"]
    if info.get("CFBundleIdentifier") != expected_id:
        raise RuntimeError("Base app profile differs from the shipped data identity; rebuild it with the stable identity.")
    build_id = info.get("MassarBuildIdentifier", "")
    source_hash = info.get("MassarSourceFingerprint", "")
    defines = info.get("MassarDartDefines", [])
    app_version = read_app_version(ROOT)
    version_name, version_number = app_version.split("+")
    expected_defines = {f"MASSAR_BUILD_ID={build_id}",
                        "MASSAR_CLIENT_ONLY=" + ("true" if expected_client else "false"),
                        f"MASSAR_APP_VERSION={app_version}"}
    if (info.get("MassarAppVersion") != app_version or
            info.get("CFBundleVersion") != version_number or
            info.get("CFBundleShortVersionString") != version_name.split("-")[0]):
        raise RuntimeError("Base app version differs from its compiled release metadata; rebuild it.")
    if type(info.get("MassarClientOnly")) is not bool or info["MassarClientOnly"] != expected_client:
        raise RuntimeError(f"Expected a dedicated {mode} base app; its client-only marker is incorrect.")
    if not isinstance(build_id, str) or not isinstance(source_hash, str) or not re.fullmatch(r"[0-9a-f]{16}", build_id) or not re.fullmatch(r"[0-9a-f]{64}", source_hash):
        raise RuntimeError("Missing verified build/source fingerprint in base app.")
    if not isinstance(defines, list) or len(defines) != 3 or any(not isinstance(define, str) for define in defines) or set(defines) != expected_defines:
        raise RuntimeError("Dart compile defines do not match the requested base mode.")
    aot = aot_identity(bundle)
    if aot["uuids"] != info.get("MassarAotUUIDs") or aot["sha256"] != info.get("MassarAotSha256"):
        raise RuntimeError("Compiled Dart AOT image differs from its base build marker.")
    admin_present = any(bundle.rglob("admin_account.json"))
    if expected_client and (admin_present or any(bundle.rglob("installation_seed.json"))):
        raise RuntimeError("Dedicated client base still contains the bundled private installation data.")
    if not expected_client and (not admin_present or not any(bundle.rglob("installation_seed.json"))):
        raise RuntimeError("Host base is missing its bundled admin account or installation seed.")
    return {"clientOnly": expected_client, "buildIdentifier": build_id,
            "appVersion": app_version,
            "sourceFingerprint": source_hash, "aotUUIDs": aot["uuids"],
            "aotSha256": aot["sha256"], "aotRelativePath": aot["relativePath"],
            "containsBundledAdmin": admin_present}


def verify_base_pair(metadata):
    host, client = metadata["primary"], metadata["secondary"]
    if host["clientOnly"] or not client["clientOnly"]:
        raise RuntimeError("Primary must be host and secondary must be dedicated client.")
    if host["sourceFingerprint"] != client["sourceFingerprint"]:
        raise RuntimeError("Host and client were built from different source versions.")
    if host["buildIdentifier"] == client["buildIdentifier"]:
        raise RuntimeError("Host and client must have separate compile identifiers.")
    if host["aotSha256"] == client["aotSha256"] or set(host["aotUUIDs"].values()) & set(client["aotUUIDs"].values()):
        raise RuntimeError("Host and client must contain independently compiled Dart AOT images.")


def strip_client_private_assets(bundle):
    info = plistlib.loads((bundle / "Contents/Info.plist").read_bytes())
    if info.get("MassarClientOnly") is not True or "MASSAR_CLIENT_ONLY=true" not in info.get("MassarDartDefines", []):
        raise RuntimeError("Refusing to strip admin asset from an unverified client.")
    for asset in (asset for name in ("admin_account.json", "installation_seed.json", "data_repair_20261002.json", "data_repair_20261004.json", "academic_import_20261004.json", "cairo_academic_import_20261004.json", "cairo_codes_20261004.json", "gec_codes_20261004.json", "gec_duplicate_codes_20261004.json") for asset in bundle.rglob(name)):
        if asset.parts[-3:] != ("flutter_assets", "assets", asset.name) or bundle.resolve() not in asset.resolve().parents:
            raise RuntimeError("Unexpected client private asset location.")
        asset.unlink()


def signature_details(component):
    inspected = subprocess.run(
        ["codesign", "-dv", "--verbose=4", str(component)],
        check=True, text=True, capture_output=True)
    details = inspected.stderr
    team = re.search(r"^TeamIdentifier=(.+)$", details, re.M)
    return {"teamIdentifier": team.group(1) if team else None,
            "authorities": re.findall(r"^Authority=(.+)$", details, re.M),
            "adHoc": "Signature=adhoc" in details}


def sign_bundle(bundle, identity):
    frameworks = sorted((bundle / "Contents/Frameworks").glob("*.framework"))
    gateway = bundle / "Contents/MacOS/massar-lan-host"
    if not frameworks or not gateway.is_file():
        raise RuntimeError("Flutter/plugin frameworks or LAN gateway are missing.")
    components = [*frameworks, gateway, bundle]
    for component in components[:-1]:
        run("codesign", "--force", "--sign", identity, "--timestamp=none", component)
    # Re-signing the AOT image changes its signature bytes, not its compiled UUID.
    info_path = bundle / "Contents/Info.plist"
    info = plistlib.loads(info_path.read_bytes())
    aot = aot_identity(bundle)
    info.update(MassarAotUUIDs=aot["uuids"], MassarAotSha256=aot["sha256"])
    info_path.write_bytes(plistlib.dumps(info))
    run("codesign", "--force", "--sign", identity, "--timestamp=none", bundle)
    run("codesign", "--verify", "--deep", "--strict", bundle)
    signatures = {str(component.relative_to(bundle)) if component != bundle else "app":
                  signature_details(component) for component in components}
    expected = signatures["app"]
    if identity != "-":
        if expected["adHoc"] or expected["teamIdentifier"] in (None, "not set") or not expected["authorities"]:
            raise RuntimeError("An Apple-issued signing identity was requested but not verified.")
        if any(signature["teamIdentifier"] != expected["teamIdentifier"] or
               signature["authorities"] != expected["authorities"] or signature["adHoc"]
               for signature in signatures.values()):
            raise RuntimeError("Embedded signatures do not match the app's certificate/team.")
    return signatures


def compile_launcher(role, title, destination):
    template = (ROOT / "macos/PreviewRunner.swift").read_text(encoding="utf-8")
    title_line = "window.title = " + json.dumps(title, ensure_ascii=False)
    source, count = re.subn(r'^window\.title\s*=\s*".*"$',
                            lambda _: title_line, template, flags=re.M)
    if count != 1:
        raise RuntimeError("Expected exactly one native window title in PreviewRunner.swift.")
    generated = PREPARED / role / "PreviewRunner.swift"
    generated.parent.mkdir(parents=True, exist_ok=True)
    generated.write_text(source, encoding="utf-8")
    for module in ("file_selector_macos", "printing"):
        if not (BUILD / f"{module}.swiftmodule").is_file():
            raise RuntimeError("Build the base Mac preview before compiling trial launchers.")
    sdk = subprocess.check_output(["xcrun", "--show-sdk-path"], text=True).strip()
    run("xcrun", "swiftc", "-swift-version", "5", "-target", "arm64-apple-macosx11.0",
        "-sdk", sdk, "-F", destination.parent.parent / "Frameworks", "-I", BUILD,
        "-module-name", f"MassarLANTrial{role.title()}",
        "-framework", "FlutterMacOS", "-framework", "file_selector_macos",
        "-framework", "printing", generated,
        "-Xlinker", "-rpath", "-Xlinker", "@executable_path/../Frameworks",
        "-o", destination)


def reject_runtime_file(name, source):
    if any(name == private or name.startswith(private + ".")
           for private in LAN_SETTINGS_FILES):
        raise RuntimeError("Package contains private runtime LAN/database metadata.")
    if name.endswith(SQLITE_SUFFIXES) or source.read(16) == b"SQLite format 3\x00":
        raise RuntimeError("Package contains a database.")
    if name.endswith(".json"):
        source.seek(0)
        try:
            document = json.load(source)
        except (ValueError, UnicodeError):
            return
        if isinstance(document, dict) and document.get("format") == "massar-center-backup":
            raise RuntimeError("Package contains a private runtime backup.")


def reject_bundle_databases(bundle):
    for path in bundle.rglob("*"):
        if not path.is_file():
            continue
        with path.open("rb") as source:
            reject_runtime_file(path.name, source)


def verify_distinct_uuids(base_uuids, results):
    used = set(base_uuids.values())
    for role, result in results.items():
        uuids = result["mainExecutableUUIDs"]
        if set(uuids) != {"arm64"} or set(uuids.values()) & used:
            raise RuntimeError(f"Main executable UUID is absent or shared by {role}.")
        used.update(uuids.values())


def verify_archive(archive, bundle_id, prepared):
    with zipfile.ZipFile(archive) as package:
        if package.testzip() is not None:
            raise RuntimeError("Corrupt trial archive")
        names = package.namelist()
        for name in names:
            if name.endswith("/") or name.startswith("__MACOSX/"):
                continue
            with package.open(name) as source:
                reject_runtime_file(Path(name).name, source)
        if prepared["clientOnly"] and any(Path(name).name in {"admin_account.json", "installation_seed.json"} for name in names):
            raise RuntimeError("Dedicated client archive contains the bundled private installation data.")
        if not any(name.endswith("/Contents/MacOS/massar-lan-host") for name in names):
            raise RuntimeError("LAN gateway is missing")
        plists = [name for name in names if name.endswith(".app/Contents/Info.plist") and not name.startswith("__MACOSX/")]
        if len(plists) != 1:
            raise RuntimeError("Trial archive must contain exactly one app.")
        info = plistlib.loads(package.read(plists[0]))
        if info["CFBundleIdentifier"] != bundle_id:
            raise RuntimeError("Trial data isolation identity is incorrect")
        if type(info.get("MassarClientOnly")) is not bool or info["MassarClientOnly"] != prepared["clientOnly"]:
            raise RuntimeError("Archive client-only mode differs from its verified bundle.")
        if info.get("MassarDartDefines") != [f"MASSAR_BUILD_ID={prepared['buildIdentifier']}",
            "MASSAR_CLIENT_ONLY=" + ("true" if prepared["clientOnly"] else "false"),
            f"MASSAR_APP_VERSION={prepared['appVersion']}"]:
            raise RuntimeError("Archive Dart defines differ from the verified role.")
        if info.get("MassarAppVersion") != prepared['appVersion']:
            raise RuntimeError("Archive application version differs from the verified role.")
        app_prefix = plists[0].removesuffix("Contents/Info.plist")
        aot_path = app_prefix + prepared["aotRelativePath"]
        if hashlib.sha256(package.read(aot_path)).hexdigest() != prepared["aotSha256"]:
            raise RuntimeError("Archive Dart AOT image differs from the verified role.")
        executable = plists[0].removesuffix("Info.plist") + "MacOS/" + info["CFBundleExecutable"]
        if hashlib.sha256(package.read(executable)).hexdigest() != prepared["mainExecutableSha256"]:
            raise RuntimeError("Archive main executable differs from its verified signed bundle.")
    return {**prepared, "archive": str(archive), "bytes": archive.stat().st_size,
            "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(), "zipCrc": True}


def prepare_role(base, role, options):
    name, label = ROLES[role]
    output_tag = options.output_tag or options.edition
    suffix = f"-{output_tag}" if output_tag else ""
    folder = (PREPARED / "bundles" if options.prepare_only else DIST) / f"mac-lan-trial-{role}-code{suffix}"
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / f"{name}.app"
    if target.exists():
        raise SystemExit(f"Output exists; refusing to replace a potentially running app: {target}")
    shutil.copytree(base, target, symlinks=True)
    plist_path = target / "Contents/Info.plist"
    info = plistlib.loads(plist_path.read_bytes())
    bundle_id = BUNDLE_IDENTIFIERS[role]
    info.update(CFBundleIdentifier=bundle_id, CFBundleName=label, CFBundleDisplayName=label)
    plist_path.write_bytes(plistlib.dumps(info))
    if role == "secondary":
        strip_client_private_assets(target)
    executable = main_executable(target)
    compile_launcher(role, label, executable)
    reject_bundle_databases(target)
    signatures = sign_bundle(target, options.sign_identity)
    (folder / "ابدأ هنا.txt").write_text(GUIDE, encoding="utf-8")
    metadata = build_metadata(target, "client" if role == "secondary" else "host")
    return {**metadata, "bundle": str(target), "bundleIdentifier": bundle_id, "windowTitle": label,
            "mainExecutableUUIDs": native_uuids(executable),
            "mainExecutableSha256": hashlib.sha256(executable.read_bytes()).hexdigest(),
            "signatures": signatures, "codesign": True, "containsDatabase": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-app", type=Path,
                        default=DIST / "Massar Center Mac Code Host Base.app")
    parser.add_argument("--client-base-app", type=Path, required=True,
                        help="Separately compiled --client-only Mac base app for secondary.")
    parser.add_argument("--sign-identity", default="-",
                        help="Apple-issued codesign identity name/fingerprint, or '-' for ad hoc.")
    parser.add_argument("--prepare-only", action="store_true",
                        help="Compile/sign/verify build-artifact apps without writing dist or ZIPs.")
    parser.add_argument("--edition", default=EDITION,
                        help="Fixed shipped data profile; only fresh20261002 is accepted.")
    parser.add_argument("--output-tag", default="",
                        help="Separate update filenames while retaining the edition's application data profile.")
    options = parser.parse_args()
    if options.edition != EDITION:
        raise SystemExit("Changing --edition would hide existing data. Keep fresh20261002 and use --output-tag for update filenames.")
    if options.output_tag and not re.fullmatch(r"[a-z][a-z0-9-]{0,63}", options.output_tag):
        raise SystemExit("--output-tag must be a lowercase output name.")
    output_tag = options.output_tag or options.edition
    suffix = f"-{output_tag}" if output_tag else ""
    bases = {"primary": options.base_app.resolve(), "secondary": options.client_base_app.resolve()}
    metadata, base_uuids = {}, {}
    for role, base in bases.items():
        if DIST.resolve() not in base.parents or not base.is_dir():
            raise SystemExit("Use built host/client apps inside this project's dist directory.")
        run("codesign", "--verify", "--deep", "--strict", base)
        metadata[role] = build_metadata(base, "client" if role == "secondary" else "host")
        base_uuids.update({f"{role}-{arch}": uuid for arch, uuid in native_uuids(main_executable(base)).items()})
        reject_bundle_databases(base)
    verify_base_pair(metadata)
    results = {role: prepare_role(bases[role], role, options) for role in ROLES}
    verify_distinct_uuids(base_uuids, results)
    verify_base_pair(results)
    if not options.prepare_only:
        for role, prepared in list(results.items()):
            archive = DIST / f"massar-mac-{role}-trial-arm64-code{suffix}.zip"
            if archive.exists():
                raise SystemExit(f"Archive exists; refusing to overwrite it: {archive}")
            run("ditto", "-c", "-k", "--sequesterRsrc", "--keepParent",
                Path(prepared["bundle"]).parent, archive)
            results[role] = verify_archive(archive, prepared["bundleIdentifier"], prepared)
    output = ROOT / "build/verification" / (
        f"mac-lan-code-pair-prepared{suffix}.json" if options.prepare_only else f"mac-lan-code-pair-integrity{suffix}.json")
    output.parent.mkdir(parents=True, exist_ok=True)
    manifest = {"baseBundles": {role: str(base) for role, base in bases.items()},
                "baseMainExecutableUUIDs": base_uuids,
                "preparedOnly": options.prepare_only, "edition": options.edition,
                "outputTag": output_tag, "roles": results}
    output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
