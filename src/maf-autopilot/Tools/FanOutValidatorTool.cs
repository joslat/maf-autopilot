using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: MafValidateFanOut
///
/// Detects the silent fan-in starvation pattern at the source level. A fan-out
/// executor's `[MessageHandler]` method MUST return <c>ValueTask&lt;T&gt;</c> or
/// <c>Task&lt;T&gt;</c> where T is the downstream message type. A handler that
/// returns <c>void</c>, <c>Task</c>, or <c>ValueTask</c> (non-generic) produces NO
/// output message — the fan-in barrier then starves silently. This is NOT a build
/// error; only static analysis or runtime tracing can catch it.
/// </summary>
[McpServerToolType]
public sealed class FanOutValidatorTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Validate fan-out/fan-in executor topology at the source level.

        Scans .cs files for [MessageHandler] methods and reports any whose return type
        cannot produce a downstream message (void, Task, ValueTask non-generic) —
        the silent fan-in starvation pattern.

        Input:
          - path: a file (.cs) or a directory.
          - format: "markdown" (default) for a human report, or "sarif" for a SARIF v2.1.0
            document suitable for GitHub Advanced Security ingestion. SARIF mode flags
            both `SilentStarvationRisk` and `LikelyInvalid` as MAF001 errors.

        Returns a markdown report with per-file findings, OR a SARIF document.
        """)]
    public string MafValidateFanOut(
        [Description("Path to a .cs file or a directory containing .cs files.")] string path,
        [Description("Output format: \"markdown\" (default) or \"sarif\" (v2.1.0 JSON).")] string format = "markdown")
    {
        // PathGuard checks `..` traversal + invalid chars + existence — but it only
        // accepts directories. FanOutValidator additionally supports single-file paths,
        // so we apply a relaxed-existence check here.
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path must not be empty.";
        if (path.IndexOfAny(['\0', '\r', '\n', '"', ';', '|', '&']) >= 0)
            return "Error: path contains invalid characters.";
        if (path.Replace('\\', '/').Split('/').Any(s => s == ".."))
            return "Error: path must not contain '..' segments (path traversal).";

        var files = ResolveFiles(path);
        if (files.Count == 0)
            return $"Error: no .cs files found at '{path}'.";

        var allFindings = new List<MessageHandlerFinding>();
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            allFindings.AddRange(AnalyzeSource(source, file));
        }

        return format.Equals("sarif", StringComparison.OrdinalIgnoreCase)
            ? SarifExportTool.EmitFanOutSarif(allFindings)
            : FormatReport(allFindings);
    }

    // -------------------------------------------------------------------------
    // Pure-function core (testable without files)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks the given C# source and reports every method annotated with
    /// <c>[MessageHandler]</c>. Pure: no file I/O. The fileName is echoed back
    /// into each finding for grouping.
    /// </summary>
    public static IReadOnlyList<MessageHandlerFinding> AnalyzeSource(string source, string fileName = "<inline>")
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var findings = new List<MessageHandlerFinding>();

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!HasMessageHandlerAttribute(method))
                continue;

            var returnType = method.ReturnType.ToString();
            var verdict = ClassifyReturnType(returnType);
            var location = method.GetLocation().GetLineSpan();

            findings.Add(new MessageHandlerFinding(
                File: fileName,
                Line: location.StartLinePosition.Line + 1,
                MethodName: method.Identifier.ValueText,
                ReturnType: returnType,
                Verdict: verdict));
        }

        return findings;
    }

    internal static bool HasMessageHandlerAttribute(MethodDeclarationSyntax method) =>
        method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr =>
            {
                // Attribute name may appear as `MessageHandler` or `MessageHandlerAttribute`,
                // optionally qualified (e.g., `Microsoft.Agents.AI.MessageHandler`).
                var name = attr.Name.ToString();
                var simpleName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
                return simpleName is "MessageHandler" or "MessageHandlerAttribute";
            });

    /// <summary>
    /// Classifies a handler return type. Generic <c>Task&lt;T&gt;</c> / <c>ValueTask&lt;T&gt;</c>
    /// /<c>IAsyncEnumerable&lt;T&gt;</c> are OK; non-generic <c>Task</c> / <c>ValueTask</c>
    /// and <c>void</c> are the silent-starvation risk; anything else (e.g., a raw
    /// concrete type, primitive, or non-awaitable) is <see cref="FanOutVerdict.LikelyInvalid"/>
    /// — MAF cannot fan-out from such a handler.
    /// </summary>
    internal static FanOutVerdict ClassifyReturnType(string returnType)
    {
        var trimmed = returnType.Trim();
        if (trimmed == "void")
            return FanOutVerdict.SilentStarvationRisk;

        // Strip any qualification (System.Threading.Tasks.Task → Task)
        var simple = trimmed.Contains('.') ? trimmed[(trimmed.LastIndexOf('.') + 1)..] : trimmed;

        // Generic Task<T> / ValueTask<T> — safe (returns a downstream message).
        if (simple.StartsWith("Task<", StringComparison.Ordinal)
            || simple.StartsWith("ValueTask<", StringComparison.Ordinal))
            return FanOutVerdict.Ok;

        // IAsyncEnumerable<T> — legitimate streaming pattern, safe.
        if (simple.StartsWith("IAsyncEnumerable<", StringComparison.Ordinal))
            return FanOutVerdict.Ok;

        // Non-generic Task or ValueTask — produces no message, starves the fan-in.
        if (simple is "Task" or "ValueTask")
            return FanOutVerdict.SilentStarvationRisk;

        // Anything else (concrete message type, primitive, etc.) cannot satisfy
        // MAF's fan-out contract — almost certainly a bug.
        return FanOutVerdict.LikelyInvalid;
    }

    private static IReadOnlyList<string> ResolveFiles(string path)
    {
        if (File.Exists(path) && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return new[] { path };
        if (Directory.Exists(path))
        {
            // Phase 5.G fixup — migrated from Directory.GetFiles(..., SearchOption.AllDirectories)
            // (default options follow symlinks) to SourceFileWalker.EnumerateCsFiles which sets
            // AttributesToSkip = ReparsePoint and also filters /bin/ + /obj/ noise paths.
            return SourceFileWalker.EnumerateCsFiles(path).ToList();
        }
        return Array.Empty<string>();
    }

    private static string FormatReport(IReadOnlyList<MessageHandlerFinding> findings)
    {
        if (findings.Count == 0)
            return "✅ No `[MessageHandler]` methods found at the given path.";

        var risks = findings.Where(f => f.Verdict == FanOutVerdict.SilentStarvationRisk).ToList();
        var invalid = findings.Where(f => f.Verdict == FanOutVerdict.LikelyInvalid).ToList();
        var ok = findings.Where(f => f.Verdict == FanOutVerdict.Ok).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"## Fan-out validator — {findings.Count} `[MessageHandler]` method(s) inspected");
        sb.AppendLine();

        if (risks.Count > 0)
        {
            sb.AppendLine($"### ❌ {risks.Count} SILENT STARVATION RISK");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Method | Return Type |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var f in risks)
                sb.AppendLine($"| {f.File} | {f.Line} | `{f.MethodName}` | `{f.ReturnType}` |");
            sb.AppendLine();
            sb.AppendLine("**Fix:** change the return type to `ValueTask<T>` (or `Task<T>`) where T is the downstream message type, and ensure the handler `return`s a value. See `maf://skills?name=maf-fan-out-validator`.");
            sb.AppendLine();
        }

        if (invalid.Count > 0)
        {
            sb.AppendLine($"### ❌ {invalid.Count} likely invalid return type — handler cannot satisfy MAF's fan-out contract");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Method | Return Type |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var f in invalid)
                sb.AppendLine($"| {f.File} | {f.Line} | `{f.MethodName}` | `{f.ReturnType}` |");
            sb.AppendLine();
        }

        sb.AppendLine($"### ✅ {ok.Count} OK");
        if (ok.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("| File | Line | Method | Return Type |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var f in ok)
                sb.AppendLine($"| {f.File} | {f.Line} | `{f.MethodName}` | `{f.ReturnType}` |");
        }

        return sb.ToString();
    }
}

public enum FanOutVerdict
{
    Ok,
    SilentStarvationRisk,
    LikelyInvalid,
}

public sealed record MessageHandlerFinding(
    string File,
    int Line,
    string MethodName,
    string ReturnType,
    FanOutVerdict Verdict);
