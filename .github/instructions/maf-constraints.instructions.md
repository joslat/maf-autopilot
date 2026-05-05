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
- **NEVER** use `dotnet-inspect` alone to check for obsolete APIs — use `cs0618-hunter` skill (compiler-based)
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
- `AddFanInBarrierEdge(target, params sources[])` is `[Obsolete]` in 1.3.0 — `dotnet-inspect member` will NOT flag this; only the compiler catches it

## dotnet-inspect Limitation

`dotnet-inspect` does NOT surface `[Obsolete]` attributes at the individual overload level.

When `member WorkflowBuilder` lists two overloads for `AddFanInBarrierEdge`, neither will be marked as deprecated in the output. The obsolete status is only visible in single-overload `--index` detail view (after you already know which overload to suspect).

**Always run** `dotnet build 2>&1 | Select-String "warning CS0618"` as a mandatory step — never rely on `dotnet-inspect` alone for obsolete API detection.
