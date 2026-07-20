using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafExplainFinding
///
/// Tier 2 of the doctor-suggestions work. Where <see cref="DoctorTool"/> lists
/// findings, this tool deep-dives ONE: given a finding's location (file + line,
/// as printed in the doctor report), it returns the <b>grounded substrate</b> an
/// assistant needs to explain it and propose a precise fix WITHOUT hallucinating
/// — the offending code in context, plus the rule's rationale, fix, and
/// auto-fixability (reused verbatim from <see cref="DoctorTool.Grade"/>).
///
/// The design leverage: maf-doctor runs as an MCP server inside a host that
/// already has a model (Copilot / Claude / Cursor). We don't spend our own LLM
/// budget — we hand the host model exact, deterministic material and let it
/// narrate. The companion <c>maf-explain-finding</c> prompt orchestrates that.
/// </summary>
[McpServerToolType]
public sealed class ExplainFindingTool
{
    private const int DefaultContext = 8;
    private const int MaxContext = 30;

    // F-20 — matches SourceFileWalker.ScanBudget.MaxFileBytes: the primary
    // recursive walker has capped individual files at this size since Phase
    // E, but this tool's direct, containment-checked-but-otherwise-unbounded
    // File.ReadAllText bypassed that budget entirely. A single pathologically
    // large .cs file (deeply-nested generics, a giant generated source file,
    // or one deliberately crafted to be huge) could still be fully
    // materialized in memory before this fix.
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Deep-dive a SINGLE MAF Doctor finding into grounded context for explaining
        or fixing it. Given the finding's location (file + line, as printed in the
        MafDoctor report), returns: the offending source line in context, and the
        rule's rationale ("why it bites you"), the actionable fix, and whether it
        is auto-fixable.

        Use this after MafDoctor surfaces a finding you want to understand or
        resolve. Pair it with the `maf-explain-finding` prompt, which reads
        maf://constraints + maf://guide and turns this substrate into a precise
        explanation and proposed fix.

        Input:
          - repoPath: absolute path to the repository root.
          - file: repo-relative path of the finding (exactly as shown in the
            report, e.g. 'Executors/Foo.cs').
          - line: 1-based line number of the finding.
          - context: lines of surrounding code on each side (default 8, max 30).

        Returns a markdown report, including the source lines around the finding.
        Lines that look like they contain a secret (API keys, connection-string
        secrets, tokens, JWTs, PEM private keys) are redacted, not echoed — this
        is best-effort, not exhaustive, so treat it as a safety net, not a
        guarantee. The returned source is sent to whichever model is driving
        this MCP session — do not point this at a repository where that isn't
        acceptable. `ReadOnly=true` describes filesystem effects only, not data
        exposure: it does not mean "safe to auto-approve" for a sensitive repo.
        """)]
    public string MafExplainFinding(
        [Description("Absolute path to the repository root.")] string repoPath,
        [Description("Repo-relative path of the finding, as shown in the report (e.g. 'Executors/Foo.cs').")] string file,
        [Description("1-based line number of the finding.")] int line,
        [Description("Lines of surrounding context on each side (default 8, max 30).")] int context = DefaultContext)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err) return err;

        try { BoundedInput.Validate(file, BoundedInput.PathBytes, nameof(file)); }
        catch (ArgumentException ex) { return $"Error: {ex.Message}"; }

        if (line <= 0) return "Error: line must be a positive 1-based line number.";
        context = Math.Clamp(context, 0, MaxContext);

        string full;
        try { full = PathGuard.ValidateContainment(repoPath, file, nameof(file)); }
        catch (ArgumentException ex) { return $"Error: {ex.Message}"; }

        // C#-source only: doctor findings come from .cs files, and this guards
        // against rendering a secret-bearing config (.env / appsettings / .pem)
        // through the best-effort (not exhaustive) redactor.
        if (!full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return $"Error: MafExplainFinding only inspects C# source (.cs) files; got `{file}`.";

        if (!File.Exists(full)) return $"Error: file not found under the repository: `{file}`.";

        var length = new FileInfo(full).Length;
        if (length > MaxFileBytes)
            return $"Error: `{file}` is {length:N0} bytes, over the {MaxFileBytes:N0}-byte cap for MafExplainFinding. " +
                   "Findings on very large generated files are better inspected directly.";

        var source = File.ReadAllText(full);
        var rel = file.Replace('\\', '/');

        // Reuse the doctor's analysis on just this file so the rule rationale +
        // fix + auto-fixability are identical to what MafDoctor reports.
        var summary = DoctorTool.Grade(
            AntiPatternScannerTool.ScanFile(source, rel),
            FanOutValidatorTool.AnalyzeSource(source, rel),
            PromptLintTool.LintSource(source, rel),
            EstimateCostTool.AnalyzeSource(source, rel));

        // Findings exactly on the line, else widen to ±2 (line numbers in a
        // report can drift by an attribute line etc.).
        var atLine = summary.AllFixes.Where(f => f.Line == line).ToList();
        var widened = false;
        if (atLine.Count == 0)
        {
            atLine = summary.AllFixes.Where(f => Math.Abs(f.Line - line) <= 2)
                .OrderBy(f => Math.Abs(f.Line - line)).ThenBy(f => f.Priority).ToList();
            widened = atLine.Count > 0;
        }

        var lines = source.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine($"## 🔬 Finding deep-dive — `{rel}:{line}`");
        sb.AppendLine();

        // Mark the ACTUAL finding line(s) (which, when widened, differ from the
        // user-supplied line), and centre the window on the nearest finding so it
        // stays visible even at context: 0.
        var centerLine = atLine.Count > 0 ? atLine[0].Line : line;
        var marked = atLine.Count > 0
            ? atLine.Select(f => f.Line).ToHashSet()
            : new HashSet<int> { line };
        AppendCodeWindow(sb, lines, centerLine, marked, context);

        if (atLine.Count == 0)
        {
            sb.AppendLine("> ℹ️ No active finding detected at or near this line. It may already be");
            sb.AppendLine("> fixed, or the line number may be stale — re-run `MafDoctor` and use the");
            sb.AppendLine("> exact `file:line` it prints.");
            sb.AppendLine();
            return sb.ToString();
        }

        if (widened)
        {
            sb.AppendLine($"_(No finding exactly on line {line}; showing the nearest within ±2 lines — marked above.)_");
            sb.AppendLine();
        }

        var nearOrAt = widened ? "near this line" : "at this line";
        sb.AppendLine(atLine.Count == 1
            ? (widened ? "### The finding (near your line)" : "### The finding")
            : $"### {atLine.Count} findings {nearOrAt}");
        sb.AppendLine();
        foreach (var f in atLine)
        {
            var tag = f.AutoFixable ? "auto-fixable" : "needs your judgment";
            sb.AppendLine($"**{SeverityEmoji(f.Priority)} `{f.RuleId}` — {f.Issue}** · _{tag}_");
            if (!string.IsNullOrEmpty(f.Why)) sb.AppendLine($"- **Why:** {f.Why}");
            if (!string.IsNullOrEmpty(f.FixDescription)) sb.AppendLine($"- **Fix:** {f.FixDescription}");
            sb.AppendLine();
        }

        sb.AppendLine("### How to resolve");
        sb.AppendLine();
        if (atLine.Any(f => f.AutoFixable))
            sb.AppendLine("- **Mechanical** (auto-fixable above): apply deterministically with `maf-doctor autofix-all . --apply` (CLI) or `MafAutoFixAll(repoPath, dryRun: false)` (MCP), then re-run the doctor.");
        if (atLine.Any(f => !f.AutoFixable))
            sb.AppendLine("- **Semantic** (needs your judgment): apply the **Fix** above by hand or via the `@maf-migration` agent. Verify the change against the hard rules before committing.");
        sb.AppendLine();

        sb.AppendLine("### Grounding (read before fixing)");
        sb.AppendLine();
        sb.AppendLine("- Hard rules that must never be violated: `maf://constraints`");
        sb.AppendLine("- Per-version migration guide: `maf://guide`");
        sb.AppendLine("- Obsolete / removed-surface fix lookup (e.g. `MAF-AP-EXEC-001`; CS0618-class registry entries): `MafRegistryLookup(<id>)` / `MafApiSafety(<api>)`");

        return sb.ToString();
    }

    /// <summary>
    /// Renders a numbered code window centred on <paramref name="centerLine"/>,
    /// marking every line in <paramref name="marked"/> with <c>&gt;</c>. Lines
    /// that look like they contain a secret (the shared
    /// <see cref="AntiPatternScannerTool.LooksLikeSecret"/>, single source of
    /// truth with DoctorTool's redactor) are withheld — best-effort, not
    /// exhaustive (high-entropy secrets in unrecognized shapes can slip through). The
    /// <c>"{marker} {num} | "</c> prefix also means no source line can ever form
    /// a valid CommonMark closing fence, so a line of backticks can't break out.
    /// </summary>
    private static void AppendCodeWindow(StringBuilder sb, string[] lines, int centerLine, HashSet<int> marked, int context)
    {
        // Cover the context window AND every marked finding line, so a widened
        // multi-finding window (or a small `context`) never claims "marked above"
        // for a line that falls outside the rendered range.
        var lo = marked.Count > 0 ? Math.Min(centerLine - context, marked.Min()) : centerLine - context;
        var hi = marked.Count > 0 ? Math.Max(centerLine + context, marked.Max()) : centerLine + context;
        var start = Math.Max(1, lo);
        var end = Math.Min(lines.Length, hi);
        if (start > lines.Length) return; // line past EOF — nothing to show

        sb.AppendLine("### Code in context");
        sb.AppendLine();
        sb.AppendLine("```text");
        var width = end.ToString().Length;
        for (var n = start; n <= end; n++)
        {
            var raw = lines[n - 1];
            var marker = marked.Contains(n) ? ">" : " ";
            var num = n.ToString().PadLeft(width);
            // Guard the regex: a RegexMatchTimeout must redact-on-doubt, not fault
            // the whole tool (mirrors DoctorTool.TryReadSourceLine).
            bool secret;
            try { secret = AntiPatternScannerTool.LooksLikeSecret(raw); }
            catch { secret = true; }
            var body = secret ? "(line redacted — may contain a secret)" : raw.TrimEnd();
            sb.AppendLine($"{marker} {num} | {body}");
        }
        sb.AppendLine("```");
        sb.AppendLine();
    }

    // Single source of truth in DoctorTool.SeverityFor (keeps emoji/label/JSON aligned).
    private static string SeverityEmoji(int priority) => DoctorTool.SeverityFor(priority).Emoji;
}
