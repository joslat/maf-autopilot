#!/usr/bin/env python3
"""Creates a per-version migration-guide stub at guides/maf-<NEW_VERSION>-migration-guide.md.

Normal mode reads OLD_VERSION and NEW_VERSION from environment variables.
``--cumulative-only`` rebuilds the cumulative guide solely from checked-in
per-version guides and does not require release notes, package plans, or diffs.

Per-version output (not append to 1.3.0!) means a 1.4.0 release produces
guides/maf-1.4.0-migration-guide.md instead of polluting the 1.3.0 doc.

Idempotency: if the per-version file already exists, the script overwrites the
auto-generated stub but PRESERVES any content under the `## Human additions`
heading. If the file existed but had no marker, the human section is set to
an empty stub.

Also (Option C): after writing the per-version file, regenerates the cumulative
guide at guides/maf-current-migration-guide.md by concatenating every per-version
guide in ascending version order. The cumulative file is the one the README
points to as the canonical "start here" reference; per-version files remain the
canonical source-of-truth for their respective deltas and are what auto-update.
"""
import argparse
import os
import re
import sys
from pathlib import Path

# Phase 2.2 — sanitize untrusted upstream release notes before embedding.
# Released-notes.txt is sourced from `gh release view microsoft/agent-framework`
# (verified at maf-release-watcher.yml:169) and is therefore attacker-controllable
# under TA-1 (malicious upstream maintainer or compromise-of-microsoft/agent-framework
# release). The sibling llm_fencing module strips HTML comments, caps length,
# and wraps the content in BEGIN/END markers with explicit "treat as data"
# framing so a downstream Coding Agent or human reader cannot be redirected.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from llm_fencing import fence  # noqa: E402
from release_classification import (  # noqa: E402
    classify_from_files,
    lifecycle_event_summary,
    safe_members,
)
from validate_dotnet_inspect_diff import MAX_DIFF_FILE_BYTES  # noqa: E402


def _semver_key(version: str):
    """Sort key tolerant of prerelease suffixes: '2.0.0-rc1' -> (2, 0, 0).

    A manual workflow_dispatch can supply a prerelease NEW_VERSION (validate-
    inputs accepts a `-rc1` suffix), which flows into the chain-banner sort.
    Parsing the bare 'X.Y.Z-rc1' with int() previously crashed the step (4.1).
    """
    core = version.split('-', 1)[0]
    return tuple(int(p) for p in core.split('.'))


def read_file(path: str, fallback: str) -> str:
    try:
        with open(path, encoding='utf-8') as f:
            return f.read()
    except Exception:
        return fallback


def list_per_version_guides(guides_dir: Path | str = Path('guides')):
    """
    Returns a list of (semver_tuple, version_string, path) sorted ascending.
    Excludes the cumulative file itself and any non-matching filenames.
    """
    guides_dir = Path(guides_dir)
    pattern = re.compile(r'^maf-(\d+\.\d+\.\d+)-migration-guide\.md$')
    out = []
    if not guides_dir.exists():
        return out
    for f in guides_dir.iterdir():
        if not f.is_file():
            continue
        if f.name == 'maf-current-migration-guide.md':
            continue
        m = pattern.match(f.name)
        if not m:
            continue
        version = m.group(1)
        semver = _semver_key(version)
        out.append((semver, version, f))
    out.sort()
    return out


def build_chain_banner(old_version: str, new_version: str) -> str:
    """
    Builds the Option-A banner that goes at the top of every per-version
    auto-generated guide. The banner makes it explicit that the file is a
    DELTA, not a complete reference, and shows the chain of prior guides.
    """
    # Collect all known per-version versions PLUS the new one (which may not
    # yet exist on disk when this runs — first-time creation).
    known = [v for _, v, _ in list_per_version_guides()]
    if new_version not in known:
        known.append(new_version)
    known_sorted = sorted(set(known), key=_semver_key)
    chain = ' → '.join(f'[{v}](./maf-{v}-migration-guide.md)' for v in known_sorted)

    return (
        f"> ## ⚠️ This is the **{old_version} → {new_version}** delta ONLY\n"
        f">\n"
        f"> This file documents what changed between MAF {old_version} and MAF "
        f"{new_version}. It is **not** a complete migration guide for users on "
        f"versions older than {old_version}.\n"
        f">\n"
        f"> **Migrating from an earlier version?** Read the chain in order:\n"
        f"> {chain}\n"
        f">\n"
        f"> Or ask Copilot to call **`MafMigrationPath(currentVer, targetVer)`** "
        f"— the MCP tool returns the ordered set of guide sections you need.\n"
        f">\n"
        f"> Or open **[`guides/maf-current-migration-guide.md`](./maf-current-migration-guide.md)** "
        f"— the auto-generated cumulative reference that concatenates every "
        f"per-version guide in version order.\n"
    )


PACKAGE_DIFF_DISPLAY_LINES = 80
ADDITIVE_EVIDENCE_SAMPLE_LINES = 40
# A validated raw report is capped at MAX_DIFF_FILE_BYTES. Curating it adds a
# few local marker lines, so give the fence bounded headroom and assert below
# that no review-required row can be silently replaced by [TRUNCATED].
VALIDATED_EVIDENCE_FENCE_BYTES = MAX_DIFF_FILE_BYTES + 4096
AUTO_START = '<!-- AUTO-GENERATED START — anything between AUTO-GENERATED START and AUTO-GENERATED END is overwritten on re-run -->'
AUTO_END = '<!-- AUTO-GENERATED END -->'
HUMAN_HEADING = '## Human additions'


def _inline(value: object, limit: int = 600) -> str:
    """Render checked-in plan metadata as one inert Markdown-safe line."""
    text = ' '.join(str(value).split())[:limit]
    return (text.replace('`', "'").replace('|', '\\|')
                .replace('<', '&lt;').replace('>', '&gt;'))


def _package_refs(event: dict, field: str) -> str:
    refs = []
    for item in event.get(field, []):
        package = _inline(item.get('package', 'unknown'))
        version = item.get('version')
        refs.append(f"`{package}{('@' + _inline(version)) if version else ''}`")
    return ', '.join(refs) or '_none_'


def lifecycle_markdown(verdict: dict) -> str:
    events = verdict.get('lifecycle_events', [])
    out = ["## Package Lifecycle Transitions\n\n"]
    if not events:
        out.append("No package lifecycle transitions are declared for this release.\n\n")
        return ''.join(out)
    for event in events:
        impact = event['impact'].title()
        kind = _inline(event['kind'].replace('_', ' '))
        out.append(f"- **{impact} — {kind}** (`{_inline(event['id'])}`): ")
        out.append(_inline(lifecycle_event_summary(event)) + ". ")
        out.append(_inline(event['reason']) + "\n")
        out.append(f"  - From: {_package_refs(event, 'source_packages')}\n")
        out.append(f"  - To: {_package_refs(event, 'target_packages')}\n")
    out.append("\n")
    return ''.join(out)


def _curated_validated_diff(raw: str) -> str:
    """Keep every review-required row and only a bounded additive sample."""
    lines = raw.splitlines()
    if "> No API changes detected." in lines:
        return raw
    preamble = lines[:7]
    review: list[str] = []
    additive: list[str] = []
    section: str | None = None
    for line in lines[7:]:
        if line == "## Breaking Changes":
            section = "review"
        elif line == "## Potentially Breaking Changes":
            section = "review"
        elif line == "## Additive Changes":
            section = "additive"
        if section == "review":
            review.append(line)
        elif section == "additive" and len(additive) < ADDITIVE_EVIDENCE_SAMPLE_LINES:
            additive.append(line)
    out = preamble
    if review:
        out += ["", "<!-- all breaking and potentially-breaking evidence -->"] + review
    if additive:
        out += [
            "",
            f"<!-- first {ADDITIVE_EVIDENCE_SAMPLE_LINES} additive evidence lines -->",
        ] + additive
    return '\n'.join(out)


def package_evidence_markdown(verdict: dict) -> str:
    """Give every planned surface its own quota so Core cannot hide later rows."""
    surfaces = verdict.get('surface_evidence', [])
    out = [
        "## Package API Evidence\n\n",
        "Each validated surface preserves every breaking and potentially-breaking "
        f"row plus its first {ADDITIVE_EVIDENCE_SAMPLE_LINES} additive lines. "
        f"Untrusted output is limited to {PACKAGE_DIFF_DISPLAY_LINES} diagnostic lines; "
        "validation failures remain visible and fail closed.\n\n",
    ]
    if not surfaces:
        out.append("_Package plan/results unavailable; no API evidence is trusted._\n\n")
        return ''.join(out)
    for surface in surfaces:
        package = _inline(surface['package'])
        slug = _inline(surface['slug'])
        if surface['status'] == 'diffable':
            state = 'validated' if surface['trusted'] else 'untrusted — fails closed'
            raw = surface.get('text') or '(no diff output captured)'
            excerpt = (
                _curated_validated_diff(raw)
                if surface['trusted']
                else '\n'.join(raw.splitlines()[:PACKAGE_DIFF_DISPLAY_LINES])
            )
            evidence_cap = (
                VALIDATED_EVIDENCE_FENCE_BYTES if surface['trusted'] else 32 * 1024
            )
            if surface['trusted'] and len(excerpt.encode('utf-8')) > evidence_cap:
                raise ValueError(
                    f"Curated validated evidence for {slug} exceeds its bounded "
                    "fence without preserving every review-required row."
                )
        else:
            state = surface['status']
            raw = surface.get('reason') or '(no lifecycle reason recorded)'
            excerpt = '\n'.join(raw.splitlines()[:PACKAGE_DIFF_DISPLAY_LINES])
            evidence_cap = 32 * 1024
        fenced = fence(
            f"upstream-maf-diff-{slug}", excerpt, max_bytes=evidence_cap
        )
        out.append(f"### `{package}` (`{slug}`) — {state}\n\n")
        out.append(fenced + "\n")
    return ''.join(out)


def analysis_sections(verdict: dict, new: str) -> str:
    if verdict['additive']:
        safe_added, dropped = safe_members(verdict['added'])
        if safe_added:
            patterns = '\n'.join(f"- `{m}`" for m in safe_added)
            if dropped:
                patterns += (f"\n- _({dropped} further added member(s) omitted — names failed the "
                             f"identifier safety check; see the fenced package evidence above.)_")
        elif dropped:
            patterns = (f"_({dropped} added member(s) detected but omitted — names failed the "
                        f"identifier safety check; see the fenced package evidence above.)_")
        else:
            patterns = "_No new public members surfaced in the validated diffs._"
        return (
            "## Breaking Changes (requires human verification)\n\n"
            "None — this is an **additive** release: every expected package diff "
            "validated, no `.NET … [BREAKING]` entry was found, and no breaking "
            "lifecycle transition or API change was detected.\n\n"
            "## New Patterns\n\n"
            f"Additive members new in {new} (source-compatible — no action required to upgrade):\n\n"
            f"{patterns}\n\n"
            "## Obsolete APIs Added\n\n"
            f"None — no `[Obsolete]` deprecations or removed members detected for {new}.\n\n"
            "## Known Misalignments\n\n"
            f"None known for {new}.\n\n"
        )
    api_signals = []
    if verdict.get('summary_breaking', 0):
        api_signals.append(
            f"- `{verdict['summary_breaking']}` breaking API change row(s)"
        )
    if verdict.get('summary_potentially_breaking', 0):
        api_signals.append(
            f"- `{verdict['summary_potentially_breaking']}` potentially-breaking "
            "API change row(s)"
        )
    signal_block = (
        "Automated API summary signals:\n\n" + "\n".join(api_signals) + "\n\n"
        if api_signals else ""
    )
    return (
        "## Breaking Changes (requires human verification)\n\n"
        f"{signal_block}"
        "<!-- TODO: Review every package evidence block and lifecycle transition above -->\n\n"
        "## New Patterns\n\n"
        "<!-- TODO: Document any new recommended patterns from release notes -->\n\n"
        "## Obsolete APIs Added\n\n"
        f"<!-- TODO: Use MafRunCs0618Hunt against a project pinned to {new} and document findings -->\n\n"
        "## Known Misalignments\n\n"
        "<!-- TODO: Document any discrepancies between official docs and assembly behavior -->\n\n"
    )


def generate_version_guide(old: str, new: str) -> Path:
    raw_notes = read_file('release-notes.txt', '(no release notes available)').strip()
    notes = fence('upstream-maf-release-notes', raw_notes, max_bytes=32 * 1024)
    verdict = classify_from_files()
    banner = build_chain_banner(old, new)
    auto_body = (
        f"# MAF {new} Migration Guide (draft)\n\n"
        f"<!-- introduced: {new} | applies-to: {old}.x → {new}.x | deprecated-in: none -->\n\n"
        f"{banner}\n\n"
        f"{AUTO_START}\n\n"
        "> ⚠️ Auto-generated stub. Review before relying on it for migrations.\n\n"
        "## Versions\n\n"
        f"- Migrating from: `{old}`\n"
        f"- Migrating to: `{new}`\n\n"
        f"{lifecycle_markdown(verdict)}"
        f"{package_evidence_markdown(verdict)}"
        "## Release Notes Extract\n\n"
        f"{notes}\n\n"
        f"{analysis_sections(verdict, new)}"
        f"{AUTO_END}\n\n"
        f"{HUMAN_HEADING}\n\n"
        "<!-- Add notes, corrections, and refinements below this heading.\n"
        "     Content under this heading is PRESERVED across re-runs of the watcher. -->\n"
    )

    guide_path = Path(f'guides/maf-{new}-migration-guide.md')
    guide_path.parent.mkdir(parents=True, exist_ok=True)
    auto_prefix = auto_body[:auto_body.index(AUTO_END) + len(AUTO_END)] + "\n\n"
    if guide_path.exists():
        existing = guide_path.read_text(encoding='utf-8')
        end_idx = existing.find(AUTO_END)
        if end_idx != -1:
            after = existing[end_idx + len(AUTO_END):]
            h_idx = after.find(HUMAN_HEADING)
            human_section = after[h_idx:] if h_idx != -1 else f"{HUMAN_HEADING}\n\n"
        else:
            match = re.search(
                rf'^{re.escape(HUMAN_HEADING)}.*',
                existing,
                re.MULTILINE | re.DOTALL,
            )
            human_section = match.group(0) if match else f"{HUMAN_HEADING}\n\n"
        new_content = auto_prefix + human_section
    else:
        new_content = auto_body
    guide_path.write_text(new_content, encoding='utf-8')
    print(f"Wrote {guide_path} (idempotent — human additions preserved if present).")
    return guide_path


# =============================================================================
# Option C: regenerate the cumulative guide.
# =============================================================================

def build_cumulative_guide(guides_dir: Path | str = Path('guides')) -> str | None:
    """Build the deterministic cumulative guide without writing any files.

    Keeping construction pure lets verification compare the checked-in
    cumulative guide with the exact output that regeneration would produce.
    """
    files = list_per_version_guides(guides_dir)
    if not files:
        return None

    out = []
    out.append("# MAF Migration Guide — Cumulative\n\n")
    out.append(
        "<!-- AUTO-GENERATED — do NOT hand-edit. Regenerated on every "
        "maf-release-watcher run. Edit the per-version files instead. -->\n\n"
    )
    out.append(
        "This file is the **cumulative reference**: every per-version migration "
        "guide in ascending order, concatenated. It is the canonical \"start here\" "
        "doc for anyone migrating to the current MAF version from any earlier "
        "version.\n\n"
    )
    out.append(
        "**Where do I look?**\n\n"
        "- **Migrating from any version to the latest** → start at the top of this "
        "file, skim section by section.\n"
        "- **Targeted lookup** → ask Copilot to call `MafMigrationPath(currentVer, "
        "targetVer)` to get the ordered subset.\n"
        "- **Per-version source-of-truth edits** → edit `guides/maf-X.Y.Z-migration"
        "-guide.md` directly. This cumulative file regenerates from those on every "
        "release-watcher run.\n\n"
    )
    out.append("---\n\n")

    # Table of contents
    out.append("## Table of contents\n\n")
    for _, version, _ in files:
        anchor = f"migrating-to-maf-{version.replace('.', '-')}"
        out.append(f"- [Migrating to MAF {version}](#{anchor})\n")
    out.append("\n---\n\n")

    # Per-version sections
    for _, version, path in files:
        out.append(f"## Migrating to MAF {version}\n\n")
        out.append(
            f"<small>Source: [`guides/{path.name}`](./{path.name}) — edit there "
            f"for changes to this section.</small>\n\n"
        )
        content = path.read_text(encoding='utf-8')
        out.append(content.rstrip() + "\n\n")
        out.append("---\n\n")

    return ''.join(out)


def regenerate_cumulative_guide(
    guides_dir: Path | str = Path('guides'),
) -> Path | None:
    """Write the cumulative guide from the deterministic pure builder."""
    guides_dir = Path(guides_dir)
    files = list_per_version_guides(guides_dir)
    content = build_cumulative_guide(guides_dir)
    if content is None:
        print("(no per-version guides found — skipping cumulative regeneration)")
        return None

    cumulative_path = guides_dir / 'maf-current-migration-guide.md'
    cumulative_path.write_text(content, encoding='utf-8')
    print(
        f"Regenerated {cumulative_path} "
        f"({len(files)} per-version files concatenated: "
        f"{', '.join(v for _, v, _ in files)})"
    )
    return cumulative_path


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        '--cumulative-only',
        action='store_true',
        help=(
            'regenerate guides/maf-current-migration-guide.md only from '
            'checked-in per-version guides; do not read release artifacts'
        ),
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    if not args.cumulative_only:
        old = os.environ.get('OLD_VERSION', 'unknown')
        new = os.environ.get('NEW_VERSION', 'unknown')
        generate_version_guide(old, new)
    regenerate_cumulative_guide()
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
