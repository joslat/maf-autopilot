using System.Text;
using MafDoctor.Tools;

namespace MafDoctor.Commands;

/// <summary>
/// Argument parsing + human-readable rendering for the <c>autofix-all</c> CLI
/// subcommand, extracted from Program.cs so both halves are unit-testable
/// without spawning a process (the rewriting itself lives in <see cref="AutoFixTool"/>).
///
/// Grammar: <c>autofix-all [path] [--apply] [--dry-run] [--json]</c>
/// <list type="bullet">
///   <item>The first non-flag token is the path (default: current directory at
///         the call site — left null here so the caller decides the default).</item>
///   <item><c>--apply</c> writes the changes. Without it, the command previews
///         only — this is the safe default (F-11, 2026-07-19 security
///         assessment: writing by default let a client that misread an MCP
///         confirmation hint, or a human running the wrong command, apply
///         changes nobody reviewed).</item>
///   <item><c>--dry-run</c> is kept as an explicit, no-op spelling of the
///         default so existing scripts and muscle memory keep working
///         unchanged. If both <c>--apply</c> and <c>--dry-run</c> are given,
///         preview wins.</item>
///   <item><c>--json</c> emits the machine-readable JSON (the MCP-tool shape).
///         Default is the human-readable summary produced by <see cref="Format"/>.</item>
/// </list>
/// </summary>
internal static class AutoFixCli
{
    public static (string? Path, bool Json, bool DryRun) Parse(string[] args)
    {
        string? path = null;
        var json = false;
        var apply = false;
        var explicitDryRun = false;

        // args[0] is "autofix-all"; options start at index 1.
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is "--json") { json = true; continue; }
            if (args[i] is "--dry-run") { explicitDryRun = true; continue; }
            if (args[i] is "--apply") { apply = true; continue; }
            // First non-flag token is the path. The `--` guard means a flag that
            // appears before the path (e.g. `autofix-all --json .`) is never
            // mistaken for the path.
            if (path is null && !args[i].StartsWith("--", StringComparison.Ordinal)) path = args[i];
        }

        // F-11 — preview by default; --apply opts in to writing. --dry-run
        // always wins if both are given (safety over convenience).
        var dryRun = explicitDryRun || !apply;
        return (path, json, dryRun);
    }

    /// <summary>
    /// Renders an <see cref="AutoFixTool.AutoFixAllReport"/> as a friendly,
    /// terminal-readable summary. The zero-changes branch is deliberately
    /// verbose: "0 files changed" is the single most-misread output of this
    /// tool, so it spells out *why* (mechanical-only) and *what to do next*
    /// (run <c>doctor</c>; hand semantic findings to the agent).
    /// </summary>
    public static string Format(AutoFixTool.AutoFixAllReport report)
    {
        var sb = new StringBuilder();
        var verb = report.DryRun ? "would change" : "changed";

        sb.AppendLine(report.DryRun
            ? "🔧 maf-doctor autofix-all — dry run (deterministic Roslyn fixes, mechanical only)"
            : "🔧 maf-doctor autofix-all — deterministic Roslyn fixes (mechanical only)");
        sb.AppendLine();

        // WM-21: surface per-file failures that previously vanished. A non-empty
        // list means the pass did NOT fully succeed — some files were skipped —
        // and (in Program.cs) drives a non-zero exit code.
        var errors = report.Errors ?? Array.Empty<AutoFixTool.AutoFixError>();
        if (errors.Count > 0)
        {
            sb.AppendLine($"  ⚠ {Count(errors.Count, "file")} could not be processed and {(errors.Count == 1 ? "was" : "were")} skipped:");
            foreach (var e in errors)
                sb.AppendLine($"      - {e.File}: {e.Message}");
            sb.AppendLine();
        }

        var total = report.TotalDistinctFilesChanged;

        if (total == 0)
        {
            sb.AppendLine($"  ✓ 0 files {verb} — nothing mechanical to fix here.");
            sb.AppendLine();
            sb.AppendLine("  This is normal, and does NOT mean your code is clean. autofix-all only");
            sb.AppendLine($"  applies these {report.PerRule.Count} deterministic rewriters, and found none of them:");
            sb.AppendLine();
            foreach (var r in report.PerRule)
                sb.AppendLine($"      • {r.RuleId,-18} {Describe(r.RuleId)}");
            sb.AppendLine();
            sb.AppendLine("  Most real findings are *semantic* (hard-coded keys, shared provider state,");
            sb.AppendLine("  fan-out/fan-in starvation) and cannot be rewritten mechanically. To see them:");
            sb.AppendLine();
            sb.AppendLine("      maf-doctor doctor .        # full A–F health grade + every finding");
            sb.AppendLine();
            AppendRemediationGuidance(sb);
            sb.AppendLine();
            sb.AppendLine("  (Also double-check the path — a folder with no .cs files also yields 0 changes.)");
            return sb.ToString().TrimEnd();
        }

        var changedRules = report.PerRule.Count(r => r.FilesChanged > 0);
        sb.AppendLine($"  ✓ {Count(total, "file")} {verb} across {Count(changedRules, "rule")}.");
        sb.AppendLine();
        sb.AppendLine("  Per rule:");
        foreach (var r in report.PerRule)
        {
            var mark = r.FilesChanged > 0 ? "✓" : "·";
            sb.AppendLine($"      {mark} {r.RuleId,-18} {r.FilesChanged,2} {(r.FilesChanged == 1 ? "file " : "files")}  {Describe(r.RuleId)}");
        }
        sb.AppendLine();
        sb.AppendLine($"  Files {verb}:");
        foreach (var f in report.AffectedFiles)
            sb.AppendLine($"      - {f}");
        sb.AppendLine();
        if (report.DryRun)
        {
            // WM-24: a preview that shows only file NAMES gives the F-11
            // "review before apply" gate nothing to review. Inline the actual
            // unified diff for each changed file when the set is small enough to
            // read; otherwise point at --json for the full per-file diffs.
            var diffs = report.Diffs ?? Array.Empty<AutoFixTool.AutoFixFileDiff>();
            const int inlineDiffCap = 10;
            if (diffs.Count > 0 && total <= inlineDiffCap)
            {
                sb.AppendLine("  Preview (unified diff — nothing has been written):");
                sb.AppendLine();
                foreach (var d in diffs)
                {
                    sb.AppendLine($"      ── {d.File}   [{string.Join(", ", d.RuleIds)}]");
                    foreach (var line in d.Diff.TrimEnd('\n').Split('\n'))
                        sb.AppendLine("        " + line);
                    sb.AppendLine();
                }
            }
            else if (diffs.Count > 0)
            {
                sb.AppendLine($"  ({total} files would change — too many to inline; re-run with --json for the full per-file diffs.)");
                sb.AppendLine();
            }

            // F-11 (2026-07-19 security assessment, found in a subsequent review
            // round): dry-run is now the DEFAULT, so "re-run without --dry-run"
            // was stale/misleading advice for the common case (a user who ran
            // `autofix-all <path>` with no flags never passed --dry-run in the
            // first place). --apply is the actual opt-in flag.
            sb.AppendLine("  Dry run — re-run with --apply to apply these changes.");
        }
        sb.AppendLine("  Next: `maf-doctor doctor .` to re-grade.");
        sb.AppendLine();
        AppendRemediationGuidance(sb);
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Shared footer that both the zero-change and the with-changes branches end
    /// on: states plainly that this command is mechanical-only and tells the user
    /// how to get at the rest (an LLM-driven plan). Kept in one place so the two
    /// branches never drift.
    /// </summary>
    private static void AppendRemediationGuidance(StringBuilder sb)
    {
        sb.AppendLine("  ⚙️  autofix-all only applies DETERMINISTIC, MECHANICAL fixes — that's all a");
        sb.AppendLine("     Roslyn rewriter can safely do without understanding your code's intent.");
        sb.AppendLine("     The semantic findings (hard-coded keys, shared state, fan-out starvation,");
        sb.AppendLine("     DevUI guards, cost caps) need judgment. To work through them, let an LLM");
        sb.AppendLine("     drive maf-doctor:");
        sb.AppendLine();
        sb.AppendLine("       maf-doctor doctor . --plan      # ordered, checkboxed remediation plan");
        sb.AppendLine();
        sb.AppendLine("     …or in an MCP client (Copilot / Claude / Cursor) just ask:");
        sb.AppendLine("       \"make me a plan to fix these issues\"");
        sb.AppendLine("     and the model works through them with maf-doctor's tools + the");
        sb.AppendLine("     @maf-migration agent. Some findings are heuristic and may be FALSE");
        sb.AppendLine("     POSITIVES in your codebase — verify each before changing it.");
    }

    private static string Describe(string ruleId) =>
        AutoFixTool.RuleDescriptions.TryGetValue(ruleId, out var d) ? d : ruleId;

    private static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";
}
