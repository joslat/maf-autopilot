"""Static contracts for sequential release-watcher dispatches."""
from pathlib import Path


WORKFLOW = (
    Path(__file__).resolve().parents[2] / "workflows" / "maf-release-watcher.yml"
)


def test_manual_version_is_a_sequential_assertion_not_a_skip_override():
    text = WORKFLOW.read_text(encoding="utf-8")
    assert "must be a stable X.Y.Z version" in text
    assert 'NEXT=$(echo "$RAW" | python3 .github/scripts/get_latest_stable.py --after "$CURRENT")' in text
    assert '[ "$INPUT_VERSION" != "$NEXT" ]' in text
    assert "Process releases sequentially" in text
    assert 'LATEST="$INPUT_VERSION"' not in text


def test_push_pat_is_not_embedded_in_git_argv_or_config():
    text = WORKFLOW.read_text(encoding="utf-8")
    assert "git -c http.extraheader=" not in text
    assert "GIT_CONFIG_KEY_0=http.extraheader" in text
    assert 'GIT_CONFIG_VALUE_0="$AUTH_VALUE"' in text
    assert "git remote set-url" not in text


def test_nuget_outage_fails_closed_instead_of_reporting_no_update():
    text = WORKFLOW.read_text(encoding="utf-8")
    outage = text.split('if [ -z "$RAW" ]; then', 1)[1].split("fi", 1)[0]
    assert "exit 1" in outage
    assert "has_update=false" not in outage


def test_watcher_commits_a_per_release_immutable_obligations_artifact():
    text = WORKFLOW.read_text(encoding="utf-8")
    assert "build_scaffold_obligations.py" in text
    assert '.github/maf-scaffold-obligations/maf-${NEW_VERSION}.json' in text
    assert "--base-commit \"$BASE_COMMIT\"" in text
    assert "--target-branch \"$TARGET_BRANCH\"" in text


def test_retained_closed_branch_gets_a_fresh_target_reused_end_to_end():
    text = WORKFLOW.read_text(encoding="utf-8")
    assert "gh pr list --state open --json number,headRefName" in text
    assert 'TARGET_BRANCH="${PUSH_TARGET:-release-watcher/maf-${LATEST}}"' in text
    assert 'TARGET_BRANCH="${TARGET_BRANCH}-run-${RUN_ID}-${RUN_ATTEMPT}"' in text
    assert "target_branch: ${{ steps.version-check.outputs.target_branch }}" in text
    downstream = "${{ needs.check-for-new-maf-release.outputs.target_branch }}"
    assert text.count(downstream) == 2
    assert 'BRANCH="${PUSH_TARGET:-release-watcher/maf-${NEW_VERSION}}"' not in text
    assert 'gh pr list --state open --head "$BRANCH"' in text
    assert 'gh pr view "$BRANCH"' not in text
    assert "git push --force-with-lease" not in text


def test_registry_metadata_is_final_before_obligations_are_recorded():
    text = WORKFLOW.read_text(encoding="utf-8")
    evidence_gate = text.index("release_classification.py --require-complete-evidence")
    version_update = text.index("- name: Update .maf-version")
    metadata_update = text.index("update_registry_metadata.py")
    obligations = text.index("build_scaffold_obligations.py")
    assert evidence_gate < version_update < metadata_update < obligations
    assert '--target-version "$NEW_VERSION"' in text[metadata_update:obligations]
    assert '--last-updated "$LAST_UPDATED"' in text[metadata_update:obligations]


def test_dedupe_collision_invalidates_extraction_ledger():
    text = WORKFLOW.read_text(encoding="utf-8")
    dedupe = text.split("dedupe_registry_entries.py", 1)[1].split("fi", 1)[0]
    assert "rm -f maf-registry-extraction-results.json" in dedupe
    assert "exit 1" in dedupe
