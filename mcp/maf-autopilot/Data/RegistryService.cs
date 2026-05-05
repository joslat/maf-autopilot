using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MafAutopilot.Data;

/// <summary>
/// Loads and queries the MAF obsolete-API registry.
///
/// Load order:
///   1. MAF_REGISTRY_PATH environment variable — for local dev override or custom registries.
///   2. Embedded resource — bundled at build time from
///      .github/skills/obsolete-api-registry/registry.yaml.
///      This ensures the tool works standalone as a NuGet global tool without external file access.
/// </summary>
public sealed class RegistryService
{
    private readonly Registry _registry;

    public RegistryService()
    {
        _registry = Load();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>All entry IDs in the registry, sorted.</summary>
    public IReadOnlyList<string> AllIds =>
        _registry.Entries.Select(e => e.Id).OrderBy(id => id).ToList();

    /// <summary>MAF version this registry targets (e.g. "1.3.0").</summary>
    public string TargetVersion => _registry.TargetMafVersion;

    /// <summary>Date the registry was last updated.</summary>
    public string LastUpdated => _registry.LastUpdated;

    /// <summary>Find an entry by exact ID (case-insensitive).</summary>
    public RegistryEntry? FindById(string id) =>
        _registry.Entries.FirstOrDefault(e =>
            e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Search for entries where the API name appears in the method name, obsolete signature,
    /// type name, or replacement signature — case-insensitive partial match.
    /// </summary>
    public IReadOnlyList<RegistryEntry> SearchByApiName(string apiName) =>
        _registry.Entries
            .Where(e => Contains(e.Method, apiName)
                     || Contains(e.ObsoleteSignature, apiName)
                     || Contains(e.Type, apiName)
                     || Contains(e.ReplacementSignature, apiName))
            .ToList();

    // -------------------------------------------------------------------------
    // Formatting helpers used by tools
    // -------------------------------------------------------------------------

    public static string FormatEntry(RegistryEntry e)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## Registry entry: {e.Id}");
        sb.AppendLine();
        sb.AppendLine($"**Package:** `{e.Package}`  ");
        sb.AppendLine($"**Type:** `{e.Type}`  ");
        sb.AppendLine($"**Method/API:** `{e.Method}`  ");
        sb.AppendLine($"**Warning:** `{e.CsWarning}`  ");
        sb.AppendLine($"**Guide section:** {e.GuideSection}  ");
        sb.AppendLine($"**dotnet-inspect detectable:** {(e.DotnetInspectDetectable ? "Yes" : "**No** — compiler only")}  ");
        if (e.ArgumentOrderChange)
            sb.AppendLine("**⚠️ Argument order change:** Yes — swapping arguments may compile and run silently with wrong behaviour.");
        sb.AppendLine();
        sb.AppendLine($"### Obsolete (broken)");
        sb.AppendLine($"`{e.ObsoleteSignature}`");
        sb.AppendLine();
        sb.AppendLine($"### Replacement (correct)");
        sb.AppendLine($"`{e.ReplacementSignature}`");
        sb.AppendLine();
        sb.AppendLine($"### Fix");
        sb.AppendLine(e.FixDescription.Trim());
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(e.ExampleBefore))
        {
            sb.AppendLine("### Before");
            sb.AppendLine("```csharp");
            sb.AppendLine(e.ExampleBefore.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(e.ExampleAfter))
        {
            sb.AppendLine("### After");
            sb.AppendLine("```csharp");
            sb.AppendLine(e.ExampleAfter.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(e.Notes))
        {
            sb.AppendLine("### Notes");
            sb.AppendLine(e.Notes.Trim());
        }
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool Contains(string source, string term) =>
        source.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static Registry Load()
    {
        var yaml = ReadYaml();
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance) // we use explicit [YamlMember] aliases
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<Registry>(yaml);
    }

    private static string ReadYaml()
    {
        // 1. Developer override via environment variable.
        var envPath = Environment.GetEnvironmentVariable("MAF_REGISTRY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return File.ReadAllText(envPath);

        // 2. Embedded resource (bundled at build time — works standalone as a NuGet tool).
        var asm = typeof(RegistryService).Assembly;
        using var stream = asm.GetManifestResourceStream("registry.yaml");
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        throw new InvalidOperationException(
            "Cannot locate registry.yaml. " +
            "Set the MAF_REGISTRY_PATH environment variable to the absolute path of the registry file, " +
            "or rebuild the tool so the file is embedded as a resource.");
    }
}
