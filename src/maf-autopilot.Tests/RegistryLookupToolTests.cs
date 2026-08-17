using MafDoctor.Data;
using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

public class RegistryLookupToolTests
{
    private readonly RegistryService _registry = new();
    private readonly RegistryLookupTool _tool;

    public RegistryLookupToolTests()
    {
        _tool = new RegistryLookupTool(_registry);
    }

    // -------------------------------------------------------------------------
    // MafRegistryLookup
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Lookup_EmptyId_ReturnsErrorMessage(string id)
    {
        var result = _tool.MafRegistryLookup(id);
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("MAF130-FAN-IN-001")]
    [InlineData("maf130-fan-in-001")]   // case-insensitive
    [InlineData("  MAF130-FAN-IN-001  ")] // trim
    public void Lookup_KnownId_ReturnsFormattedEntry(string id)
    {
        var result = _tool.MafRegistryLookup(id);
        Assert.Contains("MAF130-FAN-IN-001", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Registry entry", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lookup_UnknownId_ReturnsNotFoundWithAvailableList()
    {
        var result = _tool.MafRegistryLookup("MAF130-DOES-NOT-EXIST");
        Assert.Contains("No registry entry found", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Available IDs", result, StringComparison.OrdinalIgnoreCase);
        // The error must list at least one real ID so the caller can recover.
        Assert.Contains("MAF130-", result, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // MafRegistryList
    // -------------------------------------------------------------------------

    [Fact]
    public void List_ReturnsMarkdownTableWithAllEntries()
    {
        var result = _tool.MafRegistryList();
        Assert.Contains($"MAF {_registry.TargetVersion} Obsolete-API Registry", result, StringComparison.Ordinal);
        // Markdown table rows for known entries
        Assert.Contains("MAF130-FAN-IN-001", result, StringComparison.Ordinal);
        Assert.Contains("MAF130-THREAD-001", result, StringComparison.Ordinal);
        // The count line and "Use MafRegistryLookup" footer
        Assert.Contains("entries", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MafRegistryLookup", result, StringComparison.OrdinalIgnoreCase);
    }
}
