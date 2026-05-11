using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Tests for the `MafCompatibility` MCP tool. Salvaged from the May-6 sketch
/// (tag: pre-phase-o-may6-sketch), but the data + the test live in lockstep
/// via the drift check at the bottom.
/// </summary>
public sealed class CompatibilityToolTests
{
    private readonly CompatibilityTool _tool = new();

    [Theory]
    [InlineData("1.3.0")]
    [InlineData("1.2.0")]
    [InlineData("1.1.0")]
    [InlineData("1.0.0")]
    public void MafCompatibility_KnownVersion_ReturnsMarkdownTable(string version)
    {
        // Arrange / Act
        var result = _tool.MafCompatibility(version);

        // Assert — every row is a Markdown table.
        Assert.Contains("## MAF " + version + " Compatibility", result);
        Assert.Contains("| Dependency", result);
        Assert.Contains(".NET runtime", result);
    }

    [Fact]
    public void MafCompatibility_EmptyVersion_ReturnsError()
    {
        // Arrange / Act / Assert
        Assert.Contains("Error", _tool.MafCompatibility(""), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Error", _tool.MafCompatibility("   "), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafCompatibility_UnknownVersion_ListsKnownVersionsAndPointsAtMatrix()
    {
        // Arrange / Act
        var result = _tool.MafCompatibility("99.0.0");

        // Assert — friendly failure with breadcrumbs.
        Assert.Contains("No compatibility data", result);
        Assert.Contains("Known versions", result);
        Assert.Contains("docs/compatibility-matrix.md", result);
        Assert.Contains("maf-release-watcher", result);
    }

    [Fact]
    public void MafCompatibility_1_3_0_FlagsRemovedPackages()
    {
        // Arrange / Act
        var result = _tool.MafCompatibility("1.3.0");

        // Assert — the 1.3.0 row must mention the DevUI / Hosting removals (high-impact regression risk).
        Assert.Contains("Microsoft.Agents.AI.DevUI", result);
        Assert.Contains("Microsoft.Agents.AI.Hosting", result);
        Assert.Contains("DEVUI_ENABLED", result);
    }

    [Fact]
    public void MafCompatibility_1_3_0_RequiresGeneratorsPackage()
    {
        // Arrange / Act
        var result = _tool.MafCompatibility("1.3.0");

        // Assert — the Generators package is the magic-required dep in 1.3.0; if the
        // matrix forgets to mention it, every executor in the consumer's project
        // breaks at build time.
        Assert.Contains("Microsoft.Agents.AI.Workflows.Generators", result);
        Assert.Contains("required", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.1.0")]
    public void MafCompatibility_PreRelease_FlagsLegacyExecutorPattern(string version)
    {
        // Arrange / Act
        var result = _tool.MafCompatibility(version);

        // Assert — pre-1.3.0 rows must mention the legacy executor surface
        // (`ReflectingExecutor<T>` + `IMessageHandler<TIn,TOut>`) so a user
        // migrating FROM that version knows what's about to break.
        Assert.Contains("ReflectingExecutor", result);
        Assert.Contains("IMessageHandler", result);
    }

    // -------------------------------------------------------------------------
    // Drift test — every key in the static Matrix must appear in the
    // compatibility-matrix.md file as a row. If the matrix doc adds 1.4.0
    // (via the watcher) and the tool data is forgotten, this fails.
    // -------------------------------------------------------------------------

    [Fact]
    public void Matrix_StaticTable_CoversEveryRowInCompatibilityMatrixDoc()
    {
        // Arrange — locate the doc by walking up from the test bin to the repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "compatibility-matrix.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var docPath = Path.Combine(dir!.FullName, "docs", "compatibility-matrix.md");
        var doc = File.ReadAllText(docPath);

        // Act — versions that appear as `| **X.Y.Z** |` rows in the matrix table.
        var pattern = new System.Text.RegularExpressions.Regex(@"\|\s*\*\*([0-9]+\.[0-9]+\.[0-9]+)\*\*\s*\|");
        var docVersions = pattern.Matches(doc)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        // Assert — every doc version has a matching static entry. When the
        // release-watcher adds a new row to the matrix, this test fails until
        // the tool's static data is also updated.
        var missing = docVersions
            .Where(v => !CompatibilityTool.Matrix.ContainsKey(v))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
        Assert.True(missing.Count == 0,
            $"`MafCompatibility` static data is missing rows for: {string.Join(", ", missing)}. " +
            $"Update `CompatibilityTool.Matrix` in lockstep with `docs/compatibility-matrix.md`.");
    }
}
