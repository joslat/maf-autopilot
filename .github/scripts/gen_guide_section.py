#!/usr/bin/env python3
"""Creates a per-version migration-guide stub at guides/maf-<NEW_VERSION>-migration-guide.md.

Reads OLD_VERSION, NEW_VERSION from environment variables.
Called by .github/workflows/maf-release-watcher.yml.

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

old = os.environ.get('OLD_VERSION', 'unknown')
new = os.environ.get('NEW_VERSION', 'unknown')


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


def list_per_version_guides():
    """
    Returns a list of (semver_tuple, version_string, path) sorted ascending.
    Excludes the cumulative file itself and any non-matching filenames.
    """
    guides_dir = Path('guides')
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


diff_lines = read_file('diff-core.txt', '(diff not available)').splitlines()
# Trim to a reasonable length — full diffs can be huge.
raw_diff = '\n'.join(diff_lines[:120])
# Phase 2.G fixup (Finding 1) — fence the dotnet-inspect diff. The diff is the
# sibling input to release notes — same TA-1 threat (malicious upstream NuGet
# author can put HTML-comment-disguised payloads or stray triple-backticks in
# a public-API XML doc or type name, which dotnet-inspect surfaces verbatim).
# Without the fence, those flow into the AI-fill issue body alongside the
# (already-fenced) release notes.
diff = fence('upstream-maf-diff-core', raw_diff, max_bytes=32 * 1024)
raw_notes = read_file('release-notes.txt', '(no release notes available)').strip()
# Phase 2.2 — fence the release notes. 32 KB cap is generous: release notes
# typically run a few KB; anything over 32 KB is almost certainly an attempt
# to flood the guide / downstream AI-fill context.
notes = fence('upstream-maf-release-notes', raw_notes, max_bytes=32 * 1024)

AUTO_START = '<!-- AUTO-GENERATED START — anything between AUTO-GENERATED START and AUTO-GENERATED END is overwritten on re-run -->'
AUTO_END = '<!-- AUTO-GENERATED END -->'
HUMAN_HEADING = '## Human additions'

banner = build_chain_banner(old, new)

auto_body = (
    f"# MAF {new} Migration Guide (draft)\n\n"
    f"<!-- introduced: {new} | applies-to: {old}.x → {new}.x | deprecated-in: none -->\n\n"
    f"{banner}\n\n"
    f"{AUTO_START}\n\n"
    f"> ⚠️ Auto-generated stub. Review before relying on it for migrations.\n\n"
    f"## Versions\n\n"
    f"- Migrating from: `{old}`\n"
    f"- Migrating to: `{new}`\n\n"
    f"## Diff Summary (first 120 lines of `dotnet-inspect` output)\n\n"
    f"{diff}\n\n"
    f"## Release Notes Extract\n\n"
    f"{notes}\n\n"
    f"## Breaking Changes (requires human verification)\n\n"
    f"<!-- TODO: Review the diff above and list breaking changes here -->\n\n"
    f"## New Patterns\n\n"
    f"<!-- TODO: Document any new recommended patterns from release notes -->\n\n"
    f"## Obsolete APIs Added\n\n"
    f"<!-- TODO: Use MafRunCs0618Hunt against a project pinned to {new} and document findings -->\n\n"
    f"## Known Misalignments\n\n"
    f"<!-- TODO: Document any discrepancies between official docs and assembly behavior -->\n\n"
    f"{AUTO_END}\n\n"
    f"{HUMAN_HEADING}\n\n"
    f"<!-- Add notes, corrections, and refinements below this heading.\n"
    f"     Content under this heading is PRESERVED across re-runs of the watcher. -->\n"
)

guide_path = Path(f'guides/maf-{new}-migration-guide.md')
guide_path.parent.mkdir(parents=True, exist_ok=True)

# Anchor the auto/human boundary to the script-controlled AUTO_END sentinel,
# NOT the bare HUMAN_HEADING (task 4.5). Upstream release notes / diffs are
# fenced (llm_fencing strips HTML comments), so an attacker can inject a literal
# "## Human additions" line into the notes — which would hijack a HUMAN_HEADING
# split on BOTH the existing file (resurrecting fenced content as "human notes")
# and the fresh auto_body (truncating the auto block). They cannot reproduce the
# AUTO_END HTML comment, because the fence strips it. So split on AUTO_END.
auto_prefix = auto_body[:auto_body.index(AUTO_END) + len(AUTO_END)] + "\n\n"

if guide_path.exists():
    existing = guide_path.read_text(encoding='utf-8')
    end_idx = existing.find(AUTO_END)
    if end_idx != -1:
        after = existing[end_idx + len(AUTO_END):]
        h_idx = after.find(HUMAN_HEADING)
        human_section = after[h_idx:] if h_idx != -1 else f"{HUMAN_HEADING}\n\n"
    else:
        # Legacy file with no AUTO_END marker — fall back to the heading split.
        match = re.search(rf'^{re.escape(HUMAN_HEADING)}.*', existing, re.MULTILINE | re.DOTALL)
        human_section = match.group(0) if match else f"{HUMAN_HEADING}\n\n"
    new_content = auto_prefix + human_section
else:
    new_content = auto_body

guide_path.write_text(new_content, encoding='utf-8')
print(f"Wrote {guide_path} (idempotent — human additions preserved if present).")


# =============================================================================
# Option C: regenerate the cumulative guide.
# =============================================================================

def regenerate_cumulative_guide():
    """
    Writes guides/maf-current-migration-guide.md by concatenating every
    maf-<X.Y.Z>-migration-guide.md in ascending version order.

    The cumulative file is OVERWRITTEN on every run — humans should NOT hand-
    edit it; edit the per-version files instead. A README banner at the top
    of the cumulative file states this.
    """
    files = list_per_version_guides()
    if not files:
        print("(no per-version guides found — skipping cumulative regeneration)")
        return

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
        anchor = f"migrating-to-maf-{version.replace('.', '-')}"
        out.append(f"## Migrating to MAF {version}\n\n")
        out.append(
            f"<small>Source: [`{path.as_posix()}`](./{path.name}) — edit there "
            f"for changes to this section.</small>\n\n"
        )
        content = path.read_text(encoding='utf-8')
        out.append(content.rstrip() + "\n\n")
        out.append("---\n\n")

    cumulative_path = Path('guides/maf-current-migration-guide.md')
    cumulative_path.write_text(''.join(out), encoding='utf-8')
    print(
        f"Regenerated {cumulative_path} "
        f"({len(files)} per-version files concatenated: "
        f"{', '.join(v for _, v, _ in files)})"
    )


regenerate_cumulative_guide()
