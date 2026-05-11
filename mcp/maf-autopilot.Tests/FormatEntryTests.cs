using MafAutopilot.Data;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Pinpoint tests for <see cref="RegistryService.FormatEntry"/> — the markdown formatter
/// every registry-display tool ultimately calls. Without these, a silent rename of a field
/// label ("Method/API:" → "API:") or a reorder of sections would ship undetected.
/// </summary>
public class FormatEntryTests
{
    private readonly RegistryService _registry = new();

    [Fact]
    public void FormatEntry_RealRegistryEntry_ContainsAllExpectedSections()
    {
        var entry = _registry.FindById("MAF130-FAN-IN-001");
        Assert.NotNull(entry);

        var formatted = RegistryService.FormatEntry(entry!);

        Assert.Contains("## Registry entry: MAF130-FAN-IN-001", formatted, StringComparison.Ordinal);
        Assert.Contains("**Package:**", formatted, StringComparison.Ordinal);
        Assert.Contains("**Type:**", formatted, StringComparison.Ordinal);
        Assert.Contains("**Method/API:**", formatted, StringComparison.Ordinal);
        Assert.Contains("**Warning:**", formatted, StringComparison.Ordinal);
        Assert.Contains("**Guide section:**", formatted, StringComparison.Ordinal);
        Assert.Contains("### Obsolete (broken)", formatted, StringComparison.Ordinal);
        Assert.Contains("### Replacement (correct)", formatted, StringComparison.Ordinal);
        Assert.Contains("### Fix", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEntry_WithArgumentOrderChange_RendersWarningBanner()
    {
        var entry = _registry.FindById("MAF130-FAN-IN-001");
        Assert.NotNull(entry);
        Assert.True(entry!.ArgumentOrderChange,
            "Test premise: MAF130-FAN-IN-001 has argument_order_change: true in the registry.");

        var formatted = RegistryService.FormatEntry(entry);

        Assert.Contains("Argument order change", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("swapping arguments may compile and run silently", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatEntry_WithBeforeAndAfterExamples_RendersCodeFences()
    {
        var entry = _registry.FindById("MAF130-THREAD-001");
        Assert.NotNull(entry);

        var formatted = RegistryService.FormatEntry(entry!);

        Assert.Contains("### Before", formatted, StringComparison.Ordinal);
        Assert.Contains("### After", formatted, StringComparison.Ordinal);
        Assert.Contains("```csharp", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEntry_DotnetInspectDetectable_RendersStatus()
    {
        // The registry mixes detectable=true and detectable=false entries — assert both render.
        foreach (var id in _registry.AllIds)
        {
            var entry = _registry.FindById(id)!;
            var formatted = RegistryService.FormatEntry(entry);
            var expected = entry.DotnetInspectDetectable
                ? "Yes"
                : "**No** — compiler only";
            Assert.Contains(expected, formatted, StringComparison.Ordinal);
        }
    }
}
