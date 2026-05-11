using MafAutopilot.Data;
using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class PreUpgradeDryRunToolTests
{
    private readonly RegistryService _registry = new();

    [Fact]
    public void AssessImpact_NoChanges_ReturnsCleanAssessment()
    {
        var parsed = new DiffParseResult([], [], NoChangesDetected: true);
        var assessment = PreUpgradeDryRunTool.AssessImpact(parsed, []);

        Assert.True(assessment.NoChangesDetected);
        Assert.Empty(assessment.HighImpact);
        Assert.Empty(assessment.UnusedBreaking);
        Assert.Equal(0, assessment.AdditiveCount);
    }

    [Fact]
    public void AssessImpact_BreakingChangeReferencedInRepo_GoesToHighImpact()
    {
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("AgentSkillsProvider", "Member '.ctor' signature changed", DiffEntryKind.SignatureChanged, IsObsolete: false)],
            Additive: [],
            NoChangesDetected: false);

        var sources = new[] { ("src/MyApp.cs", "var p = new AgentSkillsProvider();") };

        var assessment = PreUpgradeDryRunTool.AssessImpact(parsed, sources);

        Assert.Single(assessment.HighImpact);
        Assert.Empty(assessment.UnusedBreaking);
        Assert.Equal("AgentSkillsProvider", assessment.HighImpact[0].Type);
        Assert.NotEmpty(assessment.HighImpact[0].Hits);
    }

    [Fact]
    public void AssessImpact_BreakingChangeNOTReferenced_GoesToUnusedBreaking()
    {
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("UnrelatedType", "Member 'X' was removed", DiffEntryKind.Removed, IsObsolete: false)],
            Additive: [],
            NoChangesDetected: false);

        var sources = new[] { ("src/MyApp.cs", "class Foo { public void Bar() { } }") };

        var assessment = PreUpgradeDryRunTool.AssessImpact(parsed, sources);

        Assert.Empty(assessment.HighImpact);
        Assert.Single(assessment.UnusedBreaking);
        Assert.Equal("UnrelatedType", assessment.UnusedBreaking[0].Type);
    }

    [Fact]
    public void AssessImpact_AdditiveCount_IsPreserved()
    {
        var parsed = new DiffParseResult(
            Breaking: [],
            Additive: [
                new DiffEntry("NewType1", "Type was added", DiffEntryKind.Added, false),
                new DiffEntry("NewType2", "Type was added", DiffEntryKind.Added, false),
                new DiffEntry("NewType3", "Type was added", DiffEntryKind.Added, false),
            ],
            NoChangesDetected: false);

        var assessment = PreUpgradeDryRunTool.AssessImpact(parsed, []);

        Assert.Equal(3, assessment.AdditiveCount);
        Assert.Empty(assessment.HighImpact);
        Assert.Empty(assessment.UnusedBreaking);
    }

    [Fact]
    public void AssessImpact_MultipleFilesReferencingSameType_AllHitsCollected()
    {
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("FraudExecutor", "Type was removed", DiffEntryKind.Removed, false)],
            Additive: [],
            NoChangesDetected: false);

        var sources = new[]
        {
            ("src/A.cs", "var x = new FraudExecutor();"),
            ("src/B.cs", "public class Wiring { public void Wire() { var y = new FraudExecutor(); } }"),
        };

        var assessment = PreUpgradeDryRunTool.AssessImpact(parsed, sources);

        Assert.Single(assessment.HighImpact);
        var hits = assessment.HighImpact[0].Hits;
        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.File == "src/A.cs");
        Assert.Contains(hits, h => h.File == "src/B.cs");
    }

    [Fact]
    public void AssessImpact_RegistryMatch_FillsRegistryHint()
    {
        // FAN-IN-001 covers AddFanInBarrierEdge; check that a diff entry mentioning
        // that type pulls in the registry id.
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("WorkflowBuilder", "Member 'AddFanInBarrierEdge' signature changed", DiffEntryKind.SignatureChanged, false)],
            Additive: [],
            NoChangesDetected: false);

        var sources = new[] { ("src/Use.cs", "builder.AddFanInBarrierEdge(sources, target);") };

        var assessment = PreUpgradeDryRunTool.AssessImpact(parsed, sources, _registry);

        Assert.Single(assessment.HighImpact);
        Assert.NotNull(assessment.HighImpact[0].RegistryEntryId);
    }

    // -------------------------------------------------------------------------
    // ExtractSymbols
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractSymbols_TypeName_AlwaysIncluded()
    {
        var entry = new DiffEntry("MyType", "Some body without quotes", DiffEntryKind.Other, false);
        var symbols = PreUpgradeDryRunTool.ExtractSymbols(entry).ToList();
        Assert.Contains("MyType", symbols);
    }

    [Fact]
    public void ExtractSymbols_QuotedMemberName_IsExtracted()
    {
        var entry = new DiffEntry("OuterType", "Member 'Foo' signature changed", DiffEntryKind.SignatureChanged, false);
        var symbols = PreUpgradeDryRunTool.ExtractSymbols(entry).ToList();
        Assert.Contains("OuterType", symbols);
        Assert.Contains("Foo", symbols);
    }

    // -------------------------------------------------------------------------
    // MCP entry — input validation
    // -------------------------------------------------------------------------

    [Fact]
    public void MafPreUpgradeDryRun_InvalidPackageId_ReturnsError()
    {
        var tool = new PreUpgradeDryRunTool(_registry);
        var result = tool.MafPreUpgradeDryRun(Path.GetTempPath(), "Foo; rm -rf /", "1.0", "2.0");
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafPreUpgradeDryRun_NonexistentRepo_ReturnsError()
    {
        var tool = new PreUpgradeDryRunTool(_registry);
        var result = tool.MafPreUpgradeDryRun("/path/does/not/exist", "Microsoft.Agents.AI", "1.0.0", "1.3.0");
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }
}
