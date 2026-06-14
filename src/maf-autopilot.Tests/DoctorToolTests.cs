using System.Text.Json;
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

    // -------------------------------------------------------------------------
    // JSON output format (format: "json")
    // -------------------------------------------------------------------------

    [Fact]
    public void MafDoctor_JsonFormat_ReturnsValidJson()
    {
        var tool = new DoctorTool();
        // Use a path that exists but has no MAF .cs files — should return grade A with valid JSON.
        var tempDir = Path.Combine(Path.GetTempPath(), "maf-doctor-json-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var output = tool.MafDoctor(tempDir, format: "json");

            // Must parse as valid JSON
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            // Required fields
            Assert.Equal("1", root.GetProperty("schema_version").GetString());
            var verdict = root.GetProperty("verdict").GetString();
            Assert.Contains(verdict, new[] { "A", "B", "C", "F" });
            Assert.True(root.TryGetProperty("errors_count", out _));
            Assert.True(root.TryGetProperty("warnings_count", out _));
            Assert.True(root.TryGetProperty("silent_starvation_risks", out _));
            Assert.True(root.TryGetProperty("top_fixes", out _));
            Assert.True(root.TryGetProperty("summary_md", out _));

            // Grade A for empty dir
            Assert.Equal("A", verdict);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void MafDoctor_JsonFormat_DefaultIsMarkdown()
    {
        var tool = new DoctorTool();
        var tempDir = Path.Combine(Path.GetTempPath(), "maf-doctor-md-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // Default call (no format param) should return markdown, not JSON
            var output = tool.MafDoctor(tempDir);
            Assert.Contains("MAF health grade", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"schema_version\"", output);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void MafDoctor_JsonFormat_TopFixesHasDiscreteFields()
    {
        // Arrange a source that trips MAF-AP-SEC-001
        var tempDir = Path.Combine(Path.GetTempPath(), "maf-doctor-fields-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Bad.cs"), """
                using Azure.Identity;
                public class Bad
                {
                    public Bad() { var c = new DefaultAzureCredential(); }
                }
                """);
            var tool = new DoctorTool();
            var output = tool.MafDoctor(tempDir, format: "json");

            using var doc = JsonDocument.Parse(output);
            var fixes = doc.RootElement.GetProperty("top_fixes");
            Assert.True(fixes.GetArrayLength() > 0, "Expected at least one top_fix");
            var fix = fixes[0];

            Assert.Equal("MAF-AP-SEC-001", fix.GetProperty("rule_id").GetString());
            Assert.Contains("Bad.cs", fix.GetProperty("file").GetString() ?? "");
            Assert.True(fix.GetProperty("line").GetInt32() > 0);
            Assert.False(string.IsNullOrEmpty(fix.GetProperty("issue").GetString()));
            Assert.False(string.IsNullOrEmpty(fix.GetProperty("fix_description").GetString()));
            Assert.True(fix.GetProperty("auto_fixable").GetBoolean());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Phase 3.1 — path exclusion. The drift detector scans the repo, which
    // ships intentional anti-pattern bait under samples/ + test fixtures.
    // `Run(..., excludes)` must skip those so the grade reflects product code.
    // -------------------------------------------------------------------------

    [Fact]
    public void Run_ExcludedPaths_DoNotAffectGrade()
    {
        // NOTE: the AntiPatternScanner already self-skips `samples`/`tests` path
        // segments for skipInTestFiles rules (SourceFileWalker isTestFile). The
        // walker-level --exclude is BROADER — it drops files before ANY scanner
        // (incl. fan-out / prompt / cost, which have no such skip). To exercise
        // it cleanly we put the bait under a non-sample dir the scanner does NOT
        // self-skip, then exclude that dir at the walker level.
        var root = Path.Combine(Path.GetTempPath(), "maf-doctor-exclude-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "legacy"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        try
        {
            // Bait under legacy/ — DefaultAzureCredential (MAF-AP-SEC-001 error).
            File.WriteAllText(Path.Combine(root, "legacy", "Bait.cs"), """
                using Azure.Identity;
                public class Bait
                {
                    public Bait() { var a = new DefaultAzureCredential(); }
                }
                """);
            // Clean product code under src/.
            File.WriteAllText(Path.Combine(root, "src", "Calc.cs"),
                "public class Calc { public int Add(int a, int b) => a + b; }");

            var tool = new DoctorTool();

            // Unscoped: the legacy bait drags the grade off A.
            using (var doc = JsonDocument.Parse(tool.Run(root, "json", excludes: null)))
                Assert.NotEqual("A", doc.RootElement.GetProperty("verdict").GetString());

            // Scoped: exclude legacy/ → only clean src/ remains → grade A.
            using (var doc = JsonDocument.Parse(tool.Run(root, "json", new[] { "legacy/" })))
                Assert.Equal("A", doc.RootElement.GetProperty("verdict").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
