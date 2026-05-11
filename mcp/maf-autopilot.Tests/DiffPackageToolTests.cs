using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Tests for the pure parser of DiffPackageTool. The fixture string in
/// <see cref="ParseDiffOutput_RealFormat_1_0_0_to_1_3_0_Microsoft_Agents_AI"/> is
/// captured from a real run of `dotnet-inspect@0.7.8 -- diff` on 2026-05-11 and
/// pins the parser against the actual upstream output format.
/// </summary>
public class DiffPackageToolTests
{
    [Fact]
    public void ParseDiffOutput_Empty_ReturnsEmpty()
    {
        var result = DiffPackageTool.ParseDiffOutput("");
        Assert.Empty(result.Breaking);
        Assert.Empty(result.Additive);
        Assert.False(result.NoChangesDetected);
    }

    [Fact]
    public void ParseDiffOutput_NoApiChangesNote_SetsFlag()
    {
        const string output = """
            # API Diff: Microsoft.Agents.AI

            | Field | Value |
            | ----- | ----- |
            | Versions | **1.2.0** -> **1.3.0** |

            > [!NOTE]
            > No API changes detected.
            """;

        var result = DiffPackageTool.ParseDiffOutput(output);
        Assert.True(result.NoChangesDetected);
        Assert.Empty(result.Breaking);
        Assert.Empty(result.Additive);
    }

    /// <summary>
    /// Anchor test — fixture is a real captured run from 2026-05-11.
    /// If upstream changes the diff format, this test will fail loudly.
    /// </summary>
    [Fact]
    public void ParseDiffOutput_RealFormat_1_0_0_to_1_3_0_Microsoft_Agents_AI()
    {
        const string realOutput = """
            # API Diff: Microsoft.Agents.AI

            | Field | Value |
            | ----- | ----- |
            | Versions | **1.0.0** -> **1.3.0** |
            | Summary | **Summary:** 6 breaking, 22 additive across 23 types |

            ## Breaking Changes

            ### AgentInlineSkill

            - Member '.ctor' signature changed: `void .ctor(string name, string description)` -> `void .ctor(string name, string description, string? license = null)`
            - Member 'AddResource' signature changed: `AgentInlineSkill AddResource(string name)` -> `AgentInlineSkill AddResource(string name, JsonSerializerOptions? opts = null)`

            ### AgentSkillsProvider

            - Member '.ctor' signature changed: `void .ctor(params AgentInlineSkill[] skills)` -> `void .ctor(params AgentSkill[] skills)`

            ## Additive Changes

            ### AgentClassSkill`1

            - Type 'Microsoft.Agents.AI.AgentClassSkill`1' was added

            ### AgentFileSkillsSourceOptions

            - Member 'ScriptDirectories' was added
            - Member 'ResourceDirectories' was added

            ### EvalCheck

            - Type 'Microsoft.Agents.AI.EvalCheck' was added
            """;

        var result = DiffPackageTool.ParseDiffOutput(realOutput);

        Assert.False(result.NoChangesDetected);
        Assert.Equal(3, result.Breaking.Count);
        Assert.Equal(4, result.Additive.Count);

        // Type grouping is preserved
        Assert.Contains(result.Breaking, e => e.Type == "AgentInlineSkill");
        Assert.Contains(result.Breaking, e => e.Type == "AgentSkillsProvider");
        Assert.Contains(result.Additive, e => e.Type == "AgentFileSkillsSourceOptions");

        // Kind classification
        Assert.All(result.Breaking.Where(e => e.Body.Contains("signature changed")),
            e => Assert.Equal(DiffEntryKind.SignatureChanged, e.Kind));
        Assert.All(result.Additive.Where(e => e.Body.Contains("was added")),
            e => Assert.Equal(DiffEntryKind.Added, e.Kind));
    }

    [Fact]
    public void ParseDiffOutput_BreakingSection_AddedMembersDoNotLeak()
    {
        // Make sure section boundaries are respected: an "additive" bullet appearing
        // BEFORE the additive section header still belongs to breaking.
        const string output = """
            ## Breaking Changes

            ### Foo
            - Member 'X' was removed

            ## Additive Changes

            ### Bar
            - Type 'Bar' was added
            """;
        var result = DiffPackageTool.ParseDiffOutput(output);
        Assert.Single(result.Breaking);
        Assert.Single(result.Additive);
        Assert.Equal("Foo", result.Breaking[0].Type);
        Assert.Equal(DiffEntryKind.Removed, result.Breaking[0].Kind);
    }

    [Fact]
    public void ParseDiffOutput_ObsoleteAnnotation_IsFlagged()
    {
        const string output = """
            ## Breaking Changes

            ### LegacyAgent
            - Member 'Foo' signature changed: `void Foo()` -> `[Obsolete("Use Bar")] void Foo()`
            """;

        var result = DiffPackageTool.ParseDiffOutput(output);
        Assert.True(result.Breaking[0].IsObsolete);
    }

    [Fact]
    public void ParseDiffOutput_BulletsWithoutType_AreIgnored()
    {
        // Defensive: bullets appearing before any ### header have no type context.
        const string output = """
            ## Breaking Changes
            - Stray bullet (no type) should not crash or be attributed.

            ### RealType
            - Member 'X' was removed
            """;

        var result = DiffPackageTool.ParseDiffOutput(output);
        Assert.Single(result.Breaking);
        Assert.Equal("RealType", result.Breaking[0].Type);
    }

    [Fact]
    public void ParseDiffOutput_UnknownSection_BulletsAreSkipped()
    {
        const string output = """
            ## Some Other Heading

            ### IgnoredType
            - Member 'X' was added — but section is unknown
            """;
        var result = DiffPackageTool.ParseDiffOutput(output);
        Assert.Empty(result.Breaking);
        Assert.Empty(result.Additive);
    }

    // -------------------------------------------------------------------------
    // Input validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Microsoft.Agents.AI")]
    [InlineData("Foo_Bar-Baz")]
    [InlineData("A.B.C.D")]
    public void IsValidPackageId_ValidNames_Accepted(string id) =>
        Assert.True(DiffPackageTool.IsValidPackageId(id));

    [Theory]
    [InlineData("")]
    [InlineData("Foo Bar")]              // spaces
    [InlineData("Foo;rm -rf /")]         // shell metachar
    [InlineData("Foo\" --source evil")] // quote escape attempt
    [InlineData("Foo|cmd")]
    public void IsValidPackageId_InvalidNames_Rejected(string id) =>
        Assert.False(DiffPackageTool.IsValidPackageId(id));

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.3.0-alpha-4")]
    [InlineData("1.3.0+build.42")]
    public void IsValidVersion_ValidVersions_Accepted(string v) =>
        Assert.True(DiffPackageTool.IsValidVersion(v));

    [Theory]
    [InlineData("")]
    [InlineData("1.0 ; rm")]
    [InlineData("\"1.0\"")]
    public void IsValidVersion_InvalidVersions_Rejected(string v) =>
        Assert.False(DiffPackageTool.IsValidVersion(v));

    [Fact]
    public void Mcp_InvalidArguments_ReturnError()
    {
        var tool = new DiffPackageTool(new MafAutopilot.Data.RegistryService());

        Assert.Contains("Error", tool.MafDiffPackage("", "1.0", "2.0"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Error", tool.MafDiffPackage("Foo;rm", "1.0", "2.0"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Error", tool.MafDiffPackage("Foo", "", "2.0"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Error", tool.MafDiffPackage("Foo", "1.0", "1.0 ; rm"), StringComparison.OrdinalIgnoreCase);
    }
}
