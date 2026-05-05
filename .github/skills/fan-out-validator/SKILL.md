---
name: fan-out-validator
description: "Validates fan-out/fan-in executor topology in MAF 1.3.0 workflows. Detects the silent runtime failure where a fan-out executor handler returns void or non-generic ValueTask, causing the fan-in barrier to starve with no build error. Use during Phase 3 review and whenever a workflow contains AddFanOutEdge."
---

# fan-out-validator

## The Silent Failure Pattern

In MAF 1.3.0, `WorkflowBuilder.AddFanOutEdge` routes the **return value** of a fan-out executor handler to all connected targets. If the handler returns `void` or non-generic `ValueTask`, it produces no output message. The fan-in barrier never receives the required number of messages, so it never fires — and the workflow exits cleanly with no error.

```
Build:   ✅ green
Test:    ✅ passes (if tests don't check fan-in output)
Runtime: ✗ fan-in never reached, partial results silently dropped
```

This is the most dangerous class of MAF migration bug.

## When to Use This Skill

- Whenever a workflow file contains `AddFanOutEdge`
- During Phase 3 review (mandatory, after CS0618 check)
- After any executor handler method is modified
- When a workflow "runs to completion" but produces unexpected/missing output

## The Validation Workflow

### Step 1 — Find all fan-out edges

```powershell
Select-String -Path "src/**/*.cs" -Pattern "AddFanOutEdge" -Recurse
```

For each match, note:
1. The file and line
2. The executor binding passed as the source (e.g., `fanOutExec`)

### Step 2 — Find the executor class and handler

For each fan-out executor binding, find the `Executor` subclass it wraps. Then find its `[MessageHandler]`-attributed method.

The critical property: **what is the return type?**

### Step 3 — Check return types

```
CORRECT:   async ValueTask<T> HandleAsync(TInput input, ...)
           where T = the message type that fan-in expects
           
WRONG:     async ValueTask HandleAsync(...)        ← produces nothing
WRONG:     void HandleAsync(...)                   ← produces nothing
WRONG:     Task HandleAsync(...)                   ← produces nothing
```

If ANY fan-out handler has a non-generic return type → it is broken.

### Step 4 — Verify fan-in match

For each `AddFanInBarrierEdge` call in the same workflow:
1. Confirm it uses the **non-obsolete overload**: `AddFanInBarrierEdge(IEnumerable<ExecutorBinding> sources, ExecutorBinding target)`
2. Confirm the `sources` list contains ALL fan-out executor bindings that should contribute
3. Confirm the count: if 3 fan-out executors feed into a fan-in, the barrier expects exactly 3 messages

### Step 5 — Fix pattern

**Wrong (void fan-out handler):**
```csharp
[MessageHandler]
public async ValueTask HandleAsync(ClaimData claim, ExecutorContext context)
{
    var result = await ProcessAsync(claim);
    await context.SendMessageAsync(result);  // ← old pattern, wrong in 1.3.0
}
```

**Correct (ValueTask<T> fan-out handler):**
```csharp
[MessageHandler]
public async ValueTask<ValidationResult> HandleAsync(ClaimData claim, ExecutorContext context)
{
    return await ProcessAsync(claim);  // ← return value IS the fan-out message
}
```

**Wrong (obsolete fan-in overload):**
```csharp
builder.AddFanInBarrierEdge(aggregatorExec, osintExec, userHistoryExec, transactionExec);
//                          ↑ target first — obsolete (CS0618)
```

**Correct (sources first):**
```csharp
builder.AddFanInBarrierEdge(
    new[] { osintExec, userHistoryExec, transactionExec },  // sources first
    aggregatorExec);                                         // target last
```

---

## Validation Checklist

For each workflow with fan-out:

- [ ] Every fan-out executor handler returns `ValueTask<T>` (generic)
- [ ] The `T` type matches what the fan-in aggregator handler accepts as input
- [ ] `AddFanInBarrierEdge` uses sources-first overload
- [ ] The sources list is complete (no missing fan-out executor)
- [ ] Count matches: N fan-out executors → fan-in barrier expects N messages

All boxes must be checked before marking fan-out tasks as `✅ Verified`.
