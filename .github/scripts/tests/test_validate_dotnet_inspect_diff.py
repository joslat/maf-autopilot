"""Regression tests for the release watcher's raw diff evidence gate."""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from validate_dotnet_inspect_diff import (  # noqa: E402
    MAX_DIFF_FILE_BYTES,
    validate_diff_file,
    validate_diff_text,
)


PACKAGE = "Microsoft.Agents.AI"
OLD = "1.13.0"
NEW = "1.14.0"

VALID_CHANGED = """# API Diff: Microsoft.Agents.AI

| Field | Value |
| ----- | ----- |
| Versions | **1.13.0** -> **1.14.0** |
| Summary | **Summary:** 2 breaking, 1 additive across 2 types |

## Breaking Changes

### AgentMode

- Member 'Description' was removed
- Member '.ctor' signature changed: `old` -> `new`

## Additive Changes

### AgentModeProvider

- Member 'GetModeAsync' was added
"""

VALID_NO_CHANGES = """# API Diff: Microsoft.Agents.AI.Workflows

| Field | Value |
| ----- | ----- |
| Versions | **1.13.0** -> **1.14.0** |

> [!NOTE]
> No API changes detected.
"""

VALID_POTENTIALLY_BREAKING = """# API Diff: Microsoft.Agents.AI

| Field | Value |
| ----- | ----- |
| Versions | **1.13.0** -> **1.14.0** |
| Summary | **Summary:** 1 potentially breaking across 1 type |

## Potentially Breaking Changes

### DerivedAgent

- Base type changed from 'OldBase' to 'NewBase'
"""

# Exact diff-core.txt bytes downloaded from watcher run 32047961912.  The
# .NET 11 preview 3 Unix Console bug overwrote offset zero on each buffered
# write, leaving this 256-byte middle fragment even though the tool exited 0.
RUN_32047961912_FRAGMENT = """provalNotRequiredFunctionBypassing' was added
- Member 'UseApprovalResponseBinding' was added
' was added

### ToolAutoApprovalRuleContext

- Type 'Microsoft.Agents.AI.ToolAutoApprovalRuleContext' was added

### ChatClientBuilderExtensions

- Member 'UseAp"""


def test_complete_change_report_reconciles_summary_rows_and_types():
    assert validate_diff_text(VALID_CHANGED, PACKAGE, OLD, NEW) == []


def test_complete_no_change_report_is_accepted():
    assert validate_diff_text(
        VALID_NO_CHANGES,
        "Microsoft.Agents.AI.Workflows",
        OLD,
        NEW,
    ) == []


def test_complete_potentially_breaking_report_is_accepted():
    assert validate_diff_text(
        VALID_POTENTIALLY_BREAKING, PACKAGE, OLD, NEW
    ) == []


def test_no_change_report_rejects_injected_type_heading():
    injected = VALID_NO_CHANGES.replace(
        "> [!NOTE]", "### unexpected-in-no-change\n\n> [!NOTE]"
    )
    errors = validate_diff_text(
        injected, "Microsoft.Agents.AI.Workflows", OLD, NEW
    )
    assert any("no-change report must exactly contain" in error for error in errors)


def test_duplicate_preamble_row_is_rejected():
    versions = f"| Versions | **{OLD}** -> **{NEW}** |"
    duplicate = VALID_CHANGED.replace(versions, versions + "\n" + versions)
    errors = validate_diff_text(duplicate, PACKAGE, OLD, NEW)
    assert any("exactly one versions row" in error for error in errors)
    assert any("Summary row must immediately follow" in error for error in errors)


def test_reordered_preamble_rows_are_rejected():
    reordered = VALID_CHANGED.replace(
        "| Field | Value |\n| ----- | ----- |",
        "| ----- | ----- |\n| Field | Value |",
    )
    errors = validate_diff_text(reordered, PACKAGE, OLD, NEW)
    assert any("preamble is missing, duplicated, or out of order" in error for error in errors)


def test_exact_256_byte_watcher_fragment_is_rejected():
    assert len(RUN_32047961912_FRAGMENT.encode("utf-8")) == 256
    errors = validate_diff_text(RUN_32047961912_FRAGMENT, PACKAGE, OLD, NEW)
    assert errors
    assert any("expected report header" in error for error in errors)


def test_summary_count_mismatch_is_rejected():
    truncated = VALID_CHANGED.replace("2 breaking", "3 breaking")
    errors = validate_diff_text(truncated, PACKAGE, OLD, NEW)
    assert any("breaking count mismatch" in error for error in errors)


def test_trailing_partial_garbage_is_rejected_after_all_counted_rows():
    errors = validate_diff_text(
        VALID_CHANGED + "trailing partial frag",
        PACKAGE,
        OLD,
        NEW,
    )
    assert any("final non-empty line" in error for error in errors)


def test_arbitrary_middle_report_garbage_is_rejected():
    malformed = VALID_CHANGED.replace(
        "## Additive Changes",
        "arbitrary formatter drift\n\n## Additive Changes",
    )
    errors = validate_diff_text(malformed, PACKAGE, OLD, NEW)
    assert any("unrecognized non-empty line" in error for error in errors)


def test_truncated_final_change_row_is_not_treated_as_complete():
    truncated = VALID_CHANGED.replace(
        "- Member 'GetModeAsync' was added",
        "- Member 'GetModeAsync",
    )
    errors = validate_diff_text(truncated, PACKAGE, OLD, NEW)
    assert any("final non-empty line" in error for error in errors)


def test_malformed_middle_row_is_rejected_even_when_final_row_is_complete():
    malformed = VALID_CHANGED.replace(
        "- Member 'Description' was removed",
        "- Member 'Description' was",
    )
    errors = validate_diff_text(malformed, PACKAGE, OLD, NEW)
    assert any("change row does not match" in error for error in errors)
    assert not any("final non-empty line" in error for error in errors)


def test_disallowed_control_character_is_rejected():
    errors = validate_diff_text(
        VALID_CHANGED.replace("AgentMode\n", "AgentMode\0\n", 1),
        PACKAGE,
        OLD,
        NEW,
    )
    assert any("disallowed control characters: U+0000" in error for error in errors)


def test_oversized_diff_file_is_rejected_without_unbounded_read(tmp_path):
    path = tmp_path / "oversized.txt"
    path.write_bytes(b"A" * (MAX_DIFF_FILE_BYTES + 1))
    errors = validate_diff_file(path, PACKAGE, OLD, NEW)
    assert errors == [
        f"diff file exceeds the {MAX_DIFF_FILE_BYTES}-byte evidence limit: {path}"
    ]


def test_wrong_package_and_version_are_rejected():
    errors = validate_diff_text(VALID_CHANGED, "Wrong.Package", "1.12.0", NEW)
    assert any("expected report header" in error for error in errors)
    assert any("versions row" in error for error in errors)


def test_workflow_pins_fixed_release_and_routes_dynamic_plan_through_validator():
    repo_root = Path(__file__).resolve().parents[3]
    workflow = (
        repo_root / ".github" / "workflows" / "maf-release-watcher.yml"
    ).read_text(encoding="utf-8")

    assert workflow.count("INSPECT_VERSION=0.9.1") == 1
    assert workflow.count('--version "$INSPECT_VERSION"') == 2
    assert '${ACTUAL_VERSION%%+*}' in workflow
    assert workflow.count("build_maf_package_plan.py") == 1
    assert workflow.count("run_maf_package_diffs.py") == 1
    assert "maf-diff-results.json" in workflow
    assert "maf-registry-extraction-results.json" in workflow
    assert "--extraction-results maf-registry-extraction-results.json" in workflow
    assert "diff-*.txt" in workflow
    assert "cat release-notes.txt" not in workflow
    assert "Release-note evidence captured:" in workflow
    upload_index = workflow.index("- name: Upload diff artifacts for reviewer")
    gate_index = workflow.index(
        "- name: Gate — require complete release evidence before commit"
    )
    update_index = workflow.index("- name: Update .maf-version")
    assert upload_index < gate_index < update_index
    assert "release_classification.py --require-complete-evidence" in workflow

    runner = (
        repo_root / ".github" / "scripts" / "run_maf_package_diffs.py"
    ).read_text(encoding="utf-8")
    assert "validate_diff_file" in runner
    assert "for surface in plan[\"surfaces\"]" in runner
