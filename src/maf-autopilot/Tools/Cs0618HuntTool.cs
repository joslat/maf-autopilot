using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using MafDoctor.Data;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafRunCs0618Hunt
///
/// Runs <c>dotnet build</c> on the target project, parses C# compiler diagnostics,
/// retains the legacy CS0618/CS0246 set plus diagnostics that correlate to a
/// same-code type/member/signature in the migration registry. Returns a structured
/// markdown report with file:line, warning, and the deterministic registry-anchored
/// fix when one is known.
///
/// Why this lives in the MCP server (not as a "skill the LLM runs by hand"):
/// (1) shelling `dotnet build` and parsing its output deterministically is exactly
/// the kind of work that benefits from a tool call vs. asking the model to
/// orchestrate it; (2) the registry-join logic stays out of the prompt.
/// </summary>
[McpServerToolType]
public sealed class Cs0618HuntTool
{
    private readonly RegistryService _registry;

    public Cs0618HuntTool(RegistryService registry)
    {
        _registry = registry;
    }

    // Annotation rationale (see docs/security.md, threat-model.md §3.5):
    //   ReadOnly  = false  → `dotnet build` executes user-supplied MSBuild targets
    //                        (BeforeBuild, Exec tasks, source generators, NuGet
    //                        restore scripts). This is code execution, not a read.
    //                        Setting ReadOnly = true would let MCP-spec-compliant
    //                        clients auto-invoke the tool without user consent —
    //                        the exact opposite of what's safe.
    //   OpenWorld = true   → `dotnet build` triggers NuGet restore, which reaches
    //                        api.nuget.org (and any configured private feeds).
    //   Destructive = false → the tool itself does not write to disk; the build's
    //                        side-effects (obj/, bin/) are scoped to the project.
    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("""
        Run a registry-aware C# migration-diagnostic hunt against a .NET project.

        Shells `dotnet build` on the given path (.csproj or .sln), parses all C#
        diagnostics, and retains CS0618, CS0246, plus same-code diagnostics whose
        message names a type/member/signature represented in the MAF migration registry.
        Output includes file:line, the diagnostic, and — when a match exists — the exact fix.

        Input:
          - Path to a .csproj or .sln file. If the path is a directory, the first .csproj
            or .sln inside it is used.

        Security note: this tool spawns `dotnet build`, which compiles and executes the
        target project's MSBuild targets, source generators, and NuGet restore scripts.
        Do not invoke on untrusted repositories. Reaches the network via NuGet restore.

        Cost note: this runs `dotnet build`, which may take several seconds on a real project.
        """)]
    public string MafRunCs0618Hunt(
        [Description("Path to .csproj, .sln, or a directory containing one.")] string projectPath)
    {
        // projectPath can be a file (.csproj/.sln) OR a directory containing one,
        // so PathGuard's directory-existence check would be too strict. Apply only
        // the character + traversal checks here.
        if (string.IsNullOrWhiteSpace(projectPath))
            return "Error: projectPath must not be empty.";
        if (projectPath.IndexOfAny(['\0', '\r', '\n', '"', ';', '|', '&']) >= 0)
            return "Error: projectPath contains invalid characters.";
        if (projectPath.Replace('\\', '/').Split('/').Any(s => s == ".."))
            return "Error: projectPath must not contain '..' segments (path traversal).";

        var resolved = ResolveBuildTarget(projectPath);
        if (resolved is null)
            return $"Error: could not find a .csproj or .sln at '{projectPath}'.";

        var (exitCode, output) = ProcessRunner.RunDotnetBuild(resolved);
        var diagnostics = ParseBuildOutput(output);
        var findings = FilterRegistryRelevantDiagnostics(diagnostics, _registry.AllEntries);
        return FormatReport(resolved, exitCode, findings, _registry, output);
    }

    /// <summary>REP-16: the last <paramref name="n"/> lines of build output, for the fenced excerpt.</summary>
    private static string TailLines(string text, int n)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var start = Math.Max(0, lines.Length - n);
        return string.Join("\n", lines[start..]);
    }

    // -------------------------------------------------------------------------
    // Pure parser core (testable without an MSBuild invocation)
    // -------------------------------------------------------------------------

    // Matches MSBuild's standard diagnostic line:
    //   <file>(line,col): warning|error CSnnnn: <message>
    //
    // Phase 5.G / 7.G fixup — ReDoS hygiene. The pattern is bounded (lazy
    // `[^()\r\n]+?` between literal anchors) and `<file>` is attacker-
    // influenced (source filenames in `dotnet build` output can contain
    // odd characters from a hostile user repo). .NET 7+ allows
    // `NonBacktracking | Multiline` to combine cleanly (the prior comment
    // suggesting otherwise was wrong); we now apply both for consistency
    // with the project-wide invariant.
    private static readonly Regex DiagRegex = new(
        @"^(?<file>[^()\r\n]+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<severity>warning|error)\s+(?<code>CS\d{4}):\s+(?<msg>.+)$",
        RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.Multiline,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// Parses all C# compiler diagnostics from MSBuild stdout. Pure: no I/O.
    /// Call <see cref="FilterRegistryRelevantDiagnostics"/> before reporting them.
    /// </summary>
    public static IReadOnlyList<BuildDiagnostic> ParseBuildOutput(string buildOutput)
    {
        if (string.IsNullOrWhiteSpace(buildOutput))
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var diagnostics = new List<BuildDiagnostic>();

        foreach (Match m in DiagRegex.Matches(buildOutput))
        {
            var code = m.Groups["code"].Value;

            var file = m.Groups["file"].Value.Trim();
            // WM-38: a pathological/oversized line number must not overflow int.Parse and
            // crash the hunt after paying the full build cost — skip the malformed line.
            if (!int.TryParse(m.Groups["line"].Value, out var line))
                continue;
            var severity = m.Groups["severity"].Value;
            var msg = m.Groups["msg"].Value.Trim();

            // De-duplicate: MSBuild often emits the same warning once per target framework
            var key = $"{file}({line}):{code}:{msg}";
            if (!seen.Add(key))
                continue;

            diagnostics.Add(new BuildDiagnostic(
                File: file,
                Line: line,
                Severity: severity,
                Code: code,
                Message: msg));
        }

        return diagnostics;
    }

    private static readonly Regex CsCodeRegex = new(
        @"^CS[0-9]{4}$",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(2));

    private static readonly HashSet<string> LegacyDiagnosticCodes = new(
        ["CS0618", "CS0246"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly Regex RegistrySymbolRegex = new(
        @"[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(2));

    // Tokens that describe C# syntax, BCL containers, or namespace scaffolding rather
    // than the changed MAF API. Letting these correlate a generic CS1503 would recreate
    // the registry-wide false-positive bug under a different name.
    private static readonly HashSet<string> NonDistinctiveRegistrySymbols = new(
        [
            "abstract", "async", "base", "bool", "byte", "char", "class",
            "decimal", "double", "enum", "false", "float", "get", "in", "init",
            "int", "interface", "internal", "long", "new", "null", "object", "out",
            "override", "params", "private", "protected", "public", "readonly",
            "record", "ref", "return", "sbyte", "sealed", "set", "short", "static",
            "string", "struct", "this", "true", "uint", "ulong", "ushort", "var",
            "virtual", "void", "where",
            "Action", "CancellationToken", "Collections", "Dictionary", "Extensions",
            "Func", "Generic", "IEnumerable", "IList", "IReadOnlyCollection",
            "IReadOnlyList", "List", "Microsoft", "Nullable", "SDK", "System", "Task",
            "ValueTask",
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Bounds hunt output to the two historical migration diagnostics plus new
    /// diagnostic codes that correlate to a same-code registry type/member/signature.
    /// Code membership alone is insufficient: CS1503 and similar diagnostics are common
    /// in unrelated application code and must not inflate MAF migration finding counts.
    /// </summary>
    internal static IReadOnlyList<BuildDiagnostic> FilterRegistryRelevantDiagnostics(
        IEnumerable<BuildDiagnostic> diagnostics,
        IEnumerable<RegistryEntry> registryEntries)
    {
        var entriesByCode = registryEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.CsWarning)
                && CsCodeRegex.IsMatch(e.CsWarning.Trim()))
            .GroupBy(e => e.CsWarning.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return diagnostics.Where(d =>
        {
            if (LegacyDiagnosticCodes.Contains(d.Code))
                return true;
            return entriesByCode.TryGetValue(d.Code, out var sameCodeEntries)
                && sameCodeEntries.Any(e => DiagnosticCorrelatesWithRegistryEntry(d, e));
        }).ToList();
    }

    /// <summary>
    /// Requires both diagnostic-code equality and an identifier-level match against the
    /// registry's structured API fields. Replacement signatures are included because
    /// conversion diagnostics commonly name both the old and new parameter types while
    /// omitting the containing method (for example AITool → AIFunctionDeclaration).
    /// </summary>
    internal static bool DiagnosticCorrelatesWithRegistryEntry(
        BuildDiagnostic diagnostic,
        RegistryEntry entry)
    {
        if (!(entry.CsWarning?.Trim().Equals(
                diagnostic.Code, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        foreach (var field in new[]
        {
            entry.Type,
            entry.Method,
            entry.ObsoleteSignature,
            entry.ReplacementSignature,
        })
        {
            if (string.IsNullOrWhiteSpace(field))
                continue;
            foreach (Match match in RegistrySymbolRegex.Matches(field))
            {
                var symbol = match.Value;
                if (!IsDistinctiveRegistrySymbol(symbol))
                    continue;
                if (ContainsIdentifier(diagnostic.Message, symbol))
                    return true;
            }
        }
        return false;
    }

    private static bool IsDistinctiveRegistrySymbol(string symbol) =>
        symbol.Length >= 3
        && symbol.Any(char.IsUpper)
        && !NonDistinctiveRegistrySymbols.Contains(symbol);

    private static bool ContainsIdentifier(string message, string symbol)
    {
        var searchStart = 0;
        while (searchStart < message.Length)
        {
            var index = message.IndexOf(symbol, searchStart, StringComparison.Ordinal);
            if (index < 0)
                return false;
            var beforeIsIdentifier = index > 0 && IsIdentifierChar(message[index - 1]);
            var afterIndex = index + symbol.Length;
            var afterIsIdentifier = afterIndex < message.Length
                && IsIdentifierChar(message[afterIndex]);
            if (!beforeIsIdentifier && !afterIsIdentifier)
                return true;
            searchStart = index + 1;
        }
        return false;
    }

    private static bool IsIdentifierChar(char value) =>
        char.IsAsciiLetterOrDigit(value) || value == '_';

    /// <summary>
    /// Joins a build diagnostic to a registry entry. CS0618 messages have the canonical
    /// shape <c>'X.Y.Z' is obsolete: 'Use A instead.'</c> — the FIRST quoted segment is
    /// always the obsolete symbol; the second is the human-readable hint. We never search
    /// the second segment, since that points to the *replacement* and would yield false
    /// matches against unrelated registry entries.
    /// </summary>
    internal static RegistryEntry? MatchToRegistry(BuildDiagnostic diag, RegistryService registry)
    {
        if (!LegacyDiagnosticCodes.Contains(diag.Code))
        {
            return registry.AllEntries.FirstOrDefault(entry =>
                DiagnosticCorrelatesWithRegistryEntry(diag, entry));
        }

        var obsoleteSymbol = ExtractObsoleteSymbol(diag.Message);
        if (obsoleteSymbol is not null)
        {
            var matches = registry.SearchByApiName(obsoleteSymbol);
            if (matches.Count == 1) return matches[0];
            if (matches.Count > 1)
                return matches.FirstOrDefault(e => e.CsWarning.Equals(diag.Code, StringComparison.OrdinalIgnoreCase))
                    ?? matches[0];
        }

        // Fallback: try CamelCase identifiers that look like .NET types/members.
        foreach (var token in ExtractCamelCaseIdentifiers(diag.Message))
        {
            var matches = registry.SearchByApiName(token);
            if (matches.Count == 1) return matches[0];
        }
        return null;
    }

    // Phase 5.G fixup — NonBacktracking + 2s timeout. The `[^']+` class
    // is bounded by literal anchors so backtracking is structurally limited,
    // but the pattern matches `dotnet build` diagnostic text which is
    // attacker-influenced. Hygiene is cheap; we pay it.
    private static readonly Regex FirstQuotedRegex = new(
        @"'([^']+)'",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(2));

    internal static string? ExtractObsoleteSymbol(string message)
    {
        // CS0246: "The type or namespace name 'X' could not be found ..."
        // CS0618: "'X' is obsolete: '<hint>'"
        // In both cases the FIRST quoted span is the symbol of interest.
        var m = FirstQuotedRegex.Match(message);
        return m.Success ? m.Groups[1].Value : null;
    }

    // Security: the original pattern `\b[A-Z][a-zA-Z]+(?:[A-Z][a-zA-Z]+)+\b` had a nested
    // quantifier whose inner `[a-zA-Z]+` overlapped with the outer `[A-Z]` — classic
    // catastrophic backtracking. Measured on .NET 9: input `Aa…0` (n=30) took 9 seconds.
    // Fix is twofold: (1) the inner class is narrowed to `[a-z]+` to eliminate overlap;
    // (2) `RegexOptions.NonBacktracking` guarantees linear time even if the pattern is
    // ever changed back. The 2s timeout is belt-and-suspenders against future regex
    // edits or a runtime-tuning regression. We lose the ability to recognise tokens like
    // `XMLParser` (consecutive uppercase) — acceptable, as the registry haystack is the
    // primary match path; this method is the fallback for CS0618 messages that don't
    // contain a quoted symbol.
    private static readonly Regex CamelCaseIdentifierRegex = new(
        @"\b[A-Z][a-z]+(?:[A-Z][a-z]+)+\b",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(2));

    private static IEnumerable<string> ExtractCamelCaseIdentifiers(string message) =>
        CamelCaseIdentifierRegex.Matches(message).Select(m => m.Value);

    // -------------------------------------------------------------------------
    // Build-target resolution
    // -------------------------------------------------------------------------

    internal static string? ResolveBuildTarget(string path)
    {
        // Security: an LLM-supplied `path` of `C:\\`, `/`, or any other filesystem root
        // would otherwise pass the partial guard above and cause Directory.GetFiles to
        // scan the drive root for any *.sln / *.csproj. If one exists, `dotnet build`
        // then runs against it under the developer's identity — including any malicious
        // pre-build `<Target>` element. Reject root paths up-front.
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root)
            && string.Equals(fullPath.TrimEnd('\\', '/'),
                             root.TrimEnd('\\', '/'),
                             StringComparison.OrdinalIgnoreCase))
            return null;

        if (File.Exists(path)
            && (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
            return path;

        if (Directory.Exists(path))
        {
            var sln = Directory.GetFiles(path, "*.sln").FirstOrDefault();
            if (sln is not null) return sln;
            var csproj = Directory.GetFiles(path, "*.csproj").FirstOrDefault();
            if (csproj is not null) return csproj;
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Output formatting
    // -------------------------------------------------------------------------

    private static string FormatReport(
        string target, int exitCode,
        IReadOnlyList<BuildDiagnostic> findings,
        RegistryService registry,
        string buildOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## CS0618 hunt — `{target}`");
        sb.AppendLine($"Build exit code: {exitCode} · {findings.Count} registry-relevant C# migration diagnostic(s) found.");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            if (exitCode == 0)
            {
                sb.AppendLine("✅ Build clean, zero registry-relevant C# migration diagnostics.");
                return sb.ToString();
            }
            // REP-16: the tool used to tell the agent to "see raw build output" it never
            // returned. A failed build with no extracted registry-relevant diagnostic is only diagnosable
            // if we actually include the output — bounded + fenced (untrusted build text).
            sb.AppendLine("⚠️ Build failed but no registry-relevant C# migration diagnostics were extracted — the errors below are outside the registry's diagnostic vocabulary (or a build-config problem).");
            var tail = TailLines(buildOutput, 100);
            if (!string.IsNullOrWhiteSpace(tail))
            {
                sb.AppendLine();
                sb.AppendLine("**Build output (last lines):**");
                sb.Append(LlmFencing.Fence("dotnet-build-output", tail, maxBytes: 16 * 1024));
            }
            return sb.ToString();
        }

        foreach (var diag in findings)
        {
            // SEC-06: diag.File / diag.Message are MSBuild-emitted text derived from source
            // paths / member names — neutralize before echoing into the markdown report.
            sb.AppendLine($"### {LlmFencing.MdInline(diag.File)}({diag.Line}) — {diag.Severity} {diag.Code}");
            sb.AppendLine($"> {LlmFencing.StripHtmlComments(diag.Message)}");
            sb.AppendLine();

            var match = MatchToRegistry(diag, registry);
            if (match is not null)
            {
                sb.AppendLine($"**Registry match:** `{match.Id}`");
                sb.AppendLine(LlmFencing.StripHtmlComments(match.FixDescription.Trim()));
                if (!string.IsNullOrWhiteSpace(match.ExampleAfter))
                {
                    sb.AppendLine();
                    sb.AppendLine("```csharp");
                    // SEC-06: a ``` inside the registry example would close the fence.
                    sb.AppendLine(MafDoctor.Data.RegistryService.NeutralizeFences(match.ExampleAfter.Trim()));
                    sb.AppendLine("```");
                }
            }
            else
            {
                sb.AppendLine("_No registry match — escalate via `maf-migration-retrospective` skill so a new entry can be added._");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public sealed record BuildDiagnostic(
    string File,
    int Line,
    string Severity,
    string Code,
    string Message);
