@copilot — please fill in the TODO placeholders the release-watcher
left for **MAF {{TARGET}}**.

## Context

The watcher just committed an auto-generated update with:

- **`.github/skills/maf-obsolete-api-registry/registry.yaml`** — new entries
  whose `id` starts with **`MAF{{DIGITS}}-`** (e.g. `MAF{{DIGITS}}-AGENT-001`).
  Each entry has TODO fields: `fix_description`, `example_before`,
  `example_after`, `guide_section`. Each entry's `notes:` field
  already contains the raw `dotnet-inspect` diff text — use that as
  the source of truth for what changed.

- **`docs/compatibility-matrix.md`** — find the row containing
  `**{{TARGET}}**` at the top of the data table. Cells marked
  `unknown` need real version constraints.

- **`guides/maf-{{TARGET}}-migration-guide.md`** — sections under the
  `AUTO-GENERATED START / END` markers say `TODO`. Fill them.

## Style anchor

The existing well-filled entries are the canonical format. Match their
tone, length, and YAML structure exactly:

- `MAF130-FAN-IN-001` — SIGNATURE-CHANGED template
- `MAF130-SESSION-001` — also see the **Phase T correction** note
  for the format of corrections (use that template if your filling
  reveals the registry's diff doesn't match the public surface).
- `MAF130-THREAD-001` — TYPE-REMOVED template
- `MAF130-EXEC-001` — BEHAVIOR-CHANGED template

## Step 1 — CATEGORISE FIRST (do this before filling anything)

For EACH entry with `id: MAF{{DIGITS}}-*`, pick **exactly ONE** of
these change categories and use the matching template below. Don't
guess; if the structural diff is ambiguous, default to BEHAVIOR-CHANGED
and explain your reasoning in `notes:`.

| Category | When | What the example pair should show |
|---|---|---|
| **TYPE-REMOVED** | The diff shows a type going away entirely. cs_warning = CS0246. | example_before uses old type; example_after uses the new one. |
| **METHOD-RENAMED** | Same type, member renamed (no signature change beyond the name). | example_before/after differ only on the call-site name. |
| **SIGNATURE-CHANGED** | Same method name, different param list or return type. cs_warning = CS0618 (if [Obsolete] kept) or CS1503/CS0019. | Show OLD arg list / return → NEW arg list / return. |
| **BEHAVIOR-CHANGED** | Method compiles fine but does something different at runtime. cs_warning = RUNTIME_SILENT. | example_before is "looks fine, broken at runtime"; example_after is the safe alternative. |
| **ATTRIBUTE-REMOVED** | A [SomeAttribute] decorator no longer exists. cs_warning = CS0246. | example_before has the attribute; example_after just deletes it. |

## Step 2 — Per-entry fill

For each entry, fill these fields (only — touch nothing else):

1. **`fix_description`** — one concise sentence (≥ 20 chars).
   Pattern depends on category:
   - TYPE-REMOVED: "Replace `OldType` with `NewType`."
   - METHOD-RENAMED: "Rename `X.OldName` to `X.NewName`."
   - SIGNATURE-CHANGED: "Change `X.Y(<old>)` to `X.Y(<new>)`."
   - BEHAVIOR-CHANGED: "Replace <silent-broken pattern> with <correct pattern>."
   - ATTRIBUTE-REMOVED: "Delete `[OldAttribute]` — gone in {{TARGET}}; no replacement."

2. **`example_before`** — minimal C# call site (3-8 lines) showing
   the obsolete signature being used. Use the `obsolete_signature`
   field as the basis. Use YAML block-literal style (`|`).

   **Self-check before submitting**: parse this fragment in your head
   — does it have balanced `{}` and `()`, valid statement
   terminators, and at least one identifier from the entry's `type`
   field?

3. **`example_after`** — corrected {{TARGET}} replacement (3-8 lines),
   also `|` block-literal.

   **Self-check**: must be DIFFERENT from example_before (an identical
   pair is a non-migration); must NOT use any identifier that the
   diff says was removed.

4. **`guide_section`** — MUST be ONE of these two values, no third option:
   - The literal string **`N/A`** (use this when the change touches a
     surface that didn't exist in 1.3.0 — most new features).
   - A section ID that EXISTS VERBATIM as a heading in
     `guides/maf-1.3.0-migration-guide.md`.

   **BEFORE setting any guide_section to a number, RUN THIS GREP:**

   ```bash
   grep -E "^##+ <YOUR_SECTION_ID>\\." guides/maf-1.3.0-migration-guide.md
   ```

   **If grep returns NOTHING, the section doesn't exist — use `N/A`.**
   NEVER invent a section number. This was the #1 cause of fill rejection
   on PR #36 (Claude set `guide_section: "1"` for an EvaluateAsync change
   that has no parallel in the 1.3.0 guide). The cross-file consistency
   check will REJECT invented section IDs deterministically.

5. **`applies_to_codebases`** _(optional, Phase W.A field)_ — only
   add this if the diff's API surface DOES NOT EXIST on a public MAF
   NuGet you can verify (run `dotnet-inspect type --package
   Microsoft.Agents.AI@<version>` for each version 1.0.0 / 1.1.0 /
   1.2.0 / 1.3.0 if uncertain). Set to `"pre-1.0.0"` and add a
   Phase W.A correction paragraph to `notes:` documenting what you
   verified. Most entries WON'T need this field.

## Step 3 — Self-verification checklist (do before opening PR)

Run through this checklist on every entry you filled:

- [ ] Every required field filled (no remaining `TODO`).
- [ ] `example_before` ≠ `example_after` (a non-noop migration).
- [ ] `example_before` doesn't reference a type the diff said was
  removed — only the example_after should.
- [ ] Every `example_*` has balanced `{}` and `()`.
- [ ] Every `fix_description` is at least 20 characters.
- [ ] No invented section numbers in `guide_section` — only existing
  section IDs from the guide, OR the literal string `N/A`.

**Cross-file checks** (the rung-1 verifier WILL fail your PR if any of
these break — these are ENFORCED, not just suggestions):

Run these greps and confirm the expected output for each:

```bash
# 1. Exactly ONE {{TARGET}} row in compat-matrix — must print 1:
grep -c "^| \\*\\*{{TARGET}}\\*\\*" docs/compatibility-matrix.md

# 2. Version Tracking section is current — must print 1:
grep -c "Current tracked version: \\*\\*\\`{{TARGET}}\\`\\*\\*" docs/compatibility-matrix.md

# 3. last-updated date is today's UTC — must print 1:
grep -c "last-updated: $(date -u +%Y-%m-%d)" docs/compatibility-matrix.md

# 4. Migration guide exists for {{TARGET}} — must print 0 (no error):
ls guides/maf-{{TARGET}}-migration-guide.md > /dev/null 2>&1; echo $?

# 5. AUTO-GENERATED stub warnings preserved in {{TARGET}} guide — must print >= 1:
grep -c "Auto-generated stub" guides/maf-{{TARGET}}-migration-guide.md

# 6. No invented guide_section values — must print no MISSING lines:
grep -E "^\\s+guide_section:" .github/skills/maf-obsolete-api-registry/registry.yaml \
  | grep -v 'guide_section:.*"\?N/A"\?' \
  | awk '{gsub(/"/, "", $2); print $2}' | sort -u | while read sec; do
    [ -z "$sec" ] && continue
    grep -qE "^##+ ${sec}\\." guides/maf-1.3.0-migration-guide.md \
      || echo "MISSING: guide_section ${sec} not in 1.3.0 guide — should be N/A"
  done
```

If any of these fail, the verify check WILL block this PR from merging.
Fix locally and push before opening the PR for human review.

The PR will be automatically checked against
`maf-autopilot verify-registry` by the
`.github/workflows/maf-ai-fill-verify.yml` workflow. The check
enforces the same items above. If any fail, the check fails and the
PR is blocked from auto-merge.

## What to fill — compatibility matrix

The watcher has ALREADY inserted a placeholder row at the top of the
`## Compatibility Table` in `docs/compatibility-matrix.md`. It looks like:

```
| **{{TARGET}}** | `>= unknown` | `>= 8.0` | `>= unknown` | `{{TARGET}}` | Auto-detected — verify versions in PR review. |
```

**UPDATE THIS EXISTING ROW IN-PLACE. DO NOT ADD A NEW ROW.** A common
failure mode (observed on the 2026-05-17 live-fire of PR #26) is the bot
adding a new filled row *below* the placeholder, leaving the table with
two `**{{TARGET}}**` rows. The verifier flags duplicate rows as FAIL.

For the row `**{{TARGET}}**`:

- **`Microsoft.Extensions.AI`** column: replace `unknown` with the
  actual version constraint if the MAF release notes mention a bump.
  Otherwise leave as `unknown` and add a `<!-- TODO: verify from MAF
  csproj -->` HTML comment in the Notes column.
- **`Azure.AI.OpenAI`** column: same logic.

**CRITICAL — last-updated date.** At the top of
`docs/compatibility-matrix.md` there is an HTML comment of the form
`<!-- auto-updated-by: maf-release-watcher | last-updated: YYYY-MM-DD -->`.

**YOU MUST UPDATE this date to today's UTC date.** Get it with:

```bash
date -u +%Y-%m-%d
```

A stale date FAILS the cross-file consistency check. This was the #2 cause
of fill rejection on PR #36 (Claude left the date at 2026-05-12 when the
PR was opened 2026-05-17). Update it.

## What to fill — Version Tracking section

Lower in `docs/compatibility-matrix.md` there is a `## Version Tracking`
section that contains a line:

```
Current tracked version: **`X.Y.Z`** (see `.maf-version`)
```

**UPDATE THIS LINE to match `.maf-version` = `{{TARGET}}`.** A common
failure mode is leaving this line at the previous version. The cross-file
consistency check flags this as FAIL.

## Cross-file thoroughness — DO ALL of these

The watcher commit touched MULTIPLE files. Before opening the PR, verify
EACH of these is consistent with each other:

1. `.maf-version` — already set to `{{TARGET}}` (don't touch).
2. `.github/skills/maf-obsolete-api-registry/registry.yaml` — fill new
   entries (Step 2 above).
3. `docs/compatibility-matrix.md`:
   - Top row updated, NOT duplicated.
   - `last-updated:` date updated.
   - Version Tracking section's "Current tracked version" updated.
4. `guides/maf-{{TARGET}}-migration-guide.md` — fill the AUTO-GENERATED
   sections (Step "What to fill — migration guide" above).

Run a final consistency grep before opening the PR:

```bash
# Should find ONE row for {{TARGET}}, not two:
grep -c '^| \*\*{{TARGET}}\*\*' docs/compatibility-matrix.md

# Should print {{TARGET}}, not an older version:
grep 'Current tracked version' docs/compatibility-matrix.md
```

## What to fill — migration guide

In `guides/maf-{{TARGET}}-migration-guide.md`, fill the four
`TODO`-marked sections under the `AUTO-GENERATED START / END`
markers.

**CRITICAL — preserve the stub warning lines.** Each AUTO-GENERATED
section starts with a line like:

```
> ⚠️ Auto-generated stub — this section is regenerated by tooling and may be overwritten on re-run.
```

**KEEP this warning line even after you fill the section with real
content.** The warning is still truthful: re-running the watcher WILL
overwrite this content next cycle. Removing it creates risk of silent
data loss. Other guides (1.4.0, 1.5.0) keep these warnings — match that
pattern. Look at `guides/maf-1.5.0-migration-guide.md` for the canonical
shape: stub warning, then filled content under it.

The four `TODO`-marked sections:

- **Breaking Changes**: one bullet per registry entry. Format:
  `- <symbol>: <fix_description>`.

- **New Patterns**: from the release notes (visible at
  https://github.com/microsoft/agent-framework/releases/tag/dotnet-{{TARGET}}
  or scroll to the "Release Notes Extract" section of the guide
  file itself, which already has them embedded), highlight new types
  / features worth knowing. 3-7 bullets.

- **Obsolete APIs Added**: leave as a TODO unless the release notes
  explicitly call out new `[Obsolete]` markings. Add:
  `<!-- TODO: run \`MafRunCs0618Hunt\` against a project pinned to {{TARGET}} to populate. -->`

- **Known Misalignments**: `None documented yet.` unless the
  release notes call out doc/assembly mismatches.

## Also clean up

The migration guide's "Diff Summary" code block may contain garbled
fragments (e.g. literal `hape` or `d` from terminal escape codes
in `dotnet-inspect` output). If present, **replace the code block
with a clean human-readable bulleted summary** derived from the
release notes.

## Constraints

- **DO NOT** modify entries whose `id` does NOT start
  `MAF{{DIGITS}}-`. Earlier-version entries are off-limits.
- **DO NOT** touch any `## Human additions` section. Humans own
  that section across re-runs of the watcher.
- **DO NOT** invent facts. When uncertain, leave `TODO` /
  `unknown` with an explanation. The toolkit deliberately favours
  "honest TODO" over "confident guess."

## When you're done

Open the PR with title `chore: AI-filled TODOs for MAF {{TARGET}}` and
a brief summary of what you filled. Label it
`maf-release,ai-fill`. The maintainer reviews + merges (or has
auto-merge configured on the label).

---

*This issue was auto-generated by `.github/workflows/maf-ai-fill-todos.yml`. To re-fire manually: `gh workflow run maf-ai-fill-todos.yml -f target_version={{TARGET}}`.*
