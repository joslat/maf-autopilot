# MAF 1.6.1 Migration Guide (draft)

<!-- introduced: 1.6.1 | applies-to: 1.5.0.x → 1.6.1.x | deprecated-in: none -->

> ## ⚠️ This is the **1.5.0 → 1.6.1** delta ONLY
>
> This file documents what changed between MAF 1.5.0 and MAF 1.6.1. It is **not** a complete migration guide for users on versions older than 1.5.0.
>
> **Migrating from an earlier version?** Read the chain in order:
> [1.3.0](./maf-1.3.0-migration-guide.md) → [1.4.0](./maf-1.4.0-migration-guide.md) → [1.5.0](./maf-1.5.0-migration-guide.md) → [1.6.1](./maf-1.6.1-migration-guide.md)
>
> Or ask Copilot to call **`MafMigrationPath(currentVer, targetVer)`** — the MCP tool returns the ordered set of guide sections you need.
>
> Or open **[`guides/maf-current-migration-guide.md`](./maf-current-migration-guide.md)** — the auto-generated cumulative reference that concatenates every per-version guide in version order.


<!-- AUTO-GENERATED START — anything between AUTO-GENERATED START and AUTO-GENERATED END is overwritten on re-run -->

> ⚠️ Auto-generated stub. Review before relying on it for migrations.

## Versions

- Migrating from: `1.5.0`
- Migrating to: `1.6.1`

## Diff Summary (first 120 lines of `dotnet-inspect` output)

```
diff --package Microsoft.Agents.AI@1.5.0..1.6.1 --oneline    # summary statistics
hape
was added

### OpenTelemetryAgent

- Member '.ctor' was added

### ChatClientBuilderExtensions

- Member 'UseMessageInjection' was added
leMessageInjection' was added

#
```

## Release Notes Extract

## What's Changed
* .NET: Add hyperlight to release slnf by @westey-m in https://github.com/microsoft/agent-framework/pull/5695
* .NET: Update FoundryAgent to address HostedAgents strict URL routing by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5677
* .NET: Add IChatMessageInjector for message injection during function loop by @westey-m in https://github.com/microsoft/agent-framework/pull/5679
* .NET: Foundry.Hosting IT - eliminate MSBuild parallel-output races by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5725
* .NET: Hosted-Files sample + AgentSessionFiles SDK companion + integration test by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5698
* .NET: Simplify ClientHeadersScope to rely on AsyncLocal natural restoration by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5676
* .NET: Hosted Agents - RAG Sample with Azure AI Search (#5693) by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5701
* .NET: Fix/per service input persistence on stream error by @alliscode in https://github.com/microsoft/agent-framework/pull/5744
* .NET: Remove Foundry Toolbox server-side tools support by @alliscode in https://github.com/microsoft/agent-framework/pull/5753
* .NET: DevUI: add configurable access controls for the DevUI HTTP surface by @moonbox3 in https://github.com/microsoft/agent-framework/pull/5739
* .NET: Add A2A input-request content for human-in-the-loop scenarios by @SergeyMenshykh in https://github.com/microsoft/agent-framework/pull/5743
* .NET fix: Synthesized Handoff FunctionResult is never sent to agent by @lokitoth in https://github.com/microsoft/agent-framework/pull/5718
* .NET: Refactor harness console rendering by @westey-m in https://github.com/microsoft/agent-framework/pull/5751
* .NET: fix: align Anthropic Extensions AI version by @danyalahmed1995 in https://github.com/microsoft/agent-framework/pull/5709
* .NET: declare Magentic protocol messages by @he-yufeng in https://github.com/microsoft/agent-framework/pull/5778
* .NET: Feat/dotnet shell tool by @alliscode in https://github.com/microsoft/agent-framework/pull/5604
* .NET: Fix OpenAIResponsesAgentClient to include agentName in endpoint path by @giles17 in https://github.com/microsoft/agent-framework/pull/5748
* .NET: CI hardening — split Functions tests, re-enable skipped integration tests by @giles17 in https://github.com/microsoft/agent-framework/pull/5717
* .NET: Add harness agent package by @westey-m in https://github.com/microsoft/agent-framework/pull/5782
* .NET: [Breaking Change] Auto-wire ChatClient with OpenTelemetryChatClient in OpenTelemetryAgent by @Copilot in https://github.com/microsoft/agent-framework/pull/5750
* Dotnet: Fixing FoundryToolboxMcp sample to use created toolbox by @alliscode in https://github.com/microsoft/agent-framework/pull/5786
* .NET: fix: avoid mutating handoff message roles by @he-yufeng in https://github.com/microsoft/agent-framework/pull/5808
* .NET: feat(evals): add ground_truth/expected_output support for workflow evaluation by @alliscode in https://github.com/microsoft/agent-framework/pull/5755
* .NET: Update version for release. by @alliscode in https://github.com/microsoft/agent-framework/pull/5789
* .NET: Fix build issue CA1873 in DevUI by using LoggerMessage source generator by @alliscode in https://github.com/microsoft/agent-framework/pull/5831
* [BREAKING] Python: DevUI: tighten default access controls and CORS posture by @moonbox3 in https://github.com/microsoft/agent-framework/pull/5740
* [BREAKING] Python: Align file skill folder discovery with agentskills.io spec by @SergeyMenshykh in https://github.com/microsoft/agent-framework/pull/5807
* .NET: Filestore improvements by @westey-m in https://github.com/microsoft/agent-framework/pull/5842
* .NET: DevUI: quarantine flaky discovery integration test (#5845) by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5846
* .NET: Update version to 1.6.1 for release by @westey-m in https://github.com/microsoft/agent-framework/pull/5843

**Full Changelog**: https://github.com/microsoft/agent-framework/compare/dotnet-1.5.0...dotnet-1.6.1

## Breaking Changes (requires human verification)

MAF 1.6.1 is primarily an **additive release** with no hard breaking changes. Existing 1.5.0 code compiles without modification.

### WorkflowEvaluationExtensions.EvaluateAsync — New Optional Parameter

The `EvaluateAsync` extension method gained a new optional `string? expectedOutput = null` parameter inserted before the `cancellationToken` parameter:

**Old signature (1.5.0):**
```csharp
Task<AgentEvaluationResults> EvaluateAsync(
    Run run,
    IAgentEvaluator evaluator,
    bool includeOverall = true,
    bool includePerAgent = true,
    string evalName = "Workflow Eval",
    IConversationSplitter? splitter = null,
    CancellationToken cancellationToken = default)
```

**New signature (1.6.1):**
```csharp
Task<AgentEvaluationResults> EvaluateAsync(
    Run run,
    IAgentEvaluator evaluator,
    bool includeOverall = true,
    bool includePerAgent = true,
    string evalName = "Workflow Eval",
    IConversationSplitter? splitter = null,
    string? expectedOutput = null,        // ← NEW
    CancellationToken cancellationToken = default)
```

**Impact:** Most call sites are unaffected. Code using positional arguments for `cancellationToken` (rare) or named arguments like `cancellationToken: ct` will continue to compile. Only if you were passing all parameters positionally would you need to insert `null` for `expectedOutput`.

**New capability:** The `expectedOutput` parameter enables ground-truth evaluation by providing the expected result text for comparison during workflow evaluation.

## New Patterns

### Ground-Truth Workflow Evaluation

You can now pass an expected output string to `EvaluateAsync` for ground-truth evaluation:

```csharp
var results = await run.EvaluateAsync(
    evaluator,
    evalName: "Fraud Detection Test",
    expectedOutput: "Transaction flagged as fraudulent with 95% confidence",
    cancellationToken: ct);
```

This enables automated testing of workflow outputs against known-good results. The evaluator can compare the actual workflow output against the `expectedOutput` to compute accuracy metrics.

See the release notes for details on the new `IChatMessageInjector` for message injection during function loops and other additive features.

## Obsolete APIs Added

None. MAF 1.6.1 does not introduce any `[Obsolete]` attributes. The only API change is the additive `expectedOutput` parameter documented above.

## Known Misalignments

None identified. The `WorkflowEvaluationExtensions.EvaluateAsync` signature change is well-documented and the new parameter is properly optional with a default value.

<!-- AUTO-GENERATED END -->

## Human additions

<!-- Add notes, corrections, and refinements below this heading.
     Content under this heading is PRESERVED across re-runs of the watcher. -->
