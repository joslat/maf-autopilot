using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace MafAutopilot.Prompts;

/// <summary>
/// MCP Prompts — pre-built starting prompts for common MAF migration workflows.
///
///   /maf-audit       — audit a codebase and generate a migration plan
///   /maf-migrate     — execute specific migration tasks from a plan
///   /maf-cs0618-hunt — find and fix all CS0618 obsolete API warnings
///   /maf-review      — best practices code review (day-to-day development)
///   /maf-debug       — diagnose errors and runtime symptoms (reactive debugging)
///   /maf-scaffold    — generate correct MAF 1.3.0 boilerplate code
/// </summary>
[McpServerPromptType]
public static class MafPrompts
{
    [McpServerPrompt(Name = "maf-audit",
        Title = "MAF Audit: Generate Migration Plan")]
    [Description(
        "Generates a starter prompt for the MAF Auditor Agent. " +
        "Scans the codebase, cross-references the constraint rules and obsolete API registry, " +
        "and outputs a complete migration-plan.md tracking table.")]
    public static IList<PromptMessage> Audit(
        [Description("Path to the repository root or solution file to audit.")] string repoPath,
        [Description("Current MAF version in the codebase (e.g. 1.1.0, 1.2.0). Leave empty if unknown.")] string? fromVersion = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the MAF Auditor Agent. Your task is to audit a .NET codebase and produce a ready-to-execute migration plan.");
        sb.AppendLine();
        sb.AppendLine($"Repository: {repoPath}");
        if (fromVersion is not null)
            sb.AppendLine($"Current MAF version: {fromVersion}");
        sb.AppendLine("Target MAF version: 1.3.0");
        sb.AppendLine();
        sb.AppendLine("BEFORE STARTING:");
        sb.AppendLine("1. Read the hard constraints at maf://constraints — these must never be violated.");
        sb.AppendLine("2. Read the obsolete API registry at maf://registry for known fix patterns.");
        sb.AppendLine("3. Use maf_api_safety to validate any API you are unsure about.");
        sb.AppendLine("4. Use maf_registry_lookup to check if a specific CS0618 pattern already has a registered fix.");
        sb.AppendLine();
        sb.AppendLine("OUTPUT: A complete migration-plan.md tracking table following the migration-plan-creator skill format.");
        sb.AppendLine("Read maf://skills?name=migration-plan-creator for the canonical template and task ID conventions.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-migrate",
        Title = "MAF Migrate: Execute Migration Tasks")]
    [Description(
        "Generates a starter prompt for the MAF Migration Agent to execute one or more tasks " +
        "from an existing migration-plan.md. Enforces the hard constraints and CS0618-hunter workflow.")]
    public static IList<PromptMessage> Migrate(
        [Description("Task row number(s) from migration-plan.md to execute. Single: '5'. Range: '5,6,7'.")] string taskIds,
        [Description("Path to the migration-plan.md file. Leave empty to let the agent locate it.")] string? planPath = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the MAF Migration Agent. Execute the specified migration tasks.");
        sb.AppendLine();
        sb.AppendLine($"Tasks to execute: {taskIds}");
        if (planPath is not null)
            sb.AppendLine($"Migration plan: {planPath}");
        sb.AppendLine();
        sb.AppendLine("MANDATORY RULES (from maf://constraints — read it now):");
        sb.AppendLine("- NEVER add [StreamsMessage] or [YieldsMessage] attributes — they are removed in 1.3.0.");
        sb.AppendLine("- NEVER store session state in AIContextProvider/ChatHistoryProvider instance fields.");
        sb.AppendLine("- NEVER use DefaultAzureCredential in production code.");
        sb.AppendLine("- For CS0618 warnings: use `maf_detect_cs0618 <projectPath>` (compiler scan) or the `/maf-cs0618-hunt` prompt.");
        sb.AppendLine("  dotnet-inspect inspects NuGet package API surfaces — use the compiler scan to find call sites in your source code.");
        sb.AppendLine("- ALWAYS update the tracking table in migration-plan.md after each build-verified task.");
        sb.AppendLine();
        sb.AppendLine("Fan-out/fan-in silent failure check: after each executor change, verify handlers return ValueTask<T> (not void).");
        sb.AppendLine("Read maf://skills?name=fan-out-validator for the validation procedure.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-cs0618-hunt",
        Title = "MAF CS0618: Find and Fix Obsolete APIs")]
    [Description(
        "Generates a starter prompt to find and fix all CS0618 obsolete API warnings " +
        "using the cs0618-hunter skill. Runs the compiler on your project for project-wide file:line detection — " +
        "one pass, all warnings, all files. " +
        "dotnet-inspect (issue #316 resolved) inspects NuGet package API surfaces; " +
        "this prompt scans your source code for every warning call site.")]
    public static IList<PromptMessage> Cs0618Hunt(
        [Description("Path to the .NET solution or project file.")] string projectPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Use the cs0618-hunter skill to find and fix all CS0618 obsolete API warnings.");
        sb.AppendLine();
        sb.AppendLine($"Project: {projectPath}");
        sb.AppendLine();
        sb.AppendLine("WORKFLOW (from maf://skills?name=cs0618-hunter):");
        sb.AppendLine("1. Run: dotnet build 2>&1 | Select-String 'warning CS0618'  — this is the baseline.");
        sb.AppendLine("2. For each warning, look up the fix in maf://registry using maf_registry_lookup.");
        sb.AppendLine("3. Apply the fix. Re-run the build. Confirm the warning is gone before moving on.");
        sb.AppendLine("4. For targeted per-member lookup: dotnet-inspect can now detect [Obsolete] at the overload level (issue #316 fixed).");
        sb.AppendLine("   For project-wide scanning, the compiler approach above is faster — one pass, all files, file:line output.");
        sb.AppendLine();
        sb.AppendLine("Cross-reference with the hard constraints at maf://constraints for any AddFanInBarrierEdge usage.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-review",
        Title = "MAF Review: Best Practices Code Review")]
    [Description(
        "Generates a starter prompt for a MAF 1.3.0 best practices code review. " +
        "Not migration-specific — useful for day-to-day development, PR review, and post-migration validation. " +
        "Checks executors, sessions, fan-out topology, security, streaming, and A2A patterns.")]
    public static IList<PromptMessage> Review(
        [Description("Path to the file or directory to review. Can be a .cs file, a project directory, or 'all' to review the current context.")] string? target = null,
        [Description("Optional focus area: executors, sessions, fan-out, security, streaming, a2a. Omit for full review.")] string? focusArea = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Review the provided MAF code for compliance with MAF 1.3.0 best practices.");
        sb.AppendLine("This is NOT a migration audit — focus on code quality and correctness for 1.3.0.");
        sb.AppendLine();
        if (target is not null)
            sb.AppendLine($"Target: {target}");
        if (focusArea is not null)
            sb.AppendLine($"Focus area: {focusArea}");
        sb.AppendLine();
        sb.AppendLine("TOOLS AVAILABLE:");
        sb.AppendLine("- `maf_review_code <filePathOrSnippet>` — LLM-powered review of a file or snippet");
        sb.AppendLine("- `maf_health_check <projectPath>` — static check: critical/warning/info across all patterns (no LLM)");
        sb.AppendLine("- `maf_fan_out_validate <projectPath>` — verify fan-out executor return types");
        sb.AppendLine("- `maf_executor_pattern_check <projectPath>` — check for old executor patterns");
        sb.AppendLine();
        sb.AppendLine("REVIEW CHECKLIST (MAF 1.3.0):");
        sb.AppendLine("1. Executors: `sealed partial class : Executor` + `[MessageHandler]` on handler methods");
        sb.AppendLine("   No `ReflectingExecutor<T>`, no `IMessageHandler<>`, no `[StreamsMessage]`/`[YieldsMessage]`");
        sb.AppendLine("2. Sessions: `AgentSession` via `CreateSessionAsync` — NOT `AgentThread` or `GetNewThread()`");
        sb.AppendLine("   Session-specific state: ALWAYS use `ProviderSessionState<T>` — NEVER instance fields on providers");
        sb.AppendLine("3. Fan-out/fan-in:");
        sb.AppendLine("   - Handler methods MUST return `ValueTask<T>` (not void) — void causes silent fan-in starvation");
        sb.AppendLine("   - `AddFanInBarrierEdge(sources, target)` — sources FIRST (arg order matters)");
        sb.AppendLine("4. Security: `ManagedIdentityCredential` (not `DefaultAzureCredential`); no `EnableSensitiveData = true` in production");
        sb.AppendLine("5. Streaming: `RunStreamingAsync()` — not `InProcessExecution.StreamAsync()`");
        sb.AppendLine("6. A2A: `services.AddA2AServer(...)` + `app.MapA2AHttpJson(path)` or `app.MapA2AJsonRpc(path)`");
        sb.AppendLine("7. Agent options: `Instructions` and `Tools` inside `ChatClientAgentOptions.ChatOptions`");
        sb.AppendLine();
        sb.AppendLine("Read maf://constraints before reviewing to ensure no hard constraint is violated.");
        sb.AppendLine("Report each finding with: Severity (🚨/⚠️/ℹ️) | Location | Issue | Fix");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-debug",
        Title = "MAF Debug: Diagnose Errors and Runtime Symptoms")]
    [Description(
        "Generates a starter prompt for reactive MAF debugging. " +
        "Use when something is broken — a compiler error, a runtime failure, or a silent incorrect result. " +
        "Directs the agent to the right diagnostic tool based on the symptom type.")]
    public static IList<PromptMessage> Debug(
        [Description("What is broken? Paste a compiler error, runtime exception, or describe the symptom (e.g. 'fan-in completes but output is empty').")] string? errorOrSymptom = null,
        [Description("Path to the .NET project or solution file, if relevant.")] string? projectPath = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are diagnosing a MAF 1.3.0 issue. Identify the root cause and provide the exact fix.");
        sb.AppendLine();

        if (errorOrSymptom is not null)
        {
            sb.AppendLine($"Error / Symptom:");
            sb.AppendLine(errorOrSymptom);
            sb.AppendLine();
        }
        if (projectPath is not null)
            sb.AppendLine($"Project: {projectPath}");

        sb.AppendLine("DIAGNOSTIC PLAYBOOK (pick the right tool for the symptom):");
        sb.AppendLine();
        sb.AppendLine("**Compiler errors (CS0618, CS0246, CS1061, CS8795):**");
        sb.AppendLine("  - `maf_explain_error \"<paste error text>\"` — root cause + exact fix (uses LLM)");
        sb.AppendLine("  - `maf_detect_cs0618 <projectPath>` — full build scan for all obsolete APIs");
        sb.AppendLine("  - `maf_api_safety <apiName>` — check if a specific API name is safe to call");
        sb.AppendLine("  - `maf_registry_lookup <id>` — look up a specific registry fix pattern");
        sb.AppendLine();
        sb.AppendLine("**Silent runtime failures (workflow completes but output is wrong/empty):**");
        sb.AppendLine("  - `maf_fan_out_validate <projectPath>` — checks ALL fan-out handlers return ValueTask<T>");
        sb.AppendLine("    Root cause: void or non-generic ValueTask return starves the fan-in barrier silently");
        sb.AppendLine("  - `maf_health_check <projectPath>` — comprehensive drift check across all MAF patterns");
        sb.AppendLine();
        sb.AppendLine("**Code uses wrong API / pattern not found:**");
        sb.AppendLine("  - `maf_explain_error \"<symptom>\"` — describe symptom in natural language");
        sb.AppendLine("  - `maf_health_check <projectPath>` — static analysis: critical/warning/info findings");
        sb.AppendLine("  - `maf_executor_pattern_check <projectPath>` — check for old executor patterns");
        sb.AppendLine();
        sb.AppendLine("**Known MAF 1.3.0 silent failure patterns:**");
        sb.AppendLine("  - Fan-out handler returns `void` or `ValueTask` (not `ValueTask<T>`) → fan-in barrier starves");
        sb.AppendLine("  - Session state in AIContextProvider instance fields → leaks across sessions");
        sb.AppendLine("  - DefaultAzureCredential used in production → fails on deployed infrastructure");
        sb.AppendLine("  - AddFanInBarrierEdge(target, sources) arg order → wrong topology, builds but runs wrong");
        sb.AppendLine();
        sb.AppendLine("Always read maf://constraints to verify the fix does not violate a hard constraint.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-scaffold",
        Title = "MAF Scaffold: Generate MAF 1.3.0 Boilerplate")]
    [Description(
        "Generates a starter prompt to scaffold correct MAF 1.3.0 boilerplate code. " +
        "Use when starting a new executor, session provider, workflow, or A2A server. " +
        "Generated code enforces all hard constraints inline.")]
    public static IList<PromptMessage> Scaffold(
        [Description("Template to generate: executor, session-provider, workflow, a2a-server")] string template,
        [Description("Class name prefix (e.g. 'Order' → OrderExecutor). Omit for default 'My'.")] string? className = null,
        [Description("For executor/workflow: input message type name (e.g. 'OrderRequest').")] string? inputType = null,
        [Description("For executor/workflow: output message type name (e.g. 'OrderResult').")] string? outputType = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generate MAF 1.3.0 boilerplate by calling the `maf_scaffold` tool with these parameters:");
        sb.AppendLine($"  template={template}");
        if (className is not null) sb.AppendLine($"  className={className}");
        if (inputType is not null) sb.AppendLine($"  inputType={inputType}");
        if (outputType is not null) sb.AppendLine($"  outputType={outputType}");
        sb.AppendLine();
        sb.AppendLine("After generating the code:");
        sb.AppendLine("1. Review the inline ✅ comments — each shows which MAF 1.3.0 constraint is being followed");
        sb.AppendLine("2. Replace all TODO comments with your actual implementation");
        sb.AppendLine("3. For executor/workflow templates: ensure Microsoft.Agents.AI.Workflows.Generators 1.3.0 is in your .csproj");
        sb.AppendLine("4. Run `maf_health_check <projectPath>` after implementing to verify no constraints are violated");
        sb.AppendLine();
        sb.AppendLine("Available templates:");
        sb.AppendLine("  executor         — sealed partial class : Executor with [MessageHandler] and ValueTask<T> return");
        sb.AppendLine("  session-provider — AIContextProvider with ProviderSessionState<T> (session-isolated state)");
        sb.AppendLine("  workflow         — fan-out/fan-in workflow builder with correct AddFanInBarrierEdge arg order");
        sb.AppendLine("  a2a-server       — Program.cs A2A registration: AddA2AServer + MapA2AHttpJson");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }
}
