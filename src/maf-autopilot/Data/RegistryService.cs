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

    /// <summary>
    /// Pre-built lowercase haystack per entry, covering every searchable string field.
    /// Centralising the field list here means adding a new field is a single-line change
    /// (in <see cref="BuildHaystack"/>) instead of an OR-chain that grew bug-by-bug.
    /// </summary>
    private readonly Dictionary<RegistryEntry, string> _haystacks;

    public RegistryService()
    {
        _registry = Load();
        _haystacks = _registry.Entries.ToDictionary(e => e, BuildHaystack);
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
    /// Search for entries where the API name appears anywhere in the entry's textual
    /// content (case-insensitive substring). Covers every string field — see
    /// <see cref="BuildHaystack"/> for the list. To extend, edit that one method.
    /// </summary>
    public IReadOnlyList<RegistryEntry> SearchByApiName(string apiName)
    {
        if (string.IsNullOrWhiteSpace(apiName))
            return [];

        var needle = apiName.Trim().ToLowerInvariant();
        return _registry.Entries
            .Where(e => _haystacks[e].Contains(needle, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Return entries whose <see cref="RegistryEntry.AppliesToCodebases"/> marker
    /// is satisfied by the given codebase version. Entries with a null/empty
    /// marker are always returned (default = "applies to any").
    ///
    /// Marker grammar:
    ///   - <c>"pre-X.Y.Z"</c>  → entry applies when <paramref name="codebaseVersion"/> &lt; X.Y.Z.
    ///   - <c>"X.Y.Z+"</c>     → entry applies when <paramref name="codebaseVersion"/> &gt;= X.Y.Z.
    ///   - <c>"X.Y.Z"</c>      → entry applies when <paramref name="codebaseVersion"/> == X.Y.Z.
    ///   - anything else       → defensively treated as "applies to any" (logs a warning in v2).
    ///
    /// Version comparison uses <see cref="Version"/> semantics (build-revision-aware,
    /// so "1.10.0" sorts AFTER "1.9.0" correctly). Prerelease tags (-alpha, -beta) are stripped
    /// before parsing — close enough for v1; sufficient for SemVer-ish MAF versions.
    /// </summary>
    public IReadOnlyList<RegistryEntry> GetEntriesForCodebaseVersion(string codebaseVersion)
    {
        if (!TryParseVersion(codebaseVersion, out var queryVersion))
            return _registry.Entries.ToList(); // bad input → return all, don't filter

        return _registry.Entries
            .Where(e => AppliesTo(e, queryVersion))
            .ToList();
    }

    private static bool AppliesTo(RegistryEntry e, Version queryVersion)
    {
        var marker = e.AppliesToCodebases?.Trim();
        if (string.IsNullOrEmpty(marker))
            return true; // no marker → applies to any version

        // "pre-X.Y.Z" — applies when query < X.Y.Z.
        if (marker.StartsWith("pre-", StringComparison.OrdinalIgnoreCase))
        {
            var verPart = marker.Substring(4);
            return TryParseVersion(verPart, out var threshold) && queryVersion < threshold;
        }

        // "X.Y.Z+" — applies when query >= X.Y.Z.
        if (marker.EndsWith('+'))
        {
            var verPart = marker.Substring(0, marker.Length - 1);
            return TryParseVersion(verPart, out var threshold) && queryVersion >= threshold;
        }

        // Exact "X.Y.Z" — applies only at that version.
        if (TryParseVersion(marker, out var exact))
            return queryVersion == exact;

        // Unknown marker shape — defensively include the entry.
        return true;
    }

    /// <summary>
    /// Parses a SemVer-ish string into <see cref="Version"/>. Strips any prerelease
    /// suffix (`-alpha`, `-beta`, `-rc`, `-preview`) before parsing so "1.3.0-alpha-5" → 1.3.0.
    /// </summary>
    private static bool TryParseVersion(string raw, out Version version)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            version = new Version(0, 0);
            return false;
        }

        var dashIdx = raw.IndexOf('-');
        var core = dashIdx > 0 ? raw.Substring(0, dashIdx) : raw;
        return Version.TryParse(core, out version!);
    }

    /// <summary>
    /// Concatenates every searchable string field on a registry entry into a single
    /// lowercase blob. **This is the canonical "what is searchable" definition** — add
    /// new fields here and only here.
    ///
    /// Note: <see cref="RegistryEntry.AppliesToCodebases"/> is intentionally EXCLUDED —
    /// it's a structural marker, not API content, and including it would confuse the
    /// CS0618 hunter's symbol-based matching.
    /// </summary>
    private static string BuildHaystack(RegistryEntry e) =>
        string.Join(
            '\n',
            e.Id,
            e.Package,
            e.Type,
            e.Method,
            e.ObsoleteSignature,
            e.ReplacementSignature,
            e.FixDescription,
            e.ExampleBefore,
            e.ExampleAfter,
            e.CsWarning,
            e.GuideSection,
            e.Notes).ToLowerInvariant();

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

    private static Registry Load()
    {
        var yaml = ReadYaml();
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance) // we use explicit [YamlMember] aliases
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<Registry>(yaml);
    }

    // Security: cap the env-var-supplied registry size at 5 MB. The embedded registry
    // is ~50 KB; the env-var path is a developer convenience for local registry overrides.
    // A multi-gigabyte file silently dropped at the override path would OOM the host on
    // first MCP request. Real registries are KB-scale.
    private const long RegistryMaxBytes = 5 * 1024 * 1024;

    private static string ReadYaml()
    {
        // 1. Developer override via environment variable.
        var envPath = Environment.GetEnvironmentVariable("MAF_REGISTRY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            var info = new FileInfo(envPath);
            if (info.Length > RegistryMaxBytes)
                throw new InvalidOperationException(
                    $"MAF_REGISTRY_PATH file '{envPath}' is {info.Length / 1024} KB — exceeds the {RegistryMaxBytes / 1024 / 1024} MB safety cap. " +
                    "Real MAF registries are KB-scale; refusing to load.");
            return File.ReadAllText(envPath);
        }

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
