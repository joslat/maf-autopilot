---
name: cs0618-hunter
description: "Detects and fixes CS0618 obsolete API warnings in .NET builds. Use this skill — NOT dotnet-inspect — whenever you need to find deprecated API usage. dotnet-inspect does not flag [Obsolete] at the individual overload level; only the compiler does. This skill provides the exact workflow for finding, diagnosing, and fixing every CS0618 warning."
---

# cs0618-hunter

The **only reliable way** to detect `[Obsolete]` API usage in a .NET codebase is the compiler. This skill provides the exact workflow.

> **Root cause:** `dotnet-inspect` does not surface `[Obsolete]` at the individual overload level. This is a known upstream limitation tracked at [richlander/dotnet-inspect #316](https://github.com/richlander/dotnet-inspect/issues/316). Until that is resolved, the compiler is the only authoritative source — which is exactly what this skill uses.

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

## Why Not Use dotnet-inspect?

`dotnet-inspect member WorkflowBuilder` lists all overloads of `AddFanInBarrierEdge` but does **not** mark either overload as obsolete. Both appear identical in the listing. The `[Obsolete]` attribute is only visible in the single-overload `--index` detail view — but you must already know *which* overload to suspect before you can look it up.

This is a known limitation filed as [richlander/dotnet-inspect #316](https://github.com/richlander/dotnet-inspect/issues/316).

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
