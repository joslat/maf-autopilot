using System.Reflection;
using System.Text.RegularExpressions;
using MafDoctor.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for the `MafTour` discovery tool. The catalogue is hand-curated;
/// the drift test below enforces it stays in lockstep with the live tool surface
/// — every `[McpServerTool]` in the assembly MUST appear in the catalogue,
/// otherwise the test fails loudly. New tool? Add the row at the same time.
/// </summary>
public sealed class TourToolTests
{
    private readonly TourTool _tool = new();

    [Fact]
    public void MafTour_AllSection_ContainsEveryCategory()
    {
        // Arrange / Act
        var output = _tool.MafTour("all");

        // Assert — every section header is present.
        Assert.Contains("## 🔧 MCP tools", output);
        Assert.Contains("## 🤖 Copilot Chat agents", output);
        Assert.Contains("## 📄 MCP resources", output);
        Assert.Contains("## 💬 MCP prompts", output);
    }

    [Theory]
    [InlineData("tools")]
    [InlineData("agents")]
    [InlineData("resources")]
    [InlineData("prompts")]
    public void MafTour_SingleSection_OnlyContainsThatSection(string section)
    {
        // Arrange / Act
        var output = _tool.MafTour(section);

        // Assert — the requested section is present, and at least one of the others is absent.
        Assert.Contains("# 🗺️ maf-doctor — capability tour", output);
        var others = new[] { "🔧 MCP tools", "🤖 Copilot Chat agents", "📄 MCP resources", "💬 MCP prompts" }
            .Where(h => !h.Contains(GetEmojiForSection(section), StringComparison.Ordinal))
            .ToList();
        foreach (var otherHeader in others)
            Assert.DoesNotContain($"## {otherHeader}", output);
    }

    private static string GetEmojiForSection(string section) => section switch
    {
        "tools" => "🔧",
        "agents" => "🤖",
        "resources" => "📄",
        "prompts" => "💬",
        _ => "",
    };

    [Theory]
    [InlineData("invalid")]
    [InlineData("123")]
    public void MafTour_InvalidSection_ReturnsError(string section)
    {
        // Arrange / Act / Assert
        Assert.Contains("Error", _tool.MafTour(section), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafTour_DefaultSection_ReturnsAll()
    {
        // Arrange / Act
        var defaultOutput = _tool.MafTour();
        var explicitAll = _tool.MafTour("all");

        // Assert
        Assert.Equal(explicitAll, defaultOutput);
    }

    [Fact]
    public void MafTour_PrimaryAgent_IsMarked()
    {
        // Arrange / Act
        var output = _tool.MafTour("agents");

        // Assert — the primary marker appears for `@maf` only.
        Assert.Contains("`@maf`", output);
        Assert.Contains("primary — start here", output);
    }

    // -------------------------------------------------------------------------
    // DRIFT TEST — every [McpServerTool]-decorated method in the assembly
    // MUST appear in the TourTool catalogue. Adding a new tool without
    // adding the catalogue row will fail this test (and CI).
    // -------------------------------------------------------------------------

    [Fact]
    public void Catalogue_ListsEveryMcpServerTool_NoDrift()
    {
        // Arrange — find every public method decorated with [McpServerTool].
        var assembly = typeof(TourTool).Assembly;
        var mcpToolMethods = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Act — collect names from the catalogue.
        var cataloguedNames = TourTool.ToolCatalogue.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        // Assert — sets must match exactly.
        var missing = mcpToolMethods.Except(cataloguedNames).OrderBy(s => s).ToList();
        var extra = cataloguedNames.Except(mcpToolMethods).OrderBy(s => s).ToList();

        Assert.True(missing.Count == 0,
            $"MafTour catalogue is missing entries for live MCP tools: {string.Join(", ", missing)}. " +
            "Add each one to TourTool.ToolCatalogue.");
        Assert.True(extra.Count == 0,
            $"MafTour catalogue lists tools that don't exist as [McpServerTool] methods: {string.Join(", ", extra)}. " +
            "Remove each one, or implement it.");
    }

    [Fact]
    public void AgentCatalogue_MatchesAgentFiles_NoDrift()
    {
        // The agent catalogue is hand-curated (the @-mention personas are markdown files,
        // not reflectable types). Guard it against drift the same way tools/prompts are:
        // every .github/agents/<X>.agent.md must have an `@<X>` catalogue entry and vice
        // versa. A renamed/added/removed agent file without a catalogue edit fails here.
        var agentsDir = ResolveRepoSubdir(Path.Combine(".github", "agents"));
        var fileNames = Directory.GetFiles(agentsDir, "*.agent.md")
            .Select(f => "@" + Path.GetFileName(f)[..^".agent.md".Length])
            .ToHashSet(StringComparer.Ordinal);

        var catalogued = TourTool.AgentCatalogue.Select(a => a.Name).ToHashSet(StringComparer.Ordinal);

        var missing = fileNames.Except(catalogued).OrderBy(s => s).ToList();
        var extra = catalogued.Except(fileNames).OrderBy(s => s).ToList();

        Assert.True(missing.Count == 0,
            $"AgentCatalogue is missing entries for agent files: {string.Join(", ", missing)}. Add each to TourTool.AgentCatalogue.");
        Assert.True(extra.Count == 0,
            $"AgentCatalogue lists agents with no .agent.md file: {string.Join(", ", extra)}. Remove each, or add the file.");
    }

    /// <summary>Walks up from the test bin dir to the repo root, then to a known subdirectory.</summary>
    private static string ResolveRepoSubdir(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "agents")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, relative);
        Assert.True(Directory.Exists(path), $"Expected directory not found: {path}");
        return path;
    }

    [Fact]
    public void Catalogue_ListsEveryMcpServerPrompt_NoDrift()
    {
        // Same drift guard for prompts — every [McpServerPrompt] method must
        // appear in the prompt catalogue (and vice versa).
        var assembly = typeof(TourTool).Assembly;
        var promptNames = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerPromptTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => m.GetCustomAttribute<McpServerPromptAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Name!)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.Ordinal);

        var catalogued = TourTool.PromptCatalogue.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        var missing = promptNames.Except(catalogued).OrderBy(s => s).ToList();
        var extra = catalogued.Except(promptNames).OrderBy(s => s).ToList();

        Assert.True(missing.Count == 0,
            $"MafTour catalogue is missing entries for live MCP prompts: {string.Join(", ", missing)}. " +
            "Add each one to TourTool.PromptCatalogue.");
        Assert.True(extra.Count == 0,
            $"MafTour catalogue lists prompts that don't exist as [McpServerPrompt] methods: {string.Join(", ", extra)}.");
    }

    [Fact]
    public void Catalogue_ListsEveryMcpServerResource_NoDrift()
    {
        // PRAG-001 guard: resources were the only catalogue without a drift test — and the one
        // this feature changed (added maf://migrate-from). Every [McpServerResource] must appear
        // in ResourceCatalogue and vice-versa. Compare on the scheme+path PREFIX (strip from the
        // first '{' or '?') because the catalogue intentionally renders the human-readable example
        // form (maf://skills?name=<X>) while the UriTemplate is the RFC-6570 form (maf://skills{?name}).
        var assembly = typeof(TourTool).Assembly;
        var templates = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerResourceTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => m.GetCustomAttribute<McpServerResourceAttribute>())
            .Where(a => a?.UriTemplate is not null)
            .Select(a => UriPrefix(a!.UriTemplate!))
            .ToHashSet(StringComparer.Ordinal);

        var catalogued = TourTool.ResourceCatalogue.Select(r => UriPrefix(r.Uri)).ToHashSet(StringComparer.Ordinal);

        var missing = templates.Except(catalogued).OrderBy(s => s).ToList();
        var extra = catalogued.Except(templates).OrderBy(s => s).ToList();

        Assert.True(missing.Count == 0,
            $"ResourceCatalogue is missing entries for live [McpServerResource]s: {string.Join(", ", missing)}. " +
            "Add each one to TourTool.ResourceCatalogue.");
        Assert.True(extra.Count == 0,
            $"ResourceCatalogue lists resources with no matching [McpServerResource]: {string.Join(", ", extra)}.");

        static string UriPrefix(string uri)
        {
            var cut = uri.IndexOfAny(['{', '?']);
            return cut >= 0 ? uri[..cut] : uri;
        }
    }

    [Fact]
    public void ResourceCatalogue_SkillsCount_MatchesAllowlist()
    {
        // Pin the free-floating "N skills available" literal so it can't drift from the allowlist.
        var skillsRow = TourTool.ResourceCatalogue.Single(r => r.Uri.StartsWith("maf://skills", System.StringComparison.Ordinal));
        var n = Regex.Match(skillsRow.Description, @"(\d+)\s+skills").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(n), $"no skill count found in ResourceCatalogue skills description: '{skillsRow.Description}'");
        Assert.Equal(MafDoctor.Resources.MafResources.AllowedSkillNames.Count, int.Parse(n));
    }

    [Fact]
    public void BuildFullCatalogue_StaticMethod_RoundTripsThroughMafTour()
    {
        // Arrange / Act — the resource path (maf://help) and the tool path (MafTour)
        // must emit identical content. The resource calls BuildFullCatalogue directly;
        // MafTour("all") calls it via the entry method.
        var fromResource = TourTool.BuildFullCatalogue();
        var fromTool = _tool.MafTour("all");

        // Assert
        Assert.Equal(fromResource, fromTool);
    }
}
