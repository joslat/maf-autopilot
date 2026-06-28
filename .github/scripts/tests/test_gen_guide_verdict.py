"""Tests for gen_guide_section.py's green-on-arrival verdict: additive releases
get filled analysis sections; breaking releases keep TODO stubs; untrusted
member names are sanitized out of the unfenced New Patterns bullets.
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parents[1] / "gen_guide_section.py"


def _run(ws: Path, old: str, new: str, notes: str, diff: str) -> str:
    (ws / "release-notes.txt").write_text(notes, encoding="utf-8")
    (ws / "diff-core.txt").write_text(diff, encoding="utf-8")
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
    diff = "- Member 'NewThing' was added\n- Member 'OtherThing' was added\n"
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

    assert "TODO: Review the diff above and list breaking changes here" in guide
    assert "this is an **additive** release" not in guide


def test_removal_only_diff_is_treated_breaking(ws):
    # No .NET [BREAKING] note, but a removal in the diff ⇒ conservative breaking.
    notes = "## Changes:\n* c1 .NET: tidy up internals\n"
    diff = "- Member 'SomethingGone' was removed\n"
    guide = _run(ws, "1.12.0", "1.12.1", notes, diff)
    assert "TODO: Review the diff above" in guide


def test_malicious_member_name_is_dropped_from_patterns(ws):
    # A crafted "member name" with an HTML comment must not reach the unfenced
    # New Patterns bullets; the safe one still appears.
    notes = "## Changes:\n* d1 .NET: additive only\n"
    diff = "- Member 'GoodName' was added\n- Member '<!--evil-->payload' was added\n"
    guide = _run(ws, "1.12.0", "1.13.0", notes, diff)

    assert "- `GoodName`" in guide
    assert "evil" not in guide                     # the payload never lands
    assert "omitted — names failed the identifier safety check" in guide
