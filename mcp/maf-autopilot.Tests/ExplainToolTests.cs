using MafAutopilot.Data;
using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class ExplainToolTests
{
    private readonly RegistryService _registry = new();

    [Fact]
    public void Analyze_EmptySnippet_ReturnsEmpty()
    {
        var findings = ExplainTool.AnalyzeSnippet("", _registry);
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_ObsoletePattern_RegistryMatchPresent()
    {
        const string snippet = """
            using Microsoft.Agents.AI;
            var agent = new ChatClientAgent(client);
            var thread = agent.GetNewThread();   // legacy session API
            """;

        var findings = ExplainTool.AnalyzeSnippet(snippet, _registry);

        Assert.Contains(findings, f => f.Identifier == "ChatClientAgent");
        Assert.Contains(findings, f => f.Identifier == "GetNewThread");
        // GetNewThread should have a canonical note explaining the replacement.
        var thread = findings.First(f => f.Identifier == "GetNewThread");
        Assert.Contains("CreateSessionAsync", thread.CanonicalNote);
    }

    [Fact]
    public void Analyze_CanonicalPattern_PositiveAnnotation()
    {
        const string snippet = """
            var builder = new AIAgentBuilder(client)
                .UseOpenTelemetry(loggerFactory: lf)
                .Build();
            """;

        var findings = ExplainTool.AnalyzeSnippet(snippet, _registry);
        var oTel = findings.FirstOrDefault(f => f.Identifier == "UseOpenTelemetry");
        Assert.NotNull(oTel);
        Assert.Contains("Required for production observability", oTel!.CanonicalNote);
    }

    [Fact]
    public void Analyze_FanOutPatterns_RoutedToSection9()
    {
        const string snippet = """
            builder.AddFanInBarrierEdge(producer, target);
            """;

        var findings = ExplainTool.AnalyzeSnippet(snippet, _registry);
        var edge = findings.First(f => f.Identifier == "AddFanInBarrierEdge");
        // Some guide section must be cited — value comes from the registry when matched.
        Assert.False(string.IsNullOrWhiteSpace(edge.GuideSection),
            "AddFanInBarrierEdge must be routed to a guide section (registry-matched).");
        // And the registry matched.
        Assert.NotNull(edge.RegistryMatchId);
        Assert.Contains("FAN-IN", edge.RegistryMatchId!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_MessageHandlerAttribute_IsDetected()
    {
        const string snippet = """
            public partial class FraudExecutor
            {
                [MessageHandler]
                public Task<string> Handle(string input) => Task.FromResult(input);
            }
            """;

        var findings = ExplainTool.AnalyzeSnippet(snippet, _registry);
        Assert.Contains(findings, f => f.Identifier == "MessageHandler" && f.Kind == FindingKind.AttributeUsage);
    }

    [Fact]
    public void Analyze_NonMafCode_FindsNothing()
    {
        const string snippet = """
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }
            """;

        var findings = ExplainTool.AnalyzeSnippet(snippet, _registry);
        Assert.Empty(findings);
    }

    [Fact]
    public void MafExplain_EmptyInput_ReturnsError()
    {
        var tool = new ExplainTool(_registry);
        Assert.Contains("Error", tool.MafExplain(""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafExplain_RealSnippet_ProducesReport()
    {
        var tool = new ExplainTool(_registry);
        const string snippet = """
            var session = await agent.CreateSessionAsync(ct);
            await agent.SerializeSessionAsync(session);
            """;

        var report = tool.MafExplain(snippet);
        Assert.Contains("MAF code explanation", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CreateSessionAsync", report);
    }

    // -------------------------------------------------------------------------
    // Phase I.5 — Namespace-qualified type use. `new Microsoft.Agents.AI.X(...)`
    // produces a `oce.Type.ToString()` of "Microsoft.Agents.AI.X", not "X".
    // The downstream haystack search is substring-based so it should still
    // match. If a future refactor strips the namespace prefix incorrectly or
    // narrows haystack matching, this catches it.
    // -------------------------------------------------------------------------

    [Fact]
    public void Analyze_FullyQualifiedTypeUse_StillProducesFinding()
    {
        // Arrange — qualified at call-site instead of via `using`.
        const string snippet = """
            var agent = new Microsoft.Agents.AI.ChatClientAgent(client);
            """;

        // Act
        var findings = ExplainTool.AnalyzeSnippet(snippet, _registry);

        // Assert — the finding surfaces and carries the fully-qualified identifier;
        // the registry haystack must accept it via substring match.
        Assert.Contains(findings,
            f => f.Identifier.Contains("ChatClientAgent", StringComparison.Ordinal));
    }
}
