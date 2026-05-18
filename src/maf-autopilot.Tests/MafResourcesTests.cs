using MafAutopilot.Resources;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Tests for MafResources — the read-only knowledge surface exposed at maf:// URIs.
/// The allowlist + embedded-resource access pattern means the threat model is small,
/// but path-traversal and casing edge cases still warrant explicit guards.
/// </summary>
public class MafResourcesTests
{
    // -------------------------------------------------------------------------
    // Static resources
    // -------------------------------------------------------------------------

    [Fact]
    public void GetConstraints_ReturnsNonEmptyMarkdown()
    {
        var body = MafResources.GetConstraints();
        Assert.False(string.IsNullOrWhiteSpace(body));
        Assert.Contains("Hard Constraints", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRegistry_ReturnsNonEmptyYaml()
    {
        var body = MafResources.GetRegistry();
        Assert.False(string.IsNullOrWhiteSpace(body));
        Assert.Contains("schema_version:", body);
        Assert.Contains("entries:", body);
    }

    [Fact]
    public void GetGuide_ReturnsNonEmptyMarkdown()
    {
        var body = MafResources.GetGuide();
        Assert.False(string.IsNullOrWhiteSpace(body));
        // The guide opens with a top-level heading; assert a generic property
        // rather than exact wording so cosmetic edits don't break the test.
        Assert.Contains("MAF", body);
    }

    // -------------------------------------------------------------------------
    // Template resource: skill allowlist
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("cs0618-hunter")]
    [InlineData("dotnet-inspect")]
    [InlineData("maf-anti-pattern-scanner")]
    [InlineData("maf-fan-out-validator")]
    [InlineData("maf-issue-reporter")]
    [InlineData("maf-migration-guide")]
    [InlineData("maf-migration-plan-creator")]
    [InlineData("maf-migration-retrospective")]
    [InlineData("maf-obsolete-api-registry")]
    [InlineData("maf-release-watcher")]
    [InlineData("maf-workflow-smoke-tester")]
    [InlineData("nuget-diff-analyzer")]
    public void GetSkill_AllAllowlistedNames_Resolve(string name)
    {
        var body = MafResources.GetSkill(name);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Theory]
    [InlineData("CS0618-HUNTER")]    // case-insensitive allowlist
    [InlineData("Dotnet-Inspect")]
    public void GetSkill_CaseInsensitive_Resolves(string name)
    {
        var body = MafResources.GetSkill(name);
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    // -------------------------------------------------------------------------
    // maf://rules — live catalog (kills doc drift; reviewer-recommended)
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRules_ListsAntiPatternScannerIds()
    {
        var body = MafResources.GetRules();
        Assert.Contains("MAF-AP-SEC-001", body, StringComparison.Ordinal);
        Assert.Contains("MAF-AP-CONC-001", body, StringComparison.Ordinal);
        Assert.Contains("MAF-AP-OBS-001", body, StringComparison.Ordinal);
        Assert.Contains("MAF-AP-AGENT-001", body, StringComparison.Ordinal);
        Assert.Contains("MAF-AP-WF-001", body, StringComparison.Ordinal);
        Assert.Contains("MAF-AP-MID-001", body, StringComparison.Ordinal);
        Assert.Contains("MAF-AP-DEVUI-001", body, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRules_ListsPromptLintIds()
    {
        var body = MafResources.GetRules();
        Assert.Contains("PROMPT-001", body, StringComparison.Ordinal);
        Assert.Contains("PROMPT-002", body, StringComparison.Ordinal);
        Assert.Contains("PROMPT-003", body, StringComparison.Ordinal);
        Assert.Contains("PROMPT-004", body, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRules_ListsRoslynAnalyzerIds()
    {
        var body = MafResources.GetRules();
        Assert.Contains("MAF001", body, StringComparison.Ordinal);
        Assert.Contains("MAF002", body, StringComparison.Ordinal);
        Assert.Contains("MAF003", body, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRules_EveryScannerRuleAppearsInCatalog()
    {
        // Doc-↔-code drift property test: the maf://rules resource must list every
        // rule that AntiPatternScannerTool.AllRules actually ships. If a future rule
        // is added without updating the catalog, this fails.
        var body = MafResources.GetRules();
        foreach (var rule in MafAutopilot.Tools.AntiPatternScannerTool.AllRules)
            Assert.Contains(rule.Id, body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nonexistent-skill")]
    [InlineData("")]
    [InlineData("   ")]
    // Path-traversal attempts must be rejected by the allowlist, not silently allowed:
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("/etc/passwd")]
    [InlineData("cs0618-hunter/../../secret")]
    public void GetSkill_RejectsUnknownAndTraversalAttempts(string name)
    {
        Assert.Throws<ArgumentException>(() => MafResources.GetSkill(name));
    }
}
