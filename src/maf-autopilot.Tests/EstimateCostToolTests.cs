using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class EstimateCostToolTests
{
    [Fact]
    public void Analyze_NoAgentCalls_ReturnsEmpty()
    {
        const string source = """
            public class Calculator { public int Add(int a, int b) => a + b; }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_RunAsyncCall_IsDetected()
    {
        const string source = """
            public class Use
            {
                public async Task Run(ChatClientAgent agent)
                {
                    await agent.RunAsync("hello");
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        Assert.Single(findings);
        Assert.Equal("RunAsync", findings[0].CallSite);
    }

    [Fact]
    public void Analyze_RunStreamingAsyncCall_IsDetected()
    {
        const string source = """
            public class Use
            {
                public async Task Run(ChatClientAgent agent)
                {
                    await foreach (var u in agent.RunStreamingAsync("hello")) { }
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        Assert.Single(findings);
        Assert.Equal("RunStreamingAsync", findings[0].CallSite);
    }

    [Fact]
    public void Analyze_TaskRun_IsIgnored()
    {
        // Task.RunAsync is also a method named RunAsync. Receiver-based filter excludes it.
        const string source = """
            public class Use
            {
                public async Task Run()
                {
                    await Task.Run(() => 42);
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_MaxOutputTokensSet_IsAttributed()
    {
        const string source = """
            public class Use
            {
                public async Task Run(IChatClient client)
                {
                    var opts = new ChatOptions { MaxOutputTokens = 512 };
                    var agent = new ChatClientAgent(client, new ChatClientAgentOptions { ChatOptions = opts });
                    await agent.RunAsync("hi");
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        var f = Assert.Single(findings);
        Assert.Equal(512, f.MaxOutputTokens);
        Assert.False(f.HasCapWarning);
    }

    [Fact]
    public void Analyze_MissingMaxOutputTokens_FlagsUnbounded()
    {
        const string source = """
            public class Use
            {
                public async Task Run(IChatClient client)
                {
                    var opts = new ChatOptions { /* no MaxOutputTokens */ };
                    var agent = new ChatClientAgent(client);
                    await agent.RunAsync("hi");
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        var f = Assert.Single(findings);
        Assert.Null(f.MaxOutputTokens);
        Assert.True(f.HasCapWarning);
    }

    [Fact]
    public void Analyze_InstructionsLiteral_EstimatesInputTokens()
    {
        var prompt = new string('x', 400);  // 400 chars / 4 = 100 tokens
        var source = $$"""
            public class Use
            {
                public async Task Run(IChatClient client)
                {
                    var opts = new ChatOptions
                    {
                        MaxOutputTokens = 256,
                        Instructions = "{{prompt}}",
                    };
                    var agent = new ChatClientAgent(client, new ChatClientAgentOptions { ChatOptions = opts });
                    await agent.RunAsync("hi");
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        var f = Assert.Single(findings);
        Assert.Equal(100, f.EstimatedInputTokens);
        Assert.False(f.HasInputUnknownWarning);
    }

    [Fact]
    public void Analyze_NestedChatOptionsInsideAgentOptions_BothExtracted()
    {
        const string source = """
            public class Use
            {
                public async Task Run(IChatClient client)
                {
                    var agentOpts = new ChatClientAgentOptions
                    {
                        Name = "Bot",
                        ChatOptions = new ChatOptions { MaxOutputTokens = 1024 },
                    };
                    var agent = new ChatClientAgent(client, agentOpts);
                    await agent.RunAsync("hi");
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source);
        var f = Assert.Single(findings);
        Assert.Equal(1024, f.MaxOutputTokens);
    }

    [Fact]
    public void MafEstimateCost_EmptyPath_ReturnsError()
    {
        var tool = new EstimateCostTool();
        Assert.Contains("Error", tool.MafEstimateCost(""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafEstimateCost_NonexistentPath_ReturnsError()
    {
        var tool = new EstimateCostTool();
        Assert.Contains("Error", tool.MafEstimateCost("/path/does/not/exist"),
            StringComparison.OrdinalIgnoreCase);
    }
}
