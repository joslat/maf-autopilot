using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafScanAntiPatterns
///
/// Walks a MAF 1.3.0 codebase looking for known anti-patterns AFTER migration is complete.
/// Rules canonical: see <c>.github/skills/maf-anti-pattern-scanner/SKILL.md</c>.
///
/// This is the **identity-unlock** tool — it materialises the "co-pilot beyond migration"
/// promise by running the constraints listed in `maf-constraints.instructions.md` as
/// actual scans instead of aspirational bullets.
/// </summary>
[McpServerToolType]
public sealed class AntiPatternScannerTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Scan a MAF 1.3.0 codebase for known anti-patterns (security, concurrency,
        observability, identity, topology). Distinct from migration tooling —
        this checks idiom and configuration on code that's already on 1.3.0.

        Input:
          - repoPath: absolute path to the repo root. All *.cs files are scanned recursively
            (excluding bin/obj/tests/samples by default).
          - format: "markdown" (default) for a human-readable report, or "sarif" for a
            SARIF v2.1.0 document suitable for GitHub Advanced Security ingestion.

        Returns a categorised report grouped by severity (error / warning / info).
        Rule list source: maf://rules.
        """)]
    public string MafScanAntiPatterns(
        [Description("Absolute path to the repository root (or any directory).")] string repoPath,
        [Description("Output format: \"markdown\" (default) or \"sarif\" (v2.1.0 JSON).")] string format = "markdown")
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err) return err;

        var findings = new List<AntiPatternFinding>();
        foreach (var file in EnumerateScannableFiles(repoPath))
        {
            var source = File.ReadAllText(file);
            findings.AddRange(ScanFile(source, SourceFileWalker.MakeRelative(repoPath, file)));
        }

        return format.Equals("sarif", StringComparison.OrdinalIgnoreCase)
            ? SarifExportTool.EmitAntiPatternsSarif(findings)
            : FormatReport(repoPath, findings);
    }

    // -------------------------------------------------------------------------
    // Pure-function core (testable without files)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs all anti-pattern rules against a single source string. Pure: no I/O.
    /// </summary>
    public static IReadOnlyList<AntiPatternFinding> ScanFile(string source, string fileName = "<inline>")
    {
        var isTestFile = IsTestFile(fileName);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var findings = new List<AntiPatternFinding>();

        foreach (var rule in AllRules)
        {
            if (rule.SkipInTestFiles && isTestFile) continue;
            findings.AddRange(rule.Scan(source, root, fileName));
        }
        return findings;
    }

    internal static bool IsTestFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        var normalized = fileName.Replace('\\', '/').ToLowerInvariant();

        // Any path segment that itself looks like test / sample infrastructure.
        foreach (var segment in normalized.Split('/'))
        {
            if (segment is "tests" or "test" or "samples" or "unittests" or "integrationtests" or "e2e")
                return true;
            if (segment.EndsWith(".tests", StringComparison.Ordinal)
                || segment.EndsWith(".unittests", StringComparison.Ordinal)
                || segment.EndsWith(".integrationtests", StringComparison.Ordinal)
                || segment.EndsWith(".e2e", StringComparison.Ordinal))
                return true;
        }
        return normalized.EndsWith("tests.cs", StringComparison.Ordinal)
            || normalized.EndsWith("test.cs", StringComparison.Ordinal)
            || normalized.EndsWith("fixtures.cs", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateScannableFiles(string repoRoot)
        => SourceFileWalker.EnumerateCsFiles(repoRoot);

    // -------------------------------------------------------------------------
    // Rules
    // -------------------------------------------------------------------------

    // Phase 5.9 — ReDoS hygiene on every rule pattern.
    //
    // All AntiPatternScanner regexes carry NonBacktracking + a 100ms
    // MatchTimeout. The patterns are simple enough that catastrophic
    // backtracking is unlikely in practice, but `MAF-AP-SEC-002` (the
    // API-key matcher) and `MAF-AP-CONC-002` (the .Result/.Wait matcher)
    // both have unbounded character classes adjacent to alternation —
    // exactly the shape that can pin CPU on a crafted line in the
    // surface-scanning rewriter. The shared options constant keeps the
    // policy locally enforceable: future rules MUST be added with
    // `RegexHygiene` and the CI invariant (ci-invariants.yml Job 2)
    // confirms the contract.
    private const RegexOptions RegexHygiene =
        RegexOptions.Compiled | RegexOptions.NonBacktracking;
    private static readonly TimeSpan RegexBudget = TimeSpan.FromMilliseconds(100);

    internal static readonly IReadOnlyList<AntiPatternRule> AllRules = new AntiPatternRule[]
    {
        new RegexRule(
            id: "MAF-AP-SEC-001",
            name: "DefaultAzureCredential in production code",
            severity: AntiPatternSeverity.Error,
            pattern: new Regex(@"\bnew\s+DefaultAzureCredential\s*\(", RegexHygiene, RegexBudget),
            skipInTestFiles: true),

        new RegexRule(
            id: "MAF-AP-SEC-002",
            name: "Hard-coded API key literal",
            severity: AntiPatternSeverity.Error,
            // Matches "sk-XXXXXXXXXXXXXXXX" or "api-key=XXXXXXXXXXXXXXXX" style strings.
            pattern: new Regex(@"""(?:sk-|api[-_]?key=)[A-Za-z0-9_\-]{16,}""", RegexHygiene | RegexOptions.IgnoreCase, RegexBudget),
            skipInTestFiles: false),

        new RegexRule(
            id: "MAF-AP-SEC-003",
            name: "EnableSensitiveData = true outside development",
            severity: AntiPatternSeverity.Error,
            pattern: new Regex(@"EnableSensitiveData\s*=\s*true", RegexHygiene, RegexBudget),
            skipInTestFiles: true),

        new RegexRule(
            id: "MAF-AP-CONC-002",
            name: "Sync-over-async (.Result / .Wait())",
            severity: AntiPatternSeverity.Warning,
            pattern: new Regex(@"\b\)\s*\.(Result|Wait)\s*(\(\))?", RegexHygiene, RegexBudget),
            skipInTestFiles: true),

        new RegexRule(
            id: "MAF-AP-OBS-001",
            name: "Missing UseOpenTelemetry in file that builds an agent",
            severity: AntiPatternSeverity.Warning,
            // Two conditions in the same file: builder type AND no UseOpenTelemetry call.
            pattern: null,
            skipInTestFiles: true,
            customScan: (source, _, file) =>
            {
                var hasBuilder = source.Contains("AIAgentBuilder")
                              || Regex.IsMatch(source, @"\bnew\s+ChatClientAgent\s*\(");
                var hasOTel = source.Contains("UseOpenTelemetry");
                if (hasBuilder && !hasOTel)
                {
                    var line = FirstLineMatching(source, @"AIAgentBuilder|new\s+ChatClientAgent\s*\(");
                    return new[]
                    {
                        new AntiPatternFinding(
                            "MAF-AP-OBS-001",
                            "Missing UseOpenTelemetry in file that builds an agent",
                            AntiPatternSeverity.Warning,
                            file,
                            line,
                            "(agent built without telemetry chained)")
                    };
                }
                return Array.Empty<AntiPatternFinding>();
            }),

        new RoslynRule(
            id: "MAF-AP-CONC-001",
            name: "Instance fields on AIContextProvider (must be ProviderSessionState<T>)",
            severity: AntiPatternSeverity.Error,
            scan: (root, file) =>
            {
                var findings = new List<AntiPatternFinding>();
                foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var baseList = cls.BaseList?.Types.Select(t => t.Type.ToString()) ?? [];
                    var derivesFromProvider = baseList.Any(b =>
                        b.Contains("AIContextProvider") || b.Contains("ChatHistoryProvider"));
                    if (!derivesFromProvider) continue;

                    foreach (var field in cls.Members.OfType<FieldDeclarationSyntax>())
                    {
                        var modifiers = field.Modifiers.Select(m => m.ValueText).ToHashSet();
                        if (modifiers.Contains("readonly") || modifiers.Contains("const") || modifiers.Contains("static"))
                            continue;
                        var loc = field.GetLocation().GetLineSpan();
                        var variable = field.Declaration.Variables.FirstOrDefault();
                        findings.Add(new AntiPatternFinding(
                            "MAF-AP-CONC-001",
                            "Instance fields on AIContextProvider (must be ProviderSessionState<T>)",
                            AntiPatternSeverity.Error,
                            file,
                            loc.StartLinePosition.Line + 1,
                            $"mutable field `{variable?.Identifier.ValueText}` on `{cls.Identifier.ValueText}`"));
                    }
                }
                return findings;
            }),

        // MAF-AP-AGENT-001 — Instructions at top-level of ChatClientAgentOptions.
        // Silent runtime failure: the property compiles but the agent ignores it.
        new RoslynRule(
            id: "MAF-AP-AGENT-001",
            name: "Instructions outside ChatOptions — silently ignored in 1.3.0",
            severity: AntiPatternSeverity.Error,
            scan: (root, file) =>
            {
                var findings = new List<AntiPatternFinding>();
                // Find object initializers on ChatClientAgentOptions where an Instructions
                // assignment appears at the OUTER level (not nested inside `ChatOptions = new { ... }`).
                foreach (var oce in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    var typeName = oce.Type.ToString();
                    var simpleName = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
                    if (simpleName != "ChatClientAgentOptions") continue;
                    if (oce.Initializer is null) continue;

                    foreach (var expr in oce.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                    {
                        var leftText = expr.Left.ToString();
                        if (leftText != "Instructions") continue;
                        var loc = expr.GetLocation().GetLineSpan();
                        findings.Add(new AntiPatternFinding(
                            "MAF-AP-AGENT-001",
                            "Instructions outside ChatOptions — silently ignored in 1.3.0",
                            AntiPatternSeverity.Error,
                            file,
                            loc.StartLinePosition.Line + 1,
                            $"Instructions assigned at top of `ChatClientAgentOptions` — move inside `ChatOptions = new() {{ ... }}`"));
                    }
                }
                return findings;
            }),

        // MAF-AP-WF-001 — workflow executor class must be `sealed partial`.
        // Without `partial`, the source generator cannot emit the dispatch table → build break.
        new RoslynRule(
            id: "MAF-AP-WF-001",
            name: "Executor class must be `sealed partial`",
            severity: AntiPatternSeverity.Error,
            scan: (root, file) =>
            {
                var findings = new List<AntiPatternFinding>();
                foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    // Only consider classes that inherit from `Executor` and have [MessageHandler] methods.
                    var baseList = cls.BaseList?.Types.Select(t => t.Type.ToString()) ?? [];
                    if (!baseList.Any(b => b == "Executor" || b.EndsWith(".Executor", StringComparison.Ordinal)))
                        continue;
                    if (!cls.Members.OfType<MethodDeclarationSyntax>().Any(m =>
                        m.AttributeLists.SelectMany(a => a.Attributes).Any(a =>
                        {
                            var n = a.Name.ToString();
                            var s = n.Contains('.') ? n[(n.LastIndexOf('.') + 1)..] : n;
                            return s is "MessageHandler" or "MessageHandlerAttribute";
                        })))
                        continue;

                    var modifiers = cls.Modifiers.Select(m => m.ValueText).ToHashSet();
                    var hasPartial = modifiers.Contains("partial");
                    var hasSealed = modifiers.Contains("sealed");
                    if (hasPartial && hasSealed) continue;

                    var missing = (hasPartial, hasSealed) switch
                    {
                        (false, false) => "`partial` and `sealed`",
                        (false, true) => "`partial`",
                        (true, false) => "`sealed`",
                        _ => "(none)",
                    };
                    var loc = cls.Identifier.GetLocation().GetLineSpan();
                    findings.Add(new AntiPatternFinding(
                        "MAF-AP-WF-001",
                        "Executor class must be `sealed partial`",
                        AntiPatternSeverity.Error,
                        file,
                        loc.StartLinePosition.Line + 1,
                        $"`{cls.Identifier.ValueText}` is missing {missing}"));
                }
                return findings;
            }),

        // MAF-AP-DEVUI-001 — DevUI references must be guarded by #if DEVUI_ENABLED.
        // Without the guard, production builds fail (DevUI/Hosting have no 1.3.0 equivalent).
        new RoslynRule(
            id: "MAF-AP-DEVUI-001",
            name: "DevUI references must be guarded by #if DEVUI_ENABLED",
            severity: AntiPatternSeverity.Error,
            scan: (root, file) =>
            {
                var findings = new List<AntiPatternFinding>();

                // Collect every line range that lies inside an `#if DEVUI_ENABLED ... #endif`
                // (or any conditional whose condition mentions DEVUI_ENABLED). We then check
                // each DevUI reference against those ranges.
                var guardedLineRanges = CollectDevUiGuardedLineRanges(root);

                foreach (var node in root.DescendantNodes(descendIntoTrivia: false))
                {
                    var text = node switch
                    {
                        UsingDirectiveSyntax u => u.Name?.ToString(),
                        QualifiedNameSyntax q => q.ToString(),
                        IdentifierNameSyntax id when id.Identifier.ValueText == "DevUI" => "DevUI",
                        _ => null,
                    };
                    if (string.IsNullOrEmpty(text)) continue;
                    if (!text!.Contains("Microsoft.Agents.AI.DevUI", StringComparison.Ordinal)
                        && !text.Contains("Microsoft.Agents.AI.Hosting", StringComparison.Ordinal)
                        && text != "DevUI")
                        continue;

                    var loc = node.GetLocation().GetLineSpan();
                    var line = loc.StartLinePosition.Line + 1;
                    if (guardedLineRanges.Any(r => line >= r.Start && line <= r.End)) continue;

                    findings.Add(new AntiPatternFinding(
                        "MAF-AP-DEVUI-001",
                        "DevUI references must be guarded by #if DEVUI_ENABLED",
                        AntiPatternSeverity.Error,
                        file,
                        line,
                        text.Length > 60 ? text[..60] + "…" : text));
                }

                // Per-finding de-duplication: collapse multiple references on the same line.
                return findings
                    .GroupBy(f => (f.File, f.Line))
                    .Select(g => g.First())
                    .ToList();
            }),

        // MAF-AP-MID-001 — `.Use(runFunc:, runStreamingFunc:)` must provide BOTH callbacks.
        // Passing only one means the streaming path silently bypasses the middleware.
        new RoslynRule(
            id: "MAF-AP-MID-001",
            name: "Middleware Use() must provide both runFunc and runStreamingFunc",
            severity: AntiPatternSeverity.Warning,
            scan: (root, file) =>
            {
                var findings = new List<AntiPatternFinding>();
                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax member) continue;
                    if (member.Name.Identifier.ValueText != "Use") continue;
                    var argText = invocation.ArgumentList.ToString();

                    // Heuristic: if `runFunc` appears but `runStreamingFunc` doesn't, or
                    // either is explicitly set to `null`, the streaming path bypasses.
                    var hasRunFunc = argText.Contains("runFunc", StringComparison.Ordinal);
                    var hasStreaming = argText.Contains("runStreamingFunc", StringComparison.Ordinal);
                    var streamingIsNull = argText.Contains("runStreamingFunc: null", StringComparison.Ordinal)
                                       || argText.Contains("runStreamingFunc:null", StringComparison.Ordinal);
                    if (!hasRunFunc) continue;
                    if (hasStreaming && !streamingIsNull) continue;

                    var loc = invocation.GetLocation().GetLineSpan();
                    var why = streamingIsNull
                        ? "runStreamingFunc: null"
                        : "runStreamingFunc not provided";
                    findings.Add(new AntiPatternFinding(
                        "MAF-AP-MID-001",
                        "Middleware Use() must provide both runFunc and runStreamingFunc",
                        AntiPatternSeverity.Warning,
                        file,
                        loc.StartLinePosition.Line + 1,
                        $".Use(...) — {why}; streaming path bypasses the middleware"));
                }
                return findings;
            }),

        // MAF-AP-EXEC-001 — Pre-1.3.0 executor patterns. Detects the legacy executor
        // surface that should have been migrated. Salvaged from the May-6 pre-Phase-O
        // sketch (tag: pre-phase-o-may6-sketch). The corresponding CS0618 patterns
        // are also in the registry (`MAF130-EXEC-001`, `MAF130-ATTR-001/002`), but a
        // syntax-only scan catches them BEFORE you run `dotnet build` — useful for
        // the auditor agent's pre-migration pass.
        new RegexRule(
            id: "MAF-AP-EXEC-001",
            name: "Pre-1.3.0 executor pattern (ReflectingExecutor / IMessageHandler / [StreamsMessage] / [YieldsMessage])",
            severity: AntiPatternSeverity.Error,
            // Matches any of the four legacy surfaces in one pass.
            pattern: new Regex(@"\bReflectingExecutor\s*<|\bIMessageHandler\s*<|\[\s*StreamsMessage\s*\]|\[\s*YieldsMessage\s*\]", RegexHygiene, RegexBudget),
            skipInTestFiles: false),
    };

    private static int FirstLineMatching(string source, string regex)
    {
        // Phase 5.9 — hygiene policy applies to scanner-internal regexes too.
        var rx = new Regex(regex, RegexHygiene, RegexBudget);
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            if (rx.IsMatch(lines[i])) return i + 1;
        return 1;
    }

    /// <summary>
    /// Returns the inclusive line ranges that lie inside a <c>#if</c> block whose
    /// condition mentions <c>DEVUI_ENABLED</c> (the canonical guard symbol per the
    /// 1.3.0 migration guide §14). Used by <c>MAF-AP-DEVUI-001</c> to suppress
    /// findings for code that IS correctly guarded.
    /// </summary>
    internal static IReadOnlyList<(int Start, int End)> CollectDevUiGuardedLineRanges(Microsoft.CodeAnalysis.SyntaxNode root)
    {
        var ranges = new List<(int Start, int End)>();
        var openStack = new Stack<int>();

        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.HasStructure)
            {
                var structure = trivia.GetStructure();
                if (structure is IfDirectiveTriviaSyntax ifDir)
                {
                    var conditionText = ifDir.Condition.ToString();
                    if (conditionText.Contains("DEVUI_ENABLED", StringComparison.Ordinal))
                    {
                        var line = ifDir.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        openStack.Push(line);
                    }
                    else
                    {
                        // A non-DevUI `#if` — push a sentinel so #endif balance is preserved.
                        openStack.Push(-1);
                    }
                }
                else if (structure is EndIfDirectiveTriviaSyntax endIf)
                {
                    if (openStack.Count == 0) continue;
                    var openLine = openStack.Pop();
                    if (openLine > 0)
                    {
                        var closeLine = endIf.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        ranges.Add((openLine, closeLine));
                    }
                }
            }
        }

        return ranges;
    }

    // -------------------------------------------------------------------------
    // Report formatting
    // -------------------------------------------------------------------------

    private static string FormatReport(string repoPath, IReadOnlyList<AntiPatternFinding> findings)
    {
        var byLevel = findings.GroupBy(f => f.Severity).ToDictionary(g => g.Key, g => g.ToList());
        var errors = byLevel.GetValueOrDefault(AntiPatternSeverity.Error, []);
        var warnings = byLevel.GetValueOrDefault(AntiPatternSeverity.Warning, []);
        var infos = byLevel.GetValueOrDefault(AntiPatternSeverity.Info, []);

        var sb = new StringBuilder();
        sb.AppendLine($"## MAF anti-pattern scan — `{repoPath}`");
        sb.AppendLine();
        sb.AppendLine($"| Severity | Count |");
        sb.AppendLine($"|---|---:|");
        sb.AppendLine($"| ❌ Error | {errors.Count} |");
        sb.AppendLine($"| ⚠️ Warning | {warnings.Count} |");
        sb.AppendLine($"| ℹ️ Info | {infos.Count} |");
        sb.AppendLine();

        AppendSection(sb, "❌ Errors (must fix)", errors);
        AppendSection(sb, "⚠️ Warnings (should fix)", warnings);
        AppendSection(sb, "ℹ️ Info (consider)", infos);

        if (findings.Count == 0)
            sb.AppendLine("✅ No anti-patterns detected. Codebase follows current MAF 1.3.0 best practices for the scanned ruleset.");

        sb.AppendLine();
        sb.AppendLine("**Rule list source:** `maf://skills?name=maf-anti-pattern-scanner`");
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string heading, IReadOnlyList<AntiPatternFinding> findings)
    {
        if (findings.Count == 0) return;
        sb.AppendLine($"### {heading}");
        sb.AppendLine();
        sb.AppendLine("| Rule | File | Line | Match |");
        sb.AppendLine("|---|---|---:|---|");
        foreach (var f in findings)
            sb.AppendLine($"| `{f.RuleId}` — {f.RuleName} | `{f.File}` | {f.Line} | `{f.Match}` |");
        sb.AppendLine();
    }
}

// ----------------------------------------------------------------------------
// Rule types
// ----------------------------------------------------------------------------

public enum AntiPatternSeverity { Error, Warning, Info }

public sealed record AntiPatternFinding(
    string RuleId,
    string RuleName,
    AntiPatternSeverity Severity,
    string File,
    int Line,
    string Match);

public abstract class AntiPatternRule
{
    public string Id { get; protected init; } = "";
    public string Name { get; protected init; } = "";
    public AntiPatternSeverity Severity { get; protected init; }
    public bool SkipInTestFiles { get; protected init; }

    public abstract IEnumerable<AntiPatternFinding> Scan(
        string source, Microsoft.CodeAnalysis.SyntaxNode root, string file);
}

internal sealed class RegexRule : AntiPatternRule
{
    private readonly Regex? _pattern;
    private readonly Func<string, Microsoft.CodeAnalysis.SyntaxNode, string, IEnumerable<AntiPatternFinding>>? _custom;

    public RegexRule(
        string id, string name, AntiPatternSeverity severity,
        Regex? pattern, bool skipInTestFiles = false,
        Func<string, Microsoft.CodeAnalysis.SyntaxNode, string, IEnumerable<AntiPatternFinding>>? customScan = null)
    {
        Id = id;
        Name = name;
        Severity = severity;
        SkipInTestFiles = skipInTestFiles;
        _pattern = pattern;
        _custom = customScan;
    }

    public override IEnumerable<AntiPatternFinding> Scan(string source, Microsoft.CodeAnalysis.SyntaxNode root, string file)
    {
        if (_custom is not null)
            return _custom(source, root, file);

        if (_pattern is null) return [];

        var findings = new List<AntiPatternFinding>();
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = _pattern.Match(lines[i]);
            if (match.Success)
                findings.Add(new AntiPatternFinding(Id, Name, Severity, file, i + 1, match.Value));
        }
        return findings;
    }
}

internal sealed class RoslynRule : AntiPatternRule
{
    private readonly Func<Microsoft.CodeAnalysis.SyntaxNode, string, IEnumerable<AntiPatternFinding>> _scan;

    public RoslynRule(
        string id, string name, AntiPatternSeverity severity,
        Func<Microsoft.CodeAnalysis.SyntaxNode, string, IEnumerable<AntiPatternFinding>> scan,
        bool skipInTestFiles = false)
    {
        Id = id;
        Name = name;
        Severity = severity;
        SkipInTestFiles = skipInTestFiles;
        _scan = scan;
    }

    public override IEnumerable<AntiPatternFinding> Scan(string _, Microsoft.CodeAnalysis.SyntaxNode root, string file)
        => _scan(root, file);
}
