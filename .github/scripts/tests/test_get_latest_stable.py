"""Unit tests for get_latest_stable.py (release-watcher NuGet version filter).

Covers the cases the inline `python3 -c` version once handled, plus the edge
cases that the scheduled path depends on: ascending-order selection, prerelease
filtering, prerelease-only, empty, and malformed input.
"""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parents[1] / "get_latest_stable.py"
sys.path.insert(0, str(SCRIPT.parent))

from get_latest_stable import latest_stable  # noqa: E402


def _index(*versions: str) -> str:
    return json.dumps({"versions": list(versions)})


def test_picks_latest_stable_ignoring_prereleases():
    raw = _index("1.5.0", "1.6.0-rc1", "1.6.1", "2.0.0-preview")
    assert latest_stable(raw) == "1.6.1"


def test_ascending_order_returns_last_stable():
    raw = _index("1.0.0", "1.1.0", "1.2.0")
    assert latest_stable(raw) == "1.2.0"


# SEC-28: ANY hyphen is a SemVer prerelease label — not just the four the old
# denylist knew. -dev / -nightly / -daily / -ci must all be filtered.
@pytest.mark.parametrize(
    "suffix",
    ["-alpha", "-beta", "-rc1", "-preview.2", "-RC2",
     "-dev", "-nightly", "-daily.20260801", "-ci", "-canary"],
)
def test_prerelease_suffixes_are_filtered(suffix: str):
    assert latest_stable(_index("1.4.0", f"1.5.0{suffix}")) == "1.4.0"


def test_any_hyphen_label_is_prerelease():
    # A mixed index whose only "newer" entry is a non-standard prerelease label
    # must fall back to the stable version below it.
    assert latest_stable(_index("1.4.0", "1.5.0-daily.1")) == "1.4.0"
    # Build metadata ('+') is stable — the core version has no hyphen.
    assert latest_stable(_index("1.4.0", "1.5.0+build.7")) == "1.5.0+build.7"


def test_prerelease_only_returns_empty():
    assert latest_stable(_index("2.0.0-rc1", "2.0.0-preview")) == ""


def test_empty_and_blank_input_return_empty():
    assert latest_stable("") == ""
    assert latest_stable("   \n  ") == ""


def test_no_versions_key_returns_empty():
    assert latest_stable(json.dumps({})) == ""


def test_malformed_json_raises():
    with pytest.raises(json.JSONDecodeError):
        latest_stable("{not json")


def test_runs_as_stdin_subprocess():
    """End-to-end: the workflow pipes the index on stdin and reads stdout."""
    raw = _index("1.5.0", "1.6.0-rc1", "1.6.1")
    result = subprocess.run(
        [sys.executable, str(SCRIPT)],
        input=raw,
        capture_output=True,
        text=True,
    )
    assert result.returncode == 0, result.stderr
    assert result.stdout.strip() == "1.6.1"
