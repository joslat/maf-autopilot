# maf-autopilot Workshop — Hands-On Migration Dogfood

> **Audience:** workshop attendees + maintainers running through the toolkit end-to-end on a real MAF 1.3.0 codebase. **~50 minutes** total.
>
> **What you'll do:** open the `maf-1.3-sample/` MAF 1.3.0 codebase standalone in VS Code Insiders, wire up the `maf-autopilot` MCP server, ask Copilot Chat to scan + plan + execute a migration, and verify the toolkit fixes every deliberate anti-pattern.

---

## What `maf-1.3-sample/` is

The `samples/maf-1.3-sample/` folder is a small but realistic **multi-agent MAF 1.3.0 codebase** (FraudClaimsTriage — insurance fraud workflow) that **intentionally embeds anti-patterns** the maf-autopilot toolkit was built to find. **Every "broken" pattern is deliberate.** See [`samples/maf-1.3-sample/README.md`](./maf-1.3-sample/README.md) for the full coverage list.

Expected toolkit verdict on the sample as it ships:

```
🔴 MAF health grade: F
10 anti-pattern errors + 3 warnings + 3 silent-starvation risks
```

After the workshop, the toolkit migrates the sample and the grade should rise to **A or B**.

---

## Prerequisites

| Tool | Why | Install |
|---|---|---|
| **VS Code Insiders** (or stable) | MCP server + Copilot Chat client | https://code.visualstudio.com/insiders/ |
| **GitHub Copilot subscription** | needed to use `@maf-*` agents and `/maf-*` prompts | any Copilot tier |
| **.NET 8 SDK or later** | builds the sample (sample targets `net8.0`) | https://dotnet.microsoft.com/download |
| **`maf-autopilot` global tool** (prerelease) | the MCP server itself | `dotnet tool install -g maf-autopilot --prerelease` |
| _(optional)_ Azure OpenAI deployment | only needed for `--run` mode of the sample; not needed for the workshop | https://portal.azure.com |

Verify the tool is on your PATH — installing only if it's missing.

**bash / zsh:**

```bash
if ! command -v maf-autopilot >/dev/null 2>&1; then
  echo "  maf-autopilot not found on PATH — installing as a global tool..."
  dotnet tool install -g maf-autopilot --prerelease
  # If the install succeeds but `command -v` still misses, your shell needs to
  # pick up ~/.dotnet/tools. Either restart the shell or run:
  #   export PATH="$HOME/.dotnet/tools:$PATH"
fi
maf-autopilot --version
# → maf-autopilot 1.3.0-alpha-5 (or later)
```

**PowerShell (Windows / cross-platform):**

```powershell
if (-not (Get-Command maf-autopilot -ErrorAction SilentlyContinue)) {
    Write-Host "  maf-autopilot not found on PATH — installing as a global tool..."
    dotnet tool install -g maf-autopilot --prerelease
    # PowerShell picks up new PATH entries on next session; in this session
    # just run dotnet tool list -g to confirm.
}
maf-autopilot --version
# → maf-autopilot 1.3.0-alpha-5 (or later)
```

> **Already installed?** Run `dotnet tool update -g maf-autopilot --prerelease` to take the
> latest 1.3.0-alpha. Stable 1.3.0 publishes the workshop-validated build.

---

## Step 1 — Open the sample STANDALONE in VS Code Insiders

The workshop opens **only the sample folder**, not the full maf-autopilot repo. This is how a real customer would experience the toolkit — they don't have the maf-autopilot source on their machine, just the installed global tool.

```bash
# From wherever you cloned this repo:
code-insiders samples/maf-1.3-sample/
```

Or in Windows / via the GUI: **File → Open Folder…** and pick `samples/maf-1.3-sample`.

You should see this layout in the Explorer pane:

```
maf-1.3-sample/
├── Agents/
│   ├── DecisionAgent.cs
│   └── IntakeAgent.cs
├── Executors/
│   ├── AggregatorExecutor.cs
│   ├── HistoryInvestigator.cs
│   ├── NotificationExecutor.cs
│   ├── OsintInvestigator.cs
│   └── TransactionInvestigator.cs
├── Providers/
│   └── CaseContextProvider.cs
├── Workflows/
│   └── FraudClaimsWorkflow.cs
├── ChatClientFactory.cs
├── Config.cs
├── MafSample.FraudClaims.csproj
├── Models.cs
├── Program.cs
└── README.md
```

---

## Step 2 — Wire up the MCP server

Create `.vscode/mcp.json` inside the sample folder:

```json
{
  "servers": {
    "maf-autopilot": {
      "type": "stdio",
      "command": "maf-autopilot",
      "args": []
    }
  }
}
```

VS Code Insiders auto-detects this file. After saving, open Copilot Chat and verify:

- The `@maf` agent appears in `@`-mention completions.
- The `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt` slash commands appear in `/`-completions.
- `maf://constraints`, `maf://guide`, `maf://registry`, `maf://rules`, `maf://skills?name=…` are referenceable as resources.

> **Troubleshooting:** if no MCP is detected, check `View → Output → MCP` for the server log. Most commonly: `maf-autopilot` is not on PATH — re-run `dotnet tool install -g maf-autopilot --prerelease` and reopen VS Code.

---

## Step 3 — Smell test before the toolkit runs

Build and run the sample to set a baseline:

```bash
dotnet build
# → 1 Warning(s) (CS0618 from the obsolete fan-in overload), 0 Error(s)

dotnet run
# → prints the topology + the deliberate-anti-pattern list, exits 0
```

The `CS0618` warning is **deliberate** — it's the obsolete `AddFanInBarrierEdge(target, sources)` overload. The toolkit's `MafRunCs0618Hunt` should find this exact call site shortly.

---

## Step 4 — Ask Copilot Chat for a health grade

In Copilot Chat (still inside the sample folder):

```
@maf doctor — what's the health grade of this codebase?
```

Expected response (paraphrased — Copilot will route to `MafDoctor` and summarise):

```
🔴 MAF health grade: F

10 anti-pattern errors + 3 silent-starvation risk(s)

Top fixes (ordered by impact):
  1. HandleAsync at Executors/HistoryInvestigator.cs:19   returns ValueTask
  2. HandleAsync at Executors/OsintInvestigator.cs:23     returns ValueTask
  3. HandleAsync at Executors/TransactionInvestigator.cs:21 returns ValueTask

Run `MafScanAntiPatterns(repoPath)` for the full breakdown.
```

> **What the toolkit is doing under the hood:** the `MafDoctor` MCP tool composes 4 scanners — anti-pattern (Roslyn + regex), fan-out validator (Roslyn), prompt-lint, and token-cap auditor — into a single weighted grade.

---

## Step 5 — Drill into specific scanners

Run these one by one in Copilot Chat to see each MCP tool surface its findings:

| Ask Copilot | Tool invoked | What you'll see |
|---|---|---|
| "show me every anti-pattern in this code" | `MafScanAntiPatterns` | 10 errors + 3 warnings, each with file + line + rule ID |
| "check the fan-out executors for silent-starvation risks" | `MafValidateFanOut` | The 3 investigators flagged — `HandleAsync` returns `ValueTask` not `ValueTask<T>` |
| "diagram the workflow topology" | `MafSimulateWorkflow` | Mermaid graph: intake → fan-out → fan-in → decision → notify |
| "find any CS0618 warnings the compiler reports" | `MafRunCs0618Hunt` | `WorkflowBuilder.AddFanInBarrierEdge` obsolete overload at `Workflows/FraudClaimsWorkflow.cs:50` |
| "audit the workflow for token-cap defaults" | `MafAuditPullRequest` (or `MafScanAntiPatterns`) | 0 RunAsync sites — the sample's `--run` path is a stub |

Each scanner is independent; you can run any of them at any time on any codebase that has MAF code.

---

## Step 6 — Plan the migration

Ask Copilot to plan the fix-up:

```
@maf-auditor scan this codebase and write a migration plan to docs/migration-plan.md
```

Expected: a markdown plan with one section per finding, each citing the registry entry (e.g. `MAF130-FAN-IN-001`) and showing before/after code. The plan should match the embedded-anti-pattern list in `README.md`.

> **Phase T.6 caveat:** as of 2026-05-13, the registry includes 3 entries (`MAF130-SESSION-001`, `MAF130-INSTRUCTIONS-001`, `MAF130-MIDDLEWARE-001`) that describe pre-1.3.0 surfaces or wrong parameter names. These won't trigger on the sample (the patterns can't be reproduced on 1.3.0), but if the auditor mentions them generally, treat the advice with caution. See [`samples/maf-1.3-sample/README.md` § Phase T registry corrections](./maf-1.3-sample/README.md#phase-t-registry-corrections-real-findings-from-this-dogfood).

---

## Step 7 — Execute the migration

```
@maf-migration execute the plan in docs/migration-plan.md, with dotnet build gates between each task
```

Watch each task land. The agent should:

1. Add `sealed` to `TransactionInvestigator`.
2. Change all 3 investigators' `HandleAsync` returns from `ValueTask` → `ValueTask<InvestigationFinding>`.
3. Flip the `AddFanInBarrierEdge` arg order (sources first, target second).
4. Replace `new DefaultAzureCredential()` with `new ManagedIdentityCredential()`.
5. Remove `EnableSensitiveData = true`.
6. Add `.UseOpenTelemetry()` to the chat-client pipeline.
7. Lift the mutable fields off `CaseContextProvider` into `ProviderSessionState<T>`.
8. Eliminate the `.Result` blocking call in `NotificationExecutor`.
9. Wire both branches of the `.Use(...)` middleware.
10. Delete the hard-coded `api-key=…` string literal.

After each task, `dotnet build` should stay green (modulo the deliberate CS0618 which goes away after task 3).

---

## Step 8 — Verify

Re-run the doctor:

```
@maf doctor — what's the health grade now?
```

Expected:

```
🟢 MAF health grade: A     (or 🟡 B if minor warnings remain)
0 anti-pattern errors, 0 silent-starvation risks
```

And on the command line:

```bash
dotnet build
# → 0 Warning(s), 0 Error(s)

dotnet run
# → still prints a topology summary; the embedded-anti-pattern list should be empty
```

---

## Cleanup / Reset

Throw away all the migration's changes and return the sample to its broken-for-the-next-attendee state:

```bash
git restore samples/maf-1.3-sample/
rm -rf samples/maf-1.3-sample/.vscode
rm -f samples/maf-1.3-sample/docs/migration-plan.md
```

(Or just `git stash` if you want to compare later.)

---

## Takeaways

- The sample is a **fixed-cost regression artefact** — every future MAF version (1.4, 1.5, …) can be regression-tested against the same sample by re-running the migration loop.
- The toolkit detects **10 distinct anti-pattern errors + 3 silent-starvation risks** from a single ~150-LoC codebase.
- The whole detect → plan → fix → verify loop runs in **~30 minutes of chat interaction**.
- The 3 registry-drift findings recorded during Phase T are the kind of corrections that only surface from real-world dogfooding — exactly the value-add Phase T was meant to produce.

---

## Where to go next

- **Add your own anti-patterns.** Fork the sample, edit `MafSample.FraudClaims.csproj` to target a newer MAF (1.4 / 1.5) and add the relevant breaking-pattern examples. Re-run the workshop steps to test the toolkit against the newer surface.
- **Try the analyzer NuGet** instead of (or in addition to) the MCP server: `dotnet add package maf-autopilot.Analyzers --prerelease`. The analyzer surfaces a subset of the rules at write-time inside any consumer csproj.
- **Read the MAF migration guides** in [`/guides/`](../guides/) — `maf-1.3.0`, `maf-1.4.0`, `maf-1.5.0`, plus the auto-regenerated cumulative `maf-current-migration-guide.md`.
- **Browse the agents:** the Copilot Chat agents (`@maf`, `@maf-auditor`, `@maf-migration`, `@maf-best-practice-reviewer`, `@maf-incident-responder`, `@maf-rollback`, `@maf-onboarding`) cover the full migration lifecycle.

License: MIT. Workshop content reusable; the sample's deliberate anti-patterns are NOT.
