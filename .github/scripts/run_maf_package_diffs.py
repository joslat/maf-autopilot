#!/usr/bin/env python3
"""Run and validate every diffable surface in ``maf-package-plan.json``.

The loop is deliberately sequential, bounded, and manifest-ordered.  A failure
on one package is recorded but never hides later package evidence.  Raw stdout
and stderr are retained for audit; downstream consumers use the independent
validation result and fail closed rather than deleting malformed evidence.
"""
from __future__ import annotations

import argparse
import pathlib
import subprocess
import sys
from collections.abc import Callable
from typing import Any

from maf_package_evidence import (
    PLAN_FILENAME,
    RESULTS_FILENAME,
    load_package_plan,
    write_results,
)
from validate_dotnet_inspect_diff import validate_diff_file


NUGET_SOURCE = "https://api.nuget.org/v3/index.json"
DEFAULT_TIMEOUT_SECONDS = 180


def _append_validation_errors(path: pathlib.Path, errors: list[str]) -> None:
    if not errors:
        return
    with path.open("a", encoding="utf-8") as stream:
        stream.write("\n--- MAF Doctor evidence validation ---\n")
        for error in errors:
            stream.write(f"- {error}\n")


def execute_plan(
    plan: dict[str, Any],
    results_path: pathlib.Path,
    base_dir: pathlib.Path,
    executable: str = "dotnet-inspect",
    timeout_seconds: int = DEFAULT_TIMEOUT_SECONDS,
    runner: Callable[..., Any] = subprocess.run,
) -> tuple[dict[str, Any], int]:
    """Execute the plan and return ``(results_document, exit_code)``."""
    results: dict[str, Any] = {
        "schema_version": 1,
        "release": plan["release"],
        "surfaces": [],
    }
    write_results(results_path, results)
    any_failure = False

    for surface in plan["surfaces"]:
        diff_path = base_dir / surface["diff_artifact"]
        stderr_path = base_dir / surface["stderr_artifact"]
        diff_path.write_bytes(b"")
        stderr_path.write_bytes(b"")
        attempted = False
        tool_exit_code: int | None = None
        validation_errors: list[str] = []
        validation_status = "not_applicable"

        if surface["status"] == "diffable":
            attempted = True
            command = [
                executable,
                "diff",
                "--package",
                (
                    f"{surface['package']}@{surface['old_package_version']}"
                    f"..{surface['new_package_version']}"
                ),
                "--source",
                NUGET_SOURCE,
            ]
            try:
                with diff_path.open("wb") as stdout, stderr_path.open("wb") as stderr:
                    completed = runner(
                        command,
                        stdout=stdout,
                        stderr=stderr,
                        check=False,
                        timeout=timeout_seconds,
                    )
                tool_exit_code = int(completed.returncode)
            except subprocess.TimeoutExpired:
                validation_errors.append(
                    f"dotnet-inspect exceeded the {timeout_seconds}-second timeout"
                )
            except OSError as exc:
                validation_errors.append(f"could not execute dotnet-inspect: {exc}")

            if tool_exit_code != 0:
                validation_errors.append(
                    f"dotnet-inspect exited {tool_exit_code!r}, expected 0"
                )
            validation_errors.extend(
                validate_diff_file(
                    diff_path,
                    surface["package"],
                    surface["old_package_version"],
                    surface["new_package_version"],
                )
            )
            validation_status = "passed" if not validation_errors else "failed"
            if validation_errors:
                any_failure = True
                _append_validation_errors(stderr_path, validation_errors)
        else:
            stderr_path.write_text(
                f"{surface['status']}: {surface['reason']}\n",
                encoding="utf-8",
            )
            if surface["status"] == "unverifiable":
                any_failure = True

        results["surfaces"].append(
            {
                "order": surface["order"],
                "slug": surface["slug"],
                "package": surface["package"],
                "status": surface["status"],
                "diff_artifact": surface["diff_artifact"],
                "stderr_artifact": surface["stderr_artifact"],
                "attempted": attempted,
                "tool_exit_code": tool_exit_code,
                "validation_status": validation_status,
                "validation_errors": validation_errors,
            }
        )
        # Persist after every bounded iteration so a later package failure never
        # erases the evidence/results already captured for earlier packages.
        write_results(results_path, results)

    return results, 1 if any_failure else 0


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=pathlib.Path, default=pathlib.Path(PLAN_FILENAME))
    parser.add_argument(
        "--results", type=pathlib.Path, default=pathlib.Path(RESULTS_FILENAME)
    )
    parser.add_argument("--dotnet-inspect", default="dotnet-inspect")
    parser.add_argument(
        "--timeout-seconds",
        type=int,
        default=DEFAULT_TIMEOUT_SECONDS,
        help=f"per-surface subprocess timeout (default: {DEFAULT_TIMEOUT_SECONDS})",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    if args.timeout_seconds < 1:
        print("package diff execution failed: --timeout-seconds must be positive", file=sys.stderr)
        return 2
    try:
        plan = load_package_plan(args.plan)
        results, exit_code = execute_plan(
            plan,
            args.results,
            pathlib.Path.cwd(),
            executable=args.dotnet_inspect,
            timeout_seconds=args.timeout_seconds,
        )
    except (OSError, ValueError) as exc:
        print(f"package diff execution failed: {exc}", file=sys.stderr)
        return 2

    passed = sum(
        1
        for row in results["surfaces"]
        if row["validation_status"] == "passed"
    )
    print(
        f"Processed {len(results['surfaces'])} planned surfaces in manifest order; "
        f"{passed} validated."
    )
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
