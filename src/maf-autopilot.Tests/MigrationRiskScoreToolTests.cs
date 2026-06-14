using System.Text.Json;
using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Phase W.11 — tests for <see cref="MigrationRiskScoreTool"/>.
///
/// The tool composes existing scanners + a string-search heuristic into a
/// weighted score → HARD / MEDIUM / EASY verdict. Tests cover both the
/// pure helpers (string counter) and end-to-end against the 1.3 sample.
/// </summary>
public class MigrationRiskScoreToolTests
{
    private static string ResolveSampleRoot(string sampleName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var samplePath = Path.Combine(dir!.FullName, "samples", sampleName);
        Assert.True(Directory.Exists(samplePath), $"Sample not found: {samplePath}");
        return samplePath;
    }

    // -------------------------------------------------------------------------
    // CountSourceMatches — pure helper
    // -------------------------------------------------------------------------

    [Fact]
    public void CountSourceMatches_EmptyNeedle_ReturnsZero()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-risk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "a.cs"), "class C { }");
        try
        {
            Assert.Equal(0, MigrationRiskScoreTool.CountSourceMatches(tempRoot, ""));
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    [Fact]
    public void CountSourceMatches_MultipleOccurrencesAcrossFiles_Sums()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-risk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "a.cs"), "AgentThread AgentThread AgentThread");
        File.WriteAllText(Path.Combine(tempRoot, "b.cs"), "AgentThread");
        try
        {
            Assert.Equal(4, MigrationRiskScoreTool.CountSourceMatches(tempRoot, "AgentThread"));
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    // -------------------------------------------------------------------------
    // End-to-end against samples
    // -------------------------------------------------------------------------

    [Fact]
    public void Score_AgainstMaf13Sample_ReturnsMediumOrHard()
    {
        var samplePath = ResolveSampleRoot("maf-1.3-sample");
        var tool = new MigrationRiskScoreTool();
        var json = tool.MafScoreMigrationRisk(samplePath);

        var doc = JsonSerializer.Deserialize<JsonDocument>(json)!;
        var verdict = doc.RootElement.GetProperty("verdict").GetString();

        // The sample has 10 anti-pattern errors + 3 silent-starvation risks
        // → score is well above the EASY threshold.
        Assert.True(verdict is "MEDIUM" or "HARD", $"Expected MEDIUM or HARD; got {verdict}");

        // Breakdown should include every weighted category.
        var breakdown = doc.RootElement.GetProperty("breakdown");
        Assert.Equal(3, breakdown.GetProperty("silentStarvationRisks").GetInt32());
        Assert.True(breakdown.GetProperty("antiPatternErrors").GetInt32() > 0);
    }

    [Fact]
    public void Score_AgainstEmptyRepo_ReturnsEasy()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-risk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "a.cs"), "class CleanCode { int X() => 1; }");
        try
        {
            var tool = new MigrationRiskScoreTool();
            var json = tool.MafScoreMigrationRisk(tempRoot);
            var doc = JsonSerializer.Deserialize<JsonDocument>(json)!;
            Assert.Equal("EASY", doc.RootElement.GetProperty("verdict").GetString());
            Assert.Equal(0, doc.RootElement.GetProperty("score").GetInt32());
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    [Fact]
    public void Score_OutputIncludesRecommendation()
    {
        var samplePath = ResolveSampleRoot("maf-1.3-sample");
        var tool = new MigrationRiskScoreTool();
        var json = tool.MafScoreMigrationRisk(samplePath);
        Assert.Contains("recommendation", json);
        // The recommendation text varies by verdict (EASY/MEDIUM/HARD).
        // Loose assertion: it always mentions "migration" (every verdict's
        // recommendation talks about the migration approach).
        Assert.Contains("migration", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Score_EmptyPath_ReturnsErrorJson()
    {
        var tool = new MigrationRiskScoreTool();
        var result = tool.MafScoreMigrationRisk("");
        Assert.Contains("error", result);
    }
}
