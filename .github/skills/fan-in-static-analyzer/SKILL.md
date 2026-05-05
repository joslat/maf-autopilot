---
name: fan-in-static-analyzer
description: "Static analysis of fan-out/fan-in workflow topology without running code. Finds every fan-out executor where the handler returns void or non-generic ValueTask — the silent failure that builds green but starves the fan-in barrier. Use as a mandatory step during migration review and whenever workflow topology changes."
---

# fan-in-static-analyzer

## Purpose

The `fan-out-validator` skill tells you WHAT to check. This skill does the actual static analysis — walking the workflow builder code to map executor bindings, trace handler return types, and verify fan-in argument order — producing a definitive pass/fail verdict without running anything.

This skill is the systematic, code-reading version of fan-out validation. The `fan-out-validator` skill provides the conceptual protocol; this skill provides the step-by-step code analysis procedure.

---

## The Silent Failure Pattern (recap)

```
Build:   ✅ green — no errors, no warnings
Tests:   ✅ pass — if tests don't assert fan-in output
Runtime: ❌ fan-in barrier never fires — workflow exits cleanly with missing results
```

A fan-out executor handler returning `void` or `ValueTask` (non-generic) produces **no output message**. The fan-in barrier expects N messages and receives 0. The workflow exits cleanly. There is no exception. This is the hardest class of MAF bug to detect.

---

## Analysis Workflow

### Step 1 — Find all workflow builder files

```powershell
# Find all files containing workflow topology code
Select-String -Path "src/**/*.cs" -Pattern "AddFanOutEdge|AddFanInBarrierEdge" -Recurse -List | Select-Object Path
```

### Step 2 — For each workflow file, extract the fan-out topology

Read the workflow builder class. For each `AddFanOutEdge` call, record:

```
AddFanOutEdge(sourceExec, fanOutExec1, fanOutExec2, fanOutExec3)
                ↑                ↑            ↑            ↑
           source executor  branch 1     branch 2     branch 3
```

Also record the corresponding `AddFanInBarrierEdge` call that aggregates these branches:

```
AddFanInBarrierEdge(new[] { fanOutExec1, fanOutExec2, fanOutExec3 }, aggregatorExec)
                    ← sources (must be all fan-out branches) →           target
```

### Step 3 — Resolve each fan-out executor to its class

For each executor binding (e.g., `fanOutExec1`), trace the binding to its class:

```csharp
// Example: find where fanOutExec1 was created
var fanOutExec1 = builder.AddExecutor<PropertyTheftFanOutExecutor>();
//                                    ↑ this is the class to inspect
```

### Step 4 — Check handler return types (THE CRITICAL CHECK)

For each resolved executor class, find the `[MessageHandler]`-annotated method:

```csharp
// SEARCH PATTERN: find the [MessageHandler] method in each executor class
Select-String -Path "src/**/<ClassName>.cs" -Pattern "\[MessageHandler\]" -Recurse -A 1
```

Evaluate the return type on the line after `[MessageHandler]`:

| Return type | Verdict | Action |
|-------------|---------|--------|
| `async ValueTask<T> HandleAsync(...)` | ✅ **CORRECT** | None |
| `ValueTask<T> HandleAsync(...)` | ✅ **CORRECT** | None |
| `async ValueTask HandleAsync(...)` | ❌ **BROKEN** | Fix return type → `ValueTask<T>` |
| `ValueTask HandleAsync(...)` | ❌ **BROKEN** | Fix return type → `ValueTask<T>` |
| `async Task<T> HandleAsync(...)` | ⚠️ **WARN** | Use `ValueTask<T>` (Task works but ValueTask preferred) |
| `void HandleAsync(...)` | ❌ **BROKEN** | Fix immediately |

### Step 5 — Verify fan-in argument order

For each `AddFanInBarrierEdge` call, verify:

```csharp
// CORRECT: sources first (IEnumerable), target last (single ExecutorBinding)
builder.AddFanInBarrierEdge(
    new[] { exec1, exec2, exec3 },  // ← sources FIRST
    aggregatorExec);                 // ← target LAST

// BROKEN (CS0618): target first, sources last (obsolete overload)
builder.AddFanInBarrierEdge(aggregatorExec, exec1, exec2, exec3);  // ← WRONG ORDER
```

If the wrong order is found → this is registry entry `MAF130-FAN-IN-001`. Apply the fix.

### Step 6 — Verify branch coverage

Count fan-out branches vs. fan-in sources:

```
Fan-out adds 3 branches: exec1, exec2, exec3
Fan-in barrier sources must include: exec1, exec2, exec3 (all 3)

If fan-in sources = [exec1, exec2] only → exec3 output is silently discarded
```

For each fan-in call, verify that **every** executor in the fan-out chain appears in the sources list.

---

## Report Format

Produce a structured report:

```markdown
## Fan-Out Topology Analysis Report

### Workflow: <ClassName> (<file path>)

#### Fan-out chain: <sourceExec> → [<branch1>, <branch2>, <branch3>]

| Executor | Class | Handler method | Return type | Status |
|----------|-------|---------------|-------------|--------|
| branch1 | `PropertyTheftFanOutExecutor` | `HandleAsync` | `ValueTask` ❌ | BROKEN — will starve fan-in |
| branch2 | `TransactionFanOutExecutor` | `HandleAsync` | `ValueTask<ValidationResult>` ✅ | OK |
| branch3 | `UserHistoryFanOutExecutor` | `HandleAsync` | `ValueTask<ValidationResult>` ✅ | OK |

#### Fan-in: AddFanInBarrierEdge call
- Argument order: ✅ sources first (correct)
- Branch coverage: ⚠️ 3 fan-out branches, 3 in sources list ✅

### Issues Found

1. ❌ `PropertyTheftFanOutExecutor.HandleAsync` returns `ValueTask` (non-generic)
   - **File:** `src/Workflows/Executors/PropertyTheftFanOutExecutor.cs`
   - **Fix:** Change return type to `ValueTask<ValidationResult>` and return the result value
   - **Registry:** Not a CS0618 — silent runtime failure (see `EXEC-001` pattern)

### Overall Status: ❌ FAIL — 1 issue requires fixing before migration is complete
```

---

## Fix Pattern

```csharp
// BEFORE — broken fan-out handler
[MessageHandler]
public async ValueTask HandleAsync(ClaimData input, ExecutorContext context)
{
    var result = await AnalyzeAsync(input);
    await context.SendMessageAsync(result);  // old pattern — produces no output in 1.3.0
}

// AFTER — correct fan-out handler
[MessageHandler]
public async ValueTask<ValidationResult> HandleAsync(ClaimData input, ExecutorContext context)
{
    return await AnalyzeAsync(input);  // return value IS the output message
}
```

---

## Integration

This skill is a **mandatory step** in:
- Phase 3 (Review) of the MAF Migration Agent workflow
- Before declaring any workflow migration complete
- After any executor handler method is modified

Run AFTER `cs0618-hunter` (build errors first, then silent failures).
