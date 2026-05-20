using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using MafAutopilot.Data;
using ModelContextProtocol.Server;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: MafRunCs0618Hunt
///
/// Runs <c>dotnet build</c> on the target project, parses CS0618 (obsolete) and
/// CS0246 (type-not-found) diagnostics out of the output, and joins each diagnostic
/// to the obsolete-API registry by signature/method match. Returns a structured
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
        Run a CS0618 / CS0246 hunt against a .NET project.

        Shells `dotnet build` on the given path (.csproj or .sln), parses the diagnostics,
        and joins each warning/error to the MAF obsolete-API registry. Output includes
        the file:line, the diagnostic, and — when a registry match exists — the exact fix.

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
        var findings = ParseBuildOutput(output);
        return FormatReport(resolved, exitCode, findings, _registry);
    }

    // -------------------------------------------------------------------------
    // Pure parser core (testable without an MSBuild invocation)
    // -------------------------------------------------------------------------

    // Matches MSBuild's standard diagnostic line:
    //   <file>(line,col): warning|error CSnnnn: <message>
    //
    // Phase 5.G fixup — ReDoS hygiene. The pattern itself is bounded
    // (lazy `[^()\r\n]+?` between literal anchors), but the `<file>` slot is
    // attacker-influenced (source filenames in `dotnet build` output can
    // contain odd characters from a hostile user repo). NonBacktracking +
    // 100ms timeout cap any future drift.
    //
    // NOTE: Multiline can't combine with NonBacktracking on .NET (Multiline
    // affects `^`/`$` semantics which the regex engine handles separately
    // from backtracking). We use `Multiline` (no NonBacktracking) but still
    // attach a `MatchTimeout` — the dominant risk here is catastrophic
    // backtracking in patterns WITH backtracking, which this one cannot do.
    private static readonly Regex DiagRegex = new(
        @"^(?<file>[^()\r\n]+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<severity>warning|error)\s+(?<code>CS\d{4}):\s+(?<msg>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Parses MSBuild stdout for CS0618 / CS0246 diagnostics. Pure: no I/O.
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
            if (code is not ("CS0618" or "CS0246"))
                continue;

            var file = m.Groups["file"].Value.Trim();
            var line = int.Parse(m.Groups["line"].Value);
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

    /// <summary>
    /// Joins a build diagnostic to a registry entry. CS0618 messages have the canonical
    /// shape <c>'X.Y.Z' is obsolete: 'Use A instead.'</c> — the FIRST quoted segment is
    /// always the obsolete symbol; the second is the human-readable hint. We never search
    /// the second segment, since that points to the *replacement* and would yield false
    /// matches against unrelated registry entries.
    /// </summary>
    internal static RegistryEntry? MatchToRegistry(BuildDiagnostic diag, RegistryService registry)
    {
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

    // Phase 5.G fixup — NonBacktracking + 100ms timeout. The `[^']+` class
    // is bounded by literal anchors so backtracking is structurally limited,
    // but the pattern matches `dotnet build` diagnostic text which is
    // attacker-influenced. Hygiene is cheap; we pay it.
    private static readonly Regex FirstQuotedRegex = new(
        @"'([^']+)'",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

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
    // ever changed back. The 100ms timeout is belt-and-suspenders against future regex
    // edits or a runtime-tuning regression. We lose the ability to recognise tokens like
    // `XMLParser` (consecutive uppercase) — acceptable, as the registry haystack is the
    // primary match path; this method is the fallback for CS0618 messages that don't
    // contain a quoted symbol.
    private static readonly Regex CamelCaseIdentifierRegex = new(
        @"\b[A-Z][a-z]+(?:[A-Z][a-z]+)+\b",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

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
        RegistryService registry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## CS0618 hunt — `{target}`");
        sb.AppendLine($"Build exit code: {exitCode} · {findings.Count} CS0618/CS0246 diagnostic(s) found.");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine(exitCode == 0
                ? "✅ Build clean, zero CS0618/CS0246 diagnostics."
                : "⚠️ Build failed but no CS0618/CS0246 diagnostics were extracted — see raw build output for unrelated errors.");
            return sb.ToString();
        }

        foreach (var diag in findings)
        {
            sb.AppendLine($"### {diag.File}({diag.Line}) — {diag.Severity} {diag.Code}");
            sb.AppendLine($"> {diag.Message}");
            sb.AppendLine();

            var match = MatchToRegistry(diag, registry);
            if (match is not null)
            {
                sb.AppendLine($"**Registry match:** `{match.Id}`");
                sb.AppendLine(match.FixDescription.Trim());
                if (!string.IsNullOrWhiteSpace(match.ExampleAfter))
                {
                    sb.AppendLine();
                    sb.AppendLine("```csharp");
                    sb.AppendLine(match.ExampleAfter.Trim());
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
