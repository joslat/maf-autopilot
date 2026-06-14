namespace MafAutopilot.Tools;

/// <summary>
/// SARIF v2.1.0 emitter wrappers around the existing finding lists.
///
/// 2026-05-12 (Phase F.2, post-MCP-review): the two former MCP tools
/// `MafScanAntiPatternsSarif` and `MafValidateFanOutSarif` were removed from
/// the public tool surface. SARIF output is now reached by passing
/// <c>format: "sarif"</c> to <c>MafScanAntiPatterns</c> / <c>MafValidateFanOut</c>.
/// This class survives as the internal emitter shared by both tools.
/// </summary>
internal static class SarifExportTool
{
    private const string ToolVersion = "1.3.0-alpha-3";

    /// <summary>
    /// Builds a SARIF v2.1.0 document from a list of anti-pattern findings.
    /// </summary>
    public static string EmitAntiPatternsSarif(IReadOnlyList<AntiPatternFinding> findings)
    {
        var sarifFindings = findings.Select(f => new SarifFinding(
            RuleId: f.RuleId,
            Severity: MapSeverity(f.Severity),
            Message: f.RuleName + " — " + f.Match,
            File: f.File,
            Line: f.Line));

        return SarifEmitter.Emit(ToolVersion, sarifFindings, BuildAntiPatternRuleCatalog());
    }

    /// <summary>
    /// Builds a SARIF v2.1.0 document from fan-out handler findings. Only the
    /// risk verdicts are surfaced (<c>SilentStarvationRisk</c>, <c>LikelyInvalid</c>);
    /// <c>Ok</c> handlers are dropped from SARIF reports.
    /// </summary>
    public static string EmitFanOutSarif(IReadOnlyList<MessageHandlerFinding> findings)
    {
        var sarifFindings = findings
            .Where(f => f.Verdict != FanOutVerdict.Ok)
            .Select(f => new SarifFinding(
                RuleId: "MAF001",
                Severity: SarifSeverity.Error,
                Message: $"`{f.MethodName}` returns `{f.ReturnType}` — fan-out handler must return Task<T> or ValueTask<T>",
                File: f.File,
                Line: f.Line));

        return SarifEmitter.Emit(ToolVersion, sarifFindings, BuildFanOutRuleCatalog());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SarifSeverity MapSeverity(AntiPatternSeverity s) => s switch
    {
        AntiPatternSeverity.Error => SarifSeverity.Error,
        AntiPatternSeverity.Warning => SarifSeverity.Warning,
        AntiPatternSeverity.Info => SarifSeverity.Note,
        _ => SarifSeverity.Note,
    };

    private static IEnumerable<SarifRule> BuildAntiPatternRuleCatalog()
    {
        const string skillUri = "https://github.com/joslat/maf-doctor/blob/main/.github/skills/maf-anti-pattern-scanner/SKILL.md";
        foreach (var rule in AntiPatternScannerTool.AllRules)
        {
            yield return new SarifRule(
                Id: rule.Id,
                Name: rule.Name,
                Severity: MapSeverity(rule.Severity),
                FullDescription: rule.Name,
                HelpUri: skillUri);
        }
    }

    private static IEnumerable<SarifRule> BuildFanOutRuleCatalog()
    {
        yield return new SarifRule(
            Id: "MAF001",
            Name: "Fan-out handler must return Task<T> or ValueTask<T>",
            Severity: SarifSeverity.Error,
            FullDescription: "A [MessageHandler] method that returns void, Task, or ValueTask (non-generic) produces no downstream message and silently starves the fan-in barrier.",
            HelpUri: "https://github.com/joslat/maf-doctor/blob/main/.github/skills/maf-fan-out-validator/SKILL.md");
    }
}
