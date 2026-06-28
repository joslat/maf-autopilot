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


# --- keep-breaking-red gate: unfilled drafts for the current release ---------

def _write_registry(tmp_path, body: str) -> Path:
    reg = tmp_path / "registry.yaml"
    reg.write_text(body, encoding="utf-8")
    return reg


def test_unfilled_current_draft_is_flagged(tmp_path):
    reg = _write_registry(tmp_path, """
entries:
  - id: MAF120-FOO-001
    version_introduced: "1.12.0"
    obsolete_signature: TODO
    replacement_signature: TODO
    fix_description: TODO — review the diff entry below
""")
    findings = v.check_unfilled_current_drafts(reg, "1.12.0")
    assert len(findings) == 1
    assert "MAF120-FOO-001" in findings[0]


def test_filled_current_entry_passes(tmp_path):
    reg = _write_registry(tmp_path, """
entries:
  - id: MAF120-FOO-001
    version_introduced: "1.12.0"
    obsolete_signature: Foo.OldMethod()
    replacement_signature: Foo.NewMethod()
    fix_description: Rename OldMethod to NewMethod; the signature is unchanged.
    example_before: |
      foo.OldMethod();
    example_after: |
      foo.NewMethod();
""")
    assert v.check_unfilled_current_drafts(reg, "1.12.0") == []


def test_missing_examples_is_flagged(tmp_path):
    # fix_description filled, but no example_before/after at all -> incomplete.
    reg = _write_registry(tmp_path, """
entries:
  - id: MAF120-FOO-001
    version_introduced: "1.12.0"
    obsolete_signature: Foo.Bar()
    replacement_signature: Foo.Baz()
    fix_description: Rename Bar to Baz everywhere; same semantics.
""")
    assert len(v.check_unfilled_current_drafts(reg, "1.12.0")) == 1


def test_unfilled_signature_is_flagged(tmp_path):
    # Prose + examples filled, but a signature is still TODO -> incomplete draft.
    reg = _write_registry(tmp_path, """
entries:
  - id: MAF120-FOO-001
    version_introduced: "1.12.0"
    obsolete_signature: TODO
    replacement_signature: Foo.Baz()
    fix_description: Rename Bar to Baz everywhere; same semantics.
    example_before: |
      foo.Bar();
    example_after: |
      foo.Baz();
""")
    assert len(v.check_unfilled_current_drafts(reg, "1.12.0")) == 1


def test_signature_change_draft_with_filled_sigs_is_flagged(tmp_path):
    # The extractor auto-fills BOTH signatures for a SignatureChanged entry, so an
    # all-three-AND gate would miss it. The strengthened gate fires on the still-
    # TODO fix_description.
    reg = _write_registry(tmp_path, """
entries:
  - id: MAF120-FOO-001
    version_introduced: "1.12.0"
    obsolete_signature: Foo.Bar(int)
    replacement_signature: Foo.Bar(int, bool)
    fix_description: TODO — review the diff entry below and document the canonical fix
""")
    assert len(v.check_unfilled_current_drafts(reg, "1.12.0")) == 1


def test_todo_example_stub_is_flagged(tmp_path):
    # fix_description filled, but the example is still the `// TODO` stub.
    reg = _write_registry(tmp_path, """
entries:
  - id: MAF120-FOO-001
    version_introduced: "1.12.0"
    obsolete_signature: Foo.Bar()
    replacement_signature: Foo.Baz()
    fix_description: Rename Bar to Baz; same semantics, just a new name everywhere.
    example_before: |
      // TODO — show a real call site that breaks
    example_after: |
      var x = foo.Baz();
""")
    assert len(v.check_unfilled_current_drafts(reg, "1.12.0")) == 1


def test_old_cycle_unfilled_draft_is_ignored(tmp_path):
    # An unfilled draft from an OLDER release must not block a newer PR.
    reg = _write_registry(tmp_path, """
entries:
  - id: MAF150-BAR-001
    version_introduced: "1.5.0"
    obsolete_signature: TODO
    replacement_signature: TODO
    fix_description: TODO
""")
    assert v.check_unfilled_current_drafts(reg, "1.12.0") == []


def test_additive_release_no_entries_passes(tmp_path):
    assert v.check_unfilled_current_drafts(_write_registry(tmp_path, "entries: []\n"), "1.12.0") == []


def test_looks_unfilled_classifier():
    assert v._looks_unfilled(None)
    assert v._looks_unfilled("")
    assert v._looks_unfilled("TODO")
    assert v._looks_unfilled("TODO — fill me")
    assert not v._looks_unfilled("Foo.Bar()")
