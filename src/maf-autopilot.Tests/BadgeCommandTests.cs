using MafDoctor.Commands;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Phase W.13 — tests for <see cref="BadgeCommand"/>.
///
/// The command extracts a grade letter from the doctor's markdown output
/// and emits shields.io-endpoint-badge JSON. Tests cover: grade extraction
/// regex, colour mapping, and the full Build() round-trip against a known
/// codebase (`samples/maf-1.3-sample` — should yield F → red).
/// </summary>
public class BadgeCommandTests
{
    [Theory]
    [InlineData("A", "brightgreen")]
    [InlineData("B", "green")]
    [InlineData("C", "yellow")]
    [InlineData("D", "orange")]
    [InlineData("F", "red")]
    [InlineData("?", "lightgrey")]
    public void ColorForGrade_MapsEveryGrade(string grade, string expectedColor)
    {
        Assert.Equal(expectedColor, BadgeCommand.ColorForGrade(grade));
    }

    [Theory]
    [InlineData("## 🟢 MAF health grade: **A**\n\n_..._", "A")]
    [InlineData("## 🟡 MAF health grade: **B**\n\n_..._", "B")]
    [InlineData("## 🔴 MAF health grade: **F**\n\n_..._", "F")]
    public void ExtractGrade_FindsBoldLetter(string doctorReport, string expectedGrade)
    {
        Assert.Equal(expectedGrade, BadgeCommand.ExtractGrade(doctorReport));
    }

    [Fact]
    public void ExtractGrade_NoMatch_ReturnsNull()
    {
        Assert.Null(BadgeCommand.ExtractGrade("nothing useful here"));
    }

    [Fact]
    public void Build_AgainstSample13_EmitsJsonWithRedGrade()
    {
        // Sample resolution: walk up from test bin to repo root, then to samples/maf-1.3-sample/.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var samplePath = Path.Combine(dir!.FullName, "samples", "maf-1.3-sample");
        Assert.True(Directory.Exists(samplePath), $"Sample not found at {samplePath}");

        var json = BadgeCommand.Build(samplePath);

        // Must be valid JSON with shields.io endpoint schema fields.
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"label\":\"MAF Health\"", json);
        Assert.Contains("\"message\":\"F\"", json);
        Assert.Contains("\"color\":\"red\"", json);
    }
}
