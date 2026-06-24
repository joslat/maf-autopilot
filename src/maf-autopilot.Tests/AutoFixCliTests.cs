using MafDoctor.Commands;
using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Unit tests for the <c>autofix-all</c> CLI surface: argument parsing
/// (<see cref="AutoFixCli.Parse"/>) and the human-readable renderer
/// (<see cref="AutoFixCli.Format"/>). The rewriting itself is covered by
/// <c>AutoFixToolTests</c>; these tests stay process-free.
/// </summary>
public class AutoFixCliTests
{
    // -------------------------------------------------------------------------
    // Parse
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_NoFlags_DefaultsToHumanNoDryRun()
    {
        var (path, json, dryRun) = AutoFixCli.Parse(new[] { "autofix-all", "." });
        Assert.Equal(".", path);
        Assert.False(json);
        Assert.False(dryRun);
    }

    [Fact]
    public void Parse_JsonFlag_SetsJson()
    {
        var (_, json, _) = AutoFixCli.Parse(new[] { "autofix-all", ".", "--json" });
        Assert.True(json);
    }

    [Fact]
    public void Parse_DryRunFlag_SetsDryRun()
    {
        var (_, _, dryRun) = AutoFixCli.Parse(new[] { "autofix-all", ".", "--dry-run" });
        Assert.True(dryRun);
    }

    [Fact]
    public void Parse_FlagBeforePath_StillCapturesPath()
    {
        // `autofix-all --json .` — the flag must not be mistaken for the path.
        var (path, json, _) = AutoFixCli.Parse(new[] { "autofix-all", "--json", "." });
        Assert.Equal(".", path);
        Assert.True(json);
    }

    [Fact]
    public void Parse_NoPath_ReturnsNullPath()
    {
        // Caller (Program.cs) substitutes the current directory for a null path.
        var (path, _, _) = AutoFixCli.Parse(new[] { "autofix-all", "--dry-run" });
        Assert.Null(path);
    }

    [Fact]
    public void Parse_BothFlags_SetsBoth()
    {
        var (path, json, dryRun) = AutoFixCli.Parse(new[] { "autofix-all", "src", "--dry-run", "--json" });
        Assert.Equal("src", path);
        Assert.True(json);
        Assert.True(dryRun);
    }

    // -------------------------------------------------------------------------
    // Format — zero changes (the most-misread output)
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_ZeroChanges_ExplainsAndPointsToDoctor()
    {
        var report = new AutoFixTool.AutoFixAllReport(
            DryRun: false,
            OrderOfExecution: AutoFixTool.OrderedRuleIds,
            TotalDistinctFilesChanged: 0,
            AffectedFiles: Array.Empty<string>(),
            PerRule: AutoFixTool.OrderedRuleIds
                .Select(id => new AutoFixTool.AutoFixRuleReport(id, 0, Array.Empty<string>()))
                .ToList());

        var output = AutoFixCli.Format(report);

        Assert.Contains("0 files changed", output);
        // Must reassure that 0 != clean, and route the user to the right next step.
        Assert.Contains("does NOT mean your code is clean", output);
        Assert.Contains("maf-doctor doctor", output);
        Assert.Contains("@maf-migration", output);
        // Every supported rule is listed so the user sees what *is* covered.
        foreach (var id in AutoFixTool.OrderedRuleIds)
            Assert.Contains(id, output);
    }

    // -------------------------------------------------------------------------
    // Format — with changes
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_WithChanges_ListsCountsAndFiles()
    {
        var report = new AutoFixTool.AutoFixAllReport(
            DryRun: false,
            OrderOfExecution: AutoFixTool.OrderedRuleIds,
            TotalDistinctFilesChanged: 2,
            AffectedFiles: new[] { "A.cs", "B.cs" },
            PerRule: new[]
            {
                new AutoFixTool.AutoFixRuleReport("MAF-AP-WF-001", 1, new[] { "A.cs" }),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-SEC-003", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-SEC-001", 1, new[] { "B.cs" }),
                new AutoFixTool.AutoFixRuleReport("MAF130-FAN-IN-001", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-CONC-002", 0, Array.Empty<string>()),
            });

        var output = AutoFixCli.Format(report);

        Assert.Contains("2 files changed", output);
        Assert.Contains("A.cs", output);
        Assert.Contains("B.cs", output);
        Assert.DoesNotContain("does NOT mean your code is clean", output);
        // Pin the apply-vs-dry-run wording split in the OTHER direction too: the real-apply
        // path must NOT leak the dry-run verb or footer.
        Assert.DoesNotContain("would change", output);
        Assert.DoesNotContain("re-run without --dry-run", output);
    }

    [Fact]
    public void Format_DryRun_UsesConditionalWording()
    {
        var report = new AutoFixTool.AutoFixAllReport(
            DryRun: true,
            OrderOfExecution: AutoFixTool.OrderedRuleIds,
            TotalDistinctFilesChanged: 1,
            AffectedFiles: new[] { "A.cs" },
            PerRule: new[]
            {
                new AutoFixTool.AutoFixRuleReport("MAF-AP-WF-001", 1, new[] { "A.cs" }),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-SEC-003", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-SEC-001", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF130-FAN-IN-001", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-CONC-002", 0, Array.Empty<string>()),
            });

        var output = AutoFixCli.Format(report);

        Assert.Contains("would change", output);
        Assert.Contains("dry run", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("re-run without --dry-run", output);
    }

    [Fact]
    public void Format_SingularFile_UsesSingularNoun()
    {
        var report = new AutoFixTool.AutoFixAllReport(
            DryRun: false,
            OrderOfExecution: AutoFixTool.OrderedRuleIds,
            TotalDistinctFilesChanged: 1,
            AffectedFiles: new[] { "A.cs" },
            PerRule: new[]
            {
                new AutoFixTool.AutoFixRuleReport("MAF-AP-WF-001", 1, new[] { "A.cs" }),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-SEC-003", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-SEC-001", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF130-FAN-IN-001", 0, Array.Empty<string>()),
                new AutoFixTool.AutoFixRuleReport("MAF-AP-CONC-002", 0, Array.Empty<string>()),
            });

        var output = AutoFixCli.Format(report);

        Assert.Contains("1 file changed", output);
        Assert.DoesNotContain("1 files changed", output);
    }
}
