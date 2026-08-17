using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using MafDoctor.Resources;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafMigrationPath
///
/// Walks the embedded MAF migration guide's version-keyed section metadata
/// (the <c>&lt;!-- introduced: X.Y.Z | applies-to: A.B.x → C.D.x --&gt;</c> comments)
/// and emits an ordered list of sections that apply to a user's specific
/// version range — so 1.13.0 → 1.17.0 migrations get every intermediate step
/// surfaced in one pass instead of being treated as a direct hop only.
/// </summary>
[McpServerToolType]
public sealed class MigrationPathTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Build an ordered, version-keyed migration plan for a specific version range.

        Walks every section of the embedded MAF migration guide, reads its
        `applies-to: A.B.x → C.D.x` metadata, and selects sections introduced
        after `currentVersion` and no later than `targetVersion`. Returns them in
        introduced-version order — the canonical step sequence.

        Input:
          - currentVersion: the MAF version your code is on today (e.g. "1.0.0").
          - targetVersion: the MAF version you want to reach (e.g. "1.17.0").

        Returns a markdown report listing each matching section's title,
        introduced version, applies-to range, and section path so the migration
        agent can load them in order.
        """)]
    public string MafMigrationPath(
        [Description("Source MAF version, e.g. 1.0.0")] string currentVersion,
        [Description("Target MAF version, e.g. 1.17.0")] string targetVersion)
    {
        if (!TryParseVersion(currentVersion, out var fromVer))
            return $"Error: '{currentVersion}' is not a valid SemVer.";
        if (!TryParseVersion(targetVersion, out var toVer))
            return $"Error: '{targetVersion}' is not a valid SemVer.";
        if (fromVer >= toVer)
            return $"Error: currentVersion ({currentVersion}) must be strictly less than targetVersion ({targetVersion}).";

        var guide = MafResources.GetGuide();
        var sections = ExtractSections(guide);
        var applicable = SelectApplicable(sections, fromVer, toVer);

        return FormatReport(currentVersion, targetVersion, applicable);
    }

    // -------------------------------------------------------------------------
    // Pure parsing core (testable without DI)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses every heading + version-metadata comment from the migration guide.
    /// Returns one entry per heading that has an `applies-to:` metadata comment
    /// immediately before it (the original 1.3 guide shape) or near the start of
    /// its body (the generated per-version guide shape).
    /// </summary>
    public static IReadOnlyList<GuideSection> ExtractSections(string guideMarkdown)
    {
        var sections = new List<GuideSection>();
        if (string.IsNullOrEmpty(guideMarkdown)) return sections;

        var lines = guideMarkdown.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var headingMatch = Regex.Match(line, @"^(#{1,3})\s+(?<title>.+?)\s*$");
            if (!headingMatch.Success) continue;

            var metadataLine = FindMetadataForHeading(lines, i);
            if (metadataLine is null) continue;

            var meta = ParseMetadata(metadataLine);
            if (meta is null) continue;

            sections.Add(new GuideSection(
                Title: headingMatch.Groups["title"].Value,
                LineNumber: i + 1,
                IntroducedAt: meta.Value.Introduced,
                AppliesFrom: meta.Value.AppliesFrom,
                AppliesTo: meta.Value.AppliesTo));
        }
        return sections;
    }

    /// <summary>
    /// Associates metadata only when it is adjacent to the heading boundary.
    /// This supports both checked-in guide conventions without stealing the
    /// metadata that precedes a later heading in a short section.
    /// </summary>
    private static string? FindMetadataForHeading(string[] lines, int headingIndex)
    {
        // The hand-authored 1.3 guide puts metadata immediately before each
        // section heading. Permit blank lines and a horizontal rule between the
        // two, but stop at body text or another heading.
        for (var j = headingIndex - 1; j >= Math.Max(headingIndex - 6, 0); j--)
        {
            var candidate = lines[j].Trim();
            if (candidate.Contains("applies-to:", StringComparison.Ordinal))
                return candidate;
            if (candidate.Length == 0 || candidate == "---")
                continue;
            break;
        }

        // Generated per-version guides put metadata at the beginning of the
        // heading body. Stop at the first real body line or following heading so
        // a later section's leading metadata cannot be attributed here.
        for (var j = headingIndex + 1; j < Math.Min(headingIndex + 7, lines.Length); j++)
        {
            var candidate = lines[j].Trim();
            if (Regex.IsMatch(candidate, @"^#{1,3}\s"))
                break;
            if (candidate.Contains("applies-to:", StringComparison.Ordinal))
            {
                // A metadata comment immediately followed by another heading
                // belongs to that following section (the original 1.3 shape),
                // not to the current heading whose body happened to be short.
                if (MetadataPrecedesAnotherHeading(lines, j))
                    break;
                return candidate;
            }
            if (candidate.Length == 0 || candidate == "---")
                continue;
            break;
        }

        return null;
    }

    private static bool MetadataPrecedesAnotherHeading(string[] lines, int metadataIndex)
    {
        for (var j = metadataIndex + 1; j < lines.Length; j++)
        {
            var candidate = lines[j].Trim();
            if (candidate.Length == 0 || candidate == "---")
                continue;
            return Regex.IsMatch(candidate, @"^#{1,3}\s");
        }

        return false;
    }

    /// <summary>
    /// Returns every section introduced strictly after the installed source
    /// version and no later than the target. The metadata range must also overlap
    /// the requested path. This makes the source boundary exclusive: a 1.13 →
    /// 1.17 plan starts with the 1.14 step, not the already-applied 1.13 step.
    /// </summary>
    public static IReadOnlyList<GuideSection> SelectApplicable(
        IReadOnlyList<GuideSection> sections,
        SemVer fromVer,
        SemVer toVer)
    {
        return sections
            .Where(s => s.IntroducedAt > fromVer
                && s.IntroducedAt <= toVer
                && RangesOverlap(s.AppliesFrom, s.AppliesTo, fromVer, toVer))
            .OrderBy(s => s.IntroducedAt)
            .ToList();
    }

    private static bool RangesOverlap(SemVer aLow, SemVer aHigh, SemVer bLow, SemVer bHigh)
        => aLow <= bHigh && bLow <= aHigh;

    /// <summary>
    /// Parses metadata of the form:
    /// <c>&lt;!-- introduced: 1.17.0 | applies-to: 1.16.0.x → 1.17.0.x | deprecated-in: ... --&gt;</c>
    /// Legacy two-component wildcards such as <c>1.2.x</c> are also accepted.
    /// Returns null if the comment doesn't have the expected shape.
    /// </summary>
    internal static (SemVer Introduced, SemVer AppliesFrom, SemVer AppliesTo)? ParseMetadata(string line)
    {
        var intro = Regex.Match(line, @"introduced:\s*([0-9]+\.[0-9]+\.[0-9]+)");
        var applies = Regex.Match(
            line,
            @"applies-to:\s*([0-9]+\.[0-9]+(?:\.[0-9]+)?)\.x\s*(?:→|->)\s*([0-9]+\.[0-9]+(?:\.[0-9]+)?)\.x");
        if (!intro.Success || !applies.Success) return null;

        if (!TryParseVersion(intro.Groups[1].Value, out var introVer)) return null;
        if (!TryParseWildcardBase(applies.Groups[1].Value, out var fromVer)) return null;
        if (!TryParseWildcardBase(applies.Groups[2].Value, out var toVer)) return null;

        return (introVer, fromVer, toVer);
    }

    private static bool TryParseWildcardBase(string value, out SemVer version)
    {
        var dotCount = value.Count(c => c == '.');
        return TryParseVersion(dotCount == 1 ? value + ".0" : value, out version);
    }

    internal static bool TryParseVersion(string s, out SemVer version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var match = Regex.Match(s, @"^(?<maj>\d+)\.(?<min>\d+)\.(?<patch>\d+)(?:-.*)?$");
        if (!match.Success) return false;
        version = new SemVer(
            int.Parse(match.Groups["maj"].Value),
            int.Parse(match.Groups["min"].Value),
            int.Parse(match.Groups["patch"].Value));
        return true;
    }

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    private static string FormatReport(string current, string target, IReadOnlyList<GuideSection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 🛤️ MAF migration path — `{current}` → `{target}`");
        sb.AppendLine();

        if (sections.Count == 0)
        {
            sb.AppendLine("ℹ️ No version-keyed guide sections apply to this version range. Either the migration is a no-op, or the guide doesn't yet carry metadata for these versions.");
            return sb.ToString();
        }

        sb.AppendLine($"**{sections.Count}** guide section(s) apply, in order of introduced version:");
        sb.AppendLine();
        sb.AppendLine("| # | Section | Introduced | Applies to |");
        sb.AppendLine("|---:|---|---|---|");
        var i = 1;
        foreach (var s in sections)
        {
            sb.AppendLine($"| {i} | `{s.Title}` (guide line {s.LineNumber}) | `{s.IntroducedAt}` | `{s.AppliesFrom}.x → {s.AppliesTo}.x` |");
            i++;
        }

        sb.AppendLine();
        sb.AppendLine("### How to use this");
        sb.AppendLine("Use this ordered list with the `maf-audit` MCP prompt to generate one consolidated migration plan covering every intermediate step. If the optional Copilot specialist agents are installed, `@maf-auditor` provides the equivalent guided flow.");
        return sb.ToString();
    }
}

// -----------------------------------------------------------------------------
// Models
// -----------------------------------------------------------------------------

public readonly record struct SemVer(int Major, int Minor, int Patch) : IComparable<SemVer>
{
    public int CompareTo(SemVer other)
    {
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        return Patch.CompareTo(other.Patch);
    }

    public static bool operator <(SemVer a, SemVer b) => a.CompareTo(b) < 0;
    public static bool operator >(SemVer a, SemVer b) => a.CompareTo(b) > 0;
    public static bool operator <=(SemVer a, SemVer b) => a.CompareTo(b) <= 0;
    public static bool operator >=(SemVer a, SemVer b) => a.CompareTo(b) >= 0;
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public sealed record GuideSection(
    string Title,
    int LineNumber,
    SemVer IntroducedAt,
    SemVer AppliesFrom,
    SemVer AppliesTo);
