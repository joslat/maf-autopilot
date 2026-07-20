using System.ComponentModel;
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
/// Implementation: shells <c>git diff -z --name-only base...HEAD</c>, filters
/// to <c>*.cs</c>, then runs each scanner per file using its pure-function
/// entry point. No extra dependencies, no semantic model needed.
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
        // F-09 — previously read stdout to completion synchronously before touching
        // stderr, so a child that fills the stderr pipe while stdout is still being
        // read could deadlock the parent inside ReadToEnd before the 30s timeout was
        // ever evaluated. ProcessRunner.RunGit drains both streams concurrently.
        //
        // `git diff --name-only base...HEAD` lists files changed since the merge-base.
        // The triple-dot form gives the symmetric difference with the merge-base,
        // which is what PR review wants.
        //
        // F-07 (round-2 review fixup) — `-z` makes the entry separator NUL
        // instead of newline. A newline-split parse mis-handles a filename
        // containing a literal embedded newline (legal on POSIX filesystems):
        // it would fragment into two bogus "paths". NUL is the only byte a
        // POSIX path can never contain, so it's the only safe separator.
        int exitCode;
        string stdout;
        try
        {
            (exitCode, stdout) = ProcessRunner.RunGit(
                repoPath, TimeSpan.FromSeconds(30), "diff", "-z", "--name-only", $"{baseBranch}...HEAD");
        }
        catch (Win32Exception)
        {
            // git not on PATH
            return null;
        }

        if (exitCode != 0) return null;
        return ParseGitDiffOutput(stdout);
    }

    /// <summary>
    /// Parses the NUL-delimited stdout of <c>git diff -z --name-only base...HEAD</c>
    /// into the .cs paths we want to scan: filters to .cs files, drops bin/obj
    /// noise. Pure: no I/O — the testable seam.
    /// </summary>
    internal static IReadOnlyList<string> ParseGitDiffOutput(string stdout) =>
        stdout
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.Contains("/bin/", StringComparison.Ordinal)
                        && !line.Contains("/obj/", StringComparison.Ordinal))
            .ToList();

    // -------------------------------------------------------------------------
    // Report
    // -------------------------------------------------------------------------

    // F-07 — mirrors maf-pr-audit.yml's own compiler-bomb cap (200 changed files /
    // 5 MB aggregate) so the MCP tool is bounded the same way when a client calls
    // it directly, not only when the CI wrapper's shell-level pre-check runs first.
    internal const int MaxChangedFiles = 200;
    internal const long MaxFileBytes = 10 * 1024 * 1024; // matches SourceFileWalker.ScanBudget's default
    internal const long MaxAggregateBytes = 5 * 1024 * 1024;

    // internal (not private) so the test assembly can exercise the containment/
    // size-cap logic directly, without needing a real git subprocess for every case
    // — same pattern as RegistryService.ValidateOverridePath.
    internal static string BuildReport(string repoPath, string baseBranch, IReadOnlyList<string> changedFiles)
    {
        var perFileFindings = new List<(string File, List<string> Lines)>();
        var totals = new Totals();
        var skipped = new List<string>();
        var scanComplete = true;

        var filesToScan = changedFiles;
        if (changedFiles.Count > MaxChangedFiles)
        {
            filesToScan = changedFiles.Take(MaxChangedFiles).ToList();
            scanComplete = false;
            skipped.Add($"{changedFiles.Count - MaxChangedFiles} additional changed file(s) beyond the {MaxChangedFiles}-file audit cap were not scanned.");
        }

        long aggregateBytes = 0;
        var actuallyScanned = 0;
        foreach (var relPath in filesToScan)
        {
            // F-07 — previously combined repoPath + relPath with plain Path.Combine
            // and read it with File.ReadAllText: no containment check (a symlinked
            // changed-file entry could read outside the repo) and no per-file/
            // aggregate size cap (a large diff'd file could pin Roslyn-host memory).
            string absPath;
            try
            {
                absPath = PathGuard.ValidateContainment(repoPath, relPath, "changed file");
            }
            catch (ArgumentException ex)
            {
                scanComplete = false;
                skipped.Add($"`{relPath}` — refused: {ex.Message}");
                continue;
            }

            if (!File.Exists(absPath)) continue;     // file deleted in the diff

            var length = new FileInfo(absPath).Length;
            if (length > MaxFileBytes)
            {
                scanComplete = false;
                skipped.Add($"`{relPath}` — {length / (1024 * 1024)} MB exceeds the {MaxFileBytes / (1024 * 1024)} MB per-file cap, skipped.");
                continue;
            }
            if (aggregateBytes + length > MaxAggregateBytes)
            {
                scanComplete = false;
                skipped.Add($"remaining changed file(s) beyond the {MaxAggregateBytes / (1024 * 1024)} MB aggregate cap were not scanned.");
                break;
            }
            aggregateBytes += length;
            actuallyScanned++;

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
        // Verified-in-review fixup: this used to always report changedFiles.Count
        // (the total diff size), which read as "files scanned" even when F-07's
        // caps/containment checks meant some of those files were never actually
        // read — a report claiming "Files scanned: 500" immediately above a
        // "Scan incomplete, only 200 processed" warning was self-contradictory.
        sb.AppendLine(actuallyScanned == changedFiles.Count
            ? $"**Files scanned:** {actuallyScanned} changed `.cs` file(s)."
            : $"**Files scanned:** {actuallyScanned} of {changedFiles.Count} changed `.cs` file(s).");
        if (!scanComplete)
        {
            sb.AppendLine();
            sb.AppendLine("⚠️ **Scan incomplete** — do not treat this report as a clean bill of health:");
            foreach (var reason in skipped) sb.AppendLine($"- {reason}");
        }
        sb.AppendLine();
        sb.AppendLine($"| ❌ Errors | ⚠️ Warnings | ℹ️ Info |");
        sb.AppendLine($"|---:|---:|---:|");
        sb.AppendLine($"| {totals.Errors} | {totals.Warnings} | {totals.Infos} |");
        sb.AppendLine();

        if (perFileFindings.Count == 0)
        {
            sb.AppendLine(scanComplete
                ? "✅ All changed files are clean against the configured scanner rules. Ship it."
                : "⚠️ No findings in the file(s) that were scanned — but the scan above was incomplete. This is NOT a clean bill of health.");
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
