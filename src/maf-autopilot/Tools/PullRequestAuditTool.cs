using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafAuditPullRequest
///
/// Scopes all the existing scanners (anti-pattern, fan-out, prompt-lint) to the
/// set of <c>*.cs</c> files changed in the current branch versus a base branch.
/// Turns a full-repo audit into a PR-comment-friendly scan — the CI integration
/// unlock.
///
/// Implementation: shells <c>git diff --name-only base...HEAD</c>, filters to
/// <c>*.cs</c>, then runs each scanner per file using its pure-function entry
/// point. No extra dependencies, no semantic model needed.
/// </summary>
[McpServerToolType]
public sealed class PullRequestAuditTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Audit the current pull-request: scope every MAF scanner to the .cs files
        changed in this branch vs the base branch. Returns a markdown report
        suitable for posting as a PR comment.

        Input:
          - repoPath: absolute path to the repository root (must contain `.git`).
          - baseBranch: the branch to diff against (default: `main`).

        Returns a markdown report grouped by changed file, with anti-pattern,
        fan-out, and prompt-lint findings. Files with zero findings are omitted.
        """)]
    public string MafAuditPullRequest(
        [Description("Absolute path to the repository root.")] string repoPath,
        [Description("Base branch to diff against. Default: main.")] string baseBranch = "main")
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err) return err;
        if (!IsSafeBranchName(baseBranch))
            return $"Error: baseBranch '{baseBranch}' contains invalid characters.";

        var changedFiles = GetChangedCsFiles(repoPath, baseBranch);
        if (changedFiles is null)
            return "Error: `git diff` failed. Confirm the repo is a git checkout and the base branch exists.";

        if (changedFiles.Count == 0)
            return $"✅ No `.cs` files changed in this branch vs `{baseBranch}`. Nothing to audit.";

        return BuildReport(repoPath, baseBranch, changedFiles);
    }

    // -------------------------------------------------------------------------
    // git diff
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the *.cs paths (repo-relative, forward-slashed) that differ
    /// between the base branch and HEAD. Returns null if git fails.
    /// </summary>
    internal static IReadOnlyList<string>? GetChangedCsFiles(string repoPath, string baseBranch)
    {
        // `git diff --name-only base...HEAD` lists files changed since the merge-base.
        // The triple-dot form gives the symmetric difference with the merge-base,
        // which is what PR review wants.
        var psi = new ProcessStartInfo("git")
        {
            ArgumentList = { "-C", repoPath, "diff", "--name-only", $"{baseBranch}...HEAD" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        string stdout;
        int exitCode;
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return null;
            stdout = p.StandardOutput.ReadToEnd();
            // Drain stderr so the child can't deadlock on a full stderr pipe.
            _ = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(30_000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return null;
            }
            exitCode = p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // git not on PATH
            return null;
        }

        if (exitCode != 0) return null;
        return ParseGitDiffOutput(stdout);
    }

    /// <summary>
    /// Parses the stdout of <c>git diff --name-only base...HEAD</c> into the
    /// .cs paths we want to scan. Strips CR for Windows checkouts, filters to
    /// .cs files, drops bin/obj noise. Pure: no I/O — the testable seam.
    /// </summary>
    internal static IReadOnlyList<string> ParseGitDiffOutput(string stdout) =>
        stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.Contains("/bin/", StringComparison.Ordinal)
                        && !line.Contains("/obj/", StringComparison.Ordinal))
            .ToList();

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    private static string BuildReport(string repoPath, string baseBranch, IReadOnlyList<string> changedFiles)
    {
        var perFileFindings = new List<(string File, List<string> Lines)>();
        var totals = new Totals();

        foreach (var relPath in changedFiles)
        {
            var absPath = Path.Combine(repoPath, relPath);
            if (!File.Exists(absPath)) continue;     // file deleted in the diff
            var source = File.ReadAllText(absPath);

            var anti = AntiPatternScannerTool.ScanFile(source, relPath);
            var fanOut = FanOutValidatorTool.AnalyzeSource(source, relPath)
                .Where(f => f.Verdict is FanOutVerdict.SilentStarvationRisk or FanOutVerdict.LikelyInvalid)
                .ToList();
            var prompt = PromptLintTool.LintSource(source, relPath);

            if (anti.Count == 0 && fanOut.Count == 0 && prompt.Count == 0) continue;

            var lines = new List<string>();
            foreach (var f in anti)
            {
                lines.Add($"- **{Severity(f.Severity)}** `{f.RuleId}` line {f.Line}: {f.RuleName}");
                Bump(totals, f.Severity);
            }
            foreach (var f in fanOut)
            {
                lines.Add($"- **❌ Error** `MAF001` line {f.Line}: `{f.MethodName}` returns `{f.ReturnType}` — fan-out handler must return `Task<T>`");
                totals.Errors++;
            }
            foreach (var f in prompt)
            {
                var sev = f.Severity == PromptSeverity.Error ? "❌ Error" : "⚠️ Warning";
                lines.Add($"- **{sev}** `{f.RuleId}` line {f.Line}: {f.Message}");
                if (f.Severity == PromptSeverity.Error) totals.Errors++;
                else totals.Warnings++;
            }

            perFileFindings.Add((relPath, lines));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## 🔍 MAF PR audit — `{baseBranch}` → HEAD");
        sb.AppendLine();
        sb.AppendLine($"**Files scanned:** {changedFiles.Count} changed `.cs` file(s).");
        sb.AppendLine();
        sb.AppendLine($"| ❌ Errors | ⚠️ Warnings | ℹ️ Info |");
        sb.AppendLine($"|---:|---:|---:|");
        sb.AppendLine($"| {totals.Errors} | {totals.Warnings} | {totals.Infos} |");
        sb.AppendLine();

        if (perFileFindings.Count == 0)
        {
            sb.AppendLine("✅ All changed files are clean against the configured scanner rules. Ship it.");
            return sb.ToString();
        }

        sb.AppendLine("### Findings by file");
        sb.AppendLine();
        foreach (var (file, lines) in perFileFindings.OrderByDescending(f => f.Lines.Count))
        {
            sb.AppendLine($"#### `{file}`");
            foreach (var line in lines) sb.AppendLine(line);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("_Audited by `maf-doctor` MCP tool `MafAuditPullRequest`. Full-repo scans available via `MafDoctor` / `MafScanAntiPatterns` / `MafValidateFanOut` / `MafLintAgentPrompt`._");
        return sb.ToString();
    }

    private static string Severity(AntiPatternSeverity s) => s switch
    {
        AntiPatternSeverity.Error => "❌ Error",
        AntiPatternSeverity.Warning => "⚠️ Warning",
        _ => "ℹ️ Info",
    };

    private static void Bump(Totals t, AntiPatternSeverity s)
    {
        switch (s)
        {
            case AntiPatternSeverity.Error: t.Errors++; break;
            case AntiPatternSeverity.Warning: t.Warnings++; break;
            default: t.Infos++; break;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static bool IsSafeBranchName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        // Security: legitimate git branch names never start with `-`. Without this check,
        // `baseBranch = "--output=PATH"` would flow into `git diff` argv as
        // `["diff", "--name-only", "--output=PATH...HEAD"]`, where git's argv parser
        // honours `--output=PATH` and writes the diff to PATH — an arbitrary-write
        // primitive in the developer's filesystem. Same family: `--exec=`, `--upload-pack=`.
        if (s.StartsWith('-')) return false;
        // Reject shell metacharacters; allow git's legal branch chars.
        foreach (var c in s)
            if (c is ';' or '|' or '&' or '"' or '\'' or '\0' or '\n' or '\r' or ' ')
                return false;
        return true;
    }

    private sealed class Totals
    {
        public int Errors;
        public int Warnings;
        public int Infos;
    }
}
