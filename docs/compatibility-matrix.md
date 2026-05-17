# MAF Compatibility Matrix

<!-- auto-updated-by: maf-release-watcher | last-updated: 2026-05-12 -->

<!--
  Note on the Azure.AI.OpenAI column: MAF doesn't pin Azure.AI.OpenAI directly.
  It exposes IChatClient as the extension point, and Azure.AI.OpenAI (or any
  other backing implementation) is configured by the consumer at composition
  time. The 1.3.0 value (≥ 2.8.0-beta.1) reflects the version the MAF docs /
  samples used at the time of the 1.3.0 release — not a hard csproj constraint.
  For 1.4+, the chat-client extension point is unchanged; we don't have a
  hard MAF-enforced pin to record so the cell shows "(not pinned by MAF)".
-->

> This file is the single source of truth for MAF version ↔ dependency version compatibility.
> It is referenced by `maf-autopilot` MCP server tools (`maf_compatibility`) and auto-updated by the `maf-release-watcher` skill and GitHub Actions workflow.

---

## Compatibility Table

| MAF Version | Microsoft.Extensions.AI | .NET | Azure.AI.OpenAI | Generators Package | Notes |
|-------------|------------------------|------|-----------------|--------------------|-------|
| **1.6.1** | `≥ 10.5.1` | `≥ 8.0` | _(not pinned by MAF — BYO via IChatClient)_ | `1.6.1` | Adds `expectedOutput` parameter to workflow evaluation API. |
| **1.5.0** | `≥ 10.5.1` | `≥ 8.0` | _(not pinned by MAF — BYO via IChatClient)_ | `1.5.0` | Additive release; new `ToolApprovalAgent`. |
| **1.4.0** | `≥ 10.5.0` | `≥ 8.0` | _(not pinned by MAF — BYO via IChatClient)_ | `1.4.0` | `AgentSkillScript` surface changed: `AIFunctionArguments` → `JsonElement?` + `IServiceProvider?`. |
| **1.3.0** | `≥ 10.5.0` | `≥ 8.0` | `≥ 2.8.0-beta.1` | `1.3.0` **(required)** | Major breaking release. Generators package mandatory for executors. |
| 1.2.0 | `≥ 10.5.0` | `≥ 8.0` | `≥ 2.8.0-beta.1` | N/A | No source generator requirement. _Pins corrected 2026-05-13 from earlier stale "≥ 10.3.0 / ≥ 2.6.0" claim — see [Phase W.A finding](../samples/maf-1.2-sample/README.md#phase-wa-finding--maf-1213-public-surface-is-essentially-the-same)._ |
| 1.1.0 | `≥ 10.5.0` | `≥ 8.0` | `≥ 2.8.0-beta.1` | N/A | Same transitive pins as 1.2.0. _Pins corrected 2026-05-13 from earlier stale "≥ 9.4.0 / ≥ 2.4.0" claim._ |
| 1.0.0 | `≥ 10.5.0` | `≥ 8.0` | `≥ 2.8.0-beta.1` | N/A | Initial release. Same transitive pins as 1.2.0 (verified via `dotnet restore` against `Microsoft.Agents.AI 1.0.0`). _Pins corrected 2026-05-13 from earlier stale "≥ 9.0.0 / ≥ 2.0.0" claim._ |

---

## Removed Packages by Version

| Package | Removed In | Replacement |
|---------|-----------|-------------|
| `Microsoft.Agents.AI.DevUI` | _NOT removed — see correction below_ | _(see note)_ |
| `Microsoft.Agents.AI.Hosting` | _NOT removed — see correction below_ | _(see note)_ |

> **Correction (2026-05-13):** the original "Removed in 1.3.0" claim was wrong.
> Both packages are still on nuget.org and continue to publish previews tracking
> the main MAF release stream (latest at time of correction:
> `1.5.0-preview.260507.1` for both). They're preview-only — not promoted to a
> stable release alongside the main `Microsoft.Agents.AI` package — but they
> have NEVER been removed. Customers who relied on these in earlier previews
> should continue using preview-channel versions.

---

## NuGet Package IDs

| Logical name | NuGet Package ID |
|-------------|-----------------|
| Core framework | `Microsoft.Agents.AI` |
| Workflows | `Microsoft.Agents.AI.Workflows` |
| Source generator | `Microsoft.Agents.AI.Workflows.Generators` |
| Extensions.AI | `Microsoft.Extensions.AI` |
| Azure OpenAI | `Azure.AI.OpenAI` |

---

## Version Tracking

The `.maf-version` file at the repository root records the latest MAF version this toolkit's data covers. The `maf-release-watcher` GitHub Actions workflow compares this against the NuGet feed to detect new releases.

Current tracked version: **`1.6.1`** (see `.maf-version`)

---

## Update Process

When a new MAF version ships:

1. `maf-release-watcher` GitHub Actions workflow detects the new version (weekly schedule or manual dispatch)
2. Runs `nuget-diff-analyzer` skill for the version delta
3. Updates this file with a new row
4. Updates `maf-obsolete-api-registry/registry.yaml` with new CS0618 entries
5. Adds a new section to `guides/maf-1.3.0-migration-guide.md` (or creates a new guide for the new version)
6. Creates a PR for human review

---

*Maintained by maf-autopilot | See `.github/skills/maf-release-watcher/SKILL.md` for update process*
