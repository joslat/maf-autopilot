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
///   4. Bounded output (16M-character cap) to protect against pathological
///      build logs.
/// </summary>
internal static class ProcessRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    internal const string DotnetInspectVersion = "0.9.1";
    private static readonly TimeSpan DotnetInspectVersionProbeTimeout = TimeSpan.FromSeconds(30);

    internal enum DotnetInspectProbeStatus
    {
        Supported,
        Missing,
        Mismatched,
        Unverifiable,
    }

    internal enum DotnetInspectRoute
    {
        Global,
        ExactDnxFallback,
        FailClosed,
    }

    // Round-2 review fixup (F-08) — this was named/documented as MaxBytes and
    // described as a "16 MB cap", but SharedCharBudget consumes .NET `char`
    // units (UTF-16 code units) read from the pipe, not UTF-8 bytes. For
    // ASCII-heavy output the two are near-identical; for heavy non-ASCII
    // output (e.g. CJK text, where one UTF-16 code unit can decode to a
    // multi-byte UTF-8 sequence) the actual pipe bytes consumed before this
    // cap kicks in can run several times past the number the name implied.
    // The memory-safety property this exists for is unaffected either way —
    // retained managed memory is bounded to MaxChars UTF-16 code units
    // (a fixed, deterministic ceiling) regardless of the source encoding —
    // so this is a naming/message-accuracy fix, not a behavior change.
    private const int MaxChars = 16 * 1024 * 1024;

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
    // annotation (which would also block the common, low-risk case where an exact
    // supported dotnet-inspect is not yet on PATH in an otherwise-trusted dev
    // environment), gate the download itself behind an explicit opt-in that
    // defaults to off — a client auto-invoking this ReadOnly tool can no
    // longer trigger a silent download as a side effect.
    //
    // Reviewed scope note: RegistryExtractCommand's `maf-doctor
    // registry-extract` CLI subcommand also calls this method (shared code,
    // not duplicated), so a local developer running it by hand without exact
    // dotnet-inspect 0.9.1 pre-installed now sees the same fail-closed message
    // instead of a silent download, unless they set this env var.
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

    /// <summary>
    /// Accepts the exact supported stable release and that release with SemVer build
    /// metadata (the upstream Native-AOT binary reports values such as
    /// <c>0.9.1+891e68b</c>). Prerelease suffixes and all neighboring versions fail.
    /// </summary>
    internal static bool IsSupportedDotnetInspectVersion(string output)
    {
        var version = output.Trim();
        if (version.Equals(DotnetInspectVersion, StringComparison.Ordinal))
            return true;

        var metadataPrefix = DotnetInspectVersion + "+";
        if (!version.StartsWith(metadataPrefix, StringComparison.Ordinal))
            return false;

        var metadata = version[metadataPrefix.Length..];
        return metadata.Length > 0
            && metadata[0] != '.'
            && metadata[^1] != '.'
            && !metadata.Contains("..", StringComparison.Ordinal)
            && metadata.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.');
    }

    internal static DotnetInspectProbeStatus ClassifyDotnetInspectProbe(
        int exitCode, string output)
    {
        if (exitCode == 127)
            return DotnetInspectProbeStatus.Missing;
        if (exitCode != 0)
            return DotnetInspectProbeStatus.Unverifiable;
        return IsSupportedDotnetInspectVersion(output)
            ? DotnetInspectProbeStatus.Supported
            : LooksLikeVersionToken(output)
                ? DotnetInspectProbeStatus.Mismatched
                : DotnetInspectProbeStatus.Unverifiable;
    }

    internal static DotnetInspectRoute SelectDotnetInspectRoute(
        int probeExitCode, string probeOutput, bool allowExactFallback)
    {
        var status = ClassifyDotnetInspectProbe(probeExitCode, probeOutput);
        if (status == DotnetInspectProbeStatus.Supported)
            return DotnetInspectRoute.Global;
        return allowExactFallback
            ? DotnetInspectRoute.ExactDnxFallback
            : DotnetInspectRoute.FailClosed;
    }

    public static (int ExitCode, string Output) RunDotnetInspectDiff(
        string packageId, string oldVersion, string newVersion)
    {
        // Probe before use: 0.7.8 can exit 0 while returning corrupt redirected output,
        // so "some dotnet-inspect is on PATH" is not a sufficient safety check.
        var versionProbe = new ProcessStartInfo("dotnet-inspect")
        {
            ArgumentList = { "--version" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Run() deliberately converts a missing executable into exit 127 so every
        // other caller gets a graceful error. Route on that typed outcome here rather
        // than relying on the old, unreachable outer Win32Exception catch.
        var (probeExitCode, probeOutput) = Run(versionProbe, DotnetInspectVersionProbeTimeout);
        var route = SelectDotnetInspectRoute(
            probeExitCode, probeOutput, ToolDownloadAllowed());

        if (route == DotnetInspectRoute.Global)
        {
            var global = CreateDotnetInspectDiffStartInfo(
                "dotnet-inspect", packageId, oldVersion, newVersion, throughDnx: false);
            return Run(global);
        }

        if (route == DotnetInspectRoute.FailClosed)
            return (1, DescribeUnsupportedDotnetInspect(probeExitCode, probeOutput));

        var fallback = CreateDotnetInspectDiffStartInfo(
            "dnx", packageId, oldVersion, newVersion, throughDnx: true);
        var fallbackResult = Run(fallback);
        if (fallbackResult.ExitCode == 127)
        {
            return (1,
                $"The exact dotnet-inspect {DotnetInspectVersion} fallback was requested, " +
                "but `dnx` is not available. Install the supported tool directly with " +
                $"`dotnet tool install --global dotnet-inspect --version {DotnetInspectVersion}`.");
        }
        return fallbackResult;
    }

    internal static ProcessStartInfo CreateDotnetInspectDiffStartInfo(
        string executable,
        string packageId,
        string oldVersion,
        string newVersion,
        bool throughDnx)
    {
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (throughDnx)
        {
            psi.ArgumentList.Add($"dotnet-inspect@{DotnetInspectVersion}");
            psi.ArgumentList.Add("-y");
            psi.ArgumentList.Add("--source");
            psi.ArgumentList.Add("https://api.nuget.org/v3/index.json");
            psi.ArgumentList.Add("--");
        }
        psi.ArgumentList.Add("diff");
        psi.ArgumentList.Add("--package");
        psi.ArgumentList.Add($"{packageId}@{oldVersion}..{newVersion}");
        psi.ArgumentList.Add("--source");
        psi.ArgumentList.Add("https://api.nuget.org/v3/index.json");
        return psi;
    }

    internal static string DescribeUnsupportedDotnetInspect(int exitCode, string output)
    {
        var status = ClassifyDotnetInspectProbe(exitCode, output);
        var reason = status switch
        {
            DotnetInspectProbeStatus.Missing => "`dotnet-inspect` is not installed or not on PATH.",
            DotnetInspectProbeStatus.Mismatched =>
                $"The `dotnet-inspect` on PATH reports version `{output.Trim()}`; exact " +
                $"{DotnetInspectVersion} is required.",
            _ => "The `dotnet-inspect --version` result could not be verified; refusing to run a potentially incompatible tool.",
        };
        return reason + " Install or update it with " +
            $"`dotnet tool update --global dotnet-inspect --version {DotnetInspectVersion}` " +
            "(use `dotnet tool install` if it is absent), or set " +
            $"{AllowToolDownloadEnvName}=1 to allow the exact-version `dnx` fallback.";
    }

    private static bool LooksLikeVersionToken(string output)
    {
        var value = output.Trim();
        return value.Length is > 0 and <= 64
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');
    }

    /// <summary>
    /// Runs `git -C &lt;repoPath&gt; &lt;args...&gt;` with the same concurrent-drain,
    /// bounded-output, process-tree-kill-on-timeout behavior as the build/inspect
    /// runners above. A missing executable is returned as exit code 127 with an
    /// actionable message by the shared runner; it is not thrown to the caller.
    /// </summary>
    public static (int ExitCode, string Output) RunGit(string repoPath, TimeSpan timeout, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // SEC-22 (defense-in-depth): neutralize repo-controlled command-config so a
        // malicious `.git/config` in an untrusted repo can't execute commands via
        // fsmonitor / hooks / pager / the ext transport. Command-line `-c` overrides
        // `.git/config`; these must precede the subcommand. Output is unchanged.
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("core.fsmonitor=false");
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("core.hooksPath=");
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("core.pager=cat");
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("protocol.ext.allow=never");
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repoPath);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        return Run(psi, timeout);
    }

    private static (int ExitCode, string Output) Run(ProcessStartInfo psi) => Run(psi, DefaultTimeout);

    private static (int ExitCode, string Output) Run(ProcessStartInfo psi, TimeSpan timeout)
    {
        // SEC-09: strip credential-like env vars so an untrusted child (dotnet build /
        // dotnet-inspect / git in a repo we don't yet trust) can't read the MCP
        // server's secrets. Done centrally so all four call paths are covered.
        ScrubSensitiveEnvironment(psi);

        Process p;
        try
        {
            p = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start process: {psi.FileName}");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // SEC-10: the binary isn't on PATH — most commonly inside the slim container
            // image, which ships only the runtime (no SDK / git / dnx / dotnet-inspect).
            // Return a clear, actionable message instead of an opaque Win32Exception crash.
            return (127,
                $"`{psi.FileName}` is not available in this environment. The slim container image " +
                "ships only the runtime — the build / git / package-diff tools need the .NET SDK, " +
                "so run maf-doctor as the global tool instead: `dotnet tool install -g maf-doctor`.");
        }
        using var _proc = p;

        // F-08 — bound memory WHILE reading instead of truncating a fully-buffered
        // string after the fact. A pathological build log could otherwise allocate
        // hundreds of MB (stream-reader buffers + the complete stdout/stderr strings)
        // before the old post-hoc truncation ever ran. The two drains share one byte
        // budget (matching the prior "16 MB of combined output" contract) and keep
        // consuming-and-discarding past the cap so a chatty child can't block on a
        // full pipe waiting for a drain that stopped early.
        var budget = new SharedCharBudget(MaxChars);
        var stdoutTask = DrainBoundedAsync(p.StandardOutput, budget);
        var stderrTask = DrainBoundedAsync(p.StandardError, budget);

        if (!p.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }

            // SEC-24: give the drains up to 2s to flush what they captured before the
            // process handle is disposed, and surface it — instead of discarding all
            // output and leaving the drain tasks to fault unobserved on a dead pipe.
            var partial = string.Empty;
            try
            {
                if (Task.WhenAll(stdoutTask, stderrTask).Wait(TimeSpan.FromSeconds(2)))
                    partial = (stdoutTask.Result.Text + Environment.NewLine + stderrTask.Result.Text).Trim();
            }
            catch { /* drains faulted on the disposed pipe — report what we already have */ }

            var timeoutMsg = $"⚠️ Process timed out after {timeout.TotalMinutes:0.#} minutes and was killed.";
            if (!string.IsNullOrWhiteSpace(partial))
                timeoutMsg += Environment.NewLine + "Partial output captured before the kill:" + Environment.NewLine + partial;
            return (ExitCode: -1, Output: timeoutMsg);
        }

        // Ensure the drains finished after process exit (they may still be flushing
        // the last buffered chunk).
        var (stdout, stdoutTruncated) = stdoutTask.GetAwaiter().GetResult();
        var (stderr, stderrTruncated) = stderrTask.GetAwaiter().GetResult();

        var combined = stdout + Environment.NewLine + stderr;
        if (stdoutTruncated || stderrTruncated)
            combined += Environment.NewLine + $"⚠️ Output truncated at {MaxChars:N0} characters.";

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
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                var allowed = budget.TryConsume(read);
                if (allowed > 0)
                    sb.Append(buffer, 0, allowed);
                if (allowed < read)
                    truncated = true;
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException)
        {
            // SEC-24: the process was killed on timeout and its pipe disposed —
            // return whatever we captured rather than faulting the drain task.
        }
        return (sb.ToString(), truncated);
    }

    /// <summary>
    /// SEC-09: removes credential-like variables from a child process's environment
    /// so an untrusted build/inspect/git child cannot read the MCP server's secrets.
    /// Uses a pattern denylist (not an allowlist) so a build/restore never loses a
    /// variable it needs — PATH / DOTNET_* / NUGET_* / HOME / USERPROFILE / TEMP /
    /// APPDATA and the like are all kept.
    /// </summary>
    private static void ScrubSensitiveEnvironment(ProcessStartInfo psi)
    {
        foreach (var key in psi.Environment.Keys.ToList())
            if (IsSensitiveEnvKey(key))
                psi.Environment.Remove(key);
    }

    internal static bool IsSensitiveEnvKey(string key)
    {
        var k = key.ToUpperInvariant();
        return k.Contains("TOKEN") || k.Contains("SECRET") || k.Contains("KEY")
            || k.Contains("PASSWORD") || k.Contains("PASSWD")
            || k.EndsWith("PAT")
            || k.StartsWith("AWS_") || k.StartsWith("AZURE_")
            || k.StartsWith("GITHUB_") || k.StartsWith("GH_");
    }

    /// <summary>
    /// Thread-safe shared cap consumed by both the stdout and stderr drains, so the
    /// combined output — not each stream independently — stays under <see cref="MaxChars"/>
    /// UTF-16 characters (not UTF-8 bytes — see the constant's doc comment).
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
