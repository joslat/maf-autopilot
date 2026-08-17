#!/usr/bin/env python3
"""Validate that a captured ``dotnet-inspect diff`` is complete and coherent.

``dotnet-inspect`` 0.7.8's Linux Native-AOT build was affected by the .NET 11
preview 3 Unix console regression (dotnet/runtime#126843): redirecting stdout to
a regular file made each buffered write overwrite offset zero.  The process
still exited zero, leaving a roughly 256-byte middle fragment.  Exit status is
therefore necessary but not sufficient evidence that a watcher diff completed.

The watcher pins a fixed tool release, and this validator is the independent
fail-closed guard.  It accepts either the explicit no-change report or a change
report whose summary counts reconcile with every emitted classification row.
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys
import unicodedata


MAX_DIFF_FILE_BYTES = 4 * 1024 * 1024

_SECTION_TO_CLASSIFICATION = {
    "## Breaking Changes": "breaking",
    "## Potentially Breaking Changes": "potentially breaking",
    "## Additive Changes": "additive",
}
_SUMMARY_RE = re.compile(
    r"^\| Summary \| \*\*Summary:\*\* (?P<counts>.+) across "
    r"(?P<types>[1-9][0-9]*) types? \|$"
)
_COUNT_RE = re.compile(
    r"(?P<count>[0-9]+) (?P<classification>potentially breaking|breaking|additive)"
)
_COMPLETE_CHANGE_ROW_PATTERNS = tuple(
    re.compile(pattern)
    for pattern in (
        r"^- (?:Type|Interface|Member) '[^']+' was (?:added|removed)$",
        r"^- Type kind changed from '[^']+' to '[^']+'$",
        r"^- Type was (?:sealed|unsealed|made abstract|made non-abstract)$",
        r"^- Base type changed from '[^']+' to '[^']+'$",
        r"^- Generic parameter count changed from [0-9]+ to [0-9]+$",
        r"^- Generic parameter '[^']+' variance changed from '[^']+' to '[^']+'$",
        r"^- Generic parameter '[^']+' (?:added|removed) constraints: .+$",
        r"^- Member '[^']+' signature changed: `.+` -> `.+`$",
        r"^- Member '[^']+' (?:is no longer virtual|was made abstract)$",
        r"^- Enum member '[^']+' value changed from -?[0-9]+ to -?[0-9]+$",
    )
)


def _is_complete_change_row(line: str) -> bool:
    """Recognize every change-message shape emitted by pinned release 0.9.1."""
    return any(pattern.fullmatch(line) for pattern in _COMPLETE_CHANGE_ROW_PATTERNS)


def validate_diff_text(
    text: str,
    package: str,
    old_version: str,
    new_version: str,
) -> list[str]:
    """Return structural validation errors; an empty list means complete."""
    errors: list[str] = []
    disallowed_controls = sorted(
        {
            f"U+{ord(character):04X}"
            for character in text
            if unicodedata.category(character) == "Cc"
            and character not in "\t\n\r"
        }
    )
    if disallowed_controls:
        errors.append(
            "diff contains disallowed control characters: "
            + ", ".join(disallowed_controls)
        )

    lines = text.splitlines()
    expected_header = f"# API Diff: {package}"
    expected_versions = f"| Versions | **{old_version}** -> **{new_version}** |"

    if not lines:
        return ["diff output is empty"]
    if lines[0] != expected_header:
        errors.append(
            f"first line is not the expected report header {expected_header!r}"
        )
    if lines.count(expected_versions) != 1:
        errors.append(
            f"expected exactly one versions row {expected_versions!r}"
        )

    expected_prefix = [
        expected_header,
        "",
        "| Field | Value |",
        "| ----- | ----- |",
        expected_versions,
    ]
    if lines[:len(expected_prefix)] != expected_prefix:
        errors.append(
            "report header/table preamble is missing, duplicated, or out of order"
        )

    no_changes = "> No API changes detected."
    no_change_count = lines.count(no_changes)
    summary_lines = [line for line in lines if line.startswith("| Summary |")]

    if no_change_count:
        expected_no_change = expected_prefix + ["", "> [!NOTE]", no_changes]
        if lines != expected_no_change:
            errors.append(
                "no-change report must exactly contain the ordered preamble, NOTE, "
                "and final no-change banner"
            )
        return errors

    if len(summary_lines) != 1:
        errors.append("change report must contain exactly one Summary row")
        return errors
    if len(lines) < 7 or lines[5] != summary_lines[0] or lines[6] != "":
        errors.append(
            "change report Summary row must immediately follow the versions row "
            "and precede a blank body separator"
        )

    summary_match = _SUMMARY_RE.fullmatch(summary_lines[0])
    if summary_match is None:
        errors.append("Summary row does not match the pinned dotnet-inspect format")
        return errors

    expected_counts = {
        "breaking": 0,
        "potentially breaking": 0,
        "additive": 0,
    }
    count_matches = list(_COUNT_RE.finditer(summary_match.group("counts")))
    if not count_matches:
        errors.append("Summary row contains no recognized classification counts")
        return errors

    # Reject unparsed text between the comma-separated count phrases.  Without
    # this check a format drift could silently validate only the familiar part.
    reconstructed_counts = ", ".join(match.group(0) for match in count_matches)
    if reconstructed_counts != summary_match.group("counts"):
        errors.append("Summary row contains an unrecognized count expression")
    seen_classifications: set[str] = set()
    for match in count_matches:
        classification = match.group("classification")
        if classification in seen_classifications:
            errors.append(f"Summary repeats the {classification!r} count")
        seen_classifications.add(classification)
        expected_counts[classification] = int(match.group("count"))

    actual_counts = {key: 0 for key in expected_counts}
    type_names: set[str] = set()
    current_classification: str | None = None
    current_type: str | None = None
    current_type_rows = 0
    current_section_has_type = False
    seen_sections: set[str] = set()
    section_order = {
        "breaking": 0,
        "potentially breaking": 1,
        "additive": 2,
    }
    last_section_order = -1

    for line in lines[7:]:
        if not line.strip():
            continue
        if line in _SECTION_TO_CLASSIFICATION:
            if current_type is not None and current_type_rows == 0:
                errors.append(f"type heading {current_type!r} contains no change rows")
            if current_classification is not None and not current_section_has_type:
                errors.append(
                    f"{current_classification} section contains no type headings"
                )
            classification = _SECTION_TO_CLASSIFICATION[line]
            if classification in seen_sections:
                errors.append(f"duplicate {classification} section")
            order = section_order[classification]
            if order <= last_section_order:
                errors.append("change sections are not in pinned formatter order")
            seen_sections.add(classification)
            last_section_order = order
            current_classification = classification
            current_type = None
            current_type_rows = 0
            current_section_has_type = False
            continue
        if line.startswith("### "):
            if current_classification is None:
                errors.append(f"type heading appears outside a change section: {line!r}")
                continue
            if current_type is not None and current_type_rows == 0:
                errors.append(f"type heading {current_type!r} contains no change rows")
            current_type = line[4:].strip()
            current_type_rows = 0
            current_section_has_type = True
            if not current_type:
                errors.append("empty type heading in change section")
            else:
                type_names.add(current_type)
            continue
        if line.startswith("- "):
            if not _is_complete_change_row(line):
                errors.append(
                    "change row does not match the pinned dotnet-inspect format: "
                    f"{line!r}"
                )
            if current_classification is None or current_type is None:
                errors.append(f"change row appears outside a typed change section: {line!r}")
                continue
            actual_counts[current_classification] += 1
            current_type_rows += 1
            continue
        errors.append(
            "unrecognized non-empty line in pinned dotnet-inspect report: "
            f"{line!r}"
        )

    if current_type is not None and current_type_rows == 0:
        errors.append(f"type heading {current_type!r} contains no change rows")
    if current_classification is not None and not current_section_has_type:
        errors.append(f"{current_classification} section contains no type headings")

    for classification, expected in expected_counts.items():
        actual = actual_counts[classification]
        if actual != expected:
            errors.append(
                f"{classification} count mismatch: Summary says {expected}, "
                f"but the report contains {actual} rows"
            )

    expected_types = int(summary_match.group("types"))
    if len(type_names) != expected_types:
        errors.append(
            f"type count mismatch: Summary says {expected_types}, "
            f"but the report contains {len(type_names)} distinct type headings"
        )
    if sum(actual_counts.values()) == 0:
        errors.append("change report contains no change rows")

    non_empty_lines = [line for line in lines if line.strip()]
    final_line = non_empty_lines[-1]
    if not _is_complete_change_row(final_line):
        errors.append(
            "final non-empty line is not a complete dotnet-inspect change row: "
            f"{final_line!r}"
        )

    return errors


def validate_diff_file(
    path: pathlib.Path,
    package: str,
    old_version: str,
    new_version: str,
) -> list[str]:
    try:
        with path.open("rb") as stream:
            raw = stream.read(MAX_DIFF_FILE_BYTES + 1)
    except FileNotFoundError:
        return [f"diff file does not exist: {path}"]
    except OSError as exc:
        return [f"cannot read diff file {path}: {exc}"]
    if len(raw) > MAX_DIFF_FILE_BYTES:
        return [
            f"diff file exceeds the {MAX_DIFF_FILE_BYTES}-byte evidence limit: {path}"
        ]
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as exc:
        return [f"diff file is not valid UTF-8: {exc}"]
    return validate_diff_text(text, package, old_version, new_version)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=pathlib.Path)
    parser.add_argument("--package", required=True)
    parser.add_argument("--old-version", required=True)
    parser.add_argument("--new-version", required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    errors = validate_diff_file(
        args.path, args.package, args.old_version, args.new_version
    )
    if errors:
        print(
            f"dotnet-inspect evidence validation failed for {args.path}:",
            file=sys.stderr,
        )
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print(f"Validated complete dotnet-inspect evidence: {args.path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
