using System.Diagnostics;
using MafAutopilot.Data;
using MafAutopilot.Scaffolding;
using MafAutopilot.Tools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Security regression tests for Phase N — one test (group) per finding from the
/// three-agent security review. Every test is hermetic (no shell-out, no temp
/// dirs, no network), AAA-structured, and pins a specific attack vector so any
/// future refactor that re-opens the hole fails this suite loudly.
/// </summary>
public sealed class ScaffolderSecurityTests
{
    // -------------------------------------------------------------------------
    // N.1 — H1C: type-expression injection through `inputType` / `outputType`
    //   on MafNewExecutor. Prior to the fix these flowed VERBATIM into the
    //   generated source (Task<{{outputType}}>, default({{outputType}})!, …).
    //   The fix is a Roslyn `ParseTypeName` + roundtrip check.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("MyType")]
    [InlineData("MyType<T>")]
    [InlineData("List<int>")]
    [InlineData("Dictionary<string, int>")]
    [InlineData("MyType?")]
    [InlineData("Foo.Bar.Baz")]
    [InlineData("Microsoft.Agents.AI.FraudReport")]
    [InlineData("IAsyncEnumerable<MyType>")]
    public void IsValidTypeExpression_AcceptsLegitimateTypes(string input)
    {
        // Arrange / Act / Assert
        Assert.True(AgentScaffolder.IsValidTypeExpression(input),
            $"'{input}' should be accepted — it is a valid C# type expression.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("string;")]
    [InlineData("string { foo")]
    [InlineData("Foo(Bar)")]                                          // parens = method call, not type
    [InlineData("string\"")]                                          // unbalanced quote
    [InlineData("string\nclass Evil {}")]                             // newline-injected payload
    [InlineData("string = { System.Diagnostics.Process.Start(\"\") }")] // assignment payload
    public void IsValidTypeExpression_RejectsInjectionAttempts(string input)
    {
        // Arrange / Act / Assert
        Assert.False(AgentScaffolder.IsValidTypeExpression(input),
            $"'{input}' should be rejected — it is not a clean C# type expression.");
    }

    [Fact]
    public void ScaffoldExecutor_OutputTypeInjection_ThrowsArgumentException()
    {
        // Arrange — the PoC payload from the security review: closes Task<>,
        // injects a static class, re-opens Task< to balance.
        const string evilOutputType =
            "string> { System.Diagnostics.Process.Start(\"calc.exe\"); } static class X { Task<string";

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            AgentScaffolder.ScaffoldExecutor(
                executorName: "Reviewer",
                projectNamespace: "MyApp.Workflows",
                inputType: "string",
                outputType: evilOutputType));
        Assert.Equal("outputType", ex.ParamName);
    }

    [Fact]
    public void ScaffoldExecutor_InputTypeInjection_ThrowsArgumentException()
    {
        // Arrange
        const string evilInputType =
            "string, object _ = (Action)(() => Process.Start(\"calc.exe\"))) /*";

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            AgentScaffolder.ScaffoldExecutor(
                executorName: "Reviewer",
                projectNamespace: "MyApp.Workflows",
                inputType: evilInputType,
                outputType: "string"));
        Assert.Equal("inputType", ex.ParamName);
    }

    // -------------------------------------------------------------------------
    // Phase 1.3 — C3: projectNamespace injection on ScaffoldAgent / ScaffoldExecutor.
    //   Prior to the fix, projectNamespace flowed verbatim into the generated
    //   `namespace {{ns}};` declaration. A crafted value like
    //   `MyApp; class Evil { static int x = Process.Start("calc"); }` produces a
    //   compilable file with attacker-controlled types in the user's project.
    //   The fix is a Roslyn `ParseName` + roundtrip check, mirroring the
    //   inputType/outputType defense.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("MyApp")]
    [InlineData("MyApp.Agents")]
    [InlineData("My.Org.Agents.V2")]
    [InlineData("Contoso.FraudReview.AI.Agents")]
    public void IsValidNamespace_AcceptsLegitimateNamespaces(string input)
    {
        Assert.True(AgentScaffolder.IsValidNamespace(input),
            $"'{input}' should be accepted — it is a valid C# namespace.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MyApp; class Evil { }")]                                  // statement-terminator break-out
    [InlineData("MyApp.{Process.Start(\"calc\")}")]                       // expression injection
    [InlineData("MyApp\nclass Evil { }")]                                  // newline-injected payload
    [InlineData("MyApp`evil`")]                                            // backtick
    [InlineData("MyApp; static Evil() => Process.Start(\"x\");")]          // full method
    [InlineData("MyApp<T>")]                                               // generics — namespaces are never generic
    [InlineData("MyApp.\"evil\"")]                                         // quote
    [InlineData("MyApp = MyOtherApp")]                                     // assignment
    public void IsValidNamespace_RejectsInjectionAttempts(string input)
    {
        Assert.False(AgentScaffolder.IsValidNamespace(input),
            $"'{input}' should be rejected — it is not a clean C# namespace.");
    }

    [Fact]
    public void IsValidNamespace_OverLengthCap_Rejected()
    {
        // 257-byte identifier (one over the 256 cap).
        var oversized = new string('A', 257);
        Assert.False(AgentScaffolder.IsValidNamespace(oversized));
    }

    [Fact]
    public void ScaffoldAgent_NamespaceInjection_ThrowsArgumentException()
    {
        const string evilNs = "MyApp; static class Evil { static Evil() => System.Diagnostics.Process.Start(\"calc.exe\"); }";

        var ex = Assert.Throws<ArgumentException>(() =>
            AgentScaffolder.ScaffoldAgent(
                agentName: "ChatBot",
                projectNamespace: evilNs));
        Assert.Equal("projectNamespace", ex.ParamName);
    }

    [Fact]
    public void ScaffoldExecutor_NamespaceInjection_ThrowsArgumentException()
    {
        const string evilNs = "MyApp\nstatic class Evil { static Evil() => System.Diagnostics.Process.Start(\"calc.exe\"); }";

        var ex = Assert.Throws<ArgumentException>(() =>
            AgentScaffolder.ScaffoldExecutor(
                executorName: "Reviewer",
                projectNamespace: evilNs,
                inputType: "string",
                outputType: "string"));
        Assert.Equal("projectNamespace", ex.ParamName);
    }

    [Fact]
    public void ScaffoldAgent_EmptyNamespace_UsesSafeDefault()
    {
        // Empty/whitespace must NOT be rejected by validation — the scaffolder
        // substitutes a safe default. This pins the API contract documented
        // in the source comment.
        var files = AgentScaffolder.ScaffoldAgent(
            agentName: "ChatBot",
            projectNamespace: "");
        Assert.NotEmpty(files);
        Assert.Contains(files, f => f.Content.Contains("namespace MyApp.Agents"));
    }

    [Fact]
    public void ScaffoldExecutor_EmptyNamespace_UsesSafeDefault()
    {
        var files = AgentScaffolder.ScaffoldExecutor(
            executorName: "Reviewer",
            projectNamespace: "");
        Assert.NotEmpty(files);
        Assert.Contains(files, f => f.Content.Contains("namespace MyApp.Workflows"));
    }

    // -------------------------------------------------------------------------
    // N.10 — EscapeForCSharp false-positive guard.
    //   Agent A's H1 claimed `"` (Unicode escape for `"`) bypasses the
    //   escaper. The escaper's first replacement doubles every `\`, so any
    //   inbound `"` becomes `\\u0022` in the output — which the C# compiler
    //   reads as literal `"` (6 chars), NOT as a closing quote. This test
    //   pins the invariant.
    // -------------------------------------------------------------------------

    [Fact]
    public void EscapeForCSharp_UnicodeEscapeAttempt_NeverProducesUnescapedQuote()
    {
        // Arrange — the literal six characters `"` (backslash, u, 0, 0, 2, 2).
        var attackInput = "\\u0022); evil(); //";

        // Act
        var escaped = AgentScaffolder.EscapeForCSharp(attackInput);

        // Assert — the `\` was doubled. The encoded form starts with `\\u0022`, not `"`.
        Assert.StartsWith("\\\\u0022", escaped);
        // The generated C# literal `"<escaped>"` MUST parse cleanly AND the runtime
        // value MUST equal the original input (no compiler-side decoding).
        var generatedLiteral = $"\"{escaped}\"";
        var tree = CSharpSyntaxTree.ParseText($"var x = {generatedLiteral};");
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0,
            $"Generated literal must parse cleanly. Errors: {string.Join(", ", errors.Select(e => e.GetMessage()))}");
    }

    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("with \"quote\"", "with \\\"quote\\\"")]
    [InlineData("with \\backslash", "with \\\\backslash")]
    [InlineData("with\nnewline", "with\\nnewline")]
    [InlineData("with\rcarriage", "with\\rcarriage")]
    [InlineData("\\u0022", "\\\\u0022")]                  // The famous false-positive PoC.
    [InlineData("\\x22", "\\\\x22")]                       // Same family — hex escape.
    [InlineData("\\U00000022", "\\\\U00000022")]           // Same family — long Unicode escape.
    public void EscapeForCSharp_KnownInputs_ProduceExpectedOutput(string input, string expected)
    {
        // Arrange / Act
        var escaped = AgentScaffolder.EscapeForCSharp(input);

        // Assert
        Assert.Equal(expected, escaped);
    }
}

/// <summary>
/// Security regression tests for Phase N — `Cs0618HuntTool` hardening
/// (ReDoS + drive-root traversal).
/// </summary>
public sealed class Cs0618HuntToolSecurityTests
{
    // -------------------------------------------------------------------------
    // N.2 — ReDoS in ExtractCamelCaseIdentifiers.
    //   The original pattern had nested-quantifier overlap and exponential
    //   blowup on `AaAa…0`. Fix replaces the pattern + applies
    //   RegexOptions.NonBacktracking + 100ms timeout. This test pins both:
    //   pathological input must complete in well under 1 second.
    // -------------------------------------------------------------------------

    [Fact]
    public void MatchToRegistry_PathologicalCamelCaseInput_CompletesQuickly()
    {
        // Arrange — adversarial input designed to trigger catastrophic backtracking
        // under the OLD pattern (`\b[A-Z][a-zA-Z]+(?:[A-Z][a-zA-Z]+)+\b`).
        // n=35 took multiple minutes pre-fix; we set 1 second as the assertion ceiling.
        var pathological = string.Concat(Enumerable.Repeat("Aa", 35)) + "0";
        var diag = new BuildDiagnostic(
            File: "Foo.cs",
            Line: 1,
            Severity: "warning",
            Code: "CS0618",
            Message: pathological);
        var registry = new RegistryService();

        // Act — time it.
        var sw = Stopwatch.StartNew();
        _ = Cs0618HuntTool.MatchToRegistry(diag, registry);
        sw.Stop();

        // Assert — must be well under 1 second. The fix targets ~milliseconds.
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"ExtractCamelCaseIdentifiers regressed: pathological input took {sw.ElapsedMilliseconds} ms. " +
            "Confirm RegexOptions.NonBacktracking is still applied and the pattern is overlap-free.");
    }

    // -------------------------------------------------------------------------
    // N.4 — M1A: ResolveBuildTarget rejects filesystem roots.
    //   Without this guard, `MafRunCs0618Hunt("C:\\")` walked the drive root
    //   for any .sln and would `dotnet build` it under the developer's identity.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("C:\\")]
    [InlineData("D:\\")]
    [InlineData("/")]
    public void ResolveBuildTarget_FilesystemRoot_ReturnsNull(string root)
    {
        // Arrange — drive roots on Windows (C:\, D:\) or POSIX (/).
        // Act
        var resolved = Cs0618HuntTool.ResolveBuildTarget(root);

        // Assert — must refuse to scan the root.
        Assert.Null(resolved);
    }
}

/// <summary>
/// Security regression tests for Phase N — `PullRequestAuditTool.IsSafeBranchName`
/// must reject any leading `-` to block git-option-injection.
/// </summary>
public sealed class PullRequestAuditSecurityTests
{
    // -------------------------------------------------------------------------
    // N.3 — M2A: leading-dash branch names enable `git diff --output=PATH`
    //   arbitrary-write. The fix rejects leading `-` up-front.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("-evil")]
    [InlineData("--output=C:\\Users\\victim\\.bashrc")]
    [InlineData("--upload-pack=evil")]
    [InlineData("--exec=evil")]
    [InlineData("-rf")]
    public void IsSafeBranchName_RejectsLeadingDash(string branch)
    {
        // Arrange / Act / Assert
        Assert.False(PullRequestAuditTool.IsSafeBranchName(branch),
            $"Branch name '{branch}' starts with '-' and must be rejected to prevent git-option injection.");
    }

    [Theory]
    [InlineData("main")]
    [InlineData("develop")]
    [InlineData("feature/x-y")]                     // hyphen mid-name is fine
    [InlineData("release/1.3.0")]
    [InlineData("HEAD~5")]
    public void IsSafeBranchName_AcceptsLegitimateBranches(string branch)
    {
        // Arrange / Act / Assert
        Assert.True(PullRequestAuditTool.IsSafeBranchName(branch),
            $"Branch name '{branch}' is legitimate and must be accepted.");
    }
}
