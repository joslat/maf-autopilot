---
name: maf-migration-guide
description: "Full MAF 1.3.0 migration reference guide, navigated by section. Load this skill when you need API signatures, before/after code patterns, or detailed explanations of MAF 1.3.0 changes. The guide lives at guides/maf-1.3.0-migration-guide.md — this skill is the smart index that tells you which section to read for a given question."
---

# MAF 1.3.0 Migration Guide — Navigator

The full guide is at **`guides/maf-1.3.0-migration-guide.md`**.

This SKILL.md is the **index** — use it to find the right section for your question, then read only that section of the guide with `read_file`. This keeps context efficient.

---

## Section Index

| Section | Title | Read when… |
|---------|-------|-----------|
| 1 | Package References & Dependencies | Updating NuGet package names or versions |
| 2 | Namespace Changes | Old vs. new namespace mappings |
| 3 | Creating Agents — `.AsAIAgent()` | Migrating agent instantiation, `.AsAIAgent()` extensions |
| 4 | Running Agents — `RunAsync` & `RunStreamingAsync` | `RunAsync`/`RunStreamingAsync` patterns, per-run `ChatClientAgentRunOptions` overrides |
| 5 | Function Tools | `AIFunctionFactory.Create()`, tool registration |
| 6 | Agent-as-a-Tool Composition | Using one agent as a tool for another agent |
| 7 | Sessions & Multi-Turn Conversations | `AgentThread` → `AgentSession`, `GetNewThread()` → `CreateSessionAsync()` |
| 8 | Context Providers & Agent Memory (`AIContextProvider`) | `AIContextProvider`, `ProviderSessionState<T>`, memory/RAG, state injection |
| 9 | Chat History Management (`ChatHistoryProvider`) | `ChatHistoryProvider`, `InMemoryChatHistoryProvider`, history persistence |
| 10 | The Agent Pipeline Architecture | Understanding the pipeline; `AsBuilder()` pattern overview |
| 11 | Middleware | `.AsBuilder().Use(runFunc:, runStreamingFunc:).Build()` middleware chain |
| 12 | Workflows — `WorkflowBuilder` & Executors | `WorkflowBuilder`, `AddFanOutEdge`, `AddFanInBarrierEdge` argument order |
| 13 | Source-Generated Executors (`[MessageHandler]`) | `ReflectingExecutor<T>` → `sealed partial class : Executor` + `[MessageHandler]`; fan-out return type; `[StreamsMessage]`/`[YieldsMessage]` removal |
| 14 | Agents in Workflows | `agent.BindAsExecutor(emitEvents: true)` |
| 15 | Workflow Execution & Events | `InProcessExecution.RunStreamingAsync()`, event types, `StreamingRun` |
| 16 | Response & Content Types | `AgentResponse.Text`, `.Messages`, `IAgentContent` types |
| 17 | Streaming Patterns | `RunStreamingAsync()` patterns; `AgentResponseUpdate` streaming type; `.ToAgentResponse()` aggregation; `WatchStreamAsync()` workflow events |
| 17.5 | Compaction Strategies (Experimental) | Long conversations, token limits, `CompactionProvider` |
| 17.6 | Structured Output | `RunAsync<T>()` returning `AgentResponse<T>` |
| 17.7 | Tool Approval — Human-in-the-Loop | `ApprovalRequiredAIFunction`, `FunctionApprovalRequestContent` |
| 17.8 | Observability — OpenTelemetry | `.UseOpenTelemetry(sourceName:)`, activity tracing |
| 17.9 | Multimodal — Images | Working with image inputs in agent requests |
| 17.10 | A2A Agent — Remote Protocol Proxies | `AIAgentExtensions` → `AddA2AServer()`, `MapA2A` → `MapA2AHttpJson`/`MapA2AJsonRpc` |
| 17.11 | Dynamic Tool Expansion (New in 1.3.0) | Registering tools at runtime after agent creation |
| 17.12 | Server-Side Foundry Toolbox (New in 1.3.0) | Azure AI Foundry server-side tool execution |
| 18 | Session Serialization & Replay | `agent.SerializeSession()` → `await agent.SerializeSessionAsync()`, session persistence and round-trip |
| 19 | API Patterns Summary | Quick API reference table with section cross-links |
| 20 | Migration Checklist | End-to-end migration verification, phase-by-phase checklist |
| 21 | Obsolete API Registry (CS0618) | Known `[Obsolete]` APIs with exact fix patterns; dotnet-inspect issue #316 resolved — `member --index` now detects them |

---

## Quick Lookup by Symptom

| Symptom / Error | Read sections |
|-----------------|---------------|
| `CS0246: Type 'ReflectingExecutor' not found` | 12, 13 |
| `CS0246: [StreamsMessage] not found` | 13 |
| `CS0618: AddFanInBarrierEdge is obsolete` | 12, 21 |
| Fan-in never reached, workflow exits silently | 12, 13 |
| `AgentThread` not found | 7 |
| `GetNewThread()` not found | 7 |
| `SerializeSession` is obsolete | 18 |
| `Deserialize<T>()` not found on `AgentResponse` | 17.6 |
| `StreamAsync()` not found | 17 |
| `AgentRunUpdateEvent` not found | 15, 17 |
| `AIAgentExtensions` not found | 17.10 |
| `MapA2A` not found | 17.10 |
| `Instructions =` compiles but has no effect | 3 |
| Source generator not generating `ExecuteCoreAsync` | 13 |
| IDE shows `CS0534` on executor class | 13 |
| `DefaultAzureCredential` in production | Any — constraint violation, use `ManagedIdentityCredential` |

---

## How to Read Sections

```
read_file(
  filePath: "guides/maf-1.3.0-migration-guide.md",
  startLine: <section start line>,
  endLine: <section end line>
)
```

If you don't know the line numbers, read the guide's table of contents first (typically the first 50 lines) to find section headings, then read the specific section.

---

## Sections 12 & 13 — Executor Topology (Most Complex)

Executor and workflow content spans two sections in the guide:
- **§12** (line 1078): `WorkflowBuilder`, `AddFanOutEdge`, `AddFanInBarrierEdge` argument order
- **§13** (line 1118): `sealed partial class : Executor` + `[MessageHandler]`, source generator setup, fan-out return types, `[StreamsMessage]`/`[YieldsMessage]` removal

Key patterns in §12-§13:

| Topic | Section |
|-------|---------|
| `sealed partial class : Executor` + `[MessageHandler]` pattern | 13 |
| Source generator setup (`Microsoft.Agents.AI.Workflows.Generators`, `EmitCompilerGeneratedFiles`) | 13 |
| Agent `.BindAsExecutor(emitEvents: true)` | 14 |
| Fan-out executor — **must return `ValueTask<T>`** | 13 |
| Fan-in barrier — `AddFanInBarrierEdge(sources, target)` correct argument order | 12 |
| Complete working example — full workflow with fan-out and fan-in | 12, 13 |

When diagnosing a fan-out/fan-in issue, read §12 and §13 first.

---

## Key Patterns at a Glance

### Executor (was: ReflectingExecutor)
```csharp
// BEFORE
public class MyExecutor : ReflectingExecutor<MyInput>
{
    public async Task<MyOutput> HandleAsync(MyInput input) { ... }
}

// AFTER
public sealed partial class MyExecutor : Executor
{
    [MessageHandler]
    public async ValueTask<MyOutput> HandleAsync(MyInput input, ExecutorContext context) { ... }
}
```

### Session (was: AgentThread)
```csharp
// BEFORE
var thread = agent.GetNewThread();
var response = await agent.RunAsync(input, thread);

// AFTER
var session = await agent.CreateSessionAsync(cancellationToken);
var response = await agent.RunAsync(input, session);
```

### Fan-out handler (must return the message)
```csharp
// BEFORE (wrong — void return starves fan-in)
[MessageHandler]
public async ValueTask HandleAsync(ClaimData claim, ExecutorContext context)
{
    var result = await ProcessAsync(claim);
    await context.SendMessageAsync(result);  // ← wrong in 1.3.0
}

// AFTER (correct — return value IS the fan-out message)
[MessageHandler]
public async ValueTask<ValidationResult> HandleAsync(ClaimData claim, ExecutorContext context)
{
    return await ProcessAsync(claim);
}
```

---

## Version-Keyed Section Metadata

Every section in `guides/maf-1.3.0-migration-guide.md` carries a machine-readable metadata comment:

```html
<!-- introduced: 1.3.0 | applies-to: 1.2.x → 1.3.x | deprecated-in: none -->
## N. Title
```

**What the fields mean:**

| Field | Values | Purpose |
|-------|--------|---------|
| `introduced` | MAF version | When this section's content first became relevant |
| `applies-to` | Source → target range | Which migration paths need this section |
| `deprecated-in` | MAF version or `none` | When this section's patterns became obsolete |

**How to use these metadata tags:**

When migrating from version A to version B, load only sections where:
- `applies-to` includes the A → B path
- `deprecated-in` is `none` OR is a version later than B

Example: Migrating 1.2.0 → 1.3.0 → load sections with `applies-to: 1.2.x → 1.3.x` and `deprecated-in: none`.  
Example: Migrating 1.3.0 → 1.4.0 → skip sections tagged `introduced: 1.3.0` (already applied) and load sections tagged `introduced: 1.4.0`.

When the `maf-release-watcher` adds new sections for future MAF versions, they will carry updated `introduced` and `applies-to` tags so agents automatically load the right sections for the active migration path.
