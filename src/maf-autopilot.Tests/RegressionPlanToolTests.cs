using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Phase W.12 — tests for <see cref="RegressionPlanTool"/>.
///
/// Covers the version-parsing helper, Mermaid node-ID sanitisation, and
/// the end-to-end roadmap rendering for representative version pairs.
/// </summary>
public class RegressionPlanToolTests
{
    private readonly RegressionPlanTool _tool = new();

    // -------------------------------------------------------------------------
    // TryParseVersion — handle prerelease tags
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1.3.0", true)]
    [InlineData("1.3.0-alpha-5", true)]   // prerelease tag stripped, core parsed
    [InlineData("1.10.0", true)]           // double-digit minor
    [InlineData("", false)]
    [InlineData("not-a-version", false)]
    public void TryParseVersion_HandlesPrerelease(string raw, bool expected)
    {
        Assert.Equal(expected, RegressionPlanTool.TryParseVersion(raw, out _));
    }

    // -------------------------------------------------------------------------
    // SanitizeNodeId — strip dots + prefix
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1.0.0", "v100")]
    [InlineData("1.3.0", "v130")]
    [InlineData("1.10.0", "v1100")]
    [InlineData("1.3.0-alpha-5", "v130")]
    public void SanitizeNodeId_StripsDotsAndPrerelease(string version, string expected)
    {
        Assert.Equal(expected, RegressionPlanTool.SanitizeNodeId(version));
    }

    // -------------------------------------------------------------------------
    // Plan generation — end-to-end
    // -------------------------------------------------------------------------

    [Fact]
    public void Plan_FromEqualsTo_ReturnsEmptyNote()
    {
        var result = _tool.MafGenerateRegressionPlan("1.3.0", "1.3.0");
        Assert.Contains("Empty plan", result);
    }

    [Fact]
    public void Plan_FromHigherThanTo_ReturnsError()
    {
        var result = _tool.MafGenerateRegressionPlan("1.5.0", "1.0.0");
        Assert.Contains("HIGHER", result);
        Assert.Contains("rollback", result);
    }

    [Fact]
    public void Plan_InvalidFromVersion_ReturnsError()
    {
        var result = _tool.MafGenerateRegressionPlan("not-semver", "1.5.0");
        Assert.Contains("not a valid SemVer", result);
    }

    [Fact]
    public void Plan_FromOneZeroToFiveZero_EmitsFiveStepLadder()
    {
        var result = _tool.MafGenerateRegressionPlan("1.0.0", "1.5.0");

        // Should contain a Mermaid block.
        Assert.Contains("```mermaid", result);
        Assert.Contains("flowchart LR", result);

        // Should include every intermediate version as a node label.
        Assert.Contains("1.1.0", result);
        Assert.Contains("1.2.0", result);
        Assert.Contains("1.3.0", result);
        Assert.Contains("1.4.0", result);
        Assert.Contains("1.5.0", result);

        // Should mention the source version as starting point.
        Assert.Contains("1.0.0", result);
        Assert.Contains("source", result);

        // Should have a per-step changes table.
        Assert.Contains("Per-step changes", result);

        // Should link to the per-version migration guides.
        Assert.Contains("maf-1.3.0-migration-guide.md", result);
    }

    [Fact]
    public void Plan_FromTwoZeroToThreeZero_EmitsSingleStep()
    {
        // 1.2 → 1.3 should produce a 1-step plan: just the 1.3 node.
        var result = _tool.MafGenerateRegressionPlan("1.2.0", "1.3.0");
        Assert.Contains("1 version step(s)", result);
        Assert.Contains("1.3.0", result);
    }

    [Fact]
    public void Plan_OneStep_IncludesRegistryChangeCount()
    {
        // 1.2 → 1.3 should mention the count of registry entries that fire
        // at 1.3 (the MAF130-* entries). Several of the MAF130-* entries
        // are marked applies_to_codebases: "pre-1.0.0" and so don't fire
        // here, but the non-pre-1.0 ones (FAN-IN-001, EXEC-001/002,
        // SESSION-001, INSTRUCTIONS-001, MIDDLEWARE-001) should.
        var result = _tool.MafGenerateRegressionPlan("1.2.0", "1.3.0");
        Assert.Contains("change(s)", result);
        Assert.Contains("MAF130", result);   // at least one MAF130-* ID in the table
    }

    [Fact]
    public void Plan_OutputIsValidMarkdown_WithExecutionInstructions()
    {
        var result = _tool.MafGenerateRegressionPlan("1.0.0", "1.5.0");
        Assert.Contains("# Migration roadmap", result);
        Assert.Contains("## Migration graph", result);
        Assert.Contains("## Per-step changes", result);
        Assert.Contains("To execute this plan", result);
        Assert.Contains("maf-doctor doctor", result);
        Assert.Contains("MafAutoFix", result);
    }
}
