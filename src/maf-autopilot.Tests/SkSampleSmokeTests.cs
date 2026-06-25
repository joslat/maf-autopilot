using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// End-to-end smoke test for the SK→MAF detector against the on-disk fixture at
/// <c>samples/sk-sample/</c>. Pins the advertised demo: a Semantic Kernel app whose
/// constructs span all three strategies (🌉 bridge / 🔁 rewrite / 🏗 re-architect)
/// scans to a reproducible inventory + a HARD verdict. If a future detector change
/// regresses on a real project layout, this fails before it ships.
/// </summary>
public class SkSampleSmokeTests
{
    private static string ResolveSamplePath(string sampleRelativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        var samplePath = Path.Combine(dir!.FullName, sampleRelativePath);
        Assert.True(Directory.Exists(samplePath), $"Sample folder not found: {samplePath}");
        return samplePath;
    }

    [Fact]
    public void MigrateScan_OnSkSample_DetectsAllThreeStrategies_VerdictHard()
    {
        var samplePath = ResolveSamplePath(Path.Combine("samples", "sk-sample"));

        var result = SemanticKernelDetectorTool.Scan(samplePath);

        Assert.True(result.SemanticKernelDetected);

        // Package detection (explicit Version on the PackageReference).
        Assert.Contains(result.Packages, p => p.Id == "Microsoft.SemanticKernel" && p.Version == "1.45.0");

        // Exact strategy counts for this fixed-shape fixture — catches over-counting and a
        // construct silently migrating between strategies, not just a bucket going empty.
        // The fixture has a known layout (WeatherTools / ClaimAgent / Triage); when it changes,
        // update these in the same commit.
        Assert.Equal(1, result.Bridgeable);   // [KernelFunction] (one kind, ×2 occurrences)
        // 8 Rewrite RECORDS (counts are per-file, so ChatCompletionAgent appears twice —
        // once in ClaimAgent.cs, once in Triage.cs): Kernel, ChatCompletionAgent (ClaimAgent),
        // ChatCompletionAgent (Triage), AgentThread, ChatHistoryAgentThread,
        // *PromptExecutionSettings, AgentInvokeOptions, Invoke*Async.
        Assert.Equal(8, result.Rewrite);
        Assert.Equal(2, result.Rearchitect);  // AgentGroupChat + FunctionCallingStepwisePlanner
        Assert.Equal(15, result.TotalOccurrences);
        Assert.Equal("HARD", result.Verdict);

        // Spot-check a representative construct from each strategy.
        Assert.Contains(result.Constructs, c => c.Kind.StartsWith("[KernelFunction]", System.StringComparison.Ordinal));
        Assert.Contains(result.Constructs, c => c.Kind.StartsWith("SK agent (`ChatCompletionAgent`", System.StringComparison.Ordinal));
        Assert.Contains(result.Constructs, c => c.Kind.StartsWith("Thread (`ChatHistoryAgentThread`", System.StringComparison.Ordinal));
        Assert.Contains(result.Constructs, c => c.Kind.StartsWith("Multi-agent group chat", System.StringComparison.Ordinal));
        Assert.Contains(result.Constructs, c => c.Kind.StartsWith("Planner", System.StringComparison.Ordinal));
    }
}
