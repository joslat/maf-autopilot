"""Integration tests for the green-on-arrival compat-matrix pipeline:
update_compat_matrix.py (real pins + tracking line + date + verdict) followed by
sync_compat_tool.py (code-matrix row). Run as subprocesses against a temp
workspace, mirroring how the watcher chains them.
"""
from __future__ import annotations

import os
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
DIFF_ADDITIVE = "- Member 'NewThing' was added\n- Member 'OtherThing' was added\n"

NOTES_BREAKING = """## Changes:
* b1 .NET: [BREAKING] Make all AgentSkillsProvider tools require approval by default (#6729)
"""
DIFF_BREAKING = "- Member 'UseScriptApproval' was removed\n"


def _run(script: str, ws: Path, old: str, new: str):
    env = os.environ.copy()
    env['OLD_VERSION'] = old
    env['NEW_VERSION'] = new
    r = subprocess.run(
        [sys.executable, str(SCRIPTS / script)],
        cwd=str(ws), env=env, capture_output=True, text=True,
    )
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


def _seed_inputs(ws: Path, notes: str, diff: str):
    (ws / "release-notes.txt").write_text(notes, encoding="utf-8")
    (ws / "diff-core.txt").write_text(diff, encoding="utf-8")


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
    (w / "release-notes.txt").write_text("## Changes:\n* .NET: Add CreateMcpTool overload\n", encoding="utf-8")
    (w / "diff-core.txt").write_text("- Member 'NewThing' was added\n", encoding="utf-8")

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
    diff = "- Member 'GoodName' was added\n- Member 'Evil\"\"\"; class Pwn {}' was added\n"
    _seed_inputs(ws, notes, diff)
    _run("update_compat_matrix.py", ws, "1.11.0", "1.12.0")
    _run("sync_compat_tool.py", ws, "1.11.0", "1.12.0")
    matrix = (ws / "docs" / "compatibility-matrix.md").read_text(encoding="utf-8")
    tool = (ws / "src" / "maf-autopilot" / "Tools" / "CompatibilityTool.cs").read_text(encoding="utf-8")
    assert "GoodName" in matrix                       # the safe member survives
    assert "Pwn" not in matrix and "Pwn" not in tool  # the payload is dropped


def test_neutralize_defangs_triple_quote_and_newlines():
    import sync_compat_tool as s  # safe: main() is __main__-guarded
    # A single replace('"""', ...) leaves a surviving `"""` for runs of 5/8/11;
    # the regex form must defang EVERY run length.
    for n in range(2, 9):
        assert '"""' not in s._neutralize('a' + ('"' * n) + 'b'), f"run of {n} survived"
    assert s._neutralize('x\r\ny') == 'x  y'
