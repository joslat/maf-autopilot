"""Tests for verify_crossfile_consistency.py helpers (tasks 4.2 / 4.4)."""
from __future__ import annotations

import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPT_DIR))

import verify_crossfile_consistency as v  # noqa: E402


# --- Task 4.2: union across all per-version guides, not just 1.3.0 -----------

def test_collect_all_guide_sections_unions_versions(tmp_path):
    guides = tmp_path / "guides"
    guides.mkdir()
    (guides / "maf-1.3.0-migration-guide.md").write_text("## 1. Intro\n## 2. Foo\n", encoding="utf-8")
    (guides / "maf-1.6.1-migration-guide.md").write_text("## 7. NewThing\n", encoding="utf-8")
    # The cumulative file is excluded (regenerated superset).
    (guides / "maf-current-migration-guide.md").write_text("## 99. Cumulative\n", encoding="utf-8")

    sections = v.collect_all_guide_sections(guides)
    assert {"1", "2", "7"} <= sections   # the 4.2 fix: 1.6.1's "7" is now valid
    assert "99" not in sections          # cumulative file skipped


def test_collect_all_guide_sections_missing_dir():
    assert v.collect_all_guide_sections(Path("/no/such/dir")) == set()


# --- Task 4.4: detect unreplaced watcher placeholders in the top row ---------

def test_placeholder_row_is_flagged():
    row = ("| **1.7.0** | `>= unknown` | `>= 8.0` | `>= unknown` | `1.7.0` | "
           "Auto-detected — verify versions in PR review. |")
    leftover = v.unreplaced_placeholders(v.top_row_line(row))
    assert ">= unknown" in leftover
    assert "Auto-detected — verify" in leftover


def test_clean_row_has_no_placeholders():
    row = ("| **1.7.0** | `>= 9.7.0` | `>= 8.0` | `>= 2.1.0` | `1.7.0` | "
           "Verified against the 1.7.0 release. |")
    assert v.unreplaced_placeholders(v.top_row_line(row)) == []


def test_top_row_line_returns_first_version_row():
    text = "| Version | X |\n|---|---|\n| **1.7.0** | a |\n| **1.6.0** | b |\n"
    assert "1.7.0" in v.top_row_line(text)


def test_top_row_line_none_when_no_rows():
    assert v.top_row_line("no version rows here") is None
    assert v.unreplaced_placeholders(None) == []
