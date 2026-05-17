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

4. **`guide_section`** — section number from the existing 1.3.0
   guide if there's a clear parallel pattern (look at the headings of
   `guides/maf-1.3.0-migration-guide.md`). If there's NO parallel
   section (because the change touches a surface that didn't exist
   in 1.3.0), use **`N/A`** — not `TBD`, not `TODO`. Never
   invent section numbers.

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
these break):

- [ ] `docs/compatibility-matrix.md` has exactly ONE `**{{TARGET}}**` row,
  not two. Run `grep -c '^| \*\*{{TARGET}}\*\*' docs/compatibility-matrix.md`
  — must print `1`.
- [ ] `## Version Tracking` section's "Current tracked version" line says
  `**`{{TARGET}}`**`, not an older version.
- [ ] `guides/maf-{{TARGET}}-migration-guide.md` exists and its
  AUTO-GENERATED sections are filled (no `TODO`).
- [ ] `last-updated:` date in the compatibility-matrix HTML comment
  is today's UTC date.

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

Also at the top of `docs/compatibility-matrix.md` there is an HTML
comment of the form
`<!-- auto-updated-by: maf-release-watcher | last-updated: YYYY-MM-DD -->`.
Update the date to today (UTC).

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
markers:

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
