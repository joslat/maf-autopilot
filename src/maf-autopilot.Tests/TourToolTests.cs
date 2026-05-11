using System.Reflection;
using MafAutopilot.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace MafAutopilot.Tests;

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
        Assert.Contains("# 🗺️ maf-autopilot — capability tour", output);
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
