"""Read the public version used by native packaging and compiled diagnostics."""
import re
from windows_package_assets import assert_clean


VERSION_PATTERN = r"\d{1,6}\.\d{1,6}\.\d{1,6}(?:-[0-9A-Za-z][0-9A-Za-z.-]{0,31})?\+\d{1,10}"


def read_app_version(root):
    assert_clean(root)
    matches = re.findall(r"^version:\s*([^\s#]+)\s*(?:#.*)?$",
                         (root / "pubspec.yaml").read_text(encoding="utf-8"), re.M)
    if len(matches) != 1:
        raise RuntimeError("pubspec.yaml must declare one application version.")
    version = matches[0].strip("\"'")
    if not re.fullmatch(VERSION_PATTERN, version):
        raise RuntimeError("Application version must include a bounded numeric build number.")
    return version
