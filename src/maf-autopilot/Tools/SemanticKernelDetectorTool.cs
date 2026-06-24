using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: <c>MafDetectSourceFramework</c>.
///
/// **Phase 1 of the cross-framework migration assistant** (Semantic Kernel → MAF).
/// Scans a repo for Semantic Kernel usage — package references + source-level
/// constructs — and returns a *construct inventory* where every finding is tagged
/// with its migration strategy:
/// <list type="bullet">
///   <item><b>🌉 bridgeable</b> — reuse the SK code as-is via an interop shim
///         (e.g. <c>KernelFunction.AsAIFunction()</c>, <c>IChatCompletionService.AsChatClient()</c>).</item>
///   <item><b>🔁 rewrite</b> — a mechanical SK→MAF API swap (agent creation,
///         threads→sessions, invocation, options, DI).</item>
///   <item><b>🏗 re-architect</b> — no 1:1 mapping; needs MAF workflows/middleware
///         (group chat, planners, function-invocation filters).</item>
/// </list>
/// The inventory is the migration work-list the <c>maf-migrate-from</c> flow drives.
/// Grounded in <c>docs/migration/from-semantic-kernel-to-maf.md</c> (served at
/// <c>maf://migrate-from?source=semantic-kernel</c>). Detection is purely syntactic
/// (Roslyn) and scoped to files that import a <c>Microsoft.SemanticKernel</c>
/// namespace, so it does not over-match unrelated code.
/// </summary>
[McpServerToolType]
public sealed class SemanticKernelDetectorTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Detect Semantic Kernel usage in a repo and inventory every construct that a
        migration to Microsoft Agent Framework would touch, tagged by migration
        strategy (bridgeable / rewrite / re-architect) with file:line, plus an
        overall complexity verdict (EASY / MEDIUM / HARD). The work-list for the
        `maf-migrate-from` flow. Read-only; nothing is written.

        Input:
          - repoPath: absolute path to the repo to scan.
          - format: "markdown" (default) for a human report, or "json".

        Returns the inventory. If no Semantic Kernel usage is found, says so.
        """)]
    public string MafDetectSourceFramework(
        [Description("Absolute path to the repo or project to scan.")] string repoPath,
        [Description("Output format: \"markdown\" (default) or \"json\".")] string format = "markdown")
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err)
            return format.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? JsonSerializer.Serialize(new { error = err })
                : err;

        var report = Scan(repoPath);
        return format.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Serialize(report.ToJson(), new JsonSerializerOptions { WriteIndented = true })
            : report.ToMarkdown();
    }

    // -------------------------------------------------------------------------
    // Pure scan core (testable without MCP)
    // -------------------------------------------------------------------------

    /// <summary>Scans a repo: csproj package references + every .cs file's SK constructs.</summary>
    internal static SkScanResult Scan(string repoPath)
    {
        var packages = DetectPackages(repoPath);
        var constructs = new List<SkConstruct>();
        foreach (var file in SourceFileWalker.EnumerateCsFiles(repoPath))
        {
            string rel;
            try { rel = SourceFileWalker.MakeRelative(repoPath, file); }
            catch (InvalidOperationException) { rel = file; }
            constructs.AddRange(AnalyzeSource(File.ReadAllText(file), rel));
        }
        return new SkScanResult(packages, constructs);
    }

    /// <summary>
    /// Reads every .csproj under the repo and returns the Semantic Kernel package
    /// references it finds (id + version). Pure XML — no build/restore.
    /// </summary>
    internal static IReadOnlyList<SkPackage> DetectPackages(string repoPath)
    {
        var packages = new List<SkPackage>();
        if (!Directory.Exists(repoPath))
            return packages;

        foreach (var csproj in SourceFileWalker.EnumerateCsprojFiles(repoPath))
        {
            XDocument doc;
            try { doc = XDocument.Load(csproj); }
            catch { continue; } // malformed csproj — skip, don't fail the scan
            foreach (var pr in doc.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
            {
                var id = (string?)pr.Attribute("Include") ?? (string?)pr.Attribute("Update");
                if (id is null || !id.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal))
                    continue;
                var version = (string?)pr.Attribute("Version")
                    ?? pr.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;
                packages.Add(new SkPackage(id, version ?? "(unpinned)"));
            }
        }
        return packages
            .GroupBy(p => p.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Inventories the Semantic Kernel constructs in one C# file. Returns at most one
    /// entry per construct KIND (first occurrence line + count), so a file with five
    /// <c>[KernelFunction]</c> methods reports one "[KernelFunction] plugin method" ×5.
    /// Scoped to files that import a <c>Microsoft.SemanticKernel</c> namespace.
    /// </summary>
    public static IReadOnlyList<SkConstruct> AnalyzeSource(string source, string fileName = "<inline>")
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();

        var importsSk = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Any(u => u.Name?.ToString().StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal) == true);
        if (!importsSk)
            return Array.Empty<SkConstruct>();

        // kind -> (strategy, note, firstLine, count)
        var hits = new Dictionary<string, (MigrationStrategy Strategy, string Note, int Line, int Count)>(StringComparer.Ordinal);

        void Record(string kind, MigrationStrategy strategy, string note, int line)
        {
            if (hits.TryGetValue(kind, out var cur))
                hits[kind] = (cur.Strategy, cur.Note, Math.Min(cur.Line, line), cur.Count + 1);
            else
                hits[kind] = (strategy, note, line, 1);
        }

        static int LineOf(SyntaxNode n) => n.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                // [KernelFunction] plugin methods — 🌉 reuse via AsAIFunction(), or rewrite as a plain [Description] method.
                case AttributeSyntax attr when SimpleAttrName(attr) is "KernelFunction" or "KernelFunctionFromMethod":
                    Record("[KernelFunction] plugin method", MigrationStrategy.Bridgeable,
                        "Reuse via `KernelFunction.AsAIFunction()`, or rewrite as a plain `[Description]` method passed to `tools:`.",
                        LineOf(attr));
                    break;

                // Any reference to a known SK type (object-creation, base type, generic, identifier).
                case IdentifierNameSyntax id:
                    Classify(id.Identifier.ValueText, LineOf(id));
                    break;
                case GenericNameSyntax gen:
                    Classify(gen.Identifier.ValueText, LineOf(gen));
                    break;

                // SK invocation pattern: agent.InvokeAsync / InvokeStreamingAsync -> 🔁 RunAsync / RunStreamingAsync.
                case MemberAccessExpressionSyntax m when m.Name.Identifier.ValueText is "InvokeAsync" or "InvokeStreamingAsync":
                    Record("Agent invocation (`Invoke*Async`)", MigrationStrategy.Rewrite,
                        "→ `RunAsync` / `RunStreamingAsync`; adapt to `AgentResponse` / `AgentResponseUpdate`.",
                        LineOf(m));
                    break;

                // Kernel-level OpenTelemetry wiring -> 🔁 .UseOpenTelemetry() on the IChatClient chain.
                case MemberAccessExpressionSyntax ot when ot.Name.Identifier.ValueText == "AddOpenTelemetry":
                    Record("Observability (`AddOpenTelemetry`)", MigrationStrategy.Rewrite,
                        "→ `.UseOpenTelemetry()` on the `IChatClient` build chain (subscribe to source `Microsoft.Extensions.AI`).",
                        LineOf(ot));
                    break;
            }
        }

        void Classify(string typeName, int line)
        {
            // Planners were removed in SK → re-architect onto MAF's automatic function-calling loop.
            if (typeName.EndsWith("Planner", StringComparison.Ordinal) && typeName != "Planner")
            {
                Record($"Planner (`{typeName}`)", MigrationStrategy.Rearchitect,
                    "Planners were removed — re-architect onto MAF's automatic function-calling loop.", line);
                return;
            }
            // Function-invocation filters → MAF agent middleware. A clean port (defined
            // target), so a 🔁 rewrite rather than a 🏗 re-architecture.
            if (typeName is "IFunctionInvocationFilter" or "IAutoFunctionInvocationFilter" or "IPromptRenderFilter")
            {
                Record($"Filter (`{typeName}`)", MigrationStrategy.Rewrite,
                    "→ MAF agent middleware (function-calling / agent-run) via `.AsBuilder().Use(...)`.", line);
                return;
            }
            if (KnownTypes.TryGetValue(typeName, out var rule))
                Record(rule.Kind, rule.Strategy, rule.Note, line);
        }

        return hits
            .Select(kv => new SkConstruct(kv.Key, kv.Value.Strategy, fileName, kv.Value.Line, kv.Value.Note, kv.Value.Count))
            .OrderBy(c => (int)c.Strategy)
            .ThenBy(c => c.Line)
            .ToList();
    }

    private static string SimpleAttrName(AttributeSyntax attr)
    {
        var n = attr.Name.ToString();
        n = n.Contains('.') ? n[(n.LastIndexOf('.') + 1)..] : n;
        return n.EndsWith("Attribute", StringComparison.Ordinal) ? n[..^"Attribute".Length] : n;
    }

    /// <summary>SK type name → migration construct + strategy. Distinctive SK names only.</summary>
    private static readonly IReadOnlyDictionary<string, (string Kind, MigrationStrategy Strategy, string Note)> KnownTypes =
        new Dictionary<string, (string, MigrationStrategy, string)>(StringComparer.Ordinal)
        {
            ["AgentGroupChat"]         = ("Multi-agent group chat (`AgentGroupChat`)", MigrationStrategy.Rearchitect, "→ a MAF group-chat workflow (`AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)`)."),
            ["ChatCompletionAgent"]    = ("SK agent (`ChatCompletionAgent`)", MigrationStrategy.Rewrite, "→ `chatClient.AsAIAgent(instructions:, tools:)` (drop the `Kernel`)."),
            ["AzureAIAgent"]           = ("SK agent (`AzureAIAgent`)", MigrationStrategy.Rewrite, "→ `aiProjectClient.AsAIAgent(model:, instructions:)`."),
            ["OpenAIAssistantAgent"]   = ("SK agent (`OpenAIAssistantAgent`)", MigrationStrategy.Rewrite, "→ `assistantClient.CreateAIAgentAsync(instructions:)`."),
            ["IChatCompletionService"] = ("Chat service (`IChatCompletionService`)", MigrationStrategy.Bridgeable, "→ bridge with `.AsChatClient()`, or use an `IChatClient` provider directly."),
            ["KernelArguments"]        = ("Run options (`KernelArguments`)", MigrationStrategy.Rewrite, "→ `ChatOptions` / `ChatClientAgentRunOptions`."),
            ["KernelPlugin"]           = ("Plugin wrapper (`KernelPlugin`)", MigrationStrategy.Bridgeable, "→ no plugin concept; bridge each function via `.AsAIFunction()` or pass methods to `tools:`."),
            ["KernelPluginFactory"]    = ("Plugin factory (`KernelPluginFactory`)", MigrationStrategy.Rewrite, "→ pass methods directly to `tools:` (no plugin wrapper)."),
            ["KernelFunctionFactory"]  = ("Function factory (`KernelFunctionFactory`)", MigrationStrategy.Rewrite, "→ `AIFunctionFactory.Create(...)`."),
            // `Kernel` itself — distinctive enough inside an SK-importing file.
            ["Kernel"]                 = ("Kernel (`Kernel`)", MigrationStrategy.Rewrite, "Removed in MAF — create the agent straight from the chat client; delete the `Kernel`."),
            ["IKernelBuilder"]         = ("Kernel builder (`IKernelBuilder`)", MigrationStrategy.Rewrite, "Removed in MAF — no kernel to build."),
            // Chat history persistence — maps to a MAF provider / session serialization.
            ["ChatHistory"]            = ("Chat history (`ChatHistory`)", MigrationStrategy.Rewrite, "→ `ChatHistoryProvider` / `InMemoryChatHistoryProvider`, or `agent.SerializeSession(...)` for whole-session persistence."),
            // Vector stores / RAG — re-architect onto a Context Provider.
            ["IVectorStore"]           = ("Vector store (`IVectorStore`)", MigrationStrategy.Rearchitect, "→ a `TextSearchProvider` attached via `ChatClientAgentOptions.AIContextProviders` (storage via `Microsoft.Extensions.VectorData`)."),
            ["ITextSearch"]            = ("Text search (`ITextSearch`)", MigrationStrategy.Rearchitect, "→ `TextSearchProvider` (RAG via Context Providers)."),
            ["VectorStoreTextSearch"]  = ("Vector text search (`VectorStoreTextSearch`)", MigrationStrategy.Rearchitect, "→ `TextSearchProvider` + `AIContextProviders`."),
            // Prompt templates — no built-in template engine in MAF.
            ["KernelPromptTemplate"]   = ("Prompt template (`KernelPromptTemplate`)", MigrationStrategy.Rearchitect, "No built-in MAF template engine — render the template yourself (e.g. Handlebars.NET / Scriban) and pass the result as `instructions`, or move dynamic data into a Context Provider."),
            ["PromptTemplateConfig"]   = ("Prompt template (`PromptTemplateConfig`)", MigrationStrategy.Rearchitect, "No built-in MAF template engine — render externally and pass as `instructions`."),
            ["HandlebarsPromptTemplateFactory"] = ("Prompt template (Handlebars)", MigrationStrategy.Rearchitect, "No built-in MAF template engine — render Handlebars yourself and pass the result as `instructions`."),
            ["LiquidPromptTemplateFactory"]     = ("Prompt template (Liquid)", MigrationStrategy.Rearchitect, "No built-in MAF template engine — render Liquid yourself and pass the result as `instructions`."),
            ["KernelFunctionFromPrompt"]        = ("Prompt-template function (`KernelFunctionFromPrompt`)", MigrationStrategy.Rearchitect, "No built-in template engine — render the prompt and expose it as a plain tool/method, or move dynamic context into a Context Provider."),
        };

    // -------------------------------------------------------------------------
    // Result model + rendering
    // -------------------------------------------------------------------------

    internal sealed record SkScanResult(IReadOnlyList<SkPackage> Packages, IReadOnlyList<SkConstruct> Constructs)
    {
        public bool SemanticKernelDetected => Packages.Count > 0 || Constructs.Count > 0;

        public int TotalOccurrences => Constructs.Sum(c => c.Count);
        public int Bridgeable => Constructs.Count(c => c.Strategy == MigrationStrategy.Bridgeable);
        public int Rewrite => Constructs.Count(c => c.Strategy == MigrationStrategy.Rewrite);
        public int Rearchitect => Constructs.Count(c => c.Strategy == MigrationStrategy.Rearchitect);

        /// <summary>EASY (bridge/rewrite only, small), MEDIUM (rewrites), HARD (re-architecture present).</summary>
        public string Verdict =>
            Rearchitect > 0 ? "HARD"
            : (Rewrite > 0 || TotalOccurrences > 8) ? "MEDIUM"
            : "EASY";

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Semantic Kernel → MAF — migration scan");
            sb.AppendLine();
            if (!SemanticKernelDetected)
            {
                sb.AppendLine("✅ No Semantic Kernel usage detected (no `Microsoft.SemanticKernel` package reference or source import). Nothing to migrate from SK.");
                return sb.ToString();
            }

            if (Packages.Count > 0)
            {
                sb.AppendLine("**Detected packages:**");
                foreach (var p in Packages) sb.AppendLine($"- `{p.Id}` {p.Version}");
                sb.AppendLine();
            }

            sb.AppendLine($"**Migration complexity: {Verdict}** — {Constructs.Count} construct kind(s), {TotalOccurrences} occurrence(s): " +
                          $"🌉 {Bridgeable} bridgeable · 🔁 {Rewrite} rewrite · 🏗 {Rearchitect} re-architect.");
            sb.AppendLine();

            AppendGroup(sb, MigrationStrategy.Bridgeable, "🌉 Bridgeable — reuse the SK code via an interop shim (gradual / side-by-side)");
            AppendGroup(sb, MigrationStrategy.Rewrite, "🔁 Rewrite — mechanical SK→MAF API swap");
            AppendGroup(sb, MigrationStrategy.Rearchitect, "🏗 Re-architect — no 1:1; redesign onto MAF workflows / middleware");

            sb.AppendLine("> **Next:** run the `maf-migrate-from` prompt (source: `semantic-kernel`) in an MCP client to turn this into a `migration-plan.md` and a side-by-side MAF project. Mappings: `maf://migrate-from?source=semantic-kernel`.");
            return sb.ToString();
        }

        private void AppendGroup(StringBuilder sb, MigrationStrategy strategy, string heading)
        {
            var items = Constructs.Where(c => c.Strategy == strategy).ToList();
            if (items.Count == 0) return;
            sb.AppendLine($"### {heading}");
            sb.AppendLine();
            foreach (var c in items)
            {
                var count = c.Count > 1 ? $" ×{c.Count}" : "";
                sb.AppendLine($"- **{c.Kind}**{count} — `{c.File}:{c.Line}`");
                sb.AppendLine($"  - {c.Note}");
            }
            sb.AppendLine();
        }

        public object ToJson() => new
        {
            semantic_kernel_detected = SemanticKernelDetected,
            verdict = Verdict,
            packages = Packages.Select(p => new { id = p.Id, version = p.Version }),
            counts = new { bridgeable = Bridgeable, rewrite = Rewrite, rearchitect = Rearchitect, occurrences = TotalOccurrences },
            constructs = Constructs.Select(c => new
            {
                kind = c.Kind,
                strategy = c.Strategy.ToString().ToLowerInvariant(),
                file = c.File,
                line = c.Line,
                count = c.Count,
                note = c.Note,
            }),
        };
    }
}

/// <summary>Migration strategy for a Semantic Kernel construct. Order = report order.</summary>
public enum MigrationStrategy
{
    /// <summary>Reuse as-is via an interop shim (AsAIFunction / AsChatClient).</summary>
    Bridgeable = 0,
    /// <summary>Mechanical SK→MAF API swap.</summary>
    Rewrite = 1,
    /// <summary>No 1:1 mapping — redesign onto MAF workflows / middleware.</summary>
    Rearchitect = 2,
}

/// <summary>One Semantic Kernel package reference found in a csproj.</summary>
public sealed record SkPackage(string Id, string Version);

/// <summary>One Semantic Kernel construct kind found in a file (first line + occurrence count).</summary>
public sealed record SkConstruct(
    string Kind,
    MigrationStrategy Strategy,
    string File,
    int Line,
    string Note,
    int Count);
