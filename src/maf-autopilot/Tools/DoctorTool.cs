using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
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
          - format: output format — 'markdown' (default, human-readable) or 'json'
            (machine-readable for CI/dashboards). JSON output follows the schema
            documented in docs/output-schemas.md.

        Returns a markdown report (default) or JSON. Treat as advisory — fix the
        top items, re-run.
        """)]
    public string MafDoctor(
        [Description("Absolute path to the repository root.")] string repoPath,
        [Description("Output format: 'markdown' (default, human-readable) or 'json' (machine-readable for CI/dashboards).")]
        string format = "markdown")
        => RunCore(repoPath, format, excludes: null);

    /// <summary>
    /// CLI entry point. Identical to <see cref="MafDoctor"/> but accepts
    /// repo-relative path substrings to exclude from the scan. Used by the
    /// drift detector to skip <c>samples/</c> and test fixtures so the grade
    /// reflects product code rather than the repo's intentional anti-pattern
    /// bait. Deliberately NOT an MCP tool — keeping it off the tool surface
    /// preserves the stable MafDoctor schema for clients.
    /// </summary>
    internal string Run(string repoPath, string format, IReadOnlyList<string>? excludes)
        => RunCore(repoPath, format, excludes);

    private string RunCore(string repoPath, string format, IReadOnlyList<string>? excludes)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err) return err;

        var summary = AnalyzeRepo(repoPath, excludes);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var result = BuildJsonResult(repoPath, summary);
            return JsonSerializer.Serialize(result, DoctorJsonContext.Default.DoctorJsonResult);
        }

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
                Description: $"`{h.MethodName}` at {h.File}:{h.Line} returns `{h.ReturnType}` — fan-out handler must return Task<T>",
                RuleId: "MAF001",
                File: h.File,
                Line: h.Line,
                Issue: $"`{h.MethodName}` returns `{h.ReturnType}` — fan-out handler must return Task<T>",
                FixDescription: "Change return type to Task<T>, ValueTask<T>, or IAsyncEnumerable<T> so the fan-in barrier receives messages.",
                AutoFixable: false))
            .Concat(antiPatterns
                .Where(a => a.Severity == AntiPatternSeverity.Error)
                .Select(a => new DoctorRecommendation(
                    Priority: 2,
                    Source: "MafScanAntiPatterns",
                    Description: $"{a.RuleId} at {a.File}:{a.Line} — {a.RuleName}",
                    RuleId: a.RuleId,
                    File: a.File,
                    Line: a.Line,
                    Issue: a.RuleName,
                    FixDescription: GetAntiPatternFix(a.RuleId),
                    AutoFixable: IsAutoFixable(a.RuleId))))
            .Concat(promptFindings
                .Where(p => p.Severity == PromptSeverity.Error)
                .Select(p => new DoctorRecommendation(
                    Priority: 2,
                    Source: "MafLintAgentPrompt",
                    Description: $"{p.RuleId} at {p.File}:{p.Line} — {p.Message}",
                    RuleId: p.RuleId,
                    File: p.File,
                    Line: p.Line,
                    Issue: p.Message,
                    FixDescription: GetPromptFix(p.RuleId),
                    AutoFixable: false)))
            .Concat(costFindings
                .Where(c => c.HasCapWarning)
                .Select(c => new DoctorRecommendation(
                    Priority: 3,
                    Source: "MafEstimateCost",
                    Description: $"Unbounded `{c.CallSite}` at {c.File}:{c.Line} — set MaxOutputTokens on the nearest ChatOptions",
                    RuleId: "COST-001",
                    File: c.File,
                    Line: c.Line,
                    Issue: $"Unbounded `{c.CallSite}`",
                    FixDescription: "Set `MaxOutputTokens` on the nearest `ChatOptions` to cap the per-call cost.",
                    AutoFixable: false)))
            .Concat(antiPatterns
                .Where(a => a.Severity == AntiPatternSeverity.Warning)
                .Select(a => new DoctorRecommendation(
                    Priority: 4,
                    Source: "MafScanAntiPatterns",
                    Description: $"{a.RuleId} at {a.File}:{a.Line} — {a.RuleName}",
                    RuleId: a.RuleId,
                    File: a.File,
                    Line: a.Line,
                    Issue: a.RuleName,
                    FixDescription: GetAntiPatternFix(a.RuleId),
                    AutoFixable: IsAutoFixable(a.RuleId))))
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
    // JSON output support
    // -------------------------------------------------------------------------

    private static DoctorJsonResult BuildJsonResult(string repoPath, DoctorSummary s)
    {
        var markdownSummary = FormatReport(repoPath, s);
        var findings = s.TopFixes
            .Select(f => new DoctorJsonFinding(
                RuleId: f.RuleId,
                Severity: f.Priority switch { 1 => "starvation_risk", 2 => "error", _ => "warning" },
                File: f.File,
                Line: f.Line,
                Issue: f.Issue,
                FixDescription: f.FixDescription,
                AutoFixable: f.AutoFixable))
            .ToList();

        return new DoctorJsonResult(
            SchemaVersion: "1",
            Verdict: s.Grade.ToString(),
            ErrorsCount: s.AntiPatternErrors + s.PromptErrors,
            WarningsCount: s.AntiPatternWarnings + s.PromptWarnings,
            SilentStarvationRisks: s.SilentStarvationRisks,
            TopFixes: findings,
            SummaryMd: markdownSummary);
    }

    /// <summary>True if MafAutoFix knows how to apply a deterministic rewriter for this rule.</summary>
    private static bool IsAutoFixable(string ruleId) => ruleId switch
    {
        "MAF-AP-SEC-001" => true,    // DefaultAzureCredential → ManagedIdentityCredential
        "MAF002" => true,            // analyzer-aligned alias
        "MAF-AP-SEC-003" => true,    // EnableSensitiveData = true → removed
        "MAF003" => true,            // analyzer-aligned alias
        "MAF-AP-WF-001" => true,     // sealed modifier on Executor
        "MAF130-FAN-IN-001" => true, // fan-in argument order swap
        "MAF-AP-CONC-002" => true,   // .Result / .Wait() → await
        _ => false,
    };

    /// <summary>One-line actionable fix description per anti-pattern rule ID.</summary>
    private static string GetAntiPatternFix(string ruleId) => ruleId switch
    {
        "MAF-AP-SEC-001" => "Replace `DefaultAzureCredential` with `ManagedIdentityCredential` in production code.",
        "MAF-AP-SEC-003" => "Remove `EnableSensitiveData = true` from non-dev configurations.",
        "MAF-AP-WF-001" => "Add `sealed` modifier to the Executor class.",
        "MAF130-FAN-IN-001" => "Swap `AddFanInBarrierEdge` argument order — sources first, target second.",
        "MAF-AP-CONC-002" => "Replace `.Result` / `.Wait()` with `await`.",
        "MAF-AP-OBS-001" => "Wire `UseOpenTelemetry` on the IChatClient pipeline.",
        "MAF-AP-IDN-001" => "Use `ManagedIdentityCredential` instead of secret-based auth.",
        "MAF-AP-CONC-001" => "Move session state out of `AIContextProvider` instance fields into `ProviderSessionState<T>`.",
        "MAF-AP-AGENT-001" => "Move `Instructions` inside `ChatClientAgentOptions.ChatOptions` (top-level placement is silently ignored).",
        _ => "See MafRegistryLookup for the canonical fix for this rule.",
    };

    /// <summary>One-line actionable fix description per prompt-lint rule ID.</summary>
    private static string GetPromptFix(string ruleId) => ruleId switch
    {
        "PROMPT-001" => "Fill in a meaningful Instructions string, or remove the empty assignment.",
        "PROMPT-002" => "Trim the Instructions literal — over ~2000 tokens of system prompt is bloat.",
        "PROMPT-003" => "Add a refusal clause to the Instructions (\"do not...\", \"refuse if...\").",
        "PROMPT-004" => "Stop concatenating untrusted input into Instructions — use ChatOptions or function tool args instead.",
        _ => "See MafRegistryLookup for the canonical fix.",
    };

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DoctorSummary AnalyzeRepo(string repoPath, IReadOnlyList<string>? excludes)
    {
        var antiPatterns = new List<AntiPatternFinding>();
        var handlers = new List<MessageHandlerFinding>();
        var promptFindings = new List<PromptFinding>();
        var costFindings = new List<CostFinding>();

        foreach (var path in EnumerateScannableFiles(repoPath, excludes))
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

    private static IEnumerable<string> EnumerateScannableFiles(string repoRoot, IReadOnlyList<string>? excludes)
        => SourceFileWalker.EnumerateCsFiles(repoRoot, excludes);

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

public sealed record DoctorRecommendation(
    int Priority,
    string Source,
    string Description,    // for markdown output (keep existing)
    string RuleId,         // NEW — for JSON
    string File,           // NEW — for JSON
    int Line,              // NEW — for JSON
    string Issue,          // NEW — short title for JSON
    string FixDescription, // NEW — actionable fix string for JSON
    bool AutoFixable);     // NEW — whether MafAutoFix can handle this

// -------------------------------------------------------------------------
// JSON output schema types (format: "json")
// -------------------------------------------------------------------------

/// <summary>
/// JSON output schema for MafDoctor (format: "json").
/// Schema version "1" — stable within 1.x. See docs/output-schemas.md.
/// </summary>
public sealed record DoctorJsonResult(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("errors_count")] int ErrorsCount,
    [property: JsonPropertyName("warnings_count")] int WarningsCount,
    [property: JsonPropertyName("silent_starvation_risks")] int SilentStarvationRisks,
    [property: JsonPropertyName("top_fixes")] IReadOnlyList<DoctorJsonFinding> TopFixes,
    [property: JsonPropertyName("summary_md")] string SummaryMd);

public sealed record DoctorJsonFinding(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("issue")] string Issue,
    [property: JsonPropertyName("fix_description")] string FixDescription,
    [property: JsonPropertyName("auto_fixable")] bool AutoFixable);

[JsonSerializable(typeof(DoctorJsonResult))]
[JsonSerializable(typeof(DoctorJsonFinding))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class DoctorJsonContext : JsonSerializerContext { }
