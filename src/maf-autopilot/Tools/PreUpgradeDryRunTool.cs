using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using MafDoctor.Data;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafPreUpgradeDryRun
///
/// "What would break if I upgraded?" — given a current MAF version and a target,
/// diffs the two NuGet versions via <c>dotnet-inspect</c>, then grep-scans the
/// user's codebase for references to every breaking symbol. The output ranks
/// findings by THIS codebase's actual exposure: files that touch the changed API
/// are surfaced first; pure additive changes are noted but de-emphasised.
///
/// Lets users preview damage BEFORE modifying any code — pairs with the
/// release-watcher auto-PR so reviewers can quickly see "yes / no this affects me".
/// </summary>
[McpServerToolType]
public sealed class PreUpgradeDryRunTool
{
    private readonly RegistryService _registry;

    public PreUpgradeDryRunTool(RegistryService registry)
    {
        _registry = registry;
    }

    // Annotation rationale: OpenWorld = true because dotnet-inspect reaches
    // api.nuget.org to download the two MAF NuGet versions being diffed.
    // ReadOnly = true is correct — only emits a report; the repo scan is
    // grep-only, no writes.
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = true)]
    [Description("""
        Preview the impact of a MAF upgrade WITHOUT modifying any code.

        Diffs `currentVersion` → `targetVersion` via dotnet-inspect, extracts every
        breaking-change symbol, then grep-scans the repo for references to each.
        Files that touch changed surface are surfaced first; pure additive
        changes are noted but de-emphasised.

        Input:
          - repoPath: absolute path to the repository root.
          - packageId: NuGet package to diff (e.g. `Microsoft.Agents.AI`).
          - currentVersion: the version your code is on today.
          - targetVersion: the version you're considering upgrading to.

        Returns a markdown report with three sections: 🔴 high-impact (you ARE
        affected), 🟡 unused-but-breaking (you can ignore), 🟢 additive (safe).

        Note: requires `dotnet-inspect` on PATH. If it's missing, this returns
        install instructions rather than downloading it automatically — set
        MAF_DOCTOR_ALLOW_TOOL_DOWNLOAD=1 in the MCP server's env config to allow
        on-demand fetch via `dnx` instead.
        """)]
    public string MafPreUpgradeDryRun(
        [Description("Absolute path to the repository root.")] string repoPath,
        [Description("NuGet package id, e.g. Microsoft.Agents.AI")] string packageId,
        [Description("Current version your code references, e.g. 1.2.0")] string currentVersion,
        [Description("Proposed target version, e.g. 1.3.0")] string targetVersion)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } pathErr) return pathErr;
        if (!DiffPackageTool.IsValidPackageId(packageId))
            return $"Error: packageId '{packageId}' is not a valid package identifier.";
        if (!DiffPackageTool.IsValidVersion(currentVersion) || !DiffPackageTool.IsValidVersion(targetVersion))
            return "Error: currentVersion and targetVersion must be valid SemVer-ish strings.";

        var (exitCode, output) = ProcessRunner.RunDotnetInspectDiff(packageId, currentVersion, targetVersion);
        var parsed = DiffPackageTool.ParseDiffOutput(output);

        var sources = LoadSources(repoPath);
        var assessment = AssessImpact(parsed, sources, _registry);

        return FormatReport(repoPath, packageId, currentVersion, targetVersion, exitCode, assessment);
    }

    // -------------------------------------------------------------------------
    // Pure-function core (testable)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given a parsed diff and the repo's source-file map, classifies every
    /// breaking entry as "you ARE affected" (high), "unused-but-breaking"
    /// (medium), or rolls additive entries into the low bucket. Pure: no I/O.
    /// </summary>
    public static UpgradeAssessment AssessImpact(
        DiffParseResult parsed,
        IReadOnlyList<(string File, string Source)> sources,
        RegistryService? registry = null)
    {
        var high = new List<UpgradeFinding>();
        var medium = new List<UpgradeFinding>();
        var additive = parsed.Additive.Count;

        foreach (var entry in parsed.Breaking)
        {
            var symbols = ExtractSymbols(entry).ToList();
            var hits = new List<(string File, int Line)>();
            foreach (var symbol in symbols)
            foreach (var (file, source) in sources)
            {
                foreach (var lineMatch in FindLines(source, symbol))
                    hits.Add((file, lineMatch));
            }

            var registryHint = registry?.SearchByApiName(entry.Type).FirstOrDefault();

            if (hits.Count > 0)
            {
                high.Add(new UpgradeFinding(
                    Type: entry.Type,
                    Body: entry.Body,
                    Kind: entry.Kind,
                    Hits: hits.DistinctBy(h => (h.File, h.Line)).ToList(),
                    RegistryEntryId: registryHint?.Id));
            }
            else
            {
                medium.Add(new UpgradeFinding(
                    Type: entry.Type,
                    Body: entry.Body,
                    Kind: entry.Kind,
                    Hits: [],
                    RegistryEntryId: registryHint?.Id));
            }
        }

        return new UpgradeAssessment(
            HighImpact: high,
            UnusedBreaking: medium,
            AdditiveCount: additive,
            NoChangesDetected: parsed.NoChangesDetected);
    }

    /// <summary>
    /// Extracts likely-grep-able identifier tokens from a diff entry. Currently:
    /// the Type name + any quoted symbol name in the body (e.g. `'AddResource'`).
    /// </summary>
    internal static IEnumerable<string> ExtractSymbols(DiffEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Type))
            yield return entry.Type;

        foreach (Match m in Regex.Matches(entry.Body, @"'([A-Za-z_][A-Za-z0-9_]*)'"))
            yield return m.Groups[1].Value;
    }

    private static IEnumerable<int> FindLines(string source, string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) yield break;
        // Phase 5.9 — ReDoS hygiene. Even though the pattern is bounded
        // (Regex.Escape on an attacker-controlled `symbol` plus literal \b
        // anchors), NonBacktracking + 2s timeout cap any future drift
        // (e.g. someone changing `Regex.Escape` to direct interpolation).
        var pattern = new Regex(
            $@"\b{Regex.Escape(symbol)}\b",
            RegexOptions.Compiled | RegexOptions.NonBacktracking,
            TimeSpan.FromSeconds(2));
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            if (pattern.IsMatch(lines[i])) yield return i + 1;
    }

    private static IReadOnlyList<(string File, string Source)> LoadSources(string repoPath)
    {
        var list = new List<(string, string)>();
        foreach (var path in SourceFileWalker.EnumerateCsFiles(repoPath))
        {
            try
            {
                list.Add((SourceFileWalker.MakeRelative(repoPath, path), File.ReadAllText(path)));
            }
            catch
            {
                // Skip unreadable files (binary, locked, permissions).
            }
        }
        return list;
    }

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    private static string FormatReport(
        string repoPath, string packageId, string currentVersion, string targetVersion,
        int exitCode, UpgradeAssessment a)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 🧭 MAF pre-upgrade dry-run — `{packageId}` {currentVersion} → {targetVersion}");
        sb.AppendLine();
        sb.AppendLine($"**Repo:** `{repoPath}`");
        sb.AppendLine($"_dotnet-inspect exit code: {exitCode}_");
        sb.AppendLine();

        if (a.NoChangesDetected)
        {
            sb.AppendLine("✅ No API changes detected between these versions. Upgrade is mechanical.");
            return sb.ToString();
        }

        sb.AppendLine($"| Bucket | Count |");
        sb.AppendLine($"|---|---:|");
        sb.AppendLine($"| 🔴 You ARE affected | {a.HighImpact.Count} |");
        sb.AppendLine($"| 🟡 Breaking but unused | {a.UnusedBreaking.Count} |");
        sb.AppendLine($"| 🟢 Additive (informational) | {a.AdditiveCount} |");
        sb.AppendLine();

        if (a.HighImpact.Count > 0)
        {
            sb.AppendLine("### 🔴 You ARE affected (must fix before upgrade)");
            sb.AppendLine();
            foreach (var f in a.HighImpact)
            {
                sb.AppendLine($"#### `{f.Type}` — {f.Kind}");
                sb.AppendLine();
                sb.AppendLine($"_{f.Body}_");
                if (f.RegistryEntryId is not null)
                    sb.AppendLine($"➤ Canonical fix: registry entry **{f.RegistryEntryId}** (call `MafRegistryLookup` for details).");
                sb.AppendLine();
                sb.AppendLine("| File | Line |");
                sb.AppendLine("|---|---:|");
                foreach (var (file, line) in f.Hits.Take(10))
                    sb.AppendLine($"| `{file}` | {line} |");
                if (f.Hits.Count > 10)
                    sb.AppendLine($"| _… and {f.Hits.Count - 10} more references_ | |");
                sb.AppendLine();
            }
        }

        if (a.UnusedBreaking.Count > 0)
        {
            sb.AppendLine("### 🟡 Breaking but unused — safe to ignore");
            sb.AppendLine();
            sb.AppendLine("These APIs changed but your codebase doesn't reference them today. No action needed for the upgrade itself.");
            sb.AppendLine();
            foreach (var f in a.UnusedBreaking.Take(20))
                sb.AppendLine($"- `{f.Type}` — {f.Body.Substring(0, Math.Min(80, f.Body.Length))}");
            if (a.UnusedBreaking.Count > 20)
                sb.AppendLine($"- _… and {a.UnusedBreaking.Count - 20} more_");
            sb.AppendLine();
        }

        sb.AppendLine($"### 🟢 Additive changes: {a.AdditiveCount}");
        sb.AppendLine();
        sb.AppendLine("These are new APIs in the target version — informational only, no migration risk.");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine("_When you're ready to actually upgrade, hand this report to `@maf-auditor` to generate a migration plan, then `@maf-migration` to execute it._");
        return sb.ToString();
    }
}

// -----------------------------------------------------------------------------
// Models
// -----------------------------------------------------------------------------

public sealed record UpgradeFinding(
    string Type,
    string Body,
    DiffEntryKind Kind,
    IReadOnlyList<(string File, int Line)> Hits,
    string? RegistryEntryId);

public sealed record UpgradeAssessment(
    IReadOnlyList<UpgradeFinding> HighImpact,
    IReadOnlyList<UpgradeFinding> UnusedBreaking,
    int AdditiveCount,
    bool NoChangesDetected);
