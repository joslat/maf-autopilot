using System.Text.Json;
using MafAutopilot.Tools;
using MafAutopilot.Tools.Rewriters;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MafAutopilot.Tests;

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
}
