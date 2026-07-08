"""Cross-language lock for the registry placeholder-token grammar.

placeholder-fixtures.json is the single source of truth: this Python test AND a
C# test (VerifyRegistryCommandTests) both assert their predicate against the same
table, so the two gates can never silently drift (the historical bug: 'TODO —'
with an em-dash passed C# but failed Python).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import verify_crossfile_consistency as v  # noqa: E402

FIXTURES = (Path(__file__).resolve().parents[3]
            / ".github" / "skills" / "maf-obsolete-api-registry" / "placeholder-fixtures.json")


def test_is_placeholder_token_matches_shared_fixture():
    data = json.loads(FIXTURES.read_text(encoding="utf-8"))
    assert data["cases"], "fixture has no cases"
    for c in data["cases"]:
        got = v.is_placeholder_token(c["value"])
        assert got == c["placeholder"], (
            f"is_placeholder_token({c['value']!r}) == {got}, expected {c['placeholder']} "
            f"(per placeholder-fixtures.json)")


def test_em_dash_todo_is_a_placeholder():
    # The specific historical divergence — must be True on both sides now.
    assert v.is_placeholder_token("TODO — review the diff entry below") is True
