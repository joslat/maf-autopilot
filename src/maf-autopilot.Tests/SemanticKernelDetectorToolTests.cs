using System.Text.Json;
using System.Text.RegularExpressions;
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
    public void GlobalQualifiedSkUsing_IsRecognized()
    {
        // `using global::Microsoft.SemanticKernel;` must still mark the file as SK.
        const string source = """
            using global::Microsoft.SemanticKernel;
            public class P { [KernelFunction] public string A() => ""; }
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

    [Fact]
    public void UserPlannerType_IsNotFlagged()
    {
        // A user type whose name merely ends in "Planner" must NOT be flagged — only the
        // real SK planner types are. (Previously a `*Planner` suffix over-matched.)
        const string source = """
            using Microsoft.SemanticKernel;
            public class Scheduler { public Scheduler(CapacityPlanner planner) { } }
            """;
        Assert.DoesNotContain(
            SemanticKernelDetectorTool.AnalyzeSource(source, "src/Scheduler.cs"),
            c => c.Kind.StartsWith("Planner", System.StringComparison.Ordinal));
    }

    [Fact]
    public void AgentThread_IsRewrite_ToSession()
    {
        const string source = """
            using Microsoft.SemanticKernel.Agents;
            public class T { public T(AgentThread thread) { } }
            """;
        var c = One(source, "Thread");
        Assert.Equal(MigrationStrategy.Rewrite, c.Strategy);
    }

    [Fact]
    public void PromptExecutionSettings_IsRewrite()
    {
        const string source = """
            using Microsoft.SemanticKernel;
            using Microsoft.SemanticKernel.Connectors.OpenAI;
            public class O { public O(OpenAIPromptExecutionSettings settings) { } }
            """;
        var c = One(source, "Execution settings");
        Assert.Equal(MigrationStrategy.Rewrite, c.Strategy);
    }

    [Fact]
    public void KernelInvokeAsync_IsClassifiedAsKernelFunctionInvocation()
    {
        // kernel.InvokeAsync(fn) is SK function invocation, NOT agent invocation.
        const string source = """
            using Microsoft.SemanticKernel;
            public class R { async void M(Kernel kernel, KernelFunction fn) { await kernel.InvokeAsync(fn); } }
            """;
        var findings = SemanticKernelDetectorTool.AnalyzeSource(source, "src/R.cs");
        Assert.Contains(findings, c => c.Kind.StartsWith("Kernel function invocation", System.StringComparison.Ordinal));
        Assert.DoesNotContain(findings, c => c.Kind.StartsWith("Agent invocation", System.StringComparison.Ordinal));
    }

    [Fact]
    public void HostAddOpenTelemetry_IsNotFlagged_ButKernelReceiverIs()
    {
        // services.AddOpenTelemetry() is ASP.NET / OTel host wiring — must NOT be flagged.
        const string host = """
            using Microsoft.SemanticKernel;
            public class Startup { void M(object services) { ((dynamic)services).AddOpenTelemetry(); } }
            """;
        Assert.DoesNotContain(
            SemanticKernelDetectorTool.AnalyzeSource(host, "src/Startup.cs"),
            c => c.Kind.StartsWith("Observability", System.StringComparison.Ordinal));

        // A kernel-builder receiver IS flagged.
        const string kernel = """
            using Microsoft.SemanticKernel;
            public class Setup { void M(dynamic kernelBuilder) { kernelBuilder.Services.AddOpenTelemetry(); } }
            """;
        Assert.Contains(
            SemanticKernelDetectorTool.AnalyzeSource(kernel, "src/Setup.cs"),
            c => c.Kind.StartsWith("Observability", System.StringComparison.Ordinal));
    }

    [Fact]
    public void PropertyNamedLikeType_IsNotCountedAsTypeUsage()
    {
        // `new ChatCompletionAgent { Kernel = kernel }` (initializer LHS) and `agent.Kernel`
        // (member access) must NOT inflate the `Kernel` TYPE count — only the real `Kernel`
        // parameter type counts.
        const string source = """
            using Microsoft.SemanticKernel;
            using Microsoft.SemanticKernel.Agents;
            public class Setup
            {
                public void Build(Kernel kernel)
                {
                    var agent = new ChatCompletionAgent { Kernel = kernel };
                    var k2 = agent.Kernel;
                }
            }
            """;
        var kernelConstruct = Assert.Single(
            SemanticKernelDetectorTool.AnalyzeSource(source, "src/Setup.cs"),
            c => c.Kind.StartsWith("Kernel (", System.StringComparison.Ordinal));
        Assert.Equal(1, kernelConstruct.Count); // the parameter type only
    }

    [Fact]
    public void ChatHistoryAgentThread_IsRewrite_ToSession()
    {
        // A concrete *AgentThread subtype must be caught even with `var` (no base annotation),
        // via the suffix fallback — not only when declared as the base `AgentThread`.
        const string source = """
            using Microsoft.SemanticKernel.Agents;
            public class T { void M() { var t = new ChatHistoryAgentThread(); } }
            """;
        var c = One(source, "Thread (`ChatHistoryAgentThread`");
        Assert.Equal(MigrationStrategy.Rewrite, c.Strategy);
    }

    [Fact]
    public void ThisQualifiedKernel_InvokeAsync_IsKernelFunctionInvocation()
    {
        // `this._kernel.InvokeAsync(fn)` — the receiver root is the field name `_kernel`
        // (a kernel), so it must classify as kernel-function invocation, not agent.
        const string source = """
            using Microsoft.SemanticKernel;
            public class R { Kernel _kernel; async void M(KernelFunction fn) { await this._kernel.InvokeAsync(fn); } }
            """;
        var findings = SemanticKernelDetectorTool.AnalyzeSource(source, "src/R.cs");
        Assert.Contains(findings, c => c.Kind.StartsWith("Kernel function invocation", System.StringComparison.Ordinal));
        Assert.DoesNotContain(findings, c => c.Kind.StartsWith("Agent invocation", System.StringComparison.Ordinal));
    }

    [Fact]
    public void UserTypeEndingInAgentThread_IsFlagged_AcceptedTradeoff()
    {
        // Documents the deliberate boundary: the `*AgentThread` suffix is SK-distinctive
        // enough that we accept flagging a user type named `…AgentThread` as a (rare) false
        // positive — the inverse of the `*Planner` decision. If this ever becomes a real
        // problem, this test is the place to tighten it.
        const string source = """
            using Microsoft.SemanticKernel.Agents;
            public class T { void M() { var t = new MyAgentThread(); } }
            """;
        Assert.Contains(
            SemanticKernelDetectorTool.AnalyzeSource(source, "src/T.cs"),
            c => c.Kind.StartsWith("Thread (`MyAgentThread`", System.StringComparison.Ordinal));
    }

    [Fact]
    public void NameofType_IsNotCountedAsTypeUsage()
    {
        // nameof(Kernel) is a string literal, not a type reference — must not be counted.
        const string source = """
            using Microsoft.SemanticKernel;
            public class C { string N() => nameof(Kernel); }
            """;
        Assert.DoesNotContain(
            SemanticKernelDetectorTool.AnalyzeSource(source, "src/C.cs"),
            c => c.Kind.StartsWith("Kernel (", System.StringComparison.Ordinal));
    }

    [Fact]
    public void HostOtel_SubstringVariableName_IsNotFlagged()
    {
        // A host OTel call on a variable that merely CONTAINS "kernel" as a substring
        // (whole-word gate) must NOT be flagged as a Kernel observability rewrite.
        const string source = """
            using Microsoft.SemanticKernel;
            public class S { void M(dynamic kernelMetricsServices) { kernelMetricsServices.AddOpenTelemetry(); } }
            """;
        Assert.DoesNotContain(
            SemanticKernelDetectorTool.AnalyzeSource(source, "src/S.cs"),
            c => c.Kind.StartsWith("Observability", System.StringComparison.Ordinal));
    }

    [Fact]
    public void MethodGroupInvokeReference_IsNotFlagged()
    {
        // A bare method-group reference (no call) must NOT be flagged as an invocation —
        // only actual call sites are.
        const string source = """
            using Microsoft.SemanticKernel.Agents;
            public class R { void M(dynamic agent) { var f = agent.InvokeAsync; } }
            """;
        Assert.DoesNotContain(
            SemanticKernelDetectorTool.AnalyzeSource(source, "src/R.cs"),
            c => c.Kind.Contains("invocation", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KernelAgentReceiver_InvokeAsync_IsAgentInvocation()
    {
        // `kernelAgent` is an AGENT (whole-word root is not a kernel) — must classify as
        // agent invocation, not kernel-function invocation, despite the "kernel" substring.
        const string source = """
            using Microsoft.SemanticKernel.Agents;
            public class R { async void M(dynamic kernelAgent) { await kernelAgent.InvokeAsync("hi"); } }
            """;
        var findings = SemanticKernelDetectorTool.AnalyzeSource(source, "src/R.cs");
        Assert.Contains(findings, c => c.Kind.StartsWith("Agent invocation", System.StringComparison.Ordinal));
        Assert.DoesNotContain(findings, c => c.Kind.StartsWith("Kernel function invocation", System.StringComparison.Ordinal));
    }

    [Fact]
    public void LargeBridgeableFootprint_VerdictIsMedium()
    {
        // A bridge-only repo with > LargeFootprintThreshold occurrences escalates EASY → MEDIUM.
        using var repo = new TempDir(
            ("Plugins.cs", """
                using Microsoft.SemanticKernel;
                public class P
                {
                    [KernelFunction] public string A() => "";
                    [KernelFunction] public string B() => "";
                    [KernelFunction] public string C() => "";
                    [KernelFunction] public string D() => "";
                    [KernelFunction] public string E() => "";
                    [KernelFunction] public string F() => "";
                    [KernelFunction] public string G() => "";
                    [KernelFunction] public string H() => "";
                    [KernelFunction] public string I() => "";
                }
                """));

        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.Equal(0, result.Rewrite);
        Assert.Equal(0, result.Rearchitect);
        Assert.True(result.TotalOccurrences > 8);
        Assert.Equal("MEDIUM", result.Verdict);
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

    // Round-2 review fixup (F-10/F-20) — XDocument.Load(csproj) / .props) was
    // unbounded, the same weakness already fixed for MafExplainFinding's
    // .cs-file path. A pathologically large .csproj or Directory.Packages.props
    // must be skipped, not fully materialized (twice over: raw bytes + XML DOM).
    [Fact]
    public void DetectPackages_CsprojOverSizeCap_SkippedWithoutCrashing()
    {
        using var repo = new TempDir(
            ("Huge.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.SemanticKernel" Version="1.45.0" />
                  </ItemGroup>
                </Project>
                """));
        var hugeCsproj = System.IO.Path.Combine(repo.Path, "Huge.csproj");
        // Sparse length beyond the cap without allocating real bytes on disk —
        // DetectPackages only inspects FileInfo.Length before deciding to skip.
        using (var fs = new FileStream(hugeCsproj, FileMode.Open))
        {
            fs.SetLength(SemanticKernelDetectorTool.MaxProjectFileBytes + 1);
        }

        var packages = SemanticKernelDetectorTool.DetectPackages(repo.Path);

        Assert.Empty(packages);
    }

    [Fact]
    public void DetectPackages_CentralPackagePropsOverSizeCap_SkippedWithoutCrashing()
    {
        using var repo = new TempDir(
            ("Directory.Packages.props", """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Microsoft.SemanticKernel" Version="1.45.0" />
                  </ItemGroup>
                </Project>
                """),
            ("App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.SemanticKernel" />
                  </ItemGroup>
                </Project>
                """));
        var propsPath = System.IO.Path.Combine(repo.Path, "Directory.Packages.props");
        using (var fs = new FileStream(propsPath, FileMode.Open))
        {
            fs.SetLength(SemanticKernelDetectorTool.MaxProjectFileBytes + 1);
        }

        // The CPM pin is unreadable (props file skipped), so the PackageReference
        // resolves to "(unpinned)" rather than crashing or silently vanishing.
        var packages = SemanticKernelDetectorTool.DetectPackages(repo.Path);

        var sk = Assert.Single(packages, p => p.Id == "Microsoft.SemanticKernel");
        Assert.Equal("(unpinned)", sk.Version);
    }

    [Fact]
    public void DetectPackages_ResolvesCentralPackageManagementVersion()
    {
        // Under CPM the PackageReference has no Version; the pin lives in
        // Directory.Packages.props. The detector must resolve it, not say "(unpinned)".
        using var repo = new TempDir(
            ("Directory.Packages.props", """
                <Project>
                  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Microsoft.SemanticKernel" Version="1.45.0" />
                  </ItemGroup>
                </Project>
                """),
            ("App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.SemanticKernel" />
                  </ItemGroup>
                </Project>
                """));

        var packages = SemanticKernelDetectorTool.DetectPackages(repo.Path);
        var sk = Assert.Single(packages, p => p.Id == "Microsoft.SemanticKernel");
        Assert.Equal("1.45.0", sk.Version);
    }

    [Fact]
    public void DetectPackages_VersionOverride_BeatsCentralPin()
    {
        using var repo = new TempDir(
            ("Directory.Packages.props", """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Microsoft.SemanticKernel" Version="1.45.0" />
                  </ItemGroup>
                </Project>
                """),
            ("App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.SemanticKernel" VersionOverride="1.40.0" />
                  </ItemGroup>
                </Project>
                """));

        var sk = Assert.Single(SemanticKernelDetectorTool.DetectPackages(repo.Path), p => p.Id == "Microsoft.SemanticKernel");
        Assert.Equal("1.40.0", sk.Version); // VersionOverride wins over the central pin
    }

    [Fact]
    public void DetectPackages_NoVersionAnywhere_IsUnpinned()
    {
        using var repo = new TempDir(
            ("App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.SemanticKernel" />
                  </ItemGroup>
                </Project>
                """));

        var sk = Assert.Single(SemanticKernelDetectorTool.DetectPackages(repo.Path), p => p.Id == "Microsoft.SemanticKernel");
        Assert.Equal("(unpinned)", sk.Version);
    }

    [Fact]
    public void DetectPackages_IsCaseInsensitive()
    {
        // NuGet IDs are case-insensitive — a lowercase Include must still be detected.
        using var repo = new TempDir(
            ("App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="microsoft.semantickernel" Version="1.45.0" />
                  </ItemGroup>
                </Project>
                """));

        var sk = Assert.Single(SemanticKernelDetectorTool.DetectPackages(repo.Path));
        Assert.Equal("1.45.0", sk.Version);
    }

    [Fact]
    public void DetectPackages_CpmResolution_IsCaseInsensitive()
    {
        // A differently-cased PackageReference must resolve against the central pin.
        using var repo = new TempDir(
            ("Directory.Packages.props", """
                <Project><ItemGroup>
                  <PackageVersion Include="Microsoft.SemanticKernel" Version="1.45.0" />
                </ItemGroup></Project>
                """),
            ("App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
                  <PackageReference Include="microsoft.semantickernel" />
                </ItemGroup></Project>
                """));

        var sk = Assert.Single(SemanticKernelDetectorTool.DetectPackages(repo.Path));
        Assert.Equal("1.45.0", sk.Version);
    }

    [Fact]
    public void DetectPackages_ConflictingPins_AreSurfacedDeterministically()
    {
        // Two projects pinning the same SK id to DIFFERENT versions → both surfaced
        // (sorted, deterministic) so the mismatch is visible, not silently collapsed.
        using var repo = new TempDir(
            ("A.csproj", """
                <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
                  <PackageReference Include="Microsoft.SemanticKernel" Version="1.45.0" />
                </ItemGroup></Project>
                """),
            ("B.csproj", """
                <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
                  <PackageReference Include="Microsoft.SemanticKernel" Version="1.40.0" />
                </ItemGroup></Project>
                """));

        var sk = Assert.Single(SemanticKernelDetectorTool.DetectPackages(repo.Path));
        Assert.Equal("1.40.0, 1.45.0", sk.Version);
    }

    [Fact]
    public void MafDetectSourceFramework_CsprojFilePath_ScansContainingDirectory()
    {
        // Passing a .csproj / .sln FILE path scans its directory (advertised "repo or project").
        using var repo = new TempDir(
            ("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>"),
            ("Plugin.cs", "using Microsoft.SemanticKernel;\npublic class P { [KernelFunction] public string A() => \"\"; }"));
        var csproj = System.IO.Path.Combine(repo.Path, "App.csproj");

        var report = new SemanticKernelDetectorTool().MafDetectSourceFramework(csproj);
        Assert.Contains("Semantic Kernel → MAF", report);
        Assert.DoesNotContain("Error:", report);
    }

    [Fact]
    public void Scan_SingleRearchitect_VerdictIsMedium()
    {
        // A lone re-architecture is tractable → MEDIUM, not HARD (one redesign).
        using var repo = new TempDir(
            ("Orchestrator.cs", """
                using Microsoft.SemanticKernel.Agents;
                using Microsoft.SemanticKernel.Agents.Chat;
                public class O { public void Run(AgentGroupChat chat) { } }
                """));

        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.True(result.SemanticKernelDetected);
        Assert.Equal("MEDIUM", result.Verdict);
        Assert.Equal(1, result.Rearchitect);
    }

    [Fact]
    public void Scan_TwoRearchitectKinds_VerdictIsHard()
    {
        // Two distinct re-architecture kinds compound → HARD.
        using var repo = new TempDir(
            ("Orchestrator.cs", """
                using Microsoft.SemanticKernel.Agents;
                using Microsoft.SemanticKernel.Agents.Chat;
                using Microsoft.SemanticKernel;
                public class O { public void Run(AgentGroupChat chat, IVectorStore store) { } }
                """));

        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.Equal("HARD", result.Verdict);
        Assert.Equal(2, result.Rearchitect);
    }

    [Fact]
    public void Scan_SameRearchitectKindAcrossFiles_IsNotHard()
    {
        // The SAME re-architect kind (AgentGroupChat) in two files is ONE distinct kind →
        // MEDIUM, not HARD. Regression: the verdict counted per-file records, not kinds.
        using var repo = new TempDir(
            ("A.cs", """
                using Microsoft.SemanticKernel.Agents;
                using Microsoft.SemanticKernel.Agents.Chat;
                public class A { public void Run(AgentGroupChat chat) { } }
                """),
            ("B.cs", """
                using Microsoft.SemanticKernel.Agents;
                using Microsoft.SemanticKernel.Agents.Chat;
                public class B { public void Run(AgentGroupChat chat) { } }
                """));

        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.Equal(2, result.Rearchitect);    // two per-file records (display count unchanged)
        Assert.Equal("MEDIUM", result.Verdict); // but only ONE distinct kind → not HARD
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
    public void Scan_GlobalUsingsFile_MakesOtherFilesSkContext()
    {
        // A `global using Microsoft.SemanticKernel...` in GlobalUsings.cs puts SK in scope
        // for the whole project, so a file with NO local using must still be scanned.
        using var repo = new TempDir(
            ("GlobalUsings.cs", "global using Microsoft.SemanticKernel;\nglobal using Microsoft.SemanticKernel.Agents;"),
            ("Agent.cs", "public class Setup { public void M(ChatCompletionAgent agent) { } }"));

        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.True(result.SemanticKernelDetected);
        Assert.Contains(result.Constructs, c => c.Kind.Contains("ChatCompletionAgent", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_NoSemanticKernel_DetectedFalse()
    {
        using var repo = new TempDir(("Plain.cs", "public class C { public int X() => 1; }"));
        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.False(result.SemanticKernelDetected);
        Assert.Contains("No Semantic Kernel usage", result.ToMarkdown());
    }

    // -------------------------------------------------------------------------
    // F-10 (2026-07-19 security assessment) — a truncated scan must not render
    // as an unqualified clean/complete verdict. These construct SkScanResult
    // directly (rather than forcing Scan() to truncate against real files,
    // which would need thousands of on-disk files for a fast unit test) since
    // what matters here is the rendering contract, not re-testing ScanBudget
    // itself (already covered in SourceFileWalkerTests).
    // -------------------------------------------------------------------------

    [Fact]
    public void ToMarkdown_TruncatedWithNoDetections_WarnsInsteadOfClaimingClean()
    {
        var result = new SemanticKernelDetectorTool.SkScanResult(
            Packages: [], Constructs: [], Truncated: true);

        var md = result.ToMarkdown();
        Assert.DoesNotContain("✅ No Semantic Kernel usage detected", md);
        Assert.Contains("partial result", md, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToMarkdown_TruncatedWithDetections_IncludesWarningBanner()
    {
        var result = new SemanticKernelDetectorTool.SkScanResult(
            Packages: [new SkPackage("Microsoft.SemanticKernel", "1.0.0")],
            Constructs: [new SkConstruct(
                Kind: "[KernelFunction] plugin method", Strategy: MigrationStrategy.Bridgeable,
                File: "Plugin.cs", Line: 1, Note: "note", Count: 1)],
            Truncated: true);

        var md = result.ToMarkdown();
        Assert.Contains("Scan incomplete", md);
    }

    [Fact]
    public void ToMarkdown_NotTruncated_NoWarningBanner()
    {
        var result = new SemanticKernelDetectorTool.SkScanResult(
            Packages: [], Constructs: [], Truncated: false);

        Assert.DoesNotContain("Scan incomplete", result.ToMarkdown());
    }

    [Fact]
    public void ToJson_SurfacesTruncatedField()
    {
        var truncated = new SemanticKernelDetectorTool.SkScanResult(Packages: [], Constructs: [], Truncated: true);
        var notTruncated = new SemanticKernelDetectorTool.SkScanResult(Packages: [], Constructs: [], Truncated: false);

        var truncatedJson = JsonSerializer.Serialize(truncated.ToJson());
        var cleanJson = JsonSerializer.Serialize(notTruncated.ToJson());

        Assert.Contains("\"truncated\":true", truncatedJson);
        Assert.Contains("\"truncated\":false", cleanJson);
    }

    [Fact]
    public void Scan_SmallRepo_NotTruncated()
    {
        // Sanity: normal-sized repos (this project's own test fixtures) never
        // trip the new aggregate cap — MaxTotalBytes defaults to 500 MB.
        using var repo = new TempDir(("Plain.cs", "public class C { public int X() => 1; }"));
        var result = SemanticKernelDetectorTool.Scan(repo.Path);
        Assert.False(result.Truncated);
    }

    // -------------------------------------------------------------------------
    // F-10 review follow-up — the budget-injectable Scan overload lets these
    // exercise REAL truncation (via SourceFileWalker.EnumerateCsFiles actually
    // stopping early), not just SkScanResult constructed with a hand-set
    // Truncated bool (which only proves the rendering code reads the flag it's
    // given, not that Scan() itself ever sets it correctly).
    // -------------------------------------------------------------------------

    [Fact]
    public void Scan_BudgetInjected_TinyFileCap_ReportsTruncatedForReal()
    {
        using var repo = new TempDir(
            ("A.cs", "public class A { }"),
            ("B.cs", "public class B { }"),
            ("C.cs", "public class C { }"));

        var result = SemanticKernelDetectorTool.Scan(
            repo.Path,
            pass1Budget: new SourceFileWalker.ScanBudget { MaxFiles = 1 },
            pass2Budget: new SourceFileWalker.ScanBudget { MaxFiles = 1 });

        Assert.True(result.Truncated);
    }

    [Fact]
    public void Scan_Pass1BudgetExhaustedBeforeGlobalUsing_Pass2StillMissesSkContextInScannedFile()
    {
        // Deterministic reproduction of the reviewed correctness caveat
        // documented on Scan(): pass 1 gets a budget that sees ZERO files
        // (MaxFiles: 0 — true regardless of filesystem enumeration order, so
        // this isn't a flaky ordering-dependent test), so repoEstablishesSk
        // stays false even though the repo DOES rely on a global using
        // elsewhere in spirit. Pass 2 gets a normal budget and DOES read
        // Agent.cs — but since it has no LOCAL `using Microsoft.SemanticKernel`
        // of its own, and repoEstablishesSk is false, AnalyzeSource's importsSk
        // gate is false and the ChatCompletionAgent construct is silently
        // never recorded, even though the file was genuinely scanned.
        using var repo = new TempDir(
            ("GlobalUsings.cs", "global using Microsoft.SemanticKernel;\nglobal using Microsoft.SemanticKernel.Agents;"),
            ("Agent.cs", "public class Setup { public void M(ChatCompletionAgent agent) { } }"));

        var result = SemanticKernelDetectorTool.Scan(
            repo.Path,
            pass1Budget: new SourceFileWalker.ScanBudget { MaxFiles = 0 },
            pass2Budget: new SourceFileWalker.ScanBudget());

        Assert.True(result.Truncated); // pass 1's budget was genuinely exhausted
        // The construct that a full, single-pass scan WOULD have found
        // (see Scan_GlobalUsingsFile_MakesOtherFilesSkContext) is missing —
        // demonstrating the "in-budget file, silently under-detected" gap.
        Assert.DoesNotContain(result.Constructs, c => c.Kind.Contains("ChatCompletionAgent", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_DefaultOverload_MatchesBudgetInjectedOverloadWithFreshBudgets()
    {
        // Regression guard: the public Scan(repoPath) overload must keep
        // delegating to fresh default budgets, not silently share state
        // across calls or drift from the tested overload's behavior.
        using var repo = new TempDir(
            ("GlobalUsings.cs", "global using Microsoft.SemanticKernel;\nglobal using Microsoft.SemanticKernel.Agents;"),
            ("Agent.cs", "public class Setup { public void M(ChatCompletionAgent agent) { } }"));

        var viaDefault = SemanticKernelDetectorTool.Scan(repo.Path);
        var viaExplicit = SemanticKernelDetectorTool.Scan(
            repo.Path, new SourceFileWalker.ScanBudget(), new SourceFileWalker.ScanBudget());

        Assert.Equal(viaExplicit.Truncated, viaDefault.Truncated);
        Assert.Equal(viaExplicit.Constructs.Count, viaDefault.Constructs.Count);
        Assert.False(viaDefault.Truncated);
        Assert.Contains(viaDefault.Constructs, c => c.Kind.Contains("ChatCompletionAgent", System.StringComparison.Ordinal));
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
        // Empty / null / whitespace are unsupported sources (no silent default) — they throw.
        Assert.Throws<System.ArgumentException>(() => MafDoctor.Resources.MafResources.GetMigrationFrom(""));
        Assert.Throws<System.ArgumentException>(() => MafDoctor.Resources.MafResources.GetMigrationFrom(null));
        Assert.Throws<System.ArgumentException>(() => MafDoctor.Resources.MafResources.GetMigrationFrom("   "));
    }

    // -------------------------------------------------------------------------
    // MCP entry point (MafDetectSourceFramework) — format dispatch + error shape
    // -------------------------------------------------------------------------

    [Fact]
    public void MafDetectSourceFramework_DefaultFormat_IsMarkdown()
    {
        using var repo = new TempDir(
            ("Plugin.cs", "using Microsoft.SemanticKernel;\npublic class P { [KernelFunction] public string A() => \"\"; }"));

        var report = new SemanticKernelDetectorTool().MafDetectSourceFramework(repo.Path);
        Assert.Contains("Semantic Kernel → MAF", report);
        Assert.Contains("Migration complexity:", report);
    }

    [Fact]
    public void MafDetectSourceFramework_Json_ShapeAndCountsReconcile()
    {
        using var repo = new TempDir(
            ("Setup.cs", """
                using Microsoft.SemanticKernel;
                using Microsoft.SemanticKernel.Agents;
                using Microsoft.SemanticKernel.Agents.Chat;
                public class O { void M(Kernel kernel, AgentGroupChat chat) { var a = new ChatCompletionAgent(); } }
                """));

        using var doc = JsonDocument.Parse(new SemanticKernelDetectorTool().MafDetectSourceFramework(repo.Path, "json"));
        var root = doc.RootElement;

        Assert.True(root.GetProperty("semantic_kernel_detected").GetBoolean());
        Assert.Contains(root.GetProperty("verdict").GetString(), new[] { "EASY", "MEDIUM", "HARD" });

        var constructs = root.GetProperty("constructs");
        var counts = root.GetProperty("counts");
        // counts reconcile with the constructs array:
        var kindCount = constructs.GetArrayLength();
        Assert.Equal(kindCount,
            counts.GetProperty("bridgeable").GetInt32()
            + counts.GetProperty("rewrite").GetInt32()
            + counts.GetProperty("rearchitect").GetInt32());
        var occSum = 0;
        foreach (var c in constructs.EnumerateArray())
        {
            occSum += c.GetProperty("count").GetInt32();
            Assert.Contains(c.GetProperty("strategy").GetString(), new[] { "bridgeable", "rewrite", "rearchitect" });
        }
        Assert.Equal(occSum, counts.GetProperty("occurrences").GetInt32());
    }

    [Fact]
    public void MafDetectSourceFramework_InvalidPath_ReturnsErrorInRequestedShape()
    {
        const string bad = "this/path/does/not/exist-xyz";

        var markdown = new SemanticKernelDetectorTool().MafDetectSourceFramework(bad);
        Assert.StartsWith("Error:", markdown); // PathGuard plain-text error for markdown

        using var doc = JsonDocument.Parse(new SemanticKernelDetectorTool().MafDetectSourceFramework(bad, "json"));
        Assert.True(doc.RootElement.TryGetProperty("error", out var err));
        Assert.False(string.IsNullOrWhiteSpace(err.GetString()));
    }

    // -------------------------------------------------------------------------
    // Doc ↔ code drift: the construct-mapping strategy tags (DRY-001)
    // -------------------------------------------------------------------------

    [Fact]
    public void SkillConstructTable_StrategyTags_MatchDetector_NoDrift()
    {
        // DRY-001 guard. The SK→MAF construct mapping is hand-authored in BOTH KnownTypes/Classify
        // (code) AND the navigator skill's "Construct → MAF target → strategy" table (doc). This
        // pins the doc's 🌉/🔁/🏗 tag to the detector for every SK *type* the table names that the
        // detector actually classifies — so a strategy that flips in one place but not the other
        // fails CI. (The migration guide is a third copy of the same knowledge; coupling it the
        // same way is a reasonable follow-up — see docs/reviews/sk-migration-remediation-plan.md.)
        var skill = ResolveRepoFile(System.IO.Path.Combine(".github", "skills", "maf-from-semantic-kernel", "SKILL.md"));
        var lines = System.IO.File.ReadAllLines(skill);

        var emojiToStrategy = new Dictionary<string, MigrationStrategy>
        {
            ["🌉"] = MigrationStrategy.Bridgeable,
            ["🔁"] = MigrationStrategy.Rewrite,
            ["🏗"] = MigrationStrategy.Rearchitect,
        };
        // A clean C# type name (optionally an interface): excludes attributes (`[KernelFunction]`),
        // method/property tokens, wildcards (`*AgentThread`), and dotted/parenthesised forms.
        var typeName = new Regex("^I?[A-Z][A-Za-z0-9]+$");

        var coupled = 0;
        var inTable = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("| SK construct ", System.StringComparison.Ordinal)) { inTable = true; continue; }
            if (inTable && !line.StartsWith("|", System.StringComparison.Ordinal)) { inTable = false; continue; }
            if (!inTable || line.StartsWith("|---", System.StringComparison.Ordinal) || line.StartsWith("| ---", System.StringComparison.Ordinal))
                continue;

            var cells = line.Split('|');
            if (cells.Length < 4) continue;
            var constructCell = cells[1];
            var strategyCell = cells[3];

            var rowStrategies = emojiToStrategy
                .Where(kv => strategyCell.Contains(kv.Key, System.StringComparison.Ordinal))
                .Select(kv => kv.Value).ToHashSet();
            if (rowStrategies.Count == 0) continue; // not a strategy-bearing construct row

            foreach (Match m in typeNameMatches(constructCell))
            {
                var token = m.Groups[1].Value;
                if (!typeName.IsMatch(token)) continue;

                var constructs = SemanticKernelDetectorTool.AnalyzeSource(
                    "using Microsoft.SemanticKernel;\npublic class T { void M(" + token + " x) { } }", "T.cs");
                if (constructs.Count == 0) continue; // type the detector doesn't classify in type position — out of scope

                coupled++;
                Assert.True(
                    constructs.Any(c => rowStrategies.Contains(c.Strategy)),
                    $"SKILL.md tags `{token}` as [{string.Join("/", rowStrategies)}] but the detector classifies it as " +
                    $"[{string.Join(", ", constructs.Select(c => c.Strategy))}] — KnownTypes/Classify and the skill table have drifted.");
            }
        }

        // Floor: the table couples a substantial set of types, so a parser regression that silently
        // matches nothing can't pass.
        Assert.True(coupled >= 12, $"expected to couple ≥12 SK types to the skill table; coupled {coupled}");

        static MatchCollection typeNameMatches(string cell) => Regex.Matches(cell, "`([^`]+)`");
    }

    /// <summary>Walks up from the test bin dir to the repo root (marker: <c>.github</c>), then to a relative file.</summary>
    private static string ResolveRepoFile(string relative)
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, ".github")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = System.IO.Path.Combine(dir!.FullName, relative);
        Assert.True(System.IO.File.Exists(path), $"file not found: {path}");
        return path;
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
