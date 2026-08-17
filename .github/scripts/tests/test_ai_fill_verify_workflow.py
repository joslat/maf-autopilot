"""Static trust-boundary contracts for the required AI-fill verifier."""
from pathlib import Path

import yaml


WORKFLOW = (
    Path(__file__).resolve().parents[2] / "workflows" / "maf-ai-fill-verify.yml"
)


def _steps() -> list[dict]:
    document = yaml.safe_load(WORKFLOW.read_text(encoding="utf-8"))
    return document["jobs"]["verify"]["steps"]


def test_pr_data_and_trusted_verifier_use_separate_checkouts():
    checkouts = [
        step
        for step in _steps()
        if str(step.get("uses", "")).startswith("actions/checkout@")
    ]
    assert len(checkouts) == 2
    assert checkouts[0]["with"]["ref"] == "${{ github.event.pull_request.head.sha }}"
    assert checkouts[0]["with"]["path"] == "pr"
    assert checkouts[1]["with"]["ref"] == "${{ github.event.pull_request.base.sha }}"
    assert checkouts[1]["with"]["path"] == "trusted"
    assert all(step["with"]["persist-credentials"] is False for step in checkouts)


def test_scope_discovery_and_verifiers_fail_closed_from_trusted_base():
    steps = _steps()
    scope = next(step for step in steps if step.get("id") == "scope")["run"]
    assert "git -C pr diff --name-only" in scope
    assert "|| true" not in scope

    registry = next(step for step in steps if step.get("id") == "verify")
    assert "trusted/src/maf-autopilot" in registry["run"]
    assert "/pr/.github/skills/maf-obsolete-api-registry/registry.yaml" in registry["env"]["MAF_REGISTRY_PATH"]

    crossfile = next(step for step in steps if step.get("id") == "crossfile")["run"]
    assert "../trusted/.github/scripts/verify_crossfile_consistency.py" in crossfile

    delta = next(step for step in steps if step.get("id") == "delta")["run"]
    assert "trusted/.github/scripts/verify_ai_fill_delta.py" in delta
    assert "--repo pr" in delta
    assert "--head-root pr" in delta
    assert '--base-sha "$BASE_SHA"' in delta
    assert '--head-sha "$HEAD_SHA"' in delta


def test_first_verifier_bootstrap_cannot_approve_a_release_scaffold():
    delta = next(step for step in _steps() if step.get("id") == "delta")["run"]
    assert 'git -C pr ls-tree "$BASE_SHA" -- .github/scripts/verify_ai_fill_delta.py' in delta
    assert '[ "$TRUSTED_TREE_EXIT" -ne 0 ]' in delta
    assert '[ "$TRUSTED_MODE" != "100644" ]' in delta
    assert '[ -L "$TRUSTED_DELTA" ]' in delta
    assert delta.index('[ "$TRUSTED_MODE" != "100644" ]') < delta.index('python3 "$TRUSTED_DELTA"')
    assert 'git -C pr show "${BASE_SHA}:.maf-version"' in delta
    assert 'git -C pr show "${HEAD_SHA}:.maf-version"' in delta
    assert 'git -C pr cat-file -e "${HEAD_SHA}:.github/scripts/verify_ai_fill_delta.py"' in delta
    assert 'git -C pr diff --name-status "$BASE_SHA" "$HEAD_SHA"' in delta
    assert "$'A\\t.github/scripts/verify_ai_fill_delta.py'" in delta
    assert 'git -C pr ls-tree "$HEAD_SHA" -- .github/scripts/verify_ai_fill_delta.py' in delta
    assert '[ "$VERIFIER_TREE_EXIT" -ne 0 ]' in delta
    assert '[ "$VERIFIER_MODE" != "100644" ]' in delta
    assert "git -C pr ls-tree -r --name-only" in delta
    assert '.github/maf-scaffold-obligations/' in delta
    assert '[ "$BASE_VERSION" != "$HEAD_VERSION" ]' in delta
    assert '[ -n "$BASE_CONTRACT_FILES" ] || [ -n "$HEAD_CONTRACT_FILES" ]' in delta
    assert '[ "$BASE_CONTRACT_LIST_EXIT" -ne 0 ]' in delta
    assert '[ "$HEAD_CONTRACT_LIST_EXIT" -ne 0 ]' in delta
    assert '[ "$HEAD_VERIFIER_EXIT" -ne 0 ]' in delta
    assert "A release scaffold may not use the bootstrap path." in delta
    assert "EXIT=1" in delta


def test_obligations_are_branch_name_independent_and_recovered_from_history():
    steps = _steps()
    delta = next(step for step in steps if step.get("id") == "delta")
    assert "startsWith(github.base_ref" not in str(delta.get("if", ""))
    assert delta["if"] == "steps.scope.outputs.in_scope == 'true'"
    assert "--base-ref" in delta["run"]
    assert "--head-ref" in delta["run"]
    workflow = WORKFLOW.read_text(encoding="utf-8")
    assert ".github/maf-scaffold-obligations/maf-" in workflow
