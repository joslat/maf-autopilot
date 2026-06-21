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
/// version range — so 1.0.0 → 1.3.0 migrations get every intermediate step
/// surfaced in one pass instead of being treated as a 1.2 → 1.3 hop only.
/// </summary>
[McpServerToolType]
public sealed class MigrationPathTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Build an ordered, version-keyed migration plan for a specific version range.

        Walks every section of the embedded MAF migration guide, reads its
        `applies-to: A.B.x → C.D.x` metadata, and selects sections whose range
        overlaps the user's requested `currentVersion → targetVersion`. Returns
        them in introduced-version order — the canonical step sequence.

        Input:
          - currentVersion: the MAF version your code is on today (e.g. "1.0.0").
          - targetVersion: the MAF version you want to reach (e.g. "1.3.0").

        Returns a markdown report listing each matching section's title,
        introduced version, applies-to range, and section path so the migration
        agent can load them in order.
        """)]
    public string MafMigrationPath(
        [Description("Source MAF version, e.g. 1.0.0")] string currentVersion,
        [Description("Target MAF version, e.g. 1.3.0")] string targetVersion)
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
    /// somewhere within the next ~6 lines of body text.
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

            // Search the next 6 lines for a metadata comment — but STOP if we
            // hit another heading, so we don't accidentally attribute a later
            // section's metadata to this one.
            string? metadataLine = null;
            for (var j = i + 1; j < Math.Min(i + 7, lines.Length); j++)
            {
                if (Regex.IsMatch(lines[j], @"^#{1,3}\s"))
                    break;
                if (lines[j].Contains("applies-to:", StringComparison.Ordinal))
                {
                    metadataLine = lines[j];
                    break;
                }
            }
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
    /// Returns every section whose [AppliesFrom, AppliesTo] range overlaps the
    /// user's [fromVer, toVer] migration path, ordered by introduced version.
    /// </summary>
    public static IReadOnlyList<GuideSection> SelectApplicable(
        IReadOnlyList<GuideSection> sections,
        SemVer fromVer,
        SemVer toVer)
    {
        return sections
            .Where(s => RangesOverlap(s.AppliesFrom, s.AppliesTo, fromVer, toVer))
            .OrderBy(s => s.IntroducedAt)
            .ToList();
    }

    private static bool RangesOverlap(SemVer aLow, SemVer aHigh, SemVer bLow, SemVer bHigh)
        => aLow <= bHigh && bLow <= aHigh;

    /// <summary>
    /// Parses metadata of the form:
    /// <c>&lt;!-- introduced: 1.3.0 | applies-to: 1.0.x → 1.3.x | deprecated-in: ... --&gt;</c>
    /// Returns null if the comment doesn't have the expected shape.
    /// </summary>
    internal static (SemVer Introduced, SemVer AppliesFrom, SemVer AppliesTo)? ParseMetadata(string line)
    {
        var intro = Regex.Match(line, @"introduced:\s*([0-9]+\.[0-9]+\.[0-9]+)");
        var applies = Regex.Match(line, @"applies-to:\s*([0-9]+\.[0-9]+)\.x\s*(?:→|->)\s*([0-9]+\.[0-9]+)\.x");
        if (!intro.Success || !applies.Success) return null;

        if (!TryParseVersion(intro.Groups[1].Value, out var introVer)) return null;
        if (!TryParseVersion(applies.Groups[1].Value + ".0", out var fromVer)) return null;
        if (!TryParseVersion(applies.Groups[2].Value + ".0", out var toVer)) return null;

        return (introVer, fromVer, toVer);
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
            sb.AppendLine($"| {i} | `{s.Title}` (guide line {s.LineNumber}) | `{s.IntroducedAt}` | `{s.AppliesFrom.Major}.{s.AppliesFrom.Minor}.x → {s.AppliesTo.Major}.{s.AppliesTo.Minor}.x` |");
            i++;
        }

        sb.AppendLine();
        sb.AppendLine("### How to use this");
        sb.AppendLine("Hand this list to `@maf-auditor` — it will load each section in order via the `maf-migration-guide` skill and generate a single consolidated migration plan covering every intermediate step.");
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
