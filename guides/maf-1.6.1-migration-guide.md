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

- `WorkflowEvaluationExtensions.EvaluateAsync` signature changed by adding `string? expectedOutput = null` before `CancellationToken`. Positional call sites that passed `cancellationToken` as the 7th argument must now pass `expectedOutput` first (or switch to named arguments).
- `OpenTelemetryAgent` now auto-wires `OpenTelemetryChatClient` (release notes mark this as breaking). If your host already wraps the same client with OpenTelemetry, remove duplicate wrapping to avoid double instrumentation.

## New Patterns

- **Workflow evaluation with explicit expected output**: when calling `EvaluateAsync`, provide `expectedOutput` to compare generated output against a known target in eval scenarios.
- **Message injection in function loops**: `ChatClientBuilderExtensions.UseMessageInjection` and `IChatMessageInjector` enable deterministic message injection during tool/function loops.
- **OpenTelemetry-first agent wiring**: `OpenTelemetryAgent` now handles chat-client telemetry wrapping automatically; prefer that built-in path instead of ad-hoc wrapper stacking.

## Obsolete APIs Added

- Registry entry: `MAF161-EXTENSIO-001` (`WorkflowEvaluationExtensions.EvaluateAsync` signature change, CS0618).
- Migration action: update positional `EvaluateAsync` call sites to include `expectedOutput` before `cancellationToken`, or use named arguments to avoid positional drift.

## Known Misalignments

- `dotnet-inspect diff` reports the `EvaluateAsync` signature change, but the break only manifests for positional argument call sites that supplied `cancellationToken`; named-argument call sites can compile unchanged.
- Release notes classify `OpenTelemetryAgent` auto-wiring as a breaking change, while the API diff is additive. Treat this as runtime-behavior drift (instrumentation topology change), not a compile-time signature break.

<!-- AUTO-GENERATED END -->

## Human additions

<!-- Add notes, corrections, and refinements below this heading.
     Content under this heading is PRESERVED across re-runs of the watcher. -->
