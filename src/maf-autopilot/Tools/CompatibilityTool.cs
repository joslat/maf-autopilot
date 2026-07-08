using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafCompatibility
///
/// Returns the compatibility row for a specific MAF version — which .NET runtime,
/// `Microsoft.Extensions.AI`, Azure.AI.OpenAI, and Generators-package versions are
/// required / compatible. Data mirrors `docs/compatibility-matrix.md`, which is
/// the canonical doc; this tool just exposes it programmatically for LLM consumption.
///
/// Salvaged from the May-6 pre-Phase-O sketch (tag: `pre-phase-o-may6-sketch`),
/// rewritten in local PascalCase style + drift-tested against the doc.
/// </summary>
[McpServerToolType]
public sealed class CompatibilityTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Look up compatibility requirements for a specific MAF version: required .NET
        runtime, Azure SDK, Microsoft.Extensions.AI, and Generators-package versions.

        Use this BEFORE bumping MAF in your csproj to confirm your runtime/SDK can host
        the target version. For migration steps between versions, use MafMigrationPath.

        Input:
          - mafVersion: SemVer string, e.g. "1.3.0" / "1.2.0" / "1.1.0" / "1.0.0".

        Returns markdown with: .NET runtime, Azure SDK, Extensions.AI, and Generators
        package version requirements. If the version isn't known, returns the list of
        known versions + a pointer to docs/compatibility-matrix.md.
        """)]
    public string MafCompatibility(
        [Description("MAF version to query, e.g. '1.3.0'.")] string mafVersion)
    {
        if (string.IsNullOrWhiteSpace(mafVersion))
            return "Error: mafVersion must not be empty.";

        var key = mafVersion.Trim();
        if (Matrix.TryGetValue(key, out var info))
            return info;

        return $"""
            No compatibility data for MAF version '{mafVersion}'.

            Known versions: {string.Join(", ", Matrix.Keys.OrderBy(k => k, StringComparer.Ordinal))}

            For up-to-date data:
            - `docs/compatibility-matrix.md` in the maf-doctor repo (canonical source).
            - The `maf-release-watcher` GitHub Actions workflow auto-adds a new row when MAF
              ships a new version; if your version is missing, fire the watcher manually
              (`gh workflow run maf-release-watcher.yml -f maf_version={mafVersion}`).
            """;
    }

    /// <summary>
    /// Static compatibility matrix — exposed `internal static` so the drift test
    /// (in <c>CompatibilityToolTests</c>) can verify each row stays in lockstep with
    /// `docs/compatibility-matrix.md`.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Matrix =
        new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1.13.0"] = """
                ## MAF 1.13.0 Compatibility
                
                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.6.0`      | Same transitive pin as 1.12.0 |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` — consumer chooses the backing implementation |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.13.0`         | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |
                
                Breaking (.NET): file-store API renamed — *FileAsync methods removed from AgentFileStore/FileSystemAgentFileStore/InMemoryAgentFileStore; use WriteAsync/ReadAsync/DeleteAsync/ListChildrenAsync/SearchAsync. FileListEntry.FileName→Name. FileAccessProvider tool-name constants renamed. Additive: composable skills-source types; IDisposable on AgentSkillsProvider/AgentSkillsSource/FileAccessProvider. Transitive pins carried from 1.12.0.
                """,

            ["1.12.0"] = """
                ## MAF 1.12.0 Compatibility
                
                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.6.0`      | Same transitive pin as 1.11.1 |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` — consumer chooses the backing implementation |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.12.0`         | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |
                
                Breaking (.NET): the skills-source API gained a required `AgentSkillsSourceContext` — `GetSkillsAsync` (on `AgentSkillsSource` / `AgentFileSkillsSource`) and `AgentSkillsProviderBuilder.UseFilter`'s predicate now receive it (positional call sites / overrides break); `AgentSkillsProviderOptions.DisableCaching` was removed (caching moved to the new `CachingAgentSkillsSourceOptions`). Additive: `AgentSkillsSourceContext` / `CachingAgentSkillsSourceOptions` types. Transitive pins carried from 1.11.1.
                """,

            ["1.11.1"] = """
                ## MAF 1.11.1 Compatibility

                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.6.0`      | Same transitive pin as 1.11.0 |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` — consumer chooses the backing implementation |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.11.1`         | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |

                Breaking (.NET): `AgentSkillsProvider` tools now require approval by default (#6729) — the opt-in `AgentSkillsProviderBuilder.UseScriptApproval()` and `AgentSkillsProviderOptions.ScriptApproval` were removed (CS0246 for old call sites); `AgentMcpSkillsSource` supports archive-type skills (#6631). Additive: `AgentSkillsProviderBuilder.UseSource`; new `ReadSkillResourceToolName` / `RunSkillScriptToolName` constants.
                """,

            ["1.11.0"] = """
                ## MAF 1.11.0 Compatibility

                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.6.0`      | |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` — consumer chooses the backing implementation |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.11.0`         | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |

                Breaking (positional call sites only): `SearchFilesAsync` inserts a `recursive` parameter before `cancellationToken` on `AgentFileStore` / `FileSystemAgentFileStore` / `InMemoryAgentFileStore`. Additive (trailing optional params, source-compatible): `AgentInlineSkill` constructors add `argumentMarshaler`; `TodoProvider` `GetAllTodosAsync` / `GetRemainingTodosAsync` add a trailing `CancellationToken`.
                """,

            ["1.10.0"] = """
                ## MAF 1.10.0 Compatibility

                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.6.0`      | |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` — consumer chooses the backing implementation |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.10.0`         | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |

                Breaking: skill `Content`/`Resources`/`Scripts` properties removed; `ToolApprovalAgent` constructor takes `ToolApprovalAgentOptions?` instead of `JsonSerializerOptions?`; `SubAgentsProvider` / `SubTaskInfo` / `SubTaskStatus` removed; `GroupChatWorkflowBuilder`/`MagenticWorkflowBuilder` `.WithName`/`.WithDescription` removed.
                """,

            ["1.6.1"] = """
                ## MAF 1.6.1 Compatibility

                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.5.1`      | |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` — consumer chooses the backing implementation |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.6.1`          | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |

                Additive release; adds `expectedOutput` parameter to `WorkflowEvaluationExtensions.EvaluateAsync` for ground-truth evaluation.
                """,

            ["1.5.0"] = """
                ## MAF 1.5.0 Compatibility

                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.5.1`      | |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` — consumer chooses the backing implementation |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.5.0`          | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |

                Additive release; new `ToolApprovalAgent` for human-in-the-loop tool approval flows.
                """,

            ["1.4.0"] = """
                ## MAF 1.4.0 Compatibility

                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0, net9.0, net10.0 TFMs all supported |
                | Microsoft.Extensions.AI                   | `>= 10.5.0`      | |
                | Azure.AI.OpenAI                           | _not pinned by MAF_ | BYO via `IChatClient` |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.4.0`          | Source-gen package |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |

                Breaking: `AgentSkillScript.RunAsync` + `AgentFileSkillScriptRunner.Invoke/BeginInvoke` signatures changed —
                `AIFunctionArguments arguments` → `JsonElement? arguments, IServiceProvider? serviceProvider`.
                """,

            ["1.3.0"] = """
                ## MAF 1.3.0 Compatibility

                | Dependency                                | Version          | Notes |
                |-------------------------------------------|------------------|-------|
                | .NET runtime                              | `>= 8.0`         | net8.0 and net9.0 TFMs both supported |
                | Microsoft.Extensions.AI                   | `>= 10.5.0`      | |
                | Azure.AI.OpenAI                           | `>= 2.8.0-beta.1`| |
                | Microsoft.Agents.AI.Workflows.Generators  | `1.3.0` (required) | Source-gen package — executor partial classes need it |
                | Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |

                **Note on optional packages**: `Microsoft.Agents.AI.DevUI` and
                `Microsoft.Agents.AI.Hosting` remain on NuGet as preview-only
                channels (latest `1.5.0-preview.260507.1`). The earlier claim
                "Removed in 1.3.0" was incorrect.
                """,

            ["1.2.0"] = """
                ## MAF 1.2.0 Compatibility

                | Dependency                | Version          | Notes |
                |---------------------------|------------------|-------|
                | .NET runtime              | `>= 8.0`         | |
                | Microsoft.Extensions.AI   | `>= 10.5.0`      | _Pins corrected 2026-05-13 from earlier stale "≥ 10.3.0" claim — see [Phase W.A finding](../../samples/maf-1.2-sample/README.md)._ |
                | Azure.AI.OpenAI           | `>= 2.8.0-beta.1`| 2.6.0/2.7.0 stable were never published. |
                | Generators package        | N/A              | Not required at this version. |
                """,

            ["1.1.0"] = """
                ## MAF 1.1.0 Compatibility

                | Dependency                | Version          | Notes |
                |---------------------------|------------------|-------|
                | .NET runtime              | `>= 8.0`         | |
                | Microsoft.Extensions.AI   | `>= 10.5.0`      | Same transitive pin as 1.2.0 (pins corrected 2026-05-13). |
                | Azure.AI.OpenAI           | `>= 2.8.0-beta.1`| |
                | Generators package        | N/A              | Not required at this version. |
                """,

            ["1.0.0"] = """
                ## MAF 1.0.0 Compatibility

                | Dependency                | Version          | Notes |
                |---------------------------|------------------|-------|
                | .NET runtime              | `>= 8.0`         | |
                | Microsoft.Extensions.AI   | `>= 10.5.0`      | Same transitive pin as 1.2.0 (pins corrected 2026-05-13). |
                | Azure.AI.OpenAI           | `>= 2.8.0-beta.1`| |
                | Sessions                  | `AgentSession` (`ChatClientAgentSession`) | Public surface confirms `AgentThread` was **never** in the 1.0.0 public NuGet — see Phase W.A registry chronology finding. |
                """,
        };
}
