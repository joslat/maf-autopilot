using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MafDoctor.Data;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafDiffPackage
///
/// Wraps <c>dotnet-inspect@0.7.8 diff</c> for a NuGet package version range and
/// post-processes the markdown output into a structured report: breaking changes,
/// additive changes, and (where present) newly obsoleted members cross-referenced
/// to the MAF registry.
///
/// The output format was verified against `dotnet-inspect@0.7.8` on 2026-05-11:
/// markdown with `## Breaking Changes` / `## Additive Changes` sections, each
/// containing `### TypeName` subsections with `- Member ...` / `- Type ...` bullets.
/// </summary>
[McpServerToolType]
public sealed class DiffPackageTool
{
    private readonly RegistryService _registry;

    public DiffPackageTool(RegistryService registry)
    {
        _registry = registry;
    }

    // Annotation rationale: OpenWorld = true because dotnet-inspect reaches
    // api.nuget.org (and any configured private feeds) to download the package
    // versions being diffed. ReadOnly = true is correct — the tool produces a
    // text report and writes nothing to the user's workspace.
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = true)]
    [Description("""
        Diff two versions of a NuGet package's public API surface.

        Runs `dotnet-inspect diff --package <id>@<old>..<new>` and post-processes the
        markdown output: breaking changes (signature changes, removals), additive
        changes (new types and members), and cross-references each finding to the MAF
        obsolete-API registry where possible.

        Example: MafDiffPackage("Microsoft.Agents.AI", "1.2.0", "1.3.0")

        Note: requires `dotnet-inspect` (or `dnx dotnet-inspect`) on PATH.
        Pin to v0.7.8 or later — earlier versions miss [Obsolete] in member listings.
        """)]
    public string MafDiffPackage(
        [Description("NuGet package id, e.g. Microsoft.Agents.AI")] string packageId,
        [Description("Old (baseline) version, e.g. 1.2.0")] string oldVersion,
        [Description("New (target) version, e.g. 1.3.0")] string newVersion)
    {
        if (!IsValidPackageId(packageId))
            return $"Error: packageId must match `[A-Za-z0-9._-]+`, got '{packageId}'.";
        if (!IsValidVersion(oldVersion) || !IsValidVersion(newVersion))
            return $"Error: oldVersion and newVersion must be valid SemVer-ish strings; got '{oldVersion}', '{newVersion}'.";

        var (exitCode, output) = ProcessRunner.RunDotnetInspectDiff(packageId, oldVersion, newVersion);
        var parsed = ParseDiffOutput(output);
        return FormatReport(packageId, oldVersion, newVersion, exitCode, parsed, _registry);
    }

    // -------------------------------------------------------------------------
    // Input validation (defends against argument injection via the LLM)
    // -------------------------------------------------------------------------

    // Phase 5.G fixup — ReDoS hygiene on every regex in this file. The
    // patterns here parse `dotnet-inspect` markdown output (third-party
    // attacker-influenced text via package metadata) so the +/.+ classes
    // could in theory be probed for backtracking. NonBacktracking on the
    // bounded patterns + a 2s timeout caps any future drift.
    private const RegexOptions HygieneOptions =
        RegexOptions.Compiled | RegexOptions.NonBacktracking;
    private const RegexOptions HygieneOptionsMultiline =
        RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.Multiline;
    private static readonly TimeSpan RegexBudget = TimeSpan.FromSeconds(2);

    private static readonly Regex PackageIdRegex = new(@"^[A-Za-z0-9._-]+$", HygieneOptions, RegexBudget);
    private static readonly Regex VersionRegex = new(@"^[A-Za-z0-9.+\-]+$", HygieneOptions, RegexBudget);

    internal static bool IsValidPackageId(string s) =>
        !string.IsNullOrWhiteSpace(s) && PackageIdRegex.IsMatch(s);
    internal static bool IsValidVersion(string s) =>
        !string.IsNullOrWhiteSpace(s) && VersionRegex.IsMatch(s);

    // -------------------------------------------------------------------------
    // Pure parser core (testable without an external invocation)
    //
    // Real dotnet-inspect@0.7.8 -- diff output (verified 2026-05-11):
    //
    //     # API Diff: <package-name>
    //     | Field | Value |
    //     | ----- | ----- |
    //     | Versions | **<old>** -> **<new>** |
    //     | Summary | **Summary:** N breaking, M additive across K types |
    //
    //     ## Breaking Changes
    //
    //     ### TypeA
    //     - Member 'foo' signature changed: `oldsig` -> `newsig`
    //     - Member 'bar' was removed
    //
    //     ## Additive Changes
    //
    //     ### TypeB
    //     - Type '<FullName>' was added
    //     - Member 'baz' was added
    //
    // When there are no changes the body is:
    //
    //     > [!NOTE]
    //     > No API changes detected.
    // -------------------------------------------------------------------------

    private static readonly Regex SectionRegex = new(@"^##\s+(?<heading>.+?)\s*$", HygieneOptions, RegexBudget);
    private static readonly Regex TypeRegex = new(@"^###\s+(?<type>.+?)\s*$", HygieneOptions, RegexBudget);
    private static readonly Regex BulletRegex = new(@"^\s*-\s+(?<body>.+?)\s*$", HygieneOptions, RegexBudget);

    /// <summary>
    /// Parses the markdown-formatted output of `dotnet-inspect diff`. Pure: no I/O.
    /// </summary>
    public static DiffParseResult ParseDiffOutput(string diffOutput)
    {
        var breaking = new List<DiffEntry>();
        var additive = new List<DiffEntry>();

        if (string.IsNullOrWhiteSpace(diffOutput))
            return new DiffParseResult([], [], false);

        var noChanges = diffOutput.Contains("No API changes detected", StringComparison.OrdinalIgnoreCase);

        DiffSection current = DiffSection.None;
        string? currentType = null;

        foreach (var rawLine in diffOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            var sectionMatch = SectionRegex.Match(line);
            if (sectionMatch.Success)
            {
                current = ClassifySection(sectionMatch.Groups["heading"].Value);
                currentType = null;
                continue;
            }

            var typeMatch = TypeRegex.Match(line);
            if (typeMatch.Success)
            {
                currentType = typeMatch.Groups["type"].Value;
                continue;
            }

            if (current == DiffSection.None) continue;

            var bulletMatch = BulletRegex.Match(line);
            if (!bulletMatch.Success || currentType is null) continue;

            var body = bulletMatch.Groups["body"].Value;
            var entry = new DiffEntry(
                Type: currentType,
                Body: body,
                Kind: ClassifyKind(body),
                IsObsolete: HasObsoleteAnnotation(body));

            (current == DiffSection.Breaking ? breaking : additive).Add(entry);
        }

        return new DiffParseResult(breaking, additive, noChanges);
    }

    private static DiffSection ClassifySection(string heading)
    {
        var h = heading.ToLowerInvariant();
        if (h.Contains("breaking")) return DiffSection.Breaking;
        if (h.Contains("additive") || h.Contains("added")) return DiffSection.Additive;
        return DiffSection.None;
    }

    private static DiffEntryKind ClassifyKind(string body)
    {
        // Phrase-based — robust against the exact verb form chosen by upstream.
        if (body.Contains("was added", StringComparison.OrdinalIgnoreCase)) return DiffEntryKind.Added;
        if (body.Contains("was removed", StringComparison.OrdinalIgnoreCase)) return DiffEntryKind.Removed;
        if (body.Contains("signature changed", StringComparison.OrdinalIgnoreCase)) return DiffEntryKind.SignatureChanged;
        return DiffEntryKind.Other;
    }

    private static readonly Regex ObsoleteAnnotationRegex = new(
        @"\[Obsolete(?:Attribute)?\b|\bObsolete\s*[-—]",
        HygieneOptions | RegexOptions.IgnoreCase,
        RegexBudget);

    private static bool HasObsoleteAnnotation(string body) =>
        ObsoleteAnnotationRegex.IsMatch(body);

    // -------------------------------------------------------------------------
    // Output formatting
    // -------------------------------------------------------------------------

    private static string FormatReport(
        string packageId, string oldVer, string newVer, int exitCode,
        DiffParseResult parsed, RegistryService registry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Package diff — {packageId} {oldVer} → {newVer}");
        sb.AppendLine($"dotnet-inspect exit code: {exitCode}");
        sb.AppendLine();

        if (parsed.NoChangesDetected)
        {
            sb.AppendLine("✅ No API changes detected.");
            return sb.ToString();
        }

        var newlyObsolete = parsed.Breaking.Concat(parsed.Additive).Where(e => e.IsObsolete).ToList();
        if (newlyObsolete.Count > 0)
        {
            sb.AppendLine($"### 🆕 Possibly newly obsolete ({newlyObsolete.Count})");
            sb.AppendLine();
            foreach (var e in newlyObsolete)
            {
                sb.AppendLine($"- `{e.Type}` — {e.Body}");
                var match = registry.SearchByApiName(e.Type).FirstOrDefault();
                if (match is not null)
                    sb.AppendLine($"  → registry: **{match.Id}** — {match.FixDescription.Trim()}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"### ❌ Breaking changes ({parsed.Breaking.Count})");
        AppendByType(sb, parsed.Breaking);

        sb.AppendLine($"### ✨ Additive changes ({parsed.Additive.Count})");
        AppendByType(sb, parsed.Additive);

        return sb.ToString();
    }

    private static void AppendByType(StringBuilder sb, IReadOnlyList<DiffEntry> entries)
    {
        if (entries.Count == 0)
        {
            sb.AppendLine("_(none)_");
            sb.AppendLine();
            return;
        }
        foreach (var grp in entries.GroupBy(e => e.Type))
        {
            sb.AppendLine($"**{grp.Key}**");
            foreach (var e in grp)
                sb.AppendLine($"- {e.Body}");
            sb.AppendLine();
        }
    }
}

// -----------------------------------------------------------------------------
// Internal types
// -----------------------------------------------------------------------------

internal enum DiffSection { None, Breaking, Additive }

public enum DiffEntryKind { Other, Added, Removed, SignatureChanged }

public sealed record DiffEntry(
    string Type,
    string Body,
    DiffEntryKind Kind,
    bool IsObsolete);

public sealed record DiffParseResult(
    IReadOnlyList<DiffEntry> Breaking,
    IReadOnlyList<DiffEntry> Additive,
    bool NoChangesDetected);
