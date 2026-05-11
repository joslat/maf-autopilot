---
description: "Always-loaded compact reference for MAF 1.3.0 migrations. Contains the breaking changes table, hard constraints, and quick-reference rules. Applies to all files when the MAF migration or auditor agents are active."
applyTo: "**"
---

# MAF 1.3.0 — Constraints & Breaking Changes Reference

## Hard Constraints (Never Violate)

- **NEVER** introduce patterns not present in MAF 1.3.0
- **NEVER** store session-specific state in `AIContextProvider` or `ChatHistoryProvider` instance fields — always use `ProviderSessionState<T>`
- **NEVER** add `[StreamsMessage]` or `[YieldsMessage]` attributes — removed in 1.3.0, causes `CS0246`
- **NEVER** use `DefaultAzureCredential` in production code — prefer `ManagedIdentityCredential`
- **NEVER** enable `EnableSensitiveData = true` in non-development environments
- **ALWAYS** pin `dotnet-inspect` to v0.7.8 or later. v0.7.8 surfaces `[Obsolete]` in listings; versions ≤ v0.7.7 miss obsoletions at the overload level. For ground-truth on what your project actually triggers, run `cs0618-hunter` (compiler-based) — it catches transitive obsoletions, overload-resolution surprises, and project-local `[Obsolete]` attributes that static inspection cannot see.
- **ALWAYS** update `src/docs/migration-plan.md` tracking table after each task is completed and build-verified

## Fan-out / Fan-in Rules (Silent Failure Risk)

```
Fan-out executor handlers MUST return ValueTask<T> where T is the message type.
A void or non-generic ValueTask return produces NO output message.
The fan-in barrier then starves silently — the workflow exits cleanly but incompletely.
This is NOT a build error. The only detection is runtime or fan-out-validator skill.
```

```
AddFanInBarrierEdge argument order:
  CORRECT:   AddFanInBarrierEdge(IEnumerable<ExecutorBinding> sources, ExecutorBinding target)
  OBSOLETE:  AddFanInBarrierEdge(ExecutorBinding target, IEnumerable<ExecutorBinding> sources)
  The obsolete overload compiles and runs but triggers CS0618.
```

## Key Breaking Changes

| Area | Old (broken) | New (correct) |
|------|-------------|---------------|
| Executors | `ReflectingExecutor<T>` | `sealed partial class : Executor` + `[MessageHandler]` |
| Executors | `IMessageHandler<TIn,TOut>` | `[MessageHandler]` on handler methods |
| Executors | `[StreamsMessage]`, `[YieldsMessage]` | **Removed** — delete these attributes |
| Source gen | *(missing package)* | Add `Microsoft.Agents.AI.Workflows.Generators 1.3.0` |
| Sessions | `AgentThread` | `AgentSession` via `await agent.CreateSessionAsync()` |
| Sessions | `GetNewThread()` | `await agent.CreateSessionAsync(ct)` |
| Sessions | `agent.SerializeSession(session)` | `await agent.SerializeSessionAsync(session)` |
| Response | `AgentResponse.Deserialize<T>()` | `RunAsync<T>()` + `.Result` |
| Streaming | `InProcessExecution.StreamAsync()` | `RunStreamingAsync()` |
| Events | `AgentRunUpdateEvent` | `AgentResponseUpdateEvent` |
| A2A DI | `AIAgentExtensions` | `services.AddA2AServer(agent, new A2AServerRegistrationOptions { AgentCard = ... })` |
| A2A endpoint | `app.MapA2A(...)` | `app.MapA2AHttpJson(path)` or `app.MapA2AJsonRpc(path)` |
| Agent options | `Instructions` / `Tools` at top-level | Must be inside `ChatClientAgentOptions.ChatOptions` |
| DevUI/Hosting | `Microsoft.Agents.AI.DevUI` / `.Hosting` | No 1.3.0 equivalent — guard with `#if DEVUI_ENABLED` |
| Fan-in | `AddFanInBarrierEdge(target, sources)` | `AddFanInBarrierEdge(sources, target)` — sources first |
| Fan-out | `async ValueTask HandleAsync(...)` returning void | `async ValueTask<T> HandleAsync(...)` returning the message |

## Known Misalignments (Official Docs vs. Reality)

- `SerializeSession` in official docs is **wrong** — use `await agent.SerializeSessionAsync(session)` (async)
- `FunctionApprovalRequestContent` vs `ToolApprovalRequestContent` — verify with `dotnet-inspect` for your exact version
- `RunAsync<T>` exists on `ChatClientAgent` but NOT on `AIAgent` interface — cast may be needed
- `ChatClientAgentOptions` has undocumented properties: `UseProvidedChatClientAsIs`, `RequirePerServiceCallChatHistoryPersistence`, `ClearOnChatHistoryProviderConflict`
- `AddFanInBarrierEdge(target, params sources[])` is `[Obsolete]` in 1.3.0 — surfaced by `dotnet-inspect member` as of v0.7.8; older `dotnet-inspect` versions miss it and require the compiler

## dotnet-inspect — Required Version

**Pin to `dotnet-inspect@0.7.8` or later.** v0.7.8 ([release notes](https://github.com/richlander/dotnet-inspect/releases/tag/v0.7.8), 2026-05-04) surfaces `[Obsolete]` members in listings (PR #318 closes issue #316). Earlier versions (≤ v0.7.7) miss obsoletions at the overload level and will silently mislead.

**Always run** `dotnet build 2>&1 | Select-String "warning CS0618"` as the final gate — the compiler is ground-truth and catches transitive / overload-resolution / project-local `[Obsolete]` cases that no static inspector can see. `dotnet-inspect@0.7.8` is the fast pre-build path; the compiler is the verification path. Use both.

## How to verify each constraint (use the MCP tools)

The `maf-autopilot` MCP server provides executable tools that enforce most constraints. Tool names are PascalCase.

| Constraint above                               | Verify by calling                                  |
|------------------------------------------------|----------------------------------------------------|
| NEVER instance state in `AIContextProvider`    | `MafScanAntiPatterns(repoPath)` → rule `MAF-AP-CONC-001` |
| NEVER `DefaultAzureCredential` in production   | `MafScanAntiPatterns(repoPath)` → rule `MAF-AP-SEC-001`  |
| NEVER `EnableSensitiveData = true` outside dev | `MafScanAntiPatterns(repoPath)` → rule `MAF-AP-SEC-003`  |
| Fan-out handler must return `Task<T>`          | `MafValidateFanOut(repoPath)` (or `MafSimulateWorkflow` for full topology) |
| `AddFanInBarrierEdge` argument order           | `MafRunCs0618Hunt(projectPath)` (CS0618 path)            |
| No `[StreamsMessage]` / `[YieldsMessage]`      | `MafRunCs0618Hunt(projectPath)` (CS0246 path)            |
| Always run tracking-table updates              | (process — agents enforce this)                     |

Also useful: `MafApiSafety(apiName)` for a quick SAFE/UNSAFE check on any MAF API, `MafExplain(snippet)` to annotate a code fragment with guide citations, and the `maf-autopilot.Analyzers` NuGet package for write-time IDE squigglies (`MAF001`/`MAF002`/`MAF003`).
