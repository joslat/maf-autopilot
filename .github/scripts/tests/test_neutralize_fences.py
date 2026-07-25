"""Tests for .github/scripts/neutralize_fences.py (SEC-18).

The neutralizer breaks triple-backtick runs in captured tool output so they can't
close an enclosing ```-fence in a PR comment. Run from repo root:
    pytest .github/scripts/tests/test_neutralize_fences.py
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "neutralize_fences.py"
_ZWSP = chr(0x200B)


def _run(text: str) -> str:
    result = subprocess.run(
        [sys.executable, str(SCRIPT)],
        input=text.encode("utf-8"),
        capture_output=True,
    )
    assert result.returncode == 0, result.stderr
    return result.stdout.decode("utf-8")


def test_triple_backtick_is_broken_with_zwsp():
    out = _run("safe\n```\nevil\n```\nend\n")
    # No bare triple-backtick line survives (each ``` gains zero-width spaces).
    assert "\n```\n" not in out
    assert _ZWSP in out
    # The visible characters (minus the ZWSP) are unchanged for a human reader.
    assert out.replace(_ZWSP, "") == "safe\n```\nevil\n```\nend\n"


def test_no_backticks_passes_through_unchanged():
    assert _run("just some text\nno fences here\n") == "just some text\nno fences here\n"


def test_inline_single_backticks_untouched():
    # Only triple runs are neutralized; a lone `code` span is preserved.
    assert _run("use `x` here") == "use `x` here"
