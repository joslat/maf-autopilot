using MafDoctor.Data;
using MafDoctor.Tools;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Regression tests for the Phase 3 (scan/exec hardening) findings:
/// WM-16 (parse-once shared root), WM-17 (single migration-risk walk),
/// WM-18 (per-file read isolation), WM-19 (parallel scan determinism),
/// WM-38 (int-overflow-safe diagnostic parse), WM-39 (compute-once AllIds),
/// WM-40 (authoritative AggregateBytes), SEC-04 (truncation surfacing),
/// SEC-17 (bounded csproj / generic walks).
/// </summary>
public sealed class Phase3HardeningTests
{
    // -------------------------------------------------------------------------
    // Temp-dir helper
    // -------------------------------------------------------------------------

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "maf-phase3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // -------------------------------------------------------------------------
    // SEC-09 — credential-like env keys are stripped; build/restore keys are kept
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GITHUB_TOKEN")]
    [InlineData("GH_TOKEN")]
    [InlineData("AWS_SECRET_ACCESS_KEY")]
    [InlineData("AWS_ACCESS_KEY_ID")]
    [InlineData("AZURE_CLIENT_SECRET")]
    [InlineData("MyApi_Password")]
    [InlineData("NUGET_API_KEY")]
    [InlineData("SomeService_PAT")]
    public void IsSensitiveEnvKey_StripsCredentialLikeKeys(string key)
        => Assert.True(ProcessRunner.IsSensitiveEnvKey(key));

    [Theory]
    [InlineData("PATH")]
    [InlineData("HOME")]
    [InlineData("USERPROFILE")]
    [InlineData("TEMP")]
    [InlineData("APPDATA")]
    [InlineData("DOTNET_ROOT")]
    [InlineData("NUGET_PACKAGES")]
    public void IsSensitiveEnvKey_KeepsBuildAndRestoreKeys(string key)
        => Assert.False(ProcessRunner.IsSensitiveEnvKey(key));

    // -------------------------------------------------------------------------
    // WM-38 — oversized line number must not overflow int.Parse
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseBuildOutput_OverflowingLineNumber_SkipsLineWithoutThrowing()
    {
        // 20 nines overflows Int32. The old int.Parse threw OverflowException AFTER
        // paying the full build cost; the valid diagnostic on the next line was lost.
        var output = string.Join('\n', new[]
        {
            @"C:\repo\Bad.cs(99999999999999999999,5): warning CS0618: 'X' is obsolete",
            @"C:\repo\Good.cs(42,5): warning CS0618: 'Y' is obsolete",
        });

        var diagnostics = Cs0618HuntTool.ParseBuildOutput(output);

        // The overflowing line is skipped; the valid one survives.
        Assert.Single(diagnostics);
        Assert.Equal(42, diagnostics[0].Line);
        Assert.EndsWith("Good.cs", diagnostics[0].File);
    }

    // -------------------------------------------------------------------------
    // WM-39 — AllIds computed once (get-only), ordinal order
    // -------------------------------------------------------------------------

    [Fact]
    public void AllIds_IsComputedOnce_SameInstanceAcrossAccesses()
    {
        var registry = new RegistryService();
        var first = registry.AllIds;
        var second = registry.AllIds;

        // A get-only auto-property returns the SAME list instance — no re-sort /
        // re-materialize per access (the old expression-bodied property allocated each time).
        Assert.Same(first, second);
        Assert.Equal(first.OrderBy(id => id, StringComparer.Ordinal), first);
    }

    // -------------------------------------------------------------------------
    // WM-40 — AggregateBytes is the single source of truth for the aggregate cap
    // -------------------------------------------------------------------------

    [Fact]
    public void ScanBudget_MaxTotalBytes_DefaultsToAggregateBytesConstant()
    {
        Assert.Equal(500L * 1024 * 1024, BoundedInput.AggregateBytes);
        Assert.Equal(BoundedInput.AggregateBytes, new SourceFileWalker.ScanBudget().MaxTotalBytes);
    }

    // -------------------------------------------------------------------------
    // SEC-04 — ScanBudget.IncompleteNote surfaces every incompleteness cause
    // -------------------------------------------------------------------------

    [Fact]
    public void IncompleteNote_CleanScan_ReturnsNull()
    {
        var budget = new SourceFileWalker.ScanBudget();
        budget.TryAccept(10);
        Assert.Null(budget.IncompleteNote());
    }

    [Fact]
    public void IncompleteNote_Truncated_MentionsRepoSizeCap()
    {
        var budget = new SourceFileWalker.ScanBudget { MaxFiles = 1 };
        Assert.True(budget.TryAccept(10));   // accepted
        Assert.False(budget.TryAccept(10));  // over the file-count cap → Truncated

        var note = budget.IncompleteNote();
        Assert.NotNull(note);
        Assert.Contains("repo-size cap", note);
    }

    [Fact]
    public void IncompleteNote_OversizedAndUnreadable_MentionBoth()
    {
        var budget = new SourceFileWalker.ScanBudget();
        budget.TryAccept(10);
        budget.RecordOversizedSkip();

        var note = budget.IncompleteNote(skippedUnreadable: 3);
        Assert.NotNull(note);
        Assert.Contains("oversized", note);
        Assert.Contains("unreadable", note);
    }

    // -------------------------------------------------------------------------
    // SEC-17 — csproj / generic walks are bounded and skip bin/obj
    // -------------------------------------------------------------------------

    [Fact]
    public void EnumerateCsprojFiles_SkipsBinAndObj()
    {
        var dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "App.csproj"), "<Project/>");
            Directory.CreateDirectory(Path.Combine(dir, "obj"));
            File.WriteAllText(Path.Combine(dir, "obj", "App.g.csproj"), "<Project/>");
            Directory.CreateDirectory(Path.Combine(dir, "bin"));
            File.WriteAllText(Path.Combine(dir, "bin", "Ref.csproj"), "<Project/>");

            var found = SourceFileWalker.EnumerateCsprojFiles(dir).ToList();

            Assert.Single(found);
            Assert.EndsWith("App.csproj", found[0]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void EnumerateFiles_RespectsFileCountBudget()
    {
        var dir = NewTempDir();
        try
        {
            for (var i = 0; i < 3; i++)
                File.WriteAllText(Path.Combine(dir, $"f{i}.props"), "x");

            var budget = new SourceFileWalker.ScanBudget { MaxFiles = 2 };
            var found = SourceFileWalker.EnumerateFiles(dir, "*.props", budget).ToList();

            Assert.Equal(2, found.Count);
            Assert.True(budget.Truncated);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // -------------------------------------------------------------------------
    // WM-16 — the root-accepting overloads produce identical results to the
    // string overloads (which now parse-and-delegate to them)
    // -------------------------------------------------------------------------

    [Fact]
    public void RootOverloads_ProduceIdenticalFindings_ToStringOverloads()
    {
        const string src = """
            using Microsoft.Agents.AI.Workflows;
            public class Prod
            {
                public void M()
                {
                    var cred = new DefaultAzureCredential();
                    var r = Compute().Result;
                    var opts = new ChatClientAgentOptions { Instructions = "" };
                    agent.RunAsync("hi");
                }
                [MessageHandler]
                public void Handle(string s) { }
            }
            """;
        var root = CSharpSyntaxTree.ParseText(src).GetRoot();

        Assert.Equal(
            AntiPatternScannerTool.ScanFile(src, "Prod.cs"),
            AntiPatternScannerTool.ScanFile(src, root, "Prod.cs"));
        Assert.Equal(
            FanOutValidatorTool.AnalyzeSource(src, "Prod.cs"),
            FanOutValidatorTool.AnalyzeSource(root, "Prod.cs"));
        Assert.Equal(
            PromptLintTool.LintSource(src, "Prod.cs"),
            PromptLintTool.LintSource(root, "Prod.cs"));
        Assert.Equal(
            EstimateCostTool.AnalyzeSource(src, "Prod.cs"),
            EstimateCostTool.AnalyzeSource(root, "Prod.cs"));
    }

    // -------------------------------------------------------------------------
    // WM-17 — pure per-source needle counter
    // -------------------------------------------------------------------------

    [Fact]
    public void CountMatches_SumsEveryOccurrenceOfEveryNeedle()
    {
        const string src = "AgentThread AgentThread MapA2A";
        Assert.Equal(3, MigrationRiskScoreTool.CountMatches(src, "AgentThread", "MapA2A"));
        Assert.Equal(0, MigrationRiskScoreTool.CountMatches(src, "NotPresent"));
        Assert.Equal(0, MigrationRiskScoreTool.CountMatches(src, ""));
    }

    // -------------------------------------------------------------------------
    // WM-19 — parallel scan is output-deterministic
    // -------------------------------------------------------------------------

    [Fact]
    public void AnalyzeRepo_ParallelScan_IsDeterministicAcrossRuns()
    {
        var dir = NewTempDir();
        try
        {
            // Several files, each with a distinct anti-pattern, so the parallel merge
            // has real content to (potentially) reorder.
            for (var i = 0; i < 8; i++)
            {
                File.WriteAllText(Path.Combine(dir, $"Prod{i}.cs"), $$"""
                    public class Prod{{i}}
                    {
                        public void M()
                        {
                            var cred = new DefaultAzureCredential();
                            var opts = new ChatClientAgentOptions { Instructions = "" };
                        }
                    }
                    """);
            }

            static (char Grade, int Files, string Fixes) Project(DoctorSummary s) =>
                (s.Grade, s.FilesScanned,
                 string.Join("|", s.AllFixes.Select(f => $"{f.RuleId}:{f.File}:{f.Line}")));

            var a = Project(DoctorTool.AnalyzeRepo(dir, excludes: null));
            var b = Project(DoctorTool.AnalyzeRepo(dir, excludes: null));

            Assert.Equal(a, b);
            Assert.Equal(8, a.Files);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
