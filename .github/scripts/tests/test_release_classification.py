"""Unit tests for release_classification — the additive-vs-breaking verdict that
drives the watcher's green-on-arrival path.

The dangerous direction is a false "additive" (it would let an unreviewed
breaking release go green), so the tests lean on that: real .NET breaking notes,
Python-only breaking notes, and removal-only diffs.
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from release_classification import (  # noqa: E402
    added_members,
    classify,
    diff_is_trustworthy,
    dotnet_breaking_lines,
    is_additive,
    removed_members,
    safe_members,
    signature_changed_members,
)

# Trimmed from the real MAF 1.11.1 release notes.
NOTES_1_11_1 = """
## Changes:
* d9ec5ea update package version (#6752)
* e3b64fd .NET: [BREAKING] Make all AgentSkillsProvider tools require approval by default (#6729)
* 3a5bbb5 .NET: Add DeclarativeWorkflowJsonOptions for AOT-safe declarative workflow checkpointing (#6745)
* 9f1ee23 Python: [BREAKING] Refactor FileSkillsSource for depth-based discovery (#6488)
"""

# A purely additive .NET release: a Python breaking line, but nothing .NET-breaking.
NOTES_ADDITIVE = """
## Changes:
* aaa0001 .NET: Add CreateMcpTool projectConnectionId overload (#6703)
* aaa0002 Python: [BREAKING] Something python-only (#1)
* aaa0003 .NET: Fix hosted agent crash after tool call (#6231)
"""

DIFF_ADDITIVE = """
- Member 'ReadSkillResourceToolName' was added
- Member 'RunSkillScriptToolName' was added
### AgentSkillsProviderBuilder
- Member 'UseSource' was added
"""

DIFF_WITH_REMOVAL = """
- Member 'UseScriptApproval' was removed
- Member 'UseSource' was added
"""


def test_dotnet_breaking_lines_picks_only_dotnet():
    lines = dotnet_breaking_lines(NOTES_1_11_1)
    assert len(lines) == 1
    assert "AgentSkillsProvider" in lines[0]
    # The Python [BREAKING] line must NOT be counted.
    assert all("Python:" not in ln or ".NET" in ln for ln in lines)


def test_1_11_1_is_breaking():
    # Notes flag .NET breaking AND the diff removed a member — both say breaking.
    assert is_additive(NOTES_1_11_1, DIFF_WITH_REMOVAL) is False
    assert classify(NOTES_1_11_1, DIFF_WITH_REMOVAL)["additive"] is False


def test_python_only_breaking_is_additive_for_dotnet():
    # A Python-only [BREAKING] must not flip the .NET verdict; additive diff.
    assert is_additive(NOTES_ADDITIVE, DIFF_ADDITIVE) is True


def test_removal_forces_breaking_even_with_clean_notes():
    # No .NET breaking note, but the diff removed a member ⇒ conservative breaking.
    assert is_additive(NOTES_ADDITIVE, DIFF_WITH_REMOVAL) is False


def test_empty_inputs_are_breaking_fail_closed():
    # No diff = no positive evidence of additivity => NOT additive (fail-closed).
    assert is_additive("", "") is False
    assert classify("", "")["additive"] is False
    assert diff_is_trustworthy("") is False


def test_failed_diff_is_breaking_even_with_clean_notes():
    # An error dump (no dotnet-inspect markers) is not trustworthy => breaking.
    err = "error NU1101: Unable to find package Microsoft.Agents.AI 1.9.9.\nStack trace ..."
    assert diff_is_trustworthy(err) is False
    assert is_additive("## Changes:\n* .NET: tidy internals\n", err) is False


def test_no_api_changes_banner_is_trustworthy_additive():
    assert diff_is_trustworthy("> [!NOTE]\n> No API changes detected.\n") is True
    assert is_additive("## Changes:\n* .NET: docs only\n", "No API changes detected.") is True


def test_signature_change_is_breaking():
    diff = "- Member 'SearchFilesAsync' signature changed: `old` -> `new`\n"
    assert signature_changed_members(diff) == ["SearchFilesAsync"]
    assert is_additive("## Changes:\n* .NET: adjust SearchFilesAsync\n", diff) is False
    assert classify("", diff)["additive"] is False


def test_safe_members_drops_trailing_newline_name():
    # The bug the review caught: `$` matched before a trailing \n; \A..\Z must not.
    safe, dropped = safe_members(["GoodName", "Evil\n", "Bad\rName", "Has Space"])
    assert safe == ["GoodName"]
    assert dropped == 3


def test_usage_token_and_header_alone_are_not_trustworthy():
    # 'diff --package' is invocation/usage syntax, not proof a diff parsed.
    usage = "Usage: dotnet-inspect diff --package <id>\nerror: missing required argument"
    assert diff_is_trustworthy(usage) is False
    assert is_additive("## Changes:\n* .NET: x\n", usage) is False
    # The report HEADER alone is the FIRST line — a truncated diff carries it with
    # no content bullets, so it must NOT count as a completed diff (fail-closed).
    assert diff_is_trustworthy("# API Diff: Microsoft.Agents.AI\n") is False
    # A completed diff carries at least one content marker.
    assert diff_is_trustworthy("# API Diff: X\n- Member 'A' was added\n") is True


def test_new_obsolete_member_is_not_additive():
    # The REAL dotnet-inspect form carries a message: [Obsolete("...")].
    diff = "- Member 'OldWay' was added: `[Obsolete(\"Use NewWay\")] void OldWay()`\n"
    assert is_additive("## Changes:\n* .NET: deprecate OldWay\n", diff) is False
    assert classify("", diff)["obsolete"] is True
    # Bare and Attribute forms also match.
    assert is_additive("", "- Member 'X' was added [ObsoleteAttribute]\n") is False
    assert is_additive("", "- Member 'Y' was added [Obsolete]\n") is False


def test_summary_breaking_count_forces_breaking_on_truncated_diff():
    # Additive bullet present (trustworthy) but the summary claims breaking>0 with
    # the breaking bullets truncated => must classify breaking.
    diff = "# API Diff: X\n**Summary:** 3 breaking, 1 additive\n- Member 'New' was added\n"
    assert is_additive("## Changes:\n* .NET: x\n", diff) is False
    assert classify("", diff)["summary_breaking"] == 3
    # A clean summary (0 breaking) does not block additive.
    diff2 = "**Summary:** 0 breaking, 2 additive\n- Member 'New' was added\n"
    assert is_additive("## Changes:\n* .NET: x\n", diff2) is True


def test_breaking_marker_is_order_independent():
    assert dotnet_breaking_lines("* x [BREAKING] .NET: removed Foo")  # [BREAKING] before .NET
    assert dotnet_breaking_lines("* x .NET: [BREAKING] removed Foo")  # .NET before [BREAKING]
    assert not dotnet_breaking_lines("* x Python: [BREAKING] py-only")


def test_added_and_removed_member_extraction():
    assert added_members(DIFF_ADDITIVE) == [
        "ReadSkillResourceToolName",
        "RunSkillScriptToolName",
        "UseSource",
    ]
    assert removed_members(DIFF_WITH_REMOVAL) == ["UseScriptApproval"]
    # De-duplicates while preserving first-seen order.
    assert added_members(DIFF_ADDITIVE + DIFF_ADDITIVE) == added_members(DIFF_ADDITIVE)
