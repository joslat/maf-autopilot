using System.Text;
using System.Text.RegularExpressions;
using MafAutopilot.Data;
using MafAutopilot.Tools;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MafAutopilot;

/// <summary>
/// CLI subcommand: <c>maf-autopilot registry-extract &lt;packageId&gt; &lt;oldVer&gt; &lt;newVer&gt;</c>.
///
/// Runs `dotnet-inspect diff` for the version range, parses the output via the
/// same logic that powers <see cref="DiffPackageTool"/>, and emits draft
/// YAML registry entries to stdout — ready to be appended to registry.yaml
/// in the release-watcher's auto-PR.
///
/// Closes plan #30 — the registry-yaml auto-update loop. Human reviewer fills in
/// the TODO placeholders (replacement_signature, fix_description, example_after,
/// guide_section) during PR review, then merges.
/// </summary>
internal static class RegistryExtractCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: maf-autopilot registry-extract <packageId> <oldVersion> <newVersion>");
            return 1;
        }

        var packageId = args[1];
        var oldVersion = args[2];
        var newVersion = args[3];

        if (!DiffPackageTool.IsValidPackageId(packageId))
        {
            Console.Error.WriteLine($"Error: invalid package id '{packageId}'.");
            return 2;
        }
        if (!DiffPackageTool.IsValidVersion(oldVersion) || !DiffPackageTool.IsValidVersion(newVersion))
        {
            Console.Error.WriteLine("Error: invalid version string. Use SemVer-ish only.");
            return 2;
        }

        var (exitCode, output) = ProcessRunner.RunDotnetInspectDiff(packageId, oldVersion, newVersion);
        if (exitCode != 0)
        {
            Console.Error.WriteLine($"⚠️ dotnet-inspect exited with code {exitCode}. Output may be partial.");
        }

        var parsed = DiffPackageTool.ParseDiffOutput(output);
        var entries = ExtractDraftEntries(parsed, packageId, newVersion);

        if (entries.Count == 0)
        {
            Console.WriteLine("# No new draft registry entries — diff contained no breaking-change candidates.");
            return 0;
        }

        Console.WriteLine($"# Draft registry entries — auto-generated 2026-05-12 from {packageId} {oldVersion} → {newVersion}");
        Console.WriteLine($"# {entries.Count} candidate(s). Review each before merging — TODO fields require human input.");
        Console.WriteLine();
        foreach (var entry in entries)
            Console.WriteLine(entry);
        return 0;
    }

    // -------------------------------------------------------------------------
    // Pure-function core (testable)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Selects every entry that looks like a candidate for the obsolete-API
    /// registry (breaking-change kind: Removed or SignatureChanged, OR any
    /// entry tagged with `[Obsolete]`) and emits YAML stubs for them.
    /// </summary>
    public static IReadOnlyList<string> ExtractDraftEntries(
        DiffParseResult parsed,
        string packageId,
        string newVersion)
    {
        if (parsed.NoChangesDetected) return [];

        var candidates = parsed.Breaking
            .Where(e => e.Kind is DiffEntryKind.Removed or DiffEntryKind.SignatureChanged
                     || e.IsObsolete)
            .Concat(parsed.Additive.Where(e => e.IsObsolete))   // additive-but-obsolete = newly deprecated
            .ToList();

        var entries = new List<string>();
        var idCounter = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entry in candidates)
        {
            var area = AreaCodeFromType(entry.Type);
            idCounter.TryGetValue(area, out var count);
            count++;
            idCounter[area] = count;
            var id = $"MAF{VersionToIdSegment(newVersion)}-{area}-{count:000}";

            entries.Add(BuildYamlEntry(id, packageId, newVersion, entry));
        }
        return entries;
    }

    /// <summary>
    /// Turns a SemVer string into the registry's ID prefix: <c>1.3.0 → 130</c>.
    /// </summary>
    internal static string VersionToIdSegment(string version)
    {
        var parts = version.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var seg = new StringBuilder();
        foreach (var p in parts)
        {
            if (int.TryParse(p, out _)) seg.Append(p);
            if (seg.Length >= 3) break;
        }
        return seg.Length > 0 ? seg.ToString() : "XXX";
    }

    /// <summary>
    /// Derives a short uppercase area code from a type name. Examples:
    ///   <c>FraudExecutor → EXEC</c>, <c>AgentSession → SESSION</c>,
    ///   <c>WorkflowBuilder → WORKFLOW</c>, <c>AddFanInBarrierEdge → ADDFAN</c>.
    /// </summary>
    internal static string AreaCodeFromType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return "AREA";

        // Strip the namespace prefix.
        var simple = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
        // Drop trailing common suffixes that don't add information.
        foreach (var suffix in new[] { "Executor", "Builder", "Options", "Attribute", "Provider", "Extensions" })
        {
            if (simple.EndsWith(suffix, StringComparison.Ordinal) && simple.Length > suffix.Length)
            {
                var area = suffix.ToUpperInvariant();
                // Cap at 8 chars for readability.
                return area.Length > 8 ? area[..8] : area;
            }
        }
        // Otherwise take the first uppercase-prefixed segment.
        var match = Regex.Match(simple, @"^[A-Z][a-z]*");
        var seg = match.Success ? match.Value : simple;
        var upper = seg.ToUpperInvariant();
        return upper.Length > 8 ? upper[..8] : upper;
    }

    private static string BuildYamlEntry(string id, string packageId, string version, DiffEntry entry)
    {
        // Try to pull the signature pair out of "Member 'X' signature changed: `oldSig` -> `newSig`"
        var sigMatch = Regex.Match(entry.Body, @"`([^`]+)`\s*->\s*`([^`]+)`");
        var obsoleteSig = sigMatch.Success ? sigMatch.Groups[1].Value : "TODO";
        var replacementSig = sigMatch.Success ? sigMatch.Groups[2].Value : "TODO";

        // Pull the method name from "'Foo'" if present.
        var methodMatch = Regex.Match(entry.Body, @"'([A-Za-z_][A-Za-z0-9_.]*)'");
        var method = methodMatch.Success ? methodMatch.Groups[1].Value : "TODO";

        var verdict = entry.Kind switch
        {
            DiffEntryKind.Removed => "Type or member REMOVED in new version — references will fail to compile (CS0246).",
            DiffEntryKind.SignatureChanged => "Signature changed — call sites may fail to compile or bind to a different overload.",
            _ when entry.IsObsolete => "Member is now `[Obsolete]` — call sites trigger CS0618 warnings.",
            _ => "Behavioural / structural change — review the diff entry.",
        };

        // Build a typed RegistryEntry and serialise with YamlDotNet (Phase F.3 — was
        // string-concat which broke on YAML-significant characters in `notes`).
        var registryEntry = new RegistryEntry
        {
            Id = id,
            Package = packageId,
            VersionIntroduced = version,
            Type = entry.Type,
            Method = method,
            ObsoleteSignature = obsoleteSig,
            ReplacementSignature = replacementSig,
            ArgumentOrderChange = false,
            FixDescription = "TODO — review the diff entry below and document the canonical fix",
            ExampleBefore = "// TODO — show a real call site that breaks\n",
            ExampleAfter = "// TODO — show the canonical 1.x replacement\n",
            CsWarning = ClassifyCsWarning(entry),
            GuideSection = "TODO",
            DotnetInspectDetectable = entry.IsObsolete,
            Notes =
                $"AUTO-GENERATED by `maf-autopilot registry-extract`. " +
                $"Diff entry: {entry.Body.Replace("\n", " ")}. " +
                $"Verdict: {verdict} " +
                "Requires human review before merging.",
        };

        // YamlDotNet emits ONE entry; we wrap in a list-item dash for canonical
        // registry.yaml syntax.
        var serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)   // YamlMember(Alias) drives the keys
            .DisableAliases()
            .Build();
        var yaml = serializer.Serialize(new[] { registryEntry });

        // Indent every line by 2 spaces so the entry slots cleanly under the existing
        // `entries:` block in registry.yaml. The serializer emits `- id: …` at column 0;
        // the production registry indents list items by 2. Without this, the watcher's
        // `>> $REGISTRY` append produces malformed YAML (the appended `- id: …` at
        // column 0 isn't a child of `entries:` anymore).
        // Pinned by `RegistryExtractCommandTests.Additivity_DraftEntries_AppendedToExistingRegistry_PreserveAllOriginalEntries`.
        var indented = string.Join('\n', yaml.Split('\n').Select(line => line.Length == 0 ? line : "  " + line));
        return indented + "\n";
    }

    private static string ClassifyCsWarning(DiffEntry entry) => entry switch
    {
        { Kind: DiffEntryKind.Removed } => "CS0246",
        _ when entry.IsObsolete => "CS0618",
        _ => "CS0618",
    };
}
