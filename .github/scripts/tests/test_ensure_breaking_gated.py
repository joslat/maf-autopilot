"""Tests for ensure_breaking_gated.py — the guard that guarantees a BREAKING
release leaves the watcher PR red even when registry-extract produced no entries.
"""
from __future__ import annotations

import os
import json
import subprocess
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

import verify_crossfile_consistency as vc  # noqa: E402
from ensure_breaking_gated import _next_review_id  # noqa: E402

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
    package: Microsoft.Agents.AI
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


def _run(ws: Path, verdict: str, new: str, *, expected_returncode: int = 0):
    env = os.environ.copy()
    env["VERDICT"] = verdict
    env["NEW_VERSION"] = new
    r = subprocess.run(
        [sys.executable, str(SCRIPTS / "ensure_breaking_gated.py")],
        cwd=str(ws), env=env, capture_output=True, text=True,
    )
    assert r.returncode == expected_returncode, f"{r.stdout}\n{r.stderr}"
    return r


def _no_changes(package: str, old: str, new: str) -> str:
    return (
        f"# API Diff: {package}\n\n"
        "| Field | Value |\n"
        "| ----- | ----- |\n"
        f"| Versions | **{old}** -> **{new}** |\n\n"
        "> [!NOTE]\n"
        "> No API changes detected.\n"
    )


def _agui_plan(
    ws: Path, new: str = "1.14.0", *, write_extraction: bool = True
) -> None:
    plan = {
        "schema_version": 1,
        "release": {"old_version": "1.13.0", "new_version": new},
        "surfaces": [
            {
                "order": 0,
                "slug": "core",
                "package": "Microsoft.Agents.AI",
                "id_scope": "",
                "status": "diffable",
                "old_package_version": "1.13.0",
                "new_package_version": new,
                "diff_artifact": "diff-core.txt",
                "stderr_artifact": "diff-core.stderr.txt",
                "reason": "Core API",
            },
            {
                "order": 1,
                "slug": "hosting-agui",
                "package": "Microsoft.Agents.AI.Hosting.AGUI",
                "id_scope": "HOSTING-AGUI",
                "status": "diffable",
                "old_package_version": "1.13.0",
                "new_package_version": new,
                "diff_artifact": "diff-hosting-agui.txt",
                "stderr_artifact": "diff-hosting-agui.stderr.txt",
                "reason": "AGUI API",
            },
        ],
        "lifecycle_events": [
            {
                "id": "agui-package-split",
                "release_version": new,
                "kind": "package_split",
                "impact": "breaking",
                "related_surfaces": ["hosting-agui"],
                "source_packages": [
                    {"package": "Microsoft.Agents.AI.Hosting.AGUI", "version": "1.13.0"}
                ],
                "target_packages": [
                    {"package": "Microsoft.Agents.AI.Hosting.AGUI.AspNetCore", "version": new}
                ],
                "reason": "The single AGUI package split into transport-specific packages.",
            }
        ],
    }
    (ws / "maf-package-plan.json").write_text(json.dumps(plan), encoding="utf-8")
    result_rows = []
    extraction_rows = []
    for surface in plan["surfaces"]:
        (ws / surface["diff_artifact"]).write_text(
            _no_changes(
                surface["package"],
                surface["old_package_version"],
                surface["new_package_version"],
            ),
            encoding="utf-8",
        )
        (ws / surface["stderr_artifact"]).write_text("", encoding="utf-8")
        result_rows.append(
            {
                "order": surface["order"],
                "slug": surface["slug"],
                "package": surface["package"],
                "status": surface["status"],
                "diff_artifact": surface["diff_artifact"],
                "stderr_artifact": surface["stderr_artifact"],
                "attempted": True,
                "tool_exit_code": 0,
                "validation_status": "passed",
                "validation_errors": [],
            }
        )
        extraction_rows.append(
            {
                "order": surface["order"],
                "slug": surface["slug"],
                "package": surface["package"],
                "status": surface["status"],
                "attempted": True,
                "tool_exit_code": 0,
                "extraction_status": "passed",
                "generated_entry_count": 0,
                "generated_entry_ids": [],
                "errors": [],
            }
        )
    (ws / "maf-diff-results.json").write_text(
        json.dumps(
            {
                "schema_version": 1,
                "release": plan["release"],
                "surfaces": result_rows,
            }
        ),
        encoding="utf-8",
    )
    if write_extraction:
        (ws / "maf-registry-extraction-results.json").write_text(
            json.dumps(
                {
                    "schema_version": 2,
                    "release": plan["release"],
                    "surfaces": extraction_rows,
                }
            ),
            encoding="utf-8",
        )


def test_breaking_no_entries_appends_sentinel_that_gate_flags(tmp_path):
    reg = _reg(tmp_path, OLD_ONLY)
    _run(tmp_path, "breaking", "1.12.0")
    # The sentinel must be valid YAML AND make the keep-breaking-red gate fire.
    findings = vc.check_unfilled_current_drafts(reg, "1.12.0")
    assert len(findings) == 1
    assert "REVIEW" in findings[0]
    text = reg.read_text(encoding="utf-8")
    assert "cs_warning: TODO" in text
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


def test_missing_registry_is_a_hard_failure(tmp_path):
    result = _run(
        tmp_path, "breaking", "1.12.0", expected_returncode=1
    )
    assert "cannot guarantee the breaking gate" in result.stdout


def test_review_id_uses_full_major_and_minor_components(tmp_path):
    reg = _reg(tmp_path, OLD_ONLY)
    _run(tmp_path, "breaking", "1.100.0")
    assert "MAF1100-REVIEW-001" in reg.read_text(encoding="utf-8")


def test_review_id_collision_check_is_case_insensitive():
    candidate, next_number = _next_review_id(
        "entries:\n  - id: maf114-review-001\n", "114", 1
    )
    assert candidate == "MAF114-REVIEW-002"
    assert next_number == 3


def test_core_entry_cannot_hide_uncovered_agui_package_split(tmp_path):
    reg = _reg(tmp_path, HAS_CURRENT.replace("1.12.0", "1.14.0"))
    _agui_plan(tmp_path)

    _run(tmp_path, "breaking", "1.14.0")

    text = reg.read_text(encoding="utf-8")
    assert "MAF114-REVIEW-001" in text
    assert "package: Microsoft.Agents.AI.Hosting.AGUI" in text
    assert "Breaking lifecycle event agui-package-split" in text
    findings = vc.check_unfilled_current_drafts(reg, "1.14.0")
    assert any("MAF114-REVIEW-001" in finding for finding in findings)


def test_lifecycle_event_is_covered_only_by_matching_package_entry(tmp_path):
    body = HAS_CURRENT.replace("1.12.0", "1.14.0").replace(
        "package: Microsoft.Agents.AI",
        "package: Microsoft.Agents.AI.Hosting.AGUI",
    )
    reg = _reg(tmp_path, body)
    _agui_plan(tmp_path)
    before = reg.read_text(encoding="utf-8")

    _run(tmp_path, "breaking", "1.14.0")

    assert reg.read_text(encoding="utf-8") == before


def test_each_uncovered_breaking_event_gets_its_own_review_draft(tmp_path):
    reg = _reg(tmp_path, HAS_CURRENT.replace("1.12.0", "1.14.0"))
    _agui_plan(tmp_path)
    plan_path = tmp_path / "maf-package-plan.json"
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    second = dict(plan["lifecycle_events"][0])
    second["id"] = "agui-transport-contract"
    second["reason"] = "The replacement transport has a separate breaking contract."
    plan["lifecycle_events"].append(second)
    plan_path.write_text(json.dumps(plan), encoding="utf-8")

    _run(tmp_path, "breaking", "1.14.0")

    text = reg.read_text(encoding="utf-8")
    assert "MAF114-REVIEW-001" in text
    assert "MAF114-REVIEW-002" in text
    assert "Breaking lifecycle event agui-package-split" in text
    assert "Breaking lifecycle event agui-transport-contract" in text


def test_mismatched_plan_version_fails_closed(tmp_path):
    _reg(tmp_path, OLD_ONLY)
    _agui_plan(tmp_path, new="1.14.0")
    result = _run(
        tmp_path, "breaking", "1.15.0", expected_returncode=1
    )
    assert "not NEW_VERSION 1.15.0" in result.stdout


def test_missing_extraction_ledger_blocks_the_breaking_guard(tmp_path):
    _reg(tmp_path, HAS_CURRENT.replace("1.12.0", "1.14.0"))
    _agui_plan(tmp_path, write_extraction=False)

    result = _run(
        tmp_path, "breaking", "1.14.0", expected_returncode=1
    )

    assert "Cannot prove per-package registry extraction coverage" in result.stdout
    assert "extraction results do not exist" in result.stdout
