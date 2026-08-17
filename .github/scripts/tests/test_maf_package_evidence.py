"""Focused tests for package-plan execution and fail-closed consumption."""
from __future__ import annotations

import json
import os
import subprocess
import sys
from copy import deepcopy
from pathlib import Path
from types import SimpleNamespace

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from extract_registry_from_plan import (  # noqa: E402
    REGISTRY_EXTRACTION_RESULTS_FILENAME,
    extract_registry_entries,
    load_registry_extraction_results,
)
from maf_package_evidence import (  # noqa: E402
    PackageEvidenceError,
    evaluate_package_evidence,
    load_package_plan,
    validate_results_document,
)
from release_classification import classify_from_files  # noqa: E402
from run_maf_package_diffs import execute_plan  # noqa: E402
from validate_dotnet_inspect_diff import MAX_DIFF_FILE_BYTES  # noqa: E402


OLD = "1.13.0"
NEW = "1.14.0"


def _surface(
    order: int,
    slug: str,
    package: str,
    scope: str,
    *,
    status: str = "diffable",
    old: str | None = OLD,
    new: str | None = NEW,
) -> dict:
    return {
        "order": order,
        "slug": slug,
        "package": package,
        "id_scope": scope,
        "status": status,
        "old_package_version": old,
        "new_package_version": new,
        "diff_artifact": f"diff-{slug}.txt",
        "stderr_artifact": f"diff-{slug}.stderr.txt",
        "reason": f"fixture reason for {slug}",
    }


def _plan(surfaces: list[dict], events: list[dict] | None = None, *, old=OLD, new=NEW):
    return {
        "schema_version": 1,
        "release": {"old_version": old, "new_version": new},
        "surfaces": surfaces,
        "lifecycle_events": events or [],
    }


def _result(surface: dict, passed: bool | None = True) -> dict:
    diffable = surface["status"] == "diffable"
    if not diffable:
        validation_status = "not_applicable"
        attempted = False
        tool_exit_code = None
        errors = []
    else:
        validation_status = "passed" if passed else "failed"
        attempted = True
        tool_exit_code = 0
        errors = [] if passed else ["fixture validation failure"]
    return {
        "order": surface["order"],
        "slug": surface["slug"],
        "package": surface["package"],
        "status": surface["status"],
        "diff_artifact": surface["diff_artifact"],
        "stderr_artifact": surface["stderr_artifact"],
        "attempted": attempted,
        "tool_exit_code": tool_exit_code,
        "validation_status": validation_status,
        "validation_errors": errors,
    }


def _no_changes(package: str, old=OLD, new=NEW) -> str:
    return f"""# API Diff: {package}

| Field | Value |
| ----- | ----- |
| Versions | **{old}** -> **{new}** |

> [!NOTE]
> No API changes detected.
"""


def _changed(
    package: str,
    row: str,
    *,
    classification: str = "Breaking Changes",
    count_label: str = "breaking",
    old=OLD,
    new=NEW,
) -> str:
    return f"""# API Diff: {package}

| Field | Value |
| ----- | ----- |
| Versions | **{old}** -> **{new}** |
| Summary | **Summary:** 1 {count_label} across 1 type |

## {classification}

### FixtureType

{row}
"""


def _write_release(
    root: Path,
    plan: dict,
    texts: dict[str, str],
    passed: dict[str, bool] | None = None,
    notes: str | None = "## Changes:\n* .NET: routine release\n",
) -> None:
    passed = passed or {}
    (root / "maf-package-plan.json").write_text(
        json.dumps(plan), encoding="utf-8"
    )
    rows = []
    for surface in plan["surfaces"]:
        (root / surface["diff_artifact"]).write_text(
            texts.get(surface["slug"], ""), encoding="utf-8"
        )
        (root / surface["stderr_artifact"]).write_text("", encoding="utf-8")
        rows.append(_result(surface, passed.get(surface["slug"], True)))
    (root / "maf-diff-results.json").write_text(
        json.dumps(
            {
                "schema_version": 1,
                "release": plan["release"],
                "surfaces": rows,
            }
        ),
        encoding="utf-8",
    )
    if notes is not None:
        (root / "release-notes.txt").write_text(notes, encoding="utf-8")


def _write_extraction_ledger(
    root: Path,
    plan: dict,
    rows_by_slug: dict[str, tuple[str, int, list[str]]],
) -> None:
    rows = []
    for surface in plan["surfaces"]:
        extraction_status, count, errors = rows_by_slug[surface["slug"]]
        generated_ids = (
            ["CORE-DRAFT"]
            if surface["slug"] == "core" and count == 1
            else [
                f"{surface['slug'].upper()}-DRAFT-{index + 1}"
                for index in range(count)
            ]
        )
        rows.append(
            {
                "order": surface["order"],
                "slug": surface["slug"],
                "package": surface["package"],
                "status": surface["status"],
                "attempted": extraction_status != "not_applicable",
                "tool_exit_code": 0 if extraction_status == "passed" else None,
                "extraction_status": extraction_status,
                "generated_entry_count": count,
                "generated_entry_ids": generated_ids,
                "errors": errors,
            }
        )
    (root / "maf-registry-extraction-results.json").write_text(
        json.dumps(
            {
                "schema_version": 2,
                "release": plan["release"],
                "surfaces": rows,
            }
        ),
        encoding="utf-8",
    )


def _seed_core_registry(root: Path, version: str) -> Path:
    registry_dir = root / ".github" / "skills" / "maf-obsolete-api-registry"
    registry_dir.mkdir(parents=True, exist_ok=True)
    registry = registry_dir / "registry.yaml"
    registry.write_text(
        "entries:\n"
        "  - id: CORE-DRAFT\n"
        "    package: Microsoft.Agents.AI\n"
        f'    version_introduced: "{version}"\n'
        "    obsolete_signature: TODO\n",
        encoding="utf-8",
    )
    return registry


def _run_breaking_guard(root: Path, version: str) -> subprocess.CompletedProcess:
    env = os.environ.copy()
    env.update(VERDICT="breaking", NEW_VERSION=version)
    return subprocess.run(
        [sys.executable, str(SCRIPTS / "ensure_breaking_gated.py")],
        cwd=root,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )


def test_valid_core_plus_empty_harness_fails_closed(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    harness = _surface(1, "harness", "Microsoft.Agents.AI.Harness", "HARNESS")
    plan = _plan([core, harness])
    _write_release(
        tmp_path,
        plan,
        {"core": _no_changes(core["package"]), "harness": ""},
        passed={"harness": False},
    )

    verdict = classify_from_files(tmp_path)
    assert verdict["additive"] is False
    assert verdict["diff_trustworthy"] is False
    assert any(error.startswith("harness:") for error in verdict["evidence_errors"])


def test_third_package_harness_break_is_not_hidden_by_clean_core(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    harness = _surface(1, "harness", "Microsoft.Agents.AI.Harness", "HARNESS")
    plan = _plan([core, harness])
    _write_release(
        tmp_path,
        plan,
        {
            "core": _no_changes(core["package"]),
            "harness": _changed(
                harness["package"], "- Member 'LegacyHarness' was removed"
            ),
        },
    )

    verdict = classify_from_files(tmp_path)
    assert verdict["diff_trustworthy"] is True
    assert verdict["additive"] is False
    assert verdict["removed"] == ["LegacyHarness"]


def test_potential_only_third_package_is_not_mislabeled_additive(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    workflows = _surface(
        1, "workflows", "Microsoft.Agents.AI.Workflows", "WORKFLOWS"
    )
    shell = _surface(
        2, "tools-shell", "Microsoft.Agents.AI.Tools.Shell", "TOOLS-SHELL"
    )
    plan = _plan([core, workflows, shell])
    _write_release(
        tmp_path,
        plan,
        {
            "core": _no_changes(core["package"]),
            "workflows": _no_changes(workflows["package"]),
            "tools-shell": _changed(
                shell["package"],
                "- Base type changed from 'OldShellPolicy' to 'ShellPolicyBase'",
                classification="Potentially Breaking Changes",
                count_label="potentially breaking",
            ),
        },
    )

    verdict = classify_from_files(tmp_path)
    assert verdict["diff_trustworthy"] is True
    assert verdict["summary_breaking"] == 0
    assert verdict["summary_potentially_breaking"] == 1
    assert verdict["removed"] == []
    assert verdict["signature_changed"] == []
    assert verdict["additive"] is False


def test_core_draft_cannot_hide_harness_extraction_timeout(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    harness = _surface(1, "harness", "Microsoft.Agents.AI.Harness", "HARNESS")
    plan = _plan([core, harness])
    _write_release(
        tmp_path,
        plan,
        {
            "core": _changed(core["package"], "- Member 'OldCore' was removed"),
            "harness": _changed(
                harness["package"], "- Member 'OldHarness' was removed"
            ),
        },
    )
    _write_extraction_ledger(
        tmp_path,
        plan,
        {
            "core": ("passed", 1, []),
            "harness": ("failed", 0, ["registry-extract timed out after 120s"]),
        },
    )
    registry = _seed_core_registry(tmp_path, NEW)

    guarded = _run_breaking_guard(tmp_path, NEW)

    assert guarded.returncode == 0, guarded.stdout + guarded.stderr
    text = registry.read_text(encoding="utf-8")
    assert "package: Microsoft.Agents.AI.Harness" in text
    assert "Registry extraction failed for review-relevant surface harness" in text


def test_same_package_entry_cannot_hide_missing_generated_draft_id(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    _write_release(
        tmp_path,
        plan,
        {"core": _changed(core["package"], "- Member 'OldCore' was removed")},
    )
    _write_extraction_ledger(tmp_path, plan, {"core": ("passed", 1, [])})
    ledger_path = tmp_path / REGISTRY_EXTRACTION_RESULTS_FILENAME
    ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
    ledger["surfaces"][0]["generated_entry_ids"] = ["EXPECTED-CORE-DRAFT"]
    ledger_path.write_text(json.dumps(ledger), encoding="utf-8")
    registry = _seed_core_registry(tmp_path, NEW)

    guarded = _run_breaking_guard(tmp_path, NEW)

    assert guarded.returncode == 0, guarded.stdout + guarded.stderr
    text = registry.read_text(encoding="utf-8")
    assert "MAF114-REVIEW-001" in text
    assert "EXPECTED-CORE-DRAFT" in text
    assert "Registry append did not preserve" in text


def test_core_draft_cannot_hide_third_package_potential_break(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    workflows = _surface(
        1, "workflows", "Microsoft.Agents.AI.Workflows", "WORKFLOWS"
    )
    shell = _surface(
        2, "tools-shell", "Microsoft.Agents.AI.Tools.Shell", "TOOLS-SHELL"
    )
    plan = _plan([core, workflows, shell])
    _write_release(
        tmp_path,
        plan,
        {
            "core": _changed(core["package"], "- Member 'OldCore' was removed"),
            "workflows": _no_changes(workflows["package"]),
            "tools-shell": _changed(
                shell["package"],
                "- Type was sealed",
                classification="Potentially Breaking Changes",
                count_label="potentially breaking",
            ),
        },
    )
    _write_extraction_ledger(
        tmp_path,
        plan,
        {
            "core": ("passed", 1, []),
            "workflows": ("passed", 0, []),
            "tools-shell": ("passed", 0, []),
        },
    )
    registry = _seed_core_registry(tmp_path, NEW)

    guarded = _run_breaking_guard(tmp_path, NEW)

    assert guarded.returncode == 0, guarded.stdout + guarded.stderr
    text = registry.read_text(encoding="utf-8")
    assert "package: Microsoft.Agents.AI.Tools.Shell" in text
    assert "1 potentially breaking" in text


def test_normal_package_draft_cannot_hide_unrepresented_structural_row(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    report = _changed(
        core["package"],
        "- Member 'OldCore' was removed\n- Type was sealed",
    ).replace("1 breaking", "2 breaking")
    _write_release(tmp_path, plan, {"core": report})
    _write_extraction_ledger(
        tmp_path, plan, {"core": ("passed", 1, [])}
    )
    registry = _seed_core_registry(tmp_path, NEW)

    guarded = _run_breaking_guard(tmp_path, NEW)

    assert guarded.returncode == 0, guarded.stdout + guarded.stderr
    text = registry.read_text(encoding="utf-8")
    assert "MAF114-REVIEW-001" in text
    assert "structural breaking/potentially-breaking row(s)" in text


def test_agui_package_split_event_is_breaking_without_api_removal(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    agui = _surface(
        1,
        "hosting-agui",
        "Microsoft.Agents.AI.Hosting.AGUI.AspNetCore",
        "HOSTING-AGUI",
        status="informational",
        old=None,
        new=None,
    )
    event = {
        "id": "maf-1.14.0-agui-split",
        "release_version": NEW,
        "kind": "package_split",
        "impact": "breaking",
        "related_surfaces": ["hosting-agui"],
        "source_packages": [{"package": "Microsoft.Agents.AI.AGUI", "version": OLD}],
        "target_packages": [{"package": "AGUI.Client", "version": "0.0.3"}],
        "reason": "The legacy AGUI package split into the external SDK.",
    }
    plan = _plan([core, agui], [event])
    _write_release(tmp_path, plan, {"core": _no_changes(core["package"])})

    verdict = classify_from_files(tmp_path)
    assert verdict["diff_trustworthy"] is True
    assert verdict["additive"] is False
    assert [item["id"] for item in verdict["breaking_lifecycle_events"]] == [
        event["id"]
    ]


def test_1_17_externalization_is_visible_but_informational(tmp_path):
    old, new = "1.16.0", "1.17.0"
    core = _surface(0, "core", "Microsoft.Agents.AI", "", old=old, new=new)
    durable = _surface(
        1,
        "durable",
        "Microsoft.Agents.AI.DurableTask",
        "DURABLE",
        status="informational",
        old="1.16.0-preview.1",
        new=None,
    )
    azure = _surface(
        2,
        "azure-functions",
        "Microsoft.Agents.AI.Hosting.AzureFunctions",
        "AZURE-FUNCTIONS",
        status="informational",
        old="1.16.0-preview.1",
        new=None,
    )
    event = {
        "id": "maf-1.17.0-durable-extension-externalization",
        "release_version": new,
        "kind": "repository_externalization",
        "impact": "informational",
        "related_surfaces": ["durable", "azure-functions"],
        "source_packages": [
            {"package": durable["package"], "version": durable["old_package_version"]},
            {"package": azure["package"], "version": azure["old_package_version"]},
        ],
        "target_packages": [
            {"package": durable["package"]},
            {"package": azure["package"]},
        ],
        "source_repository": "microsoft/agent-framework",
        "target_repository": "microsoft/agent-framework-durable-extension",
        "same_package_ids": True,
        "reason": "Ownership moved while Microsoft retained package IDs.",
    }
    plan = _plan([core, durable, azure], [event], old=old, new=new)
    _write_release(
        tmp_path,
        plan,
        {"core": _no_changes(core["package"], old, new)},
    )

    verdict = classify_from_files(tmp_path)
    assert verdict["additive"] is True
    assert verdict["breaking_lifecycle_events"] == []
    assert verdict["informational_lifecycle_events"][0]["id"] == event["id"]


def test_missing_release_notes_fail_closed_even_with_valid_no_change_diff(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    _write_release(
        tmp_path,
        plan,
        {"core": _no_changes(core["package"])},
        notes=None,
    )
    verdict = classify_from_files(tmp_path)
    assert verdict["additive"] is False
    assert verdict["release_notes_trustworthy"] is False
    assert any("release-note evidence" in item for item in verdict["evidence_errors"])


def test_final_evidence_gate_blocks_missing_notes_and_invalid_surface(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    harness = _surface(1, "harness", "Microsoft.Agents.AI.Harness", "HARNESS")
    plan = _plan([core, harness])
    _write_release(
        tmp_path,
        plan,
        {"core": _no_changes(core["package"]), "harness": ""},
        passed={"harness": False},
        notes=None,
    )

    gated = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "release_classification.py"),
            "--require-complete-evidence",
        ],
        cwd=tmp_path,
        capture_output=True,
        text=True,
        check=False,
    )

    assert gated.returncode == 1
    assert "refusing to advance .maf-version" in gated.stderr
    assert "harness:" in gated.stderr
    assert "release-note evidence" in gated.stderr


def test_final_evidence_gate_allows_complete_breaking_evidence(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    _write_release(
        tmp_path,
        plan,
        {"core": _changed(core["package"], "- Member 'Old' was removed")},
        notes="* .NET: [BREAKING] Old was removed\n",
    )
    _write_extraction_ledger(
        tmp_path, plan, {"core": ("passed", 1, [])}
    )

    gated = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "release_classification.py"),
            "--require-complete-evidence",
        ],
        cwd=tmp_path,
        capture_output=True,
        text=True,
        check=False,
    )

    assert gated.returncode == 0
    assert gated.stdout.strip() == "complete"


def test_final_evidence_gate_blocks_missing_extraction_ledger(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    _write_release(tmp_path, plan, {"core": _no_changes(core["package"])})

    gated = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "release_classification.py"),
            "--require-complete-evidence",
        ],
        cwd=tmp_path,
        capture_output=True,
        text=True,
        check=False,
    )

    assert gated.returncode == 1
    assert "registry extraction results do not exist" in gated.stderr


def test_runner_preserves_deterministic_manifest_and_artifact_order(tmp_path):
    surfaces = [
        _surface(0, "core", "Microsoft.Agents.AI", ""),
        _surface(1, "harness", "Microsoft.Agents.AI.Harness", "HARNESS"),
        _surface(
            2,
            "hosting-openai",
            "Microsoft.Agents.AI.Hosting.OpenAI",
            "HOSTING-OPENAI",
        ),
    ]
    plan = _plan(surfaces)
    calls = []

    def fake_run(command, *, stdout, stderr, check, timeout):
        del stderr, check
        calls.append((command, timeout))
        package_range = command[command.index("--package") + 1]
        package = package_range.split("@", 1)[0]
        stdout.write(_no_changes(package).encode("utf-8"))
        return SimpleNamespace(returncode=0)

    results, code = execute_plan(
        plan,
        tmp_path / "maf-diff-results.json",
        tmp_path,
        runner=fake_run,
    )
    assert code == 0
    assert [row["slug"] for row in results["surfaces"]] == [
        "core",
        "harness",
        "hosting-openai",
    ]
    assert [row["diff_artifact"] for row in results["surfaces"]] == [
        "diff-core.txt",
        "diff-harness.txt",
        "diff-hosting-openai.txt",
    ]
    assert [call[0][3].split("@", 1)[0] for call in calls] == [
        surface["package"] for surface in surfaces
    ]
    assert all(timeout == 180 for _, timeout in calls)


def test_runner_timeout_is_recorded_and_fails_closed(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])

    def timeout_run(command, **kwargs):
        raise subprocess.TimeoutExpired(command, kwargs["timeout"])

    results, code = execute_plan(
        plan,
        tmp_path / "maf-diff-results.json",
        tmp_path,
        timeout_seconds=7,
        runner=timeout_run,
    )
    assert code == 1
    assert results["surfaces"][0]["validation_status"] == "failed"
    assert any("7-second timeout" in item for item in results["surfaces"][0]["validation_errors"])


def test_oversized_surface_is_not_read_or_trusted_downstream(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    _write_release(tmp_path, plan, {"core": ""})
    (tmp_path / "diff-core.txt").write_bytes(b"A" * (MAX_DIFF_FILE_BYTES + 1))
    verdict = classify_from_files(tmp_path)
    assert verdict["additive"] is False
    assert verdict["diff_trustworthy"] is False
    assert any("exceeds" in item for item in verdict["evidence_errors"])


@pytest.mark.parametrize(
    "mutate",
    [
        lambda plan: plan["surfaces"][0].update(diff_artifact="../escape.txt"),
        lambda plan: plan["surfaces"][0].update(order=9),
        lambda plan: plan["lifecycle_events"][0].update(kind="run_command"),
        lambda plan: plan["lifecycle_events"][0].update(same_package_ids=False),
    ],
)
def test_hostile_or_malformed_plan_is_rejected(tmp_path, mutate):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    event = {
        "id": "maf-1.14.0-externalization",
        "release_version": NEW,
        "kind": "repository_externalization",
        "impact": "informational",
        "related_surfaces": ["core"],
        "source_packages": [{"package": core["package"]}],
        "target_packages": [{"package": core["package"]}],
        "source_repository": "microsoft/agent-framework",
        "target_repository": "microsoft/other",
        "same_package_ids": True,
        "reason": "fixture",
    }
    plan = _plan([core], [event])
    mutate(plan)
    path = tmp_path / "plan.json"
    path.write_text(json.dumps(plan), encoding="utf-8")
    with pytest.raises(PackageEvidenceError):
        load_package_plan(path)


@pytest.mark.parametrize("duplicate_field", ["package", "id_scope"])
def test_duplicate_package_or_scope_is_rejected(tmp_path, duplicate_field):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    harness = _surface(1, "harness", "Microsoft.Agents.AI.Harness", "HARNESS")
    if duplicate_field == "package":
        harness["package"] = core["package"].lower()
        surfaces = [core, harness]
    else:
        third = _surface(2, "hosting", "Microsoft.Agents.AI.Hosting", "HARNESS")
        surfaces = [core, harness, third]
    path = tmp_path / "plan.json"
    path.write_text(json.dumps(_plan(surfaces)), encoding="utf-8")
    with pytest.raises(PackageEvidenceError, match="duplicate"):
        load_package_plan(path)


def test_passed_result_cannot_carry_validation_errors():
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    row = _result(core)
    row["validation_errors"] = ["runner reported a contradiction"]
    errors = validate_results_document(
        {"schema_version": 1, "release": plan["release"], "surfaces": [row]},
        plan,
    )
    assert any("passed diff result" in error for error in errors)


def test_registry_extraction_uses_each_validated_diff_without_network(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    harness = _surface(1, "harness", "Microsoft.Agents.AI.Harness", "HARNESS")
    plan = _plan([core, harness])
    _write_release(
        tmp_path,
        plan,
        {
            "core": _no_changes(core["package"]),
            "harness": _no_changes(harness["package"]),
        },
    )
    evidence = evaluate_package_evidence(
        plan,
        json.loads((tmp_path / "maf-diff-results.json").read_text(encoding="utf-8")),
        tmp_path,
    )
    calls = []

    def fake_extract(command, **kwargs):
        calls.append((command, kwargs))
        return SimpleNamespace(returncode=0, stdout="entries: []\n", stderr="")

    old_cwd = Path.cwd()
    try:
        os.chdir(tmp_path)
        code = extract_registry_entries(
            plan,
            evidence,
            Path("maf-doctor.dll"),
            Path("drafts.yaml"),
            runner=fake_extract,
        )
    finally:
        os.chdir(old_cwd)
    assert code == 0
    assert len(calls) == 2
    assert "--diff-file" in calls[0][0]
    assert calls[0][0][calls[0][0].index("--release-version") + 1] == NEW
    assert "--id-scope" not in calls[0][0]
    assert calls[1][0][-2:] == ["--id-scope", "HARNESS"]
    assert all("https://" not in arg for command, _ in calls for arg in command)
    ledger = load_registry_extraction_results(
        tmp_path / REGISTRY_EXTRACTION_RESULTS_FILENAME, plan
    )
    assert [row["extraction_status"] for row in ledger["surfaces"]] == [
        "passed",
        "passed",
    ]
    assert [row["generated_entry_count"] for row in ledger["surfaces"]] == [0, 0]


def test_registry_extraction_timeout_continues_fail_closed(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    _write_release(tmp_path, plan, {"core": _no_changes(core["package"])})
    results = json.loads((tmp_path / "maf-diff-results.json").read_text(encoding="utf-8"))
    evidence = evaluate_package_evidence(plan, results, tmp_path)

    def timeout_extract(command, **kwargs):
        raise subprocess.TimeoutExpired(command, kwargs["timeout"])

    old_cwd = Path.cwd()
    try:
        os.chdir(tmp_path)
        code = extract_registry_entries(
            plan,
            evidence,
            Path("maf-doctor.dll"),
            Path("drafts.yaml"),
            timeout_seconds=3,
            runner=timeout_extract,
        )
    finally:
        os.chdir(old_cwd)
    assert code == 1
    assert (tmp_path / "drafts.yaml").read_text(encoding="utf-8") == ""
    ledger = load_registry_extraction_results(
        tmp_path / REGISTRY_EXTRACTION_RESULTS_FILENAME, plan
    )
    assert ledger["surfaces"][0]["extraction_status"] == "failed"
    assert "timed out" in ledger["surfaces"][0]["errors"][0]


def test_extraction_ledger_rejects_generated_id_count_mismatch(tmp_path):
    core = _surface(0, "core", "Microsoft.Agents.AI", "")
    plan = _plan([core])
    _write_extraction_ledger(tmp_path, plan, {"core": ("passed", 1, [])})
    path = tmp_path / REGISTRY_EXTRACTION_RESULTS_FILENAME
    ledger = json.loads(path.read_text(encoding="utf-8"))
    ledger["surfaces"][0]["generated_entry_ids"] = []
    path.write_text(json.dumps(ledger), encoding="utf-8")

    with pytest.raises(PackageEvidenceError, match="length must equal"):
        load_registry_extraction_results(path, plan)


def test_1_15_base_hosting_break_produces_scoped_offline_extraction(tmp_path):
    old, new = "1.14.0", "1.15.0"
    core = _surface(0, "core", "Microsoft.Agents.AI", "", old=old, new=new)
    hosting = _surface(
        1,
        "hosting",
        "Microsoft.Agents.AI.Hosting",
        "HOSTING",
        old=old,
        new=new,
    )
    plan = _plan([core, hosting], old=old, new=new)
    core_additive = _changed(
        core["package"],
        "- Member 'ResponsesHostingOption' was added",
        classification="Additive Changes",
        count_label="additive",
        old=old,
        new=new,
    )
    hosting_break = _changed(
        hosting["package"],
        "- Member 'DeleteSessionAsync' signature changed: `old` -> `new`",
        old=old,
        new=new,
    )
    _write_release(
        tmp_path,
        plan,
        {"core": core_additive, "hosting": hosting_break},
        notes=(
            "## Changes:\n"
            "* .NET: [BREAKING] Hosting AgentSessionStore source contract changed (#7000)\n"
        ),
    )
    verdict = classify_from_files(tmp_path)
    assert verdict["additive"] is False
    assert verdict["signature_changed"] == ["DeleteSessionAsync"]

    results = json.loads((tmp_path / "maf-diff-results.json").read_text(encoding="utf-8"))
    evidence = evaluate_package_evidence(plan, results, tmp_path)
    calls = []

    def fake_extract(command, **kwargs):
        del kwargs
        calls.append(command)
        package = command[3]
        output = "# No new draft registry entries — diff contained no breaking-change candidates.\n"
        if package == hosting["package"]:
            output = (
                "  - id: MAF115-HOSTING-DELETESESSIONASYNC-001\n"
                "    version_introduced: \"1.15.0\"\n"
                "    obsolete_signature: TODO\n"
            )
        return SimpleNamespace(returncode=0, stdout=output, stderr="")

    old_cwd = Path.cwd()
    try:
        os.chdir(tmp_path)
        extract_code = extract_registry_entries(
            plan,
            evidence,
            Path("maf-doctor.dll"),
            Path("drafts.yaml"),
            runner=fake_extract,
        )
    finally:
        os.chdir(old_cwd)
    assert extract_code == 0
    hosting_command = next(command for command in calls if command[3] == hosting["package"])
    assert hosting_command[-2:] == ["--id-scope", "HOSTING"]
    assert hosting_command[hosting_command.index("--diff-file") + 1] == "diff-hosting.txt"
    assert "MAF115-HOSTING-DELETESESSIONASYNC-001" in (
        tmp_path / "drafts.yaml"
    ).read_text(encoding="utf-8")
    ledger = load_registry_extraction_results(
        tmp_path / REGISTRY_EXTRACTION_RESULTS_FILENAME, plan
    )
    assert ledger["surfaces"][1]["generated_entry_ids"] == [
        "MAF115-HOSTING-DELETESESSIONASYNC-001"
    ]


def test_release_note_only_behavioral_break_keeps_review_sentinel(tmp_path):
    old, new = "1.18.0", "1.19.0"
    core = _surface(0, "core", "Microsoft.Agents.AI", "", old=old, new=new)
    plan = _plan([core], old=old, new=new)
    additive = _changed(
        core["package"],
        "- Member 'NewOption' was added",
        classification="Additive Changes",
        count_label="additive",
        old=old,
        new=new,
    )
    _write_release(
        tmp_path,
        plan,
        {"core": additive},
        notes="## Changes:\n* .NET: [BREAKING] A behavioral default changed (#1)\n",
    )
    _write_extraction_ledger(
        tmp_path, plan, {"core": ("passed", 0, [])}
    )
    registry = tmp_path / ".github" / "skills" / "maf-obsolete-api-registry"
    registry.mkdir(parents=True)
    (registry / "registry.yaml").write_text(
        "entries:\n  - id: OLD\n    version_introduced: \"1.18.0\"\n",
        encoding="utf-8",
    )

    classified = subprocess.run(
        [sys.executable, str(SCRIPTS / "release_classification.py")],
        cwd=tmp_path,
        capture_output=True,
        text=True,
        check=False,
    )
    assert classified.returncode == 0
    assert classified.stdout.strip() == "breaking"
    env = os.environ.copy()
    env.update(VERDICT="breaking", NEW_VERSION=new)
    guarded = subprocess.run(
        [sys.executable, str(SCRIPTS / "ensure_breaking_gated.py")],
        cwd=tmp_path,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )
    assert guarded.returncode == 0
    registry_text = (registry / "registry.yaml").read_text(encoding="utf-8")
    assert "MAF119-REVIEW-001" in registry_text
    assert "Validated evidence for 1.19.0" in registry_text
    assert "cs_warning: TODO" in registry_text
