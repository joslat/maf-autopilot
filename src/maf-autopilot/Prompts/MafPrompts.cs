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
        sb.AppendLine("3. Use MafApiSafety to validate any API you are unsure about.");
        sb.AppendLine("4. Use MafRegistryLookup to check if a specific CS0618 pattern already has a registered fix.");
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
        sb.AppendLine("- ALWAYS update the tracking table in migration-plan.md after each build-verified task.");
        sb.AppendLine();
        sb.AppendLine("EXECUTION LOOP — for each task:");
        sb.AppendLine("1. Apply the change.");
        sb.AppendLine("2. Run `dotnet build` and ensure it is green before mark-complete.");
        sb.AppendLine("3. If you touched CS0618 sites: call `MafRunCs0618Hunt(projectPath)` to confirm no warnings remain.");
        sb.AppendLine("4. If you touched an executor: call `MafValidateFanOut(<path>)` — silent fan-in starvation is the #1 class of bug this toolkit catches.");
        sb.AppendLine("5. After every 5 tasks (or at end-of-session): call `MafDoctor(repoPath)` for an A/B/C/F grade + top 3 remaining fixes.");
        sb.AppendLine();
        sb.AppendLine("Use `MafAuditPullRequest(repoPath, baseBranch)` before opening the migration PR — it runs every scanner only on the files this branch changed, so the comment stays scoped.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-cs0618-hunt",
        Title = "MAF CS0618: Find and Fix Obsolete APIs")]
    [Description(
        "Calls the MafRunCs0618Hunt tool to detect and fix every CS0618 / CS0246 " +
        "diagnostic in the project. Replaces the older 'run bash and parse output by hand' " +
        "procedure with a single deterministic tool call.")]
    public static IList<PromptMessage> Cs0618Hunt(
        [Description("Path to the .NET solution or project file.")] string projectPath)
    {
        // Rewritten 2026-05-12 (Phase F.1, post-Opus review). Previously the prompt
        // instructed the LLM to run `dotnet build | Select-String "CS0618"` — but
        // `MafRunCs0618Hunt` now automates that AND registry-joins each finding to
        // its canonical fix. Telling the LLM to do it by hand is a footgun: it skips
        // the registry join and produces a noisier, less-actionable report.
        var sb = new StringBuilder();
        sb.AppendLine($"Call `MafRunCs0618Hunt(projectPath: \"{projectPath}\")` to detect every CS0618 / CS0246 diagnostic in the project.");
        sb.AppendLine();
        sb.AppendLine("The tool runs `dotnet build`, parses every diagnostic, and joins each one to its canonical fix in the obsolete-API registry. For each finding the tool returns:");
        sb.AppendLine();
        sb.AppendLine("- File + line of the diagnostic");
        sb.AppendLine("- The exact warning / error message");
        sb.AppendLine("- The matching registry entry ID (e.g. `MAF130-THREAD-001`) when one exists");
        sb.AppendLine("- The deterministic fix to apply");
        sb.AppendLine();
        sb.AppendLine("For each finding:");
        sb.AppendLine("1. Read the registry entry via `MafRegistryLookup(<entry-id>)` if you need the full before/after example.");
        sb.AppendLine("2. Apply the documented fix.");
        sb.AppendLine("3. Re-run `MafRunCs0618Hunt` to confirm the warning is gone before moving to the next.");
        sb.AppendLine();
        sb.AppendLine("Cross-reference `maf://constraints` for any `AddFanInBarrierEdge` usage that doesn't surface a registry hit.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-help",
        Title = "MAF Help: Guided 3-question onboarding")]
    [Description(
        "3-question interactive flow for users who don't yet know which tool / agent fits their task. " +
        "Asks: (1) current MAF state, (2) immediate goal, (3) production context. Routes to the right entry point.")]
    public static IList<PromptMessage> Help()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are `@maf` triaging a new user. Walk them through the **3-question intake**:");
        sb.AppendLine();
        sb.AppendLine("**Q1.** What's the current MAF state in your codebase?");
        sb.AppendLine("- *(a)* Already on MAF 1.3.0 — clean build, no CS0618 warnings.");
        sb.AppendLine("- *(b)* Pre-1.3.0 (on 1.0/1.1/1.2) and want to upgrade.");
        sb.AppendLine("- *(c)* Greenfield — no MAF code yet, planning a new project.");
        sb.AppendLine("- *(d)* I don't know — help me find out.");
        sb.AppendLine();
        sb.AppendLine("**Q2.** What do you want to do next?");
        sb.AppendLine("- *(a)* Review / audit the code for anti-patterns.");
        sb.AppendLine("- *(b)* Plan or execute a version migration.");
        sb.AppendLine("- *(c)* Investigate a production failure / silent workflow exit.");
        sb.AppendLine("- *(d)* Scaffold a new agent or workflow executor.");
        sb.AppendLine("- *(e)* Understand the codebase (I just joined the team).");
        sb.AppendLine("- *(f)* File an upstream MAF bug.");
        sb.AppendLine();
        sb.AppendLine("**Q3.** Anything time-sensitive? (production incident / approaching release / blocking PR)");
        sb.AppendLine();
        sb.AppendLine("Once the user answers, route as follows:");
        sb.AppendLine();
        sb.AppendLine("| Q1 + Q2 combo | Route |");
        sb.AppendLine("|---|---|");
        sb.AppendLine("| (a)+(a) | `@maf-best-practice-reviewer` |");
        sb.AppendLine("| (b)+(a) or (b)+(b) | `@maf-auditor` → `@maf-migration` |");
        sb.AppendLine("| any+(c) | `@maf-incident-responder` |");
        sb.AppendLine("| (c)+(d) | Call `MafNewAgent` / `MafNewExecutor` directly |");
        sb.AppendLine("| any+(e) | `@maf-onboarding` |");
        sb.AppendLine("| any+(f) | Call `MafDraftIssue` directly |");
        sb.AppendLine("| (d) for Q1 | Call `MafDoctor(repoPath)` first to determine state, then re-route. |");
        sb.AppendLine();
        sb.AppendLine("**Always cite the route**: \"Based on your answers, I'm pointing you at `<X>` because <reason>.\" Never auto-handoff — the user @-mentions the specialist themselves.");
        sb.AppendLine();
        sb.AppendLine("If Q3 indicates time-sensitivity, prefix the route with a one-line `MafDoctor` call to surface the most-impactful fix first.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }
}
