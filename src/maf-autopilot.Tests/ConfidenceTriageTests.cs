using System.Text.Json;
using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for the finding-confidence triage signal (certain / high / heuristic) that
/// drives false-positive triage in doctor / --plan / --json and the maf-remediate loop.
/// </summary>
public sealed class ConfidenceTriageTests
{
    [Theory]
    // Structural AST rules, low residual FP → high.
    [InlineData("MAF001", "high")]
    [InlineData("MAF-AP-SEC-003", "high")]
    [InlineData("MAF-AP-CONC-001", "high")]
    [InlineData("MAF-AP-CONC-002", "high")]
    [InlineData("MAF-AP-DEVUI-001", "high")]
    [InlineData("MAF-AP-EXEC-001", "high")]
    // Name-only / text / scope-limited → heuristic (verify first).
    [InlineData("COST-001", "heuristic")]
    [InlineData("MAF-AP-SEC-002", "heuristic")]
    [InlineData("MAF-AP-OBS-001", "heuristic")]
    [InlineData("MAF-AP-MID-001", "heuristic")]
    [InlineData("PROMPT-004", "heuristic")]
    // Compiler ground-truth → certain.
    [InlineData("CS0618", "certain")]
    public void ConfidenceFor_MapsRuleToExpectedTier(string ruleId, string expected)
    {
        Assert.Equal(expected, DoctorTool.ConfidenceFor(ruleId));
    }

    [Fact]
    public void ConfidenceFor_UnknownRule_DefaultsToHeuristic()
    {
        // Verify-first default so a new/unclassified rule errs toward human review.
        Assert.Equal("heuristic", DoctorTool.ConfidenceFor("MAF-AP-SOMETHING-NEW-999"));
    }

    [Fact]
    public void Json_CarriesConfidencePerFinding()
    {
        // A temp repo with one high-confidence finding (DEVUI-001) and one heuristic
        // (COST-001) — both must surface their confidence so a remediation loop can triage.
        using var repo = new TempRepo(
            ("Program.cs", """
                using Microsoft.Agents.AI.Hosting;
                public class Program
                {
                    public static async System.Threading.Tasks.Task Main(dynamic agent)
                    {
                        var r = await agent.RunAsync("hi");
                    }
                }
                """));

        var json = new DoctorTool().Run(repo.Path, format: "json", excludes: new List<string>(), full: true);
        using var doc = JsonDocument.Parse(json);
        var fixes = doc.RootElement.GetProperty("top_fixes");

        Assert.Contains(fixes.EnumerateArray(), f =>
            f.GetProperty("rule_id").GetString() == "MAF-AP-DEVUI-001"
            && f.GetProperty("confidence").GetString() == "high");
        Assert.Contains(fixes.EnumerateArray(), f =>
            f.GetProperty("rule_id").GetString() == "COST-001"
            && f.GetProperty("confidence").GetString() == "heuristic");
    }

    [Fact]
    public void Plan_FlagsHeuristicFindingsForVerification()
    {
        using var repo = new TempRepo(
            ("Program.cs", """
                public class Program
                {
                    public static async System.Threading.Tasks.Task Main(dynamic agent)
                    {
                        var r = await agent.RunAsync("hi");   // COST-001 — heuristic
                    }
                }
                """));

        var plan = new DoctorTool().Run(repo.Path, format: "plan", excludes: new List<string>(), full: true);
        Assert.Contains("heuristic", plan, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verify first", plan, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A throwaway directory with seeded .cs files, deleted on dispose.</summary>
    private sealed class TempRepo : System.IDisposable
    {
        public string Path { get; }
        public TempRepo(params (string Name, string Content)[] files)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "maf-conf-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Path);
            foreach (var (name, content) in files)
                System.IO.File.WriteAllText(System.IO.Path.Combine(Path, name), content);
        }
        public void Dispose()
        {
            try { System.IO.Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }
}
