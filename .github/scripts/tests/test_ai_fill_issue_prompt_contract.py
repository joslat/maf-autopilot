"""Contracts for the release Coding Agent's generated issue instructions."""
from pathlib import Path


TEMPLATE = (
    Path(__file__).resolve().parents[1] / "ai_fill_issue_prompt.md.tpl"
).read_text(encoding="utf-8")


def test_fill_prompt_covers_every_watcher_managed_release_surface():
    assert "Edit **FIVE files**" in TEMPLATE
    assert "guides/maf-current-migration-guide.md" in TEMPLATE
    assert "--cumulative-only" in TEMPLATE
    assert "verify_crossfile_consistency.py" in TEMPLATE
    assert 'dotnet "$MAF_DOCTOR" verify-registry' in TEMPLATE
    assert 'if [ "$FAILURES" -ne 0 ]' in TEMPLATE
    assert "exactly one ordered AUTO marker pair" in TEMPLATE
    assert "no unresolved <!-- TODO: placeholders" in TEMPLATE
    assert "current registry metadata: OK" in TEMPLATE
    assert "applies_to_codebases is" in TEMPLATE
    assert "non-mutating deterministic gate" in TEMPLATE
    assert "exact cumulative guide" in TEMPLATE


def test_fill_prompt_requires_version_scoped_evidence_backed_registry_fields():
    assert 'version_introduced` is `{{TARGET}}`' in TEMPLATE
    assert '`"pre-{{TARGET}}"`' in TEMPLATE
    assert "compile the exact before example" in TEMPLATE
    assert "`CS0618` only" in TEMPLATE
    assert "BINARY-SIGNATURE-CHANGED" in TEMPLATE
    assert "`BINARY_BREAK`" in TEMPLATE
    assert "Package transitions" in TEMPLATE
    assert "MAF{{ID_SEGMENT}}-REVIEW-*" in TEMPLATE
    assert "MAF{{DIGITS}}" not in TEMPLATE


def test_fill_prompt_describes_category_specific_before_examples():
    assert "TYPE-REMOVED references or instantiates" in TEMPLATE
    assert "METHOD-RENAMED calls the old member name" in TEMPLATE
    assert "SIGNATURE-CHANGED and BINARY-SIGNATURE-CHANGED invoke the old signature" in TEMPLATE
    assert "ATTRIBUTE-REMOVED applies the removed attribute" in TEMPLATE
    assert "Must use a type the diff says was removed" not in TEMPLATE


def test_fill_prompt_does_not_preserve_the_old_false_assumptions():
    assert "Edit **FOUR files**" not in TEMPLATE
    assert "applies_to_codebases` *(optional" not in TEMPLATE
    assert "default to BEHAVIOR-CHANGED" not in TEMPLATE
    assert "may contain garbled" not in TEMPLATE
    assert "Step 5 post-implementation" not in TEMPLATE
