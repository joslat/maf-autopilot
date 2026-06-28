"""Tests for ensure_breaking_gated.py — the guard that guarantees a BREAKING
release leaves the watcher PR red even when registry-extract produced no entries.
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

import verify_crossfile_consistency as vc  # noqa: E402

# A block-style registry with one old, filled entry (mirrors the real file shape,
# so appending a sentinel `- id:` extends the list).
OLD_ONLY = """entries:
  - id: MAF100-OLD-001
    version_introduced: "1.0.0"
    obsolete_signature: Foo.Old()
    replacement_signature: Foo.New()
    fix_description: Rename Old to New; same semantics.
"""

HAS_CURRENT = """entries:
  - id: MAF120-FOO-001
    version_introduced: "1.12.0"
    obsolete_signature: Foo.Bar()
    replacement_signature: Foo.Baz()
    fix_description: TODO — fill me
"""


def _reg(ws: Path, body: str) -> Path:
    d = ws / ".github" / "skills" / "maf-obsolete-api-registry"
    d.mkdir(parents=True, exist_ok=True)
    reg = d / "registry.yaml"
    reg.write_text(body, encoding="utf-8")
    return reg


def _run(ws: Path, verdict: str, new: str):
    env = os.environ.copy()
    env["VERDICT"] = verdict
    env["NEW_VERSION"] = new
    r = subprocess.run(
        [sys.executable, str(SCRIPTS / "ensure_breaking_gated.py")],
        cwd=str(ws), env=env, capture_output=True, text=True,
    )
    assert r.returncode == 0, f"{r.stdout}\n{r.stderr}"
    return r


def test_breaking_no_entries_appends_sentinel_that_gate_flags(tmp_path):
    reg = _reg(tmp_path, OLD_ONLY)
    _run(tmp_path, "breaking", "1.12.0")
    # The sentinel must be valid YAML AND make the keep-breaking-red gate fire.
    findings = vc.check_unfilled_current_drafts(reg, "1.12.0")
    assert len(findings) == 1
    assert "SENTINEL" in findings[0]
    # The old 1.0.0 entry is untouched (still parseable, not flagged for 1.12.0).
    assert "MAF100-OLD-001" in reg.read_text(encoding="utf-8")


def test_breaking_with_existing_entry_is_noop(tmp_path):
    reg = _reg(tmp_path, HAS_CURRENT)
    before = reg.read_text(encoding="utf-8")
    _run(tmp_path, "breaking", "1.12.0")
    assert reg.read_text(encoding="utf-8") == before  # no sentinel appended


def test_additive_verdict_is_noop(tmp_path):
    reg = _reg(tmp_path, OLD_ONLY)
    before = reg.read_text(encoding="utf-8")
    _run(tmp_path, "additive", "1.12.0")
    assert reg.read_text(encoding="utf-8") == before
