using System.Diagnostics;

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

    public static (int ExitCode, string Output) RunDotnetInspectDiff(
        string packageId, string oldVersion, string newVersion)
    {
        // We try the installed global tool first; fall back to `dnx dotnet-inspect@0.7.8`.
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
            // dotnet-inspect not on PATH — fall back to dnx-on-demand.
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

    private static (int ExitCode, string Output) Run(ProcessStartInfo psi) => Run(psi, DefaultTimeout);

    private static (int ExitCode, string Output) Run(ProcessStartInfo psi, TimeSpan timeout)
    {
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {psi.FileName}");

        // Drain stdout and stderr concurrently to avoid pipe-buffer deadlock.
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return (
                ExitCode: -1,
                Output: $"⚠️ Process timed out after {timeout.TotalMinutes:0.#} minutes and was killed.");
        }

        // Ensure async streams finished after process exit.
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        var combined = stdout + Environment.NewLine + stderr;

        if (combined.Length > MaxBytes)
            combined = combined.Substring(0, MaxBytes)
                + Environment.NewLine
                + $"⚠️ Output truncated at {MaxBytes / (1024 * 1024)} MB.";

        return (p.ExitCode, combined);
    }
}
