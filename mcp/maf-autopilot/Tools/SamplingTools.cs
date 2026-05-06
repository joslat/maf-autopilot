using MafAutopilot.Data;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace MafAutopilot.Tools;

/// <summary>
/// Sampling tools — uses server.SampleAsync() to invoke the host LLM (VS Code Copilot)
/// for multi-step agentic analysis without requiring external API keys.
///
///   maf_full_audit        — scan repo for MAF patterns, then use LLM to generate a migration plan
///   maf_migration_suggest — given a task/snippet, suggest the correct MAF 1.3.0 migration
///   maf_review_code       — LLM-powered best practices code review for a file or snippet
///   maf_explain_error     — explain a compiler error or runtime symptom, provide the exact fix
/// </summary>
[McpServerToolType]
public sealed class SamplingTools
{
    private readonly RegistryService _registry;

    public SamplingTools(RegistryService registry)
    {
        _registry = registry;
    }

    [McpServerTool(Name = "maf_full_audit")]
    [Description("""
        Full agentic MAF migration audit. Scans the specified repository for MAF package
        references and known pre-1.3.0 patterns, then uses the host LLM (VS Code Copilot)
        to generate a complete, ready-to-execute migration-plan.md tracking table.

        Patterns detected:
          - MAF NuGet package versions in .csproj files
          - ReflectingExecutor<T>, IMessageHandler<TIn,TOut> (executor migration needed)
          - AgentThread, GetNewThread() (session API migration needed)
          - [StreamsMessage], [YieldsMessage] attributes (must be removed — causes CS0246)
          - .SerializeSession( (use SerializeSessionAsync)
          - .StreamAsync() (renamed to RunStreamingAsync)
          - AgentRunUpdateEvent (renamed to AgentResponseUpdateEvent)
          - DefaultAzureCredential (forbidden in production)

        Returns a complete migration plan in tracking-table format, ready to execute.

        Requires the MCP host to support sampling (VS Code Copilot with agent mode).
        """)]
    public async Task<string> MafFullAudit(
        RequestContext<CallToolRequestParams> context,
        [Description("Absolute path to the repository root or solution directory to audit.")] string repoPath,
        [Description("Current MAF version in the codebase (e.g. '1.2.0'). Leave null if unknown.")] string? fromVersion = null,
        [Description("Optional: absolute path to write the generated migration-plan.md. If provided, the plan is written to disk and the path is confirmed in the response.")] string? outputPath = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(repoPath))
            return $"Error: Directory not found: {repoPath}";

        var findings = ScanRepository(repoPath);
        var prompt = BuildAuditPrompt(repoPath, fromVersion, findings);

        try
        {
            var result = await context.Server.SampleAsync(
                new CreateMessageRequestParams
                {
                    Messages = [new SamplingMessage { Role = Role.User, Content = [new TextContentBlock { Text = prompt }] }],
                    MaxTokens = 4096,
                    SystemPrompt = AuditSystemPrompt,
                },
                ct);

            var planText = ExtractText(result);

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
                    Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(outputPath, planText, ct);
                    return $"migration-plan.md written to: {outputPath}\n\n{planText}";
                }
                catch (Exception writeEx)
                {
                    return $"Warning: could not write to '{outputPath}': {writeEx.Message}\n\n{planText}";
                }
            }

            return planText;
        }
        catch (Exception ex)
        {
            return $"""
                ⚠️ Sampling unavailable: {ex.Message}

                This tool requires the MCP host to support sampling (VS Code Copilot in agent mode).
                Use the /maf-audit prompt instead — it generates the same starting context for the MAF Auditor Agent.

                Scan findings (for manual use with /maf-audit):
                {findings.ToReport()}
                """;
        }
    }

    [McpServerTool(Name = "maf_migration_suggest")]
    [Description("""
        Suggest the correct MAF 1.3.0 migration for a specific task or code pattern.

        Provide a description of what needs to migrate (e.g., "I have a ReflectingExecutor<T>
        that implements IMessageHandler<MyInput, MyOutput>") and optionally a code snippet.

        The tool cross-references the MAF 1.3.0 obsolete API registry, then uses the host LLM
        to provide the exact before/after migration with build verification steps.

        Requires the MCP host to support sampling (VS Code Copilot with agent mode).
        """)]
    public async Task<string> MafMigrationSuggest(
        RequestContext<CallToolRequestParams> context,
        [Description("Description of what needs to migrate. E.g. 'ReflectingExecutor with IMessageHandler' or 'AgentThread to AgentSession'.")] string taskDescription,
        [Description("Optional: a C# code snippet showing the current (pre-1.3.0) code.")] string? codeSnippet = null,
        CancellationToken ct = default)
    {
        var prompt = BuildSuggestPrompt(taskDescription, codeSnippet);

        try
        {
            var result = await context.Server.SampleAsync(
                new CreateMessageRequestParams
                {
                    Messages = [new SamplingMessage { Role = Role.User, Content = [new TextContentBlock { Text = prompt }] }],
                    MaxTokens = 2048,
                    SystemPrompt = MigrationSystemPrompt,
                },
                ct);

            return ExtractText(result);
        }
        catch (Exception ex)
        {
            return $"⚠️ Sampling unavailable: {ex.Message}. Use the /maf-migrate prompt or @maf-migration agent instead.";
        }
    }

    // -------------------------------------------------------------------------
    // Repository scanning
    // -------------------------------------------------------------------------

    private static RepoFindings ScanRepository(string repoPath)
    {
        var findings = new RepoFindings();

        foreach (var csprojPath in Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsInObjOrBin(csprojPath)) continue;
            try { ExtractMafPackages(csprojPath, File.ReadAllText(csprojPath), findings); }
            catch { /* skip unreadable */ }
        }

        foreach (var csPath in Directory.EnumerateFiles(repoPath, "*.cs", SearchOption.AllDirectories))
        {
            if (IsInObjOrBin(csPath)) continue;
            try
            {
                var content = File.ReadAllText(csPath);
                var relPath = Path.GetRelativePath(repoPath, csPath);
                CheckFileForPatterns(relPath, content, findings);
            }
            catch { /* skip unreadable */ }
        }

        return findings;
    }

    private static bool IsInObjOrBin(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void ExtractMafPackages(string csprojPath, string content, RepoFindings findings)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            content,
            @"PackageReference\s+Include=""(Microsoft\.Agents[^""]*)""\s+Version=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match m in matches)
            findings.MafPackages.Add($"{m.Groups[1].Value} {m.Groups[2].Value} (in {Path.GetFileName(csprojPath)})");
    }

    private static void CheckFileForPatterns(string relPath, string content, RepoFindings findings)
    {
        var patterns = new[]
        {
            ("ReflectingExecutor<",         "executor-old-pattern",    "P1"),
            ("IMessageHandler<",            "executor-old-interface",   "P1"),
            ("[StreamsMessage]",            "removed-attribute",        "P1"),
            ("[YieldsMessage]",             "removed-attribute",        "P1"),
            ("AgentThread",                 "session-api-old",          "P1"),
            ("GetNewThread()",              "session-api-old",          "P1"),
            (".SerializeSession(",          "session-serialize-old",    "P1"),
            ("AgentResponse.Deserialize",   "response-api-old",         "P1"),
            (".StreamAsync()",              "streaming-old",            "P1"),
            ("InProcessExecution",         "streaming-old",            "P1"),
            ("AgentRunUpdateEvent",         "event-old",               "P1"),
            ("AIAgentExtensions",           "a2a-di-old",              "P1"),
            ("app.MapA2A(",                 "a2a-endpoint-old",        "P1"),
            ("DefaultAzureCredential",      "credential-forbidden",    "P1"),
            ("EnableSensitiveData = true",  "sensitive-data-forbidden", "P1"),
        };

        foreach (var (pattern, category, priority) in patterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                findings.Issues.Add(new Finding(relPath, pattern, category, priority));
        }
    }

    // -------------------------------------------------------------------------
    // Prompt builders
    // -------------------------------------------------------------------------

    private string BuildAuditPrompt(string repoPath, string? fromVersion, RepoFindings findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MAF Migration Audit Request");
        sb.AppendLine();
        sb.AppendLine($"Repository: `{repoPath}`");
        sb.AppendLine($"Target MAF version: 1.3.0");
        if (fromVersion is not null) sb.AppendLine($"Current MAF version: {fromVersion}");
        sb.AppendLine();

        if (findings.MafPackages.Count > 0)
        {
            sb.AppendLine("### MAF Packages Found");
            foreach (var pkg in findings.MafPackages) sb.AppendLine($"  - {pkg}");
        }
        else
        {
            sb.AppendLine("### MAF Packages");
            sb.AppendLine("  No `Microsoft.Agents.*` package references found in .csproj files.");
        }
        sb.AppendLine();

        if (findings.Issues.Count > 0)
        {
            sb.AppendLine("### Patterns Requiring Migration");
            foreach (var group in findings.Issues.GroupBy(x => x.Category))
            {
                sb.AppendLine($"\n**{group.Key}** ({group.First().Priority}):");
                foreach (var f in group)
                    sb.AppendLine($"  - `{f.Pattern}` found in `{f.File}`");
            }
        }
        else
        {
            sb.AppendLine("### Patterns");
            sb.AppendLine("  No pre-1.3.0 patterns detected. Codebase may already be migrated or uses a different structure.");
        }
        sb.AppendLine();

        // Include top registry entries for context
        sb.AppendLine("### Known Fix Patterns (registry excerpt)");
        foreach (var entry in _registry.AllIds.Take(5))
        {
            var e = _registry.FindById(entry)!;
            sb.AppendLine($"  - **{e.Id}**: `{e.ObsoleteSignature}` → `{e.ReplacementSignature}`");
        }
        sb.AppendLine();

        sb.AppendLine("### Task");
        sb.AppendLine("Generate a complete `migration-plan.md` tracking table for migrating this codebase to MAF 1.3.0.");
        sb.AppendLine("Columns: `# | Name | Description | Priority | Status | % Done | % Reviewed | Notes`");
        sb.AppendLine("Priority: P1=Critical path, P2=High, P3=Nice-to-have. Status starts as `❌ NOT STARTED`.");
        sb.AppendLine("Map each detected pattern above to one or more migration tasks.");
        sb.AppendLine("If no issues found, generate a smoke-test verification task.");
        return sb.ToString();
    }

    private string BuildSuggestPrompt(string taskDescription, string? codeSnippet)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MAF 1.3.0 Migration Suggestion Request");
        sb.AppendLine();
        sb.AppendLine($"Task: {taskDescription}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(codeSnippet))
        {
            sb.AppendLine("Current code (before migration):");
            sb.AppendLine("```csharp");
            sb.AppendLine(codeSnippet.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        var hits = _registry.SearchByApiName(taskDescription);
        if (hits.Count > 0)
        {
            sb.AppendLine("### Relevant Registry Entries");
            foreach (var entry in hits.Take(3))
            {
                sb.AppendLine($"- **{entry.Id}** (`{entry.CsWarning}`): `{entry.ObsoleteSignature}` → `{entry.ReplacementSignature}`");
                sb.AppendLine($"  Fix: {entry.FixDescription}");
                if (!string.IsNullOrWhiteSpace(entry.ExampleAfter))
                {
                    sb.AppendLine($"  After: `{entry.ExampleAfter.Trim()}`");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("### Task");
        sb.AppendLine("Provide the exact MAF 1.3.0 migration with:");
        sb.AppendLine("1. Which pattern is obsolete and the MAF 1.3.0 rule that applies");
        sb.AppendLine("2. The corrected code (after migration) as a C# snippet");
        sb.AppendLine("3. Any build verification step (e.g., `dotnet build | Select-String CS0618`)");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string ExtractText(CreateMessageResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(b => b.Text ?? string.Empty));
    }

    private static bool LooksLikeCodeSnippet(string s) =>
        s.Contains('\n') || s.Contains('{') || s.Contains(';') || s.Contains("//");

    private const string AuditSystemPrompt =
        "You are a MAF 1.3.0 migration expert. Analyze the codebase findings and generate a complete, " +
        "ready-to-execute migration-plan.md tracking table. " +
        "Hard constraints: NEVER add [StreamsMessage] or [YieldsMessage]; NEVER use DefaultAzureCredential; " +
        "fan-out handlers MUST return ValueTask<T> not void; use AgentSession not AgentThread; " +
        "use AddFanInBarrierEdge(sources, target) — sources first.";

    private const string MigrationSystemPrompt =
        "You are a MAF 1.3.0 migration expert. Provide concise, actionable migration guidance. " +
        "Show exact before/after C# code. " +
        "Hard constraints: NEVER add [StreamsMessage] or [YieldsMessage]; NEVER use DefaultAzureCredential; " +
        "use AgentSession not AgentThread; fan-out handlers MUST return ValueTask<T>.";

    // -------------------------------------------------------------------------
    // maf_review_code
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_review_code")]
    [Description("""
        LLM-powered MAF 1.3.0 best practices code review for a file or snippet.

        Unlike the migration tools, this is for general code quality and correctness —
        useful both during migration and for day-to-day MAF development.

        Provide either:
          - A file path (absolute) — file contents are read automatically
          - A C# code snippet — pasted directly as the input

        Optional focusArea to narrow the review:
          - "executors"  — executor patterns, [MessageHandler], partial classes
          - "sessions"   — session lifecycle, CreateSessionAsync, ProviderSessionState<T>
          - "fan-out"    — fan-out/fan-in topology, ValueTask<T> return types
          - "security"   — credentials, sensitive data
          - "streaming"  — RunStreamingAsync patterns
          - "a2a"        — AddA2AServer, MapA2AHttpJson/MapA2AJsonRpc
          - (omit)       — full review covering all areas

        Returns a structured review: severity (🚨/⚠️/ℹ️), location, issue, and fix suggestion.

        Requires the MCP host to support sampling (VS Code Copilot with agent mode).
        For static analysis without LLM, use `maf_health_check` instead.
        """)]
    public async Task<string> MafReviewCode(
        RequestContext<CallToolRequestParams> context,
        [Description("Absolute file path to review, OR a C# code snippet to review directly.")] string codeOrFilePath,
        [Description("Optional focus area: executors, sessions, fan-out, security, streaming, a2a. Omit for full review.")] string? focusArea = null,
        CancellationToken ct = default)
    {
        string code;
        string codeSource;

        var resolvedPath = Path.IsPathRooted(codeOrFilePath)
            ? codeOrFilePath
            : Path.GetFullPath(codeOrFilePath);

        if (File.Exists(resolvedPath))
        {
            try
            {
                code = File.ReadAllText(resolvedPath);
                codeSource = $"File: `{Path.GetFileName(resolvedPath)}`";
            }
            catch (Exception ex)
            {
                return $"Error reading file '{resolvedPath}': {ex.Message}";
            }
        }
        else if (LooksLikeCodeSnippet(codeOrFilePath))
        {
            code = codeOrFilePath;
            codeSource = "Provided snippet";
        }
        else
        {
            return $"Error: File not found: '{resolvedPath}' — provide an absolute path, a relative path, or a C# code snippet.";
        }

        var trimNote = string.Empty;
        if (code.Length > 8000)
        {
            code = code[..8000];
            trimNote = "\n\n*(Note: code was trimmed to 8000 chars)*";
        }

        var prompt = BuildReviewPrompt(code, codeSource, focusArea);

        try
        {
            var result = await context.Server.SampleAsync(
                new CreateMessageRequestParams
                {
                    Messages = [new SamplingMessage { Role = Role.User, Content = [new TextContentBlock { Text = prompt }] }],
                    MaxTokens = 3000,
                    SystemPrompt = ReviewSystemPrompt,
                },
                ct);

            return ExtractText(result) + trimNote;
        }
        catch (Exception ex)
        {
            return $"""
                ⚠️ Sampling unavailable: {ex.Message}

                For static analysis without LLM, use:
                  - `maf_health_check <projectPath>` — comprehensive static check (no LLM)
                  - `maf_executor_pattern_check <projectPath>` — executor patterns only
                  - `maf_fan_out_validate <projectPath>` — fan-out topology only
                """;
        }
    }

    private string BuildReviewPrompt(string code, string codeSource, string? focusArea)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MAF 1.3.0 Code Review Request");
        sb.AppendLine();
        sb.AppendLine($"**Source:** {codeSource}");
        if (focusArea is not null)
            sb.AppendLine($"**Focus area:** {focusArea}");
        sb.AppendLine();
        sb.AppendLine("### Code");
        sb.AppendLine("```csharp");
        sb.AppendLine(code.TrimEnd());
        sb.AppendLine("```");
        sb.AppendLine();

        if (_registry.AllIds.Count > 0)
        {
            sb.AppendLine("### Known obsolete patterns (registry excerpt)");
            foreach (var id in _registry.AllIds.Take(6))
            {
                var e = _registry.FindById(id)!;
                sb.AppendLine($"  - `{e.ObsoleteSignature}` \u2192 `{e.ReplacementSignature}`");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### Review task");
        if (string.IsNullOrWhiteSpace(focusArea))
        {
            sb.AppendLine("Review for all MAF 1.3.0 best practices:");
            sb.AppendLine("1. **Executors** — `sealed partial class : Executor` + `[MessageHandler]`; no `ReflectingExecutor<T>` / `IMessageHandler<>`");
            sb.AppendLine("2. **Session state** — uses `AgentSession` / `CreateSessionAsync`; no `AgentThread`; session-specific state goes in `ProviderSessionState<T>` (NEVER in `AIContextProvider` instance fields)");
            sb.AppendLine("3. **Fan-out/fan-in** — handlers return `ValueTask<T>` (NOT void); `AddFanInBarrierEdge` has sources as first arg");
            sb.AppendLine("4. **Security** — no `DefaultAzureCredential` in production; no `EnableSensitiveData = true` in non-dev");
            sb.AppendLine("5. **Streaming** — `RunStreamingAsync()` not `InProcessExecution.StreamAsync()`");
            sb.AppendLine("6. **A2A** — `AddA2AServer` + `MapA2AHttpJson`/`MapA2AJsonRpc`; not `AIAgentExtensions` or `MapA2A`");
            sb.AppendLine("7. **Agent options** — `Instructions` and `Tools` inside `ChatClientAgentOptions.ChatOptions`");
            sb.AppendLine("8. **Removed attributes** — no `[StreamsMessage]` or `[YieldsMessage]`");
        }
        else
        {
            sb.AppendLine($"Focus specifically on: **{focusArea}** — only report findings in this area.");
        }

        sb.AppendLine();
        sb.AppendLine("### Output format");
        sb.AppendLine("For each issue: **Severity** (🚨 Critical / ⚠️ Warning / ℹ️ Info) | **Location** | **Issue** | **Fix** (with code snippet if helpful)");
        sb.AppendLine("End with a **Summary**: overall quality + count by severity.");
        sb.AppendLine("If the code is compliant, explicitly confirm which best practices it follows.");
        return sb.ToString();
    }

    private const string ReviewSystemPrompt =
        "You are a MAF 1.3.0 code reviewer. Review C# code for compliance with MAF 1.3.0 best practices. " +
        "Be specific: cite exact method or type names. Focus on real issues. " +
        "Hard constraints to check: no [StreamsMessage]/[YieldsMessage], no DefaultAzureCredential in production, " +
        "fan-out handlers MUST return ValueTask<T> (not void), session state in ProviderSessionState<T>, " +
        "AgentSession not AgentThread, sources before target in AddFanInBarrierEdge.";

    // -------------------------------------------------------------------------
    // maf_explain_error
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "maf_explain_error")]
    [Description("""
        Explain a MAF-related error, compiler warning, or runtime symptom and provide the exact fix.

        Accepts any of:
          - A compiler error:    "CS0618: 'AddFanInBarrierEdge(ExecutorBinding, ...)' is obsolete"
          - Build output paste:  one or many lines from `dotnet build`
          - A runtime symptom:  "fan-in barrier completes but workflow output is empty"
          - Just a CS code:     "CS0246"

        The tool extracts CS error codes, searches the registry for known patterns, then uses the
        host LLM to explain the MAF 1.3.0 root cause and provide corrected code.

        If sampling is unavailable, falls back to static registry lookup and CS-code explanations.
        For compiler-only scanning without LLM, use `maf_detect_cs0618` or `maf_api_safety`.

        Requires the MCP host to support sampling (VS Code Copilot with agent mode).
        """)]
    public async Task<string> MafExplainError(
        RequestContext<CallToolRequestParams> context,
        [Description("The error text, build output lines, runtime symptom, or CS error code to explain.")] string errorText,
        [Description("Optional additional context: what you were doing, relevant code snippets, or expected behaviour.")] string? additionalContext = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(errorText))
            return "Error: errorText is required.";

        var prompt = BuildExplainPrompt(errorText, additionalContext);

        try
        {
            var result = await context.Server.SampleAsync(
                new CreateMessageRequestParams
                {
                    Messages = [new SamplingMessage { Role = Role.User, Content = [new TextContentBlock { Text = prompt }] }],
                    MaxTokens = 2000,
                    SystemPrompt = ExplainSystemPrompt,
                },
                ct);

            return ExtractText(result);
        }
        catch (Exception ex)
        {
            // Fallback: static analysis without LLM
            var csMatches = Regex.Matches(errorText, @"CS\d{4}", RegexOptions.IgnoreCase)
                .Cast<Match>().Select(m => m.Value.ToUpper()).Distinct().ToList();

            var memberMatches = Regex.Matches(errorText, @"'([^']+)'")
                .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

            var hits = new List<RegistryEntry>();
            var seenHitIds = new HashSet<string>();
            foreach (var member in memberMatches)
            {
                var simpleName = Regex.Match(member, @"\.?(\w+)\s*\(").Groups[1].Value;
                if (string.IsNullOrEmpty(simpleName)) simpleName = member.Split('.').LastOrDefault() ?? member;
                hits.AddRange(_registry.SearchByApiName(simpleName).Where(h => seenHitIds.Add(h.Id)));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"⚠️ Sampling unavailable: {ex.Message}");
            sb.AppendLine();

            if (csMatches.Count > 0)
            {
                sb.AppendLine($"**CS codes detected:** {string.Join(", ", csMatches)}");
                foreach (var cs in csMatches)
                {
                    var explanation = cs switch
                    {
                        "CS0618" => "Obsolete API — look up the replacement in the registry (`maf_registry_lookup`) or run `maf_detect_cs0618`.",
                        "CS0246" => "Type not found — likely a removed attribute (`[StreamsMessage]`/`[YieldsMessage]`) or missing `Microsoft.Agents.AI.Workflows.Generators` package.",
                        "CS1061" => "Member not found — likely a renamed method (e.g. `SerializeSession` → `SerializeSessionAsync`, `AgentThread` → `AgentSession`).",
                        "CS8795" => "Partial method has no implementation — ensure `Microsoft.Agents.AI.Workflows.Generators` is referenced so the source generator runs.",
                        _ => "See maf://constraints for common breaking changes."
                    };
                    sb.AppendLine($"  - `{cs}`: {explanation}");
                }
                sb.AppendLine();
            }

            if (hits.Count > 0)
            {
                sb.AppendLine("**Registry matches (known fixes):**");
                foreach (var h in hits.Take(3))
                {
                    sb.AppendLine($"  - **{h.Id}**: `{h.ObsoleteSignature}` → `{h.ReplacementSignature}`");
                    sb.AppendLine($"    {h.FixDescription}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("**Next steps without LLM:**");
            sb.AppendLine("  - `maf_detect_cs0618 <projectPath>` — run the compiler scan for all CS0618 warnings");
            sb.AppendLine("  - `maf_api_safety <apiName>` — check if a specific API is safe to use");
            sb.AppendLine("  - `maf_registry_lookup <id>` — look up a specific registry entry");
            sb.AppendLine("  - `maf://constraints` — read the hard constraints and breaking changes table");
            return sb.ToString();
        }
    }

    private string BuildExplainPrompt(string errorText, string? additionalContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MAF Error Explanation Request");
        sb.AppendLine();
        sb.AppendLine("### Error / Symptom");
        sb.AppendLine("```");
        sb.AppendLine(errorText.Length > 3000 ? errorText[..3000] + "\n...[truncated]" : errorText);
        sb.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            sb.AppendLine();
            sb.AppendLine("### Additional Context");
            sb.AppendLine(additionalContext);
        }

        // Extract CS codes and search registry for context
        var csMatches = Regex.Matches(errorText, @"CS\d{4}", RegexOptions.IgnoreCase)
            .Cast<Match>().Select(m => m.Value.ToUpper()).Distinct().ToList();

        var memberMatches = Regex.Matches(errorText, @"'([^']+)'")
            .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

        var registryHits = new List<RegistryEntry>();
        var seenRegistryIds = new HashSet<string>();
        foreach (var member in memberMatches)
        {
            var simpleName = Regex.Match(member, @"\.?(\w+)\s*\(").Groups[1].Value;
            if (string.IsNullOrEmpty(simpleName)) simpleName = member.Split('.').LastOrDefault() ?? member;
            registryHits.AddRange(_registry.SearchByApiName(simpleName).Where(h => seenRegistryIds.Add(h.Id)));
        }

        if (registryHits.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Registry Matches (known MAF patterns)");
            foreach (var e in registryHits.Take(5))
            {
                sb.AppendLine($"- **{e.Id}** (`{e.CsWarning}`): `{e.ObsoleteSignature}` → `{e.ReplacementSignature}`");
                sb.AppendLine($"  {e.FixDescription}");
                if (!string.IsNullOrWhiteSpace(e.ExampleAfter))
                    sb.AppendLine($"  After: `{e.ExampleAfter.Trim()}`");
            }
        }

        if (csMatches.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"### CS Codes Found: {string.Join(", ", csMatches)}");
            if (csMatches.Contains("CS0618"))
                sb.AppendLine("CS0618 = obsolete API — has a direct registered replacement.");
            if (csMatches.Contains("CS0246"))
                sb.AppendLine("CS0246 = type not found — likely a removed attribute or missing Generators package.");
            if (csMatches.Contains("CS1061"))
                sb.AppendLine("CS1061 = member not found — likely a renamed method or property.");
            if (csMatches.Contains("CS8795"))
                sb.AppendLine("CS8795 = partial method has no implementation — source generator likely not running.");
        }

        sb.AppendLine();
        sb.AppendLine("### Task");
        sb.AppendLine("Explain this error or symptom in the context of MAF 1.3.0:");
        sb.AppendLine("1. **Root cause** — which MAF 1.3.0 breaking change, constraint violation, or API removal caused this?");
        sb.AppendLine("2. **Exact fix** — show the corrected code (before → after). If a registry match above applies, use it.");
        sb.AppendLine("3. **Verification** — the build or test command to confirm the fix worked.");
        return sb.ToString();
    }

    private const string ExplainSystemPrompt =
        "You are a MAF 1.3.0 expert diagnosing compiler errors and runtime symptoms. " +
        "Explain the root cause clearly in terms of MAF 1.3.0 breaking changes. " +
        "Always provide the exact corrected code, not just a description. " +
        "Key patterns: CS0246=[StreamsMessage]/[YieldsMessage] deleted or Generators package missing; " +
        "CS0618=obsolete API with a registered replacement; CS1061=renamed member (e.g. SerializeSession→SerializeSessionAsync); " +
        "CS8795=source generator not running (add Microsoft.Agents.AI.Workflows.Generators 1.3.0).";
}

// -------------------------------------------------------------------------
// Internal data types
// -------------------------------------------------------------------------

internal sealed record Finding(string File, string Pattern, string Category, string Priority);

internal sealed class RepoFindings
{
    public List<string> MafPackages { get; } = [];
    public List<Finding> Issues { get; } = [];

    public string ToReport()
    {
        var sb = new StringBuilder();
        if (MafPackages.Count > 0)
        {
            sb.AppendLine("MAF Packages:");
            foreach (var p in MafPackages) sb.AppendLine($"  - {p}");
        }
        if (Issues.Count > 0)
        {
            sb.AppendLine($"Issues ({Issues.Count} total across {Issues.Select(i => i.File).Distinct().Count()} files):");
            foreach (var i in Issues) sb.AppendLine($"  - {i.Pattern} in {i.File}");
        }
        else
        {
            sb.AppendLine("No pre-1.3.0 patterns detected.");
        }
        return sb.ToString();
    }
}
