using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for <see cref="ProcessRunner"/>'s bounded-streaming capture (F-08) and
/// git wrapper (F-09). <c>DrainBoundedAsync</c> / <c>SharedCharBudget</c> were
/// promoted from private to internal so these can exercise the streaming
/// contract directly against an in-memory <see cref="TextReader"/> — spawning a
/// real subprocess that emits 16+ MB just to prove truncation would be slow and
/// environment-dependent for no extra coverage value.
/// </summary>
public sealed class ProcessRunnerTests : IDisposable
{
    [Fact]
    public void DotnetInspectVersion_MatchesRepositoryPin()
    {
        Assert.Equal("0.9.1", ProcessRunner.DotnetInspectVersion);
    }

    [Theory]
    [InlineData("0.9.1")]
    [InlineData("  0.9.1\r\n")]
    [InlineData("0.9.1+891e68b")]
    [InlineData("0.9.1+build.42-linux-x64")]
    public void IsSupportedDotnetInspectVersion_AcceptsExactAndBuildMetadata(string output)
    {
        Assert.True(ProcessRunner.IsSupportedDotnetInspectVersion(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0.9.0")]
    [InlineData("0.9.10")]
    [InlineData("0.9.1-preview.1")]
    [InlineData("v0.9.1")]
    [InlineData("dotnet-inspect 0.9.1")]
    [InlineData("0.9.1+")]
    [InlineData("0.9.1+.metadata")]
    [InlineData("0.9.1+metadata.")]
    [InlineData("0.9.1+metadata..second")]
    [InlineData("0.9.1+méta")]
    public void IsSupportedDotnetInspectVersion_RejectsEverythingElse(string output)
    {
        Assert.False(ProcessRunner.IsSupportedDotnetInspectVersion(output));
    }

    [Fact]
    public void ClassifyDotnetInspectProbe_DistinguishesSupportedMissingMismatchAndFailure()
    {
        Assert.Equal(
            ProcessRunner.DotnetInspectProbeStatus.Supported,
            ProcessRunner.ClassifyDotnetInspectProbe(0, "0.9.1+build.7"));
        Assert.Equal(
            ProcessRunner.DotnetInspectProbeStatus.Missing,
            ProcessRunner.ClassifyDotnetInspectProbe(127, "not available"));
        Assert.Equal(
            ProcessRunner.DotnetInspectProbeStatus.Mismatched,
            ProcessRunner.ClassifyDotnetInspectProbe(0, "0.7.8+891e68b"));
        Assert.Equal(
            ProcessRunner.DotnetInspectProbeStatus.Unverifiable,
            ProcessRunner.ClassifyDotnetInspectProbe(2, "0.9.1"));
        Assert.Equal(
            ProcessRunner.DotnetInspectProbeStatus.Unverifiable,
            ProcessRunner.ClassifyDotnetInspectProbe(0, "unexpected multiline\noutput"));
    }

    [Fact]
    public void SelectDotnetInspectRoute_UsesGlobalOnlyForVerifiedExactVersion()
    {
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.Global,
            ProcessRunner.SelectDotnetInspectRoute(0, "0.9.1+commit.abc", allowExactFallback: false));
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.Global,
            ProcessRunner.SelectDotnetInspectRoute(0, "0.9.1", allowExactFallback: true));
    }

    [Fact]
    public void SelectDotnetInspectRoute_MissingExit127_ReachesExactFallbackOnlyWhenOptedIn()
    {
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.FailClosed,
            ProcessRunner.SelectDotnetInspectRoute(127, "not available", allowExactFallback: false));
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.ExactDnxFallback,
            ProcessRunner.SelectDotnetInspectRoute(127, "not available", allowExactFallback: true));
    }

    [Fact]
    public void SelectDotnetInspectRoute_MismatchAndUnverifiableResultsFailClosedOrFallback()
    {
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.FailClosed,
            ProcessRunner.SelectDotnetInspectRoute(0, "0.7.8+old", allowExactFallback: false));
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.ExactDnxFallback,
            ProcessRunner.SelectDotnetInspectRoute(0, "0.7.8+old", allowExactFallback: true));
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.FailClosed,
            ProcessRunner.SelectDotnetInspectRoute(1, "probe failed", allowExactFallback: false));
        Assert.Equal(
            ProcessRunner.DotnetInspectRoute.ExactDnxFallback,
            ProcessRunner.SelectDotnetInspectRoute(1, "probe failed", allowExactFallback: true));
    }

    [Fact]
    public void CreateDotnetInspectDiffStartInfo_ExactFallbackPinsVersionAndGlobalDoesNotUseDnx()
    {
        var fallback = ProcessRunner.CreateDotnetInspectDiffStartInfo(
            "dnx", "Example.Package", "1.0.0", "2.0.0", throughDnx: true);
        Assert.Equal("dnx", fallback.FileName);
        Assert.Equal("dotnet-inspect@0.9.1", fallback.ArgumentList[0]);
        Assert.Contains("Example.Package@1.0.0..2.0.0", fallback.ArgumentList);

        var global = ProcessRunner.CreateDotnetInspectDiffStartInfo(
            "dotnet-inspect", "Example.Package", "1.0.0", "2.0.0", throughDnx: false);
        Assert.Equal("dotnet-inspect", global.FileName);
        Assert.Equal("diff", global.ArgumentList[0]);
        Assert.DoesNotContain(global.ArgumentList, arg => arg.StartsWith("dotnet-inspect@", StringComparison.Ordinal));
    }

    [Fact]
    public void DescribeUnsupportedDotnetInspect_IsActionableAndDoesNotEchoUnverifiedOutput()
    {
        var mismatch = ProcessRunner.DescribeUnsupportedDotnetInspect(0, "0.7.8+old");
        Assert.Contains("0.7.8+old", mismatch);
        Assert.Contains("0.9.1", mismatch);
        Assert.Contains(ProcessRunner.AllowToolDownloadEnvName, mismatch);

        var unverified = ProcessRunner.DescribeUnsupportedDotnetInspect(
            0, "hostile\nmultiline `output`");
        Assert.Contains("could not be verified", unverified);
        Assert.DoesNotContain("hostile", unverified);
    }

    public void Dispose() =>
        Environment.SetEnvironmentVariable(ProcessRunner.AllowToolDownloadEnvName, null);

    // -------------------------------------------------------------------------
    // F-12 (2026-07-19 security assessment) — the dnx-on-demand fallback must
    // stay off unless explicitly opted into, since MafDiffPackage/
    // MafPreUpgradeDryRun are annotated ReadOnly=true and a client can
    // auto-invoke a ReadOnly tool without confirmation.
    // -------------------------------------------------------------------------

    [Fact]
    public void ToolDownloadAllowed_Unset_DefaultsToFalse()
    {
        Environment.SetEnvironmentVariable(ProcessRunner.AllowToolDownloadEnvName, null);
        Assert.False(ProcessRunner.ToolDownloadAllowed());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("")]
    public void ToolDownloadAllowed_NonAffirmativeValues_False(string value)
    {
        Environment.SetEnvironmentVariable(ProcessRunner.AllowToolDownloadEnvName, value);
        Assert.False(ProcessRunner.ToolDownloadAllowed());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    public void ToolDownloadAllowed_AffirmativeValues_True(string value)
    {
        Environment.SetEnvironmentVariable(ProcessRunner.AllowToolDownloadEnvName, value);
        Assert.True(ProcessRunner.ToolDownloadAllowed());
    }

    [Fact]
    public async Task DrainBoundedAsync_UnderBudget_ReturnsFullTextNotTruncated()
    {
        var budget = new ProcessRunner.SharedCharBudget(1000);
        using var reader = new StringReader("hello world");

        var (text, truncated) = await ProcessRunner.DrainBoundedAsync(reader, budget);

        Assert.Equal("hello world", text);
        Assert.False(truncated);
    }

    [Fact]
    public async Task DrainBoundedAsync_OverBudget_TruncatesAtCapAndReportsTruncated()
    {
        var budget = new ProcessRunner.SharedCharBudget(10);
        using var reader = new StringReader(new string('x', 100));

        var (text, truncated) = await ProcessRunner.DrainBoundedAsync(reader, budget);

        Assert.Equal(10, text.Length);
        Assert.True(truncated);
    }

    [Fact]
    public async Task DrainBoundedAsync_SharedBudgetAcrossTwoStreams_SplitsCapNotDoubled()
    {
        // Mirrors ProcessRunner.Run's actual usage: stdout and stderr drain
        // concurrently against ONE shared budget, so truncation bounds the
        // COMBINED output rather than each stream independently.
        var budget = new ProcessRunner.SharedCharBudget(10);
        using var readerA = new StringReader(new string('a', 6));
        using var readerB = new StringReader(new string('b', 6));

        var taskA = ProcessRunner.DrainBoundedAsync(readerA, budget);
        var taskB = ProcessRunner.DrainBoundedAsync(readerB, budget);
        var (textA, truncatedA) = await taskA;
        var (textB, truncatedB) = await taskB;

        Assert.Equal(10, textA.Length + textB.Length);
        Assert.True(truncatedA || truncatedB);
    }

    [Fact]
    public async Task DrainBoundedAsync_ExactlyAtBudget_NotTruncated()
    {
        var budget = new ProcessRunner.SharedCharBudget(5);
        using var reader = new StringReader("12345");

        var (text, truncated) = await ProcessRunner.DrainBoundedAsync(reader, budget);

        Assert.Equal("12345", text);
        Assert.False(truncated);
    }

    [Fact]
    public void SharedCharBudget_TryConsume_NeverExceedsCapAcrossConcurrentCallers()
    {
        var budget = new ProcessRunner.SharedCharBudget(1000);
        var totalTaken = 0;

        Parallel.For(0, 200, _ =>
        {
            var taken = budget.TryConsume(37);
            Interlocked.Add(ref totalTaken, taken);
        });

        Assert.Equal(1000, totalTaken);
    }

    // -------------------------------------------------------------------------
    // F-09 — GetChangedCsFiles previously read stdout to completion synchronously
    // before touching stderr, risking a deadlock before the 30s timeout was ever
    // evaluated. It now goes through ProcessRunner.RunGit's concurrent drain.
    // This is an end-to-end sanity check against a real (tiny) git repo.
    // -------------------------------------------------------------------------

    [Fact]
    public void RunGit_AgainstRealRepo_ReturnsExpectedOutput()
    {
        var (exitCode, output) = ProcessRunner.RunGit(
            Path.GetTempPath(), TimeSpan.FromSeconds(10), "--version");

        Assert.Equal(0, exitCode);
        Assert.Contains("git version", output, StringComparison.OrdinalIgnoreCase);
    }
}
