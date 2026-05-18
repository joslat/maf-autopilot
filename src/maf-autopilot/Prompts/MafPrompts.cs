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
        sb.AppendLine("OUTPUT: A complete migration-plan.md tracking table following the maf-migration-plan-creator skill format.");
        sb.AppendLine("Read maf://skills?name=maf-migration-plan-creator for the canonical template and task ID conventions.");

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

    // -------------------------------------------------------------------------
    // Salvaged from May-6 pre-Phase-O sketch (tag: pre-phase-o-may6-sketch).
    // Rewritten in local PascalCase / MCP-tool-name house style.
    // -------------------------------------------------------------------------

    [McpServerPrompt(Name = "maf-review",
        Title = "MAF Review: Best-Practices Code Review")]
    [Description(
        "Starter prompt for a MAF 1.3.0 best-practices code review (not migration). " +
        "Useful for day-to-day development, PR review, and post-migration validation. " +
        "Covers executors, sessions, fan-out topology, security, streaming, A2A.")]
    public static IList<PromptMessage> Review(
        [Description("Path to the file or directory to review. A .cs file path, a project directory, or omit for the current context.")] string? target = null,
        [Description("Optional focus area: executors / sessions / fan-out / security / streaming / a2a. Omit for full sweep.")] string? focusArea = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Review the supplied MAF 1.3.0 code for best-practice compliance. This is NOT a migration audit — focus on correctness + idiom for an already-migrated 1.3.0 codebase.");
        sb.AppendLine();
        if (target is not null) sb.AppendLine($"Target: `{target}`");
        if (focusArea is not null) sb.AppendLine($"Focus area: `{focusArea}`");
        sb.AppendLine();
        sb.AppendLine("**Suggested tool calls** (most-to-least lightweight):");
        sb.AppendLine();
        sb.AppendLine("- `MafDoctor(repoPath)` — A/B/C/F grade + top 3 fixes. Best triage signal.");
        sb.AppendLine("- `MafScanAntiPatterns(repoPath)` — 10-rule security/concurrency/observability scan.");
        sb.AppendLine("- `MafValidateFanOut(repoPath)` — silent fan-in starvation detector.");
        sb.AppendLine("- `MafLintAgentPrompt(repoPath)` — agent-prompt safety (empty / bloat / refusal / injection).");
        sb.AppendLine("- `MafEstimateCost(repoPath)` — token-cost auditor (missing `MaxOutputTokens` cap).");
        sb.AppendLine("- `MafExplain(snippet)` — annotate a suspicious snippet inline.");
        sb.AppendLine();
        sb.AppendLine("**Review checklist (MAF 1.3.0):**");
        sb.AppendLine("1. Executors: `sealed partial class : Executor` + `[MessageHandler]` on handlers. NO `ReflectingExecutor<T>`, NO `IMessageHandler<>`, NO `[StreamsMessage]` / `[YieldsMessage]`.");
        sb.AppendLine("2. Sessions: `AgentSession` via `CreateSessionAsync` (NOT `AgentThread.GetNewThread()`). State in `ProviderSessionState<T>`, NEVER instance fields on providers.");
        sb.AppendLine("3. Fan-out / fan-in: handlers MUST return `Task<T>` / `ValueTask<T>` / `IAsyncEnumerable<T>` (void or non-generic Task starves the barrier). `AddFanInBarrierEdge(sources, target)` — sources first.");
        sb.AppendLine("4. Security: `ManagedIdentityCredential` (NOT `DefaultAzureCredential`); no `EnableSensitiveData = true` outside dev. Roslyn analyzers `MAF002` / `MAF003` enforce at write-time.");
        sb.AppendLine("5. Streaming: `RunStreamingAsync()` (NOT `InProcessExecution.StreamAsync()`).");
        sb.AppendLine("6. A2A: `services.AddA2AServer(...)` + `app.MapA2AHttpJson(path)` (or `MapA2AJsonRpc`).");
        sb.AppendLine("7. Agent options: `Instructions` + `Tools` inside `ChatClientAgentOptions.ChatOptions` (top-level placement is silently ignored — `MAF-AP-AGENT-001`).");
        sb.AppendLine();
        sb.AppendLine("Read `maf://constraints` before reviewing — every hard rule lives there.");
        sb.AppendLine("Report each finding as: **Severity** (🚨 / ⚠️ / ℹ️) | **Location** | **Issue** | **Fix**.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-debug",
        Title = "MAF Debug: Diagnose Errors and Runtime Symptoms")]
    [Description(
        "Starter prompt for reactive MAF debugging. Use when something is broken — " +
        "compiler error, runtime exception, or silent-incorrect-result. Routes to the " +
        "right diagnostic tool based on the symptom class.")]
    public static IList<PromptMessage> Debug(
        [Description("What is broken? Paste a compiler error, runtime exception, or describe the symptom (e.g. 'fan-in completes but output is empty').")] string? errorOrSymptom = null,
        [Description("Path to the .NET project or solution, if relevant.")] string? projectPath = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are diagnosing a MAF 1.3.0 issue. Identify the root cause and provide the exact fix (or recommend the right specialist agent).");
        sb.AppendLine();
        if (errorOrSymptom is not null)
        {
            sb.AppendLine("**Error / Symptom:**");
            sb.AppendLine("> " + errorOrSymptom.Replace("\n", "\n> "));
            sb.AppendLine();
        }
        if (projectPath is not null) sb.AppendLine($"Project: `{projectPath}`");
        sb.AppendLine();
        sb.AppendLine("**Diagnostic playbook — pick by symptom class:**");
        sb.AppendLine();
        sb.AppendLine("**Compiler errors (CS0618 / CS0246 / CS1061):**");
        sb.AppendLine("- `MafRunCs0618Hunt(projectPath)` — full-build scan; joins each diagnostic to the obsolete-API registry with deterministic fix.");
        sb.AppendLine("- `MafApiSafety(apiName)` — is this specific symbol obsolete in 1.3.0?");
        sb.AppendLine("- `MafRegistryLookup(id)` — full registry entry once you have the ID.");
        sb.AppendLine();
        sb.AppendLine("**Silent runtime failures (workflow completes, output empty/wrong):**");
        sb.AppendLine("- `MafValidateFanOut(repoPath)` — checks every `[MessageHandler]` returns `Task<T>` / `ValueTask<T>` / `IAsyncEnumerable<T>`. Void / non-generic Task starves the fan-in barrier — the #1 silent-failure class.");
        sb.AppendLine("- `MafSimulateWorkflow(repoPath)` — Mermaid topology diagram + per-edge forecast. Shows where the message stops flowing.");
        sb.AppendLine();
        sb.AppendLine("**Wrong API / pattern not in any registry hit:**");
        sb.AppendLine("- `MafExplain(snippet)` — annotated walk through every MAF API touchpoint.");
        sb.AppendLine("- `MafDoctor(repoPath)` — aggregate A/B/C/F grade — quick triage.");
        sb.AppendLine();
        sb.AppendLine("**Production failure / exception in deployed code:**");
        sb.AppendLine("- Hand off to `@maf-incident-responder` — that agent's whole job is mapping prod symptoms to MAF patterns + the deterministic fix.");
        sb.AppendLine();
        sb.AppendLine("**Known silent-failure patterns:**");
        sb.AppendLine("- Fan-out handler returns `void` / non-generic Task → fan-in starves.");
        sb.AppendLine("- Session state in `AIContextProvider` instance fields → leaks across sessions (CONC-001).");
        sb.AppendLine("- `DefaultAzureCredential` in prod → walks stale dev tokens.");
        sb.AppendLine("- `AddFanInBarrierEdge(target, sources)` wrong argument order → wrong topology, runs but wrong.");
        sb.AppendLine("- `Instructions` outside `ChatOptions` → silently ignored.");
        sb.AppendLine();
        sb.AppendLine("Always read `maf://constraints` to verify the fix doesn't violate a hard constraint.");

        return [new PromptMessage { Role = Role.User, Content = new TextContentBlock { Text = sb.ToString() } }];
    }

    [McpServerPrompt(Name = "maf-scaffold",
        Title = "MAF Scaffold: Generate MAF 1.3.0 Boilerplate")]
    [Description(
        "Starter prompt for scaffolding MAF 1.3.0 boilerplate. Routes to the right " +
        "scaffolder MCP tool based on what you want to build.")]
    public static IList<PromptMessage> Scaffold(
        [Description("What to scaffold: \"agent\" (chat-client agent class) or \"executor\" (workflow message handler).")] string template = "agent",
        [Description("Class name (PascalCase). E.g. \"ChatBot\" → ChatBot agent or ChatBotExecutor.")] string? name = null,
        [Description("For executor only: incoming message type (default: string).")] string? inputType = null,
        [Description("For executor only: outgoing message type (default: string).")] string? outputType = null)
    {
        var sb = new StringBuilder();
        var t = (template ?? "agent").Trim().ToLowerInvariant();
        sb.AppendLine("Scaffold MAF 1.3.0 boilerplate that passes every scanner and analyzer out of the box.");
        sb.AppendLine();

        switch (t)
        {
            case "agent":
                sb.AppendLine($"**Call:** `MafNewAgent(projectPath: \"<your-project>\", agentName: \"{name ?? "MyAgent"}\", instructions: \"<optional system prompt>\")`");
                sb.AppendLine();
                sb.AppendLine("Generates:");
                sb.AppendLine("- `Agents/<Name>.cs` — `ChatClientAgent`-based class with `UseOpenTelemetry`, `MaxOutputTokens = 1024`, `Instructions` inside `ChatOptions` (the correct 1.3.0 placement)");
                sb.AppendLine("- `Tests/<Name>Tests.cs` — hermetic xUnit smoke test using a `ScriptedChatClient` mock (zero LLM cost)");
                sb.AppendLine();
                sb.AppendLine("Generator contract: the output passes `MafScanAntiPatterns` + `MafValidateFanOut` + compile-validation on first run.");
                break;
            case "executor":
                sb.AppendLine($"**Call:** `MafNewExecutor(projectPath: \"<your-project>\", executorName: \"{name ?? "MyExecutor"}\", inputType: \"{inputType ?? "string"}\", outputType: \"{outputType ?? "string"}\")`");
                sb.AppendLine();
                sb.AppendLine("Generates:");
                sb.AppendLine("- `Workflows/<Name>Executor.cs` — `sealed partial class : Executor` with `[MessageHandler]` returning `Task<TOutput>` (fan-out-safe by construction)");
                sb.AppendLine("- `Tests/<Name>ExecutorTests.cs` — reflection-based shape-verification test (fails at build time if return type ever regresses)");
                sb.AppendLine();
                sb.AppendLine("Type-expression validation: `inputType` and `outputType` are Roslyn-parsed + roundtrip-validated (security-pinned in `ScaffolderSecurityTests`).");
                break;
            default:
                sb.AppendLine($"⚠️ Unknown template `{template}`. Supported: `agent`, `executor`.");
                sb.AppendLine();
                sb.AppendLine("If you need a session-provider or A2A server scaffold (not yet auto-generated), follow the patterns in `maf://guide` §§7–9 or 17 respectively, and call `maf-review` after writing to verify.");
                break;
        }
        sb.AppendLine();
        sb.AppendLine("After generation:");
        sb.AppendLine("1. Run `dotnet build` — confirm 0 errors / 0 warnings.");
        sb.AppendLine("2. (Optional) Run `MafScanAntiPatterns(repoPath)` + `MafValidateFanOut(repoPath)` against the project — should be clean.");
        sb.AppendLine("3. Replace TODO comments in the smoke test with assertions specific to your handler.");

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
