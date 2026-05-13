using MafAutopilot.Commands;
using MafAutopilot.Data;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// #5 polish — tests for <see cref="VerifyRegistryCommand"/>.
///
/// The command is exit-code-based for CI consumption. Tests focus on the
/// pure helpers (<c>BraceImbalance</c>, <c>LooksLikePlaceholder</c>,
/// <c>CheckEntry</c>) plus an integration check that the LIVE embedded
/// registry passes verification.
/// </summary>
public class VerifyRegistryCommandTests
{
    // -------------------------------------------------------------------------
    // BraceImbalance
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("{ }", 0)]
    [InlineData("( )", 0)]
    [InlineData("{ ( ) }", 0)]
    [InlineData("{ ", 1)]            // missing close
    [InlineData(" }", -1)]            // missing open
    [InlineData("{ { } ", 1)]         // 1 open unbalanced
    [InlineData("class C { void M() { var x = 1; } }", 0)]
    [InlineData("", 0)]
    public void BraceImbalance_CountsCorrectly(string input, int expected)
    {
        Assert.Equal(expected, VerifyRegistryCommand.BraceImbalance(input));
    }

    // -------------------------------------------------------------------------
    // LooksLikePlaceholder
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("TODO", true)]
    [InlineData("tbd", true)]
    [InlineData("xxx", true)]
    [InlineData("TODO: replace with real fix", true)]
    [InlineData("TBD: figure out", true)]
    [InlineData("Replace `DefaultAzureCredential` with `ManagedIdentityCredential`.", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikePlaceholder_DetectsLeftovers(string? input, bool expected)
    {
        Assert.Equal(expected, VerifyRegistryCommand.LooksLikePlaceholder(input));
    }

    // -------------------------------------------------------------------------
    // CheckEntry — failure modes
    // -------------------------------------------------------------------------

    [Fact]
    public void CheckEntry_EmptyFixDescription_FlagsAsIssue()
    {
        var entry = MakeEntry(fixDescription: "");
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        Assert.Contains(issues, i => i.Contains("fix_description is empty"));
    }

    [Fact]
    public void CheckEntry_TodoFixDescription_FlagsAsIssue()
    {
        var entry = MakeEntry(fixDescription: "TODO: write something");
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        Assert.Contains(issues, i => i.Contains("placeholder"));
    }

    [Fact]
    public void CheckEntry_ShortFixDescription_FlagsAsTooShort()
    {
        var entry = MakeEntry(fixDescription: "fix this");  // 8 chars
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        Assert.Contains(issues, i => i.Contains("too short"));
    }

    [Fact]
    public void CheckEntry_ExampleBeforeEqualsAfter_FlagsAsNoop()
    {
        var entry = MakeEntry(
            exampleBefore: "var x = Foo();",
            exampleAfter: "var x = Foo();");
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        Assert.Contains(issues, i => i.Contains("example_before == example_after"));
    }

    [Fact]
    public void CheckEntry_UnbalancedBraces_FlagsAsIssue()
    {
        var entry = MakeEntry(
            exampleBefore: "var x = Foo( ;",   // open paren not closed
            exampleAfter:  "var x = Foo();");
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        Assert.Contains(issues, i => i.Contains("unbalanced braces"));
    }

    [Fact]
    public void CheckEntry_TbdGuideSection_FlagsAsPlaceholder()
    {
        var entry = MakeEntry(guideSection: "TBD");
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        Assert.Contains(issues, i => i.Contains("guide_section"));
    }

    [Fact]
    public void CheckEntry_NaGuideSection_PassesValidation()
    {
        var entry = MakeEntry(guideSection: "N/A");
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        // "N/A" is the canonical "no parallel section" marker — must not flag.
        Assert.DoesNotContain(issues, i => i.Contains("guide_section"));
    }

    [Fact]
    public void CheckEntry_FullyValidEntry_FlagsNothing()
    {
        var entry = MakeEntry();
        var issues = new List<string>();
        VerifyRegistryCommand.CheckEntry(entry, issues);
        Assert.Empty(issues);
    }

    // -------------------------------------------------------------------------
    // Integration: the LIVE embedded registry must pass verification
    // -------------------------------------------------------------------------

    [Fact]
    public void LiveRegistry_PassesVerification()
    {
        // This is the canary test for #5 polish — any change that breaks
        // the existing registry causes this test to fail in CI BEFORE the
        // PR merges. Mirrors the same skip logic as VerifyRegistryCommand.Run():
        //   - pre-1.0.0 historical entries
        //   - raw drafts (all-TODO entries waiting for AI-fill)
        var registry = new RegistryService();
        var issues = new List<string>();
        foreach (var entry in registry.AllEntries)
        {
            var marker = entry.AppliesToCodebases?.Trim();
            if (marker?.StartsWith("pre-", StringComparison.OrdinalIgnoreCase) == true) continue;
            if (VerifyRegistryCommand.IsRawDraft(entry)) continue;
            VerifyRegistryCommand.CheckEntry(entry, issues);
        }

        // If this test fails, registry.yaml has an entry that won't pass the
        // PR-triggered verification gate. Investigate the specific failure
        // listed in the message.
        Assert.True(issues.Count == 0,
            $"Embedded registry has {issues.Count} verification issue(s):\n" +
            string.Join("\n", issues.Select(i => "  • " + i)));
    }

    [Fact]
    public void IsRawDraft_DetectsAutoGeneratedDraftEntry()
    {
        // The shape `registry-extract` produces: every fillable core field
        // is a TODO/placeholder. Verifier should skip these.
        var entry = new RegistryEntry
        {
            Id = "MAFNEW-001",
            ObsoleteSignature = "TODO",
            ReplacementSignature = "TODO",
            FixDescription = "TODO — review the diff entry below and document the canonical fix",
            ExampleBefore = "// TODO — show a real call site that breaks\n",
            ExampleAfter = "// TODO — show the canonical 1.x replacement\n",
        };
        Assert.True(VerifyRegistryCommand.IsRawDraft(entry));
    }

    [Fact]
    public void IsRawDraft_RejectsPartiallyFilledEntry()
    {
        // Once any core field is filled, the entry is no longer a raw draft
        // — the verifier should run its checks.
        var entry = new RegistryEntry
        {
            Id = "MAFNEW-001",
            ObsoleteSignature = "Foo.OldMethod()",  // FILLED — not a draft anymore
            ReplacementSignature = "TODO",
            FixDescription = "TODO — pending",
            ExampleBefore = "// TODO",
            ExampleAfter = "// TODO",
        };
        Assert.False(VerifyRegistryCommand.IsRawDraft(entry));
    }

    // -------------------------------------------------------------------------
    // Test helper
    // -------------------------------------------------------------------------

    private static RegistryEntry MakeEntry(
        string id = "TEST-001",
        string fixDescription = "Replace OldThing with NewThing in your code.",
        string exampleBefore = "var x = OldThing.Run();",
        string exampleAfter = "var x = NewThing.RunAsync();",
        string guideSection = "5")
        => new()
        {
            Id = id,
            FixDescription = fixDescription,
            ExampleBefore = exampleBefore,
            ExampleAfter = exampleAfter,
            GuideSection = guideSection,
        };
}
