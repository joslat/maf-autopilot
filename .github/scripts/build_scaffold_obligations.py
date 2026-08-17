#!/usr/bin/env python3
"""Write the deterministic immutable-obligations artifact for a watcher scaffold."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

from verify_ai_fill_delta import build_obligations, serialize_obligations


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("."))
    parser.add_argument("--old-version", required=True)
    parser.add_argument("--target-version", required=True)
    parser.add_argument("--base-commit", required=True)
    parser.add_argument("--target-branch", required=True)
    parser.add_argument(
        "--output",
        type=Path,
        help="Output path (defaults to the target-version contract path)",
    )
    parser.add_argument("--dry-run", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        document = build_obligations(
            args.root,
            old_version=args.old_version,
            target_version=args.target_version,
            base_commit=args.base_commit,
            target_branch=args.target_branch,
        )
        requested = args.output or Path(
            f".github/maf-scaffold-obligations/maf-{args.target_version}.json"
        )
        output = requested if requested.is_absolute() else args.root / requested
        if args.dry_run:
            print(serialize_obligations(document).decode("utf-8"), end="")
            return 0
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(serialize_obligations(document))
    except (OSError, UnicodeDecodeError, ValueError) as exc:
        print(f"Cannot build scaffold obligations: {exc}", file=sys.stderr)
        return 1
    print(f"Wrote immutable scaffold obligations to {output}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
