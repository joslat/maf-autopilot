using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

public class MigrationPathToolTests
{
    // -------------------------------------------------------------------------
    // SemVer comparison
    // -------------------------------------------------------------------------

    [Fact]
    public void SemVer_Comparison_Works()
    {
        var a = new SemVer(1, 2, 0);
        var b = new SemVer(1, 3, 0);
        var c = new SemVer(2, 0, 0);
        Assert.True(a < b);
        Assert.True(b < c);
        Assert.True(a < c);
        Assert.False(b < a);
        Assert.Equal(0, a.CompareTo(new SemVer(1, 2, 0)));
    }

    // -------------------------------------------------------------------------
    // Metadata parsing
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseMetadata_FullMetadata_Extracts()
    {
        const string line = "<!-- introduced: 1.3.0 | applies-to: 1.0.x → 1.3.x | deprecated-in: 1.4.0 -->";
        var result = MigrationPathTool.ParseMetadata(line);
        Assert.NotNull(result);
        Assert.Equal(new SemVer(1, 3, 0), result!.Value.Introduced);
        Assert.Equal(new SemVer(1, 0, 0), result!.Value.AppliesFrom);
        Assert.Equal(new SemVer(1, 3, 0), result!.Value.AppliesTo);
    }

    [Theory]
    [InlineData("just a regular line")]
    [InlineData("<!-- introduced: 1.3.0 -->")]               // no applies-to
    [InlineData("<!-- applies-to: 1.0.x → 1.3.x -->")]        // no introduced
    [InlineData("<!-- introduced: not-a-version | applies-to: 1.0.x → 1.3.x -->")]
    public void ParseMetadata_Invalid_ReturnsNull(string line) =>
        Assert.Null(MigrationPathTool.ParseMetadata(line));

    [Fact]
    public void ParseMetadata_AsciiArrow_AlsoWorks()
    {
        // The guide currently uses → but ASCII -> should work too.
        const string line = "<!-- introduced: 1.3.0 | applies-to: 1.0.x -> 1.3.x -->";
        var result = MigrationPathTool.ParseMetadata(line);
        Assert.NotNull(result);
    }

    [Fact]
    public void ParseMetadata_PatchQualifiedWildcards_Extracts()
    {
        const string line = "<!-- introduced: 1.17.0 | applies-to: 1.16.0.x → 1.17.0.x | deprecated-in: none -->";
        var result = MigrationPathTool.ParseMetadata(line);

        Assert.NotNull(result);
        Assert.Equal(new SemVer(1, 17, 0), result!.Value.Introduced);
        Assert.Equal(new SemVer(1, 16, 0), result.Value.AppliesFrom);
        Assert.Equal(new SemVer(1, 17, 0), result.Value.AppliesTo);
    }

    // -------------------------------------------------------------------------
    // Section extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractSections_HeadingWithMetadata_IsExtracted()
    {
        const string md = """
            # Top
            ## Section A
            <!-- introduced: 1.3.0 | applies-to: 1.0.x → 1.3.x -->
            Body content.
            ## Section B
            <!-- introduced: 1.2.0 | applies-to: 1.0.x → 1.2.x -->
            More body.
            """;

        var sections = MigrationPathTool.ExtractSections(md);
        Assert.Equal(2, sections.Count);
        Assert.Equal("Section A", sections[0].Title);
        Assert.Equal(new SemVer(1, 3, 0), sections[0].IntroducedAt);
    }

    [Fact]
    public void ExtractSections_HeadingWithoutMetadata_IsSkipped()
    {
        const string md = """
            ## Section without metadata
            Body content.
            ## Section with metadata
            <!-- introduced: 1.3.0 | applies-to: 1.0.x → 1.3.x -->
            """;
        var sections = MigrationPathTool.ExtractSections(md);
        Assert.Single(sections);
        Assert.Equal("Section with metadata", sections[0].Title);
    }

    [Fact]
    public void ExtractSections_MetadataBeforeHeading_IsExtracted()
    {
        const string md = """
            # Top
            <!-- introduced: 1.3.0 | applies-to: 1.2.x → 1.3.x -->
            ## Legacy section
            Body content.
            """;

        var section = Assert.Single(MigrationPathTool.ExtractSections(md));
        Assert.Equal("Legacy section", section.Title);
        Assert.Equal(new SemVer(1, 3, 0), section.IntroducedAt);
    }

    [Fact]
    public void ExtractSections_DoesNotStealMetadataFromFollowingHeading()
    {
        const string md = """
            ## Section without metadata
            Body content.
            <!-- introduced: 1.4.0 | applies-to: 1.3.0.x → 1.4.0.x -->
            ## Section with leading metadata
            More body.
            """;

        var section = Assert.Single(MigrationPathTool.ExtractSections(md));
        Assert.Equal("Section with leading metadata", section.Title);
    }

    // -------------------------------------------------------------------------
    // Path selection
    // -------------------------------------------------------------------------

    [Fact]
    public void SelectApplicable_OrdersByIntroducedVersion()
    {
        var sections = new[]
        {
            new GuideSection("LaterSection", 1, new SemVer(1, 3, 0), new SemVer(1, 0, 0), new SemVer(1, 3, 0)),
            new GuideSection("EarlierSection", 5, new SemVer(1, 1, 0), new SemVer(1, 0, 0), new SemVer(1, 1, 0)),
            new GuideSection("MiddleSection", 10, new SemVer(1, 2, 0), new SemVer(1, 0, 0), new SemVer(1, 2, 0)),
        };

        var ordered = MigrationPathTool.SelectApplicable(sections, new SemVer(1, 0, 0), new SemVer(1, 3, 0));
        Assert.Equal("EarlierSection", ordered[0].Title);
        Assert.Equal("MiddleSection", ordered[1].Title);
        Assert.Equal("LaterSection", ordered[2].Title);
    }

    [Fact]
    public void SelectApplicable_FiltersByOverlap()
    {
        var sections = new[]
        {
            new GuideSection("OldSection", 1, new SemVer(1, 1, 0), new SemVer(1, 0, 0), new SemVer(1, 1, 0)),
            new GuideSection("MidSection", 5, new SemVer(1, 3, 0), new SemVer(1, 2, 0), new SemVer(1, 3, 0)),
            new GuideSection("FutureSection", 9, new SemVer(2, 0, 0), new SemVer(1, 5, 0), new SemVer(2, 0, 0)),
        };

        // Migration path 1.2.0 → 1.3.0 — only MidSection should apply.
        var result = MigrationPathTool.SelectApplicable(sections, new SemVer(1, 2, 0), new SemVer(1, 3, 0));
        Assert.Single(result);
        Assert.Equal("MidSection", result[0].Title);
    }

    [Fact]
    public void SelectApplicable_FullPath1_0To1_3_PicksAllOverlapping()
    {
        var sections = new[]
        {
            new GuideSection("Step1", 1, new SemVer(1, 1, 0), new SemVer(1, 0, 0), new SemVer(1, 1, 0)),
            new GuideSection("Step2", 5, new SemVer(1, 2, 0), new SemVer(1, 1, 0), new SemVer(1, 2, 0)),
            new GuideSection("Step3", 9, new SemVer(1, 3, 0), new SemVer(1, 2, 0), new SemVer(1, 3, 0)),
            new GuideSection("Future", 13, new SemVer(2, 0, 0), new SemVer(1, 5, 0), new SemVer(2, 0, 0)),
        };
        var result = MigrationPathTool.SelectApplicable(sections, new SemVer(1, 0, 0), new SemVer(1, 3, 0));
        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, r => r.Title == "Future");
    }

    [Fact]
    public void SelectApplicable_ExcludesAlreadyAppliedSourceVersion()
    {
        var sections = new[]
        {
            new GuideSection("Current", 1, new SemVer(1, 13, 0), new SemVer(1, 12, 0), new SemVer(1, 13, 0)),
            new GuideSection("Next", 5, new SemVer(1, 14, 0), new SemVer(1, 13, 0), new SemVer(1, 14, 0)),
        };

        var result = MigrationPathTool.SelectApplicable(
            sections, new SemVer(1, 13, 0), new SemVer(1, 14, 0));

        var section = Assert.Single(result);
        Assert.Equal("Next", section.Title);
    }

    // -------------------------------------------------------------------------
    // MCP tool entry — input validation
    // -------------------------------------------------------------------------

    [Fact]
    public void MafMigrationPath_InvalidCurrentVersion_ReturnsError()
    {
        var tool = new MigrationPathTool();
        Assert.Contains("Error", tool.MafMigrationPath("not-a-version", "1.3.0"),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafMigrationPath_CurrentNotLessThanTarget_ReturnsError()
    {
        var tool = new MigrationPathTool();
        Assert.Contains("Error", tool.MafMigrationPath("1.3.0", "1.3.0"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Error", tool.MafMigrationPath("1.3.0", "1.2.0"),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafMigrationPath_RealGuide_LegacyMetadataProducesGuidance()
    {
        var tool = new MigrationPathTool();
        var result = tool.MafMigrationPath("1.2.0", "1.3.0");

        Assert.Contains("migration path", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No version-keyed guide sections", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Package References & Dependencies", result, StringComparison.Ordinal);
    }

    [Fact]
    public void MafMigrationPath_RealGuide_1_13To1_17_ContainsEveryStepInOrder()
    {
        var result = new MigrationPathTool().MafMigrationPath("1.13.0", "1.17.0");

        Assert.DoesNotContain("Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No version-keyed guide sections", result, StringComparison.OrdinalIgnoreCase);

        var v14 = result.IndexOf("MAF 1.14.0 Migration Guide", StringComparison.Ordinal);
        var v15 = result.IndexOf("MAF 1.15.0 Migration Guide", StringComparison.Ordinal);
        var v16 = result.IndexOf("MAF 1.16.0 Migration Guide", StringComparison.Ordinal);
        var v17 = result.IndexOf("MAF 1.17.0 Migration Guide", StringComparison.Ordinal);

        Assert.True(v14 >= 0, "The 1.14 migration step was missing.");
        Assert.True(v14 < v15, "The 1.15 migration step was missing or out of order.");
        Assert.True(v15 < v16, "The 1.16 migration step was missing or out of order.");
        Assert.True(v16 < v17, "The 1.17 migration step was missing or out of order.");
        Assert.DoesNotContain("MAF 1.13.0 Migration Guide", result, StringComparison.Ordinal);
    }
}
