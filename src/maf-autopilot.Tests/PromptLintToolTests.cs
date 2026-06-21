using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

public class PromptLintToolTests
{
    // -------------------------------------------------------------------------
    // PROMPT-001 — Empty Instructions
    // -------------------------------------------------------------------------

    [Fact]
    public void Prompt001_EmptyString_Flags()
    {
        const string source = """
            var opts = new ChatOptions { Instructions = "" };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.Contains(findings, f => f.RuleId == "PROMPT-001");
    }

    [Fact]
    public void Prompt001_WhitespaceOnly_Flags()
    {
        const string source = """
            var opts = new ChatOptions { Instructions = "   " };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.Contains(findings, f => f.RuleId == "PROMPT-001");
    }

    [Fact]
    public void Prompt001_NonEmptyString_DoesNotFlag()
    {
        const string source = """
            var opts = new ChatOptions { Instructions = "You are helpful." };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.DoesNotContain(findings, f => f.RuleId == "PROMPT-001");
    }

    // -------------------------------------------------------------------------
    // PROMPT-002 — Token bloat
    // -------------------------------------------------------------------------

    [Fact]
    public void Prompt002_LongInstructions_Flags()
    {
        var bigPrompt = new string('x', PromptLintTool.BloatCharThreshold + 1);
        var source = $$"""
            var opts = new ChatOptions { Instructions = "{{bigPrompt}}" };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.Contains(findings, f => f.RuleId == "PROMPT-002");
    }

    [Fact]
    public void Prompt002_ShortInstructions_DoesNotFlag()
    {
        const string source = """
            var opts = new ChatOptions { Instructions = "You are concise." };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.DoesNotContain(findings, f => f.RuleId == "PROMPT-002");
    }

    // -------------------------------------------------------------------------
    // PROMPT-003 — Missing refusal pattern on substantive prompts
    // -------------------------------------------------------------------------

    [Fact]
    public void Prompt003_SubstantivePromptWithoutRefusal_Flags()
    {
        // Build a >500-char prompt with no refusal markers.
        var s = "You are an assistant. " + string.Concat(Enumerable.Repeat("Be helpful and clear. ", 30));
        var source = $$"""
            var opts = new ChatOptions { Instructions = "{{s}}" };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.Contains(findings, f => f.RuleId == "PROMPT-003");
    }

    [Fact]
    public void Prompt003_SubstantivePromptWithRefusal_DoesNotFlag()
    {
        var s = "You are an assistant. Do not answer questions outside finance. "
              + string.Concat(Enumerable.Repeat("Be helpful and clear. ", 30));
        var source = $$"""
            var opts = new ChatOptions { Instructions = "{{s}}" };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.DoesNotContain(findings, f => f.RuleId == "PROMPT-003");
    }

    [Fact]
    public void Prompt003_ShortPrompt_DoesNotFlag()
    {
        // Below the refusal-check minimum length — refusal patterns are optional.
        const string source = """
            var opts = new ChatOptions { Instructions = "You are helpful." };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.DoesNotContain(findings, f => f.RuleId == "PROMPT-003");
    }

    [Theory]
    [InlineData("do not")]
    [InlineData("DO NOT")]
    [InlineData("refuse")]
    [InlineData("cannot")]
    [InlineData("won't")]
    [InlineData("must not")]
    public void ContainsRefusalPattern_AcceptsKnownMarkers(string fragment)
    {
        var s = $"You are helpful but {fragment} answer off-topic questions.";
        Assert.True(PromptLintTool.ContainsRefusalPattern(s));
    }

    // -------------------------------------------------------------------------
    // PROMPT-004 — Untrusted-input concatenation
    // -------------------------------------------------------------------------

    [Fact]
    public void Prompt004_ConcatenationWithVariable_Flags()
    {
        // userInput could be anything — this is the injection-risk shape.
        const string source = """
            string userInput = "from request";
            var opts = new ChatOptions { Instructions = "You are helpful. " + userInput };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.Contains(findings, f => f.RuleId == "PROMPT-004" && f.Severity == PromptSeverity.Error);
    }

    [Fact]
    public void Prompt004_InterpolatedStringWithVariable_Flags()
    {
        const string source = """
            string userInput = "from request";
            var opts = new ChatOptions { Instructions = $"You are helpful. {userInput}" };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.Contains(findings, f => f.RuleId == "PROMPT-004");
    }

    [Fact]
    public void Prompt004_PureLiteralConcat_DoesNotFlag()
    {
        // Compile-time-constant concatenation is safe.
        const string source = """
            var opts = new ChatOptions { Instructions = "You are " + "helpful." };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.DoesNotContain(findings, f => f.RuleId == "PROMPT-004");
    }

    [Fact]
    public void Prompt004_PureLiteralInterpolation_DoesNotFlag()
    {
        const string source = """
            var opts = new ChatOptions { Instructions = $"You are helpful." };
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.DoesNotContain(findings, f => f.RuleId == "PROMPT-004");
    }

    // -------------------------------------------------------------------------
    // Coverage of named-arg assignment shape
    // -------------------------------------------------------------------------

    [Fact]
    public void NamedArgument_IsAlsoChecked()
    {
        // Constructor-style: `new ChatClientAgent(client, instructions: "")`
        const string source = """
            var agent = new ChatClientAgent(client, instructions: "");
            """;
        var findings = PromptLintTool.LintSource(source);
        Assert.Contains(findings, f => f.RuleId == "PROMPT-001");
    }

    // -------------------------------------------------------------------------
    // MCP entry
    // -------------------------------------------------------------------------

    [Fact]
    public void MafLintAgentPrompt_EmptyPath_ReturnsError()
    {
        var tool = new PromptLintTool();
        Assert.Contains("Error", tool.MafLintAgentPrompt(""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafLintAgentPrompt_NonexistentDirectory_ReturnsError()
    {
        var tool = new PromptLintTool();
        Assert.Contains("Error", tool.MafLintAgentPrompt("/path/that/does/not/exist"),
            StringComparison.OrdinalIgnoreCase);
    }
}
