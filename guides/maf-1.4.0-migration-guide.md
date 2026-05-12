# MAF 1.4.0 Migration Guide (draft)

<!-- introduced: 1.4.0 | applies-to: 1.3.0.x → 1.4.0.x | deprecated-in: none -->

<!-- AUTO-GENERATED START — anything between AUTO-GENERATED START and AUTO-GENERATED END is overwritten on re-run -->

> ⚠️ Auto-generated stub. Review before relying on it for migrations.

## Versions

- Migrating from: `1.3.0`
- Migrating to: `1.4.0`

## Diff Summary (first 120 lines of `dotnet-inspect` output)

```
diff --package Microsoft.Agents.AI@1.3.0..1.4.0 --oneline    # summary statistics
hape
d

### ToolApprovalRequestContentExtensions

- Type 'Microsoft.Agents.AI.ToolApprovalRequestContentExtensions' was added
rovalAgent' was added

### ToolApprovalAgentBuil
```

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

<!-- TODO: Review the diff above and list breaking changes here -->

## New Patterns

<!-- TODO: Document any new recommended patterns from release notes -->

## Obsolete APIs Added

<!-- TODO: Use MafRunCs0618Hunt against a project pinned to 1.4.0 and document findings -->

## Known Misalignments

<!-- TODO: Document any discrepancies between official docs and assembly behavior -->

<!-- AUTO-GENERATED END -->

## Human additions

<!-- Add notes, corrections, and refinements below this heading.
     Content under this heading is PRESERVED across re-runs of the watcher. -->
