---
name: maf-from-semantic-kernel
description: "Cross-framework migration map: Semantic Kernel (C#) → Microsoft Agent Framework. Load this skill when porting an SK app to MAF (NOT a MAF version bump). It is the smart index over docs/migration/from-semantic-kernel-to-maf.md — it tells you each SK construct's MAF target and its migration strategy (🌉 bridge / 🔁 rewrite / 🏗 re-architect), so you migrate construct-by-construct, side-by-side, non-destructively."
---

# Semantic Kernel → MAF — Migration Navigator

The full mapping is at **`docs/migration/from-semantic-kernel-to-maf.md`** (also served at `maf://migrate-from?source=semantic-kernel` when the MCP server is running).

This SKILL.md is the **index** — find the construct you're porting, read its strategy, then read that section of the guide. Migrate **side-by-side**: build a new MAF project beside the original SK project; never edit the SK code until the user switches over.

> **This is a cross-framework migration, not a version upgrade.** For "upgrade MAF 1.x → 1.y" use `maf-migration-guide` instead.

---

## Strategy legend

| Tag | Meaning | Do this |
|---|---|---|
| 🌉 **bridge** | Reuse the SK code as-is via an interop shim. | Lowest risk — prefer it. Keep the SK asset, wrap it. |
| 🔁 **rewrite** | Mechanical SK→MAF API swap (1:1 target). | Follow the guide's before/after. |
| 🏗 **re-architect** | No 1:1 mapping — redesign onto MAF workflows / providers. | Needs judgment — propose, confirm, then build. |

---

## Construct → MAF target → strategy (the lookup)

| SK construct | MAF target | Strategy | Guide § |
|---|---|---|---|
| `Kernel` / `IKernelBuilder` | *removed* — create the agent straight from the chat client | 🔁 | 2 |
| `ChatCompletionAgent` | `chatClient.AsAIAgent(instructions:, tools:)` → `ChatClientAgent` | 🔁 | 2 |
| `AzureAIAgent` / `OpenAIAssistantAgent` | `aiProjectClient.AsAIAgent(...)` / `assistantClient.CreateAIAgentAsync(...)` | 🔁 | 2 |
| `IChatCompletionService` | bridge `.AsChatClient()` → `IChatClient`, or a provider `IChatClient` | 🌉 | 11 |
| `[KernelFunction]` plugin | bridge `KernelFunction.AsAIFunction()`, or a plain `[Description]` method in `tools:` | 🌉 | 5 / 8 |
| `KernelPlugin` / `KernelPluginFactory` | *no plugin concept* — pass methods to `tools:` | 🌉 / 🔁 | 5 |
| `AgentThread` / `*AgentThread` | `await agent.CreateSessionAsync()` → `AgentSession` | 🔁 | 3 / 4 |
| `InvokeAsync` / `InvokeStreamingAsync` (agent) | `RunAsync` / `RunStreamingAsync` → `AgentResponse` / `AgentResponseUpdate` | 🔁 | 6 / 7 |
| `kernel.InvokeAsync(fn)` (function) | call the tool directly, or rely on automatic function-calling | 🔁 | 5 |
| `KernelArguments` / `AgentInvokeOptions` / `*PromptExecutionSettings` | `ChatOptions` / `ChatClientAgentRunOptions` | 🔁 | 9 |
| Structured output (`responseFormat`) | `ChatResponseFormat.ForJsonSchema<T>()` or typed `RunAsync<T>` | 🔁 | "Beyond the official page" |
| `ChatHistory` | `ChatHistoryProvider` / `InMemoryChatHistoryProvider` / `await agent.SerializeSessionAsync(session)` (async — the sync `SerializeSession` is gone on 1.3.0+) | 🔁 | "Beyond the official page" |
| Function-invocation filters (`IFunctionInvocationFilter`, …) | agent **middleware** via `.AsBuilder().Use(...)` | 🔁 | "Beyond the official page" |
| `AddOpenTelemetry` (kernel-level) | `.UseOpenTelemetry()` on the `IChatClient` chain | 🔁 | "Beyond the official page" |
| `AgentGroupChat` | a MAF **group-chat workflow** (`AgentWorkflowBuilder.CreateGroupChatBuilderWith`) | 🏗 | "Multi-agent" |
| Planners (`FunctionCallingStepwisePlanner`, `HandlebarsPlanner`, …) | *removed* — automatic function-calling loop | 🏗 | "Multi-agent" |
| Prompt templates (`KernelPromptTemplate`, Handlebars/Liquid, `KernelFunctionFromPrompt`) | render externally → pass as `instructions`, or a Context Provider | 🏗 | "Beyond the official page" |
| Vector stores / RAG (`IVectorStore`, `ITextSearch`, …) | `TextSearchProvider` via `AIContextProviders` | 🏗 | "Beyond the official page" |
| Hand-rolled memory injection | an `AIContextProvider` (no SK type — find it by reading the code) | 🏗 | "Beyond the official page" |

---

## Interop bridges (the gradual / side-by-side enabler)

There is **no SK→MAF converter**. The official aid is a set of C# interop bridges that let MAF reuse SK assets as-is (so SK + MAF can run side by side):

- `IChatCompletionService.AsChatClient()` → `IChatClient` (reverse: `IChatClient.AsChatCompletionService()`).
- `KernelFunction.AsAIFunction(Kernel?)` → `AIFunction` (reverse: `AIFunction.AsKernelFunction()`).
- Bulk: `kernel.Plugins.SelectMany(p => p).Select(f => f.AsAIFunction())`.

Bridge first, rewrite when ready. See the guide's **"Interop bridges"** section for the exact namespaces.

---

## Recommended order (for the plan)

1. **Packages & namespaces** — swap `Microsoft.SemanticKernel*` for `Microsoft.Agents.AI` + `Microsoft.Extensions.AI` (+ provider client).
2. **Tools first** — `[KernelFunction]` → plain methods (`AIFunctionFactory.Create`) — agents depend on them.
3. **Agents** — `…Agent { Kernel = … }` → `client.AsAIAgent(instructions:, tools:)`; drop the `Kernel`.
4. **Threads → sessions** — `new …AgentThread()` → `await agent.CreateSessionAsync()`.
5. **Invocation** — `Invoke*Async` → `Run*Async`.
6. **Options & DI** — `KernelArguments` / execution-settings → `ChatClientAgentRunOptions` / `ChatOptions`; `AddKernel` → `AddKeyedSingleton<AIAgent>`.
7. **Multi-agent / planners / memory / filters** — re-architect onto MAF workflows + middleware + Context Providers.
8. **Validate** — `maf-doctor doctor` on the migrated project; it should be anti-pattern clean.

---

## How to drive this

- **With the MCP server:** run `MafDetectSourceFramework(repoPath)` (CLI: `maf-doctor migrate-scan`) for the inventory + EASY/MEDIUM/HARD verdict, then the `maf-migrate-from` prompt to plan → scaffold → port.
- **Skills + agents only (no MCP):** read the construct table above, then the matching section of `docs/migration/from-semantic-kernel-to-maf.md`, and port one construct at a time. `dotnet build` the new project after each — green before moving on.

> ⚠ The two "no type signature" rows (hand-rolled memory, multi-agent beyond group chat) won't appear in the scan — review the code for them by hand.
