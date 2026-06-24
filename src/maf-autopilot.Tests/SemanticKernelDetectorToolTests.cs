using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for the Semantic Kernel source-framework detector — pure Roslyn + XML, no MCP.
/// Verifies the construct inventory, the per-construct migration-strategy tags, the
/// csproj package detection, and the complexity verdict.
/// </summary>
public sealed class SemanticKernelDetectorToolTests
{
    private static SkConstruct One(string source, string kind)
        => Assert.Single(SemanticKernelDetectorTool.AnalyzeSource(source, "src/X.cs"), c => c.Kind.StartsWith(kind, System.StringComparison.Ordinal));

    [Fact]
    public void NonSemanticKernelFile_YieldsNothing()
    {
        // No Microsoft.SemanticKernel import → not an SK file, even if a type happens to be named Kernel.
        const string source = """
            using System;
            public class Kernel { public void InvokeAsync() { } }
            public class App { void M() { var k = new Kernel(); k.InvokeAsync(); } }
            """;
        Assert.Empty(SemanticKernelDetectorTool.AnalyzeSource(source, "src/App.cs"));
    }

    [Fact]
    public void KernelFunctionAttribute_IsBridgeable()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            public class WeatherPlugin
            {
                [KernelFunction] public string GetForecast(string city) => "sunny";
            }
            """;
        var c = One(source, "[KernelFunction] plugin method");
        Assert.Equal(MigrationStrategy.Bridgeable, c.Strategy);
    }

    [Fact]
    public void AgentGroupChat_IsRearchitect()
    {
        const string source = """
            using Microsoft.SemanticKernel.Agents;
            using Microsoft.SemanticKernel.Agents.Chat;
            public class Orchestrator
            {
                public void Run(AgentGroupChat chat) { }
            }
            """;
        var c = One(source, "Multi-agent group chat");
        Assert.Equal(MigrationStrategy.Rearchitect, c.Strategy);
    }

    [Fact]
    public void ChatCompletionAgentAndKernel_AreRewrite()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            using Microsoft.SemanticKernel.Agents;
            public class Setup
            {
                public void Build(Kernel kernel)
                {
                    var agent = new ChatCompletionAgent { Kernel = kernel };
                }
            }
            """;
        var findings = SemanticKernelDetectorTool.AnalyzeSource(source, "src/Setup.cs");
        Assert.Contains(findings, c => c.Kind.Contains("ChatCompletionAgent") && c.Strategy == MigrationStrategy.Rewrite);
        Assert.Contains(findings, c => c.Kind.Contains("Kernel") && c.Strategy == MigrationStrategy.Rewrite);
    }

    [Fact]
    public void IChatCompletionService_IsBridgeable()
    {
        const string source = """
            using Microsoft.SemanticKernel.ChatCompletion;
            public class Svc { public Svc(IChatCompletionService chat) { } }
            """;
        var c = One(source, "Chat service");
        Assert.Equal(MigrationStrategy.Bridgeable, c.Strategy);
    }

    [Fact]
    public void Planner_IsRearchitect()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            using Microsoft.SemanticKernel.Planning;
            public class P { public P(FunctionCallingStepwisePlanner planner) { } }
            """;
        var c = One(source, "Planner");
        Assert.Equal(MigrationStrategy.Rearchitect, c.Strategy);
    }

    [Fact]
    public void FunctionInvocationFilter_IsRewrite_ToMiddleware()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            public class MyFilter : IFunctionInvocationFilter { }
            """;
        var c = One(source, "Filter");
        Assert.Equal(MigrationStrategy.Rewrite, c.Strategy);
    }

    [Fact]
    public void PromptTemplate_IsRearchitect()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            public class T { public T(KernelPromptTemplate tpl) { } }
            """;
        var c = One(source, "Prompt template");
        Assert.Equal(MigrationStrategy.Rearchitect, c.Strategy);
    }

    [Fact]
    public void VectorStore_IsRearchitect()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            public class R { public R(IVectorStore store) { } }
            """;
        var c = One(source, "Vector store");
        Assert.Equal(MigrationStrategy.Rearchitect, c.Strategy);
    }

    [Fact]
    public void InvokeAsync_IsRewrite()
    {
        const string source = """
            using Microsoft.SemanticKernel.Agents;
            public class R { async void M(dynamic agent) { await foreach (var x in agent.InvokeAsync("hi")) { } } }
            """;
        var c = One(source, "Agent invocation");
        Assert.Equal(MigrationStrategy.Rewrite, c.Strategy);
    }

    [Fact]
    public void RepeatedConstruct_IsCountedOncePerKindWithCount()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            public class Plugins
            {
                [KernelFunction] public string A() => "";
                [KernelFunction] public string B() => "";
                [KernelFunction] public string C() => "";
            }
            """;
        var c = One(source, "[KernelFunction] plugin method");
        Assert.Equal(3, c.Count);
    }

    // -------------------------------------------------------------------------
    // Package detection + verdict (repo-level)
    // -------------------------------------------------------------------------

    [Fact]
    public void DetectPackages_ReadsSemanticKernelReferences()
    {
        using var repo = new TempDir(
            ("App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.SemanticKernel" Version="1.45.0" />
                    <PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.45.0" />
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                  </ItemGroup>
                </Project>
                """));

        var packages = SemanticKernelDetectorTool.DetectPackages(repo.Path);
        Assert.Equal(2, packages.Count); // SK packages only, not Newtonsoft
        Assert.Contains(packages, p => p.Id == "Microsoft.SemanticKernel" && p.Version == "1.45.0");
        Assert.DoesNotContain(packages, p => p.Id == "Newtonsoft.Json");
    }

    [Fact]
    public void Scan_RearchitectPresent_VerdictIsHard()
    {
        using var repo = new TempDir(
            ("Orchestrator.cs", """
                using Microsoft.SemanticKernel.Agents;
                using Microsoft.SemanticKernel.Agents.Chat;
                public class O { public void Run(AgentGroupChat chat) { } }
                """));

        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.True(result.SemanticKernelDetected);
        Assert.Equal("HARD", result.Verdict);
        Assert.Equal(1, result.Rearchitect);
    }

    [Fact]
    public void Scan_OnlyBridgeable_VerdictIsEasy()
    {
        using var repo = new TempDir(
            ("Plugin.cs", """
                using Microsoft.SemanticKernel;
                public class WeatherPlugin { [KernelFunction] public string GetForecast(string c) => ""; }
                """));

        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.Equal("EASY", result.Verdict);
        Assert.Equal(1, result.Bridgeable);
        Assert.Equal(0, result.Rearchitect);
    }

    [Fact]
    public void Scan_NoSemanticKernel_DetectedFalse()
    {
        using var repo = new TempDir(("Plain.cs", "public class C { public int X() => 1; }"));
        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.False(result.SemanticKernelDetected);
        Assert.Contains("No Semantic Kernel usage", result.ToMarkdown());
    }

    [Fact]
    public void MigrateFromResource_SemanticKernel_Resolves()
    {
        var body = MafDoctor.Resources.MafResources.GetMigrationFrom("semantic-kernel");
        Assert.Contains("Semantic Kernel", body);
        Assert.Contains("AsAIFunction", body); // the interop-bridge content made it in
        // alias + unknown handling
        Assert.Contains("Semantic Kernel", MafDoctor.Resources.MafResources.GetMigrationFrom("sk"));
        Assert.Throws<System.ArgumentException>(() => MafDoctor.Resources.MafResources.GetMigrationFrom("cobol"));
    }

    private sealed class TempDir : System.IDisposable
    {
        public string Path { get; }
        public TempDir(params (string Name, string Content)[] files)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "maf-sk-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Path);
            foreach (var (name, content) in files)
                System.IO.File.WriteAllText(System.IO.Path.Combine(Path, name), content);
        }
        public void Dispose() { try { System.IO.Directory.Delete(Path, recursive: true); } catch { } }
    }
}
