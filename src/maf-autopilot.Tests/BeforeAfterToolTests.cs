using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Phase W.7 — tests for <see cref="BeforeAfterTool"/>.
///
/// The tool itself is a thin orchestrator: parse the rule IDs, walk
/// every .cs file, dry-run each rewriter, render the diff. The
/// rewriters themselves are tested in <see cref="AutoFixToolTests"/>.
/// </summary>
public class BeforeAfterToolTests
{
    [Fact]
    public void BeforeAfter_UnknownRule_ListsUnknownInReport()
    {
        var tool = new BeforeAfterTool();
        var result = tool.MafBeforeAfter(Path.GetTempPath(), "NOT-A-REAL-RULE");
        Assert.Contains("unknown", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BeforeAfter_EmptyRuleIds_ReturnsError()
    {
        var tool = new BeforeAfterTool();
        var result = tool.MafBeforeAfter(Path.GetTempPath(), "");
        Assert.Contains("No ruleIds", result);
    }

    [Fact]
    public void BeforeAfter_EmptyPath_ReturnsError()
    {
        var tool = new BeforeAfterTool();
        var result = tool.MafBeforeAfter("", "MAF-AP-SEC-001");
        Assert.StartsWith("❌", result);
    }

    [Fact]
    public void BeforeAfter_RealChange_RendersUnifiedDiff()
    {
        // Arrange — temp dir with one file matching the rule.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-beforeafter-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(
            Path.Combine(tempRoot, "Bad.cs"),
            "class C { object F() => new DefaultAzureCredential(); }");

        try
        {
            // Act.
            var tool = new BeforeAfterTool();
            var result = tool.MafBeforeAfter(tempRoot, "MAF-AP-SEC-001");

            // Assert — markdown with a diff block + the rewrite visible.
            Assert.Contains("```diff", result);
            Assert.Contains("-class", result); // line containing DefaultAzureCredential is marked removed
            Assert.Contains("+class", result); // line with ManagedIdentityCredential is marked added
            Assert.Contains("ManagedIdentityCredential", result);
            Assert.Contains("Summary:", result);
            Assert.Contains("file change(s)", result);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BeforeAfter_NoChange_ReportsNoChanges()
    {
        // Arrange — temp dir with a file that has NO occurrences of the rule.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-beforeafter-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "Clean.cs"), "class C { int F() => 0; }");

        try
        {
            var tool = new BeforeAfterTool();
            var result = tool.MafBeforeAfter(tempRoot, "MAF-AP-SEC-001");
            Assert.Contains("No changes", result);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // RenderUnifiedDiff direct tests
    // -------------------------------------------------------------------------

    [Fact]
    public void RenderUnifiedDiff_IdenticalInputs_ReturnsEmpty()
    {
        var diff = BeforeAfterTool.RenderUnifiedDiff("hello\nworld", "hello\nworld", contextLines: 2);
        Assert.Equal(string.Empty, diff);
    }

    [Fact]
    public void RenderUnifiedDiff_OneLineChange_ProducesPlusMinusBlock()
    {
        var before = "a\nb\nc\nd\ne\n";
        var after  = "a\nb\nX\nd\ne\n";
        var diff = BeforeAfterTool.RenderUnifiedDiff(before, after, contextLines: 1);
        Assert.Contains("@@", diff);
        Assert.Contains("-c", diff);
        Assert.Contains("+X", diff);
    }
}
