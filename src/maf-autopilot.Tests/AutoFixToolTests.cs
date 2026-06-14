using System.Text.Json;
using MafDoctor.Tools;
using MafDoctor.Tools.Rewriters;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Phase W.6 — tests for <see cref="AutoFixTool"/> + each rewriter.
///
/// Pattern: each rewriter test parses a synthetic input, runs the rewriter
/// directly (no I/O), asserts the output's text. Plus an end-to-end test
/// that exercises <see cref="AutoFixTool.MafAutoFix"/> against a temp dir
/// with a real sample file.
/// </summary>
public class AutoFixToolTests
{
    private static string ApplyRewriter(IRuleRewriter rewriter, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var newRoot = rewriter.Visit(tree.GetRoot());
        return newRoot.ToFullString();
    }

    // -------------------------------------------------------------------------
    // DefaultAzureCredentialRewriter (MAF-AP-SEC-001 / MAF002)
    // -------------------------------------------------------------------------

    [Fact]
    public void DefaultAzureCredentialRewriter_RewritesToManagedIdentity()
    {
        var input = """
            class C { object F() => new DefaultAzureCredential(); }
            """;
        var output = ApplyRewriter(new DefaultAzureCredentialRewriter(), input);
        Assert.Contains("new ManagedIdentityCredential()", output);
        Assert.DoesNotContain("DefaultAzureCredential", output);
    }

    [Fact]
    public void DefaultAzureCredentialRewriter_PreservesLeadingComment()
    {
        var input = """
            class C
            {
                // important comment about credentials
                object F() => new DefaultAzureCredential();
            }
            """;
        var output = ApplyRewriter(new DefaultAzureCredentialRewriter(), input);
        Assert.Contains("// important comment about credentials", output);
        Assert.Contains("new ManagedIdentityCredential()", output);
    }

    // -------------------------------------------------------------------------
    // EnableSensitiveDataRewriter (MAF-AP-SEC-003 / MAF003)
    // -------------------------------------------------------------------------

    [Fact]
    public void EnableSensitiveDataRewriter_DropsAssignment()
    {
        var input = """
            class C
            {
                object F() => new ChatOptions
                {
                    EnableSensitiveData = true,
                    Temperature = 0.7,
                };
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.DoesNotContain("EnableSensitiveData", output);
        Assert.Contains("Temperature = 0.7", output);
    }

    [Fact]
    public void EnableSensitiveDataRewriter_DropsDictionaryStyle()
    {
        var input = """
            class C
            {
                object F() => new AdditionalPropertiesDictionary
                {
                    ["EnableSensitiveData"] = true,
                    ["Other"] = 1,
                };
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.DoesNotContain("EnableSensitiveData", output);
        Assert.Contains("[\"Other\"] = 1", output);
    }

    [Fact]
    public void EnableSensitiveDataRewriter_NoOpIfFalse()
    {
        var input = """
            class C { object F() => new ChatOptions { EnableSensitiveData = false }; }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        // EnableSensitiveData = false is fine — only `true` triggers the fix.
        Assert.Contains("EnableSensitiveData = false", output);
    }

    // -------------------------------------------------------------------------
    // Phase 4.G fixup — shape C (member-access statement) + shape D (standalone
    // element-access statement). Analyzer Phase 4.4 caught these; rewriter
    // was missing parity. Now closed.
    // -------------------------------------------------------------------------

    [Fact]
    public void EnableSensitiveDataRewriter_MemberAccessStatement_StatementRemoved()
    {
        var input = """
            class C
            {
                void Configure()
                {
                    var opts = new ChatOptions();
                    opts.EnableSensitiveData = true;
                    opts.MaxTokens = 1024;
                }
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.DoesNotContain("opts.EnableSensitiveData = true", output);
        Assert.Contains("opts.MaxTokens = 1024", output);
    }

    [Fact]
    public void EnableSensitiveDataRewriter_ElementAccessStatement_StatementRemoved()
    {
        var input = """
            using System.Collections.Generic;
            class C
            {
                void Configure()
                {
                    var dict = new Dictionary<string, object>();
                    dict["EnableSensitiveData"] = true;
                    dict["KeepThis"] = true;
                }
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.DoesNotContain("dict[\"EnableSensitiveData\"]", output);
        Assert.Contains("dict[\"KeepThis\"]", output);
    }

    [Fact]
    public void EnableSensitiveDataRewriter_MemberAccessStatementFalse_NotRemoved()
    {
        var input = """
            class C
            {
                void Configure()
                {
                    var opts = new ChatOptions();
                    opts.EnableSensitiveData = false;
                }
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.Contains("opts.EnableSensitiveData = false", output);
    }

    // -------------------------------------------------------------------------
    // Phase 7-final fixup — embedded-statement contexts.
    //
    // Returning `null` from `VisitExpressionStatement` to remove a statement
    // CRASHES Roslyn's `VisitIfStatement` / `VisitForStatement` / etc. when
    // the single embedded child is a removable assignment (the parent is
    // not a block and expects a non-null replacement). Fix: substitute an
    // empty `;` statement in those positions.
    //
    // The crash was caught by AutoFixTool's outer try/catch — but the rule
    // silently no-op'd on those files, re-creating the analyzer/rewriter
    // asymmetry Phase 4.G was meant to close. Tests below pin every
    // embedded shape.
    // -------------------------------------------------------------------------

    [Fact]
    public void EnableSensitiveDataRewriter_EmbeddedInSingleLineIf_ReplacedWithEmptyStatement()
    {
        var input = """
            class C
            {
                void Configure(bool cond)
                {
                    var opts = new ChatOptions();
                    if (cond) opts.EnableSensitiveData = true;
                    opts.MaxTokens = 1024;
                }
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        // The dangerous assignment is gone.
        Assert.DoesNotContain("opts.EnableSensitiveData = true", output);
        // Surrounding statements survive intact.
        Assert.Contains("opts.MaxTokens = 1024", output);
        // The output re-parses cleanly (no crash, no orphan tokens).
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(output);
        Assert.Empty(tree.GetDiagnostics().Where(
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
    }

    [Fact]
    public void EnableSensitiveDataRewriter_EmbeddedInSingleLineFor_ReplacedWithEmptyStatement()
    {
        var input = """
            class C
            {
                void Configure()
                {
                    var opts = new ChatOptions();
                    for (int i = 0; i < 10; i++) opts.EnableSensitiveData = true;
                }
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.DoesNotContain("opts.EnableSensitiveData = true", output);
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(output);
        Assert.Empty(tree.GetDiagnostics().Where(
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
    }

    [Fact]
    public void EnableSensitiveDataRewriter_EmbeddedInIfElse_ReplacedWithEmptyStatement()
    {
        var input = """
            class C
            {
                void Configure(bool cond)
                {
                    var opts = new ChatOptions();
                    if (cond) { opts.MaxTokens = 100; }
                    else opts.EnableSensitiveData = true;
                }
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.DoesNotContain("opts.EnableSensitiveData = true", output);
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(output);
        Assert.Empty(tree.GetDiagnostics().Where(
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
    }

    [Fact]
    public void EnableSensitiveDataRewriter_EmbeddedDictionaryStyleInSingleLineIf_Handled()
    {
        var input = """
            using System.Collections.Generic;
            class C
            {
                void Configure(bool cond, Dictionary<string, object> dict)
                {
                    if (cond) dict["EnableSensitiveData"] = true;
                }
            }
            """;
        var output = ApplyRewriter(new EnableSensitiveDataRewriter(), input);
        Assert.DoesNotContain("[\"EnableSensitiveData\"] = true", output);
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(output);
        Assert.Empty(tree.GetDiagnostics().Where(
            d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
    }

    // -------------------------------------------------------------------------
    // ExecutorSealedRewriter (MAF-AP-WF-001)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExecutorSealedRewriter_AddsSealedToPartialClass()
    {
        var input = """
            using Microsoft.Agents.AI.Workflows;
            public partial class MyExec : Executor
            {
                [MessageHandler]
                public ValueTask H(int x) => default;
            }
            """;
        var output = ApplyRewriter(new ExecutorSealedRewriter(), input);
        Assert.Contains("sealed partial class MyExec", output);
    }

    [Fact]
    public void ExecutorSealedRewriter_NoOpIfAlreadySealed()
    {
        var input = """
            using Microsoft.Agents.AI.Workflows;
            public sealed partial class MyExec : Executor
            {
                [MessageHandler]
                public ValueTask H(int x) => default;
            }
            """;
        var output = ApplyRewriter(new ExecutorSealedRewriter(), input);
        // Must NOT double-add `sealed`.
        Assert.Equal(input, output.TrimEnd());
    }

    [Fact]
    public void ExecutorSealedRewriter_NoOpIfNoMessageHandler()
    {
        // Class derives from Executor but has no [MessageHandler] method.
        // Rewriter must not touch it (might be a base class or fixture).
        var input = """
            using Microsoft.Agents.AI.Workflows;
            public partial class MyExec : Executor
            {
                public ValueTask H(int x) => default;
            }
            """;
        var output = ApplyRewriter(new ExecutorSealedRewriter(), input);
        Assert.Equal(input, output.TrimEnd());
    }

    // -------------------------------------------------------------------------
    // FanInArgOrderRewriter (MAF130-FAN-IN-001)
    // -------------------------------------------------------------------------

    [Fact]
    public void FanInArgOrderRewriter_SwapsPositionalArgs()
    {
        var input = """
            class C
            {
                void F(object builder, object aggregator, object osint, object history)
                {
                    builder.AddFanInBarrierEdge(aggregator, new[] { osint, history });
                }
            }
            """;
        var output = ApplyRewriter(new FanInArgOrderRewriter(), input);
        // Expect `(new[] { ... }, aggregator)` order.
        var idxArray = output.IndexOf("new[] {");
        var idxAggregator = output.IndexOf("aggregator", StringComparison.Ordinal);
        // Skip the method-parameter occurrences ("object aggregator," in the signature).
        var idxAggregatorInCall = output.IndexOf("aggregator", idxArray, StringComparison.Ordinal);
        Assert.True(idxArray > 0);
        Assert.True(idxAggregatorInCall > idxArray, "Expected `aggregator` to appear AFTER the array in the new call.");
    }

    [Fact]
    public void FanInArgOrderRewriter_NoOpIfAlreadyCorrect()
    {
        var input = """
            class C
            {
                void F(object builder, object aggregator, object osint, object history)
                {
                    builder.AddFanInBarrierEdge(new[] { osint, history }, aggregator);
                }
            }
            """;
        var output = ApplyRewriter(new FanInArgOrderRewriter(), input);
        Assert.Equal(input, output.TrimEnd());
    }

    [Fact]
    public void FanInArgOrderRewriter_SwapsNamedArgs()
    {
        var input = """
            class C
            {
                void F(object builder, object aggregator, object osint, object history)
                {
                    builder.AddFanInBarrierEdge(target: aggregator, sources: new[] { osint, history });
                }
            }
            """;
        var output = ApplyRewriter(new FanInArgOrderRewriter(), input);
        // Named labels stripped + args reordered.
        Assert.DoesNotContain("target:", output);
        Assert.DoesNotContain("sources:", output);
    }

    // -------------------------------------------------------------------------
    // SyncOverAsyncRewriter (MAF-AP-CONC-002)
    // -------------------------------------------------------------------------

    [Fact]
    public void SyncOverAsyncRewriter_ReplacesResultWithAwait()
    {
        var input = """
            using System.Threading.Tasks;
            class C { async Task F() { var x = SomeAsync().Result; } static Task<int> SomeAsync() => default; }
            """;
        var output = ApplyRewriter(new SyncOverAsyncRewriter(), input);
        Assert.True(output.Contains("await SomeAsync()"), $"Output:\n{output}");
        Assert.DoesNotContain(".Result", output);
    }

    [Fact]
    public void SyncOverAsyncRewriter_ReplacesWaitWithAwait()
    {
        var input = """
            using System.Threading.Tasks;
            class C { async Task F() { SomeAsync().Wait(); } static Task SomeAsync() => default; }
            """;
        var output = ApplyRewriter(new SyncOverAsyncRewriter(), input);
        Assert.True(output.Contains("await SomeAsync()"), $"Output:\n{output}");
        Assert.DoesNotContain(".Wait(", output);
    }

    // -------------------------------------------------------------------------
    // AutoFixTool — orchestration
    // -------------------------------------------------------------------------

    [Fact]
    public void AutoFixTool_UnknownRule_ReturnsErrorJson()
    {
        var tool = new AutoFixTool();
        var result = tool.MafAutoFix(Path.GetTempPath(), "MAF-AP-NOT-A-RULE");
        Assert.Contains("Unknown ruleId", result);
    }

    [Fact]
    public void AutoFixTool_EmptyPath_ReturnsErrorJson()
    {
        var tool = new AutoFixTool();
        var result = tool.MafAutoFix("", "MAF-AP-SEC-001");
        // PathGuard rejects empty.
        Assert.Contains("error", result);
    }

    [Fact]
    public void AutoFixTool_DryRun_DoesNotWriteFiles()
    {
        // Arrange — temp dir with one offending file.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var testFile = Path.Combine(tempRoot, "Bad.cs");
        var original = "class C { object F() => new DefaultAzureCredential(); }";
        File.WriteAllText(testFile, original);

        try
        {
            // Act — dry run.
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFix(tempRoot, "MAF-AP-SEC-001", dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;

            // Assert — JSON says 1 file would change, but disk is untouched.
            Assert.Equal(1, result.RootElement.GetProperty("filesChanged").GetInt32());
            Assert.True(result.RootElement.GetProperty("dryRun").GetBoolean());
            Assert.Equal(original, File.ReadAllText(testFile));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AutoFixTool_AppliesFixAndWritesFile()
    {
        // Arrange — temp dir with one offending file.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var testFile = Path.Combine(tempRoot, "Bad.cs");
        File.WriteAllText(testFile, "class C { object F() => new DefaultAzureCredential(); }");

        try
        {
            // Act.
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFix(tempRoot, "MAF-AP-SEC-001");
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;

            // Assert — JSON says 1 file changed; disk reflects the rewrite.
            Assert.Equal(1, result.RootElement.GetProperty("filesChanged").GetInt32());
            Assert.False(result.RootElement.GetProperty("dryRun").GetBoolean());
            Assert.Contains("ManagedIdentityCredential", File.ReadAllText(testFile));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AutoFixTool_AliasedRuleIds_AreEquivalent()
    {
        // MAF-AP-SEC-001 and MAF002 must produce the same rewriter behaviour.
        Assert.Contains("MAF-AP-SEC-001", AutoFixTool.SupportedRuleIds);
        Assert.Contains("MAF002", AutoFixTool.SupportedRuleIds);
        Assert.Contains("MAF-AP-SEC-003", AutoFixTool.SupportedRuleIds);
        Assert.Contains("MAF003", AutoFixTool.SupportedRuleIds);
    }

    // -------------------------------------------------------------------------
    // MafAutoFixAll — the "fix everything fixable" batch command
    // -------------------------------------------------------------------------

    [Fact]
    public void AutoFixAll_EmptyPath_ReturnsErrorJson()
    {
        var tool = new AutoFixTool();
        var result = tool.MafAutoFixAll("");
        Assert.Contains("error", result);
    }

    [Fact]
    public void AutoFixAll_AppliesAllRulesInOrder()
    {
        // Arrange — temp dir with files triggering multiple rules at once.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-all-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "Sec.cs"),
            "class C { object F() => new DefaultAzureCredential(); }");
        File.WriteAllText(Path.Combine(tempRoot, "Sensitive.cs"),
            "class C { object F() => new ChatOptions { EnableSensitiveData = true, Temperature = 0.5 }; }");

        try
        {
            // Act.
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFixAll(tempRoot);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;

            // Assert — 2 distinct files changed across the rule set.
            Assert.Equal(2, result.RootElement.GetProperty("totalDistinctFilesChanged").GetInt32());

            // The per-rule breakdown should show the SEC-001 + SEC-003 entries.
            var perRule = result.RootElement.GetProperty("perRule");
            Assert.Equal(1, perRule.GetProperty("MAF-AP-SEC-001").GetProperty("filesChanged").GetInt32());
            Assert.Equal(1, perRule.GetProperty("MAF-AP-SEC-003").GetProperty("filesChanged").GetInt32());

            // Disk state confirms the rewrites.
            Assert.Contains("ManagedIdentityCredential", File.ReadAllText(Path.Combine(tempRoot, "Sec.cs")));
            Assert.DoesNotContain("EnableSensitiveData", File.ReadAllText(Path.Combine(tempRoot, "Sensitive.cs")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AutoFixAll_DryRun_DoesNotWriteFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-all-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var path = Path.Combine(tempRoot, "Bad.cs");
        var original = "class C { object F() => new DefaultAzureCredential(); }";
        File.WriteAllText(path, original);

        try
        {
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFixAll(tempRoot, dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;

            // Dry-run reports 1 file would change AND leaves the disk content untouched.
            Assert.Equal(1, result.RootElement.GetProperty("totalDistinctFilesChanged").GetInt32());
            Assert.True(result.RootElement.GetProperty("dryRun").GetBoolean());
            Assert.Equal(original, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AutoFixAll_OrderOfExecution_PutsSealedBeforeFanIn()
    {
        // Critical invariant: the dependency-safe order is documented in the
        // tool's docstring AND in the returned `orderOfExecution` field. This
        // test pins the order so future refactors don't silently break it.
        //
        // Use a unique temp SUBDIRECTORY (not Path.GetTempPath() itself) — on
        // Linux CI the bare temp root contains systemd-private-* dirs owned by
        // root which trigger UnauthorizedAccessException in the walker. The
        // walker is now defensive (IgnoreInaccessible=true) but the test
        // shouldn't depend on a system-wide path either way.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFixAll(tempRoot, dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;

            var order = result.RootElement.GetProperty("orderOfExecution")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .ToList();

            var sealedIdx = order.IndexOf("MAF-AP-WF-001");
            var fanInIdx = order.IndexOf("MAF130-FAN-IN-001");
            var syncIdx = order.IndexOf("MAF-AP-CONC-002");

            Assert.True(sealedIdx >= 0 && fanInIdx >= 0 && syncIdx >= 0);
            Assert.True(sealedIdx < fanInIdx, "ExecutorSealed must run before FanInArgOrder");
            Assert.True(fanInIdx < syncIdx,   "FanInArgOrder must run before SyncOverAsync");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AutoFixAll_NoOpRepo_ReportsZeroChanges()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-all-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "Clean.cs"), "class C { int X() => 1; }");

        try
        {
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFixAll(tempRoot);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;
            Assert.Equal(0, result.RootElement.GetProperty("totalDistinctFilesChanged").GetInt32());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Phase 1.1 — C1 path-escape fix
    //
    // MafAutoFix.specificFile previously joined to repoPath with a
    // `Path.IsPathRooted` short-circuit that accepted absolute paths and
    // unblocked `..` segments. The new flow runs every non-empty specificFile
    // through PathGuard.ValidateContainment at the public entry point.
    // -------------------------------------------------------------------------

    [Fact]
    public void MafAutoFix_SpecificFile_AbsolutePathOutsideRepo_ReturnsError()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-c1-abs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        // Pick an absolute path that exists but lives outside the repo root.
        // %SystemRoot%\System32\drivers\etc\hosts on Windows or /etc/hosts on POSIX.
        var outsidePath = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts")
            : "/etc/hosts";

        try
        {
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFix(tempRoot, "MAF-AP-SEC-001",
                specificFile: outsidePath, dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;
            Assert.True(result.RootElement.TryGetProperty("error", out var err),
                $"Expected error property, got: {resultJson}");
            Assert.Contains("repository root", err.GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MafAutoFix_SpecificFile_DotDotEscape_ReturnsError()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-c1-dotdot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var tool = new AutoFixTool();
            // Relative `..` segments that would syntactically escape the root.
            var resultJson = tool.MafAutoFix(tempRoot, "MAF-AP-SEC-001",
                specificFile: "../../../etc/hosts", dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;
            Assert.True(result.RootElement.TryGetProperty("error", out var err),
                $"Expected error property, got: {resultJson}");
            // PathGuard rejects `..` segments with the "traversal" wording from ValidateRepoPath.
            Assert.Contains("..", err.GetString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MafAutoFix_SpecificFile_ShellMetacharacter_ReturnsError()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-c1-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFix(tempRoot, "MAF-AP-SEC-001",
                specificFile: "Foo.cs; rm -rf /", dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;
            Assert.True(result.RootElement.TryGetProperty("error", out var err),
                $"Expected error property, got: {resultJson}");
            Assert.Contains("invalid characters", err.GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MafAutoFix_SpecificFile_LegitRelativePath_NoContainmentError()
    {
        // A legitimate relative path inside the repo must not be rejected by
        // containment; the tool should reach EnumerateFiles and return a normal
        // (empty changed-files) result rather than an `error` property.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofix-c1-legit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "Foo.cs"), "class C { int X() => 1; }");

        try
        {
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFix(tempRoot, "MAF-AP-SEC-001",
                specificFile: "Foo.cs", dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;
            Assert.False(result.RootElement.TryGetProperty("error", out _),
                $"Did not expect error property for legit relative path, got: {resultJson}");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Phase 4.1a — ExecutorSealedRewriter corruption guards.
    //
    // `sealed abstract class` is a C# compile error (the two modifiers are
    // mutually exclusive). Pre-fix the rewriter happily inserted `sealed` on
    // any class deriving from Executor with a [MessageHandler], producing
    // uncompilable user code when the source class was abstract. Same for
    // `sealed static class` (also rejected by the compiler). Now: skip the
    // rewrite and emit a leading-trivia comment explaining why.
    // -------------------------------------------------------------------------

    [Fact]
    public void ExecutorSealedRewriter_AbstractClass_NotRewritten_WithWarningComment()
    {
        var src = """
            using Microsoft.Agents.AI.Workflow;
            public abstract class BaseAuditor : Executor
            {
                [MessageHandler]
                public abstract System.Threading.Tasks.Task<string> Audit(string input);
            }
            """;
        var output = ApplyRewriter(new ExecutorSealedRewriter(), src);

        // The output must NOT contain the uncompilable `sealed abstract` order
        // (the unguarded path would insert `sealed` right after `abstract`,
        // producing `abstract sealed class`).
        Assert.DoesNotContain("sealed", output);
        // The skip-warning comment is present.
        Assert.Contains("cannot seal an abstract Executor", output);
    }

    [Fact]
    public void ExecutorSealedRewriter_RegularClass_StillRewritten()
    {
        // Sanity check — the guard doesn't accidentally skip a legitimate target.
        var src = """
            using Microsoft.Agents.AI.Workflow;
            public partial class FraudAuditor : Executor
            {
                [MessageHandler]
                public System.Threading.Tasks.Task<string> Audit(string input) => null!;
            }
            """;
        var output = ApplyRewriter(new ExecutorSealedRewriter(), src);
        Assert.Contains("public sealed partial class FraudAuditor", output);
    }

    // -------------------------------------------------------------------------
    // Phase 4.1b — SyncOverAsyncRewriter corruption guards.
    //
    // `await` is a compile error (a) inside a `lock` block (CS1996) and
    // (b) inside a non-async method (CS0117). Pre-fix the rewriter inserted
    // `await` without checking either context, producing uncompilable code.
    // Now: skip the rewrite and emit a TODO comment.
    // -------------------------------------------------------------------------

    [Fact]
    public void SyncOverAsyncRewriter_InsideLockBlock_NotRewritten_WithWarning()
    {
        var src = """
            class C
            {
                readonly object _lock = new();
                async System.Threading.Tasks.Task M()
                {
                    lock (_lock)
                    {
                        var x = Foo().Result;
                    }
                }
                System.Threading.Tasks.Task<int> Foo() => null!;
            }
            """;
        var output = ApplyRewriter(new SyncOverAsyncRewriter(), src);

        // Result access still uses `.Result` — the rewriter did NOT insert `await`.
        Assert.Contains(".Result", output);
        Assert.Contains("cannot await inside a lock", output);
    }

    [Fact]
    public void SyncOverAsyncRewriter_NonAsyncMethod_NotRewritten_WithWarning()
    {
        var src = """
            class C
            {
                int M()
                {
                    return Foo().Result;
                }
                System.Threading.Tasks.Task<int> Foo() => null!;
            }
            """;
        var output = ApplyRewriter(new SyncOverAsyncRewriter(), src);

        Assert.Contains(".Result", output);
        Assert.Contains("enclosing method is not async", output);
    }

    [Fact]
    public void SyncOverAsyncRewriter_AsyncMethodOutsideLock_StillRewritten()
    {
        // Sanity check — the guard doesn't break the happy path.
        var src = """
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    var x = Foo().Result;
                }
                System.Threading.Tasks.Task<int> Foo() => null!;
            }
            """;
        var output = ApplyRewriter(new SyncOverAsyncRewriter(), src);
        Assert.Contains("await Foo()", output);
        Assert.DoesNotContain(".Result", output);
    }

    [Fact]
    public void SyncOverAsyncRewriter_WaitCall_InsideLock_NotRewritten()
    {
        var src = """
            class C
            {
                readonly object _lock = new();
                async System.Threading.Tasks.Task M()
                {
                    lock (_lock)
                    {
                        Foo().Wait();
                    }
                }
                System.Threading.Tasks.Task Foo() => null!;
            }
            """;
        var output = ApplyRewriter(new SyncOverAsyncRewriter(), src);
        Assert.Contains(".Wait()", output);
        Assert.Contains("cannot await inside a lock", output);
    }

    [Fact]
    public void MafAutoFixAll_SpecificFile_AbsolutePathOutsideRepo_ReturnsError()
    {
        // Same fix applied to MafAutoFixAll; multiplies blast radius via the
        // ordered rule pass, so an independent test pins the behavior.
        var tempRoot = Path.Combine(Path.GetTempPath(), "maf-autofixall-c1-abs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var outsidePath = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts")
            : "/etc/hosts";

        try
        {
            var tool = new AutoFixTool();
            var resultJson = tool.MafAutoFixAll(tempRoot, specificFile: outsidePath, dryRun: true);
            var result = JsonSerializer.Deserialize<JsonDocument>(resultJson)!;
            Assert.True(result.RootElement.TryGetProperty("error", out var err),
                $"Expected error property, got: {resultJson}");
            Assert.Contains("repository root", err.GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
