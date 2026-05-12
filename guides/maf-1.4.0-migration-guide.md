# MAF 1.4.0 Migration Guide (draft)

<!-- introduced: 1.4.0 | applies-to: 1.3.0.x → 1.4.0.x | deprecated-in: none -->

<!-- AUTO-GENERATED START — anything between AUTO-GENERATED START and AUTO-GENERATED END is overwritten on re-run -->

> ⚠️ Auto-generated stub. Review before relying on it for migrations.

## Versions

- Migrating from: `1.3.0`
- Migrating to: `1.4.0`

## Diff Summary (first 120 lines of `dotnet-inspect` output)

- **`AgentFileSkillScript.RunAsync`** signature changed: `AIFunctionArguments arguments` → `JsonElement? arguments, IServiceProvider? serviceProvider`
- **`AgentFileSkillScriptRunner.Invoke`** signature changed: same `AIFunctionArguments` → `JsonElement?` + `IServiceProvider?` insertion
- **`AgentFileSkillScriptRunner.BeginInvoke`** signature changed: same argument-type replacement with added `IServiceProvider?` parameter
- **`AgentSkillScript.RunAsync`** signature changed: `AIFunctionArguments arguments` → `JsonElement? arguments, IServiceProvider? serviceProvider`
- **`ToolApprovalRequestContentExtensions`** type added (new helper for tool-approval flows)
- **`ToolApprovalAgent`** type added (new agent type for human-in-the-loop approval)

## Release Notes Extract

## What's Changed
* .NET: Bump OpenTelemetry packages to 1.15.3 by @SergeyMenshykh in https://github.com/microsoft/agent-framework/pull/5478
* .NET: Support returning durable workflow results from HTTP trigger endpoint by @kshyju in https://github.com/microsoft/agent-framework/pull/5321
* .NET: [Breaking] Support string[] arguments for file-based skill scripts by @SergeyMenshykh in https://github.com/microsoft/agent-framework/pull/5475
* .NET: Add HttpRequestAction support to declarative workflows by @peibekwe in https://github.com/microsoft/agent-framework/pull/5474
* .NET: Add declarative HttpRequestAction sample by @peibekwe in https://github.com/microsoft/agent-framework/pull/5572
* .NET: dotnet: Add hosted-agent User-Agent supplement to outgoing requests by @alliscode in https://github.com/microsoft/agent-framework/pull/5453
* .NET: Add dedicated Foundry.Hosting UnitTest project by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5592
* .NET: Harness Feature branch by @westey-m in https://github.com/microsoft/agent-framework/pull/5310
* .NET: Hosting updates to declarative workflows by @alliscode in https://github.com/microsoft/agent-framework/pull/5589
* docs: enhance README with 1.0 features and improved structure by @chetantoshniwal in https://github.com/microsoft/agent-framework/pull/5534
* .NET: Update version for release by @westey-m in https://github.com/microsoft/agent-framework/pull/5636
* .NET: Add Microsoft.Agents.AI.Hyperlight package for CodeAct integration (.NET) by @eavanvalkenburg in https://github.com/microsoft/agent-framework/pull/5329

**Full Changelog**: https://github.com/microsoft/agent-framework/compare/dotnet-1.3.0...dotnet-1.4.0

## Breaking Changes (requires human verification)

- `AgentFileSkillScript.RunAsync`: Replace `AIFunctionArguments arguments` with `JsonElement? arguments` and add `IServiceProvider? serviceProvider` parameter. Serialize arguments to JSON before passing.
- `AgentFileSkillScriptRunner.Invoke`: Replace `AIFunctionArguments arguments` with `JsonElement? arguments` and add `IServiceProvider? serviceProvider` before `cancellationToken`. Serialize arguments to JSON before passing.
- `AgentFileSkillScriptRunner.BeginInvoke`: Replace `AIFunctionArguments arguments` with `JsonElement? arguments` and add `IServiceProvider? serviceProvider` after arguments. Prefer the async `Invoke` overload over `BeginInvoke` where possible.
- `AgentSkillScript.RunAsync`: Replace `AIFunctionArguments arguments` with `JsonElement? arguments` and add `IServiceProvider? serviceProvider` parameter. Serialize arguments to JSON before passing.

## New Patterns

- **`ToolApprovalRequestContentExtensions` / `ToolApprovalAgent`**: New types supporting human-in-the-loop tool approval flows. Use `ToolApprovalAgent` to intercept tool calls and request human confirmation before execution.
- **Durable workflow results from HTTP trigger**: `RunAsync` results from workflow HTTP endpoints are now surfaced back to the caller, enabling synchronous-style durable workflow invocation over HTTP (PR #5321).
- **`HttpRequestAction` in declarative workflows**: Declarative workflow definitions can now include `HttpRequestAction` steps to call external HTTP endpoints directly from the workflow YAML/JSON definition (PR #5474).
- **`string[]` arguments for file-based skill scripts**: Skill script `RunAsync` / `Invoke` now accept `JsonElement?` arguments instead of `AIFunctionArguments`, enabling richer JSON payloads including string arrays (PR #5475).
- **Hosted-agent User-Agent supplement**: Outgoing HTTP requests from hosted agents now include an identifying `User-Agent` supplement, improving observability and service-side diagnostics (PR #5453).
- **OpenTelemetry packages bumped to 1.15.3**: All OpenTelemetry dependencies updated; no API changes required (PR #5478).
- **`Microsoft.Agents.AI.Hyperlight` package**: New opt-in package for CodeAct integration (Python code execution within agent workflows) via the Hyperlight sandbox (PR #5329).

## Obsolete APIs Added

<!-- TODO: run `MafRunCs0618Hunt` against a project pinned to 1.4.0 to populate. -->

## Known Misalignments

None documented yet.

<!-- AUTO-GENERATED END -->

## Human additions

<!-- Add notes, corrections, and refinements below this heading.
     Content under this heading is PRESERVED across re-runs of the watcher. -->
