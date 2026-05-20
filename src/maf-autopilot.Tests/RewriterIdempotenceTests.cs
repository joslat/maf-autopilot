using MafAutopilot.Tools.Rewriters;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Idempotence property tests for every <c>IRuleRewriter</c>: applying a
/// rewriter twice must produce the same output as applying it once. A failure
/// here means a rewriter is non-idempotent — for example because it accidentally
/// matches its own output and rewrites again, or because trivia handling drifts
/// between passes.
///
/// Idempotence is a load-bearing invariant for <c>MafAutoFix</c> and
/// <c>MafAutoFixAll</c>: the developer-facing contract is that running the
/// fix twice is a no-op on the second run. Without these tests, regressions
/// would only surface in production via "the diff is non-empty after running
/// the fix" — far too late.
///
/// Phase 4.3 of the v1.1 security-hardening release.
/// </summary>
public sealed class RewriterIdempotenceTests
{
    private static string ApplyTwice(IRuleRewriter rewriter, string source)
    {
        var pass1 = rewriter.Visit(CSharpSyntaxTree.ParseText(source).GetRoot()).ToFullString();
        var pass2 = rewriter.Visit(CSharpSyntaxTree.ParseText(pass1).GetRoot()).ToFullString();
        Assert.Equal(pass1, pass2);
        return pass1;
    }

    // -------------------------------------------------------------------------
    // DefaultAzureCredentialRewriter
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("var c = new DefaultAzureCredential();")]
    [InlineData("var c = new DefaultAzureCredential(new DefaultAzureCredentialOptions());")]
    [InlineData("class C { void M() { var x = new DefaultAzureCredential(); } }")]
    [InlineData("// nothing to do here\nclass C { }")]
    public void DefaultAzureCredentialRewriter_Idempotent(string source)
    {
        ApplyTwice(new DefaultAzureCredentialRewriter(), source);
    }

    // -------------------------------------------------------------------------
    // EnableSensitiveDataRewriter
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("var opt = new ChatOptions { EnableSensitiveData = true };")]
    [InlineData("var opt = new Dictionary<string, object> { [\"EnableSensitiveData\"] = true };")]
    [InlineData("class C { void M() { var x = 1; } }")]
    public void EnableSensitiveDataRewriter_Idempotent(string source)
    {
        ApplyTwice(new EnableSensitiveDataRewriter(), source);
    }

    // -------------------------------------------------------------------------
    // ExecutorSealedRewriter (Phase 4.1a guards)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExecutorSealedRewriter_Idempotent_PartialClass()
    {
        const string src = """
            using Microsoft.Agents.AI.Workflow;
            public partial class FraudAuditor : Executor
            {
                [MessageHandler]
                public System.Threading.Tasks.Task<string> Audit(string input) => null!;
            }
            """;
        var output = ApplyTwice(new ExecutorSealedRewriter(), src);
        Assert.Contains("public sealed partial class FraudAuditor", output);
    }

    [Fact]
    public void ExecutorSealedRewriter_Idempotent_AbstractClass_SkipPath()
    {
        // The abstract-class guard skips the rewrite with a warning comment.
        // Running it twice should leave the comment exactly once — the second
        // pass should detect the comment-bearing skip and not duplicate.
        // (The guard fires before the modifier check on EVERY visit, so the
        // comment is added on each pass — verify the output is stable
        // regardless.)
        const string src = """
            using Microsoft.Agents.AI.Workflow;
            public abstract class BaseAuditor : Executor
            {
                [MessageHandler]
                public abstract System.Threading.Tasks.Task<string> Audit(string input);
            }
            """;
        ApplyTwice(new ExecutorSealedRewriter(), src);
    }

    [Fact]
    public void ExecutorSealedRewriter_Idempotent_AlreadySealed()
    {
        const string src = """
            using Microsoft.Agents.AI.Workflow;
            public sealed partial class FraudAuditor : Executor
            {
                [MessageHandler]
                public System.Threading.Tasks.Task<string> Audit(string input) => null!;
            }
            """;
        var output = ApplyTwice(new ExecutorSealedRewriter(), src);
        // Only one `sealed` — no duplication.
        Assert.Single(output.Split("sealed").Skip(1).ToArray());
    }

    // -------------------------------------------------------------------------
    // FanInArgOrderRewriter
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("builder.AddFanInBarrierEdge(target, new[] { src1, src2 });")]
    [InlineData("builder.AddFanInBarrierEdge(sources: new[] { src1 }, target: t);")]
    [InlineData("var x = 1;")]
    public void FanInArgOrderRewriter_Idempotent(string source)
    {
        ApplyTwice(new FanInArgOrderRewriter(), source);
    }

    // -------------------------------------------------------------------------
    // SyncOverAsyncRewriter (Phase 4.1b guards)
    // -------------------------------------------------------------------------

    [Fact]
    public void SyncOverAsyncRewriter_Idempotent_AsyncMethod_HappyPath()
    {
        // After the rewrite produces `await Foo()`, a second pass should NOT
        // re-rewrite (`.Result` is gone; nothing matches).
        const string src = """
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    var x = Foo().Result;
                }
                System.Threading.Tasks.Task<int> Foo() => null!;
            }
            """;
        var output = ApplyTwice(new SyncOverAsyncRewriter(), src);
        Assert.Contains("await Foo()", output);
        Assert.DoesNotContain(".Result", output);
    }

    [Fact]
    public void SyncOverAsyncRewriter_Idempotent_InsideLock_SkipPath()
    {
        // The lock guard skips with a TODO comment. Running twice must not
        // duplicate the comment infinitely — the rewriter's behavior should
        // converge after pass 1 (no further match after pass 1 produces the
        // comment + leaves `.Result` intact).
        const string src = """
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
        ApplyTwice(new SyncOverAsyncRewriter(), src);
    }

    [Fact]
    public void SyncOverAsyncRewriter_Idempotent_WaitCall()
    {
        const string src = """
            class C
            {
                async System.Threading.Tasks.Task M()
                {
                    Foo().Wait();
                }
                System.Threading.Tasks.Task Foo() => null!;
            }
            """;
        var output = ApplyTwice(new SyncOverAsyncRewriter(), src);
        Assert.Contains("await Foo()", output);
    }
}
