"""Integration tests for the green-on-arrival compat-matrix pipeline:
update_compat_matrix.py (real pins + tracking line + date + verdict) followed by
sync_compat_tool.py (code-matrix row). Run as subprocesses against a temp
workspace, mirroring how the watcher chains them.
"""
from __future__ import annotations

import os
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

MATRIX_SEED = """# MAF Compatibility Matrix

<!-- auto-updated-by: maf-release-watcher | last-updated: 2020-01-01 -->

## Compatibility Table

| MAF Version | Microsoft.Extensions.AI | .NET | Azure.AI.OpenAI | Generators Package | Notes |
|-------------|------------------------|------|-----------------|--------------------|-------|
| **1.11.0** | `≥ 10.6.0` | `≥ 8.0` | _(not pinned by MAF — BYO via IChatClient)_ | `1.11.0` | Prior row. |

## Version Tracking

Current tracked version: **`1.11.0`** (see `.maf-version`)
"""

TOOL_SEED = '''using System.ComponentModel;

namespace MafDoctor.Tools;

public sealed class CompatibilityTool
{
    internal static readonly IReadOnlyDictionary<string, string> Matrix =
        new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1.11.0"] = """
                ## MAF 1.11.0 Compatibility

                | Dependency | Version | Notes |
                |------------|---------|-------|
                | .NET runtime | `>= 8.0` | |

                Prior row.
                """,
        };
}
'''

NOTES_ADDITIVE = """## Changes:
* a1 .NET: Add CreateMcpTool projectConnectionId overload (#6703)
* a2 Python: [BREAKING] python-only change (#1)
"""
DIFF_ADDITIVE = "- Member 'NewThing' was added\n- Member 'OtherThing' was added\n**Summary:** 0 breaking, 2 additive\n"

NOTES_BREAKING = """## Changes:
* b1 .NET: [BREAKING] Make all AgentSkillsProvider tools require approval by default (#6729)
"""
DIFF_BREAKING = "- Member 'UseScriptApproval' was removed\n"


def _invoke(script: str, ws: Path, old: str, new: str):
    env = os.environ.copy()
    env['OLD_VERSION'] = old
    env['NEW_VERSION'] = new
    return subprocess.run(
        [sys.executable, str(SCRIPTS / script)],
        cwd=str(ws), env=env, capture_output=True, text=True,
    )


def _run(script: str, ws: Path, old: str, new: str):
    r = _invoke(script, ws, old, new)
    assert r.returncode == 0, f"{script} failed:\n{r.stdout}\n{r.stderr}"
    return r


@pytest.fixture
def ws():
    d = Path(tempfile.mkdtemp(prefix="maf-green-test-"))
    try:
        (d / "docs").mkdir()
        (d / "docs" / "compatibility-matrix.md").write_text(MATRIX_SEED, encoding="utf-8")
        (d / "src" / "maf-autopilot" / "Tools").mkdir(parents=True)
        (d / "src" / "maf-autopilot" / "Tools" / "CompatibilityTool.cs").write_text(TOOL_SEED, encoding="utf-8")
        yield d
    finally:
        shutil.rmtree(d, ignore_errors=True)


def _seed_inputs(
    ws: Path,
    notes: str,
    diff: str,
    old: str = "1.11.0",
    new: str = "1.12.0",
):
    (ws / "release-notes.txt").write_text(notes, encoding="utf-8")
    package = "Microsoft.Agents.AI"
    rows = [line for line in diff.splitlines() if line.startswith("- ")]
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
    complete = (
        f"# API Diff: {package}\n\n"
        "| Field | Value |\n| ----- | ----- |\n"
        f"| Versions | **{old}** -> **{new}** |\n"
        f"| Summary | **Summary:** {', '.join(counts)} across 1 type |\n\n"
        + "\n\n".join(sections)
        + "\n"
    )
    (ws / "diff-core.txt").write_text(complete, encoding="utf-8")
    (ws / "diff-core.stderr.txt").write_text("", encoding="utf-8")
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
    (ws / "maf-package-plan.json").write_text(json.dumps(plan), encoding="utf-8")
    (ws / "maf-diff-results.json").write_text(json.dumps(results), encoding="utf-8")


def test_additive_row_is_filled_no_placeholders(ws):
    _seed_inputs(ws, NOTES_ADDITIVE, DIFF_ADDITIVE)
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    matrix = (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8")

    assert "| **1.12.0** |" in matrix
    assert ">= unknown" not in matrix and "Auto-detected" not in matrix
    assert "`≥ 10.6.0`" in matrix          # carried forward
    assert "| `1.12.0` |" in matrix        # generators column
    assert "Additive release" in matrix
    assert "Current tracked version: **`1.12.0`**" in matrix
    assert "last-updated: 2020-01-01" not in matrix  # date advanced


def test_breaking_row_is_marked_for_review(ws):
    _seed_inputs(ws, NOTES_BREAKING, DIFF_BREAKING)
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    matrix = (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8")
    assert "| **1.12.0** |" in matrix
    assert "**Breaking** — review required" in matrix
    assert "UseScriptApproval" in matrix  # removal surfaced in the note


def test_potentially_breaking_summary_is_visible_in_matrix(ws):
    _seed_inputs(
        ws,
        "## Changes:\n* .NET: adjust inheritance\n",
        "- Base type changed from 'OldBase' to 'NewBase'\n",
    )
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    matrix = (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8")
    assert "**Breaking** — review required" in matrix
    assert "API summary: 1 potentially breaking" in matrix


def test_sync_adds_code_matrix_entry(ws):
    _seed_inputs(ws, NOTES_ADDITIVE, DIFF_ADDITIVE)
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    _run("sync_compat_tool.py", ws, "1.11.0", "1.12.0")
    tool = (ws / "src" / "maf-autopilot" / "Tools" / "CompatibilityTool.cs").read_text(encoding="utf-8")
    assert '["1.12.0"] = """' in tool
    assert "## MAF 1.12.0 Compatibility" in tool
    # New entry inserted ABOVE the prior one (newest-first).
    assert tool.index('["1.12.0"]') < tool.index('["1.11.0"]')
    # Generators cell must not be double-backticked (the parsed cell already
    # carries its backticks).
    assert "``" not in tool


def test_pipeline_is_idempotent(ws):
    _seed_inputs(ws, NOTES_ADDITIVE, DIFF_ADDITIVE)
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    _run("sync_compat_tool.py", ws, "1.11.0", "1.12.0")
    first_matrix = (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8")
    first_tool = (ws / "src" / "maf-autopilot" / "Tools" / "CompatibilityTool.cs").read_text(encoding="utf-8")
    # Re-run: both must be no-ops (no duplicate rows / entries).
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    _run("sync_compat_tool.py", ws, "1.11.0", "1.12.0")
    assert (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8") == first_matrix
    assert (ws / "src" / "maf-autopilot" / "Tools" / "CompatibilityTool.cs").read_text(encoding="utf-8") == first_tool
    assert first_matrix.count("| **1.12.0** |") == 1


def test_existing_row_repairs_single_stale_metadata_without_duplication(ws):
    _seed_inputs(ws, NOTES_ADDITIVE, DIFF_ADDITIVE)
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    matrix_path = ws / "docs" / "compatibility-matrix.md"
    stale = matrix_path.read_text(encoding="utf-8").replace(
        "Current tracked version: **`1.12.0`**",
        "Current tracked version: **`1.11.0`**",
    )
    stale = re.sub(
        r"last-updated: \d{4}-\d{2}-\d{2}",
        "last-updated: 2020-01-01",
        stale,
    )
    matrix_path.write_text(stale, encoding="utf-8")

    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    repaired = matrix_path.read_text(encoding="utf-8")

    assert repaired.count("| **1.12.0** |") == 1
    assert repaired.count("Current tracked version: **`1.12.0`**") == 1
    assert "last-updated: 2020-01-01" not in repaired


@pytest.mark.parametrize(
    ("metadata_case", "expected"),
    [
        ("missing-tracked", "0 valid value(s) across 0 field(s)"),
        ("duplicate-tracked", "2 valid value(s) across 2 field(s)"),
        ("malformed-extra-tracked", "1 valid value(s) across 2 field(s)"),
        ("missing-date", "0 valid value(s) across 0 field(s)"),
        ("duplicate-date", "2 valid value(s) across 2 field(s)"),
        ("malformed-extra-date", "1 valid value(s) across 2 field(s)"),
    ],
)
def test_matrix_update_fails_closed_when_metadata_is_not_singular(
    ws, metadata_case, expected
):
    matrix_path = ws / "docs" / "compatibility-matrix.md"
    matrix = matrix_path.read_text(encoding="utf-8")
    tracking = "Current tracked version: **`1.11.0`** (see `.maf-version`)"
    date = "last-updated: 2020-01-01"
    if metadata_case == "missing-tracked":
        matrix = matrix.replace(tracking, "")
    elif metadata_case == "duplicate-tracked":
        matrix = matrix.replace(tracking, f"{tracking}\n{tracking}")
    elif metadata_case == "malformed-extra-tracked":
        matrix = matrix.replace(
            tracking, f"{tracking}\nCurrent tracked version: **`unknown`**"
        )
    elif metadata_case == "missing-date":
        matrix = matrix.replace(date, "updated-on: 2020-01-01")
    elif metadata_case == "duplicate-date":
        matrix = matrix.replace(date, f"{date}\n<!-- {date} -->")
    else:
        matrix = matrix.replace(date, f"{date}\n<!-- last-updated: yesterday -->")
    matrix_path.write_text(matrix, encoding="utf-8")
    before = matrix_path.read_text(encoding="utf-8")

    result = _invoke("update_compat_matrix.py", ws, "1.11.0", "1.12.0")

    assert result.returncode == 1
    assert "::error::update_compat_matrix.py cannot update release metadata" in result.stdout
    assert expected in result.stdout
    assert matrix_path.read_text(encoding="utf-8") == before
    assert "| **1.12.0** |" not in before


def test_end_to_end_additive_pipeline_is_green(tmp_path):
    # The actual green-on-arrival promise: run the full additive chain
    # (matrix -> code sync -> guide), set .maf-version, then assert
    # verify_crossfile_consistency.py exits 0.
    w = tmp_path
    (w / "docs").mkdir()
    (w / "docs" / "compatibility-matrix.md").write_text(MATRIX_SEED, encoding="utf-8")
    (w / "src" / "maf-autopilot" / "Tools").mkdir(parents=True)
    (w / "src" / "maf-autopilot" / "Tools" / "CompatibilityTool.cs").write_text(TOOL_SEED, encoding="utf-8")
    (w / "guides").mkdir()
    (w / "guides" / "maf-1.3.0-migration-guide.md").write_text("## 1. Intro\n", encoding="utf-8")
    skills = w / ".github" / "skills" / "maf-obsolete-api-registry"
    skills.mkdir(parents=True)
    (skills / "registry.yaml").write_text("entries: []\n", encoding="utf-8")
    _seed_inputs(
        w,
        "## Changes:\n* .NET: Add CreateMcpTool overload\n",
        "- Member 'NewThing' was added\n",
    )

    _run("update_compat_matrix.py", w, "1.11.0", "1.12.0")
    _run("sync_compat_tool.py", w, "1.11.0", "1.12.0")
    _run("gen_guide_section.py", w, "1.11.0", "1.12.0")
    (w / ".maf-version").write_text("1.12.0\n", encoding="utf-8")

    r = subprocess.run(
        [sys.executable, str(SCRIPTS / "verify_crossfile_consistency.py")],
        cwd=str(w), capture_output=True, text=True,
    )
    assert r.returncode == 0, f"cross-file gate not green:\n{r.stdout}\n{r.stderr}"


def test_untrusted_member_name_with_quotes_is_dropped(ws):
    # A crafted "member name" carrying a C# raw-string breakout must never reach
    # the matrix note OR the generated CompatibilityTool.cs source.
    notes = "## Changes:\n* x .NET: additive only\n"
    diff = "- Member 'GoodName' was added\n- Member 'Evil\"\"\"; class Pwn {}' was added\n**Summary:** 0 breaking, 2 additive\n"
    _seed_inputs(ws, notes, diff)
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    _run("sync_compat_tool.py", ws, "1.11.0", "1.12.0")
    matrix = (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8")
    tool = (ws / "src" / "maf-autopilot" / "Tools" / "CompatibilityTool.cs").read_text(encoding="utf-8")
    assert "GoodName" in matrix                       # the safe member survives
    assert "Pwn" not in matrix and "Pwn" not in tool  # the payload is dropped


def test_matrix_note_summarizes_informational_externalization(ws):
    old, new = "1.16.0", "1.17.0"
    _seed_inputs(
        ws,
        "## Changes:\n* .NET: routine release\n",
        "- Member 'NewThing' was added\n",
        old,
        new,
    )
    plan_path = ws / "maf-package-plan.json"
    results_path = ws / "maf-diff-results.json"
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    results = json.loads(results_path.read_text(encoding="utf-8"))
    durable = {
        "order": 1,
        "slug": "durable",
        "package": "Microsoft.Agents.AI.DurableTask",
        "id_scope": "DURABLE",
        "status": "informational",
        "old_package_version": "1.16.0-preview.1",
        "new_package_version": None,
        "diff_artifact": "diff-durable.txt",
        "stderr_artifact": "diff-durable.stderr.txt",
        "reason": "ownership moved",
    }
    plan["surfaces"].append(durable)
    plan["lifecycle_events"] = [
        {
            "id": "maf-1.17.0-durable-externalization",
            "release_version": new,
            "kind": "repository_externalization",
            "impact": "informational",
            "related_surfaces": ["durable"],
            "source_packages": [
                {"package": durable["package"], "version": durable["old_package_version"]}
            ],
            "target_packages": [{"package": durable["package"]}],
            "source_repository": "microsoft/agent-framework",
            "target_repository": "microsoft/agent-framework-durable-extension",
            "same_package_ids": True,
            "reason": "ownership moved while package IDs stayed stable",
        }
    ]
    results["surfaces"].append(
        {
            "order": 1,
            "slug": "durable",
            "package": durable["package"],
            "status": "informational",
            "diff_artifact": durable["diff_artifact"],
            "stderr_artifact": durable["stderr_artifact"],
            "attempted": False,
            "tool_exit_code": None,
            "validation_status": "not_applicable",
            "validation_errors": [],
        }
    )
    plan_path.write_text(json.dumps(plan), encoding="utf-8")
    results_path.write_text(json.dumps(results), encoding="utf-8")
    (ws / "diff-durable.txt").write_text("", encoding="utf-8")
    (ws / "diff-durable.stderr.txt").write_text("informational\n", encoding="utf-8")

    _run("update_compat_matrix.py", ws, old, new)
    matrix = (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8")
    assert "Informational: repository externalization" in matrix
    assert "microsoft/agent-framework-durable-extension" in matrix
    assert "package IDs unchanged" in matrix


def test_neutralize_defangs_triple_quote_and_newlines():
    import sync_compat_tool as s  # safe: main() is __main__-guarded
    # A single replace('"""', ...) leaves a surviving `"""` for runs of 5/8/11;
    # the regex form must defang EVERY run length.
    for n in range(2, 9):
        assert '"""' not in s._neutralize('a' + ('"' * n) + 'b'), f"run of {n} survived"
    assert s._neutralize('x\r\ny') == 'x  y'
