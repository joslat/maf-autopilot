#!/usr/bin/env python3
"""Guarantee that a BREAKING release leaves the watcher PR RED.

The keep-breaking-red gate (verify_crossfile_consistency.check_unfilled_current_drafts)
only turns a PR red when there is >=1 UNFILLED registry draft for the current
version. Those drafts come from a SEPARATE, continue-on-error `registry-extract`
run (its own dotnet-inspect). If that run produced nothing — a correlated diff
failure, dedupe stripping every entry, or a parser that missed a removal the
classifier caught — a genuinely breaking release would otherwise sail through
GREEN while the PR body/label say "breaking".

This guard closes that gap: when the classifier verdict is `breaking` and the
registry has NO entry for the new version, it appends ONE sentinel unfilled draft
so the gate fires. The maintainer fills it (or replaces it with real entries)
before merge. Run AFTER registry-extract + classify, BEFORE commit.

Reads NEW_VERSION and VERDICT from the environment.
"""
import os
import sys
from pathlib import Path

REGISTRY = Path(".github/skills/maf-obsolete-api-registry/registry.yaml")


def main() -> int:
    verdict = os.environ.get("VERDICT", "").strip()
    new = os.environ.get("NEW_VERSION", "").strip()

    if verdict != "breaking":
        print(f"Verdict is '{verdict or '(unset)'}' — no keep-breaking-red guard needed.")
        return 0
    if not new:
        print("::error::NEW_VERSION not set; cannot guard the breaking gate.")
        return 1
    if not REGISTRY.is_file():
        print(f"::warning::{REGISTRY} not found; cannot append the sentinel.")
        return 0

    import yaml
    try:
        data = yaml.safe_load(REGISTRY.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as exc:
        print(f"::error::registry.yaml is not valid YAML: {exc}")
        return 1

    has_current = any(
        str(e.get("version_introduced", "")).strip() == new
        for e in data.get("entries", [])
    )
    if has_current:
        print(f"Registry already has >=1 entry for {new}; the keep-breaking-red gate will fire normally.")
        return 0

    seg = new.replace(".", "")[:3]
    sentinel = (
        f"\n# === Keep-breaking-red sentinel auto-added by release-watcher "
        f"(verdict=breaking, registry-extract produced no entries). "
        f"Review the release manually and replace with real entries. ===\n"
        f"  - id: MAF{seg}-SENTINEL-001\n"
        f"    package: Microsoft.Agents.AI\n"
        f'    version_introduced: "{new}"\n'
        f"    type: UNKNOWN\n"
        f"    method: UNKNOWN\n"
        f"    obsolete_signature: TODO\n"
        f"    replacement_signature: TODO\n"
        f"    argument_order_change: false\n"
        f"    fix_description: >-\n"
        f"      TODO — the dotnet-inspect diff for {new} produced no registry entries but the\n"
        f"      release was classified BREAKING. Review the release notes and the diff\n"
        f"      artifacts on the watcher run, document the breaking change(s) here, then\n"
        f"      replace this sentinel with real entries.\n"
        f"    example_before: |\n"
        f"      // TODO — show a real call site that breaks\n"
        f"    example_after: |\n"
        f"      // TODO — show the canonical replacement\n"
        f"    cs_warning: CS0618\n"
        f'    guide_section: "N/A"\n'
        f"    dotnet_inspect_detectable: false\n"
        f"    notes: >-\n"
        f"      Sentinel placeholder. The breaking verdict came from release_classification\n"
        f"      but registry-extract produced no entries (likely a failed or garbled diff).\n"
        f'    applies_to_codebases: "pre-{new}"\n'
    )
    with REGISTRY.open("a", encoding="utf-8") as f:
        f.write(sentinel)
    print(
        f"::warning::Breaking release {new} had no extracted registry entries — appended a "
        f"sentinel draft so the keep-breaking-red gate fires. Fill or replace it before merge."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
