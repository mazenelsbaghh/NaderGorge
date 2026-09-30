"""Identify migrations that EF Core can discover at runtime."""

from __future__ import annotations

import re
from collections.abc import Callable, Iterable
from pathlib import PurePosixPath


MIGRATION_FILE = re.compile(r"^(\d{14}_[A-Za-z0-9_]+)\.cs$")
MIGRATION_ATTRIBUTE = re.compile(r'\[Migration\("([^"\n]+)"\)\]')


def migration_inventory(
    names: Iterable[str], read_text: Callable[[str], str]
) -> tuple[list[str], list[str]]:
    """Return registered IDs and source files without EF migration metadata."""
    files = set(names)
    registered: list[str] = []
    unregistered: list[str] = []
    for name in sorted(files):
        match = MIGRATION_FILE.fullmatch(PurePosixPath(name).name)
        if not match or name.endswith(".Designer.cs"):
            continue
        migration_id = match.group(1)
        designer = name.removesuffix(".cs") + ".Designer.cs"
        metadata_file = designer if designer in files else name
        attribute = MIGRATION_ATTRIBUTE.search(read_text(metadata_file))
        if attribute is None:
            unregistered.append(migration_id)
        elif attribute.group(1) != migration_id:
            raise ValueError(f"EF migration metadata does not match file name: {name}")
        else:
            registered.append(migration_id)
    return registered, unregistered
