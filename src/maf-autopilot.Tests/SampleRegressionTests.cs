using System.Text.RegularExpressions;
using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Phase W.9 — sample-level regression test. Iterates every <c>samples/*-sample/</c>
/// folder, runs <see cref="DoctorTool.MafDoctor"/> against it, and asserts the
/// reported grade + counts match the sample's <c>.expected-doctor.yaml</c>.
///
/// **Purpose:** pins workshop invariants. If a future toolkit change regresses
/// detection on any sample (e.g. the fan-out validator stops finding the
/// non-generic ValueTask return), this test fails BEFORE the regression ships.
///
/// **Schema of .expected-doctor.yaml:**
/// <code>
/// grade: F
/// anti_pattern_errors: 10
/// anti_pattern_warnings: 3
/// silent_starvation_risks: 3
/// </code>
///
/// When changing a sample's deliberate-anti-pattern set, update its
/// <c>.expected-doctor.yaml</c> in the SAME commit.
/// </summary>
public class SampleRegressionTests
{
    private static readonly DoctorTool _doctor = new();

    /// <summary>
    /// Resolves a sample folder relative to the repo root, walking up from
    /// AppContext.BaseDirectory (i.e. the test bin/&lt;TFM&gt;/ directory).
    /// </summary>
    private static string ResolveSamplePath(string sampleRelativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        var samplePath = Path.Combine(dir!.FullName, sampleRelativePath);
        Assert.True(Directory.Exists(samplePath), $"Sample folder not found: {samplePath}");
        return samplePath;
    }

    private static ExpectedDoctor LoadExpectations(string samplePath)
    {
        var yamlPath = Path.Combine(samplePath, ".expected-doctor.yaml");
        Assert.True(File.Exists(yamlPath), $".expected-doctor.yaml missing at {yamlPath}");

        var expected = new ExpectedDoctor();
        foreach (var line in File.ReadAllLines(yamlPath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || string.IsNullOrEmpty(trimmed)) continue;

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = trimmed.Substring(0, colonIdx).Trim();
            var value = trimmed.Substring(colonIdx + 1).Trim();

            switch (key)
            {
                case "grade": expected.Grade = value; break;
                case "anti_pattern_errors": expected.AntiPatternErrors = int.Parse(value); break;
                case "anti_pattern_warnings": expected.AntiPatternWarnings = int.Parse(value); break;
                case "silent_starvation_risks": expected.SilentStarvationRisks = int.Parse(value); break;
            }
        }
        return expected;
    }

    /// <summary>
    /// Extract grade + counts from the doctor's markdown output.
    /// </summary>
    private static ActualDoctor ParseDoctorOutput(string markdown)
    {
        // "## 🔴 MAF health grade: **F**"
        var gradeMatch = Regex.Match(markdown, @"MAF health grade: \*\*([A-F])\*\*");
        Assert.True(gradeMatch.Success, $"Could not find grade in doctor output:\n{markdown[..Math.Min(500, markdown.Length)]}");

        // Table rows: "| Anti-pattern errors | 10 |"
        var errorsMatch = Regex.Match(markdown, @"\|\s*Anti-pattern errors\s*\|\s*(\d+)\s*\|");
        var warningsMatch = Regex.Match(markdown, @"\|\s*Anti-pattern warnings\s*\|\s*(\d+)\s*\|");
        var starvationMatch = Regex.Match(markdown, @"\|\s*Silent-starvation risks[^|]*\|\s*(\d+)\s*\|");

        return new ActualDoctor
        {
            Grade = gradeMatch.Groups[1].Value,
            AntiPatternErrors = errorsMatch.Success ? int.Parse(errorsMatch.Groups[1].Value) : -1,
            AntiPatternWarnings = warningsMatch.Success ? int.Parse(warningsMatch.Groups[1].Value) : -1,
            SilentStarvationRisks = starvationMatch.Success ? int.Parse(starvationMatch.Groups[1].Value) : -1,
        };
    }

    [Theory]
    [InlineData("samples/maf-1.3-sample")]
    [InlineData("samples/maf-1.2-sample")]
    [InlineData("samples/maf-1.0-sample")]
    public void Doctor_Grade_MatchesExpectedYaml(string sampleRelativePath)
    {
        var samplePath = ResolveSamplePath(sampleRelativePath);
        var expected = LoadExpectations(samplePath);

        var actual = ParseDoctorOutput(_doctor.MafDoctor(samplePath));

        Assert.Equal(expected.Grade, actual.Grade);
        Assert.Equal(expected.AntiPatternErrors, actual.AntiPatternErrors);
        Assert.Equal(expected.AntiPatternWarnings, actual.AntiPatternWarnings);
        Assert.Equal(expected.SilentStarvationRisks, actual.SilentStarvationRisks);
    }

    /// <summary>
    /// Every sample must ship with an <c>.expected-doctor.yaml</c>. Catches
    /// the "new sample but forgot to pin expectations" foot-gun.
    /// </summary>
    [Theory]
    [InlineData("samples/maf-1.3-sample")]
    [InlineData("samples/maf-1.2-sample")]
    [InlineData("samples/maf-1.0-sample")]
    public void Sample_HasExpectedDoctorYaml(string sampleRelativePath)
    {
        var samplePath = ResolveSamplePath(sampleRelativePath);
        var yamlPath = Path.Combine(samplePath, ".expected-doctor.yaml");
        Assert.True(File.Exists(yamlPath), $"Missing .expected-doctor.yaml at {yamlPath}");
    }

    private sealed class ExpectedDoctor
    {
        public string Grade { get; set; } = "";
        public int AntiPatternErrors { get; set; }
        public int AntiPatternWarnings { get; set; }
        public int SilentStarvationRisks { get; set; }
    }

    private sealed class ActualDoctor
    {
        public string Grade { get; set; } = "";
        public int AntiPatternErrors { get; set; }
        public int AntiPatternWarnings { get; set; }
        public int SilentStarvationRisks { get; set; }
    }
}
