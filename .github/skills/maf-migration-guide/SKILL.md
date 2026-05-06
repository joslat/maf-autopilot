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
| 1 | Introduction & scope | You need to understand what changed at a high level |
| 2 | Package matrix | Looking up the correct version of any MAF/Extensions.AI package |
| 3 | Agent creation — `.AsAIAgent()` | Migrating agent instantiation patterns |
| 4 | `ChatClientAgentOptions` — Instructions placement | `Instructions` or `Tools` at wrong level |
| 5 | A2A agent registration | Migrating `AIAgentExtensions` or `app.MapA2A` |
| 6 | Sessions — `AgentSession` | Migrating `AgentThread`, `GetNewThread()`, session serialization |
| 7 | Memory and context providers | `ChatHistoryProvider`, `AIContextProvider`, `ProviderSessionState<T>` |
| 8 | Middleware | `.AsBuilder().Use().Build()` pattern |
| 9 | Executors and workflows | `ReflectingExecutor` → `partial class : Executor` + `[MessageHandler]`, fan-out/fan-in |
| 10 | Response processing | `AgentResponse.Text`, `.Messages`, streaming updates |
| 11 | Structured output | `RunAsync<T>()` returning `AgentResponse<T>` |
| 12 | Tool approval | `ApprovalRequiredAIFunction`, `FunctionApprovalRequestContent` |
| 13 | Observability | `.UseOpenTelemetry(sourceName:)` |
| 14 | DevUI and Hosting | `#if DEVUI_ENABLED` guard pattern |
| 15 | Source generator setup | `Microsoft.Agents.AI.Workflows.Generators`, `EmitCompilerGeneratedFiles` |
| 16 | `[StreamsMessage]` / `[YieldsMessage]` removal | These attributes are gone; what to do instead |
| 17 | Streaming — `RunStreamingAsync()` | `InProcessExecution.StreamAsync()` → `RunStreamingAsync()` |
| 18 | Event rename | `AgentRunUpdateEvent` → `AgentResponseUpdateEvent` |
| 19 | Namespace changes | Old vs. new namespace mappings |
| 20 | Migration checklist | End-to-end verification steps |
| 21 | Obsolete API Registry (CS0618) | Known `[Obsolete]` APIs; dotnet-inspect issue #316 resolved — `member --index` now detects them |

---

## Quick Lookup by Symptom

| Symptom / Error | Read sections |
|-----------------|---------------|
| `CS0246: Type 'ReflectingExecutor' not found` | 9 |
| `CS0246: [StreamsMessage] not found` | 9, 16 |
| `CS0618: AddFanInBarrierEdge is obsolete` | 9, 21 |
| Fan-in never reached, workflow exits silently | 9 |
| `AgentThread` not found | 6 |
| `GetNewThread()` not found | 6 |
| `Deserialize<T>()` not found on `AgentResponse` | 11 |
| `StreamAsync()` not found | 17 |
| `AgentRunUpdateEvent` not found | 18 |
| `AIAgentExtensions` not found | 5 |
| `MapA2A` not found | 5 |
| `Instructions =` compiles but has no effect | 4 |
| Source generator not generating `ExecuteCoreAsync` | 15 |
| IDE shows `CS0534` on executor class | 15 |
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

## Section 9 Subsections (Executors — Most Complex)

Section 9 is the largest. Key subsections:

| Subsection | Topic |
|------------|-------|
| 9.1 | `partial class : Executor` + `[MessageHandler]` pattern |
| 9.2 | Source generator setup and `sealed partial` requirement |
| 9.3 | Agent `.BindAsExecutor(emitEvents: true)` |
| 9.4 | Fan-out executor — **must return `ValueTask<T>`** |
| 9.5 | Fan-in barrier — `AddFanInBarrierEdge(sources, target)` correct argument order |
| 9.6 | Complete working example — full workflow with fan-out and fan-in |

When diagnosing a fan-out/fan-in issue, read 9.4 and 9.5 first.

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
## Section N — Title
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
