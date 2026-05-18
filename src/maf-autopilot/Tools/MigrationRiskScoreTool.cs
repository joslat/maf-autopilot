using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: <c>MafScoreMigrationRisk</c>.
///
/// **Pre-migration risk verdict.** Sets expectations BEFORE a migration
/// starts so the maintainer doesn't sleep-walk into a 2-day rewrite
/// thinking they're 30 minutes from green.
///
/// **How the score is computed.** A weighted count of detected findings:
///
/// <code>
///   silent_starvation_risks × 3   ← runtime-only bugs, hardest to fix
///   cs0246_findings         × 2   ← removed types; structural rewrites
///   cs0117_findings         × 2   ← removed members; structural rewrites
///   cs0618_findings         × 1   ← obsolete-but-still-there
///   anti_pattern_errors     × 1   ← config / pattern fixes
/// </code>
///
/// Thresholds:
/// <list type="bullet">
///   <item><c>score &lt; 5</c>  → <b>EASY</b> (1-2 hr migration; low risk)</item>
///   <item><c>5 ≤ score &lt; 15</c> → <b>MEDIUM</b> (½ day; watch tests carefully)</item>
///   <item><c>score ≥ 15</c> → <b>HARD</b> (1+ day; expect rework, plan for rollback)</item>
/// </list>
///
/// The weights are deliberately conservative — calibration against more
/// real-world migrations is a Phase V+ refinement. Each verdict ships
/// with the per-category breakdown so the maintainer can sanity-check.
///
/// Phase W.11 (2026-05-13).
/// </summary>
[McpServerToolType]
public sealed class MigrationRiskScoreTool
{
    /// <summary>The score below which a codebase is rated EASY.</summary>
    internal const int EasyThreshold = 5;

    /// <summary>The score at which a codebase becomes HARD.</summary>
    internal const int HardThreshold = 15;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Score the migration-risk of a codebase BEFORE migration starts.
        Returns HARD / MEDIUM / EASY plus the per-category breakdown that
        produced the score.

        Input:
          - repoPath: absolute path to the codebase.

        Returns a JSON document:
          {
            "verdict": "MEDIUM",
            "score": 12,
            "thresholds": { "easyBelow": 5, "hardAtOrAbove": 15 },
            "breakdown": {
              "antiPatternErrors": 6,
              "antiPatternErrorsWeight": 1,
              "silentStarvationRisks": 2,
              "silentStarvationRisksWeight": 3,
              ...
            },
            "recommendation": "..."
          }
        """)]
    public string MafScoreMigrationRisk(
        [Description("Absolute path to the repo or project to score.")] string repoPath)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err)
            return JsonSerializer.Serialize(new { error = err });

        // Run the existing scanners over every .cs file — same composition
        // pattern DoctorTool uses (per-file ScanFile / AnalyzeSource).
        var antiPatternFindings = new List<AntiPatternFinding>();
        var handlers = new List<MessageHandlerFinding>();
        foreach (var file in SourceFileWalker.EnumerateCsFiles(repoPath))
        {
            var source = File.ReadAllText(file);
            var rel = SourceFileWalker.MakeRelative(repoPath, file);
            antiPatternFindings.AddRange(AntiPatternScannerTool.ScanFile(source, rel));
            handlers.AddRange(FanOutValidatorTool.AnalyzeSource(source, rel));
        }

        var antiPatternErrors = antiPatternFindings.Count(f => f.Severity == AntiPatternSeverity.Error);
        var silentStarvation = handlers.Count(h =>
            h.Verdict == FanOutVerdict.SilentStarvationRisk
            || h.Verdict == FanOutVerdict.LikelyInvalid);

        // CS warning counts via the registry. SearchByApiName ⇒ string match
        // on the source text isn't a build-perfect signal, but it's a
        // reasonable proxy for "how many obsolete-API touches does the
        // codebase have" without paying for a full dotnet build.
        var cs0246Count = CountSourceMatches(repoPath, "AgentThread", "MapA2A", "InProcessExecution", "AgentRunUpdateEvent");
        var cs0618Count = CountSourceMatches(repoPath, "AddFanInBarrierEdge(",  /* one of the known [Obsolete] surfaces */
                                              "SerializeSession(");
        var cs0117Count = CountSourceMatches(repoPath, ".Instructions");

        var score = (silentStarvation * 3)
                  + (cs0246Count * 2)
                  + (cs0117Count * 2)
                  + (cs0618Count * 1)
                  + (antiPatternErrors * 1);

        var verdict = score < EasyThreshold ? "EASY"
                    : score < HardThreshold ? "MEDIUM"
                                            : "HARD";

        var recommendation = verdict switch
        {
            "EASY"   => "1-2 hour migration. Apply MafAutoFix for each finding; one round of dotnet build should be green.",
            "MEDIUM" => "Half-day migration. Migrate in order: silent-starvation first (highest behavioural risk), then CS0246 (structural removals), then anti-patterns. Watch the test suite carefully after each batch.",
            "HARD"   => "1+ day migration. Plan for rework. Prepare a rollback branch BEFORE starting (@maf-rollback). Migrate file-by-file with `dotnet build` gates between each. Consider running the workshop on a parallel branch to validate the approach.",
            _ => "(unknown verdict)",
        };

        return JsonSerializer.Serialize(new
        {
            verdict,
            score,
            thresholds = new
            {
                easyBelow = EasyThreshold,
                hardAtOrAbove = HardThreshold,
            },
            breakdown = new
            {
                antiPatternErrors,
                antiPatternErrorsWeight = 1,
                silentStarvationRisks = silentStarvation,
                silentStarvationRisksWeight = 3,
                cs0246Count,
                cs0246Weight = 2,
                cs0117Count,
                cs0117Weight = 2,
                cs0618Count,
                cs0618Weight = 1,
            },
            recommendation,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Count source-text occurrences of any of the given needles across
    /// all .cs files in <paramref name="repoPath"/>. A needle appearing
    /// multiple times in a single file counts once per occurrence (which
    /// matches how the compiler would emit warnings).
    /// </summary>
    internal static int CountSourceMatches(string repoPath, params string[] needles)
    {
        var count = 0;
        foreach (var file in SourceFileWalker.EnumerateCsFiles(repoPath))
        {
            var src = File.ReadAllText(file);
            foreach (var needle in needles)
                count += CountOccurrences(src, needle);
        }
        return count;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
