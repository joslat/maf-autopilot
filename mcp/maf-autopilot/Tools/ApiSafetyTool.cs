using MafAutopilot.Data;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: maf_api_safety
///
/// Answers the question: "Is this MAF API safe to use in MAF 1.3.0?"
///
/// Searches the compiler-verified registry for known CS0618/CS0246/runtime-silent
/// patterns matching the given API name. Complements dotnet-inspect (which queries
/// NuGet package API surfaces) by checking whether a specific call site is known-unsafe
/// in the migration context.
/// </summary>
[McpServerToolType]
public sealed class ApiSafetyTool
{
    private readonly RegistryService _registry;

    public ApiSafetyTool(RegistryService registry)
    {
        _registry = registry;
    }

    [McpServerTool(Name = "maf_api_safety")]
    [Description("""
        Check whether a MAF API is safe to use in MAF 1.3.0.

        Searches the obsolete-API registry for known CS0618 warnings, silent runtime failures,
        and removed types (CS0246) matching the given API name.

        Input can be:
          - A method name:              "AddFanInBarrierEdge"
          - A type name:                "AgentThread"
          - A partial call expression:  "agent.SerializeSession"
          - A full class.method:        "WorkflowBuilder.AddFanInBarrierEdge"

        Returns SAFE (no known issues) or UNSAFE (with the registry entry, fix, and guide section).

        IMPORTANT: This tool only covers *known* issues in the registry. For a complete check,
        also run: dotnet build 2>&1 | Select-String "warning CS0618|error CS0246"
        """)]
    public string MafApiSafety(
        [Description("API name to check — method name, type name, or partial call expression.")] string apiName)
    {
        if (string.IsNullOrWhiteSpace(apiName))
            return "Error: apiName must not be empty.";

        var matches = _registry.SearchByApiName(apiName.Trim());

        if (matches.Count == 0)
        {
            return $"""
                ✅ SAFE — No known issues for '{apiName}' in MAF {_registry.TargetVersion}.

                The registry has {_registry.AllIds.Count} entries as of {_registry.LastUpdated}.
                This covers all CS0618/CS0246 patterns discovered in real migrations.

                Note: This checks the *known* registry only. Always run the compiler for a full check:
                    dotnet build 2>&1 | Select-String "warning CS0618|error CS0246"
                """;
        }

        if (matches.Count == 1)
        {
            return $"""
                ❌ UNSAFE — '{apiName}' matches a known issue in MAF {_registry.TargetVersion}:

                {RegistryService.FormatEntry(matches[0])}
                """;
        }

        // Multiple matches — show a summary list and let the caller drill in with maf_registry_lookup.
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"⚠️ MULTIPLE MATCHES — '{apiName}' matches {matches.Count} registry entries in MAF {_registry.TargetVersion}:");
        sb.AppendLine();
        foreach (var entry in matches)
        {
            sb.AppendLine($"  • **{entry.Id}** — `{entry.ObsoleteSignature}` → `{entry.ReplacementSignature}`");
            sb.AppendLine($"    Warning: `{entry.CsWarning}` | Guide section: {entry.GuideSection}");
        }
        sb.AppendLine();
        sb.AppendLine("Use `maf_registry_lookup` with a specific entry ID for the full fix details.");
        return sb.ToString();
    }
}
