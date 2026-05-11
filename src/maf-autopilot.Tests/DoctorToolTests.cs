using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class DoctorToolTests
{
    // -------------------------------------------------------------------------
    // Grade boundaries
    // -------------------------------------------------------------------------

    [Fact]
    public void Grade_Clean_ReturnsA()
    {
        var summary = DoctorTool.Grade([], []);
        Assert.Equal('A', summary.Grade);
        Assert.Empty(summary.TopFixes);
    }

    [Fact]
    public void Grade_OneWarning_NoErrors_ReturnsB()
    {
        var antiPatterns = new[]
        {
            new AntiPatternFinding("MAF-AP-OBS-001", "name", AntiPatternSeverity.Warning, "f", 1, "m"),
        };
        var summary = DoctorTool.Grade(antiPatterns, []);
        Assert.Equal('B', summary.Grade);
    }

    [Fact]
    public void Grade_OneError_ReturnsC()
    {
        var antiPatterns = new[]
        {
            new AntiPatternFinding("MAF-AP-SEC-001", "name", AntiPatternSeverity.Error, "f", 1, "m"),
        };
        var summary = DoctorTool.Grade(antiPatterns, []);
        Assert.Equal('C', summary.Grade);
        Assert.NotEmpty(summary.TopFixes);
    }

    [Fact]
    public void Grade_FourErrors_ReturnsF()
    {
        var antiPatterns = Enumerable.Range(0, 4)
            .Select(i => new AntiPatternFinding($"R{i}", "n", AntiPatternSeverity.Error, "f", i, "m"))
            .ToList();
        var summary = DoctorTool.Grade(antiPatterns, []);
        Assert.Equal('F', summary.Grade);
    }

    [Fact]
    public void Grade_TwoStarvationRisks_ReturnsF()
    {
        var handlers = new[]
        {
            new MessageHandlerFinding("f", 1, "H1", "void", FanOutVerdict.SilentStarvationRisk),
            new MessageHandlerFinding("f", 2, "H2", "Task", FanOutVerdict.SilentStarvationRisk),
        };
        var summary = DoctorTool.Grade([], handlers);
        Assert.Equal('F', summary.Grade);
        Assert.Equal(2, summary.SilentStarvationRisks);
    }

    [Fact]
    public void Grade_OneStarvationRisk_ReturnsC()
    {
        var handlers = new[]
        {
            new MessageHandlerFinding("f", 1, "H1", "void", FanOutVerdict.SilentStarvationRisk),
        };
        var summary = DoctorTool.Grade([], handlers);
        Assert.Equal('C', summary.Grade);
    }

    // -------------------------------------------------------------------------
    // Top-fix prioritisation
    // -------------------------------------------------------------------------

    [Fact]
    public void TopFixes_PrioritisesStarvationOverErrors()
    {
        var antiPatterns = new[]
        {
            new AntiPatternFinding("MAF-AP-SEC-001", "Sec rule", AntiPatternSeverity.Error, "src/A.cs", 5, "match"),
        };
        var handlers = new[]
        {
            new MessageHandlerFinding("src/W.cs", 10, "VoidHandler", "void", FanOutVerdict.SilentStarvationRisk),
        };

        var summary = DoctorTool.Grade(antiPatterns, handlers);

        // First fix should be the starvation risk (priority 1), not the security error (priority 2).
        Assert.Equal("MafValidateFanOut", summary.TopFixes[0].Source);
        Assert.Equal("MafScanAntiPatterns", summary.TopFixes[1].Source);
    }

    [Fact]
    public void TopFixes_Caps3()
    {
        var antiPatterns = Enumerable.Range(0, 10)
            .Select(i => new AntiPatternFinding($"R{i}", "n", AntiPatternSeverity.Error, "f", i, "m"))
            .ToList();
        var summary = DoctorTool.Grade(antiPatterns, []);
        Assert.Equal(3, summary.TopFixes.Count);
    }

    // -------------------------------------------------------------------------
    // MCP tool entry
    // -------------------------------------------------------------------------

    [Fact]
    public void MafDoctor_EmptyPath_ReturnsError()
    {
        var tool = new DoctorTool();
        Assert.Contains("Error", tool.MafDoctor(""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafDoctor_NonexistentDirectory_ReturnsError()
    {
        var tool = new DoctorTool();
        Assert.Contains("Error", tool.MafDoctor("/path/that/definitely/does/not/exist"),
            StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Phase I.4 — Composition: Grade() over findings from every real scanner
    // running on the same source. Catches breakage in the 4-scanner fan-in
    // (anti-pattern + fan-out + prompt + cost). Hermetic — string sources only.
    // -------------------------------------------------------------------------

    [Fact]
    public void Grade_AggregatesFromAllFourRealScanners_SurfacesSecurityError()
    {
        // Arrange — source that legitimately trips MAF-AP-SEC-001 (DefaultAzureCredential).
        const string source = """
            using Azure.Identity;
            public class BadAgent
            {
                public BadAgent()
                {
                    var cred = new DefaultAzureCredential();
                }
            }
            """;

        // Act — run every scanner pure on the source, then aggregate.
        var antiPatterns = AntiPatternScannerTool.ScanFile(source, "src/BadAgent.cs");
        var handlers = FanOutValidatorTool.AnalyzeSource(source, "src/BadAgent.cs");
        var prompts = PromptLintTool.LintSource(source, "src/BadAgent.cs");
        var costs = EstimateCostTool.AnalyzeSource(source, "src/BadAgent.cs");

        var summary = DoctorTool.Grade(antiPatterns, handlers, prompts, costs);

        // Assert — the security error propagates to the grade AND the top-fix list.
        Assert.True(summary.AntiPatternErrors >= 1,
            "DefaultAzureCredential should have tripped MAF-AP-SEC-001 — composition is broken.");
        Assert.NotEqual('A', summary.Grade);
        Assert.Contains(summary.TopFixes,
            fix => fix.Description.Contains("MAF-AP-SEC-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Grade_CleanSource_AllFourScanners_StaysAGrade()
    {
        // Arrange — non-MAF source with no patterns any scanner cares about.
        const string source = """
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }
            """;

        // Act
        var antiPatterns = AntiPatternScannerTool.ScanFile(source, "src/Calculator.cs");
        var handlers = FanOutValidatorTool.AnalyzeSource(source, "src/Calculator.cs");
        var prompts = PromptLintTool.LintSource(source, "src/Calculator.cs");
        var costs = EstimateCostTool.AnalyzeSource(source, "src/Calculator.cs");

        var summary = DoctorTool.Grade(antiPatterns, handlers, prompts, costs);

        // Assert
        Assert.Equal('A', summary.Grade);
        Assert.Empty(summary.TopFixes);
    }
}
