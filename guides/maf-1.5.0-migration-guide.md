# MAF 1.5.0 Migration Guide (draft)

<!-- introduced: 1.5.0 | applies-to: 1.4.0.x → 1.5.0.x | deprecated-in: none -->

<!-- AUTO-GENERATED START — anything between AUTO-GENERATED START and AUTO-GENERATED END is overwritten on re-run -->

> ⚠️ Auto-generated stub. Review before relying on it for migrations.

## Versions

- Migrating from: `1.4.0`
- Migrating to: `1.5.0`

## Diff Summary (first 120 lines of `dotnet-inspect` output)

```
diff --package Microsoft.Agents.AI@1.4.0..1.5.0 --oneline    # summary statistics
hape
stem.IDisposable' was added
- Member 'Dispose' was added
- Member 'GetAllTodosAsync' was added
- Member 'GetRemainingTodosAsync' was added

### TodoProviderOptions

- Me
```

## Release Notes Extract

## What's Changed
* .NET: feat: Implement message filtering to exclude non-portable content typ… by @tarockey in https://github.com/microsoft/agent-framework/pull/5410
* .NET: Add allow listing for WebBrowsingTool by @westey-m in https://github.com/microsoft/agent-framework/pull/5605
* .NET: fix: JSON Serialization issue with MultiPartyConversation by @lokitoth in https://github.com/microsoft/agent-framework/pull/5653
* .NET: Improve Todo multithreading and inject todos into message list by @westey-m in https://github.com/microsoft/agent-framework/pull/5655
* .NET: fix: Add missing Workflows "Shared" sources to solution by @lokitoth in https://github.com/microsoft/agent-framework/pull/5656
* .NET: Fix QuestionExecutor looping after GotoAction re-entry in declarative workflows by @peibekwe in https://github.com/microsoft/agent-framework/pull/5635
* .NET: Fix YAML block scalar parsing for file skills by @tejakusireddy in https://github.com/microsoft/agent-framework/pull/5610
* .NET: Add hosted agent observability sample by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5660
* .NET: Bump MEAI to 10.5.1 and add Foundry per-call x-client header support by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5652
* .NET: Fix flaky declarative test by @peibekwe in https://github.com/microsoft/agent-framework/pull/5669
* .NET: Add Foundry.Hosting.IntegrationTests by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5598
* .NET: Issue 5662 by @alliscode in https://github.com/microsoft/agent-framework/pull/5668
* .NET: Support reasoning events in AGUI by @jeffinsibycoremont in https://github.com/microsoft/agent-framework/pull/4953
* .NET: feat: Update Github Copilot SDK to 1.0.0-beta.2 by @lokitoth in https://github.com/microsoft/agent-framework/pull/5699
* .NET: feat: Implement Magentic Orchestration for .NET by @lokitoth in https://github.com/microsoft/agent-framework/pull/5595
* .NET: Foundry.Hosting IT - avoid MSB3026 in publish step by @rogerbarreto in https://github.com/microsoft/agent-framework/pull/5689
* .NET: Python: Add dotnet integration test report to CI by @giles17 in https://github.com/microsoft/agent-framework/pull/5515
* .NET: Non-thread-safe sequence number generation may cause duplicate or out-of-order IDs by @tuanaiseo in https://github.com/microsoft/agent-framework/pull/5320
* .NET: Fix typo: sesionElement -> sessionElement by @XiongHaoTrigger in https://github.com/microsoft/agent-framework/pull/5674
* .NET: Mark Magentic Orchestration Experimental by @lokitoth in https://github.com/microsoft/agent-framework/pull/5704
* .NET: Fix function_call_output.output to be a JSON string on the wire by @alliscode in https://github.com/microsoft/agent-framework/pull/5705
* .NET: Update version for release by @lokitoth in https://github.com/microsoft/agent-framework/pull/5703

## New Contributors
* @tarockey made their first contribution in https://github.com/microsoft/agent-framework/pull/5410
* @tejakusireddy made their first contribution in https://github.com/microsoft/agent-framework/pull/5610
* @jeffinsibycoremont made their first contribution in https://github.com/microsoft/agent-framework/pull/4953
* @tuanaiseo made their first contribution in https://github.com/microsoft/agent-framework/pull/5320
* @XiongHaoTrigger made their first contribution in https://github.com/microsoft/agent-framework/pull/5674

**Full Changelog**: https://github.com/microsoft/agent-framework/compare/dotnet-1.4.0...dotnet-1.5.0

## Breaking Changes (requires human verification)

<!-- TODO: Review the diff above and list breaking changes here -->

## New Patterns

<!-- TODO: Document any new recommended patterns from release notes -->

## Obsolete APIs Added

<!-- TODO: Use MafRunCs0618Hunt against a project pinned to 1.5.0 and document findings -->

## Known Misalignments

<!-- TODO: Document any discrepancies between official docs and assembly behavior -->

<!-- AUTO-GENERATED END -->

## Human additions

<!-- Add notes, corrections, and refinements below this heading.
     Content under this heading is PRESERVED across re-runs of the watcher. -->
