#!/usr/bin/env python3
"""Extract draft registry entries from already-validated package diff files."""
from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys
from collections.abc import Callable
from typing import Any

from maf_package_evidence import (
    MAX_METADATA_BYTES,
    PLAN_FILENAME,
    RESULTS_FILENAME,
    PackageEvidenceError,
    load_evidence_from_files,
)
from dedupe_registry_entries import split_blocks


DEFAULT_TIMEOUT_SECONDS = 120
REGISTRY_EXTRACTION_RESULTS_FILENAME = "maf-registry-extraction-results.json"


def _write_extraction_results(path: pathlib.Path, document: dict[str, Any]) -> None:
    path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")


def load_registry_extraction_results(
    path: pathlib.Path | str,
    plan: dict[str, Any],
) -> dict[str, Any]:
    """Load and strictly reconcile a per-surface registry-extraction ledger."""
    ledger_path = pathlib.Path(path)
    try:
        size = ledger_path.stat().st_size
    except FileNotFoundError as exc:
        raise PackageEvidenceError(
            f"registry extraction results do not exist: {ledger_path}"
        ) from exc
    if size > MAX_METADATA_BYTES:
        raise PackageEvidenceError(
            f"registry extraction results exceed the {MAX_METADATA_BYTES}-byte "
            f"metadata limit: {ledger_path}"
        )
    try:
        document = json.loads(ledger_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise PackageEvidenceError(
            f"cannot read registry extraction results {ledger_path}: {exc}"
        ) from exc
    if not isinstance(document, dict):
        raise PackageEvidenceError("registry extraction results root must be an object")
    if document.get("schema_version") != 2:
        raise PackageEvidenceError("registry extraction results schema_version must be 2")
    if document.get("release") != plan.get("release"):
        raise PackageEvidenceError(
            "registry extraction results release does not match the package plan"
        )
    rows = document.get("surfaces")
    if not isinstance(rows, list) or len(rows) != len(plan["surfaces"]):
        raise PackageEvidenceError(
            "registry extraction results must contain one row per planned surface"
        )
    for index, (row, surface) in enumerate(zip(rows, plan["surfaces"])):
        prefix = f"registry extraction surfaces[{index}]"
        if not isinstance(row, dict):
            raise PackageEvidenceError(f"{prefix} must be an object")
        for field in ("order", "slug", "package", "status"):
            if row.get(field) != surface.get(field):
                raise PackageEvidenceError(
                    f"{prefix}.{field} does not match the package plan"
                )
        status = row.get("extraction_status")
        allowed = (
            {"passed", "failed"}
            if surface["status"] == "diffable"
            else {"not_applicable"}
        )
        if status not in allowed:
            raise PackageEvidenceError(
                f"{prefix}.extraction_status must be one of {sorted(allowed)}"
            )
        attempted = row.get("attempted")
        if not isinstance(attempted, bool):
            raise PackageEvidenceError(f"{prefix}.attempted must be boolean")
        count = row.get("generated_entry_count")
        if isinstance(count, bool) or not isinstance(count, int) or count < 0:
            raise PackageEvidenceError(
                f"{prefix}.generated_entry_count must be a non-negative integer"
            )
        generated_ids = row.get("generated_entry_ids")
        if not isinstance(generated_ids, list) or any(
            not isinstance(entry_id, str) or not entry_id.strip()
            for entry_id in generated_ids
        ):
            raise PackageEvidenceError(
                f"{prefix}.generated_entry_ids must be an array of non-empty strings"
            )
        if len(generated_ids) != count:
            raise PackageEvidenceError(
                f"{prefix}.generated_entry_ids length must equal "
                "generated_entry_count"
            )
        if len({entry_id.casefold() for entry_id in generated_ids}) != len(
            generated_ids
        ):
            raise PackageEvidenceError(
                f"{prefix}.generated_entry_ids must be unique case-insensitively"
            )
        errors = row.get("errors")
        if not isinstance(errors, list) or any(
            not isinstance(error, str) or not error for error in errors
        ):
            raise PackageEvidenceError(f"{prefix}.errors must be an array of strings")
        exit_code = row.get("tool_exit_code")
        if exit_code is not None and (
            isinstance(exit_code, bool) or not isinstance(exit_code, int)
        ):
            raise PackageEvidenceError(f"{prefix}.tool_exit_code must be integer or null")
        if status == "passed" and (
            not attempted or exit_code != 0 or errors
        ):
            raise PackageEvidenceError(
                f"{prefix} passed rows require attempted=true, exit 0, and no errors"
            )
        if status == "failed" and not errors:
            raise PackageEvidenceError(f"{prefix} failed rows require errors")
        if status == "failed" and count != 0:
            raise PackageEvidenceError(
                f"{prefix} failed rows cannot claim generated entries"
            )
        if status == "not_applicable" and (
            attempted or exit_code is not None or count != 0 or errors
        ):
            raise PackageEvidenceError(
                f"{prefix} not_applicable row contains extraction results"
            )
    return document


def extract_registry_entries(
    plan: dict[str, Any],
    evidence: dict[str, Any],
    maf_doctor: pathlib.Path,
    output: pathlib.Path,
    extraction_results: pathlib.Path = pathlib.Path(
        REGISTRY_EXTRACTION_RESULTS_FILENAME
    ),
    dotnet: str = "dotnet",
    timeout_seconds: int = DEFAULT_TIMEOUT_SECONDS,
    runner: Callable[..., Any] = subprocess.run,
) -> int:
    """Write YAML plus a durable per-surface ledger; continue after failures."""
    output.write_text("", encoding="utf-8")
    failed = not evidence["trustworthy"]
    extracted = 0
    ledger: dict[str, Any] = {
        "schema_version": 2,
        "release": plan["release"],
        "surfaces": [],
    }
    _write_extraction_results(extraction_results, ledger)

    with output.open("a", encoding="utf-8") as stream:
        for surface in evidence["surfaces"]:
            row = {
                "order": surface["order"],
                "slug": surface["slug"],
                "package": surface["package"],
                "status": surface["status"],
                "attempted": False,
                "tool_exit_code": None,
                "extraction_status": "not_applicable",
                "generated_entry_count": 0,
                "generated_entry_ids": [],
                "errors": [],
            }
            if surface["status"] != "diffable":
                ledger["surfaces"].append(row)
                _write_extraction_results(extraction_results, ledger)
                continue
            if not surface["trusted"]:
                row["extraction_status"] = "failed"
                row["errors"] = [
                    "untrusted package diff: " + "; ".join(surface["errors"])
                ]
                print(
                    f"Skipping untrusted registry evidence for {surface['package']}: "
                    + "; ".join(surface["errors"]),
                    file=sys.stderr,
                )
                failed = True
                ledger["surfaces"].append(row)
                _write_extraction_results(extraction_results, ledger)
                continue

            command = [
                dotnet,
                str(maf_doctor),
                "registry-extract",
                surface["package"],
                surface["old_package_version"],
                surface["new_package_version"],
                "--diff-file",
                surface["diff_artifact"],
                "--release-version",
                plan["release"]["new_version"],
            ]
            if surface["id_scope"]:
                command.extend(["--id-scope", surface["id_scope"]])

            row["attempted"] = True
            try:
                completed = runner(
                    command,
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=timeout_seconds,
                )
            except subprocess.TimeoutExpired:
                row["extraction_status"] = "failed"
                row["errors"] = [
                    f"registry-extract timed out after {timeout_seconds}s"
                ]
                print(
                    f"registry-extract timed out after {timeout_seconds}s for "
                    f"{surface['package']}",
                    file=sys.stderr,
                )
                failed = True
                ledger["surfaces"].append(row)
                _write_extraction_results(extraction_results, ledger)
                continue
            except OSError as exc:
                row["extraction_status"] = "failed"
                row["errors"] = [f"registry-extract could not start: {exc}"]
                print(
                    f"registry-extract could not start for {surface['package']}: {exc}",
                    file=sys.stderr,
                )
                failed = True
                ledger["surfaces"].append(row)
                _write_extraction_results(extraction_results, ledger)
                continue
            row["tool_exit_code"] = completed.returncode
            if completed.stderr:
                print(completed.stderr.rstrip(), file=sys.stderr)
            if completed.returncode != 0:
                row["extraction_status"] = "failed"
                row["errors"] = [
                    f"registry-extract exited {completed.returncode}"
                ]
                print(
                    f"registry-extract failed for {surface['package']} "
                    f"(exit {completed.returncode})",
                    file=sys.stderr,
                )
                failed = True
                ledger["surfaces"].append(row)
                _write_extraction_results(extraction_results, ledger)
                continue
            stdout = completed.stdout or ""
            blocks = split_blocks(stdout)
            no_entries = stdout.strip() in {
                "# No new draft registry entries — diff contained no breaking-change candidates.",
                "entries: []",
            }
            if not blocks and not no_entries:
                row["extraction_status"] = "failed"
                row["errors"] = [
                    "registry-extract returned unrecognized successful output"
                ]
                print(
                    f"registry-extract output was unrecognized for {surface['package']}",
                    file=sys.stderr,
                )
                failed = True
                ledger["surfaces"].append(row)
                _write_extraction_results(extraction_results, ledger)
                continue
            row["extraction_status"] = "passed"
            row["generated_entry_count"] = len(blocks)
            row["generated_entry_ids"] = [entry_id for entry_id, _ in blocks]
            if stdout:
                stream.write(stdout.rstrip() + "\n\n")
            extracted += 1
            ledger["surfaces"].append(row)
            _write_extraction_results(extraction_results, ledger)

    print(f"Registry extraction consumed {extracted} validated package diff(s).")
    return 1 if failed else 0


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=pathlib.Path, default=pathlib.Path(PLAN_FILENAME))
    parser.add_argument(
        "--results", type=pathlib.Path, default=pathlib.Path(RESULTS_FILENAME)
    )
    parser.add_argument("--maf-doctor", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument(
        "--extraction-results",
        type=pathlib.Path,
        default=pathlib.Path(REGISTRY_EXTRACTION_RESULTS_FILENAME),
    )
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument(
        "--timeout-seconds", type=int, default=DEFAULT_TIMEOUT_SECONDS
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    if args.timeout_seconds < 1:
        print("registry extraction plan failed: --timeout-seconds must be positive", file=sys.stderr)
        return 2
    try:
        plan, evidence = load_evidence_from_files(args.plan, args.results)
        return extract_registry_entries(
            plan,
            evidence,
            args.maf_doctor,
            args.output,
            extraction_results=args.extraction_results,
            dotnet=args.dotnet,
            timeout_seconds=args.timeout_seconds,
        )
    except (OSError, ValueError) as exc:
        print(f"registry extraction plan failed: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
