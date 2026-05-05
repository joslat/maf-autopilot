using MafAutopilot.Data;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: maf_registry_lookup
///
/// Retrieves the full details for a specific obsolete-API registry entry by its ID.
/// Useful after maf_api_safety returns multiple matches, or when you already know
/// which entry you want (e.g. from the cs0618-hunter skill output).
/// </summary>
[McpServerToolType]
public sealed class RegistryLookupTool
{
    private readonly RegistryService _registry;

    public RegistryLookupTool(RegistryService registry)
    {
        _registry = registry;
    }

    [McpServerTool]
    [Description("""
        Retrieve the full details for a specific MAF obsolete-API registry entry by its ID.

        Entry IDs follow the pattern: MAF{version}-{AREA}-{NNN}
        Examples: MAF130-FAN-IN-001, MAF130-SESSION-001, MAF130-THREAD-001

        Returns the full entry including:
          - Obsolete and replacement signatures
          - Before/after code examples
          - Fix description
          - CS warning code (CS0618 / CS0246 / RUNTIME_SILENT)
          - Guide section reference
          - Whether dotnet-inspect can detect it (usually: No)
          - Notes from real migrations
        """)]
    public string MafRegistryLookup(
        [Description("Registry entry ID, e.g. MAF130-FAN-IN-001. Case-insensitive.")] string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return "Error: entryId must not be empty.";

        var entry = _registry.FindById(entryId.Trim());

        if (entry is null)
        {
            return $"""
                ❌ No registry entry found for '{entryId}'.

                Available IDs ({_registry.AllIds.Count} entries in MAF {_registry.TargetVersion} registry):
                {string.Join("\n  ", _registry.AllIds.Select(id => $"• {id}"))}

                Registry last updated: {_registry.LastUpdated}
                """;
        }

        return RegistryService.FormatEntry(entry);
    }

    [McpServerTool]
    [Description("""
        List all entry IDs in the MAF obsolete-API registry.

        Use this to discover what's in the registry before calling maf_registry_lookup,
        or to verify that a specific pattern is covered.
        """)]
    public string MafRegistryList()
    {
        var ids = _registry.AllIds;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## MAF {_registry.TargetVersion} Obsolete-API Registry");
        sb.AppendLine($"*Last updated: {_registry.LastUpdated} — {ids.Count} entries*");
        sb.AppendLine();
        foreach (var id in ids)
        {
            var entry = _registry.FindById(id)!;
            var detectable = entry.DotnetInspectDetectable ? "dotnet-inspect ✅" : "compiler only ⚠️";
            sb.AppendLine($"| `{id}` | `{entry.Method}` | `{entry.CsWarning}` | {detectable} |");
        }
        sb.AppendLine();
        sb.AppendLine("Use `maf_registry_lookup <id>` for the full fix details of any entry.");
        return sb.ToString();
    }
}
