#!/usr/bin/env python3
"""Update the registry's release-level metadata without reformatting entries."""
from __future__ import annotations

import argparse
import datetime as dt
import re
import sys
from pathlib import Path


DEFAULT_REGISTRY = Path(".github/skills/maf-obsolete-api-registry/registry.yaml")
VERSION_RE = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+$")


def _replace_root_field(text: str, field: str, value: str) -> str:
    pattern = re.compile(rf"^{re.escape(field)}:[^\r\n]*(\r?)$", re.MULTILINE)
    matches = list(pattern.finditer(text))
    if len(matches) != 1:
        raise ValueError(
            f"registry must contain exactly one root {field} field; found {len(matches)}"
        )
    return pattern.sub(lambda match: f'{field}: "{value}"{match.group(1)}', text)


def update_registry_metadata(text: str, target_version: str, last_updated: str) -> str:
    """Return *text* with the two release-level registry fields updated."""
    if not VERSION_RE.fullmatch(target_version):
        raise ValueError(f"target version is not stable X.Y.Z: {target_version!r}")
    try:
        parsed_date = dt.date.fromisoformat(last_updated)
    except ValueError as exc:
        raise ValueError(f"last-updated is not a valid ISO calendar date: {last_updated!r}") from exc
    if parsed_date.isoformat() != last_updated:
        raise ValueError(f"last-updated must use canonical YYYY-MM-DD form: {last_updated!r}")

    updated = _replace_root_field(text, "target_maf_version", target_version)
    return _replace_root_field(updated, "last_updated", last_updated)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--registry", type=Path, default=DEFAULT_REGISTRY)
    parser.add_argument("--target-version", required=True)
    parser.add_argument("--last-updated", required=True)
    parser.add_argument("--dry-run", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        raw = args.registry.read_bytes()
        text = raw.decode("utf-8")
        updated = update_registry_metadata(
            text,
            target_version=args.target_version,
            last_updated=args.last_updated,
        )
        rendered = updated.encode("utf-8")
        if args.dry_run:
            sys.stdout.buffer.write(rendered)
            return 0
        args.registry.write_bytes(rendered)
    except (OSError, UnicodeError, ValueError) as exc:
        print(f"Cannot update registry metadata: {exc}", file=sys.stderr)
        return 1

    print(
        f"Updated registry metadata to MAF {args.target_version} "
        f"({args.last_updated})."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
