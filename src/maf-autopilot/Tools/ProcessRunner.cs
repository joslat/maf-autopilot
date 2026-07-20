using System.Diagnostics;
using System.Text;
using System.Threading;

namespace MafDoctor.Tools;

/// <summary>
/// Shared, hardened external-process invocation for tools that shell out to
/// `dotnet build` and `dotnet-inspect`. Adds:
///
///   1. Safe argument passing via <see cref="ProcessStartInfo.ArgumentList"/> —
///      no string interpolation, no shell quoting issues.
///   2. Default 5-minute timeout with process-tree kill on hit.
///   3. Concurrent stdout + stderr drain to avoid pipe-buffer deadlock
///      (the classic "synchronous ReadToEnd on stdout while stderr fills up"
///      hang).
///   4. Bounded output (16 MB cap) to protect against pathological build logs.
/// </summary>
internal static class ProcessRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    private const int MaxBytes = 16 * 1024 * 1024;

    public static (int ExitCode, string Output) RunDotnetBuild(string projectOrSolutionPath)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "build", projectOrSolutionPath, "--nologo" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(projectOrSolutionPath) ?? Directory.GetCurrentDirectory(),
        };
        return Run(psi);
    }

    // F-12 (2026-07-19 security assessment) — MafDiffPackage / MafPreUpgradeDryRun
    // are annotated ReadOnly=true, which a client can read as "safe to
    // auto-invoke without confirmation." The dnx fallback below downloads and
    // executes a NuGet tool package on demand, which is a materially different
    // risk than a passive read even though it writes nothing to the user's
    // *workspace* (the annotation's literal scope). Rather than change the
    // annotation (which would also block the common, low-risk case where
    // dotnet-inspect just isn't on PATH yet in an otherwise-trusted dev
    // environment), gate the download itself behind an explicit opt-in that
    // defaults to off — a client auto-invoking this ReadOnly tool can no
    // longer trigger a silent download as a side effect.
    //
    // Reviewed scope note: RegistryExtractCommand's `maf-doctor
    // registry-extract` CLI subcommand also calls this method (shared code,
    // not duplicated), so a local developer running it by hand without
    // dotnet-inspect pre-installed now sees the same "disabled by default"
    // message instead of a silent download, unless they set this env var.
    // CI is unaffected — maf-release-watcher.yml installs dotnet-inspect at
    // an exact pinned version before this ever runs. A human running the CLI
    // deliberately is a lower-risk case than an MCP client auto-invoking a
    // ReadOnly tool, but splitting this into two code paths just to restore
    // the old CLI-only auto-download convenience isn't worth the duplication
    // for a one-time `dotnet tool install` a developer can run themselves.
    public const string AllowToolDownloadEnvName = "MAF_DOCTOR_ALLOW_TOOL_DOWNLOAD";

    // internal (not private) so tests can verify the gating logic directly
    // without depending on whether dotnet-inspect happens to be installed in
    // the test environment (which would otherwise never even reach this
    // check, making the opt-in path untestable in CI).
    internal static bool ToolDownloadAllowed() =>
        Environment.GetEnvironmentVariable(AllowToolDownloadEnvName) is "1" or "true";

    public static (int ExitCode, string Output) RunDotnetInspectDiff(
        string packageId, string oldVersion, string newVersion)
    {
        // We try the installed global tool first; fall back to `dnx dotnet-inspect@0.7.8`
        // only when the operator has explicitly opted in.
        var psi = new ProcessStartInfo("dotnet-inspect")
        {
            ArgumentList =
            {
                "diff",
                "--package", $"{packageId}@{oldVersion}..{newVersion}",
                "--source", "https://api.nuget.org/v3/index.json",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            return Run(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // dotnet-inspect not on PATH.
            if (!ToolDownloadAllowed())
            {
                return (1,
                    "dotnet-inspect is not installed and on-demand download is disabled by default. " +
                    $"Install it with `dotnet tool install --global dotnet-inspect --version 0.7.8`, " +
                    $"or set {AllowToolDownloadEnvName}=1 in the MCP server's env config to allow " +
                    "this tool to fetch and run it on demand via `dnx` when needed.");
            }

            var fallback = new ProcessStartInfo("dnx")
            {
                ArgumentList =
                {
                    "dotnet-inspect@0.7.8",
                    "-y",
                    "--source", "https://api.nuget.org/v3/index.json",
                    "--",
                    "diff",
                    "--package", $"{packageId}@{oldVersion}..{newVersion}",
                    "--source", "https://api.nuget.org/v3/index.json",
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            return Run(fallback);
        }
    }

    /// <summary>
    /// Runs `git -C &lt;repoPath&gt; &lt;args...&gt;` with the same concurrent-drain,
    /// bounded-output, process-tree-kill-on-timeout behavior as the build/inspect
    /// runners above. Callers that need to distinguish "git not on PATH" should
    /// catch <see cref="System.ComponentModel.Win32Exception"/> (matches
    /// <see cref="RunDotnetInspectDiff"/>'s pattern) — this method does not swallow it.
    /// </summary>
    public static (int ExitCode, string Output) RunGit(string repoPath, TimeSpan timeout, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repoPath);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        return Run(psi, timeout);
    }

    private static (int ExitCode, string Output) Run(ProcessStartInfo psi) => Run(psi, DefaultTimeout);

    private static (int ExitCode, string Output) Run(ProcessStartInfo psi, TimeSpan timeout)
    {
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {psi.FileName}");

        // F-08 — bound memory WHILE reading instead of truncating a fully-buffered
        // string after the fact. A pathological build log could otherwise allocate
        // hundreds of MB (stream-reader buffers + the complete stdout/stderr strings)
        // before the old post-hoc truncation ever ran. The two drains share one byte
        // budget (matching the prior "16 MB of combined output" contract) and keep
        // consuming-and-discarding past the cap so a chatty child can't block on a
        // full pipe waiting for a drain that stopped early.
        var budget = new SharedCharBudget(MaxBytes);
        var stdoutTask = DrainBoundedAsync(p.StandardOutput, budget);
        var stderrTask = DrainBoundedAsync(p.StandardError, budget);

        if (!p.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return (
                ExitCode: -1,
                Output: $"⚠️ Process timed out after {timeout.TotalMinutes:0.#} minutes and was killed.");
        }

        // Ensure the drains finished after process exit (they may still be flushing
        // the last buffered chunk).
        var (stdout, stdoutTruncated) = stdoutTask.GetAwaiter().GetResult();
        var (stderr, stderrTruncated) = stderrTask.GetAwaiter().GetResult();

        var combined = stdout + Environment.NewLine + stderr;
        if (stdoutTruncated || stderrTruncated)
            combined += Environment.NewLine + $"⚠️ Output truncated at {MaxBytes / (1024 * 1024)} MB.";

        return (p.ExitCode, combined);
    }

    // internal (not private) so the test assembly can exercise the bounded-drain
    // contract directly against an in-memory TextReader, without needing to spawn
    // a real subprocess that emits 16+ MB of output.
    internal static async Task<(string Text, bool Truncated)> DrainBoundedAsync(TextReader reader, SharedCharBudget budget)
    {
        var sb = new StringBuilder();
        var truncated = false;
        var buffer = new char[8192];
        int read;
        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            var allowed = budget.TryConsume(read);
            if (allowed > 0)
                sb.Append(buffer, 0, allowed);
            if (allowed < read)
                truncated = true;
        }
        return (sb.ToString(), truncated);
    }

    /// <summary>
    /// Thread-safe shared cap consumed by both the stdout and stderr drains, so the
    /// combined output — not each stream independently — stays under <see cref="MaxBytes"/>.
    /// </summary>
    internal sealed class SharedCharBudget(int maxChars)
    {
        private int _remaining = maxChars;

        /// <summary>Returns how many of the <paramref name="count"/> just-read chars may still be kept (0..count).</summary>
        public int TryConsume(int count)
        {
            while (true)
            {
                var current = Volatile.Read(ref _remaining);
                if (current <= 0) return 0;
                var take = Math.Min(current, count);
                if (Interlocked.CompareExchange(ref _remaining, current - take, current) == current)
                    return take;
            }
        }
    }
}
