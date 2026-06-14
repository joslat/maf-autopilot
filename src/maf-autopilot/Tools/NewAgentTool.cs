using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using MafDoctor.Scaffolding;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tools: MafNewAgent and MafNewExecutor
///
/// Greenfield magic — generates clean MAF 1.3.0 code from templates that
/// pass the anti-pattern scanner and fan-out validator out of the box.
/// </summary>
[McpServerToolType]
public sealed class NewAgentTool
{
    // Annotation rationale (see docs/security.md, CONTRIBUTING.md security rubric):
    //   Destructive = true → writes new files into the user's project tree.
    //                        Idempotent skip-on-exist does not make the act
    //                        non-destructive; the contract communicates
    //                        "this changes the workspace state."
    //   Idempotent  = true → second invocation with same args is a no-op (skip-on-exist).
    //   OpenWorld   = false → purely local: no network calls.
    [McpServerTool(Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Generate a new MAF 1.3.0 agent class + xUnit smoke test.

        Creates `Agents/<Name>.cs` and `Tests/<Name>Tests.cs` under the target
        project directory. The generated agent:
          - uses `ChatClientAgent` (not the legacy `AIAgent` base)
          - wires `UseOpenTelemetry` on the IChatClient
          - sets `MaxOutputTokens = 1024` so the cost auditor is happy
          - puts Instructions inside `ChatOptions` (the correct 1.3.0 placement)
          - includes a hermetic test using a `ScriptedChatClient` mock — zero LLM cost

        Input:
          - projectPath: absolute path to an existing project directory (must contain a .csproj).
          - agentName: PascalCase identifier, e.g. "ChatBot" or "FraudReviewer".
          - instructions: optional system-prompt text. Defaults to a generic helpful-assistant prompt.

        Returns a markdown summary listing the files written and any anti-pattern
        scan results against the generated code (should always be clean — if it isn't,
        the generator has a bug).
        """)]
    public string MafNewAgent(
        [Description("Absolute path to an existing project directory (must contain a .csproj).")] string projectPath,
        [Description("PascalCase agent name, e.g. ChatBot.")] string agentName,
        [Description("Optional system prompt for the agent's Instructions. Defaults to a generic helpful-assistant prompt.")] string? instructions = null)
    {
        var pathError = ValidateProjectPath(projectPath);
        if (pathError is not null) return pathError;
        if (!IsSafeIdentifier(agentName))
            return $"Error: '{agentName}' is not a valid PascalCase identifier.";

        // Phase 5.1 — length caps. agentName is already constrained by
        // IsSafeIdentifier (chars+length) so we cap defensively to identifier
        // class. `instructions` is the high-value DoS lane — defaults to a
        // 64 KB system-prompt class cap.
        try
        {
            BoundedInput.Validate(agentName,    BoundedInput.IdentifierBytes,   nameof(agentName));
            BoundedInput.Validate(instructions, BoundedInput.InstructionsBytes, nameof(instructions));
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        var ns = DetectNamespace(projectPath, fallback: "MyApp.Agents");
        IReadOnlyList<AgentScaffolder.ScaffoldedFile> files;
        try
        {
            files = AgentScaffolder.ScaffoldAgent(agentName, ns, instructions);
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        return WriteAndReport(projectPath, files, $"Generated MAF agent: {agentName}");
    }

    // Annotation rationale: same as MafNewAgent above — writes new files into
    // the user's project tree; idempotent on second call; no network.
    [McpServerTool(Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Generate a new MAF 1.3.0 workflow executor + xUnit smoke test.

        Creates `Workflows/<Name>Executor.cs` and `Tests/<Name>ExecutorTests.cs`.
        The generated executor:
          - is a `sealed partial class : Executor` (correct 1.3.0 shape)
          - has a `[MessageHandler]` returning `Task<TOutput>` (fan-out-safe)
          - includes a reflection-based smoke test that fails at build time if the
            return type ever regresses to void / non-generic Task — catches the
            silent fan-in starvation pattern early.

        Input:
          - projectPath: absolute path to an existing project directory.
          - executorName: PascalCase, e.g. "FraudReviewer". `Executor` suffix
            is appended automatically if absent.
          - inputType: C# type name for the incoming message (default: "string").
          - outputType: C# type name for the outgoing message (default: "string").
        """)]
    public string MafNewExecutor(
        [Description("Absolute path to an existing project directory.")] string projectPath,
        [Description("PascalCase executor name, e.g. FraudReviewer.")] string executorName,
        [Description("Incoming message type (e.g. string, FraudCheckRequest). Default: string.")] string inputType = "string",
        [Description("Outgoing message type (e.g. string, FraudReport). Default: string.")] string outputType = "string")
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return "Error: projectPath must not be empty.";
        if (!Directory.Exists(projectPath))
            return $"Error: directory does not exist: '{projectPath}'.";
        if (!IsSafeIdentifier(executorName))
            return $"Error: '{executorName}' is not a valid PascalCase identifier.";

        // Phase 5.1 — length caps. inputType/outputType already capped at 200 B
        // by AgentScaffolder.IsValidTypeExpression; defensive 256 B cap matches
        // the identifier class. executorName same.
        try
        {
            BoundedInput.Validate(executorName, BoundedInput.IdentifierBytes, nameof(executorName));
            BoundedInput.Validate(inputType,    BoundedInput.IdentifierBytes, nameof(inputType));
            BoundedInput.Validate(outputType,   BoundedInput.IdentifierBytes, nameof(outputType));
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        var ns = DetectNamespace(projectPath, fallback: "MyApp.Workflows");
        IReadOnlyList<AgentScaffolder.ScaffoldedFile> files;
        try
        {
            files = AgentScaffolder.ScaffoldExecutor(executorName, ns, inputType, outputType);
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        return WriteAndReport(projectPath, files, $"Generated MAF executor: {executorName}");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Phase 7.G fixup — regex hygiene applied to all Regex declarations.
    // NamespaceRegex matches user-controlled .csproj content; the bounded
    // `[^<]+` class is backtracking-safe but the policy requires
    // NonBacktracking + 100ms timeout regardless.
    private static readonly Regex IdentifierRegex = new(
        @"^[A-Za-z][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex NamespaceRegex = new(
        @"<RootNamespace>([^<]+)</RootNamespace>",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    internal static bool IsSafeIdentifier(string s) =>
        !string.IsNullOrWhiteSpace(s) && IdentifierRegex.IsMatch(s);

    /// <summary>
    /// Validates a project-path argument coming from the LLM. Returns an error
    /// message if invalid, or null when safe. Blocks: empty/whitespace,
    /// control characters / shell metacharacters, segment-level <c>..</c>
    /// (path traversal), and non-existent directories.
    /// </summary>
    /// <summary>
    /// Replaces characters that aren't valid in C# namespace segments with `_`.
    /// Preserves dots (so `My.App.csproj` stays `My.App`).
    /// </summary>
    internal static string SanitiseNamespace(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
            sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '_');
        // C# namespace segments must start with a letter or '_'.
        if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    /// <summary>
    /// True if every dot-separated segment is a valid C# identifier.
    /// </summary>
    internal static bool IsValidNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns)) return false;
        foreach (var segment in ns.Split('.'))
            if (!IsSafeIdentifier(segment)) return false;
        return true;
    }

    internal static string? ValidateProjectPath(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return "Error: projectPath must not be empty.";
        if (projectPath.IndexOfAny(['\0', '\r', '\n', '"', ';', '|', '&']) >= 0)
            return "Error: projectPath contains invalid characters.";

        // Block `..` as a path segment. We compare against directory separators on
        // both Windows ('\\') and POSIX ('/') so the check is OS-independent.
        var normalized = projectPath.Replace('\\', '/');
        foreach (var segment in normalized.Split('/'))
            if (segment == "..")
                return "Error: projectPath must not contain '..' segments (path traversal).";

        if (!Directory.Exists(projectPath))
            return $"Error: directory does not exist: '{projectPath}'.";
        return null;
    }

    /// <summary>
    /// Looks for &lt;RootNamespace&gt; in the first .csproj in the directory.
    /// Falls back to the directory name (sanitized) or the provided fallback.
    /// </summary>
    internal static string DetectNamespace(string projectPath, string fallback)
    {
        try
        {
            var csproj = Directory.GetFiles(projectPath, "*.csproj").FirstOrDefault();
            if (csproj is not null)
            {
                var content = File.ReadAllText(csproj);
                var match = NamespaceRegex.Match(content);
                if (match.Success)
                {
                    // Trim+validate before returning. The XML capture may include
                    // surrounding whitespace (e.g. `<RootNamespace>My App </RootNamespace>`
                    // from a formatter). Post-Phase-1.3 AgentScaffolder.IsValidNamespace
                    // rejects whitespace, so we MUST sanitise here or the scaffold
                    // call throws. Fall through to the filename fallback on rejection.
                    var rawNs = match.Groups[1].Value.Trim();
                    if (IsValidNamespace(rawNs)) return rawNs;
                }

                // Fall back to the csproj filename (typically project name). Sanitise
                // hyphens and other invalid namespace characters BEFORE returning, so
                // that `MyProject-Server.csproj` becomes a valid `MyProject_Server`
                // namespace instead of an invalid one.
                var rawName = Path.GetFileNameWithoutExtension(csproj);
                var sanitised = SanitiseNamespace(rawName);
                if (IsValidNamespace(sanitised)) return sanitised;
            }
        }
        catch
        {
            // best-effort namespace detection; never fail the scaffold
        }
        return fallback;
    }

    private static string WriteAndReport(
        string projectPath,
        IReadOnlyList<AgentScaffolder.ScaffoldedFile> files,
        string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## ✨ {title}");
        sb.AppendLine();

        foreach (var file in files)
        {
            var absolute = Path.Combine(projectPath, file.RelativePath);
            var dir = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(absolute))
            {
                sb.AppendLine($"- ⚠️ `{file.RelativePath}` already exists — skipped (delete and re-run to overwrite).");
                continue;
            }

            File.WriteAllText(absolute, file.Content);
            sb.AppendLine($"- ✅ `{file.RelativePath}` ({file.Content.Split('\n').Length} lines)");
        }

        sb.AppendLine();
        sb.AppendLine("**Next steps:**");
        sb.AppendLine("1. Wire DI: register `IChatClient` and your new class in `Program.cs`.");
        sb.AppendLine("2. Run `dotnet build` then `dotnet test`.");
        sb.AppendLine("3. Optional: run `MafScanAntiPatterns` and `MafValidateFanOut` against the project to confirm clean.");
        return sb.ToString();
    }
}
