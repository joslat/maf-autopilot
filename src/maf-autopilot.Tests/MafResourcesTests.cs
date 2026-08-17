using System.Reflection;
using MafDoctor.Resources;
using Xunit;

namespace MafDoctor.Tests;

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
    public void GetGuide_MatchesCheckedInCumulativeGuideAndCurrentTrackedVersion()
    {
        var root = ResolveRepoRoot();
        var expected = File.ReadAllText(Path.Combine(root, "guides", "maf-current-migration-guide.md"));
        var trackedVersion = File.ReadAllText(Path.Combine(root, ".maf-version")).Trim();
        var actual = MafResources.GetGuide();

        Assert.False(string.IsNullOrWhiteSpace(actual));
        Assert.Equal(expected, actual);
        Assert.Contains("# MAF Migration Guide — Cumulative", actual, StringComparison.Ordinal);
        Assert.Contains($"## Migrating to MAF {trackedVersion}", actual, StringComparison.Ordinal);
        Assert.Contains($"introduced: {trackedVersion}", actual, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // Template resource: skill allowlist
    // -------------------------------------------------------------------------

    [Fact]
    public void GetSkill_EveryAllowlistedName_Resolves()
    {
        // Source of truth = the allowlist itself (which the drift test below pins to the
        // on-disk skill folders). Every allowlisted skill must resolve to embedded content;
        // a name in the allowlist but missing from the csproj <EmbeddedResource> list throws.
        Assert.NotEmpty(MafResources.AllowedSkillNames);
        foreach (var name in MafResources.AllowedSkillNames)
            Assert.False(string.IsNullOrWhiteSpace(MafResources.GetSkill(name)), $"skill '{name}' did not resolve");
    }

    [Fact]
    public void AllowedSkillNames_MatchSkillFolders_NoDrift()
    {
        // Guard the allowlist against the on-disk .github/skills/<name>/SKILL.md folders:
        // a skill folder added/renamed without an allowlist edit (or vice versa) fails here.
        var skillsDir = ResolveSkillsDir();
        var folders = Directory.GetDirectories(skillsDir)
            .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
            .Select(d => Path.GetFileName(d))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allow = MafResources.AllowedSkillNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = folders.Except(allow).OrderBy(s => s).ToList();
        var extra = allow.Except(folders).OrderBy(s => s).ToList();

        Assert.True(missing.Count == 0, $"Skill folders not in AllowedSkillNames: {string.Join(", ", missing)}");
        Assert.True(extra.Count == 0, $"AllowedSkillNames entries with no skill folder: {string.Join(", ", extra)}");
    }

    [Fact]
    public void GetSkill_DescriptionLists_EveryAllowlistedName()
    {
        // The `name=` enumeration in the [Description] attribute is user/LLM-facing and is a
        // hand-maintained literal — pin it to AllowedSkillNames so it can't silently drift.
        var desc = typeof(MafResources).GetMethod(nameof(MafResources.GetSkill))!
            .GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()!.Description;
        foreach (var name in MafResources.AllowedSkillNames)
            Assert.Contains(name, desc, StringComparison.Ordinal);
    }

    private static string ResolveSkillsDir()
        => Path.Combine(ResolveRepoRoot(), ".github", "skills");

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
            && (!File.Exists(Path.Combine(dir.FullName, ".maf-version"))
                || !File.Exists(Path.Combine(dir.FullName, "guides", "maf-current-migration-guide.md"))))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
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
        foreach (var rule in MafDoctor.Tools.AntiPatternScannerTool.AllRules)
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
