---
name: cs0618-hunter
description: "Detects and fixes CS0618 obsolete API warnings in .NET builds using the compiler. While dotnet-inspect can now detect [Obsolete] at the overload level (issue #316 fixed), the compiler-based approach here is faster for project-wide scanning — one build pass finds all CS0618 patterns across all files."
---

# cs0618-hunter

The fastest way to detect all `[Obsolete]` API usages across a .NET codebase is the compiler. This skill provides the exact workflow.

> **Note:** `dotnet-inspect` issue #316 was resolved — it can now surface `[Obsolete]` at the individual overload level. For targeted per-member lookup, `dotnet-inspect member ... --index` is reliable. For project-wide scanning (find every CS0618 in a solution), this compiler-based approach is still the fastest: one `dotnet build` covers all files at once.

## When to Use This Skill

- During Phase 3 (Review) — mandatory step before declaring migration complete
- After any executor or workflow file is modified
- Whenever you suspect an overload might be obsolete but `dotnet-inspect` didn't flag it
- Before marking any task in the tracking table as `✅ Verified`

## The Workflow

### Step 1 — Run the CS0618 scan

```powershell
dotnet build 2>&1 | Select-String "warning CS0618"
```

> Run this from the solution root or the project directory.
> Every line that matches is a separate fix needed.

### Step 2 — Parse each warning

A CS0618 warning looks like:
```
src/Foo/Bar.cs(42,15): warning CS0618: 'WorkflowBuilder.AddFanInBarrierEdge(ExecutorBinding, IEnumerable<ExecutorBinding>)' is obsolete: 'Use AddFanInBarrierEdge(IEnumerable<ExecutorBinding>, ExecutorBinding) instead.'
```

Extract:
1. **File and line** — `src/Foo/Bar.cs(42,15)`
2. **Obsolete member** — the first quoted identifier
3. **Replacement hint** — text after `'is obsolete:'`

### Step 3 — Look up the fix pattern

Load `.github/skills/obsolete-api-registry/SKILL.md` and check if this warning is in the registry.

- **If found**: apply the exact replacement pattern from the registry entry
- **If not found**: read the obsolete message text — it usually contains the correct replacement signature. Add a new entry to `obsolete-api-registry/registry.yaml` so future migrations benefit.

### Step 4 — Apply the fix

Edit the file at the reported line. The fix is usually one of:
- Argument reorder (e.g., `AddFanInBarrierEdge`)
- Method rename
- Type rename

After editing, run:
```powershell
dotnet build 2>&1 | Select-String "warning CS0618"
```

Confirm the fixed file no longer appears in the output.

### Step 5 — Verify zero matches

```powershell
$remaining = dotnet build 2>&1 | Select-String "warning CS0618"
if ($remaining) {
    Write-Host "REMAINING CS0618 WARNINGS:"
    $remaining | ForEach-Object { Write-Host $_ }
} else {
    Write-Host "✅ Zero CS0618 warnings"
}
```

Do NOT mark Phase 3 complete until this outputs `✅ Zero CS0618 warnings`.

---

## Why Compiler-Based Detection?

`dotnet build | Select-String CS0618` finds every obsolete API call across every file in a solution in a single pass. `dotnet-inspect` (with issue #316 now resolved) can confirm whether a specific overload is marked `[Obsolete]` via `member ... --index` — useful for targeted inspection. The two tools complement each other: use this skill to discover, use `dotnet-inspect` to confirm.

---

## Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet build 2>&1 \| Select-String "warning CS0618"` | Find all obsolete API usages |
| `dotnet build 2>&1 \| Select-String "CS0618\|CS0246"` | Find both obsolete and removed APIs |
| `dotnet build 2>&1 \| Select-String "warning CS"` | All warnings (broader scan) |

Run the scan at minimum:
1. After completing all package updates
2. After all executor migrations
3. During Phase 3 review (mandatory)
4. Before marking the migration as done
