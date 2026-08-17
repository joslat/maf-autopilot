"""Focused contracts for rendering the release Coding Agent issue body."""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

import pytest


SCRIPT = Path(__file__).resolve().parents[1] / "build_ai_fill_issue_body.py"
WORKFLOW = (
    Path(__file__).resolve().parents[2] / "workflows" / "maf-ai-fill-todos.yml"
).read_text(encoding="utf-8")
sys.path.insert(0, str(SCRIPT.parent))

from build_ai_fill_issue_body import registry_id_segment  # noqa: E402


@pytest.mark.parametrize(
    ("version", "expected"),
    [
        ("1.14.0", "114"),
        ("1.14.7", "114"),
        ("1.14.0-preview.1", "114"),
        ("2.3.0", "23"),
    ],
)
def test_registry_id_segment_uses_major_and_minor_only(version: str, expected: str):
    assert registry_id_segment(version) == expected


def test_registry_id_segment_rejects_non_semver_input():
    with pytest.raises(ValueError, match="semantic version"):
        registry_id_segment("1.14")


def test_renderer_uses_maf114_for_1_14_0_without_digits_env():
    env = os.environ.copy()
    env["TARGET"] = "1.14.0"
    env.pop("DIGITS", None)
    env.pop("BRANCH", None)

    result = subprocess.run(
        [sys.executable, str(SCRIPT)],
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=env,
    )

    assert result.returncode == 0, result.stderr
    assert "MAF114-REVIEW-*" in result.stdout
    assert "MAF114-HARNESS-..." in result.stdout
    assert "MAF1140" not in result.stdout
    assert "release-watcher/maf-1.14.0" in result.stdout


def test_workflow_delegates_id_derivation_to_the_renderer():
    assert "build_ai_fill_issue_body.py" in WORKFLOW
    assert "version_digits" not in WORKFLOW
    assert "DIGITS=" not in WORKFLOW
    assert "DIGITS:" not in WORKFLOW
