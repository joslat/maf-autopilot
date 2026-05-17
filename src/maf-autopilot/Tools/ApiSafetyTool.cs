using MafAutopilot.Data;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: MafApiSafety
///
/// Answers the question: "Is this MAF API safe to use in MAF 1.3.0?"
///
/// Historically motivated by richlander/dotnet-inspect#316:
/// dotnet-inspect &lt;= v0.7.7 did not surface [Obsolete] at the individual overload level.
/// As of dotnet-inspect v0.7.8 (PR #318), [Obsolete] is surfaced in member listings —
/// but this tool remains the canonical answer because the registry also encodes
/// fix patterns, runtime-only failure classes (e.g., fan-out silent starvation),
/// and project-local invariants that no static inspector can know.
/// </summary>
[McpServerToolType]
public sealed class ApiSafetyTool
{
    private readonly RegistryService _registry;

    public ApiSafetyTool(RegistryService registry)
    {
        _registry = registry;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Check whether a single MAF API symbol is safe to use — returns SAFE or UNSAFE
        with the exact fix recipe from the curated registry.

        Use this tool when you have ONE symbol name (method, type, or partial call) and want
        to know if it's known-broken. For full-project compiler-driven scanning, use
        MafRunCs0618Hunt. For looking up a known registry entry by ID (e.g.,
        MAF130-FAN-IN-001), use MafRegistryLookup.

        Input can be:
          - A method name:              "AddFanInBarrierEdge"
          - A type name:                "AgentThread"
          - A partial call expression:  "agent.SerializeSession"
          - A full class.method:        "WorkflowBuilder.AddFanInBarrierEdge"

        Returns SAFE (no known registry issues) or UNSAFE (with entry ID, fix description,
        before/after code, and guide section). Only covers known registry entries — also
        run the compiler for a complete check.
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

        // Multiple matches — show a summary list and let the caller drill in with MafRegistryLookup.
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"⚠️ MULTIPLE MATCHES — '{apiName}' matches {matches.Count} registry entries in MAF {_registry.TargetVersion}:");
        sb.AppendLine();
        foreach (var entry in matches)
        {
            sb.AppendLine($"  • **{entry.Id}** — `{entry.ObsoleteSignature}` → `{entry.ReplacementSignature}`");
            sb.AppendLine($"    Warning: `{entry.CsWarning}` | Guide section: {entry.GuideSection}");
        }
        sb.AppendLine();
        sb.AppendLine("Use `MafRegistryLookup` with a specific entry ID for the full fix details.");
        return sb.ToString();
    }
}
