"""Tests for the registry root-metadata updater."""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from update_registry_metadata import main, update_registry_metadata  # noqa: E402


REGISTRY = """\
# retained comment\r
schema_version: "1.0"\r
target_maf_version: "1.3.0"\r
last_updated: "2026-05-12"\r
\r
entries:\r
  - id: MAF130-EXISTING-001\r
"""


def test_updates_only_root_metadata_and_preserves_registry_formatting():
    updated = update_registry_metadata(REGISTRY, "1.14.0", "2026-08-17")

    assert updated == REGISTRY.replace(
        'target_maf_version: "1.3.0"',
        'target_maf_version: "1.14.0"',
    ).replace(
        'last_updated: "2026-05-12"',
        'last_updated: "2026-08-17"',
    )


@pytest.mark.parametrize(
    ("text", "message"),
    [
        (REGISTRY.replace('target_maf_version: "1.3.0"\r\n', ""), "target_maf_version"),
        (
            REGISTRY.replace(
                'target_maf_version: "1.3.0"\r\n',
                'target_maf_version: "1.3.0"\r\ntarget_maf_version: "1.4.0"\r\n',
            ),
            "target_maf_version",
        ),
        (REGISTRY.replace('last_updated: "2026-05-12"\r\n', ""), "last_updated"),
    ],
)
def test_rejects_missing_or_duplicate_root_fields(text: str, message: str):
    with pytest.raises(ValueError, match=message):
        update_registry_metadata(text, "1.14.0", "2026-08-17")


@pytest.mark.parametrize(
    ("version", "date", "message"),
    [
        ("1.14.0-preview.1", "2026-08-17", "stable X.Y.Z"),
        ("1.14.0", "2026-02-31", "valid ISO calendar date"),
        ("1.14.0", "2026-8-17", "valid ISO calendar date"),
    ],
)
def test_rejects_invalid_version_or_date(version: str, date: str, message: str):
    with pytest.raises(ValueError, match=message):
        update_registry_metadata(REGISTRY, version, date)


def test_dry_run_prints_update_without_writing(tmp_path: Path, capfd):
    registry = tmp_path / "registry.yaml"
    registry.write_bytes(REGISTRY.encode("utf-8"))

    result = main(
        [
            "--registry",
            str(registry),
            "--target-version",
            "1.14.0",
            "--last-updated",
            "2026-08-17",
            "--dry-run",
        ]
    )

    stdout, stderr = capfd.readouterr()
    assert result == 0
    assert 'target_maf_version: "1.14.0"' in stdout
    assert 'last_updated: "2026-08-17"' in stdout
    assert stderr == ""
    assert registry.read_bytes() == REGISTRY.encode("utf-8")
