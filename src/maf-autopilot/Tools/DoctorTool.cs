using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: MafDoctor
///
/// Single-command repo health letter. Aggregates the results of every other
/// scanner — anti-pattern scan, fan-out validator — and produces an A/B/C/F
/// grade plus the top 3 fixes to address. The demo headline tool.
/// </summary>
[McpServerToolType]
public sealed class DoctorTool
{
    private readonly FanOutValidatorTool _fanOut = new();
    private readonly AntiPatternScannerTool _scanner = new();

    [McpServerTool]
    [Description("""
        Run every MAF anti-pattern scanner and fan-out validator across the repo
        and produce a single A/B/C/F health letter with the top 3 fixes to address.

        Grading:
          A — no errors, no silent-starvation risks, < 5 warnings
          B — no errors, no silent-starvation risks, some warnings/info
          C — 1–3 errors OR 1 silent-starvation risk
          F — 4+ errors OR 2+ silent-starvation risks

        Input:
          - repoPath: absolute path to the repository root.

        Returns a markdown report. Treat as advisory — fix the top items, re-run.
        """)]
    public string MafDoctor(
        [Description("Absolute path to the repository root.")] string repoPath)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err) return err;

        var summary = AnalyzeRepo(repoPath);
        return FormatReport(repoPath, summary);
    }

    // -------------------------------------------------------------------------
    // Pure-function core (testable)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Aggregates per-scanner findings into a single grade. Pure: takes raw
    /// findings lists, no I/O. Supports either the 2-scanner shape (legacy
    /// callers + tests) or the 4-scanner shape (anti-pattern + fan-out + prompt + cost).
    /// </summary>
    public static DoctorSummary Grade(
        IReadOnlyList<AntiPatternFinding> antiPatterns,
        IReadOnlyList<MessageHandlerFinding> handlers,
        IReadOnlyList<PromptFinding>? promptFindings = null,
        IReadOnlyList<CostFinding>? costFindings = null)
    {
        promptFindings ??= [];
        costFindings ??= [];

        var errors = antiPatterns.Count(f => f.Severity == AntiPatternSeverity.Error);
        var warnings = antiPatterns.Count(f => f.Severity == AntiPatternSeverity.Warning);
        var infos = antiPatterns.Count(f => f.Severity == AntiPatternSeverity.Info);
        var starvationRisks = handlers.Count(h =>
            h.Verdict == FanOutVerdict.SilentStarvationRisk
            || h.Verdict == FanOutVerdict.LikelyInvalid);
        var promptErrors = promptFindings.Count(p => p.Severity == PromptSeverity.Error);
        var promptWarnings = promptFindings.Count(p => p.Severity == PromptSeverity.Warning);
        var unboundedCostSites = costFindings.Count(c => c.HasCapWarning);

        // Grade incorporates ALL signals — prompt-injection errors count as full errors.
        var totalErrors = errors + promptErrors;

        char grade;
        string reason;
        if (totalErrors >= 4 || starvationRisks >= 2)
        {
            grade = 'F';
            reason = $"{totalErrors} error(s) + {starvationRisks} silent-starvation risk(s)";
        }
        else if (totalErrors >= 1 || starvationRisks >= 1)
        {
            grade = 'C';
            reason = $"{totalErrors} error(s) + {starvationRisks} starvation risk(s) — fix before shipping";
        }
        else if (warnings + infos + promptWarnings + unboundedCostSites >= 5)
        {
            grade = 'B';
            reason = $"clean of errors; {warnings + promptWarnings} warnings + {unboundedCostSites} unbounded-cost site(s) + {infos} info note(s) to clean up";
        }
        else if (warnings + infos + promptWarnings + unboundedCostSites > 0)
        {
            grade = 'B';
            reason = $"clean of errors, {warnings + infos + promptWarnings + unboundedCostSites} advisor note(s)";
        }
        else
        {
            grade = 'A';
            reason = "no errors, no warnings, no silent-starvation risks, all agent call sites bounded";
        }

        // Top 3 fixes: prioritise silent-starvation risks, then errors (anti-pattern + prompt),
        // then warnings (anti-pattern + prompt + cost).
        var topFixes = handlers
            .Where(h => h.Verdict == FanOutVerdict.SilentStarvationRisk
                     || h.Verdict == FanOutVerdict.LikelyInvalid)
            .Select(h => new DoctorRecommendation(
                Priority: 1,
                Source: "MafValidateFanOut",
                Description: $"`{h.MethodName}` at {h.File}:{h.Line} returns `{h.ReturnType}` — fan-out handler must return Task<T>"))
            .Concat(antiPatterns
                .Where(a => a.Severity == AntiPatternSeverity.Error)
                .Select(a => new DoctorRecommendation(
                    Priority: 2,
                    Source: "MafScanAntiPatterns",
                    Description: $"{a.RuleId} at {a.File}:{a.Line} — {a.RuleName}")))
            .Concat(promptFindings
                .Where(p => p.Severity == PromptSeverity.Error)
                .Select(p => new DoctorRecommendation(
                    Priority: 2,
                    Source: "MafLintAgentPrompt",
                    Description: $"{p.RuleId} at {p.File}:{p.Line} — {p.Message}")))
            .Concat(costFindings
                .Where(c => c.HasCapWarning)
                .Select(c => new DoctorRecommendation(
                    Priority: 3,
                    Source: "MafEstimateCost",
                    Description: $"Unbounded `{c.CallSite}` at {c.File}:{c.Line} — set MaxOutputTokens on the nearest ChatOptions")))
            .Concat(antiPatterns
                .Where(a => a.Severity == AntiPatternSeverity.Warning)
                .Select(a => new DoctorRecommendation(
                    Priority: 4,
                    Source: "MafScanAntiPatterns",
                    Description: $"{a.RuleId} at {a.File}:{a.Line} — {a.RuleName}")))
            .OrderBy(r => r.Priority)
            .Take(3)
            .ToList();

        return new DoctorSummary(
            Grade: grade,
            Reason: reason,
            AntiPatternErrors: errors,
            AntiPatternWarnings: warnings,
            AntiPatternInfos: infos,
            SilentStarvationRisks: starvationRisks,
            HandlersChecked: handlers.Count,
            PromptErrors: promptErrors,
            PromptWarnings: promptWarnings,
            UnboundedCostSites: unboundedCostSites,
            AgentCallSitesChecked: costFindings.Count,
            TopFixes: topFixes);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DoctorSummary AnalyzeRepo(string repoPath)
    {
        var antiPatterns = new List<AntiPatternFinding>();
        var handlers = new List<MessageHandlerFinding>();
        var promptFindings = new List<PromptFinding>();
        var costFindings = new List<CostFinding>();

        foreach (var path in EnumerateScannableFiles(repoPath))
        {
            var source = File.ReadAllText(path);
            var rel = MakeRelative(repoPath, path);
            antiPatterns.AddRange(AntiPatternScannerTool.ScanFile(source, rel));
            handlers.AddRange(FanOutValidatorTool.AnalyzeSource(source, rel));
            promptFindings.AddRange(PromptLintTool.LintSource(source, rel));
            costFindings.AddRange(EstimateCostTool.AnalyzeSource(source, rel));
        }

        return Grade(antiPatterns, handlers, promptFindings, costFindings);
    }

    private static IEnumerable<string> EnumerateScannableFiles(string repoRoot)
        => SourceFileWalker.EnumerateCsFiles(repoRoot);

    private static string MakeRelative(string root, string file)
        => SourceFileWalker.MakeRelative(root, file);

    private static string FormatReport(string repoPath, DoctorSummary s)
    {
        var sb = new StringBuilder();
        var emoji = s.Grade switch
        {
            'A' => "🟢",
            'B' => "🟡",
            'C' => "🟠",
            _ => "🔴",
        };

        sb.AppendLine($"## {emoji} MAF health grade: **{s.Grade}**");
        sb.AppendLine();
        sb.AppendLine($"_{s.Reason}_");
        sb.AppendLine();
        sb.AppendLine($"**Repo:** `{repoPath}`");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|---|---:|");
        sb.AppendLine($"| Anti-pattern errors | {s.AntiPatternErrors} |");
        sb.AppendLine($"| Anti-pattern warnings | {s.AntiPatternWarnings} |");
        sb.AppendLine($"| Anti-pattern info notes | {s.AntiPatternInfos} |");
        sb.AppendLine($"| Silent-starvation risks (fan-out handlers) | {s.SilentStarvationRisks} |");
        sb.AppendLine($"| `[MessageHandler]` methods inspected | {s.HandlersChecked} |");
        sb.AppendLine($"| Prompt-lint errors (injection / etc) | {s.PromptErrors} |");
        sb.AppendLine($"| Prompt-lint warnings (refusals / bloat) | {s.PromptWarnings} |");
        sb.AppendLine($"| Agent call sites without `MaxOutputTokens` cap | {s.UnboundedCostSites} |");
        sb.AppendLine($"| `RunAsync` / `RunStreamingAsync` sites inspected | {s.AgentCallSitesChecked} |");
        sb.AppendLine();

        if (s.TopFixes.Count > 0)
        {
            sb.AppendLine("### Top fixes (ordered by impact)");
            sb.AppendLine();
            var i = 1;
            foreach (var fix in s.TopFixes)
            {
                sb.AppendLine($"{i}. **[{fix.Source}]** {fix.Description}");
                i++;
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("✅ No fixes recommended — repo is clean against all configured rules.");
            sb.AppendLine();
        }

        sb.AppendLine("### Want more detail?");
        sb.AppendLine();
        sb.AppendLine("- Anti-pattern breakdown: `MafScanAntiPatterns(repoPath)`");
        sb.AppendLine("- Fan-out per-handler verdicts: `MafValidateFanOut(repoPath)`");
        sb.AppendLine("- Workflow topology + Mermaid diagram: `MafSimulateWorkflow(repoPath)`");
        sb.AppendLine("- CS0618 compiler-ground-truth: `MafRunCs0618Hunt(projectPath)`");
        sb.AppendLine("- CI-friendly SARIF output: `MafScanAntiPatterns(repoPath, format: \"sarif\")` or `MafValidateFanOut(path, format: \"sarif\")`");

        return sb.ToString();
    }
}

public sealed record DoctorSummary(
    char Grade,
    string Reason,
    int AntiPatternErrors,
    int AntiPatternWarnings,
    int AntiPatternInfos,
    int SilentStarvationRisks,
    int HandlersChecked,
    int PromptErrors,
    int PromptWarnings,
    int UnboundedCostSites,
    int AgentCallSitesChecked,
    IReadOnlyList<DoctorRecommendation> TopFixes);

public sealed record DoctorRecommendation(int Priority, string Source, string Description);
