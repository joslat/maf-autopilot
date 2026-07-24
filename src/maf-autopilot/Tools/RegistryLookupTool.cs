using MafDoctor.Data;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafRegistryLookup
///
/// Retrieves the full details for a specific obsolete-API registry entry by its ID.
/// Useful after MafApiSafety returns multiple matches, or when you already know
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

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Retrieve the full details for a specific MAF obsolete-API registry entry by ID.

        Use this when you know the registry entry ID (e.g., MAF130-FAN-IN-001) and want
        the full fix recipe. For symbol-name lookup (you have the API name, not the ID),
        use MafApiSafety. To list all entry IDs, use MafRegistryList.

        Entry IDs follow the pattern: MAF{version}-{AREA}-{NNN}
        Examples: MAF130-FAN-IN-001, MAF130-SESSION-001, MAF130-THREAD-001

        Returns the full entry including: obsolete/replacement signatures, before/after
        code examples, fix description, CS warning code, guide section reference, and
        migration notes.
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

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        List all entry IDs in the MAF obsolete-API registry with a one-line summary each.

        Use this to discover what's in the registry before calling MafRegistryLookup,
        or to verify a specific pattern is covered. For symbol-based search (you have
        the API name), use MafApiSafety. For a full entry, use MafRegistryLookup.

        Returns markdown with entry ID, type, CS warning code, and one-line description
        for each registry entry.
        """)]
    public string MafRegistryList()
    {
        var ids = _registry.AllIds;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## MAF {_registry.TargetVersion} Obsolete-API Registry");
        sb.AppendLine($"*Last updated: {_registry.LastUpdated} — {ids.Count} entries*");
        sb.AppendLine();
        // REP-36: emit the header row so the output actually renders as a table (and add
        // the Fix column the description promises). Upstream fix text is MdInline-neutralized.
        sb.AppendLine("| ID | Obsolete method | CS warning | Detection | Fix |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var id in ids)
        {
            var entry = _registry.FindById(id)!;
            var detectable = entry.DotnetInspectDetectable ? "dotnet-inspect ✅" : "compiler only ⚠️";
            var fixFirst = entry.FixDescription.Split('\n')[0].Trim();
            sb.AppendLine($"| `{id}` | `{entry.Method}` | `{entry.CsWarning}` | {detectable} | {LlmFencing.MdInline(fixFirst)} |");
        }
        sb.AppendLine();
        sb.AppendLine("Use `MafRegistryLookup <id>` for the full fix details of any entry.");
        return sb.ToString();
    }
}
