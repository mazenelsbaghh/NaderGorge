#!/usr/bin/env python3
"""Plan the smallest safe Massar build and enforce EF migration coverage."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Callable


ROOT = Path(__file__).resolve().parents[4]
PRODUCTION_IMAGE_ORDER = ("backend", "frontend", "worker", "migrator")
LOCAL_IMAGE_ORDER = (*PRODUCTION_IMAGE_ORDER, "gateway")
MIGRATION_RE = re.compile(
    r"^backend/src/NaderGorge\.Infrastructure/Migrations/"
    r"(?P<name>\d[^/.]*)\.cs$"
)
SCHEMA_PREFIXES = (
    "backend/src/NaderGorge.Domain/Entities/",
    "backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs",
    "backend/src/NaderGorge.Infrastructure/Data/Configurations/",
)


class PlanError(RuntimeError):
    pass


APPLICATION_COMPONENTS = frozenset(("frontend", "backend", "worker"))


@dataclass(frozen=True)
class ReleasePlan:
    base: str
    paths: tuple[str, ...]
    components: tuple[str, ...]
    local_images: tuple[str, ...]
    local_services: tuple[str, ...]
    database_changed: bool
    migration_added: bool
    migration_required: bool

    def as_dict(self) -> dict[str, object]:
        return {
            "base": self.base,
            "changedPaths": list(self.paths),
            "components": list(self.components),
            "localDockerImages": list(self.local_images),
            "localDockerServices": list(self.local_services),
            "productionDockerImages": list(PRODUCTION_IMAGE_ORDER),
            "databaseChanged": self.database_changed,
            "efMigrationAdded": self.migration_added,
            "efMigrationRequired": self.migration_required,
            "productionPolicy": (
                "Production always rebuilds the four immutable images; "
                "the local plan limits verification and local Docker work."
            ),
        }


def git(*arguments: str, check: bool = True) -> str:
    completed = subprocess.run(
        ("git", "-C", str(ROOT), *arguments),
        check=False,
        capture_output=True,
        text=True,
    )
    if check and completed.returncode:
        raise PlanError(completed.stderr.strip() or "Git inspection failed")
    return completed.stdout


def git_succeeds(*arguments: str) -> bool:
    return subprocess.run(
        ("git", "-C", str(ROOT), *arguments),
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    ).returncode == 0


def changed_paths(base: str) -> tuple[str, ...]:
    git("rev-parse", "--verify", f"{base}^{{commit}}")
    values: set[str] = set()
    commands = (
        ("diff", "--name-only", f"{base}..HEAD"),
        ("diff", "--name-only"),
        ("diff", "--cached", "--name-only"),
        ("ls-files", "--others", "--exclude-standard"),
    )
    for command in commands:
        values.update(
            line.strip()
            for line in git(*command).splitlines()
            if line.strip()
        )
    return tuple(sorted(values))


def resolve_base(value: str) -> str:
    if value != "AUTO":
        return value
    if git_succeeds("rev-parse", "--verify", "origin/main^{commit}"):
        return git("merge-base", "HEAD", "origin/main").strip()
    return "HEAD^"


def classify(
    base: str,
    paths: tuple[str, ...],
    path_existed_at_base: Callable[[str], bool] | None = None,
) -> ReleasePlan:
    components: set[str] = set()
    images: set[str] = set()
    existed = path_existed_at_base or (lambda _: False)
    new_main_migrations = tuple(
        match
        for path in paths
        if (match := MIGRATION_RE.fullmatch(path)) and not existed(path)
    )
    migration_added = any(
        (designer := (
            "backend/src/NaderGorge.Infrastructure/Migrations/"
            f"{match.group('name')}.Designer.cs"
        ))
        in paths
        and not existed(designer)
        and any(path.endswith("AppDbContextModelSnapshot.cs") for path in paths)
        for match in new_main_migrations
    )
    schema_inputs_changed = any(
        path == prefix or path.startswith(prefix)
        for path in paths
        for prefix in SCHEMA_PREFIXES
    )
    migrations_changed = any(
        path.startswith(
            "backend/src/NaderGorge.Infrastructure/Migrations/"
        )
        for path in paths
    )

    for path in paths:
        if path.startswith("frontend/"):
            components.add("frontend")
            if not path.startswith(("frontend/tests/", "frontend/docs/")):
                images.add("frontend")
        elif path.startswith("worker/"):
            components.add("worker")
            if not (
                path.endswith((".test.ts", ".test.mts"))
                or path.startswith("worker/tests/")
            ):
                images.add("worker")
        elif path.startswith("backend/"):
            components.add("backend")
            if not path.startswith("backend/tests/"):
                images.add("backend")
        if path.startswith("docker/nginx/"):
            components.add("infrastructure")
            images.add("gateway")
        if path.startswith(
            (
                "deploy/",
                ".agents/skills/ssh-server/",
            )
        ) or path in {
            "docker-compose.yml",
            "Makefile",
        }:
            components.add("infrastructure")
        if path.startswith(("docs/", "specs/")) or path.endswith(".md"):
            components.add("documentation")

    database_changed = schema_inputs_changed or migrations_changed
    if database_changed:
        components.add("database")
        images.update(("backend", "migrator"))
    if any(path == "backend/Dockerfile.migrator" for path in paths):
        images.add("migrator")

    ordered_images = tuple(name for name in LOCAL_IMAGE_ORDER if name in images)
    services = tuple(
        "landing" if name == "frontend" else name
        for name in ordered_images
    )
    return ReleasePlan(
        base=base,
        paths=paths,
        components=tuple(sorted(components)),
        local_images=ordered_images,
        local_services=services,
        database_changed=database_changed,
        migration_added=migration_added,
        migration_required=schema_inputs_changed and not migration_added,
    )


def render_human(plan: ReleasePlan) -> None:
    print("Massar change plan")
    print(f"  Base:              {plan.base}")
    print(f"  Changed paths:     {len(plan.paths)}")
    print(
        "  Components:        "
        + (", ".join(plan.components) or "none")
    )
    print(
        "  Local Docker:      "
        + (", ".join(plan.local_images) or "none")
    )
    print(
        "  Production Docker: "
        + ", ".join(PRODUCTION_IMAGE_ORDER)
        + " (immutable release contract)"
    )
    print(
        "  Database:          "
        + ("changed" if plan.database_changed else "unchanged")
    )
    print(
        "  EF migration:      "
        + (
            "REQUIRED — blocked"
            if plan.migration_required
            else "present"
            if plan.migration_added
            else "not required by changed paths"
        )
    )


def validate_scope(plan: ReleasePlan, scope: str) -> None:
    if scope == "all":
        return
    affected_applications = APPLICATION_COMPONENTS.intersection(plan.components)
    requires_all = bool(
        {"database", "infrastructure"}.intersection(plan.components)
    )
    if requires_all or affected_applications != {scope}:
        affected = ", ".join(plan.components) or "none"
        raise PlanError(
            f"scope '{scope}' is incompatible with affected components: {affected}; "
            "use --scope=all or select the single affected application"
        )


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser(description=__doc__)
    value.add_argument(
        "command",
        choices=(
            "plan",
            "check-db",
            "images",
            "services",
            "components",
            "validate-scope",
        ),
    )
    value.add_argument(
        "--base",
        default="AUTO",
        help="Comparison base. AUTO uses HEAD when dirty, otherwise HEAD^.",
    )
    value.add_argument("--json", action="store_true")
    value.add_argument(
        "--scope",
        choices=("frontend", "backend", "worker", "all"),
    )
    return value


def main() -> int:
    args = parser().parse_args()
    base = resolve_base(args.base)
    plan = classify(
        base,
        changed_paths(base),
        lambda path: git_succeeds("cat-file", "-e", f"{base}:{path}"),
    )
    if args.command == "validate-scope":
        if args.scope is None:
            raise PlanError("validate-scope requires --scope")
        validate_scope(plan, args.scope)
        print(f"Build scope accepted: {args.scope}")
    elif args.command == "images":
        print(" ".join(plan.local_images))
    elif args.command == "services":
        print(" ".join(plan.local_services))
    elif args.command == "components":
        print(" ".join(plan.components))
    elif args.json:
        print(json.dumps(plan.as_dict(), ensure_ascii=False, indent=2))
    else:
        render_human(plan)

    if args.command == "check-db" and plan.migration_required:
        print(
            "\nBLOCKED: EF model changes were detected without a new migration.\n"
            "Run: make ops-db-migration NAME=DescribeTheSchemaChange",
            flush=True,
        )
        return 3
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except PlanError as exc:
        print(f"release plan blocked: {exc}")
        raise SystemExit(2)
