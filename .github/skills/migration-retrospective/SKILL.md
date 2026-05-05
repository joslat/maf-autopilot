---
name: migration-retrospective
description: "Post-migration learning skill. After a migration completes, reads the migration plan notes, build history, and surprises to improve the registry, guide, and constraint rules. Invoked as the final step of any migration. This is how the toolkit gets smarter with each use."
---

# migration-retrospective

## Purpose

Every migration surfaces surprises — patterns the plan didn't predict, docs that were wrong, APIs that behaved differently than expected. This skill captures those surprises systematically and feeds them back into the toolkit's knowledge base.

After 10 migrations, the `obsolete-api-registry.yaml` covers real-world CS0618 patterns that aren't in any official docs. After 20, the Known Misalignments section becomes the most battle-tested part of the guide. This is the compound effect.

---

## When to Use This Skill

- Immediately after a migration completes (`T11.2 — Done`)
- Before closing any migration branch or PR
- When a migration hit a surprise that wasn't in the plan

---

## The Retrospective Workflow

### Step 1 — Read the completed migration plan

Read `src/docs/migration-plan.md` — specifically:
- The **Notes** column of each task row (where surprises were recorded during execution)
- Any tasks with `% Done: 100%` that have non-empty notes
- The **Risk Register** section — were any predicted risks wrong or missing?

### Step 2 — Read build error history (if available)

If terminal output is available from the migration session:
```powershell
# Look for unexpected build errors in terminal history
# Focus on: CS0246 errors not predicted by the plan, runtime exceptions, CS0618 not in registry
```

Key questions:
- Were there CS0246 errors for types/members NOT in the migration plan?
- Were there CS0618 warnings NOT in the registry?
- Were there silent runtime failures detected by smoke tests?

### Step 3 — Identify surprises

For each surprise (unexpected error, wrong doc, new pattern):

| Surprise type | Action |
|--------------|--------|
| CS0618 not in registry | Add to `registry.yaml` |
| CS0246 for type/member not in plan | Add to registry + add to breaking changes table in `maf-constraints.instructions.md` |
| Official doc was wrong | Add to Known Misalignments in `maf-1.3.0-migration-guide.md` |
| Silent runtime failure | Add to fan-out-validator or fan-in-static-analyzer skill |
| `dotnet-inspect` missed something | Note in `dotnet-inspect/SKILL.md` limitations section |
| New hard constraint discovered | Add to `maf-constraints.instructions.md` Hard Constraints section |

### Step 4 — Update registry.yaml

For each new CS0618 or unexpected breaking change:

```yaml
  - id: MAF130-<AREA>-<NNN>
    package: Microsoft.Agents.AI[.Workflows]
    version_introduced: "1.3.0"
    type: <FullyQualifiedTypeName>
    method: <MethodName>
    obsolete_signature: "<exact signature that triggered CS0618>"
    replacement_signature: "<correct replacement>"
    argument_order_change: <true/false>
    fix_description: "<what to change>"
    example_before: |
      <code that triggered the warning>
    example_after: |
      <corrected code>
    cs_warning: CS0618
    guide_section: "<section>"
    dotnet_inspect_detectable: false
    notes: >
      Discovered during: <migration name / date>.
      Was NOT predicted by migration plan.
      <Any additional context>
```

### Step 5 — Update Known Misalignments

For each official doc that was wrong, add to `guides/maf-1.3.0-migration-guide.md` → "Known Misalignments" section:

```markdown
| <What the docs claim> | <What reality is> | <Discovery source: migration name, date> |
```

### Step 6 — Update maf-constraints.instructions.md

For any new hard constraint discovered (e.g., "never use X in production", "always call Y before Z"):

Add to the **Hard Constraints** table in `.github/instructions/maf-constraints.instructions.md`.

Format:
```markdown
- **NEVER** <constraint description> — discovered in <migration name>
```

### Step 7 — Check dotnet-inspect issue queue

For each surprise that dotnet-inspect should have caught but didn't:
- Was it the known `[Obsolete]` overload detection gap (issue #316)?
- Or is it a new gap worth filing separately?

If new → add a note in `.github/skills/dotnet-inspect/SKILL.md` under "Critical Limitation".

### Step 8 — Output the retrospective summary

```markdown
## Migration Retrospective Summary

**Migration:** <codebase name>
**Date:** <YYYY-MM-DD>
**Source version:** <old MAF version>
**Target version:** <new MAF version>

### Surprises Found

| # | Type | Description | Action taken |
|---|------|-------------|-------------|
| 1 | CS0618 not in registry | `SerializeSession` was obsolete — docs showed sync version | Added MAF130-SESSION-001 to registry |
| 2 | Doc misalignment | `RunAsync<T>()` not on `IAIAgent` interface, only on `ChatClientAgent` | Added to Known Misalignments |
| 3 | Silent runtime | PropertyTheftFanOutExecutor returned non-generic ValueTask | Added to fan-out-validator SKILL.md |

### Registry Updates
- Added N new entries to `registry.yaml`

### Guide Updates
- Added N new Known Misalignments entries

### Constraint Updates
- Added N new Hard Constraints

### For the Next Migration
> Key things to watch out for in the next migration of a similar codebase:
> [list of the most impactful surprises]
```

---

## The Compound Effect

Over time:
- **Registry:** Grows from 1 entry to dozens — covering real-world patterns that official docs miss
- **Known Misalignments:** Becomes the most reliable section of the guide — battle-tested across real codebases
- **Constraints:** Hard rules that prevent re-discovering the same mistakes
- **Skills:** Fan-out validator and static analyzer improve with each failure mode documented

Each migration makes the next one faster and more reliable.
