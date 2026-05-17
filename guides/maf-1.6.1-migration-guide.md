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

## Versions

- Migrating from: `1.5.0`
- Migrating to: `1.6.1`

## Diff Summary (first 120 lines of `dotnet-inspect` output)

```
diff --package Microsoft.Agents.AI@1.5.0..1.6.1 --oneline    # summary statistics

### WorkflowEvaluationExtensions

- Member 'EvaluateAsync' signature changed: added optional `string? expectedOutput` parameter before `cancellationToken`

### New types added

- OpenTelemetryAgent
- ChatClientBuilderExtensions with UseMessageInjection extension methods

### Key additions

- IChatMessageInjector interface for message injection during function loop
- Hosted agent improvements (AgentSessionFiles SDK, RAG sample with Azure AI Search)
- A2A input-request content for human-in-the-loop scenarios
- Harness agent package for testing
```

## Release Notes Extract

## What's Changed

Key highlights from the 1.6.1 release:

**Breaking Changes:**
* `.NET: [Breaking Change] Auto-wire ChatClient with OpenTelemetryChatClient in OpenTelemetryAgent` - Automatic OpenTelemetry instrumentation
* `.NET: feat(evals): add ground_truth/expected_output support for workflow evaluation` - New parameter in `EvaluateAsync`

**New Features:**
* `.NET: Add IChatMessageInjector for message injection during function loop` - New middleware capability
* `.NET: Hosted-Files sample + AgentSessionFiles SDK companion + integration test` - File handling improvements
* `.NET: Hosted Agents - RAG Sample with Azure AI Search` - New RAG integration pattern
* `.NET: Add A2A input-request content for human-in-the-loop scenarios` - Better A2A integration
* `.NET: Add harness agent package` - New testing utilities
* `.NET: Feat/dotnet shell tool` - Shell tool support

**Fixes:**
* `.NET: Fix/per service input persistence on stream error` - Streaming reliability
* `.NET fix: Synthesized Handoff FunctionResult is never sent to agent` - Handoff bug fix
* `.NET: Fix OpenAIResponsesAgentClient to include agentName in endpoint path` - Routing fix
* `.NET: fix: avoid mutating handoff message roles` - Message handling fix

Full changelog: https://github.com/microsoft/agent-framework/compare/dotnet-1.5.0...dotnet-1.6.1

## Breaking Changes (requires human verification)

### WorkflowEvaluationExtensions.EvaluateAsync signature change

**What changed:** A new optional parameter `string? expectedOutput = null` was added before the `cancellationToken` parameter in `WorkflowEvaluationExtensions.EvaluateAsync`.

**Impact:**
- Existing code will continue to compile because the parameter has a default value
- Call sites using positional arguments should be reviewed for clarity
- Best practice: use named arguments for all optional parameters to avoid future ambiguity

**Migration:** See registry entry `MAF161-EXTENSIO-001` for details.

### OpenTelemetryAgent auto-wiring

**What changed:** `OpenTelemetryAgent` now automatically wraps the provided `ChatClient` with `OpenTelemetryChatClient` for built-in telemetry.

**Impact:**
- If you were manually wrapping your chat client with OpenTelemetry instrumentation, you may get double instrumentation
- Review your telemetry configuration when upgrading

## New Patterns

### Ground-truth evaluation in workflows

MAF 1.6.1 adds support for expected output (ground-truth) in workflow evaluations:

```csharp
var results = await run.EvaluateAsync(
    evaluator,
    includeOverall: true,
    includePerAgent: true,
    expectedOutput: "The expected agent response for comparison",
    cancellationToken: ct);
```

This enables automated quality assessment by comparing actual agent outputs against expected outputs.

### Message injection middleware

New `IChatMessageInjector` interface enables message injection during the agent function loop:

```csharp
services.AddSingleton<IChatMessageInjector, MyCustomInjector>();
chatClient.AsBuilder()
    .UseMessageInjection()
    .Build();
```

This allows for dynamic message modification during agent execution.

## Obsolete APIs Added

No new CS0618 obsoletions in 1.6.1. The release is primarily additive with one signature enhancement (`EvaluateAsync`).

See registry entry `MAF161-EXTENSIO-001` for the signature change details.

## Known Misalignments

None identified at this time. The 1.6.1 release is a minor update with mostly additive features and bug fixes.

<!-- AUTO-GENERATED END -->

## Human additions

<!-- Add notes, corrections, and refinements below this heading.
     Content under this heading is PRESERVED across re-runs of the watcher. -->
