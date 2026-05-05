---
name: migration-plan-creator
description: "Generates a complete, ready-to-execute MAF migration plan (migration-plan.md) for any .NET codebase. Use this skill when no migration plan exists yet, or when the existing plan needs to be regenerated after a codebase audit. Provides the canonical template, task ID conventions, and the process for populating each task row from an audit scan."
---

# migration-plan-creator

This skill produces a `src/docs/migration-plan.md` that the **MAF Migration Agent** can execute immediately — no manual editing needed.

## When to Use This Skill

- Starting a new MAF migration with no existing plan
- Regenerating a plan after the `maf-auditor` agent has completed a codebase scan
- Creating a plan for a different codebase (not the reference implementation)

## How to Generate a Plan

### Step 1 — Gather inputs

Before filling the template, you need:

1. **Package inventory** — current versions from all `.csproj` files
2. **Source pattern scan results** — from `maf-auditor` Phase C (what old patterns exist, which files, how many occurrences)
3. **API diff output** — from `dotnet-inspect diff` (Phase B)
4. **CS0618 pre-check results** — from `obsolete-api-registry` cross-reference (Phase D)
5. **Fan-out topology** — from `fan-out-validator` pre-check (Phase E)

### Step 2 — Assign Task IDs

Tasks are numbered by phase. Use this phase → task ID mapping:

| Phase | Tasks | Description |
|-------|-------|-------------|
| 1 | T1.1, T1.2, … | Package version updates |
| 2 | T2.1, T2.2, … | Namespace changes |
| 3 | T3.1, T3.2, … | Instructions/Tools placement |
| 4 | T4.1, T4.2, … | Executor migration |
| 5 | T5.1, T5.2, … | Session migration |
| 6 | T6.1, T6.2, … | Response deserialization |
| 7 | T7.1, T7.2, … | Streaming rename |
| 8 | T8.1, T8.2, … | Event type rename |
| 9 | T9.1, T9.2, … | DevUI/Hosting guard |
| 10 | T10.1, T10.2, … | Verification grep checks |
| 11 | T11.1, T11.2 | Polish and done |

### Step 3 — Fill the template

See `template.md` in this directory. Every task row must have:
- Exact file path (relative to repo root)
- Exact before/after code snippet (copy from source — do not paraphrase)
- Guide section reference from `maf-migration-guide` SKILL.md section index
- `% Done`: `0%`
- `Verified`: empty

### Step 4 — Order tasks by dependency

Tasks MUST be ordered so that:
- Packages are updated before any code changes
- Namespaces are updated before executor migration
- Source generator package is added before executor classes are created
- Fan-out executor handlers are migrated before fan-in edges are added
- DevUI guard is last (it depends on all other tasks completing)

### Step 5 — Add verification checks (Phase 10 section)

For every old pattern found in the audit, add a verification grep:
```
Select-String -Path "src/**/*.cs" -Pattern "ReflectingExecutor" -Recurse
```
Each grep must return 0 matches after all tasks in the corresponding phase are complete.

---

## Task Row Format

```markdown
| # | Task ID | Description | % Done | Verified | Notes |
|---|---------|-------------|--------|----------|-------|
| 1 | T1.1    | Update Microsoft.Agents.AI to 1.3.0 in ClaimsCore.Workflows.csproj | 0% | | |
```

The `Verified` column gets `✅` only after `dotnet build` passes with the change applied.

---

## What Makes a Good Plan vs. a Bad Plan

| Good plan | Bad plan |
|-----------|---------|
| Before: `agent.GetNewThread()` / After: `await agent.CreateSessionAsync(ct)` | Before: "old session API" / After: "new session API" |
| File: `src/ClaimsCore.Workflows/Workflows/ClaimsFraudDetectionWorkflow.cs` line ~42 | File: "the workflow file" |
| Occurrence count: 3 (lines 42, 87, 134) | Occurrence count: "several" |
| Guide section: 6 | Guide section: "sessions" |
| Risk: High — runtime silent failure if not done before fan-in migration | Risk: not assessed |

Be specific. The migration agent will apply changes mechanically — it needs exact before/after text.
