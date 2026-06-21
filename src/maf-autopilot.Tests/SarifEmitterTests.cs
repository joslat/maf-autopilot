using System.Text.Json;
using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// SarifEmitter and SarifExportTool are internal — exercised via InternalsVisibleTo.
/// Tests validate the SARIF v2.1.0 shape: required keys, level mapping, location
/// physical-region, and that the JSON parses cleanly.
/// </summary>
public class SarifEmitterTests
{
    [Fact]
    public void Emit_EmptyFindings_ProducesValidSarifEnvelope()
    {
        var sarif = SarifEmitter.Emit("1.0.0", []);
        using var doc = JsonDocument.Parse(sarif);
        var root = doc.RootElement;

        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        Assert.True(root.TryGetProperty("$schema", out _));
        var runs = root.GetProperty("runs");
        Assert.Equal(1, runs.GetArrayLength());
        var run = runs[0];
        Assert.Equal("maf-doctor", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.Empty(run.GetProperty("results").EnumerateArray());
    }

    [Fact]
    public void Emit_FindingsMapToCorrectSarifLevels()
    {
        var findings = new[]
        {
            new SarifFinding("MAF-AP-SEC-001", SarifSeverity.Error, "msg-e", "src/A.cs", 10),
            new SarifFinding("MAF-AP-CONC-002", SarifSeverity.Warning, "msg-w", "src/B.cs", 20),
            new SarifFinding("MAF-AP-OBS-002", SarifSeverity.Note, "msg-n", "src/C.cs", 30),
        };

        var sarif = SarifEmitter.Emit("1.0.0", findings);
        using var doc = JsonDocument.Parse(sarif);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        Assert.Equal(3, results.GetArrayLength());

        Assert.Equal("error", results[0].GetProperty("level").GetString());
        Assert.Equal("warning", results[1].GetProperty("level").GetString());
        Assert.Equal("note", results[2].GetProperty("level").GetString());
    }

    [Fact]
    public void Emit_LocationContainsArtifactAndRegion()
    {
        var findings = new[]
        {
            new SarifFinding("R1", SarifSeverity.Error, "msg", "src/Foo/Bar.cs", 42),
        };
        var sarif = SarifEmitter.Emit("1.0.0", findings);
        using var doc = JsonDocument.Parse(sarif);
        var loc = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("locations")[0].GetProperty("physicalLocation");

        Assert.Equal("src/Foo/Bar.cs", loc.GetProperty("artifactLocation").GetProperty("uri").GetString());
        Assert.Equal(42, loc.GetProperty("region").GetProperty("startLine").GetInt32());
    }

    [Fact]
    public void Emit_WindowsPaths_NormalisedToForwardSlash()
    {
        var findings = new[]
        {
            new SarifFinding("R1", SarifSeverity.Error, "msg", "src\\Foo\\Bar.cs", 1),
        };
        var sarif = SarifEmitter.Emit("1.0.0", findings);
        using var doc = JsonDocument.Parse(sarif);
        var uri = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("locations")[0].GetProperty("physicalLocation")
            .GetProperty("artifactLocation").GetProperty("uri").GetString();

        Assert.Equal("src/Foo/Bar.cs", uri);
    }

    [Fact]
    public void Emit_RuleCatalog_AppearsUnderToolDriver()
    {
        var rules = new[]
        {
            new SarifRule("R1", "Rule One", SarifSeverity.Error, "Long desc", "https://example.com/r1"),
        };
        var sarif = SarifEmitter.Emit("1.0.0", [], rules);
        using var doc = JsonDocument.Parse(sarif);
        var driverRules = doc.RootElement.GetProperty("runs")[0].GetProperty("tool")
            .GetProperty("driver").GetProperty("rules");

        Assert.Equal(1, driverRules.GetArrayLength());
        var rule = driverRules[0];
        Assert.Equal("R1", rule.GetProperty("id").GetString());
        Assert.Equal("Rule One", rule.GetProperty("name").GetString());
        Assert.Equal("https://example.com/r1", rule.GetProperty("helpUri").GetString());
        Assert.Equal("error", rule.GetProperty("defaultConfiguration").GetProperty("level").GetString());
    }

    // -------------------------------------------------------------------------
    // SARIF via format=sarif on the main tools (post-F.2 consolidation)
    // -------------------------------------------------------------------------

    [Fact]
    public void EmitAntiPatternsSarif_FromFindings_ProducesValidSarif()
    {
        var findings = new[]
        {
            new AntiPatternFinding("MAF-AP-SEC-001", "test rule", AntiPatternSeverity.Error, "src/A.cs", 10, "match"),
        };
        var sarif = SarifExportTool.EmitAntiPatternsSarif(findings);
        using var doc = JsonDocument.Parse(sarif);
        Assert.Equal("2.1.0", doc.RootElement.GetProperty("version").GetString());
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal("error", results[0].GetProperty("level").GetString());
    }

    [Fact]
    public void EmitFanOutSarif_DropsOkVerdicts_KeepsRisks()
    {
        var findings = new[]
        {
            new MessageHandlerFinding("src/A.cs", 1, "Good", "Task<int>", FanOutVerdict.Ok),
            new MessageHandlerFinding("src/B.cs", 2, "Bad", "void", FanOutVerdict.SilentStarvationRisk),
            new MessageHandlerFinding("src/C.cs", 3, "Weird", "int", FanOutVerdict.LikelyInvalid),
        };
        var sarif = SarifExportTool.EmitFanOutSarif(findings);
        using var doc = JsonDocument.Parse(sarif);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        // Only 2 risky findings survive — OK is dropped.
        Assert.Equal(2, results.GetArrayLength());
    }

    [Fact]
    public void MafScanAntiPatterns_FormatSarif_RoutesToSarifEmitter()
    {
        var tool = new AntiPatternScannerTool();
        // Empty path returns an Error string regardless of format — verify it doesn't crash
        // when format=sarif is supplied.
        var output = tool.MafScanAntiPatterns("", format: "sarif");
        Assert.Contains("Error", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafValidateFanOut_FormatSarif_RoutesToSarifEmitter()
    {
        var tool = new FanOutValidatorTool();
        var output = tool.MafValidateFanOut("", format: "sarif");
        Assert.Contains("Error", output, StringComparison.OrdinalIgnoreCase);
    }
}
