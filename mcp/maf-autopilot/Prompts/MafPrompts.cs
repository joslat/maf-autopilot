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
        sb.AppendLine("- For CS0618 warnings: use the cs0618-hunter skill workflow, NOT dotnet-inspect alone.");
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
        "using the cs0618-hunter skill. Compiler-based detection — not dotnet-inspect.")]
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
        sb.AppendLine("4. Do NOT use dotnet-inspect to detect [Obsolete] — it does not flag overload-level deprecations.");
        sb.AppendLine();
        sb.AppendLine("Cross-reference with the hard constraints at maf://constraints for any AddFanInBarrierEdge usage.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }
}
