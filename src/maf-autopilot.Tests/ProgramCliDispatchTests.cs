using System.Diagnostics;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Round-4 review fixup — Program.cs's top-level command dispatch (which
/// subcommand string maps to which handler, and what happens when none
/// match) had zero test coverage of any kind, confirmed by a review agent
/// via real execution: an unrecognized subcommand fell through to MCP-server
/// startup (a silent hang with stdin open), and `doctor`/`badge`/`autofix-all`/
/// `migrate-scan` all exited 0 even when they printed an error. Both are now
/// fixed in Program.cs; these tests spawn the actual built CLI as a
/// subprocess (the only way to exercise top-level-statement dispatch logic)
/// to pin both fixes against regression.
/// </summary>
public sealed class ProgramCliDispatchTests
{
    // Assembly.Location resolves to wherever the CLR loaded maf-doctor.dll
    // from for this test run — standard MSBuild project-reference behavior
    // copies it into this test project's own output directory.
    private static readonly string DllPath = typeof(Tools.PathGuard).Assembly.Location;

    private static (int ExitCode, string StdOut, string StdErr) Run(TimeSpan timeout, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(DllPath);
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start maf-doctor.dll");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
            Assert.Fail($"maf-doctor {string.Join(' ', args)} did not exit within {timeout.TotalSeconds}s — hung.");
        }

        return (p.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    [Fact]
    public void UnrecognizedCommand_ExitsNonZeroWithoutFallingThroughToMcpMode()
    {
        var (exitCode, _, stderr) = Run(TimeSpan.FromSeconds(15), "bogus-command");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Unknown command", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BareNewCommand_PrintsUsageAndExitsNonZero()
    {
        var (exitCode, _, stderr) = Run(TimeSpan.FromSeconds(15), "new");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Usage:", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("doctor")]
    [InlineData("badge")]
    [InlineData("autofix-all")]
    [InlineData("migrate-scan")]
    public void PathTakingCommand_NonexistentPath_ExitsNonZero(string command)
    {
        var badPath = Path.Combine(Path.GetTempPath(), "maf-doctor-cli-test-does-not-exist-" + Guid.NewGuid().ToString("N"));

        var (exitCode, _, _) = Run(TimeSpan.FromSeconds(30), command, badPath);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Doctor_ValidPath_ExitsZero()
    {
        // Never scan the shared temp ROOT: on a developer machine it may contain
        // thousands of unrelated files, and the net8/9/10 test hosts run this test
        // concurrently. Use an isolated empty repo-shaped directory instead.
        var dir = Path.Combine(Path.GetTempPath(), "maf-doctor-valid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var (exitCode, stdout, _) = Run(TimeSpan.FromSeconds(30), "doctor", dir);

            Assert.Equal(0, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(stdout));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ---- Phase 2 ----

    [Fact]
    public void Doctor_UnknownFlag_ExitsUsageError() // WM-04
    {
        var (exitCode, _, stderr) = Run(TimeSpan.FromSeconds(30), "doctor", Path.GetTempPath(), "--bogus");
        Assert.Equal(2, exitCode);
        Assert.Contains("--bogus", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Doctor_ExcludeSwallowingFlag_ExitsUsageError() // REP-05
    {
        var (exitCode, _, _) = Run(TimeSpan.FromSeconds(30), "doctor", Path.GetTempPath(), "--exclude", "--json");
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void Doctor_InvalidPath_HumanErrorGoesToStdErr_NotStdOut() // REP-08
    {
        var badPath = Path.Combine(Path.GetTempPath(), "maf-doctor-nope-" + Guid.NewGuid().ToString("N"));
        var (exitCode, stdout, stderr) = Run(TimeSpan.FromSeconds(30), "doctor", badPath);
        Assert.Equal(1, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stderr)); // error on stderr...
        Assert.True(string.IsNullOrWhiteSpace(stdout));   // ...not stdout
    }

    [Fact]
    public void Doctor_FailOnA_GateAlwaysFails_ExitsThree() // REP-09 (fail path)
    {
        var dir = Path.Combine(Path.GetTempPath(), "maf-doctor-failon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var (exitCode, _, _) = Run(TimeSpan.FromSeconds(30), "doctor", dir, "--fail-on", "A");
            Assert.Equal(3, exitCode); // any grade is at-or-below A → gate fails
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Doctor_FailOnF_CleanRepo_GatePasses_ExitsZero() // REP-09 (pass path)
    {
        var dir = Path.Combine(Path.GetTempPath(), "maf-doctor-failonf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Clean.cs"), "public class Clean { public int X => 1; }");
            var (exitCode, _, _) = Run(TimeSpan.FromSeconds(30), "doctor", dir, "--fail-on", "F");
            Assert.Equal(0, exitCode); // a clean repo grades above F → gate passes
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
