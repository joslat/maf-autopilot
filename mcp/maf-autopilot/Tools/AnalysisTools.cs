using MafAutopilot.Data;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace MafAutopilot.Tools;

/// <summary>
/// Full analysis tools — static code analysis, subprocess invocations, and compatibility lookup.
///
///   maf_health_check         — one-shot comprehensive health report (best practices + migration)
///   maf_detect_cs0618        — run dotnet build, parse and cross-reference CS0618 warnings
///   maf_api_diff             — invoke dnx dotnet-inspect diff between two MAF versions
///   maf_fan_out_validate     — search for fan-out executors with non-generic ValueTask handlers
///   maf_executor_pattern_check — search for pre-1.3.0 executor patterns
///   maf_compatibility        — return compatibility info for a MAF version
///   maf_migration_path       — return migration steps between two MAF versions
/// </summary>
[McpServerToolType]
public sealed class AnalysisTools
{
    private readonly RegistryService _registry;

    public AnalysisTools(RegistryService registry)
    {
        _registry = registry;
    }

    // -------------------------------------------------------------------------
    // maf_detect_cs0618
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_detect_cs0618")]
    [Description("""
        Run `dotnet build` on the specified project or solution and parse all CS0618 obsolete
        API warnings. Each warning is cross-referenced against the MAF registry to show the
        exact fix pattern.

        Returns a structured report with:
          - File, line, and column for each warning
          - The obsolete member name
          - The registered fix (if in registry) or the compiler's own replacement hint
          - Total warning count

        Equivalent to: dotnet build 2>&1 | Select-String "warning CS0618|error CS0246"
        """)]
    public string MafDetectCs0618(
        [Description("Path to the .sln solution file or .csproj project file.")] string projectPath)
    {
        if (!File.Exists(projectPath))
            return $"Error: File not found: {projectPath}";

        var workDir = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;

        var (output, exitCode) = RunProcess("dotnet", $"build \"{projectPath}\" --nologo -v minimal", workDir);
        var allOutput = output;

        var cs0618Lines = allOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Contains("CS0618") || l.Contains("CS0246"))
            .Select(l => l.Trim())
            .ToList();

        if (cs0618Lines.Count == 0)
        {
            return $"""
                ✅ No CS0618 or CS0246 warnings found.

                Build exit code: {exitCode}
                Project: {projectPath}

                The build is clean for obsolete and removed APIs.
                """;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## CS0618 / CS0246 Warnings: {cs0618Lines.Count} found");
        sb.AppendLine();
        sb.AppendLine($"Project: `{projectPath}`");
        sb.AppendLine();

        foreach (var line in cs0618Lines)
        {
            sb.AppendLine($"```");
            sb.AppendLine(line);
            sb.AppendLine($"```");

            // Try to extract the obsolete member name and look it up in the registry
            var memberMatch = Regex.Match(line, @"CS0618: '([^']+)'");
            if (memberMatch.Success)
            {
                var member = memberMatch.Groups[1].Value;
                // Extract just the method/type name (last segment before '(')
                var simpleName = Regex.Match(member, @"\.(\w+)\s*\(").Groups[1].Value;
                if (string.IsNullOrEmpty(simpleName))
                    simpleName = member.Split('.').LastOrDefault() ?? member;

                var registryHits = _registry.SearchByApiName(simpleName);
                if (registryHits.Count > 0)
                {
                    var entry = registryHits[0];
                    sb.AppendLine($"  ➡ **Registry fix ({entry.Id})**: `{entry.ReplacementSignature}`");
                    sb.AppendLine($"     {entry.FixDescription}");
                }
                else
                {
                    // Fall back to the compiler's own replacement hint
                    var hintMatch = Regex.Match(line, @"is obsolete: '([^']+)'");
                    if (hintMatch.Success)
                        sb.AppendLine($"  ➡ **Compiler hint**: {hintMatch.Groups[1].Value}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine($"**Total: {cs0618Lines.Count} warning(s)**");
        sb.AppendLine();
        sb.AppendLine("Fix each warning, then re-run `maf_detect_cs0618` to verify zero remaining.");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // maf_api_diff
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_api_diff")]
    [Description("""
        Run dotnet-inspect diff to compare the API surface of a MAF package between two versions.
        Surfaces removed types, renamed members, signature changes, and new additions.

        Requires `dnx` to be installed: dotnet tool install -g dnx

        Package names:
          - Microsoft.Agents.AI              (core agent types)
          - Microsoft.Agents.AI.Workflows    (workflow builder, executors)
          - Microsoft.Agents.A2A             (agent-to-agent protocol)

        Example: fromVersion=1.2.0, toVersion=1.3.0
        """)]
    public string MafApiDiff(
        [Description("Starting version, e.g. '1.2.0'.")] string fromVersion,
        [Description("Target version, e.g. '1.3.0'.")] string toVersion,
        [Description("Package name. Defaults to 'Microsoft.Agents.AI'.")] string packageName = "Microsoft.Agents.AI")
    {
        const string source = "https://api.nuget.org/v3/index.json";
        var args = $"dotnet-inspect@0.7.8 -y --source {source} -- diff " +
                   $"--package {packageName}@{fromVersion}..{toVersion} --source {source}";

        var (output, exitCode) = RunProcess("dnx", args, Directory.GetCurrentDirectory(), timeoutMs: 120_000);

        // Process.Start throws Win32Exception ("The system cannot find the file specified") when
        // the executable is not on PATH — caught and returned as "Failed to run 'dnx': …".
        // Also handles the old CMD "is not recognized" message.
        if (exitCode != 0 && (output.Contains("is not recognized") || output.StartsWith("Failed to run 'dnx'")))
        {
            return $"""
                ⚠️ `dnx` not found. Install it with:
                    dotnet tool install -g dnx

                Then retry:
                    dnx dotnet-inspect@0.7.8 -y --source {source} -- diff \
                      --package {packageName}@{fromVersion}..{toVersion} --source {source}
                """;
        }

        if (string.IsNullOrWhiteSpace(output))
            return $"No diff output returned (exit code: {exitCode}). Both versions may be identical.";

        return $"""
            ## API Diff: {packageName} {fromVersion} → {toVersion}

            {output.Trim()}

            ---
            *Tip: pipe through `nuget-diff-analyzer` skill for categorized risk assessment.*
            """;
    }

    // -------------------------------------------------------------------------
    // maf_fan_out_validate
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_fan_out_validate")]
    [Description("""
        Validate fan-out/fan-in executor topology in a MAF project.

        Searches for executor files that use the [MessageHandler] attribute and checks that
        all handler methods return ValueTask<T> (not void or non-generic ValueTask).

        A void or non-generic ValueTask return is a SILENT RUNTIME FAILURE:
          - The build succeeds with no error
          - The workflow exits cleanly with no output
          - The fan-in barrier starves silently

        Returns a list of at-risk files with the handler signatures found, plus
        files confirmed to use the correct ValueTask<T> pattern.
        """)]
    public string MafFanOutValidate(
        [Description("Path to the repository root or project directory.")] string projectPath)
    {
        if (!Directory.Exists(projectPath))
            return $"Error: Directory not found: {projectPath}";

        var results = new StringBuilder();
        results.AppendLine("## Fan-Out Validator Report");
        results.AppendLine();

        var risks = new List<string>();
        var clean = new List<string>();

        foreach (var csPath in Directory.EnumerateFiles(projectPath, "*.cs", SearchOption.AllDirectories))
        {
            if (IsInObjOrBin(csPath)) continue;
            try
            {
                var content = File.ReadAllText(csPath);
                if (!content.Contains("[MessageHandler]", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relPath = Path.GetRelativePath(projectPath, csPath);

                // Find ValueTask handler signatures in this file
                var voidHandlers = Regex.Matches(content,
                    @"async\s+ValueTask\s+\w+\s*\(",
                    RegexOptions.Multiline).Cast<Match>().Select(m => m.Value.Trim()).ToList();

                var genericHandlers = Regex.Matches(content,
                    @"async\s+ValueTask<[^>]+>\s+\w+\s*\(",
                    RegexOptions.Multiline).Cast<Match>().Select(m => m.Value.Trim()).ToList();

                if (voidHandlers.Count > 0)
                {
                    risks.Add($"❌ `{relPath}` — {voidHandlers.Count} non-generic ValueTask handler(s):");
                    foreach (var h in voidHandlers) risks.Add($"     `{h}`");
                    if (genericHandlers.Count > 0)
                    {
                        risks.Add($"   Also has {genericHandlers.Count} correct ValueTask<T> handler(s).");
                    }
                }
                else if (genericHandlers.Count > 0)
                {
                    clean.Add($"✅ `{relPath}` — {genericHandlers.Count} ValueTask<T> handler(s) — looks correct");
                }
            }
            catch { /* skip unreadable */ }
        }

        if (risks.Count == 0 && clean.Count == 0)
        {
            results.AppendLine("No executor files with `[MessageHandler]` detected.");
            results.AppendLine();
            results.AppendLine("If the project has executors, verify they use `[MessageHandler]` on their handler methods.");
        }
        else
        {
            if (risks.Count > 0)
            {
                results.AppendLine("### ⚠️ At-Risk Files (require attention)");
                foreach (var r in risks) results.AppendLine(r);
                results.AppendLine();
            }
            if (clean.Count > 0)
            {
                results.AppendLine("### ✅ Clean Files");
                foreach (var c in clean) results.AppendLine(c);
                results.AppendLine();
            }
        }

        results.AppendLine("---");
        results.AppendLine("**Rule:** Fan-out handler methods MUST return `ValueTask<T>` where `T` is the output message type.");
        results.AppendLine("`async ValueTask HandleAsync(...)` (non-generic) produces NO output — the fan-in barrier starves silently.");
        return results.ToString();
    }

    // -------------------------------------------------------------------------
    // maf_executor_pattern_check
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_executor_pattern_check")]
    [Description("""
        Search a project for pre-1.3.0 executor patterns that require migration.

        Detects:
          - ReflectingExecutor<T>        → migrate to: sealed partial class : Executor + [MessageHandler]
          - IMessageHandler<TIn,TOut>    → migrate to: [MessageHandler] attribute on handler methods
          - [StreamsMessage]             → REMOVE (attribute removed in 1.3.0, causes CS0246)
          - [YieldsMessage]              → REMOVE (attribute removed in 1.3.0, causes CS0246)

        Returns a file-by-file report with line context for each hit.
        """)]
    public string MafExecutorPatternCheck(
        [Description("Path to the repository root or project directory.")] string projectPath)
    {
        if (!Directory.Exists(projectPath))
            return $"Error: Directory not found: {projectPath}";

        var patterns = new (string Pattern, string Migration, string Severity)[]
        {
            ("ReflectingExecutor<",  "Replace with `sealed partial class : Executor` + `[MessageHandler]` method",        "P1-MIGRATE"),
            ("IMessageHandler<",     "Replace interface with `[MessageHandler]` attribute on handler methods",             "P1-MIGRATE"),
            ("[StreamsMessage]",     "REMOVE — attribute was deleted in MAF 1.3.0, causes CS0246",                        "P1-REMOVE"),
            ("[YieldsMessage]",      "REMOVE — attribute was deleted in MAF 1.3.0, causes CS0246",                        "P1-REMOVE"),
        };

        var sb = new StringBuilder();
        sb.AppendLine("## Executor Pattern Check");
        sb.AppendLine();

        var totalHits = 0;

        foreach (var (pattern, migration, severity) in patterns)
        {
            var hits = new List<string>();

            foreach (var csPath in Directory.EnumerateFiles(projectPath, "*.cs", SearchOption.AllDirectories))
            {
                if (IsInObjOrBin(csPath)) continue;
                try
                {
                    var lines = File.ReadAllLines(csPath);
                    var relPath = Path.GetRelativePath(projectPath, csPath);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            hits.Add($"`{relPath}` line {i + 1}: `{lines[i].Trim()}`");
                    }
                }
                catch { /* skip */ }
            }

            if (hits.Count > 0)
            {
                sb.AppendLine($"### {severity}: `{pattern}`");
                sb.AppendLine($"**Migration:** {migration}");
                sb.AppendLine();
                foreach (var h in hits) sb.AppendLine($"  - {h}");
                sb.AppendLine();
                totalHits += hits.Count;
            }
        }

        if (totalHits == 0)
        {
            sb.AppendLine("✅ No pre-1.3.0 executor patterns found.");
            sb.AppendLine();
            sb.AppendLine("The executor code appears to already use the MAF 1.3.0 pattern:");
            sb.AppendLine("  `sealed partial class : Executor` with `[MessageHandler]` methods.");
        }
        else
        {
            sb.AppendLine("---");
            sb.AppendLine($"**Total: {totalHits} occurrence(s) requiring action.**");
            sb.AppendLine();
            sb.AppendLine("Read `maf://skills?name=maf-migration-guide` Section 9 (Executors and workflows) for before/after patterns.");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // maf_compatibility
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_compatibility")]
    [Description("""
        Return compatibility information for a specific MAF version.

        Shows which .NET runtime, Azure SDK, and extension package versions are required
        or compatible with the requested MAF version.

        Known versions: 1.0.0, 1.1.0, 1.2.0, 1.3.0
        """)]
    public string MafCompatibility(
        [Description("MAF version to query, e.g. '1.3.0'.")] string mafVersion)
    {
        // Static compatibility data — kept in sync with docs/compatibility-matrix.md
        var matrix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1.3.0"] = """
                ## MAF 1.3.0 Compatibility

                | Dependency                          | Version      | Notes |
                |-------------------------------------|-------------|-------|
                | .NET runtime                        | 8.0 or 9.0  | net8.0 and net9.0 TFMs both supported |
                | Microsoft.Extensions.Hosting        | 8.* or 9.*  | |
                | Azure.Identity                      | 1.13.*      | Use ManagedIdentityCredential, not DefaultAzureCredential |
                | Microsoft.SemanticKernel            | 1.24.*+     | If using SK integration |
                | Microsoft.Agents.AI.Workflows.Generators | 1.3.0  | Required for source gen (executor partial classes) |

                ### Removed packages in 1.3.0
                - `Microsoft.Agents.AI.DevUI` — removed, no replacement
                - `Microsoft.Agents.AI.Hosting` — removed, no replacement
                  Guard with: `#if DEVUI_ENABLED`
                """,

            ["1.2.0"] = """
                ## MAF 1.2.0 Compatibility

                | Dependency                          | Version      | Notes |
                |-------------------------------------|-------------|-------|
                | .NET runtime                        | 8.0 or 9.0  | |
                | Microsoft.Extensions.Hosting        | 8.* or 9.*  | |
                | Azure.Identity                      | 1.12.*      | |
                | Microsoft.Agents.AI.Workflows.Generators | Not required | Source gen optional in 1.2.0 |
                """,

            ["1.1.0"] = """
                ## MAF 1.1.0 Compatibility

                | Dependency                          | Version      | Notes |
                |-------------------------------------|-------------|-------|
                | .NET runtime                        | 8.0         | net9.0 not officially supported |
                | Microsoft.Extensions.Hosting        | 8.*         | |
                | Executor pattern                    | ReflectingExecutor<T> | IMessageHandler<TIn,TOut> interface |
                """,

            ["1.0.0"] = """
                ## MAF 1.0.0 Compatibility

                | Dependency                          | Version      | Notes |
                |-------------------------------------|-------------|-------|
                | .NET runtime                        | 8.0         | |
                | Executor pattern                    | ReflectingExecutor<T> | IMessageHandler<TIn,TOut> interface |
                | Sessions                            | AgentThread | GetNewThread() API |
                """,
        };

        if (matrix.TryGetValue(mafVersion, out var info))
            return info;

        return $"""
            No compatibility data found for MAF version '{mafVersion}'.

            Known versions: {string.Join(", ", matrix.Keys.OrderBy(k => k))}

            For the most current data, read the embedded compatibility matrix at `maf://guide`
            or check `docs/compatibility-matrix.md` in the maf-autopilot repository.
            """;
    }

    // -------------------------------------------------------------------------
    // maf_migration_path
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_migration_path")]
    [Description("""
        Return the migration steps and breaking changes between two MAF versions.

        If toVersion is omitted, assumes the target is 1.3.0 (the current supported version).

        Returns a prioritized list of migration tasks, the relevant breaking changes table,
        and a reference to the full migration guide sections.
        """)]
    public string MafMigrationPath(
        [Description("Source MAF version to migrate FROM, e.g. '1.2.0'.")] string fromVersion,
        [Description("Target MAF version to migrate TO. Defaults to '1.3.0'.")] string toVersion = "1.3.0")
    {
        // Normalize
        if (!Version.TryParse(fromVersion, out var from))
            return $"Error: Invalid fromVersion '{fromVersion}'. Use format like '1.2.0'.";
        if (!Version.TryParse(toVersion, out var to))
            return $"Error: Invalid toVersion '{toVersion}'. Use format like '1.3.0'.";
        if (from >= to)
            return $"No migration needed: fromVersion ({fromVersion}) is not older than toVersion ({toVersion}).";

        var sb = new StringBuilder();
        sb.AppendLine($"## Migration Path: MAF {fromVersion} → {toVersion}");
        sb.AppendLine();

        // Only 1.3.0 is fully documented
        if (to != new Version(1, 3, 0))
        {
            sb.AppendLine($"⚠️ Only migration to MAF 1.3.0 is fully documented in this registry.");
            sb.AppendLine($"For other versions, use `maf_api_diff` to get the raw API surface diff.");
            sb.AppendLine();
        }

        // Include all breaking changes applicable from fromVersion to 1.3.0
        sb.AppendLine("### Breaking Changes");
        sb.AppendLine();

        var alwaysApply = new[]
        {
            ("P1", "Executor pattern",  "`ReflectingExecutor<T>` / `IMessageHandler<TIn,TOut>`",  "`sealed partial class : Executor` + `[MessageHandler]` method"),
            ("P1", "Removed attributes", "`[StreamsMessage]`, `[YieldsMessage]`",                 "**Delete these attributes** — causes CS0246 if present"),
            ("P1", "Source gen package", "Missing `Microsoft.Agents.AI.Workflows.Generators`",     "Add `Microsoft.Agents.AI.Workflows.Generators 1.3.0`"),
            ("P1", "Sessions",           "`AgentThread`, `GetNewThread()`",                        "`AgentSession` via `await agent.CreateSessionAsync(ct)`"),
            ("P1", "Session serialize",  "`agent.SerializeSession(session)`",                      "`await agent.SerializeSessionAsync(session)`"),
            ("P1", "Response",           "`AgentResponse.Deserialize<T>()`",                       "`RunAsync<T>()` + `.Result`"),
            ("P1", "Streaming",          "`InProcessExecution.StreamAsync()`",                     "`RunStreamingAsync()`"),
            ("P1", "Events",             "`AgentRunUpdateEvent`",                                  "`AgentResponseUpdateEvent`"),
            ("P1", "A2A DI",             "`AIAgentExtensions`",                                    "`services.AddA2AServer(agent, new A2AServerRegistrationOptions { AgentCard = ... })`"),
            ("P1", "A2A endpoint",       "`app.MapA2A(...)`",                                      "`app.MapA2AHttpJson(path)` or `app.MapA2AJsonRpc(path)`"),
            ("P1", "Agent options",      "`Instructions`/`Tools` at top-level",                    "Must be inside `ChatClientAgentOptions.ChatOptions`"),
            ("P2", "Fan-in arg order",   "`AddFanInBarrierEdge(target, sources)`",                 "`AddFanInBarrierEdge(sources, target)` — sources first"),
            ("P2", "Fan-out return type", "`async ValueTask HandleAsync(...)` (non-generic)",       "`async ValueTask<T> HandleAsync(...)` — MUST return the message type"),
            ("P2", "DevUI/Hosting",       "`Microsoft.Agents.AI.DevUI` / `.Hosting`",              "Removed — guard with `#if DEVUI_ENABLED`"),
        };

        // Apply version filtering (only some changes are new in 1.3.0 vs 1.2.0 etc.)
        // For simplicity, show all for any migration to 1.3.0
        sb.AppendLine("| Priority | Area | Old (broken) | New (correct) |");
        sb.AppendLine("|----------|------|-------------|---------------|");
        foreach (var (priority, area, old, @new) in alwaysApply)
            sb.AppendLine($"| {priority} | {area} | {old} | {@new} |");

        sb.AppendLine();
        sb.AppendLine("### Registry Entries");
        sb.AppendLine($"The registry has {_registry.AllIds.Count} known fix patterns (MAF {_registry.TargetVersion}).");
        sb.AppendLine("Use `maf_registry_list` to see all entries, or `maf_registry_lookup` for details.");
        sb.AppendLine();
        sb.AppendLine("### Full Migration Guide");
        sb.AppendLine("Read `maf://guide` for the full migration reference.");
        sb.AppendLine("Use `maf://skills?name=migration-plan-creator` to generate a structured migration plan.");
        sb.AppendLine();
        sb.AppendLine("### Recommended Workflow");
        sb.AppendLine("1. Run `maf_executor_pattern_check` — find executor patterns needing migration");
        sb.AppendLine("2. Run `maf_detect_cs0618` after package updates — catch all obsolete API calls");
        sb.AppendLine("3. Run `maf_fan_out_validate` — check for silent fan-in starvation");
        sb.AppendLine("4. Or run `maf_full_audit` to let the LLM generate a complete migration plan");

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // maf_health_check
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_health_check")]
    [Description("""
        One-shot MAF 1.3.0 health check for a project or repository.

        Runs all static checks in a single pass and returns a prioritized report:
          🚨 CRITICAL — removed APIs causing build errors, or silent runtime failures
          ⚠️  WARNING  — deprecated APIs (CS0618) requiring migration
          ℹ️  INFO     — patterns to review for correctness

        Checks performed:
          - Removed attributes: [StreamsMessage], [YieldsMessage] (→ CS0246)
          - Security: DefaultAzureCredential in production, EnableSensitiveData = true
          - Obsolete executor patterns: ReflectingExecutor<T>, IMessageHandler<TIn,TOut>
          - Old session API: AgentThread, GetNewThread(), .SerializeSession( (sync)
          - Old streaming: .StreamAsync(), InProcessExecution
          - Old event types: AgentRunUpdateEvent
          - Old A2A registration: AIAgentExtensions, app.MapA2A()
          - Fan-out topology: [MessageHandler] methods returning non-generic ValueTask (silent runtime failure)
          - Fan-in arg order: AddFanInBarrierEdge usage (flag for manual arg-order review)

        Use this as the first step in any MAF code health assessment — for migration
        readiness, post-migration validation, and periodic drift detection.
        """)]
    public string MafHealthCheck(
        [Description("Path to the repository root, solution directory, or project directory.")] string projectPath)
    {
        if (!Directory.Exists(projectPath))
            return $"Error: Directory not found: {projectPath}";

        var critical = new List<HealthFinding>();
        var warnings = new List<HealthFinding>();
        var info = new List<HealthFinding>();

        var criticalPatterns = new (string Pattern, string Area, string Fix)[]
        {
            ("[StreamsMessage]",          "Removed attribute",  "DELETE — removed in MAF 1.3.0, causes CS0246"),
            ("[YieldsMessage]",           "Removed attribute",  "DELETE — removed in MAF 1.3.0, causes CS0246"),
            ("DefaultAzureCredential",    "Security",           "Replace with `ManagedIdentityCredential` in production"),
            ("EnableSensitiveData = true","Security",           "Remove or guard with env check — NEVER in non-dev environments"),
        };

        var warningPatterns = new (string Pattern, string Area, string Fix)[]
        {
            ("ReflectingExecutor<",       "Executor (old)",     "Replace with `sealed partial class : Executor` + `[MessageHandler]`"),
            ("IMessageHandler<",          "Executor (old)",     "Replace interface with `[MessageHandler]` attribute on handler methods"),
            ("AgentThread",               "Session API (old)",  "Replace with `AgentSession` via `await agent.CreateSessionAsync(ct)`"),
            ("GetNewThread()",            "Session API (old)",  "Replace with `await agent.CreateSessionAsync(ct)`"),
            (".SerializeSession(",        "Session API (old)",  "Replace with `await agent.SerializeSessionAsync(session)` (async)"),
            ("AgentResponse.Deserialize", "Response (old)",     "Replace with `RunAsync<T>()` + `.Result`"),
            (".StreamAsync()",            "Streaming (old)",    "Replace with `RunStreamingAsync()`"),
            ("InProcessExecution",        "Streaming (old)",    "Review — may need `RunStreamingAsync()` pattern"),
            ("AgentRunUpdateEvent",       "Events (old)",       "Replace with `AgentResponseUpdateEvent`"),
            ("AIAgentExtensions",         "A2A DI (old)",       "Replace with `services.AddA2AServer(agent, new A2AServerRegistrationOptions { ... })`"),
            ("app.MapA2A(",               "A2A endpoint (old)", "Replace with `app.MapA2AHttpJson(path)` or `app.MapA2AJsonRpc(path)`"),
        };

        var infoPatterns = new (string Pattern, string Area, string Fix)[]
        {
            ("AddFanInBarrierEdge",       "Fan-in (review)",    "Verify arg order: sources FIRST — `AddFanInBarrierEdge(sources, target)`"),
        };

        foreach (var csPath in Directory.EnumerateFiles(projectPath, "*.cs", SearchOption.AllDirectories))
        {
            if (IsInObjOrBin(csPath)) continue;
            string content;
            string[] lines;
            try { content = File.ReadAllText(csPath); lines = content.Split('\n'); }
            catch { continue; }

            var relPath = Path.GetRelativePath(projectPath, csPath);

            foreach (var (pattern, area, fix) in criticalPatterns)
                ScanLines(relPath, lines, pattern, area, fix, critical);
            foreach (var (pattern, area, fix) in warningPatterns)
                ScanLines(relPath, lines, pattern, area, fix, warnings);
            foreach (var (pattern, area, fix) in infoPatterns)
                ScanLines(relPath, lines, pattern, area, fix, info);

            // Fan-out silent failure: [MessageHandler] methods with non-generic ValueTask return
            if (content.Contains("[MessageHandler]", StringComparison.OrdinalIgnoreCase))
            {
                var voidHandlers = Regex.Matches(content,
                    @"async\s+ValueTask\s+\w+\s*\(", RegexOptions.Multiline);
                foreach (Match m in voidHandlers)
                {
                    var lineNum = content[..m.Index].Count(c => c == '\n') + 1;
                    critical.Add(new HealthFinding(relPath, lineNum, m.Value.Trim(),
                        "Fan-out (silent failure)",
                        "Change return type to `ValueTask<T>` — non-generic ValueTask produces NO output; fan-in starves silently"));
                }
            }
        }

        var totalIssues = critical.Count + warnings.Count + info.Count;
        var status = critical.Count > 0 ? "🚨 CRITICAL" : warnings.Count > 0 ? "⚠️  NEEDS ATTENTION" : "✅ HEALTHY";
        var sb = new StringBuilder();
        sb.AppendLine($"## MAF Health Check — {status}");
        sb.AppendLine();
        sb.AppendLine("| Severity | Count |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| 🚨 Critical | {critical.Count} |");
        sb.AppendLine($"| ⚠️  Warning | {warnings.Count} |");
        sb.AppendLine($"| ℹ️  Info | {info.Count} |");
        sb.AppendLine($"| **Total** | **{totalIssues}** |");
        sb.AppendLine();

        if (critical.Count > 0)
        {
            sb.AppendLine("### 🚨 Critical (build errors or silent runtime failures)");
            sb.AppendLine();
            foreach (var g in critical.GroupBy(f => f.Area))
            {
                sb.AppendLine($"**{g.Key}**");
                foreach (var f in g)
                    sb.AppendLine($"  - `{f.File}` line {f.Line}: `{f.Pattern}`");
                sb.AppendLine($"  → Fix: {g.First().Fix}");
                sb.AppendLine();
            }
        }

        if (warnings.Count > 0)
        {
            sb.AppendLine("### ⚠️ Warnings (deprecated APIs requiring migration)");
            sb.AppendLine();
            foreach (var g in warnings.GroupBy(f => f.Area))
            {
                sb.AppendLine($"**{g.Key}**");
                foreach (var f in g)
                    sb.AppendLine($"  - `{f.File}` line {f.Line}: `{f.Pattern}`");
                sb.AppendLine($"  → Fix: {g.First().Fix}");
                sb.AppendLine();
            }
        }

        if (info.Count > 0)
        {
            sb.AppendLine("### ℹ️ Info (manual review recommended)");
            sb.AppendLine();
            foreach (var f in info)
                sb.AppendLine($"  - `{f.File}` line {f.Line}: `{f.Pattern}` → {f.Fix}");
            sb.AppendLine();
        }

        if (totalIssues == 0)
        {
            sb.AppendLine("✅ No issues detected. The codebase appears to follow MAF 1.3.0 best practices.");
            sb.AppendLine();
            sb.AppendLine("Run `maf_detect_cs0618 <project.csproj>` for compiler-level CS0618 verification.");
        }
        else
        {
            sb.AppendLine("---");
            sb.AppendLine("**Next steps:**");
            if (critical.Count > 0)
                sb.AppendLine("1. Fix 🚨 Critical items first — build errors and silent failures take priority.");
            sb.AppendLine($"2. Run `maf_detect_cs0618 <project.csproj>` for compiler-level CS0618 detection.");
            sb.AppendLine("3. Use `maf_review_code` for LLM-assisted review of specific files.");
            sb.AppendLine("4. Re-run `maf_health_check` after fixes to confirm zero remaining issues.");
        }

        return sb.ToString();
    }

    private static void ScanLines(string relPath, string[] lines, string pattern, string area, string fix,
        List<HealthFinding> findings)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase))
                findings.Add(new HealthFinding(relPath, i + 1, pattern, area, fix));
        }
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private static bool IsInObjOrBin(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Run a process and capture stdout+stderr, with an optional timeout.</summary>
    private static (string Output, int ExitCode) RunProcess(
        string exe, string args, string workDir, int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start '{exe}'.");

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var finished = proc.WaitForExit(timeoutMs);
            if (!finished)
            {
                proc.Kill(entireProcessTree: true);
                return ($"Process timed out after {timeoutMs / 1000}s.\n{stdout}", -1);
            }

            // Merge stdout + stderr (dotnet build writes warnings to stderr)
            var combined = stdout.ToString() + stderr.ToString();
            return (combined, proc.ExitCode);
        }
        catch (Exception ex)
        {
            return ($"Failed to run '{exe}': {ex.Message}", -1);
        }
    }
}

internal sealed record HealthFinding(string File, int Line, string Pattern, string Area, string Fix);
