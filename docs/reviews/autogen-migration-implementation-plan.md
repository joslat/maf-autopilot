# AutoGen→MAF Migration — Build Plan

> Companion to the **[Architecture & Design Review](./autogen-migration-architecture-and-design-review.md)** (2026-06-27).
> This plan specifies how to BUILD the AutoGen→MAF "migrate FROM" feature from scratch, mirroring the shipped SK feature (`src/maf-autopilot/Tools/SemanticKernelDetectorTool.cs`, v1.5.0) surface-for-surface. It is assessed against the official Microsoft Agent Framework (MAF) documentation; where the two diverge it says so and flags the gap upstream.
> **Unlike the SK feature, the construct mappings here are DERIVED, not mirrored.** The official AutoGen→MAF guide on Microsoft Learn is **Python-SDK only** (front-matter: *"migrating from AutoGen to the Microsoft Agent Framework Python SDK"*; no C# pivot, verified). Every `→ MAF` Note below is maf-doctor's own .NET inference, cross-checked against the live `Microsoft.Agents.AI` C# API — label it as such in code and docs.

Effort key: **XS** ≈ minutes · **S** ≈ <1h · **M** ≈ a few hours · **L** ≈ a day+. Risk = chance of regressing behaviour or shipping a false-positive-prone detector.

---

## 1 · Recommended scope + sequencing (the defensible v1 boundary)

**Scope v1: detect the AutoGen *.NET* surface only; defer Python by construction; ship a derived (not mirrored) guide.** Three verified facts force this boundary:

1. **maf-doctor is Roslyn + `.csproj`/CPM-XML only — no `SemanticModel`, no Python front-end.** It structurally cannot parse `pyautogen`/`autogen-agentchat`/`autogen-core`. Python AutoGen is therefore a documented **non-goal of the detector**; at most the guide/skill prose redirects Python users to the official guide. This mirrors the SK feature's syntactic model exactly.
2. **Two real, Roslyn-visible .NET AutoGen families exist — target both, weighted by maturity.** Family A `AutoGen.*` (autogen-for-net, the 0.2 line; `AutoGen.Core` **0.2.3**, last shipped 2025-04-09, ~175K downloads — *frozen/legacy but the code real .NET teams have*) and Family B `Microsoft.AutoGen.*` (the 0.4 event-driven runtime; `Microsoft.AutoGen.Core` **0.4.0-dev.3**, prerelease-only, never stable, ~14K downloads). Build the rich catalogue against Family A; collapse Family B to **one coarse Rearchitect finding**.
3. **There is no official C# AutoGen→MAF cheat-sheet to mirror.** The Learn page (`learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/`) is Python-only and contains no `::: zone pivot="programming-language-csharp"` block. So `docs/migration/from-autogen-to-maf.md` must be **authored** from (a) that guide's *conceptual* mappings, (b) the autogen-for-net API surface, and (c) the live `Microsoft.Agents.AI` .NET API — never presented as "mirrored with permission."

**Strategy-distribution expectation — the headline inversion vs SK.** AutoGen→MAF ships **no agent-level interop bridge** (there is no AutoGen analogue of SK's `IChatCompletionService.AsChatClient()` / `KernelFunction.AsAIFunction()`; `.AsAIAgent()` converts *client* types, not AutoGen agents). The only genuine bridge seam is the shared `Microsoft.Extensions.AI` substrate (`IChatClient`, `AIFunction`), which is **not an AutoGen type**. So the AutoGen catalogue skews **≈0 Bridgeable / Rewrite (single agents, tools, calling) / Rearchitect (every multi-agent, human-in-loop, code-exec, and the 0.4 runtime)** — the inverse of the SK catalogue. Say this plainly in the guide; do **not** invent Bridgeable rows.

**MAF target identifiers (verified, current).** Package `Microsoft.Agents.AI` (GA, latest **1.11.x**); base `AIAgent`, concrete `ChatClientAgent`; agent creation `chatClient.AsAIAgent(instructions:, name:, tools:)` (use **`AsAIAgent`** to match the shipped SK `KnownTypes` notes — *not* `CreateAIAgent`); tools via `AIFunctionFactory.Create(method)` (namespace `Microsoft.Extensions.AI`); orchestration in `Microsoft.Agents.AI.Workflows` — `AgentWorkflowBuilder.BuildSequential/BuildConcurrent`, `AgentWorkflowBuilder.CreateGroupChatBuilderWith(a => new RoundRobinGroupChatManager(a){MaximumIterationCount=…})`, `AgentWorkflowBuilder.CreateHandoffBuilderWith(start).WithHandoffs(…)`, and `new MagenticWorkflowBuilder(manager)…` (namespace `…Workflows.Specialized.Magentic`, **experimental `MAAIW001`**). Continuity uses the current spelling `AgentSession` via `await agent.CreateSessionAsync()` (legacy `AgentThread`/`GetNewThread()` still in the wild — keep target wording on the current name, same as the SK rows).

**Sequencing.** Land Tier 0 (decisions) and Tier 1 (detector core + allow-list + wiring) in **one PR**, *together with* the two Tier-3 seam extractions (they are now triggered and are cleaner to be born into than retrofitted). Tier 2 (drift tests + guide + sample + skill) rides the same PR train because the allow-list must not advertise a source whose resource/skill/sample files do not yet exist (the resource and several `*_NoDrift` tests would throw the moment `Canonicalize("autogen")` succeeds). The Python redirect is explicitly **deferred** (Tier 3).

> Sources: official guide `learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/`; MAF group-chat C# `learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/group-chat`; `nuget.org/packages/Microsoft.Agents.AI`, `…/AutoGen.Core`, `…/Microsoft.AutoGen.Core`; AutoGen maintenance-mode `github.com/microsoft/autogen/discussions/7066`; in-repo template `src/maf-autopilot/Tools/SemanticKernelDetectorTool.cs`.

---

## 2 · Tier 0 — Scope & architecture decisions (settle before coding)

Cheap to write, expensive to get wrong. These gate everything below.

| # | Decision | Effort | Risk | Accept when |
|---|---|---|---|---|
| 0.1 | **.NET-only scope; Python is a non-goal** | XS | none | `MigrationSources.SupportedDescription` and the guide banner state the detector targets AutoGen for .NET (`AutoGen.*` + `Microsoft.AutoGen.*`) and that Python AutoGen is out of scope. |
| 0.2 | **Two families, asymmetric treatment** | XS | none | Family A drives a full `KnownTypes`; Family B routes to ONE coarse Rearchitect construct. Documented in the detector class XML-doc. |
| 0.3 | **Canonical id + alias** | XS | low | `autogen` canonical; alias `ag` (a maf-doctor convention mirroring `sk`, not an external standard — note as such). |
| 0.4 | **Detector shape = clone the SK pure-core; do NOT add a second `[McpServerTool]` name** | XS | med | Decision recorded that `MafDetectSourceFramework` stays a **single** source-aware MCP entry that dispatches on `--source`; `AutoGenDetectorTool` exposes only the pure core (no `[McpServerToolType]`). Two tools named `MafDetectSourceFramework` would collide on the MCP surface. |
| 0.5 | **Catalogue is DERIVED, not mirrored** | XS | none | The detector class carries a `// DERIVED: no official C# AutoGen→MAF guide (Learn page is Python-only); strategy column is maf-doctor inference cross-checked vs autogen-for-net + Microsoft.Agents.AI` comment. |

- **Don't:** copy the SK gate's single-prefix `id.StartsWith("Microsoft.SemanticKernel")` shape verbatim — `AutoGen` is a far less distinctive token (see Tier 1 #1).
- **Don't:** put any AutoGen-named type in the Bridgeable bucket (§1). Add an inversion-guard test (Tier 2).

---

## 3 · Tier 1 — Detector core + allow-list + wiring (the feature)

The shippable core. Mirror the SK pure-core/thin-adapter anatomy; reuse `MigrationStrategy`, `SourceFileWalker`, `PathGuard`, `IsTypeReference`, the `Record` dedupe, and the `ToMarkdown/ToJson/Verdict` rendering verbatim.

| # | Item | Effort | Risk | Files |
|---|---|---|---|---|
| 1 | `AutoGenDetectorTool` pure core (`Scan`/`DetectPackages`/`AnalyzeSource`, records, two-family gates) | L | med | **new** `src/maf-autopilot/Tools/AutoGenDetectorTool.cs` |
| 2 | Seed the `KnownTypes` catalogue + ordered `Classify` + `[Function]`/invocation signals | M | med | (same file) |
| 3 | `MigrationSources` allow-list add (the near-one-line edit) | XS | low | `src/maf-autopilot/MigrationSources.cs` |
| 4 | Dispatch `migrate-scan --source autogen` + fix the hard-coded SK error + `--help` | S | low | `src/maf-autopilot/Program.cs`, `src/maf-autopilot/Commands/MigrateScanCli.cs` |
| 5 | Resource + prompt source-parameterization | S | low | `src/maf-autopilot/Resources/MafResources.cs`, `src/maf-autopilot/Prompts/MafPrompts.cs` |

### 1 · `AutoGenDetectorTool` pure core
- **Do:** clone `SemanticKernelDetectorTool.cs` — IO-free `internal static AutoGenScanResult Scan(string repoPath)`, `internal static IReadOnlyList<AutoGenPackage> DetectPackages(string repoPath)`, `public static IReadOnlyList<AutoGenConstruct> AnalyzeSource(string source, string fileName = "<inline>", bool repoEstablishesAgContext = false)`; records `AutoGenScanResult`/`AutoGenConstruct`/`AutoGenPackage`. Keep the visibility rule: `AnalyzeSource` public (drives the string-input unit tests), `Scan`/`DetectPackages` internal (reachable via `InternalsVisibleTo`). Reuse the shared `MigrationStrategy` enum.
- **Do — package gate (segment-aware, the dot is load-bearing):** `id.Equals("AutoGen", OIC) || id.StartsWith("AutoGen.", OIC) || id.StartsWith("Microsoft.AutoGen.", OIC)`, and **exclude** `Microsoft.Agents.*` / `Microsoft.Extensions.AI*`. Keep the CPM/`Directory.Packages.props` resolver and version precedence (`VersionOverride → Version attr → Version elem → central pin → "(unpinned)"`) unchanged.
- **Do — namespace gate (`ImportsAutoGen` / `HasGlobalAutoGenUsing`):** strip a leading `global::`, then match `== "AutoGen"` || `StartsWith("AutoGen.", Ordinal)` || `StartsWith("Microsoft.AutoGen", Ordinal)`; short-circuit to **false** for `Microsoft.Agents` / `Microsoft.Extensions.AI`. This gate is **more load-bearing than SK's** because AutoGen type names are generic; everything is reported only inside an AutoGen-imported file.
- **Don't:** use a bare `StartsWith("AutoGen")` — it matches the real (unlisted) package/namespace `AutoGenerator`. The exact-`AutoGen` arm is independently required (the bare all-in-one `AutoGen` package is real).
- **Accept when:** `DetectPackages` finds bare `AutoGen` AND `Microsoft.AutoGen.Core` but NOT `AutoGenerator`; a file importing only `Microsoft.Agents.AI`/`Microsoft.Extensions.AI` yields **zero** AutoGen findings (never tell a user to migrate off MAF); a `class GroupChat {}` / property named `Role`/`Message` with **no** AutoGen import yields nothing.

### 2 · Seed `KnownTypes` + ordered `Classify` + signals
- **Do — `Classify` ordering (load-bearing; doc-comment it like the SK `Classify`):** (a) `[Function]`/`[FunctionAttribute]` handled in the **node switch** (not `Classify`), mirroring `[KernelFunction]`; (b) exact interfaces/orchestrators (`IGroupChat`, `IOrchestrator`, `RoundRobinOrchestrator`, `RolePlayOrchestrator`, `WorkflowOrchestrator`); (c) `KnownTypes` exact lookup; (d) the **single** suffix fallback `EndsWith("GroupChat") && name != "IGroupChat"` → Rearchitect. `IGroupChat` literally ends in `GroupChat`, so the exact step MUST precede the suffix or it shadows the tailored note.
- **Do — invocation/extension signals** (in the node switch, name-gated by the AutoGen import): `GenerateReplyAsync`→`RunAsync`; `GenerateStreamingReplyAsync`→`RunStreamingAsync`; `RegisterMessageConnector`/`RegisterPrintMessage`→delete; `RegisterMiddleware`/`RegisterReply`/`RegisterPreProcess`/`RegisterPostProcess`→`.AsBuilder().Use(...)`; `InitiateChatAsync(agentB,…)`→Rearchitect (workflow); `RegisterDotnetCodeBlockExectionHook` (**verbatim misspelling**, `[Obsolete]`)→Rearchitect. **`SendAsync` is receiver-gated** (collides with `HttpClient`/`SmtpClient`): only Record when the chain root is a detected-agent variable; agent→agent overload → Rearchitect, single-message → Rewrite.
- **Don't — anti-rules (each pinned by a test):** NO `*Agent` suffix (collides with MAF's own `AIAgent`/`ChatClientAgent`, user `FooAgent`, AutoGen infra agents); NO `*Config`/`*Middleware`/`*Orchestrator` suffix; do NOT add bare `IAgent`, `Agent`, `IMiddleware` (ASP.NET Core `IMiddleware` collision), `BaseAgent`, `Message`, `TextMessage`, `Role`, `ToolCall`, `Graph`, `Transition` to `KnownTypes`. Detect only the distinctive message variants (`MultiModalMessage`, `ToolCall*Message`).
- **Accept when:** `new RoundRobinGroupChat(...)` yields the tailored row (not the `*GroupChat` suffix note); `IGroupChat` yields the IGroupChat row; `OrderAgent`/`AIAgent`/`ChatClientAgent` yield nothing; a bare `httpClient.SendAsync(...)` inside an AutoGen file is not flagged.

### 3 · `MigrationSources` allow-list
- **Do:** add `public const string AutoGen = "autogen";`; `Canonicalize` arm `"autogen" or "ag" => AutoGen`; set `SupportedDescription = "semantic-kernel (alias: sk), autogen (alias: ag — AutoGen for .NET)"`.
- **Accept when:** `Canonicalize("autogen")`/`("ag")` resolve; the description names the .NET scope.

### 4 · CLI wiring
- **Do:** in `Program.cs` (the `migrate-scan` handler, ~lines 132-153) replace the unconditional `new SemanticKernelDetectorTool()…` with a `switch (MigrationSources.Canonicalize(source))` selecting the SK or AutoGen pure core (mirroring `MafResources.GetMigrationFrom`); replace the hard-coded `"Only --source semantic-kernel (alias sk) is supported today…"` literal (line 145 — the one real single-authority violation) with an interpolation of `MigrationSources.SupportedDescription`; widen `--help` (lines 56-58) to `migrate-scan [path] [--source semantic-kernel|autogen] [--json]`. Collapse the redundant `IsSupportedSource` pre-flight into the switch `_ =>` default (emit message, `Environment.Exit(2)`).
- **Don't:** change `MigrateScanCli.Parse` — it is already source-agnostic (it already round-trips `--source=autogen`). Just refresh its `// AutoGen is a later phase` doc-comment.
- **Accept when:** `migrate-scan samples/autogen-sample --source autogen` runs the AutoGen core; an unknown `--source` prints `SupportedDescription` and exits 2; the help line lists both sources.

### 5 · Resource + prompt
- **Do — resource:** in `MafResources.GetMigrationFrom` add `MigrationSources.AutoGen => ReadEmbedded("migrate-from/autogen.md")`; extend the `[Description]` literal (a compile-time constant — **must be hand-edited**, it cannot reference `SupportedDescription`) to list `autogen`.
- **Do — prompt:** in `MafPrompts.MigrateFrom`, resolve `canonical` once after the existing `IsSupported` gate, then branch the body and the `maf://migrate-from?source={canonical}` URI; broaden the `[Title]`/`[Description]`/`source`-param help. For the AutoGen arm, **lead with 🔁 rewrite / 🏗 re-architect and state the 🌉 bridge tier is essentially empty** — do NOT reuse the SK `AsChatClient()`/`AsAIFunction()` bridge bullets.
- **Accept when:** `maf://migrate-from?source=autogen` resolves to the embedded guide; the `maf-migrate-from` prompt with `source: autogen` renders the AutoGen loop (not the STOP body) and references the AutoGen URI.

---

## 4 · Tier 2 — Drift tests, guide, sample, skill (the quality envelope)

Every catalogue/allow-list/count ships with a `*_NoDrift` guard — the toolkit's defining habit. These land in the **same PR** as Tier 1.

| # | Item | Effort | Risk | Files |
|---|---|---|---|---|
| 6 | `AutoGenDetectorToolTests` (string-input core + both-family packages + JSON reconcile + anti-rule guards) | M | low | **new** `src/maf-autopilot.Tests/AutoGenDetectorToolTests.cs` |
| 7 | Flip the existing AutoGen negative guards; add `MigrationSources` NoDrift | S | low | `MigrateScanCliTests.cs`, `MafPromptsTests.cs`, `MafResourcesTests.cs` |
| 8 | `samples/autogen-sample` fixture + smoke test (counts computed, not copied) | M | low | **new** `samples/autogen-sample/**`, `src/maf-autopilot.Tests/AutoGenSampleSmokeTests.cs` |
| 9 | `from-autogen-to-maf.md` guide (DERIVED) + embed + skill + csproj | M | low | **new** `docs/migration/from-autogen-to-maf.md`, `.github/skills/maf-from-autogen/SKILL.md`, `src/maf-autopilot/maf-autopilot.csproj` |
| 10 | Construct-mapping NoDrift (`KnownTypes` ↔ SKILL table) | M | low | (test project) |
| 11 | Prose/count + persona updates | S | none | README/docs/steering/agents (see below) |

### 6 · Detector tests
- **Do:** mirror `SemanticKernelDetectorToolTests` — ~14 `AnalyzeSource` string tests fed **AutoGen .NET source** (never Python), `DetectPackages` for BOTH families incl. CPM/`VersionOverride`/case-insensitive, EASY/MEDIUM/HARD verdict tests, and `AutoGenDetectorTool_Json_ShapeAndCountsReconcile` (keep the SK invariant: `constructs.length == bridgeable+rewrite+rearchitect` and `Σ count == occurrences`). Add the **anti-rule guards**: `MafAgentsAiFile_IsNeverFlagged`, `MicrosoftAgentsSdkFile_IsNeverFlagged` (the `Microsoft.Agents.*` M365 SDK shares the prefix), `GenericNameWithoutAnchor_YieldsNothing`, `UserTypeEndingInAgent_IsNotFlagged`, `UserTypeEndingInGroupChat_IsFlagged_AcceptedTradeoff`, `Bare_AutoGen_IsDetected`/`AutoGenerator_IsNot`, and an **inversion guard**: no AutoGen-named `KnownTypes` row is `Bridgeable`.
- **Accept when:** all pass; flipping one strategy tag fails CI with a precise message.

### 7 · Flip the pre-built negative guards
- **Do:** `MigrateScanCliTests.cs:70` `[InlineData("autogen", false)]` → `true`; add `[InlineData("ag", true)]`; **keep** the `langchain` negative at line 71. In `MafPromptsTests.cs`, swap the unsupported example at line 54 from `"autogen"` to `"langchain"` and add `autogen`/`ag` to the supported theory. Add a `MigrationSources_*_NoDrift` asserting every `Canonicalize` arm appears in `SupportedDescription` and resolves to an embedded `migrate-from/{canonical}.md` (this also catches the two hand-maintained copies in `Program.cs` and the `MafResources` `[Description]`).
- **Accept when:** the inverted assertions are the signal that the wiring is complete.

### 8 · Sample fixture
- **Do:** `samples/autogen-sample/` (Library, not in the `.sln`, never compiled — like `sk-sample`): `AutoGenSample.csproj` pinning `AutoGen.Core` 0.2.3 + `AutoGen.OpenAI` 0.2.3 + `AutoGen.SourceGenerator` 0.2.3 (and one `Microsoft.AutoGen.Core` 0.4.0-dev.3 to exercise Family B); `.cs` files spanning the strategies — a `[Function]` partial class + `OpenAIChatAgent` (Rewrite), a `RoundRobinGroupChat`/`GroupChat` (Rearchitect), a `UserProxyAgent` (a **second distinct** Rearchitect kind), and a trap file declaring user `Message`/`Role`/`IMiddleware`/`BaseAgent` with no AutoGen import (pins non-detection); a README.
- **Don't:** copy SK's pinned counts (`1/8/2/15/HARD`). Compute Bridgeable/Rewrite/Rearchitect/occurrences from the **actual** fixture against the catalogue. Note **HARD requires ≥2 distinct rearchitect KINDS** — a lone group-chat repo is MEDIUM; the `UserProxyAgent` (or Family-B runtime) is what legitimately earns HARD.
- **Accept when:** `AutoGenSampleSmokeTests` pins the real inventory; Bridgeable is **0**; the verdict is HARD for the reason the fixture was designed.

### 9 · Guide + skill + embeds
- **Do:** author `docs/migration/from-autogen-to-maf.md` in the SK guide's shape (why-migrate, construct cheat-sheet reconciled to `KnownTypes`, before/after, recommended order, "beyond the page" caveats) with a top banner: the official guide is Python-only, AutoGen for .NET is frozen/preview, maf-doctor scans the .NET surface, and the mappings are community-authored. Add `.github/skills/maf-from-autogen/SKILL.md` with the `| AutoGen construct | MAF target | Strategy | …|` table (same 🌉/🔁/🏗 legend). Wire `maf-autopilot.csproj`: `<EmbeddedResource … LogicalName="migrate-from/autogen.md"/>` and `<EmbeddedResource … LogicalName="skills/maf-from-autogen.md"/>`. Register `maf-from-autogen` in `MafResources._allowedSkillNames` AND the `GetSkill` `[Description]`.
- **Accept when:** `AllowedSkillNames_MatchSkillFolders_NoDrift`, `GetSkill_DescriptionLists_EveryAllowlistedName`, and `GetSkill_EveryAllowlistedName_Resolves` pass (they fail the instant the skill folder lands without the three wirings).

### 10 · Construct-mapping NoDrift
- **Do:** clone `SkillConstructTable_StrategyTags_MatchDetector_NoDrift` as `AutoGenSkillConstructTable_StrategyTags_MatchDetector_NoDrift` — a one-directional strategy-tag agreement test (header gate `| AutoGen construct `, type-token extraction, emoji↔`MigrationStrategy` map). **Calibrate the `coupled >= N` floor** to the count of actual round-trippable AutoGen type names; do NOT copy SK's `>= 12` (much of AutoGen is method/runtime-shaped).
- **Accept when:** changing a tag in code OR the SKILL table fails CI until both agree.

### 11 · Prose/counts + persona
- **Do:** the **only** count that moves is **skills 14→15** (`README.md`, `NUGET_README.md`, `docs/architecture.md`, `docs/quickstart.md`, `docs/init-reference.md`, and the CI-guarded `TourTool.cs:152` "14 skills available." literal). Tools/resources/prompts/agents (27/7/10/8) are **unchanged** — `maf://migrate-from` and `maf-migrate-from` are already source-parameterized. Generalize `@maf-cross-migration.agent.md` and the `maf.agent.md` SK row to multi-source (load `maf-from-semantic-kernel` vs `maf-from-autogen`), and the four `docs/steering/*.md` cross-migration bullets. Add a `CHANGELOG.md` entry.
- **Don't:** edit the 27/7/10/8 numbers.
- **Accept when:** the skills count is consistent everywhere and CI's `ResourceCatalogue_SkillsCount_MatchesAllowlist` passes; the persona STOP step lists both supported sources.

---

## 5 · Tier 3 — Shared-seam extraction (now triggered) + deferrals

The SK remediation plan gated these on "the second source (AutoGen) lands." It has landed — do **#12/#13 in the same PR** so the second detector is *born* calling the shared reader/seam, not copy-pasting ~95 lines.

| # | Item | Effort | Risk | Files |
|---|---|---|---|---|
| 12 | Extract `CsprojPackageReader.ReadPackageReferences(repoPath, idPredicate)` + CPM resolver | M | low | **new** `src/maf-autopilot/Tools/CsprojPackageReader.cs`; refactor `SemanticKernelDetectorTool.DetectPackages` |
| 13 | Extract `ISourceFrameworkDetector`; dispatch by canonical source | M–L | med | `Tools/*`, `Program.cs`, `MigrationSources.cs` |
| 14 | Python-detection deferral (informational redirect) | — | — | (not built) |

### 12 · `CsprojPackageReader`
- **Do:** move `DetectPackages`' body + `ReadCentralPackageVersions` into `CsprojPackageReader.ReadPackageReferences(repoPath, Func<string,bool> idPredicate)` returning a **framework-neutral** shape (`IReadOnlyList<(string Id, string Version)>` or a new `PackageRef` record). `SemanticKernelDetectorTool.DetectPackages` becomes a one-line wrapper passing the SK prefix predicate + projecting to `SkPackage`; `AutoGenDetectorTool` passes its three-arm predicate + projects to `AutoGenPackage`.
- **Don't:** assume "only the prefix is framework-specific" — the **return record** (`SkPackage`) is SK-specific too; that is why the shared reader returns tuples.
- **Accept when:** the 8 existing `DetectPackages_*` tests stay green untouched (SK signature preserved), and a new `CsprojPackageReaderTests` exercises the reader with both predicates (CPM, `VersionOverride`, conflicting-pin sorted-join, OIC grouping).

### 13 · `ISourceFrameworkDetector` seam
- **Do:** extract the interface the SK class XML-doc (`SemanticKernelDetectorTool.cs:34-39`) already anticipates; lift a shared `IMigrationScanResult { bool Detected; string ToMarkdown(); object ToJson(); }`; add a factory on `MigrationSources` (`CreateDetector(source)` switching on `Canonicalize`) so `Program.cs` dispatches by canonical id. **Narrow the rejection-text fix:** only `Program.cs:145` hard-codes the supported list — fix that to read `SupportedDescription`; leave `MafPrompts` (the LLM STOP directive) and `MafResources` (the `ArgumentException`) as-is — they already consume `SupportedDescription` and their distinct framing is intentional. Do NOT collapse the three into one constant.
- **Accept when:** a `MigrationSources_NoDrift` asserts every `Canonicalize` arm yields a non-null `CreateDetector` AND a resource arm; both detectors render identically through `IMigrationScanResult`.

### 14 · Python deferral (do NOT build now)
- **Defer:** a `.py`/`requirements.txt`/`pyproject.toml` construct scanner is out of the Roslyn model. The only on-brand future option is a **presence redirect** (glob `pyautogen`/`autogen-agentchat`/`autogen-core` in dependency manifests and emit ONE informational line pointing at the official Python guide), firewalled from the Roslyn construct list (no `KnownTypes` rows, no strategy counts). Record it as a deliberate non-goal in the guide.

---

## 6 · Seeded `KnownTypes` proposal (the rows to ship first)

Format mirrors the SK dictionary: keyed by **simple type name** (`StringComparer.Ordinal`), `["TypeName"] = ("Kind label", Strategy, "→ note")`. All gated by an AutoGen import. **Bridgeable is intentionally empty** (no AutoGen interop shim). Notes use the verified `Microsoft.Agents.AI` API; label the whole table DERIVED.

### Family A — `AutoGen.*` (0.2 line)

| AutoGen type | Family / ns | Kind | Strategy | Note (→ MAF) |
|---|---|---|---|---|
| `ConversableAgent` | `AutoGen` | Base conversable agent | **Rewrite** | `chatClient.AsAIAgent(instructions:, tools:)` → `ChatClientAgent`; `RunAsync` auto-runs the tool loop — delete manual reply chaining. |
| `AssistantAgent` | `AutoGen` | Assistant agent | **Rewrite** | `chatClient.AsAIAgent(name:, instructions:, tools:)`. *(Generic name — only inside the AutoGen gate.)* |
| `UserProxyAgent` | `AutoGen` | User-proxy / human-in-loop | **Rearchitect** | No MAF agent type — human input → a Workflow request/response gate (`RequestInfoExecutor`/`InputPort`); code-exec role (0.2 Python) → hosted tool. |
| `OpenAIChatAgent` | `AutoGen.OpenAI` | Provider chat agent | **Rewrite** | `openAIClient.GetChatClient(model).AsIChatClient().AsAIAgent(...)` — the SDK `ChatClient` needs `.AsIChatClient()` first. |
| `SemanticKernelAgent` / `SemanticKernelChatCompletionAgent` | `AutoGen.SemanticKernel` | SK-bridge agent | **Rewrite** | Drop the AutoGen-over-SK wrapper; create the MAF agent from the SK chat client (`kernel`/`IChatCompletionService.AsChatClient()` → `AsAIAgent`). |
| `MiddlewareAgent` / `MiddlewareStreamingAgent` | `AutoGen.Core` | Middleware agent | **Rewrite** | Re-express the `.RegisterMiddleware(...)` chain as MAF agent middleware via `agent.AsBuilder().Use(...)`. |
| `GroupChat` | `AutoGen.Core` | Group-chat orchestration | **Rearchitect** | `AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)` with a custom manager. |
| `RoundRobinGroupChat` | `AutoGen.Core` | Round-robin group chat | **Rearchitect** | `CreateGroupChatBuilderWith(a => new RoundRobinGroupChatManager(a){MaximumIterationCount=N})` (or `BuildSequential`). |
| `SequentialGroupChat` | `AutoGen.Core` *(obsolete)* | Sequential group chat | **Rearchitect** | `AgentWorkflowBuilder.BuildSequential(agents)`. |
| `GroupChatManager` | `AutoGen.Core` | Group-chat manager | **Rearchitect** | Becomes the manager-factory lambda (subclass `RoundRobinGroupChatManager`, override `ShouldTerminateAsync`). |
| `IGroupChat` | `AutoGen.Core` | Group-chat interface | **Rearchitect** | Model the chat as a typed MAF `Workflow`. |
| `IOrchestrator` / `RoundRobinOrchestrator` / `RolePlayOrchestrator` / `WorkflowOrchestrator` | `AutoGen.Core` | Speaker-selection / graph orchestrator | **Rearchitect** | Group-chat orchestrators → group-chat workflow; `WorkflowOrchestrator`/`Graph`/`Transition` → low-level `WorkflowBuilder.AddEdge(...)`. |
| `FunctionContract` / `FunctionParameterContract` / `FunctionCallMiddleware` | `AutoGen.Core` | Tool/function plumbing | **Rewrite** | `AIFunctionFactory.Create(method)` (ns `Microsoft.Extensions.AI`) passed via `tools:`; delete the source generator + middleware — MAF runs the tool loop. |
| `MultiModalMessage` / `ToolCallMessage` / `ToolCallResultMessage` / `ToolCallAggregateMessage` | `AutoGen.Core` | Distinctive messages | **Rewrite** | `Microsoft.Extensions.AI.ChatMessage` + `FunctionCallContent`/`FunctionResultContent`. *(Never the bare `Message`/`TextMessage`/`Role`.)* |
| `DotnetInteractiveKernelBuilder` / `InteractiveService` | `AutoGen.DotnetInteractive` | Local code execution | **Rearchitect** | No LOCAL C#/F# exec in MAF (roadmap) — hosted `new HostedCodeInterpreterTool()` runs provider-side (Python). |

**Node-switch / invocation signals (carried inline, no `KnownTypes` row):** `[Function]`/`[FunctionAttribute]` → Rewrite; `GenerateReplyAsync`→`RunAsync`; `GenerateStreamingReplyAsync`→`RunStreamingAsync`; `RegisterMessageConnector`/`RegisterPrintMessage`→delete; `RegisterMiddleware`/`RegisterReply`/`RegisterPreProcess`/`RegisterPostProcess`→`.AsBuilder().Use(...)`; `InitiateChatAsync(agentB,…)`→Rearchitect; `SendAsync`→Rewrite (single-msg, receiver-gated) / Rearchitect (agent→agent); `RegisterDotnetCodeBlockExectionHook` (misspelled, `[Obsolete]`)→Rearchitect.

### Family B — `Microsoft.AutoGen.*` (0.4 runtime) — ONE coarse row

| Trigger | Kind | Strategy | Note |
|---|---|---|---|
| `BaseAgent` / `IHandle<T>` / `IAgentRuntime` / `AgentId` / `TopicId` / `AgentsApp(Builder)` / `InProcessRuntime` / `[TypeSubscription]` / `[TypePrefixSubscription]`, or any `Microsoft.AutoGen.*` package/`using` | 0.4 event-driven runtime (`Microsoft.AutoGen.*`) | **Rearchitect** | Experimental distributed actor/pub-sub runtime — never shipped stable (`0.4.0-dev.3`). MAF has no topic/runtime model and is single-process today (distributed is planned); redesign onto a MAF `Workflow` of typed executors + edges. A `*.Grpc`/`RuntimeGateway.Grpc` ref escalates the note ("no MAF distributed parity"). |

### Honesty / no-target rows (the SK `*Planner`-style discipline)
Only **two** genuinely have no .NET MAF target: the distributed gRPC runtime (above) and **local code execution** (`AutoGen.DotnetInteractive`). Note (verified): `Swarm`/handoff and `SelectorGroupChat` are **Python-only** (no `AutoGen.Core` C# type → guide/skill prose only) AND already have shipped MAF .NET targets (`CreateHandoffBuilderWith`, `CreateGroupChatBuilderWith` + custom manager) — do **not** label them "roadmap/no 1:1."

---

## 7 · Assessment vs. official MAF documentation

- **Single agents (Rewrite) — aligned.** The guide treats `AssistantAgent`→`Agent` as "almost identical"; the .NET equivalent `ConversableAgent`/`AssistantAgent`/`OpenAIChatAgent` → `ChatClientAgent` via `AsAIAgent` is a clean, documented correspondence. The one wrinkle the guide flags is behavioral, not type-level: MAF agents are **multi-turn by default** (the tool loop runs to completion) and **stateless** (continuity via `AgentSession`/`CreateSessionAsync`). Our Notes reflect this; we deliberately omit the Python-only `max_tool_iterations` from .NET Notes.
- **Group chat / orchestration (Rearchitect) — aligned, with one direct lineage.** The guide explicitly frames the multi-agent jump as "event-driven → data-flow" and states **MAF Magentic "is designed based on the Magentic-One system invented by AutoGen."** Our `RoundRobin/Sequential/GroupChat` → `AgentWorkflowBuilder` mapping matches the published C# orchestration pages. `MagenticOneGroupChat`→`MagenticWorkflowBuilder` is the cleanest cross-framework row, but it lives in guide prose (Python-only source type) and behind experimental `MAAIW001`.
- **Tools (Rewrite) — aligned but unmirrored.** `[Function]`+`AutoGen.SourceGenerator`+`FunctionContract`+`FunctionCallMiddleware` → `AIFunctionFactory.Create` is correct and same-ecosystem (autogen#4040 shows AutoGen .NET already adopting `Microsoft.Extensions.AI`), but the official guide never documents the .NET `[Function]` surface — this row is purely derived.
- **The Bridgeable inversion — a divergence to state loudly.** The official guide ships **no interop bridge** for AutoGen (unlike the SK guide's bridge spine). Our catalogue therefore has ≈0 Bridgeable rows; presenting AutoGen→MAF as a "bridge-first" story would contradict Microsoft's own framing.

**Gaps to flag upstream (Microsoft):** (1) there is **no C# pivot** on the AutoGen→MAF guide — .NET teams have no first-party mapping; (2) **local code execution** and a **distributed runtime** are documented as "planned," so `AutoGen.DotnetInteractive` and `Microsoft.AutoGen.*.Grpc` have no .NET target today; (3) `Swarm`/`SelectorGroupChat` are labeled Python "Future Patterns" even though the **.NET** `CreateHandoffBuilderWith` / custom `GroupChatManager` targets already ship — a doc inconsistency worth raising.

---

## 8 · Open questions / verify before coding

1. **Re-pin the MAF target API at authoring time.** `Microsoft.Agents.AI` is GA but fast-moving; confirm `AsAIAgent` vs `CreateAIAgent`, `AgentSession`/`CreateSessionAsync` vs legacy `AgentThread`/`GetNewThread()`, the `AgentWorkflowBuilder` group-chat/handoff signatures, and that `MagenticWorkflowBuilder` is still a constructor behind `MAAIW001`. Keep Notes as guidance (detect-and-plan), not compiler-checked text.
2. **Family-B type inventory.** `Microsoft.AutoGen.Core` type names (`AgentsAppBuilder`, `RoutedAgent`, `InProcessRuntime`, `[TypeSubscription]`) come partly from secondary docs — verify against `github.com/microsoft/autogen/tree/main/dotnet` before pinning the coarse-row triggers.
3. **Provider-agent / meta-namespace names.** Confirm `OpenAIChatAgent` (and whether `GPTAgent`/`LMStudioAgent` are still current/`[Obsolete]`) against `AutoGen.OpenAI`/`AutoGen.SemanticKernel`/`AutoGen.LMStudio` API refs before they enter `KnownTypes`. `GPTAgent` looks legacy — omit or gate tightly.
4. **`AutoGen.SemanticKernel` cross-fire.** A bridge file legitimately trips BOTH the SK and AutoGen detectors — decide precedence (recommend: report under AutoGen with a dual-framework note) and add a non-collision test.
5. **`SendAsync` receiver heuristic.** AutoGen has no canonical agent-var convention (unlike SK's `kernel`), so gate on detected-agent variables, not a fixed name list; pin the chosen heuristic and the `HttpClient.SendAsync` negative.
6. **Sample-fixture verdict design.** Confirm whether `GroupChat` and `RoundRobinGroupChat` collapse to one rearchitect KIND; if so the fixture needs a second distinct kind (`UserProxyAgent` or a Family-B runtime construct) to legitimately reach HARD.
7. **Alias choice.** `ag` is a maf-doctor convention (mirrors `sk`), not an external standard — confirm it does not collide with any future source id.
8. **Guide staleness guard.** The official Python guide could gain a C# pivot later; date-stamp the `from-autogen-to-maf.md` banner ("as of 2026-06-27, the official guide is Python-only") so a future pivot is caught on review.

> None of this is blocked by missing detector functionality — it is greenfield. Tier 0 + Tier 1 + the two triggered Tier-3 seams + Tier 2's tests/guide/sample form one cohesive first PR; the Python redirect stays deferred.
