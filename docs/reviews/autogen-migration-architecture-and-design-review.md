# AutoGen → MAF Migration — Feasibility & Design Assessment

> **Scope:** assesses **building** an AutoGen → Microsoft Agent Framework (MAF) cross-framework migration feature in **maf-doctor**, mirroring the Semantic Kernel → MAF feature shipped in **v1.5.0**. This feature **does not exist yet**, so this is a *feasibility + design* assessment, not a review of shipped code.
> **Date:** 2026-06-27 · **Method:** 8 parallel research agents (framework history & versions · the two AutoGen .NET package surfaces · the MAF target API · the official migration guide · a construct-mapping deep-dive · OSS prior art · syntactic detection strategy · risk & scope), then **one agent per finding**, each **adversarially re-verified** by a second independent agent against primary sources (NuGet, Microsoft Learn, the AutoGen-for-.NET API reference, the AutoGen repo) and the shipped maf-doctor code. 35 findings; ~13 of them had a candidate sub-claim **stripped or corrected** on verification (recorded in §6).
> **Companion:** prioritized, sized build steps live in the **[AutoGen Migration Implementation Plan](./autogen-migration-implementation-plan.md)**.

---

## 1. Verdict & recommendation

**Feasible: yes — and architecturally low-risk.** The shipped SK detector is a *pure IO-free core wrapped in thin adapters* (`Scan` → `AnalyzeSource` → `SkScanResult`, plus a single-authority `MigrationSources` allow-list consumed by the CLI, the `maf://migrate-from` resource, and the `maf-migrate-from` prompt). The AutoGen feature is a near-clone of that shape against a different rule table. The repo *already anticipates this exact second source*: `MigrationSources.cs:9` names AutoGen as the "one-line edit," and `SemanticKernelDetectorTool.cs:34-39` explicitly defers the `ISourceFrameworkDetector` seam "until a second source (AutoGen) lands." The two extraction hooks (a shared `CsprojPackageReader`, the source-detector seam) are pre-designed as Tier-3 of the SK remediation plan. The .NET AutoGen surface is real and Roslyn-visible.

**Worth building: a qualified yes.** Two facts temper the ROI and must shape expectations, both **verified**:

1. **The .NET AutoGen surface is small and frozen.** `AutoGen.Core` peaked at **0.2.3** (published 2025-04-09, ~174.6K all-time downloads); the 0.4 .NET runtime `Microsoft.AutoGen.Core` **never shipped a stable build** (latest `0.4.0-dev.3`, 2025-03-19, ~14.5K downloads). AutoGen is officially in **maintenance mode** (microsoft/autogen Discussion #7066) and converges into MAF.
2. **Most AutoGen code is Python**, which a Roslyn/.NET scanner structurally cannot see, and **Microsoft's own AutoGen→MAF guide is Python-only** (no C# pivot — see §3).

The countervailing case is strong on brand and uniqueness: Microsoft frames MAF as the **convergence/successor of BOTH Semantic Kernel and AutoGen**, so completing the cross-framework story is on-mission; and we found **no OSS or Microsoft programmatic AutoGen-.NET→MAF detector/planner** — the niche is genuinely uncontested. The honest framing is: build it, right-size it, and ship it as *detect + plan*, not auto-rewrite.

### Recommended scope for a first release (crisp)

- **Detect the AutoGen-for-.NET surface only** (csproj/CPM package refs + C# type/attribute/invocation syntax via Roslyn). Mirror SK exactly.
- **Primary catalogue: the `AutoGen.*` 0.2.x family** (the stable, adopted surface — this is the legacy code a migration tool exists to find).
- **`Microsoft.AutoGen.*` 0.4 runtime: ONE coarse Rearchitect "detect-and-warn"** finding, not a per-type catalogue (low footprint, event-driven actor/gRPC model with no MAF parity).
- **Python AutoGen (`pyautogen`/`autogen-agentchat`/`autogen-core`): an explicit NON-GOAL of the detector.** At most an optional informational redirect (glob `requirements.txt`/`pyproject.toml`, point at the official Python guide), firewalled from the Roslyn construct list.
- **Strategy distribution is the inverse of SK:** **Rewrite + Rearchitect dominant, Bridgeable ≈ empty** (there is no AutoGen interop shim).
- **Author — do not mirror — the human guide.** There is no official C# AutoGen→MAF page; the `from-autogen-to-maf.md` guide is community-derived and must say so.

### Scorecard

| Category | Health | Key finding |
|---|---|---|
| **Scope** | 🟢 Good | The scope question is decisively resolvable with evidence: .NET-only, both package families (weighted by maturity), Python deferred. The only genuine subtlety is provenance — the official guide is Python-only, so mappings are **derived, not mirrored** (SCOPE-001/002/003). |
| **Architecture** | 🟢 Good | Near-verbatim clone of the shipped pure-core + thin-adapter template; the seams (`CsprojPackageReader`, `ISourceFrameworkDetector`) are already designed. Watch: do **not** register a second `[McpServerTool]` of the same name; dispatch one tool by `--source` (ARCH-001/002/003). |
| **Construct-mapping** | 🟡 Fair | The bulk of the new work, and **harder than SK**: no official C# table to mirror, an essentially-empty Bridgeable tier, and several **Python-only "phantom" types** (`MagenticOneGroupChat`, `SelectorGroupChat`, `Swarm`) that must live in prose, not `KnownTypes` (MAP-001…009). |
| **Detection-strategy** | 🟡 Fair | **Materially higher false-positive risk than SK.** AutoGen type names are generic (`Message`, `Role`, `GroupChat`, `IMiddleware`), the package token `AutoGen` is short/collision-prone, and the *from* namespace `Microsoft.AutoGen.*` is a prefix-sibling of the MAF *target* `Microsoft.Agents.AI` (DET-001…006, RISK-001). |
| **MAF-alignment** | 🟡 Fair | Every fix Note is maf-doctor **inference** validated against the live `Microsoft.Agents.AI` C# API — there is no official labelled table. Traps: `AsAIAgent` (not `CreateAIAgent`) per the shipped SK rows, the required `.AsIChatClient()` hop, and `MagenticWorkflowBuilder` being experimental (`MAAIW001`) (MAF-001/002). |
| **Risk** | 🟠 Watch | The false-positive **blast radius is onto MAF itself and the unrelated M365 Agents SDK** (`Microsoft.Agents.*`); the detector must never tell a user to "migrate off MAF." Plus the small/frozen .NET footprint caps ROI and the verdict skews MEDIUM→HARD, not "HARD-dominant" (RISK-001/002). |

---

## 2. The AutoGen → MAF landscape (verified, with honest uncertainty)

**Two AutoGen generations, two *.NET* package families.** AutoGen began as `pyautogen` (0.2: `ConversableAgent`, `AssistantAgent`, `UserProxyAgent`, `GroupChat`/`GroupChatManager`, `register_function`) and was rewritten as **0.4** (`autogen-core` + `autogen-agentchat`: event-driven actor runtime, `RoundRobinGroupChat`, `SelectorGroupChat`, `MagenticOneGroupChat`, `Swarm`, `GraphFlow`). On .NET there are **two distinct, Microsoft-published families**, and the detector must recognize both:

| Family | Lineage | NuGet ids | Latest | Root namespace |
|---|---|---|---|---|
| **AutoGen for .NET** | port of 0.2 | bare `AutoGen`, `AutoGen.Core`, `AutoGen.OpenAI`, `AutoGen.SemanticKernel`, `AutoGen.DotnetInteractive`, `AutoGen.SourceGenerator`, `AutoGen.LMStudio`, `AutoGen.Mistral`, `AutoGen.Ollama`, `AutoGen.Anthropic`, `AutoGen.Gemini`, `AutoGen.AzureAIInference`, `AutoGen.WebAPI`, `AutoGen.OpenAI.V1` | **0.2.3** stable (2025-04-09) | `AutoGen`, `AutoGen.Core`, … |
| **Microsoft.AutoGen** | the 0.4 runtime | `Microsoft.AutoGen.Core`, `.Contracts`, `.Core.Grpc`, `.RuntimeGateway.Grpc`, `.AgentChat` | **0.4.0-dev.3** prerelease only (2025-03-19) | `Microsoft.AutoGen.*` |

**MAF is the convergence/successor of *both* SK and AutoGen** ([devblogs RC announcement](https://devblogs.microsoft.com/agent-framework/migrate-your-semantic-kernel-and-autogen-projects-to-microsoft-agent-framework-release-candidate/)), GA 1.0 in April 2026 (`Microsoft.Agents.AI`, latest ~1.11.x). AutoGen is in **maintenance mode** — "stable API … critical bug fixes and security patches … no significant new features," new users directed to MAF ([microsoft/autogen #7066](https://github.com/microsoft/autogen/discussions/7066)).

**The load-bearing asymmetry vs SK — the official AutoGen→MAF guide is PYTHON-ONLY.** [`learn.microsoft.com/.../migration-guide/from-autogen/`](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/) exists and is current (updated 2026-06-26), but its front-matter description reads *"migrating from AutoGen to the Microsoft Agent Framework **Python SDK**"*, `monikers: []`, and **every code sample is Python**; the `?pivots=programming-language-csharp` URL returns the identical Python body — there is **no C# pivot**, unlike the SK guide that the SK feature mirrors with permission. Consequence: maf-doctor's AutoGen construct→MAF mappings and `docs/migration/from-autogen-to-maf.md` must be **authored** (triple-sourced from the Python guide's semantics + the AutoGen-for-.NET API + the live `Microsoft.Agents.AI` C# API), **not transcribed**, and labelled as community-derived inference.

**Honest uncertainty.** (a) The MAF .NET orchestration APIs (`AgentWorkflowBuilder`, `MagenticWorkflowBuilder` — the latter is experimental `MAAIW001`) are the youngest part of MAF and can move; pin the Notes loosely and drift-test them. (b) Several construct *source* names are Python-only (see §4 "phantom types") and must never enter a C# `KnownTypes` dictionary. (c) The 0.4 `Microsoft.AutoGen.*` internal type list (`AgentsAppBuilder`, `RoutedAgent`, `[TypeSubscription]`) was confirmed from the dev API reference but should be re-pinned against the package at build time.

---

## 3. Findings (grouped by category, P0 first)

Each finding was researched, then adversarially re-verified. Disposition is **confirmed** or **revised** (no candidate finding survived verification entirely unchanged in the revised cases — the corrected sub-claims are in §6). Source anchors use `file:line` for in-repo and URLs for external.

### 3.1 Scope decisions — **P0** (these gate everything)

**SCOPE-001 — Target the AutoGen .NET surface only; both families, weighted by maturity; Python deferred by construction.** *(revised)* The detector is Roslyn syntax + `.csproj`/`Directory.Packages.props` XML, no `SemanticModel`, no Python front-end (`SemanticKernelDetectorTool.cs`: `CSharpSyntaxTree.ParseText`, `XDocument.Load`). It structurally cannot parse Python, so the feature **must** target .NET — and both .NET AutoGen families are real and Roslyn-visible. Direction upheld. **Corrections:** the proposed `StartsWith("AutoGen.")` gate is buggy — it **misses the bare `AutoGen` meta-package id/namespace** and a naive `StartsWith("AutoGen")` **over-matches `AutoGenerated`/`AutoGenerator`** (verified: `AutoGenerator` is a real, now-unlisted NuGet package). Gate segment-aware: `id == "AutoGen" || id.StartsWith("AutoGen.") || id.StartsWith("Microsoft.AutoGen.")`. *Sources: [AutoGen.Core](https://www.nuget.org/packages/AutoGen.Core), [Microsoft.AutoGen.Core](https://www.nuget.org/packages/Microsoft.AutoGen.Core); `SemanticKernelDetectorTool.cs:162`.*

**SCOPE-002 — First release: AutoGen-for-.NET 0.2 as the primary catalogue; `Microsoft.AutoGen.*` 0.4 as a coarse detect-and-warn.** *(revised)* Verified version/adoption asymmetry (0.2.3 stable / ~175K vs 0.4.0-dev.3 prerelease / ~14K) and the repo's own README split: "AutoGen.* … will gradually be deprecated and ported into the new packages [Microsoft.AutoGen.*]," whose "APIs are not yet stable." Deprecation *strengthens* the decision — legacy 0.2 code is exactly what a migration tool exists to detect. **Correction:** the candidate's illustrative catalogue (`ConversableAgent`, `register_function`, …) leaned on **Python** names; `register_function` is Python-only, and `AssistantAgent`/`UserProxyAgent`/`ConversableAgent` live in the **meta `AutoGen` namespace, not `AutoGen.Core`** — the namespace gate must cover both. *Sources: [autogen-for-net API](https://microsoft.github.io/autogen-for-net/api/AutoGen.Core.html), [Installation](https://microsoft.github.io/autogen-for-net/articles/Installation.html).*

**SCOPE-003 — No official C# AutoGen→MAF guide exists (Python-only); .NET mappings are DERIVED, not mirrored, and must be labelled.** *(confirmed, strengthened)* Independently verified: the from-autogen page declares **no `zone_pivot_groups`** and contains **zero `::: zone pivot=` markers** across all lines; all ~40 code blocks are Python. The SK guide, by contrast, carries a real C# pivot. **Strengthening guardrail:** the .NET AutoGen *source* type names also differ entirely from the Python guide's (the guide says `AssistantAgent`/`RoundRobinGroupChat`/`MagenticOneGroupChat`; the .NET surface exposes `ConversableAgent`/`GroupChat`/`GroupChatManager`/`IAgent`). A naive implementer who "seeds KnownTypes from the Python guide" would produce a detector that **silently finds nothing** in C#. Key on the verified .NET names. *Source: [from-autogen guide](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/).*

### 3.2 Architecture

**ARCH-001 (P0) — Clone the pure-core + thin-adapter shape into `AutoGenDetectorTool`.** *(revised)* The template contract is verified present (`internal static Scan`/`DetectPackages`, `public static AnalyzeSource`, records `Sk*`, the `MigrationStrategy` enum, the repo-wide global-using sweep, `IsTypeReference`, the per-kind dedupe, `ToMarkdown`/`ToJson`/`Verdict`, the JSON-reconciliation invariant). Reuse `MigrationStrategy`, `SourceFileWalker`, `PathGuard` verbatim. **Three corrections to the candidate's "mirror VERBATIM" framing:** (1) the single-prefix `StartsWith("Microsoft.SemanticKernel")` gate (`:162`, `:413`) must become a **two-root, dot-boundary** check (AutoGen is two families and a far less distinctive token); (2) **do NOT add a second `[McpServerTool] MafDetectSourceFramework`** — duplicate MCP tool names break the server; extract the `ISourceFrameworkDetector` seam the class doc anticipates and dispatch one tool by `--source`; (3) ground the catalogue and the `AnalyzeSource` unit tests in the AutoGen **.NET** API, not the Python guide. *Sources: `SemanticKernelDetectorTool.cs:34-39,141-199`; [from-autogen guide](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/).*

**ARCH-002 (P1) — Extract the shared `CsprojPackageReader.ReadPackageReferences(repoPath, idPredicate)` + CPM resolver (SK remediation Tier-3 #12).** *(revised)* The ~94-line `DetectPackages` + `ReadCentralPackageVersions` body is the right thing to lift; the CPM resolver is **already prefix-agnostic** and moves verbatim. **Correction:** "only the `StartsWith` prefix is framework-specific" understates it — the **`SkPackage` return record is also SK-specific**, so the shared reader must return a neutral shape (`IReadOnlyList<(string Id, string Version)>` or a new `PackageRef`) and each detector projects to its own type. Land it in the *same* PR as `AutoGenDetectorTool` so the second detector is born calling the shared reader. Keep `DetectPackages` as a one-line wrapper so the 8 existing package tests stay green. **Priority P1, not P0** — the docs rate it effort-M, Tier-3, and the un-extracted state is an accepted trade-off today. *Sources: `SemanticKernelDetectorTool.cs:141-240`; `sk-migration-remediation-plan.md:94-98`.*

**ARCH-003 (P1) — Extract the `ISourceFrameworkDetector` seam and route `migrate-scan` by canonical source.** *(revised)* `Program.cs:150` unconditionally instantiates `new SemanticKernelDetectorTool()`; the moment a second canonical source exists this must dispatch on `MigrationSources.Canonicalize(source)` (mirroring `MafResources.GetMigrationFrom`). **Correction:** the candidate's "fold three duplicated rejection bodies into one constant" is wrong — the three sites are **not duplicated**. `MafPrompts.cs:201` (an LLM **STOP** directive) and `MafResources.cs:59` (an `ArgumentException`) already consume `MigrationSources.SupportedDescription` and have legitimately distinct framing; **only `Program.cs:145` hard-codes** the literal supported-list string. The correct, narrower fix: make `Program.cs:145` read `SupportedDescription`; do not collapse the three. *Sources: `Program.cs:143-152`, `MafPrompts.cs:199-204`, `MafResources.cs:55-61`.*

### 3.3 Detection-strategy

**DET-001 (P0) — Package-ref gate: exact `AutoGen` OR `AutoGen.` OR `Microsoft.AutoGen.` (OIC); the dot is load-bearing.** *(revised)* Verified the full 19-package profile on nuget.org. The exact-match arm is **provably necessary** (the bare `AutoGen` meta-package exists, latest 0.2.3) and the dot is load-bearing on the positive side (`AutoGenerator` is a real package; a bare `StartsWith("AutoGen")` would false-match it). **Corrections:** the candidate's second false-match example `AutoGenU` is **not** a NuGet id (404) — drop it; and `CsprojPackageReader.ReadPackageReferences` is a *proposed* extraction (ARCH-002), not shipped code, so either inline the predicate or pass it to the reader if ARCH-002 lands first. Pin the detector's own 19-id catalogue in a `*_NoDrift` test; put the `0.2.3`/`0.4.0-dev.3` version anchors in the `samples/autogen-sample` fixture, not a live-NuGet assertion. *Sources: [AutoGen profile](https://www.nuget.org/profiles/AutoGen), [AutoGenerator](https://www.nuget.org/packages/AutoGenerator).*

**DET-002 (P0) — The namespace `using`-gate `ImportsAutoGen` is MORE load-bearing than SK's, because AutoGen type names are generic.** *(confirmed)* `AutoGen.Core` declares `GroupChat`, `Message`, `TextMessage`, `Role` (a struct), `Graph`, `ToolCall` — names that collide with ordinary code and the `Microsoft.Extensions.AI` vocabulary MAF builds on. The gate **must** include `ns == "AutoGen"` (the bare namespace holds `AssistantAgent`/`UserProxyAgent`/`ConversableAgent`/the config types) plus `AutoGen.*` and `Microsoft.AutoGen.*` (Ordinal, `global::`-tolerant, segment-aware so `AutoGenerated.*` is excluded). *Sources: [AutoGen.Core](https://microsoft.github.io/autogen-for-net/api/AutoGen.Core.html), [AutoGen ns](https://microsoft.github.io/autogen-for-net/api/AutoGen.html).*

**DET-003 (P0) — Collision tiers; never match bare `Message`/`Role`/`GroupChat`/`IMiddleware`/`BaseAgent`; exclude the MAF prefixes; coexist with `AutoGen.SemanticKernel`.** *(revised)* Three tiers: distinctive names (`ConversableAgent`, `UserProxyAgent`, `RoundRobinGroupChat`, `GroupChatManager`, `MiddlewareAgent`) match inside the gate; generic names match **only** via exact `KnownTypes` membership; never-detect names are dropped. The **MAF-exclusion is mandatory**: the *from* namespace `Microsoft.AutoGen.*` is a prefix-sibling of the *target* `Microsoft.Agents.AI`, so the gate must match `Microsoft.AutoGen` precisely and explicitly exclude `Microsoft.Agents.*` / `Microsoft.Extensions.AI*`. `IMiddleware` collides with `Microsoft.AspNetCore.Http.IMiddleware` — never match it bare. **Corrections:** `BaseAgent` lives **only** in `Microsoft.AutoGen.Core` (not both families) and has **no Orleans link** — keep never-detect, but for the right reasons (user-subclassed base + it is the MAF *Python* base-class name); and `Message`/`Role` do **not** literally collide with MAF (MAF uses `ChatMessage`/`ChatRole`) — the justification is genericness. `AutoGen.SemanticKernel` firing both detectors on a bridge file is **correct**, a reporting-clarity item, not a bug. *Sources: [autogen-for-net](https://microsoft.github.io/autogen-for-net/api/AutoGen.Core.html), [Microsoft.AutoGen.Core BaseAgent](https://github.com/microsoft/autogen/blob/main/dotnet/src/Microsoft.AutoGen/Core/BaseAgent.cs), [Microsoft.Agents.AI](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai).*

**DET-004 (P1) — Anti-suffix rule: forbid a `*Agent` suffix fallback; the single safe suffix is `*GroupChat` (accepted trade-off, gated).** *(revised)* SK's safe suffixes (`*AgentThread`, `*PromptExecutionSettings`) are SK-coined; AutoGen has no equivalent. A blanket `*Agent` fallback is **actively harmful** — it matches MAF's own `AIAgent`/`ChatClientAgent` (verified), user `FooAgent` types, and AutoGen infra agents (`MiddlewareAgent`, `DefaultReplyAgent`). Enumerate agents explicitly (also detect the `IAgent`/`IGroupChat` interfaces, since middleware chains surface as `IAgent`). `*GroupChat` is the **one** allowed suffix — verified collision-free vs MAF (`GroupChatBuilder`/`RoundRobinGroupChatManager`/`GroupChatState` never end in "GroupChat"). **Correction:** `*GroupChat` is NOT bulletproof — "GroupChat" is plain English, so a messaging domain model is a plausible FP; pin it as an **accepted trade-off** mirroring SK's `UserTypeEndingInAgentThread_IsFlagged_AcceptedTradeoff`, not as zero-risk. *Sources: [AIAgent](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.aiagent), [group-chat orchestration](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/group-chat).*

**DET-005 (P1) — Document the load-bearing `Classify` ordering: `[Function]` in the node switch, then exact interfaces/orchestrators → `KnownTypes` → single `*GroupChat` suffix.** *(revised)* Mirror SK's documented ordering invariant. **Corrections:** `[Function]` belongs in the **node switch** (like `[KernelFunction]`), not in `Classify`; the ordered doc-comment governs only the type-name steps. The binding reason is **sharper** than SK's: `IGroupChat` literally ends in "GroupChat," a real collision, so the suffix must exclude it (`EndsWith("GroupChat") && name != "IGroupChat"`). And the concrete orchestrators (`RoundRobinOrchestrator`, `RolePlayOrchestrator`, `WorkflowOrchestrator`) end in "Orchestrator," not "GroupChat" — they must be exact `KnownTypes` rows; there is deliberately **no `*Orchestrator` suffix**. *Sources: [FunctionAttribute](https://microsoft.github.io/autogen-for-net/api/AutoGen.Core.FunctionAttribute.html), [IGroupChat](https://github.com/microsoft/autogen/blob/main/dotnet/src/AutoGen.Core/GroupChat/IGroupChat.cs).*

**DET-006 (P1) — Add attribute + invocation signals: `[Function]`+`AutoGen.SourceGenerator`, `RegisterMiddleware`/`RegisterMessageConnector`/`RegisterPrintMessage`, `InitiateChatAsync`, `GenerateReply(Streaming)Async`, gated `SendAsync`.** *(revised)* Every identifier verified. **Corrections:** `GenerateReplyAsync` is a method on the **`IAgent` interface**, not an `AgentExtension` extension; add the missing streaming twin `GenerateStreamingReplyAsync` (`IStreamingAgent`); `RegisterMessageConnector` is a **provider-package** extension (`AutoGen.OpenAI.Extension`), not `AutoGen.Core`; and `SendAsync` cannot use SK's fixed receiver-name allow-list (AutoGen has no `kernel` convention; `HttpClient.SendAsync` collides) — track detected-agent variables within the file and split the two `SendAsync` overloads (single-message → Rewrite `RunAsync`; agent→agent → Rearchitect workflow). *Sources: [AgentExtension](https://microsoft.github.io/autogen-for-net/api/AutoGen.Core.AgentExtension.html), [IAgent](https://microsoft.github.io/autogen-for-net/api/AutoGen.Core.IAgent.html), [AutoGen.SourceGenerator](https://www.nuget.org/packages/AutoGen.SourceGenerator).*

### 3.4 Construct-mapping (rendered as cheat-sheets)

The strategy legend mirrors the SK skill: **🌉 bridge** (reuse as-is via an interop shim) · **🔁 rewrite** (mechanical 1:1 API swap) · **🏗 re-architect** (no 1:1 — redesign onto MAF workflows/middleware). Every row below is maf-doctor **inference**, triple-sourced (Python guide semantics + AutoGen-for-.NET API + live `Microsoft.Agents.AI` C# API), and must validate against the shipped MAF API before it is pinned (see MAF-001).

**MAP-001 (P1) — Headline inversion vs SK: the Bridgeable tier is essentially EMPTY.** *(revised)* The official guide ships **no AutoGen-specific interop shim** (nothing analogous to SK's `.AsChatClient()` / `KernelFunction.AsAIFunction()`); it is pure rewrite + re-architect. `.AsAIAgent()` converts model **clients**, not AutoGen agents. The only genuine bridge is the shared `Microsoft.Extensions.AI` substrate — a pre-existing `IChatClient` instance and `AIFunction` tools carry over — and **none of those are AutoGen types**, so in an AutoGen-scoped scan the Bridgeable count is typically **zero**. Express it as a report note, not as mislabeled AutoGen-type rows. (Verified correction: `OpenAIChatAgent`/`OpenAIConfig`/`ILLMConfig` are Rewrite, not Bridgeable.)

**Agents & single-agent surface** *(MAP-002 — P1 / MAP-003 — P1)*

| AutoGen construct | MAF target | Strategy | Note |
|---|---|---|---|
| `ConversableAgent` / `AssistantAgent` (ns `AutoGen`) | `chatClient.AsAIAgent(instructions:, tools:)` → `ChatClientAgent` | 🔁 | 1:1-ish; `RunAsync` auto-runs the tool loop (delete manual reply/`SendAsync` chaining). `AssistantAgent` is generic — gate strictly. |
| `OpenAIChatAgent` (ns `AutoGen.OpenAI`) | `openAIClient.GetChatClient(model).AsIChatClient().AsAIAgent(...)` | 🔁 | Wraps the OpenAI SDK `ChatClient`; the **`.AsIChatClient()` hop is required** or it won't compile. |
| `SemanticKernelAgent` (ns `AutoGen.SemanticKernel`) | `ChatClientAgent` (reuse SK via `.AsChatClient()`) | 🔁 | The only "near-bridge" — keep SK inference via the SK→MAF bridge, then `AsAIAgent`. |
| `MiddlewareAgent` / `MiddlewareStreamingAgent` | `AIAgent` + `.AsBuilder().Use(...)` middleware | 🔁 | AutoGen middleware pipeline → MAF agent/function middleware. |
| `IAgent` / `IStreamingAgent` | `AIAgent` (`RunAsync` / `RunStreamingAsync`) | 🔁 | Custom agent impls become `AIAgent`. |
| `UserProxyAgent` (ns `AutoGen`) | Workflow request/response gate (`RequestInfoExecutor` + `InputPort` → `RequestInfoEvent`); code-exec → `HostedCodeInterpreterTool` | 🏗 | **No single MAF type.** The detectable .NET `UserProxyAgent` is human-input-primary (no `code_execution_config` — that is AutoGen 0.2 Python). |

**MAF target naming correction (MAP-002):** use **`AsAIAgent`** (the form in the shipped SK rows and the latest C# Learn guide), **not** `CreateAIAgent`; drop `max_tool_iterations` from .NET Notes (it is a Python `AssistantAgent` parameter).

**Tools / functions** *(MAP-005 — P2)* — all 🔁:

| AutoGen construct | MAF target | Note |
|---|---|---|
| `[Function]` / `FunctionAttribute` + `AutoGen.SourceGenerator` | `AIFunctionFactory.Create(method)` passed via `tools:` (ns `Microsoft.Extensions.AI`) | Drop the source generator; MAF infers schema from the signature + `[Description]`; any return type allowed. |
| `FunctionContract` / `FunctionParameterContract` | `AIFunction` (auto-built) | Redundant under MAF. |
| `FunctionCallMiddleware` | *removed* — MAF's function-invoking chat client runs the loop | Delete the middleware + `functionMap`. |

**Calling / streaming / connectors / messages** *(MAP-006 — P2)*:

| AutoGen construct | MAF target | Strategy | Note |
|---|---|---|---|
| `IAgent.GenerateReplyAsync` | `AIAgent.RunAsync` | 🔁 | Read `AgentResponse.Text`; adapt `IMessage`→`ChatMessage`. |
| `IStreamingAgent.GenerateStreamingReplyAsync` | `AIAgent.RunStreamingAsync` | 🔁 | Consume `AgentResponseUpdate`. |
| `SendAsync` (single-message, receiver-gated) | `AIAgent.RunAsync` | 🔁 | Gate on a detected-agent receiver — `HttpClient/SmtpClient.SendAsync` collide. |
| `SendAsync` (agent→agent overload) / `InitiateChatAsync` | a MAF workflow (`AgentWorkflowBuilder`) | 🏗 | Multi-round two-party conversation is a workflow, not a single `RunAsync`. |
| `RegisterMessageConnector()` / `RegisterPrintMessage()` | *removed* / logging middleware | 🔁 | MAF speaks `Microsoft.Extensions.AI.ChatMessage` natively. |
| `MultiModalMessage` / `ToolCallMessage` / `ToolCallResultMessage` / `ToolCallAggregateMessage` | `ChatMessage` + typed content (`FunctionCallContent`/`FunctionResultContent`) | 🔁 | **Only the distinctive message types.** **Never** add bare `Message`/`TextMessage`/`Role` to `KnownTypes` (FP risk; `Message` is AutoGen-legacy). |

**Group chat / orchestration** *(MAP-004 — P1)* — the dominant 🏗 bucket and the verdict driver:

| AutoGen construct | MAF target | Strategy | Note |
|---|---|---|---|
| `RoundRobinGroupChat` | `AgentWorkflowBuilder.CreateGroupChatBuilderWith(a => new RoundRobinGroupChatManager(a){MaximumIterationCount=N})...Build()` | 🏗 | Round-robin team → group-chat workflow. |
| `GroupChat` / `IGroupChat` | `CreateGroupChatBuilderWith(...)` with a custom manager | 🏗 | Admin-driven chat → group-chat workflow. |
| `SequentialGroupChat` *(`[Obsolete]`)* | `AgentWorkflowBuilder.BuildSequential(agents)` | 🏗 | Sequential pipeline. |
| `GroupChatManager` | manager-factory delegate / custom manager (override `ShouldTerminateAsync`) | 🏗 | Speaker-selection/termination logic. |
| `IOrchestrator` / `RoundRobinOrchestrator` / `RolePlayOrchestrator` | `CreateGroupChatBuilderWith` / `RoundRobinGroupChatManager` | 🏗 | Speaker-selection orchestration. |
| `WorkflowOrchestrator` / `Graph` / `Transition` | `WorkflowBuilder.AddEdge(source, target[, condition])` | 🏗 | **Low-level** typed graph (not `AgentWorkflowBuilder`). |

> **MAP-004 correction:** `MagenticOneGroupChat`, `SelectorGroupChat`, and `Swarm` are **Python `autogen-agentchat` types with no AutoGen .NET equivalent** — a C# detector never encounters them. They belong in the guide/skill prose (→ `MagenticWorkflowBuilder` / `CreateGroupChatBuilderWith` + custom manager / `CreateHandoffBuilderWith`), **not** in `KnownTypes` (a dead row).

**Code execution & the 0.4 runtime** *(MAP-007 — P2 / MAP-008 — P2)*:

| AutoGen construct | MAF target | Strategy | Note |
|---|---|---|---|
| `AutoGen.DotnetInteractive` (`InteractiveService`, `DotnetInteractiveKernelBuilder`, `RegisterDotnetCodeBlockExectionHook`) | `HostedCodeInterpreterTool` (hosted, provider-side, Python) | 🏗 | **No LOCAL C#/F# exec in MAF** (roadmap; emerging CodeAct/Hyperlight preview is JS/Python only). **Note the misspelled identifier** `RegisterDotnetCodeBlockExectionHook` (verbatim — `Exection`) or the detector never fires. |
| `Microsoft.AutoGen.*` 0.4 runtime (`BaseAgent`, `IHandle<T>`, `IAgentRuntime`, `AgentId`/`TopicId`, `[TypeSubscription]`, `AgentsApp(Builder)`, `InProcessRuntime`) | a MAF `Workflow` of typed executors + edges | 🏗 | **ONE coarse Rearchitect construct, never per-type.** Gate on the `Microsoft.AutoGen` prefix. |
| `Microsoft.AutoGen.*.Grpc` / `RuntimeGateway.Grpc` (distributed) | *no MAF parity* | 🏗 | "single-process today; distributed execution is planned" (verbatim, official guide). |

**MAP-009 (P1) — Honesty rows for genuinely no-MAF-target constructs.** *(revised)* Mirror SK's `*Planner → planners were removed` discipline (drift-pinned, never over-promise). **Correction:** the genuine .NET-detectable no-target rows are exactly **two** — the distributed gRPC runtime (above) and **local** code execution (above). The candidate's other two (Swarm/handoff, SelectorGroupChat) **already ship MAF .NET targets** (`CreateHandoffBuilderWith`, `CreateGroupChatBuilderWith` + custom manager) — labelling them "roadmap / no 1:1" would *under*-promise and be factually wrong; the Python "Future Patterns" label refers only to a missing *named Python builder*. *Sources: [handoff](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/handoff), [group-chat](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/group-chat).*

### 3.5 MAF-alignment

**MAF-001 (P1) — Validate every fix Note against the LIVE `Microsoft.Agents.AI` C# API; the strategy tags are maf-doctor inference, not an official table.** *(confirmed)* Each MAF target verified real (`ChatClientAgent`, `AsAIAgent`, `AIFunctionFactory.Create`, `AgentWorkflowBuilder.BuildSequential`/`BuildConcurrent`/`CreateGroupChatBuilderWith`/`CreateHandoffBuilderWith`, `MagenticWorkflowBuilder`; `Microsoft.Agents.AI` GA 1.11.x). Four traps the Note review **must** catch: `AsAIAgent`/`CreateAIAgent` are `IChatClient` extensions — a concrete provider client needs `.AsIChatClient()` first; `AIFunctionFactory` is in `Microsoft.Extensions.AI`, not `Microsoft.Agents.AI`; `MagenticWorkflowBuilder` is a **constructor** behind experimental `MAAIW001`; and there are **no AutoGen interop bridges**, so the Bridgeable tag should be rare-to-absent. *Sources: [AsAIAgent](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatclientextensions.asaiagent), [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI/), [ai-agents-for-beginners#434](https://github.com/microsoft/ai-agents-for-beginners/issues/434).*

**MAF-002 (P2) — Use current `AgentSession`/`CreateSessionAsync` on target Notes; AutoGen has no thread construct to dual-pin on the source side.** *(revised)* The MAF `AgentThread`→`AgentSession` / `GetNewThread()`→`CreateSessionAsync()` rename is real and both spellings are live; emit the current spelling on the *target* side of any continuity Note (matching the shipped SK rows). **Correction:** the candidate's "dual-pin both spellings in the sample fixture" is SK-specific and **does not transfer** — AutoGen has no `AgentThread`/`GetNewThread` source construct, so there is nothing to dual-pin, and SK's `*AgentThread` suffix machinery must **not** be ported. *Sources: [AgentSession](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai.agentsession), [magentic](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/magentic).*

### 3.6 Delivery-surfaces

**SURF-001 (P1) — The `MigrationSources.cs` edit is necessary but NOT a self-contained one-liner.** *(revised)* Add `AutoGen` const + `Canonicalize` arm + extended `SupportedDescription` — but it must land with: the `MafResources.GetMigrationFrom` switch arm + embedded `migrate-from/autogen.md` + the csproj `<EmbeddedResource>` (else `ReadEmbedded` throws the instant the gate opens); the two **hard-coded literals** at `Program.cs:145` and the `MafResources.cs:54` `[Description]` attribute (a compile-time constant that *cannot* reference `SupportedDescription`); and the **flipped negative guard-tests** (below). *Sources: `MigrationSources.cs:9`, `Program.cs:145`, `MafResources.cs:54-61`, `maf-autopilot.csproj:88`.*

**SURF-002 (P1) — `Program.cs` `migrate-scan` wiring.** *(confirmed)* Replace the hard-coded SK error (`:145`) with `SupportedDescription`, dispatch the detector on `Canonicalize(source)` (collapsing the redundant pre-flight guard into the switch default), and update the `--help` block (`:56-58`) to surface `--source` and enumerate both sources. Not independently shippable — sequence after ARCH-002 + the detector. *Source: `Program.cs:56-58,132-153`.*

**SURF-004 (P2) — Source-parameterize the `maf-migrate-from` prompt body and the `maf://migrate-from?source=<canonical>` URI.** *(confirmed)* The gate already delegates to `MigrationSources.IsSupported`, but everything below it is SK-hardwired (title, body, the literal `?source=semantic-kernel`, the SK bridge bullets). The AutoGen branch must lead with 🔁/🏗 and present 🌉 as empty — **not** reuse the SK bridge narrative. Land after the MigrationSources entry + embedded guide. *Source: `MafPrompts.cs:174-242`.*

**SURF-005 (P2) — Flip the `autogen` "rejected example" tests to "supported," re-pick a still-unsupported negative.** *(revised)* `MigrateScanCliTests.cs:70` (`[InlineData("autogen", false)]`) and `MafPromptsTests.cs:54` (STOP body for `source:"autogen"`) **invert** the moment AutoGen joins the allow-list — this is the drift discipline working as designed. **Correction:** the negative swap is needed **only** in `MafPromptsTests` (`MigrateScanCliTests` already has a `langchain` negative at `:71`). Add a `MigrationSources` `*_NoDrift` test pinning the new alias and reconciling the two hard-coded literals — that is the true "wiring complete" signal. *Sources: `MigrateScanCliTests.cs:61-71`, `MafPromptsTests.cs:54`.*

### 3.7 Docs & samples

- **DOC-001 (P2) — Author `docs/migration/from-autogen-to-maf.md` (DERIVED) + embed as `migrate-from/autogen.md`.** *(confirmed)* Open with a banner: the official guide is Python-only; AutoGen for .NET 0.2.x is **frozen/legacy** (not "experimental" — `AutoGen.Core` 0.2.3 carries no NuGet deprecation flag) and `Microsoft.AutoGen.*` is **preview/dev-only**; and the strategy distribution is Rewrite/Rearchitect with ~zero Bridgeable.
- **DOC-002 (P2) — Add `.github/skills/maf-from-autogen/SKILL.md` and wire the allow-list in the SAME commit.** *(revised)* Three `MafResourcesTests` gates fail otherwise (`AllowedSkillNames_MatchSkillFolders_NoDrift`, `GetSkill_DescriptionLists_EveryAllowlistedName`, `GetSkill_EveryAllowlistedName_Resolves`). The drift guard is a **one-directional strategy-tag agreement test** (mirror `SkillConstructTable_StrategyTags_MatchDetector_NoDrift`), and the `coupled >= 12` floor must be **recalibrated** down — many AutoGen constructs are method/runtime-shaped and won't round-trip as clean type names.
- **DOC-003 (P2) — Add `samples/autogen-sample` + `AutoGenSampleSmokeTests`, with counts computed from the REAL fixture (NOT copied from sk-sample).** *(revised)* The SK fixture's `Bridgeable=1/Rewrite=8/Rearchitect=2/15 occ/HARD` is wrong for AutoGen (Bridgeable should be **0**; `[Function]` is Rewrite). **HARD is not guaranteed** — it requires **≥2 distinct re-architect KINDS**, but `RoundRobinGroupChat : GroupChat` may collapse to one Kind → MEDIUM; the fixture must be deliberately designed (e.g. group-chat + a second distinct re-architect) to legitimately reach HARD.
- **DOC-004 (P3) — Update prose/count docs and generalize the `@maf-cross-migration` persona.** *(revised)* **Correction:** because `maf://migrate-from` and `maf-migrate-from` are `source`-parameterized, the resource/prompt/tool/agent totals (7/10/27/8) do **not** move — the **only** count that changes is **skills (14→15)**, and only the three in-code skill-count sites are CI-guarded (README/docs prose counts are hand-maintained, not test-guarded).

### 3.8 Risk & OSS prior art

- **RISK-001 (P1, borderline-P0) — False-positive risk is materially higher than SK; anchor first, exact-name second, and NoDrift-guard that MAF and the M365 Agents SDK are NEVER flagged.** *(revised)* The real danger is a **three-way `Microsoft.*` collision**: `Microsoft.AutoGen.*`/`AutoGen.*` (FROM) vs `Microsoft.Agents.AI` (MAF, the TO target) vs `Microsoft.Agents.*` Builder/Core/Hosting (the **unrelated M365 Agents SDK**, the Bot Framework successor). A sloppy match would tell users to "migrate off MAF itself." `IMiddleware` is an officially-documented ambiguous name across `AutoGen.Core` and `Microsoft.AspNetCore.Http`/the M365 SDK. *Sources: [Microsoft.Agents.AI](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai), [M365 Agents SDK](https://devblogs.microsoft.com/microsoft365dev/introducing-the-microsoft-365-agents-sdk/), [bf-migration-dotnet](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/bf-migration-dotnet).*
- **RISK-002 (P2) — Adoption/ROI + verdict-skew expectations.** *(revised)* The .NET surface is small/frozen; Python dwarfs it by ~order-of-magnitude monthly downloads and is out of scope; the official guide is Python-only. **Correction:** "HARD dominates" is overstated and self-falsifiable under the SK verdict rubric — a lone-orchestrator repo is **MEDIUM**; HARD needs ≥2 distinct re-architect kinds. Frame as "MEDIUM→HARD skew."
- **OSS-001 (P2) — Two-layer detector mirrors the SHIPPED SK detector, not an imported upgrade-assistant pattern; "no-fix ≠ Rearchitect."** *(revised)* The whole cross-framework feature is **alert-only** (all three strategies are no-auto-fix); Rearchitect is defined by "no 1:1 MAF mapping," orthogonal to whether a fixer ships — conflating them would mis-tag `ConversableAgent → AsAIAgent` (a Rewrite) as Rearchitect. Do **not** pin a *target* package version (that is the upgrade-assistant behavior maf-doctor avoids). *Sources: `SemanticKernelDetectorTool.cs`; [upgrade-assistant](https://learn.microsoft.com/en-us/dotnet/core/porting/upgrade-assistant-overview).*
- **OSS-002 (P3) — Keep the catalogue data-driven + per-row drift-tested (BannedApiAnalyzers-validated); the .NET niche is genuinely uncontested.** *(revised)* **Corrections:** jscodeshift prints a per-**file outcome tally** (borrow that for the strategy-count line), not a per-rule grouped summary (that grouping is a **linter** pattern, e.g. ESLint); and the langchain-cli `--diff`/best-effort framing belongs to the `langchain-cli migrate` command + its repeatedly-broken Grit auto-download (an argument *for* maf-doctor's self-contained Roslyn approach), not the manual-migration docs page.

---

## 4. Construct cheat-sheet (the skill / guide lookup, consolidated)

This is the table to ship in `.github/skills/maf-from-autogen/SKILL.md`, reconciled to the detector's `KnownTypes` by a `*_NoDrift` test. Strategy: 🌉 bridge · 🔁 rewrite · 🏗 re-architect. **All rows are derived inference** (no official C# source) and target `Microsoft.Agents.AI` / `Microsoft.Agents.AI.Workflows` / `Microsoft.Extensions.AI`.

| AutoGen construct (.NET) | MAF target | Strategy |
|---|---|---|
| `ConversableAgent` / `AssistantAgent` | `chatClient.AsAIAgent(instructions:, tools:)` → `ChatClientAgent` | 🔁 |
| `OpenAIChatAgent` | `…GetChatClient(m).AsIChatClient().AsAIAgent(...)` | 🔁 |
| `SemanticKernelAgent` | `ChatClientAgent` (reuse SK via `.AsChatClient()`) | 🔁 |
| `MiddlewareAgent` / `IMiddleware`-pipeline | `AIAgent` + `.AsBuilder().Use(...)` | 🔁 |
| `IAgent` / `IStreamingAgent` | `AIAgent` (`RunAsync`/`RunStreamingAsync`) | 🔁 |
| `[Function]` + `AutoGen.SourceGenerator` / `FunctionContract` / `FunctionCallMiddleware` | `AIFunctionFactory.Create(method)` via `tools:` | 🔁 |
| `GenerateReplyAsync` / `GenerateStreamingReplyAsync` / `SendAsync`(msg) | `RunAsync` / `RunStreamingAsync` | 🔁 |
| `RegisterMessageConnector` / `RegisterPrintMessage` | *removed* / logging middleware | 🔁 |
| `MultiModalMessage` / `ToolCall*Message` | `ChatMessage` + typed content | 🔁 |
| `UserProxyAgent` | Workflow `RequestInfoExecutor`/`InputPort` + `HostedCodeInterpreterTool` | 🏗 |
| `GroupChat` / `RoundRobinGroupChat` / `SequentialGroupChat` / `GroupChatManager` / `IGroupChat` | `AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)` / `BuildSequential(...)` | 🏗 |
| `IOrchestrator` / `RoundRobinOrchestrator` / `RolePlayOrchestrator` | `RoundRobinGroupChatManager` / custom manager | 🏗 |
| `WorkflowOrchestrator` / `Graph` / `Transition` | `WorkflowBuilder.AddEdge(...)` | 🏗 |
| `SendAsync`(agent→agent) / `InitiateChatAsync` | a MAF workflow | 🏗 |
| `AutoGen.DotnetInteractive` (local code exec) | `HostedCodeInterpreterTool` (hosted only; local is roadmap) | 🏗 |
| `Microsoft.AutoGen.*` 0.4 runtime (one coarse row) | MAF `Workflow` executors + typed edges | 🏗 |
| *(pre-existing)* `Microsoft.Extensions.AI.IChatClient` / `AIFunction` | carry over unchanged | 🌉 |
| **Prose-only (Python types — never in `KnownTypes`):** `MagenticOneGroupChat`→`MagenticWorkflowBuilder`; `SelectorGroupChat`→group chat + custom manager; `Swarm`→`CreateHandoffBuilderWith` | — | 🏗 |
| **Never detect:** bare `Message`/`Role`/`TextCall`, `IAgent`-bare, `IMiddleware`-bare, `BaseAgent`-bare, `Microsoft.Agents.*` (that IS MAF) | — | — |

---

## 5. Scope decisions & accepted trade-offs (defer for v1)

Recorded so a future reviewer doesn't re-litigate them. Mirrors the SK review §3.

| ID | Decision | Disposition |
|---|---|---|
| **SCOPE-TO-1** — Python AutoGen detection | A Roslyn/.NET scanner cannot parse `.py`/`requirements.txt` into a syntax tree. **Explicit non-goal of the detector.** Optional: a non-Roslyn project-level *informational redirect* (glob `pyproject.toml`/`requirements.txt` for `pyautogen`/`autogen-agentchat`/`autogen-core`, emit one line pointing at the Python guide), firewalled from `KnownTypes`. |
| **SCOPE-TO-2** — `Microsoft.AutoGen.*` 0.4 per-type catalogue | **Defer.** Emit ONE coarse Rearchitect finding for the whole 0.4 event-driven/gRPC runtime (~14K downloads, prerelease, no MAF parity). Per-type mapping is near-zero ROI. |
| **SCOPE-TO-3** — Bridgeable tier | **Accept as empty.** No AutoGen interop shim exists; do not invent Bridgeable rows. Surface the `Microsoft.Extensions.AI` (`IChatClient`/`AIFunction`) carry-over as a report note. |
| **SCOPE-TO-4** — `from-autogen-to-maf.md` provenance | **Author, don't mirror.** No official C# page exists; label the guide community-derived and date-stamp the Python-guide reference (`ms.date` / `ms.update-cycle: 180`) so staleness is detectable if Microsoft later adds a C# pivot. |
| **SCOPE-TO-5** — `*Agent` / `*Config` / `*Middleware` / `*Orchestrator` suffix fallbacks | **Forbidden** (collide with MAF/M365/user types). Enumerate explicitly. `*GroupChat` is the **only** suffix, pinned as an accepted trade-off. |
| **SCOPE-TO-6** — `MagenticWorkflowBuilder` Note | **Phrase as guidance, not a compiler-checked rewrite** — it is experimental (`MAAIW001`) and the youngest MAF API; drift-test the Note text. |
| **SCOPE-TO-7** — Default `--source` | **Keep `semantic-kernel`** so existing invocations are unchanged; `autogen`/`ag` is opt-in via `--source`. |
| **SCOPE-TO-8** — Shared `CsprojPackageReader` / `ISourceFrameworkDetector` seams | **Land with the AutoGen PR** (SK remediation Tier-3 #12/#13 trigger is "the second source"). Not before. |

---

## 6. False positives / rejected sub-claims (so they aren't re-litigated)

No finding was rejected wholesale, but adversarial verification **stripped or corrected** the following candidate sub-claims. Recording them here mirrors the SK review §4.

- **"The from-autogen guide can be mirrored from an official C# Learn page like SK."** ❌ **Rejected.** The page is Python-only (empty `monikers`, no `zone_pivot_groups`, all-Python code). Mappings are derived. *(SCOPE-001/003, DOC-001)*
- **"Seed `KnownTypes` from the Python guide's construct names."** ❌ **Rejected** — `AssistantAgent`/`RoundRobinGroupChat`/`MagenticOneGroupChat` are Python; keying on them yields a detector that finds nothing in C#. *(SCOPE-003)*
- **"`MagenticOneGroupChat` / `SelectorGroupChat` / `Swarm` are AutoGen .NET KnownTypes rows."** ❌ **Rejected** — Python `autogen-agentchat` types; prose-only. And Swarm/Selector are **not** "no MAF target" — they ship as `CreateHandoffBuilderWith` / group chat + custom manager. *(MAP-004, MAP-009)*
- **"Tag `OpenAIChatAgent` / `OpenAIConfig` / `ILLMConfig` as Bridgeable via `.AsIChatClient()`."** ❌ **Rejected** — `OpenAIChatAgent` wraps the OpenAI SDK `ChatClient` (an agent, not a bridge) → Rewrite; the config DTOs are Rewrite. *(MAP-001)*
- **"`max_tool_iterations` should be dropped in the .NET Note."** ❌ **Rejected** — it is a Python `AssistantAgent` parameter, absent from AutoGen .NET. *(MAP-002)*
- **"Use `chatClient.CreateAIAgent(...)` as the target."** ❌ **Corrected** to `AsAIAgent` (the form in the shipped SK rows and the latest C# Learn guide). *(MAP-002, MAF-001)*
- **"Add a second `[McpServerTool] MafDetectSourceFramework`."** ❌ **Rejected** — duplicate MCP tool names break the server; dispatch one tool by `--source`. *(ARCH-001)*
- **"Fold the three rejection bodies into one shared constant."** ❌ **Rejected** — the prompt STOP directive and the resource `ArgumentException` already consume `SupportedDescription` and have distinct framing; only `Program.cs:145` hard-codes. *(ARCH-003)*
- **"`AutoGenU` is a NuGet package proving the `AutoGen` prefix over-matches."** ❌ **Rejected** (404). The real example is `AutoGenerator`. *(DET-001)*
- **"`BaseAgent` collides across both AutoGen families and with Orleans."** ❌ **Rejected** — `BaseAgent` is `Microsoft.AutoGen.Core`-only, no Orleans link. (Keep never-detect, corrected reasons.) *(DET-003)*
- **"`Message`/`Role` collide with MAF's own `ChatMessage`/`ChatRole`."** ❌ **Corrected** — no literal collision; the reason to anchor-gate is genericness. *(DET-003, MAP-006)*
- **"`*GroupChat` is a zero-risk suffix like `*AgentThread`."** ❌ **Corrected** — "GroupChat" is plain English; pin as an accepted trade-off. *(DET-004)*
- **"`RegisterMessageConnector` / `GenerateReplyAsync` are `AgentExtension` extensions."** ❌ **Corrected** — `RegisterMessageConnector` is a provider-package extension; `GenerateReplyAsync` is on the `IAgent` interface. *(DET-006)*
- **"`RegisterDotnetCodeBlockExecutionHook`."** ❌ **Corrected** — the real (misspelled) identifier is `RegisterDotnetCodeBlockExectionHook`. *(MAP-007)*
- **"AutoGen→MAF is HARD-dominant."** ❌ **Corrected** — MEDIUM→HARD; HARD requires ≥2 distinct re-architect kinds. *(RISK-002, DOC-003)*
- **"Copy `Bridgeable=1/Rewrite=8/Rearchitect=2/15/HARD` into the AutoGen smoke test."** ❌ **Rejected** — compute from the real fixture; Bridgeable=0. *(DOC-003)*
- **"Adding AutoGen moves the resources/prompts/tools/agents totals."** ❌ **Rejected** — those surfaces are `source`-parameterized; only the skills count (14→15) moves. *(DOC-004)*
- **"`no-fix = Rearchitect` (upgrade-assistant analyzer-only bucket)."** ❌ **Rejected** — the whole feature is alert-only; Rearchitect = "no 1:1 mapping." *(OSS-001)*
- **"jscodeshift emits a per-rule grouped summary; cite the langchain manual-migration page."** ❌ **Corrected** — jscodeshift prints a per-file outcome tally (rule-grouping is a linter pattern); the langchain framing is the `langchain-cli migrate` command + its Grit-download breakage. *(OSS-002)*

---

## 7. How this compares to the SK feature

**Reusable almost verbatim (the architecture is a near-clone):**
- The pure-core / thin-adapter shape (`Scan`/`AnalyzeSource`/`*ScanResult`), the `MigrationStrategy {Bridgeable, Rewrite, Rearchitect}` enum, `SourceFileWalker`, `PathGuard`, the per-kind dedupe, the EASY/MEDIUM/HARD `Verdict` rubric, and the `ToMarkdown`/`ToJson` + JSON-reconciliation invariant.
- The single-authority `MigrationSources` allow-list and the three thin adapters (CLI / resource / prompt) that consume it.
- The **drift-test discipline** — a `*_NoDrift` test for every catalogue/allow-list/count, a `samples/autogen-sample` exact-count smoke test, and a skill-table↔detector strategy-tag coupling test.
- The two pre-designed extraction seams (`CsprojPackageReader`, `ISourceFrameworkDetector`) — this second source is precisely their trigger.

**Genuinely new or harder than SK:**
1. **No official C# guide to mirror.** SK transcribed a real C# Learn pivot with permission; AutoGen's guide is Python-only, so the construct catalogue, the human guide, and the skill table are all **authored inference** (triple-sourced) and must be labelled as such.
2. **The Bridgeable inversion.** SK's value proposition is a rich bridge tier (`.AsChatClient()`, `KernelFunction.AsAIFunction()`); AutoGen→MAF has **no interop shim**, so the distribution is ~Rewrite + Rearchitect with an empty Bridgeable column — the opposite shape.
3. **Two package families, not one**, and one of them (`Microsoft.AutoGen.*`) is a different paradigm (event-driven actor/gRPC runtime) that collapses to a single coarse Rearchitect finding.
4. **Materially worse false-positive surface.** AutoGen reuses generic type names (`Message`, `Role`, `GroupChat`, `IMiddleware`), the package token `AutoGen` is short/collision-prone, there is no safe suffix fallback, and the *from* prefix `Microsoft.AutoGen.*` is a sibling of the *target* `Microsoft.Agents.*` — so the anchor-first/exact-name-second discipline and an explicit "never flag MAF or the M365 Agents SDK" guard are mandatory, not optional.
5. **"Phantom" Python source types** (`MagenticOneGroupChat`, `SelectorGroupChat`, `Swarm`) that must live in prose, never in `KnownTypes`.
6. **Lower, frozen footprint** (`AutoGen.Core` 0.2.3 / `Microsoft.AutoGen.*` dev-only) — so the feature is best framed as *completing the convergence story* with right-sized expectations, not a high-volume detector.

**Bottom line:** the build is low-architectural-risk and on-mission, but the *content* (catalogue, guide, anti-false-positive guards) is where the genuine work and the genuine novelty live. Build it, scope it to AutoGen-for-.NET 0.2 as primary with a coarse 0.4 warning, defer Python, and label the mappings honestly. See the **[AutoGen Migration Implementation Plan](./autogen-migration-implementation-plan.md)** for prioritized, sized steps.
