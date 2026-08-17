"""Tests for dedupe_registry_entries.py (release-watcher idempotent append)."""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

SCRIPT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPT_DIR))

from dedupe_registry_entries import dedupe, existing_ids, main, split_blocks  # noqa: E402

REGISTRY = """\
entries:
  - id: MAF100-foo
    package: Microsoft.Agents.AI
  - id: MAF101-bar
    package: Microsoft.Agents.AI
"""

NEW_ENTRIES = """\
# === Draft entries auto-appended ===
  - id: MAF100-foo
    package: Microsoft.Agents.AI
    note: duplicate of an existing entry
  - id: MAF200-baz
    package: Microsoft.Agents.AI.Workflows
    note: genuinely new
"""


def test_existing_ids_parsed():
    assert existing_ids(REGISTRY) == {"MAF100-foo", "MAF101-bar"}


def test_split_blocks_groups_by_id():
    blocks = split_blocks(NEW_ENTRIES)
    assert [b[0] for b in blocks] == ["MAF100-foo", "MAF200-baz"]
    # The leading comment line is dropped (before the first `- id:`).
    assert "Draft entries auto-appended" not in blocks[0][1]


def test_dedupe_core_entries_drop_following_no_entry_package_message():
    concatenated = """\
# Draft registry entries from Microsoft.Agents.AI
  - id: MAF114-AGENT-001
    package: Microsoft.Agents.AI
    notes: first Core entry
  - id: MAF114-AGENT-002
    package: Microsoft.Agents.AI
    notes: second Core entry

# No new draft registry entries — diff contained no breaking-change candidates.
"""

    assert dedupe("entries:\n", concatenated) == """\
  - id: MAF114-AGENT-001
    package: Microsoft.Agents.AI
    notes: first Core entry
  - id: MAF114-AGENT-002
    package: Microsoft.Agents.AI
    notes: second Core entry
"""


def test_dedupe_drops_existing_keeps_new():
    out = dedupe(REGISTRY, NEW_ENTRIES)
    assert "MAF200-baz" in out
    assert "MAF100-foo" not in out
    assert "genuinely new" in out


def test_identical_duplicate_within_batch_is_idempotent():
    entry = """\
  - id: MAF200-baz
    package: Microsoft.Agents.AI.Workflows
    type: HarnessOptions
    method: Legacy
"""
    dupe_batch = entry + entry
    out = dedupe(REGISTRY, dupe_batch)
    assert out.count("MAF200-baz") == 1


def test_same_id_with_different_package_identity_fails_closed():
    collision = """\
  - id: MAF114-OPTIONS-001
    package: Microsoft.Agents.AI
    type: ChatClientAgentOptions
    method: Legacy
  - id: MAF114-OPTIONS-001
    package: Microsoft.Agents.AI.Hosting
    type: HarnessOptions
    method: Legacy
"""

    with pytest.raises(ValueError, match="id collision"):
        dedupe("entries:\n", collision)


def test_cli_collision_returns_failure_without_partial_output(tmp_path, capsys):
    registry = tmp_path / "registry.yaml"
    entries = tmp_path / "entries.yaml"
    registry.write_text("entries:\n", encoding="utf-8")
    entries.write_text("""\
  - id: MAF114-OPTIONS-001
    package: Microsoft.Agents.AI
    type: ChatClientAgentOptions
    method: Legacy
  - id: maf114-options-001
    package: Microsoft.Agents.AI.Hosting
    type: HarnessOptions
    method: Legacy
""", encoding="utf-8")

    assert main(["dedupe_registry_entries.py", str(registry), str(entries)]) == 1
    captured = capsys.readouterr()
    assert "id collision" in captured.err
    assert captured.out == ""


def test_all_duplicates_returns_empty():
    only_dupes = (
        "  - id: MAF100-foo\n    package: Microsoft.Agents.AI\n"
        "  - id: MAF101-bar\n    package: Microsoft.Agents.AI\n")
    assert dedupe(REGISTRY, only_dupes) == ""


def test_existing_same_identity_rerun_allows_human_filled_content():
    registry = """\
entries:
  - id: MAF114-HARNESS-OPTIONS-001
    package: Microsoft.Agents.AI.Hosting
    type: HarnessOptions
    method: Legacy
    fix_description: Human-reviewed migration guidance.
"""
    rerun = """\
  - id: maf114-harness-options-001
    package: Microsoft.Agents.AI.Hosting
    type: HarnessOptions
    method: Legacy
    fix_description: TODO
"""

    assert dedupe(registry, rerun) == ""


def test_existing_same_id_with_different_identity_fails_closed():
    registry = """\
entries:
  - id: MAF114-OPTIONS-001
    package: Microsoft.Agents.AI
    type: ChatClientAgentOptions
    method: Legacy
"""
    collision = """\
  - id: MAF114-OPTIONS-001
    package: Microsoft.Agents.AI.Hosting
    type: HarnessOptions
    method: Legacy
"""

    with pytest.raises(ValueError, match="collision with existing registry"):
        dedupe(registry, collision)
