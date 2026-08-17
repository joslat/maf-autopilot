"""Tests for gen_guide_section.py's green-on-arrival verdict: additive releases
get filled analysis sections; breaking releases keep TODO stubs; untrusted
member names are sanitized out of the unfenced New Patterns bullets.
"""
from __future__ import annotations

import os
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parents[1] / "gen_guide_section.py"


def _full_diff(package: str, old: str, new: str, fragment: str) -> str:
    rows = [line for line in fragment.splitlines() if line.startswith("- ")]
    breaking = [
        row for row in rows if "was removed" in row or "signature changed" in row
    ]
    potentially_breaking = [
        row for row in rows
        if "Base type changed" in row or "Type was sealed" in row
    ]
    additive = [
        row for row in rows
        if row not in breaking and row not in potentially_breaking
    ]
    counts = []
    if breaking:
        counts.append(f"{len(breaking)} breaking")
    if potentially_breaking:
        counts.append(f"{len(potentially_breaking)} potentially breaking")
    if additive:
        counts.append(f"{len(additive)} additive")
    sections = []
    if breaking:
        sections.append("## Breaking Changes\n\n### FixtureType\n\n" + "\n".join(breaking))
    if potentially_breaking:
        sections.append(
            "## Potentially Breaking Changes\n\n### FixtureType\n\n"
            + "\n".join(potentially_breaking)
        )
    if additive:
        sections.append("## Additive Changes\n\n### FixtureType\n\n" + "\n".join(additive))
    return (
        f"# API Diff: {package}\n\n"
        "| Field | Value |\n| ----- | ----- |\n"
        f"| Versions | **{old}** -> **{new}** |\n"
        f"| Summary | **Summary:** {', '.join(counts)} across 1 type |\n\n"
        + "\n\n".join(sections)
        + "\n"
    )


def _run(ws: Path, old: str, new: str, notes: str, diff: str) -> str:
    (ws / "release-notes.txt").write_text(notes, encoding="utf-8")
    package = "Microsoft.Agents.AI"
    complete_diff = _full_diff(package, old, new, diff)
    (ws / "diff-core.txt").write_text(complete_diff, encoding="utf-8")
    plan = {
        "schema_version": 1,
        "release": {"old_version": old, "new_version": new},
        "surfaces": [
            {
                "order": 0,
                "slug": "core",
                "package": package,
                "id_scope": "",
                "status": "diffable",
                "old_package_version": old,
                "new_package_version": new,
                "diff_artifact": "diff-core.txt",
                "stderr_artifact": "diff-core.stderr.txt",
                "reason": "fixture",
            }
        ],
        "lifecycle_events": [],
    }
    results = {
        "schema_version": 1,
        "release": plan["release"],
        "surfaces": [
            {
                "order": 0,
                "slug": "core",
                "package": package,
                "status": "diffable",
                "diff_artifact": "diff-core.txt",
                "stderr_artifact": "diff-core.stderr.txt",
                "attempted": True,
                "tool_exit_code": 0,
                "validation_status": "passed",
                "validation_errors": [],
            }
        ],
    }
    (ws / "diff-core.stderr.txt").write_text("", encoding="utf-8")
    (ws / "maf-package-plan.json").write_text(json.dumps(plan), encoding="utf-8")
    (ws / "maf-diff-results.json").write_text(json.dumps(results), encoding="utf-8")
    env = os.environ.copy()
    env["OLD_VERSION"] = old
    env["NEW_VERSION"] = new
    r = subprocess.run(
        [sys.executable, str(SCRIPT)],
        cwd=str(ws), env=env, capture_output=True, text=True,
    )
    assert r.returncode == 0, f"gen_guide_section.py failed:\n{r.stdout}\n{r.stderr}"
    return (ws / "guides" / f"maf-{new}-migration-guide.md").read_text(encoding="utf-8")


@pytest.fixture
def ws():
    d = Path(tempfile.mkdtemp(prefix="maf-guide-verdict-"))
    try:
        (d / "guides").mkdir()
        yield d
    finally:
        shutil.rmtree(d, ignore_errors=True)


def test_additive_release_fills_sections(ws):
    notes = "## Changes:\n* a1 .NET: Add CreateMcpTool overload (#6703)\n* a2 Python: [BREAKING] py-only\n"
    diff = "- Member 'NewThing' was added\n- Member 'OtherThing' was added\n**Summary:** 0 breaking, 2 additive\n"
    guide = _run(ws, "1.11.1", "1.12.0", notes, diff)

    assert "this is an **additive** release" in guide
    assert "- `NewThing`" in guide and "- `OtherThing`" in guide
    assert "None known for 1.12.0" in guide
    # No TODO stubs remain in an additive guide.
    assert "TODO: Review the diff above" not in guide


def test_breaking_release_keeps_todo(ws):
    notes = "## Changes:\n* b1 .NET: [BREAKING] Make tools require approval (#6729)\n"
    diff = "- Member 'UseScriptApproval' was removed\n"
    guide = _run(ws, "1.11.0", "1.11.1", notes, diff)

    assert "TODO: Review every package evidence block" in guide
    assert "this is an **additive** release" not in guide


def test_removal_only_diff_is_treated_breaking(ws):
    # No .NET [BREAKING] note, but a removal in the diff ⇒ conservative breaking.
    notes = "## Changes:\n* c1 .NET: tidy up internals\n"
    diff = "- Member 'SomethingGone' was removed\n"
    guide = _run(ws, "1.12.0", "1.12.1", notes, diff)
    assert "TODO: Review every package evidence block" in guide


def test_potentially_breaking_summary_is_visible_in_guide(ws):
    notes = "## Changes:\n* c1 .NET: adjust inheritance\n"
    diff = "- Base type changed from 'OldBase' to 'NewBase'\n"
    guide = _run(ws, "1.12.0", "1.12.1", notes, diff)
    assert "`1` potentially-breaking API change row(s)" in guide
    assert "TODO: Review every package evidence block" in guide


def test_malicious_member_name_is_dropped_from_patterns(ws):
    # A crafted "member name" with an HTML comment must not reach the unfenced
    # New Patterns bullets; the safe one still appears.
    notes = "## Changes:\n* d1 .NET: additive only\n"
    diff = "- Member 'GoodName' was added\n- Member '<!--evil-->payload' was added\n**Summary:** 0 breaking, 2 additive\n"
    guide = _run(ws, "1.12.0", "1.13.0", notes, diff)

    assert "- `GoodName`" in guide
    assert "evil" not in guide                     # the payload never lands
    assert "omitted — names failed the identifier safety check" in guide
