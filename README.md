# maf-autopilot

> Not just a migration tool — your permanent AI co-pilot for MAF. Migrate to 1.3.0, verify your code follows best practices, catch obsolete APIs before they bite you, and stay current automatically as MAF evolves.

**maf-autopilot** is an MCP server that turns GitHub Copilot into a MAF expert for the lifetime of your project. One install, one `init`, and Copilot gains live access to the full knowledge base — API signatures, breaking change rules, a machine-readable fix registry, and 11 specialized skills — wired up as agentic tool calls it invokes automatically while auditing, migrating, reviewing, and maintaining your agents and workflows.

### What makes it worth trying

- 🔍 **Catches the bugs that compile clean** — detects fan-out/fan-in silent failures where a workflow exits successfully but produces no output. Runtime-only, zero build signal, invisible without this tool.
- 🩺 **CS0618 detection — project-wide in one pass** — `dotnet-inspect` can inspect individual overloads for `[Obsolete]` (issue #316 resolved), but `maf-autopilot` uses the compiler directly to find every CS0618 warning across an entire solution in a single build pass. Faster and more comprehensive.
- ✅ **Best-practice auditing, not just migration** — reviews your MAF agents and workflows against current patterns after migration too. Catches drift, anti-patterns, and deprecated usage before they become production bugs.
- 🤖 **Fully agentic loop** — Copilot audits your codebase, generates a tracked migration plan, executes it task-by-task with `dotnet build` verification after every step, and updates the plan as it goes.
- 📖 **Self-updating knowledge** — a GitHub Actions workflow watches NuGet weekly, diffs the MAF API surface, and opens a PR updating the migration guide and compatibility matrix automatically when a new version ships.
- 🧰 **Deterministic fixes, not guesses** — a machine-readable Obsolete API Registry maps every known CS0618 warning to its exact replacement pattern. No hallucinated fix patterns.
- 🔄 **Keeps your code current** — as MAF evolves, re-run the auditor on your codebase. It cross-references the latest guide, flags anything that's now obsolete or has a better pattern, and tells you exactly what to change.

## How It Works

`maf-autopilot` is a **[Model Context Protocol (MCP)](https://modelcontextprotocol.io) server** packaged as a .NET global tool. When running, it exposes:

- **Tools (16)** — callable by Copilot automatically or on request:
  - *Static analysis:* `maf_health_check`, `maf_detect_cs0618`, `maf_api_diff`, `maf_fan_out_validate`, `maf_executor_pattern_check`, `maf_compatibility`, `maf_migration_path`
  - *Registry:* `maf_api_safety`, `maf_registry_lookup`, `maf_registry_list`
  - *LLM sampling:* `maf_full_audit`, `maf_migration_suggest`, `maf_review_code`, `maf_explain_error`
  - *Generation:* `maf_scaffold`
  - *Feedback:* `maf_open_feedback_issue`
- **Resources** — `maf://guide`, `maf://constraints`, `maf://registry`, `maf://skills?name=...` — the full migration knowledge base, readable on demand
- **Prompts (6)** — invoked as `/maf-*` commands in Copilot Chat: `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt`, `/maf-review`, `/maf-debug`, `/maf-scaffold`

GitHub Copilot Chat connects to the MCP server and gains agentic access to all of this during migration work.

## Quick Start — Two Integration Modes

### ⭐ Mode 1: MCP Server (recommended — full agentic power)

Install the NuGet global tool, then run `init` in your target project:

```powershell
# 1. Install the MCP server as a .NET global tool
dotnet tool install --global maf-autopilot

# 2. In your MAF project — writes .vscode/mcp.json + .github/copilot-instructions.md
cd your-maf-project
maf-autopilot init
```

VS Code picks up `.vscode/mcp.json` automatically and starts the MCP server. Copilot Chat gains live tool calls, resource reads, and structured prompts.

### Mode 2: Skills + Agents only (no MCP server required)

Copy `.github/` from this repo into your .NET repository root. You get the agents and skills via VS Code's instruction/agent system — no MCP server needed, but no live tool calls either.

```powershell
# In your MAF project root
git clone https://github.com/joslat/maf-autopilot tmp-maf
Copy-Item tmp-maf/.github -Destination .github -Recurse
Remove-Item tmp-maf -Recurse
```

Then open Copilot Chat and use `@maf-migration` or `@maf-auditor`.

## Why This Exists

`dotnet upgrade-assistant` handles package version bumps. maf-autopilot handles everything else:

- MAF-specific executor pattern migration (`ReflectingExecutor` → `partial class : Executor` + `[MessageHandler]`)
- Session API changes (`AgentThread` → `AgentSession`)
- Fan-out/fan-in silent failure detection — a class of bug that builds green but runs wrong
- CS0618 detection across your project source — `dotnet-inspect` (issue #316 resolved) inspects NuGet package API surfaces; `maf-autopilot` runs the compiler on your source to find every warning call site with `file:line` output
- Build-verified, task-by-task execution with a full tracking table
- A machine-readable Obsolete API Registry so fixes are deterministic, not guessed

---

## Project Structure

```
maf-autopilot/
├── .github/
│   ├── agents/
│   │   ├── maf-migration.agent.md       # Main migration orchestrator
│   │   └── maf-auditor.agent.md         # Audits a codebase → generates migration plan
│   ├── instructions/
│   │   └── maf-constraints.instructions.md  # Always-loaded: breaking changes, constraints
│   ├── skills/
│   │   ├── dotnet-inspect/              # NuGet API surface inspection; [Obsolete] at overload level now surfaced (issue #316 resolved)
│   │   ├── maf-migration-guide/         # Full MAF 1.3.0 guide, navigated by section
│   │   ├── obsolete-api-registry/       # Machine-readable CS0618 fix registry
│   │   ├── migration-plan-creator/      # Generates migration plans with tracking tables
│   │   ├── cs0618-hunter/               # Detects + fixes obsolete API warnings
│   │   ├── fan-out-validator/           # Validates fan-out/fan-in workflow topology (conceptual)
│   │   ├── fan-in-static-analyzer/      # Static code analysis of fan-out topology (code walking)
│   │   ├── nuget-diff-analyzer/         # Post-processes dotnet-inspect diff into actionable report
│   │   ├── maf-release-watcher/         # Detects new MAF releases, updates guide + registry
│   │   ├── workflow-smoke-tester/       # Generates structural smoke tests for workflow patterns
│   │   └── migration-retrospective/     # Post-migration learning → improves the toolkit
│   └── workflows/
│       └── maf-release-watcher.yml      # GitHub Actions: weekly MAF version check + PR
├── mcp/
│   └── maf-autopilot/                   # MCP server — NuGet global tool (dotnet tool install --global maf-autopilot)
├── guides/
│   └── maf-1.3.0-migration-guide.md    # Source-of-truth MAF 1.3.0 migration guide (29 sections)
├── docs/
│   ├── maf-migration-toolkit-plan.md   # Full project plan and design decisions
│   ├── compatibility-matrix.md         # MAF version ↔ dependency version table
│   └── setup.md                        # Developer setup and local build instructions
└── .maf-version                         # Tracks current MAF version (for release watcher)
```

---

## Skill Orchestration — When to Call What

| You need to…                                      | Use skill                      |
|---------------------------------------------------|--------------------------------|
| Look up MAF API signatures or patterns            | `maf-migration-guide`          |
| Check if a NuGet type/member exists               | `dotnet-inspect`               |
| Detect and fix CS0618 obsolete warnings           | `cs0618-hunter`                |
| Look up the fix pattern for a specific CS0618     | `obsolete-api-registry`        |
| Generate a migration plan for a new codebase      | `migration-plan-creator`       |
| Validate fan-out/fan-in workflow structure        | `fan-out-validator`            |
| Static code analysis of fan-out handler types     | `fan-in-static-analyzer`       |
| Post-process NuGet diff into actionable report    | `nuget-diff-analyzer`          |
| Check for / respond to new MAF versions           | `maf-release-watcher`          |
| Generate smoke tests for migrated workflows       | `workflow-smoke-tester`        |
| Learn from migration surprises, improve toolkit   | `migration-retrospective`      |

> **Never** use `dotnet-inspect` as the sole way to check for obsolete APIs across a whole project — a compiler scan (`dotnet build | Select-String CS0618`) is faster and covers everything at once. `dotnet-inspect` is great for targeted per-member `[Obsolete]` lookup (issue #316 is now resolved).

---

## MCP Tool & Prompt Reference

### Tools (16)

| Tool | Category | What it does |
|------|----------|--------------|
| `maf_health_check` | Analysis | Comprehensive static pattern scan across a project — checks all known pre-1.3.0 patterns. Returns 🚨/⚠️/ℹ️ severity. No LLM required. |
| `maf_detect_cs0618` | Analysis | Runs `dotnet build` and parses CS0618/CS0246 warnings. Cross-references each against the registry for the exact fix pattern with `file:line` output. |
| `maf_api_diff` | Analysis | Invokes `dotnet-inspect diff` via `dnx` to compare a MAF package API surface between two versions. Surfaces removed types, renamed members, and signature changes. |
| `maf_fan_out_validate` | Analysis | Searches executor files for `[MessageHandler]` handlers returning `void` or non-generic `ValueTask` — the silent fan-in starvation failure that builds clean. |
| `maf_executor_pattern_check` | Analysis | Searches source files for pre-1.3.0 executor patterns: `ReflectingExecutor<T>`, `IMessageHandler<TIn,TOut>`. |
| `maf_compatibility` | Analysis | Returns MAF version compatibility info — package matrix, known issues for a given version. |
| `maf_migration_path` | Analysis | Returns the recommended step-by-step migration path between two MAF versions. |
| `maf_api_safety` | Registry | Checks whether a MAF API (method name, type, or partial call) is safe in MAF 1.3.0. Returns SAFE or UNSAFE with the registry entry and fix. |
| `maf_registry_lookup` | Registry | Retrieves full details for a specific registry entry by ID (e.g. `MAF130-FAN-IN-001`): signatures, before/after code, fix description. |
| `maf_registry_list` | Registry | Lists all entry IDs in the obsolete-API registry. |
| `maf_full_audit` | LLM (sampling) | Scans a repository for MAF patterns, then uses the host LLM to generate a complete `migration-plan.md` tracking table ready to execute. |
| `maf_migration_suggest` | LLM (sampling) | Given a task description or code snippet, suggests the correct MAF 1.3.0 migration with before/after examples. |
| `maf_review_code` | LLM (sampling) | LLM-powered best-practices code review for any file path or pasted snippet. |
| `maf_explain_error` | LLM (sampling) | Paste any compiler error or runtime symptom → MAF 1.3.0 root cause + exact fix. |
| `maf_scaffold` | Generation | Generates correct MAF 1.3.0 boilerplate: `executor`, `session-provider`, `workflow`, or `a2a-server`. Deterministic, no LLM. |
| `maf_open_feedback_issue` | Feedback | Analyzes a completed migration via LLM and drafts a GitHub Issue capturing new patterns, doc errors, and silent failures for the registry and guide. |

### Prompts (6)

| Prompt | Invoke as | What it does |
|--------|-----------|--------------|
| `maf-audit` | `/maf-audit repoPath="..."` | Starts the MAF Auditor Agent — scans the codebase and generates `migration-plan.md` with a full tracking table. |
| `maf-migrate` | `/maf-migrate taskIds="1,2,3"` | Starts the MAF Migration Agent — executes specific tasks from a migration plan with `dotnet build` verification after each step. |
| `maf-cs0618-hunt` | `/maf-cs0618-hunt projectPath="..."` | Compiler scan for CS0618 obsolete API warnings — one pass, all files, every call site with `file:line` output. |
| `maf-review` | `/maf-review [target="MyFile.cs"]` | Best-practices code review on any file or project — checks for anti-patterns, deprecated usage, and drift. |
| `maf-debug` | `/maf-debug errorOrSymptom="..."` | Routes any compiler error or runtime symptom to the right diagnostic tool automatically. |
| `maf-scaffold` | `/maf-scaffold template=executor ...` | Generates correct MAF 1.3.0 boilerplate code from a named template. |

---

## Typical Migration Workflow

**Migration:**
1. **Audit** — `/maf-audit repoPath="C:\your\project"` — scans the codebase, generates `migration-plan.md`
2. **Migrate** — `/maf-migrate taskIds="1,2,3"` — executes tasks with `dotnet build` verification after each step
3. **Hunt stragglers** — `/maf-cs0618-hunt projectPath="..."` — compiler scan for remaining CS0618 warnings

**Ongoing development:**
4. **Review** — `/maf-review [target="MyFile.cs"] [focusArea=fan-out]` — best-practices check on any file or project
5. **Debug** — `/maf-debug errorOrSymptom="fan-in is silent"` — routes to the right diagnostic tool automatically
6. **Scaffold** — `/maf-scaffold template=executor className=Order inputType=OrderRequest outputType=OrderResult` — generate correct MAF 1.3.0 boilerplate

The `/maf-*` prompts are the entry points. MCP tools are called automatically by Copilot during agent runs.

---

## Roadmap

- [x] `maf-migration.agent.md` — full migration orchestrator with build-verified task loop
- [x] `maf-auditor.agent.md` — pre-migration plan generator
- [x] `maf-constraints.instructions.md` — always-loaded breaking changes + hard rules
- [x] `cs0618-hunter` — compiler-based CS0618 detection
- [x] `fan-out-validator` — silent fan-in starvation detection
- [x] `obsolete-api-registry` — machine-readable CS0618 registry (10 entries)
- [x] `migration-plan-creator` — plan template + generation instructions
- [x] `maf-migration-guide` — 29-section guide navigator
- [x] `dotnet-inspect` skill — NuGet API surface inspection; `[Obsolete]` at overload level now surfaced (issue #316 resolved)
- [x] `guides/maf-1.3.0-migration-guide.md` — full reference guide
- [x] `docs/compatibility-matrix.md` — MAF version ↔ dependency versions
- [x] `nuget-diff-analyzer` — structured diff post-processing skill
- [x] `maf-release-watcher` skill — manual + automated update workflow
- [x] `.github/workflows/maf-release-watcher.yml` — GitHub Actions weekly check
- [x] `fan-in-static-analyzer` — static code analysis of fan-out topology
- [x] `workflow-smoke-tester` — post-migration structural smoke test generation
- [x] `migration-retrospective` — post-migration learning loop
- [x] `maf-autopilot` MCP server — tools + resources + prompts, published to NuGet as `maf-autopilot` global tool
- [x] Sampling tools (`maf_full_audit`, `maf_migration_suggest`) — LLM-driven audit and migration suggestions via `server.SampleAsync()`
- [x] Full analysis tools (`maf_detect_cs0618`, `maf_api_diff`, `maf_fan_out_validate`, `maf_executor_pattern_check`, `maf_compatibility`, `maf_migration_path`)
- [x] `maf_health_check` — comprehensive static pattern scan (🚨/⚠️/ℹ️ by area, no LLM required)
- [x] `maf_review_code` — LLM-powered best-practices code review for any file or snippet
- [x] `maf_explain_error` — paste any compiler error or runtime symptom → MAF 1.3.0 root cause + exact fix
- [x] `maf_scaffold` — deterministic boilerplate generator: executor, session-provider, workflow, a2a-server
- [x] Feedback tool (`maf_open_feedback_issue`) — post-migration learning loop
- [x] Prompts expanded to 6: `/maf-review`, `/maf-debug`, `/maf-scaffold`
- [ ] Docker distribution via GHCR
- [ ] Roslyn analyzer companion — flags fan-out void handlers at write-time
- [ ] Multi-version migration paths (e.g., 1.1.0 → 1.3.0 in one pass)

---

## Acknowledgements

This toolkit relies heavily on **[dotnet-inspect](https://github.com/richlander/dotnet-inspect)** by [Rich Lander](https://github.com/richlander) — a powerful CLI for querying .NET API surfaces across NuGet packages, platform libraries, and local assemblies. The `nuget-diff-analyzer` skill and the `maf-release-watcher` workflow both use `dotnet-inspect` (via `dnx`) to diff MAF package versions and surface breaking API changes.

> One important note: `dotnet-inspect` is best used for targeted per-member API lookups. For project-wide CS0618 detection across an entire solution, use `maf_detect_cs0618` or the `cs0618-hunter` skill — a single compiler pass covers all files at once.

---

## Contributing

Whenever a migration surfaces a new surprise (unexpected breaking change, silent runtime failure, wrong pattern in the official docs), update:
1. `guides/maf-1.3.0-migration-guide.md` — add to "Known Misalignments" or create a new section
2. `.github/skills/obsolete-api-registry/registry.yaml` — add the CS0618 entry
3. `.github/instructions/maf-constraints.instructions.md` — update the breaking changes table if needed
