# Semantic Kernel → Microsoft Agent Framework — C# migration guide

> **Source:** Official Microsoft Learn guide, *"Semantic Kernel to Microsoft Agent Framework Migration Guide"* (C# pivot) — <https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-semantic-kernel/?pivots=programming-language-csharp> (ms.date 2026-04-01). Stored with permission from the MAF team (Shawn Henry); the Agent Framework is MIT-licensed. Keep this guide in sync with the upstream page.
>
> **Used by:** the `maf-migrate-from` flow (source = `semantic-kernel`) and the SK source-framework detector. This is the authoritative SK→MAF mapping the migration plan is built against.

## Why migrate

- **Simplified API** — far less boilerplate (no mandatory `Kernel`, no plugin wrappers).
- **Unified interface** — one agent type (`ChatClientAgent`) over any `IChatClient` provider instead of per-provider agent classes.
- **Built on `Microsoft.Extensions.AI`** — the shared message/content types (`IChatClient`, `ChatMessage`, `ChatOptions`, `AIFunction`).

## SK → MAF API mapping (the cheat sheet)

| Semantic Kernel (C#) | Microsoft Agent Framework | Notes |
|---|---|---|
| `using Microsoft.SemanticKernel;` / `using Microsoft.SemanticKernel.Agents;` | `using Microsoft.Extensions.AI;` + `using Microsoft.Agents.AI;` | §1 |
| **`Kernel`** (every agent requires one) | *removed* — agents are created directly from a chat client | §2 — the single biggest simplification |
| `IChatCompletionService` / `ChatCompletionService` | **`IChatClient`** (Microsoft.Extensions.AI) | §11 |
| `new ChatCompletionAgent { Kernel = kernel, Instructions = … }` | `chatClient.AsAIAgent(instructions: …)` → **`ChatClientAgent`** | §2 |
| `AzureAIAgent` (Foundry) | `aiProjectClient.AsAIAgent(model: deployment, instructions: …)` | §2/§11 |
| `OpenAIAssistantAgent` | `await assistantClient.CreateAIAgentAsync(instructions: …)` | §2/§11 |
| per-provider agent classes (`ChatCompletionAgent`, `AzureAIAgent`, `OpenAIAssistantAgent`) | a **single `ChatClientAgent`** for any `IChatClient`-based service | §11 |
| base type `Microsoft.SemanticKernel.Agents.Agent` | base type **`AIAgent`** | §10 |
| `AgentThread` created by the caller (`new OpenAIAssistantAgentThread(...)`, `new AzureAIAgentThread(...)`, `new OpenAIResponseAgentThread(...)`) | `AgentSession session = await agent.CreateSessionAsync();` — the **agent** creates the session | §3 |
| `await thread.DeleteAsync()` | no session-delete API; if the provider supports it, track the id and call the provider SDK (e.g. `await assistantClient.DeleteThreadAsync(session.ConversationId)`) | §4 |
| `[KernelFunction]` + `KernelFunctionFactory.CreateFromMethod` + `KernelPluginFactory.CreateFromFunctions` + `kernel.Plugins.Add(plugin)` | `chatClient.AsAIAgent(tools: [AIFunctionFactory.Create(GetWeather)])` — register tools **in one call** | §5 |
| `[KernelFunction]` attribute (**required** to expose a method as a tool) | plain method, no attribute; **`[Description]` optional** | §8 |
| `KernelPlugin` (wraps multiple functions) | *no plugin concept* — pass the methods directly to `tools:` | §5 |
| `await agent.InvokeAsync(input, thread, options)` → `IAsyncEnumerable<AgentResponseItem<ChatMessageContent>>` | `AgentResponse response = await agent.RunAsync(input, session);` — single `AgentResponse` (`.Text`, `.ToString()`, `.Messages`) | §6 |
| `await agent.InvokeStreamingAsync(input, thread)` → `StreamingChatMessageContent` | `await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(input, session))` | §7 |
| `new OpenAIPromptExecutionSettings { MaxTokens = 1000 }` + `new AgentInvokeOptions { KernelArguments = new(settings) }` | `new ChatClientAgentRunOptions(new() { MaxOutputTokens = 1000 })` | §9 |
| `KernelArguments` | `ChatOptions` / run options | §9 |
| DI: `services.AddKernel().AddProvider(...)` + `AddKeyedSingleton<Agent>(name, (sp,k) => new ChatCompletionAgent { Kernel = sp.GetRequiredService<Kernel>() })` | `services.AddKeyedSingleton<AIAgent>(() => client.AsAIAgent(...))` | §10 |

## Interop bridges — the gradual / side-by-side enabler

There is **no SK→MAF converter tool**; the official aid is a set of **C# interop bridges** that let MAF agents reuse existing SK assets *as-is*, so you can migrate incrementally (and run SK + MAF side by side). Verified C# APIs (SK 1.45.0, all in package `Microsoft.SemanticKernel.Abstractions`):

| Bridge | Exact C# API | Use it for |
|---|---|---|
| SK chat service → MAF client | `IChatCompletionService.AsChatClient()` → `IChatClient`  (class `ChatCompletionServiceExtensions`, ns `Microsoft.SemanticKernel.ChatCompletion`); reverse `IChatClient.AsChatCompletionService()` | Point a MAF `ChatClientAgent` at your existing SK-configured inference service. |
| SK function → MAF tool | `KernelFunction.AsAIFunction(Kernel? kernel = null)` → `Microsoft.Extensions.AI.AIFunction` (instance method, ns `Microsoft.SemanticKernel`); reverse `AIFunction.AsKernelFunction()` | Reuse an existing `[KernelFunction]` plugin **without rewriting it** — the C# analogue of Python `.as_agent_framework_tool()`. |
| All plugins → tools | `kernel.Plugins.SelectMany(p => p).Select(f => f.AsAIFunction())` | Bulk-bridge every Kernel plugin function into a MAF agent's `tools:` list. |

```csharp
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

// Reuse an SK-configured chat service + SK plugins inside a MAF agent — no rewrite:
IChatClient chatClient = skChatCompletionService.AsChatClient();
AIFunction[] tools = kernel.Plugins.SelectMany(p => p).Select(f => f.AsAIFunction()).ToArray();
AIAgent agent = chatClient.AsAIAgent(instructions: "...", tools: tools);
```

> **Migration strategy per construct** — the assistant tags each finding:
> - **🌉 bridgeable** — use the shim, keep the SK code (e.g. `[KernelFunction]` plugins, an SK chat service).
> - **🔁 rename/rewrite** — mechanical SK→MAF API swap (agent creation, threads→sessions, invocation, options, DI).
> - **🏗 re-architect** — no 1:1; needs MAF workflows/middleware (group chat, planners, filters).
> Bridge first, rewrite when ready.

## Multi-agent: `AgentGroupChat` → MAF workflows  (🏗 re-architect)

`AgentGroupChat` (ns `Microsoft.SemanticKernel.Agents.Chat`, with `SelectionStrategy`/`TerminationStrategy`) is **archived** in SK (it was superseded by `GroupChatOrchestration`, now by MAF). It maps to a MAF **group-chat workflow** (verified C#):

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents =>
        new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 5 })
    .AddParticipants(writer, reviewer)            // ChatClientAgent participants
    .Build();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));   // kicks it off
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent u) { AgentResponse r = u.AsResponse(); /* … */ }
    else if (evt is WorkflowOutputEvent o) { var history = o.As<List<ChatMessage>>(); break; }
}
```
- SK `TerminationStrategy` → override `protected override ValueTask<bool> ShouldTerminateAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct)` on a custom group-chat manager.
- Five built-in MAF orchestrations (`Microsoft.Agents.AI.Workflows`): **Sequential, Concurrent, Handoff, Group Chat, Magentic**.
- **Planners** (SK Stepwise/Handlebars) are *removed*, not migrated → use MAF's automatic function-calling loop (a 🏗 re-architecture).

## Beyond the official page — constructs it doesn't cover

A real SK app uses more than the official page maps. The detector flags the rows below that have a distinctive SK type; rows marked **⚠ no type signature** have no reliable syntactic marker (they're patterns, not named types), so the migration agent must surface those by reading intent rather than relying on the scan.

| SK construct | MAF target | Strategy | Detected? |
|---|---|---|---|
| **Prompt templates** — `KernelPromptTemplate`, `PromptTemplateConfig`, Handlebars/Liquid factories, `KernelFunctionFromPrompt` | **No built-in template engine.** Render the template yourself (Handlebars.NET / Scriban / interpolation) and pass the result as the agent's `instructions`; or move dynamic data into a Context Provider. | 🏗 re-architect | ✅ |
| **Function-invocation filters** — `IFunctionInvocationFilter`, `IAutoFunctionInvocationFilter`, `IPromptRenderFilter` | **Agent middleware** (function-calling / agent-run / `IChatClient`), wired via `agent.AsBuilder().Use(...)`. Function-calling middleware needs a `FunctionInvokingChatClient` (i.e. `ChatClientAgent`). | 🔁 rewrite | ✅ |
| **Structured output** — `responseFormat` on `*PromptExecutionSettings` | `AgentRunOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<T>()`, or the typed `agent.RunAsync<T>(...)` → `AgentResponse<T>` (`.Result`). Wrap top-level primitives/arrays in an object type. | 🔁 rewrite | ✅ (via the settings type) |
| **`ChatHistory`** (manual serialize) | `InMemoryChatHistoryProvider`, a custom `ChatHistoryProvider` subclass, or whole-session `await agent.SerializeSessionAsync(session)` / `await agent.DeserializeSessionAsync(serialized)`. (The **sync** `SerializeSession` shown in some docs does **not** exist on MAF 1.3.0+ — use the async form.) | 🔁 rewrite | ✅ |
| **Vector stores / RAG** — `IVectorStore`, `ITextSearch`, `VectorStoreTextSearch` | A `TextSearchProvider` attached via `ChatClientAgentOptions.AIContextProviders` (storage via `Microsoft.Extensions.VectorData`). | 🏗 re-architect | ✅ |
| **Observability** — `Kernel`-level `AddOpenTelemetry` | `.UseOpenTelemetry()` on the `IChatClient` build chain (before `UseFunctionInvocation()`); subscribe to source `Microsoft.Extensions.AI`. `EnableSensitiveData = true` is dev-only (also enforced by `MAF-AP-SEC-003`). | 🔁 rewrite | ✅ (kernel-builder receiver) |
| **Hand-rolled memory injection** | An `AIContextProvider` (override `InvokingAsync` → return `AIContext`). | 🏗 re-architect | ⚠ no type signature |
| **Multi-agent beyond group chat** — handoff, magentic, SK Process Framework | MAF workflow orchestrations: handoff, magentic, fan-out/fan-in, nested, with checkpoint/resume. | 🏗 re-architect | ⚠ no type signature |

> Biggest watch-outs: **prompt templates** (MAF has no template engine — render externally) and **hand-rolled memory/context** (re-architect onto Context Providers). The two **⚠** rows won't show up in the scan — review the code for them by hand.

---

## Section-by-section (before → after)

### 1. Namespaces
```csharp
// Semantic Kernel
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

// Agent Framework
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
```

### 2. Agent creation (no Kernel)
```csharp
// Semantic Kernel — every agent needs a Kernel
Kernel kernel = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId, apiKey).Build();
ChatCompletionAgent agent = new() { Instructions = agentInstructions, Kernel = kernel };

// Agent Framework — created straight from the client
AIAgent openAIAgent          = chatClient.AsAIAgent(instructions: agentInstructions);
AIAgent azureFoundryAgent    = aiProjectClient.AsAIAgent(model: deploymentName, instructions: agentInstructions);
AIAgent openAIAssistantAgent = await assistantClient.CreateAIAgentAsync(instructions: agentInstructions);
```
> Retrieve an existing hosted agent: `aiProjectClient.AsAIAgent(agentRecord)`.
> 🔐 Don't use `DefaultAzureCredential` in production — prefer `ManagedIdentityCredential` (this is also enforced by maf-doctor rule `MAF-AP-SEC-001`).

### 3. Thread → Session
```csharp
// Semantic Kernel — caller picks the thread type
AgentThread thread = new OpenAIAssistantAgentThread(this.AssistantClient);
AgentThread thread = new AzureAIAgentThread(this.Client);

// Agent Framework — the agent creates the session
AgentSession session = await agent.CreateSessionAsync();
```

### 4. Session cleanup
```csharp
// Semantic Kernel
await thread.DeleteAsync();

// Agent Framework — no session-delete API; use the provider SDK if supported
await assistantClient.DeleteThreadAsync(session.ConversationId);
```

### 5. Tool registration
```csharp
// Semantic Kernel
KernelFunction function = KernelFunctionFactory.CreateFromMethod(GetWeather);
KernelPlugin plugin = KernelPluginFactory.CreateFromFunctions("WeatherPlugin", [function]);
kernel.Plugins.Add(plugin);
ChatCompletionAgent agent = new() { Kernel = kernel, /* … */ };

// Agent Framework — one call
AIAgent agent = chatClient.AsAIAgent(tools: [AIFunctionFactory.Create(GetWeather)]);
```

### 6. Non-streaming invocation (`Invoke` → `Run`)
```csharp
// Semantic Kernel
await foreach (AgentResponseItem<ChatMessageContent> result in agent.InvokeAsync(userInput, thread, agentOptions))
    Console.WriteLine(result.Message);

// Agent Framework — one AgentResponse (.Text / .Messages)
AgentResponse agentResponse = await agent.RunAsync(userInput, session);
```

### 7. Streaming invocation
```csharp
// Semantic Kernel
await foreach (StreamingChatMessageContent update in agent.InvokeStreamingAsync(userInput, thread))
    Console.Write(update);

// Agent Framework
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userInput, session))
    Console.Write(update); // ToString()-friendly
```

### 8. Tool function signatures (`[KernelFunction]` → plain method)
```csharp
// Semantic Kernel — attribute required
public class WeatherPlugin
{
    [KernelFunction]
    public static string GetForecast(string city) => /* … */;
}

// Agent Framework — no attribute; [Description] optional
public class WeatherTools
{
    [Description("Get the forecast for a city")]
    public static string GetForecast(string city) => /* … */;
}
```

### 9. Options configuration
```csharp
// Semantic Kernel
OpenAIPromptExecutionSettings settings = new() { MaxTokens = 1000 };
AgentInvokeOptions options = new() { KernelArguments = new(settings) };

// Agent Framework
ChatClientAgentRunOptions options = new(new() { MaxOutputTokens = 1000 });
```

### 10. Dependency injection
```csharp
// Semantic Kernel — Kernel registration required
services.AddKernel().AddProvider(...);
serviceContainer.AddKeyedSingleton<SemanticKernel.Agents.Agent>("assistant",
    (sp, key) => new ChatCompletionAgent { Kernel = sp.GetRequiredService<Kernel>() });

// Agent Framework
services.AddKeyedSingleton<AIAgent>(() => client.AsAIAgent(...));
```

### 11. Agent type consolidation
- SK: `ChatCompletionAgent` (chat completion), `OpenAIAssistantAgent` (Assistants), `AzureAIAgent` (Foundry), …
- MAF: a single **`ChatClientAgent`** over any service whose SDK implements `IChatClient`.

---

## Recommended migration order (for the plan)

1. **Packages & namespaces** — swap `Microsoft.SemanticKernel*` refs for `Microsoft.Agents.AI` + `Microsoft.Extensions.AI` (+ the provider client package), update usings.
2. **Tools first** — convert `[KernelFunction]` plugins to plain methods (`AIFunctionFactory.Create`), since agents depend on them.
3. **Agents** — replace each `…Agent { Kernel = … }` with `client.AsAIAgent(instructions:, tools:)`; drop the `Kernel`.
4. **Threads → sessions** — `new …AgentThread()` → `await agent.CreateSessionAsync()`.
5. **Invocation** — `InvokeAsync`/`InvokeStreamingAsync` → `RunAsync`/`RunStreamingAsync`; adapt to `AgentResponse`/`AgentResponseUpdate`.
6. **Options & DI** — `KernelArguments`/execution-settings → `ChatClientAgentRunOptions`/`ChatOptions`; `AddKernel` → `AddKeyedSingleton<AIAgent>`.
7. **Multi-agent / planners / memory / filters** — re-architect onto MAF workflows + middleware (see the broader MAF docs; not in the page above).
8. **Validate** — `maf-doctor doctor` on the migrated project; it should be anti-pattern clean.

---

## Further reading

Official Microsoft sources for the pieces this page doesn't drill into (the framework is MIT-licensed; the Learn docs are CC-BY, used here with attribution):

- **AutoGen → MAF** migration guide (for the AutoGen source path): <https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/>
- MAF concepts behind the 🏗 re-architectures: **Workflows** <https://learn.microsoft.com/en-us/agent-framework/workflows/> · **Agent middleware** (filters → middleware) <https://learn.microsoft.com/en-us/agent-framework/agents/middleware/defining-middleware> · **Conversations & Memory** (sessions, context providers, storage) <https://learn.microsoft.com/en-us/agent-framework/agents/conversations/>
- MAF C# samples to learn the patterns: `dotnet/samples/03-workflows`, `02-agents/AgentWithMemory`, `02-agents/AgentWithRAG` — <https://github.com/microsoft/agent-framework>
- Semantic Kernel source-side concepts: **Plugins** <https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/> · **Agent orchestration** <https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/>
