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
    assert "--target-branch \"$BRANCH\"" in text


def test_dedupe_collision_invalidates_extraction_ledger():
    text = WORKFLOW.read_text(encoding="utf-8")
    dedupe = text.split("dedupe_registry_entries.py", 1)[1].split("fi", 1)[0]
    assert "rm -f maf-registry-extraction-results.json" in dedupe
    assert "exit 1" in dedupe
