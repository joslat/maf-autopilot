using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Regression tests for the false-positive hardening batch. Each test uses the exact
/// over-match snippet that the multi-agent detector audit confirmed, asserting the FP
/// is no longer flagged — paired, where useful, with the matching true positive that
/// MUST still fire so the hardening didn't blunt the detector.
/// </summary>
public sealed class FalsePositiveHardeningTests
{
    // -------------------------------------------------------------------------
    // MAF-AP-DEVUI-001 — the supported A2A hosting family must not be flagged.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("using Microsoft.Agents.AI.Hosting.A2A;")]
    [InlineData("using Microsoft.Agents.AI.Hosting.A2A.AspNetCore;")]
    public void DevUi001_SupportedA2AHostingPackage_DoesNotFlag(string usingLine)
    {
        var source = $$"""
            {{usingLine}}
            public class Server { public void Run() { } }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/Program.cs");
        Assert.DoesNotContain(findings, f => f.RuleId == "MAF-AP-DEVUI-001");
    }

    [Fact]
    public void DevUi001_BareHostingPackage_StillFlags()
    {
        // The bare (non-A2A) Hosting namespace is the unsupported preview surface.
        const string source = """
            using Microsoft.Agents.AI.Hosting;
            public class Server { public void Run() { } }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/Program.cs");
        Assert.Contains(findings, f => f.RuleId == "MAF-AP-DEVUI-001");
    }

    [Fact]
    public void DevUi001_LocalNamespaceNamedDevUI_DoesNotFlag()
    {
        // The friend's repo had a project whose OWN namespace is `DevUI` — the old
        // bare-identifier arm flagged `namespace DevUI;` / `using DevUI;`. It must not.
        const string source = """
            using DevUI;
            namespace DevUI;
            public class MotorsAgent { }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/MotorsAgent.cs");
        Assert.DoesNotContain(findings, f => f.RuleId == "MAF-AP-DEVUI-001");
    }

    // -------------------------------------------------------------------------
    // MAF-AP-CONC-001 — a ProviderSessionState<T> field is the FIX, not the bug.
    // -------------------------------------------------------------------------

    [Fact]
    public void Conc001_ProviderSessionStateField_DoesNotFlag()
    {
        const string source = """
            public sealed class MemoryProvider : AIContextProvider
            {
                private ProviderSessionState<MyState> _sessionState = new();
                public sealed class MyState { public int Count; }
            }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/MemoryProvider.cs");
        Assert.DoesNotContain(findings, f => f.RuleId == "MAF-AP-CONC-001");
    }

    [Fact]
    public void Conc001_PlainMutableField_StillFlags()
    {
        const string source = """
            public sealed class MemoryProvider : AIContextProvider
            {
                private int _counter;   // genuine per-instance mutable state
            }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/MemoryProvider.cs");
        Assert.Contains(findings, f => f.RuleId == "MAF-AP-CONC-001");
    }

    // -------------------------------------------------------------------------
    // MAF-AP-CONC-002 — AgentResponse<T>.Result and Match.Result(...) are not blocking.
    // -------------------------------------------------------------------------

    [Fact]
    public void Conc002_AwaitedResultProperty_DoesNotFlag()
    {
        // `(await agent.RunAsync<T>(...)).Result` — the recommended structured-output
        // idiom; .Result is the deserialized payload of an already-awaited AgentResponse<T>.
        const string source = """
            public class Demo
            {
                public async System.Threading.Tasks.Task<string> M(dynamic agent)
                {
                    string city = (await agent.RunAsync<string>("Tell me about Amsterdam.")).Result;
                    return city;
                }
            }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/Demo.cs");
        Assert.DoesNotContain(findings, f => f.RuleId == "MAF-AP-CONC-002");
    }

    [Fact]
    public void Conc002_RegexMatchResultMethod_DoesNotFlag()
    {
        // `regex.Match(input).Result("$1")` — Regex substitution method, not Task.Result.
        const string source = """
            using System.Text.RegularExpressions;
            public class Demo
            {
                public string M(Regex regex, string input) => regex.Match(input).Result("$1-$2");
            }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/Demo.cs");
        Assert.DoesNotContain(findings, f => f.RuleId == "MAF-AP-CONC-002");
    }

    [Fact]
    public void Conc002_BlockingTaskResult_StillFlags()
    {
        const string source = """
            public class Demo
            {
                public string M(dynamic client) => client.GetDataAsync().Result;   // sync-over-async block
            }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/Demo.cs");
        Assert.Contains(findings, f => f.RuleId == "MAF-AP-CONC-002");
    }

    // -------------------------------------------------------------------------
    // MAF-AP-SEC-003 — EnableSensitiveData behind a dev guard is sanctioned.
    // -------------------------------------------------------------------------

    [Fact]
    public void Sec003_EnableSensitiveDataInsideDebugGuard_DoesNotFlag()
    {
        const string source = """
            public class Setup
            {
                public void X(dynamic cfg)
                {
            #if DEBUG
                    cfg.EnableSensitiveData = true;
            #endif
                }
            }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/Setup.cs");
        Assert.DoesNotContain(findings, f => f.RuleId == "MAF-AP-SEC-003");
    }

    [Fact]
    public void Sec003_EnableSensitiveDataUnguarded_StillFlags()
    {
        const string source = """
            public class Setup { public void X() { var o = new Options { EnableSensitiveData = true }; } }
            """;
        var findings = AntiPatternScannerTool.ScanFile(source, "src/Setup.cs");
        Assert.Contains(findings, f => f.RuleId == "MAF-AP-SEC-003");
    }

    // -------------------------------------------------------------------------
    // COST-001 — host / workflow runners are not uncapped agent calls.
    // -------------------------------------------------------------------------

    [Fact]
    public void Cost001_AspNetHostRunAsync_DoesNotFlag()
    {
        const string source = """
            public class Program
            {
                public static async System.Threading.Tasks.Task Main(dynamic app)
                {
                    await app.RunAsync();   // ASP.NET host run loop — NOT an agent call
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source, "src/Program.cs");
        Assert.DoesNotContain(findings, f => f.ReceiverExpression == "app");
    }

    [Fact]
    public void Cost001_WorkflowRunner_DoesNotFlag()
    {
        const string source = """
            public class Program
            {
                public static async System.Threading.Tasks.Task Run(dynamic workflow, string query)
                {
                    var run = await InProcessExecution.RunStreamingAsync(workflow, input: query);
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source, "src/Program.cs");
        Assert.DoesNotContain(findings, f => f.ReceiverExpression == "InProcessExecution");
    }

    [Fact]
    public void Cost001_RealAgentRunAsync_StillFlags()
    {
        const string source = """
            public class Program
            {
                public static async System.Threading.Tasks.Task Run(dynamic agent, string msg)
                {
                    var r = await agent.RunAsync(msg);   // genuine uncapped agent call
                }
            }
            """;
        var findings = EstimateCostTool.AnalyzeSource(source, "src/Program.cs");
        Assert.Contains(findings, f => f.ReceiverExpression == "agent" && f.HasCapWarning);
    }
}
