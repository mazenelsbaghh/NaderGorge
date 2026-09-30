import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))

from ef_migration_inventory import migration_inventory


def test_designer_metadata_identifies_registered_migrations() -> None:
    files = {
        "20260101000000_Initial.cs": "public partial class Initial : Migration {}",
        "20260101000000_Initial.Designer.cs": '[Migration("20260101000000_Initial")]\n',
        "20260102000000_SourceOnly.cs": "public partial class SourceOnly : Migration {}",
    }

    registered, unregistered = migration_inventory(files, files.__getitem__)

    assert registered == ["20260101000000_Initial"]
    assert unregistered == ["20260102000000_SourceOnly"]


def test_mismatched_designer_metadata_fails_closed() -> None:
    files = {
        "20260101000000_Initial.cs": "public partial class Initial : Migration {}",
        "20260101000000_Initial.Designer.cs": '[Migration("20260102000000_Other")]\n',
    }

    with pytest.raises(ValueError, match="does not match file name"):
        migration_inventory(files, files.__getitem__)
