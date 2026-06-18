# Using MAF Doctor — prompts, tools, agents, and natural language

After `maf-doctor init` (see [init-reference.md](./init-reference.md)) and an editor restart, there are three ways to drive the toolkit. Pick whichever fits the moment — they all hit the same engine.

| Way | Looks like | When |
|---|---|---|
| **Just ask** (natural language) | "Is `AgentThread` still safe? If not, fix it." | Default — the steering makes your assistant call the right tools for you. |
| **Slash-prompts** (MCP prompts) | `/maf-audit`, `/maf-migrate` | Kick off a structured workflow with one command. |
| **CLI** | `maf-doctor doctor .` | Scripts, CI, or when you're not in a chat. |

## 1. Natural language — the main mode

`init` dropped steering into your repo that tells the assistant: *for any MAF question or change, defer to the MAF Doctor tools over training data, because MAF ships breaking changes every minor version.* So you don't have to name tools — just describe the intent:

- *"Audit this repo for MAF problems and write me a migration plan."* → calls `MafScanAntiPatterns`, the registry, `MafDoctor`.
- *"Is `EnableSensitiveData = true` OK here?"* → `MafApiSafety` / `MafScanAntiPatterns`.
- *"Upgrade me to the latest MAF and fix what breaks."* → `MafPreUpgradeDryRun`, build, `MafAutoFixAll`, the migration guide.
- *"Does this workflow actually produce output, or does it silently starve?"* → `MafValidateFanOut` / `MafSimulateWorkflow`.
- *"Fix the obsolete-API warnings."* → `MafRunCs0618Hunt` → registry fix recipes.

If the assistant *doesn't* reach for the tools, it usually means the MCP server isn't connected — reload the editor, and confirm `maf-doctor` shows up (`/mcp` in Claude Code; the MCP panel in VS Code).

## 2. MCP prompts — structured starters (`/…`)

Prompts are pre-built workflows the server exposes; your client surfaces them as slash-commands (type `/` and look for the `maf-doctor` entries). Each one *is* the matching specialist persona delivered inline — no agent files needed.

| Prompt | Does | ≈ agent |
|---|---|---|
| `maf-audit` | Scan a codebase, cross-reference constraints + registry, emit a tracked `migration-plan.md` | `@maf-auditor` |
| `maf-migrate` | Execute migration tasks from a plan, build-verified per step | `@maf-migration` |
| `maf-cs0618-hunt` | Find + fix every CS0618 obsolete-API usage | — |
| `maf-review` | Best-practice review of already-migrated code | `@maf-best-practice-reviewer` |
| `maf-debug` | Diagnose a MAF failure → root cause + fix | `@maf-incident-responder` |
| `maf-scaffold` | Generate clean agent/executor boilerplate | — |
| `maf-help` | Triage: where are you, what do you need? | `@maf-onboarding` |

## 3. MCP tools — called for you

The server exposes a couple dozen executable tools, each annotated read-only / destructive / idempotent so clients can classify them. You rarely call them by name — the assistant does, driven by your request. Grouped:

- **Is this safe?** — `MafApiSafety`, `MafRegistryLookup`, `MafListRegistry`
- **Scan code** — `MafScanAntiPatterns`, `MafValidateFanOut`, `MafLintAgentPrompt`, `MafEstimateCost`
- **Compiler ground-truth** — `MafRunCs0618Hunt`
- **Upgrade / diff** — `MafDiffPackage`, `MafPreUpgradeDryRun`, `MafMigrationPath`, `MafScoreMigrationRisk`, `MafGenerateRegressionPlan`
- **Workflow topology** — `MafSimulateWorkflow` (+ Mermaid)
- **Scaffold** — `MafNewAgent`, `MafNewExecutor`
- **Fix** — `MafAutoFix`, `MafAutoFixAll` (both support `dryRun`)
- **Report** — `MafDoctor` (A/B/C/F health letter), `MafAuditPullRequest`, `MafExplain`, `MafBeforeAfter`, `MafCompatibility`, `MafDraftIssue`

> **Live catalogue:** ask the assistant to run **`MafTour`**, or read the **`maf://rules`** resource — both are generated from the actual code, so they never drift.

## 4. MCP resources — the knowledge base (`maf://…`)

Readable on demand; the assistant pulls them when relevant:

| URI | Contents |
|---|---|
| `maf://guide` | Full migration reference guide |
| `maf://constraints` | Hard constraints (read before any migration) |
| `maf://registry` | Obsolete-API registry (CS0618 → exact fix) |
| `maf://rules` | Live catalogue of every scanner + analyzer rule |
| `maf://help` | How to use the toolkit |
| `maf://skills?name=<name>` | Any of the 12 bundled skills |

## 5. Specialist agents (`@…`)

Seven persona agents live in this toolkit's `.github/agents/`: `@maf` (triage), `@maf-auditor`, `@maf-best-practice-reviewer`, `@maf-migration`, `@maf-incident-responder`, `@maf-onboarding`, `@maf-rollback`. They're richer, persistent personas you can `@`-mention mid-conversation.

`init` does **not** install them (see [init-reference.md](./init-reference.md)); to get the `@`-mention versions, copy this repo's `.github/` into your project (**Mode 2** in the README). For most tasks the MCP **prompts** above deliver the same workflows with nothing to install.

## 6. CLI commands

The same binary, with a subcommand, runs as a CLI (no chat needed):

```bash
maf-doctor doctor [path] [--exclude <s>]… [--all] [--json]   # A/B/C/F health report (--all = every finding; --json = machine output)
maf-doctor autofix-all [path] [--dry-run]   # apply deterministic Roslyn fixes
maf-doctor new agent <Name>                 # scaffold an agent + smoke test
maf-doctor new executor <Name> [In] [Out]   # scaffold a workflow executor
maf-doctor init [--with-cursor]             # wire MCP + steering into a repo
maf-doctor badge [path]                     # shields.io health-badge JSON
maf-doctor verify-registry                  # validate the registry (CI gate)
maf-doctor --version | --help               # version / usage
```

With **no** subcommand, the binary runs as the MCP server over stdio — that's what your editor launches.
