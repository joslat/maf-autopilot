# MAF Doctor — the maf-autopilot toolkit for Microsoft Agent Framework

> Diagnose, explain, prescribe, verify — for MAF agents and workflows.

> **Why two names?** **MAF Doctor** is the brand — what the toolkit *does*. **`maf-autopilot`** is the NuGet package ID and CLI binary name. They're the same thing; we kept the package ID stable to avoid breaking existing installs.

<!--
  Two animated casts (each ~30 s). Rendered from docs/assets/*.tape via vhs.
  Re-render after editing the .tape files: bash scripts/make-cast.sh (or .ps1).

  If a GIF appears broken/missing, the renderer hasn't run yet —
  see scripts/make-cast.sh for the install + render instructions.
-->
<p align="center">
  <img src="docs/assets/install-cast.gif"
       alt="maf-autopilot — install + audit any MAF codebase in 30 seconds"
       width="900" />
  <br/>
  <em>Install + audit a real MAF codebase. Doctor returns grade F + the top fixes.</em>
</p>

<p align="center">
  <img src="docs/assets/migration-cast.gif"
       alt="maf-autopilot — auto-fix the broken codebase to grade A in ~30 seconds"
       width="900" />
  <br/>
  <em>One <code>autofix-all</code> command. Every mechanical rule. Grade F → A, deterministic.</em>
</p>

**MAF Doctor** (the maf-autopilot toolkit) is an MCP server that turns GitHub Copilot into a MAF expert for the lifetime of your project. One install, one `init`, and Copilot gains live access to the full knowledge base — API signatures, breaking change rules, a 13-entry machine-readable fix registry, and 12 specialised skills — wired up as **17 executable MCP tools** + 3 Roslyn analyzers for write-time enforcement. Copilot invokes them automatically while auditing, migrating, reviewing, and maintaining your agents and workflows.

### What makes it worth trying

- 🔍 **Catches the bugs that compile clean** — detects fan-out/fan-in silent failures where a workflow exits successfully but produces no output. Runtime-only, zero build signal, invisible without this tool.
- 🩺 **Compiler ground-truth for CS0618** — `dotnet-inspect` v0.7.8 surfaces `[Obsolete]` statically (pre-build), but only the compiler catches transitive obsoletions, overload-resolution surprises, and project-local `[Obsolete]` attributes. `maf-autopilot` pairs both.
- ✅ **Best-practice auditing, not just migration** — reviews your MAF agents and workflows against current patterns after migration too. Catches drift, anti-patterns, and deprecated usage before they become production bugs.
- 🤖 **Fully agentic loop** — Copilot audits your codebase, generates a tracked migration plan, executes it task-by-task with `dotnet build` verification after every step, and updates the plan as it goes.
- 📖 **Self-updating knowledge** — a GitHub Actions workflow watches NuGet weekly, diffs the MAF API surface, and opens a PR updating the migration guide and compatibility matrix automatically when a new version ships.
- 🧰 **Deterministic fixes, not guesses** — a machine-readable Obsolete API Registry maps every known CS0618 warning to its exact replacement pattern. No hallucinated fix patterns.
- 🔄 **Keeps your code current** — as MAF evolves, re-run the auditor on your codebase. It cross-references the latest guide, flags anything that's now obsolete or has a better pattern, and tells you exactly what to change.

## How It Works

`maf-autopilot` is a **[Model Context Protocol (MCP)](https://modelcontextprotocol.io) server** packaged as a .NET global tool. When running, it exposes:

- **17 executable tools** — registry lookup (`MafApiSafety`, `MafRegistryLookup`, `MafRegistryList`), code scanning (`MafScanAntiPatterns`, `MafValidateFanOut`, `MafLintAgentPrompt`, `MafEstimateCost`), build-verified hunts (`MafRunCs0618Hunt`), NuGet diffing (`MafDiffPackage`, `MafPreUpgradeDryRun`), workflow simulation (`MafSimulateWorkflow`), explanations (`MafExplain`), scaffolders (`MafNewAgent`, `MafNewExecutor`), PR-scoped audits (`MafAuditPullRequest`), version planning (`MafMigrationPath`), and the single-command health letter `MafDoctor`. Anti-pattern + fan-out scanners both support `format: "sarif"` for GitHub Advanced Security integration.
- **4 resources** — `maf://guide`, `maf://constraints`, `maf://registry`, `maf://skills?name=...` — the full migration knowledge base, readable on demand.
- **3 prompts** — `maf-audit`, `maf-migrate`, `maf-cs0618-hunt` — structured conversation starters.
- **3 Roslyn analyzers** (separate `maf-autopilot.Analyzers` NuGet) — `MAF001` fan-out, `MAF002` `DefaultAzureCredential`, `MAF003` `EnableSensitiveData`. Write-time enforcement.

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

> 📦 Current release: [`1.0.0`](https://www.nuget.org/packages/maf-autopilot). Supports MAF 1.0 / 1.2 / 1.3 simultaneously.

### ⚡ First 5 minutes — try these three commands

```powershell
# 1. Instant health check on any MAF project (A/B/C/F grade + top 3 fixes)
maf-autopilot doctor .

# 2. Scaffold a brand-new MAF 1.3.0 agent + smoke test (10 seconds, anti-pattern-clean)
maf-autopilot new agent ChatBot

# 3. Open VS Code; in Copilot Chat:
@maf-auditor                           # full pre-migration audit + plan
@maf-best-practice-reviewer            # steady-state best-practice review
```

For deeper queries, ask Copilot:

> "Use `MafPreUpgradeDryRun` to tell me which files would break if I upgraded `Microsoft.Agents.AI` from 1.2.0 to 1.3.0."

> "Run `MafSimulateWorkflow` against this repo and embed the Mermaid topology in my PR description."

> "Use `MafLintAgentPrompt` to audit every agent's `Instructions` string for prompt-injection risk."

VS Code picks up `.vscode/mcp.json` automatically and starts the MCP server. Copilot Chat gains live tool calls, resource reads, and structured prompts.

### ⭐ Mode 1b: Docker container (no .NET SDK on the host)

If you'd rather not install the .NET SDK locally, pull the container instead:

```bash
docker pull ghcr.io/joslat/maf-autopilot:latest
```

Wire it into your IDE's `mcp.json`:

```json
{
  "servers": {
    "maf-autopilot": {
      "type": "stdio",
      "command": "docker",
      "args": ["run", "--rm", "-i", "ghcr.io/joslat/maf-autopilot:latest"]
    }
  }
}
```

Same tools, same skills, same resources — just isolated inside a container. Multi-arch (`amd64` + `arm64`).

### Mode 2: Skills + Agents only (no MCP server required)

Copy `.github/` from this repo into your .NET repository root. You get the agents and skills via VS Code's instruction/agent system — no MCP server needed, but no live tool calls either.

```powershell
# In your MAF project root
git clone https://github.com/joslat/maf-autopilot tmp-maf
Copy-Item tmp-maf/.github -Destination .github -Recurse
Remove-Item tmp-maf -Recurse
```

Then open Copilot Chat and use `@maf-migration` or `@maf-auditor`.

## Recommended companion MCPs

MAF Doctor is laser-focused on **diagnose + fix** for Microsoft Agent Framework. For best results, pair us with these companion MCPs in your `.vscode/mcp.json` or Claude Code config:

| MCP | What it gives you | Endpoint | Auth |
|---|---|---|---|
| **[Microsoft Learn MCP](https://learn.microsoft.com/api/mcp)** | Live MAF documentation, code samples, version-aware. 3 tools. | `https://learn.microsoft.com/api/mcp` | None |
| **[Context7](https://github.com/upstash/context7)** | 9000+ library docs, version-specific NuGet docs | `https://mcp.context7.com/mcp` | API key |
| **[DeepWiki](https://mcp.deepwiki.com/mcp)** | Query `microsoft/agent-framework` source repo wiki directly | `https://mcp.deepwiki.com/mcp` | None |
| **[Serena](https://github.com/oraios/serena)** | Semantic IDE-shape navigation across 40+ languages | local install | None |

This stack gives an AI client: docs lookup (Learn / Context7 / DeepWiki), codebase navigation (Serena), and MAF-specific audit + fix + migration (MAF Doctor). Each MCP stays in its lane.

## Why This Exists

`dotnet upgrade-assistant` handles package version bumps. maf-autopilot handles everything else:

- MAF-specific executor pattern migration (`ReflectingExecutor` → `partial class : Executor` + `[MessageHandler]`)
- Session API changes (`AgentThread` → `AgentSession`)
- Fan-out/fan-in silent failure detection — a class of bug that builds green but runs wrong
- Compiler ground-truth CS0618 detection (transitive obsoletions, overload-resolution surprises, project-local `[Obsolete]`) that pairs with `dotnet-inspect@0.7.8`'s new static surfacing
- Build-verified, task-by-task execution with a full tracking table
- A machine-readable Obsolete API Registry so fixes are deterministic, not guessed

---

## Project Structure

```
maf-autopilot/
├── .github/
│   ├── agents/                            # 3 GitHub Copilot Chat agents
│   │   ├── maf-migration.agent.md         # Build-verified task-by-task migration orchestrator
│   │   ├── maf-auditor.agent.md           # Pre-migration scan → generates migration-plan.md
│   │   └── maf-best-practice-reviewer.agent.md   # Steady-state audit (post-migration) → audit-report.md
│   ├── instructions/
│   │   └── maf-constraints.instructions.md       # Always-loaded: breaking changes, constraints
│   ├── skills/                            # 12 specialised skills (loaded by agents on demand)
│   │   ├── dotnet-inspect/                # pinned to v0.7.8 (surfaces [Obsolete])
│   │   ├── maf-migration-guide/           # 21-section guide navigator (version-keyed)
│   │   ├── maf-obsolete-api-registry/     # 13-entry CS0618 fix registry
│   │   ├── maf-anti-pattern-scanner/      # 9-rule canonical anti-pattern list (post-migration)
│   │   ├── maf-migration-plan-creator/    # template + ID conventions for tracking tables
│   │   ├── cs0618-hunter/                 # compiler-ground-truth path (paired with MafRunCs0618Hunt)
│   │   ├── maf-fan-out-validator/         # silent fan-in starvation rules
│   │   ├── nuget-diff-analyzer/           # (subsumed by MafDiffPackage tool)
│   │   ├── maf-release-watcher/           # weekly NuGet check workflow
│   │   ├── maf-workflow-smoke-tester/     # smoke test templates
│   │   └── maf-migration-retrospective/   # post-migration learning loop
│   └── workflows/                         # 5 GitHub Actions
│       ├── release.yml                    # NuGet publish (test → pack → push)
│       ├── maf-release-watcher.yml        # weekly MAF version check + auto-PR with draft registry entries
│       ├── maf-pr-audit.yml               # PR-scoped scan, posts a sticky comment
│       ├── maf-drift-detector.yml         # weekly MafDoctor; opens issue if grade < A
│       └── docker-publish.yml             # multi-arch GHCR image on tag push
├── mcp/
│   ├── maf-autopilot/                     # MCP server — 17 executable tools, 4 resources, 3 prompts
│   ├── maf-autopilot.Analyzers/           # Roslyn analyzer package (MAF001/002/003 — separate NuGet)
│   ├── maf-autopilot.Tests/               # ~354 xUnit tests
│   └── maf-autopilot.Analyzers.Tests/     # ~11 analyzer tests
├── Dockerfile                             # multi-stage build → ghcr.io/joslat/maf-autopilot:latest
├── guides/
│   ├── maf-current-migration-guide.md     # Cumulative — auto-generated, all versions concatenated (start here)
│   ├── maf-1.3.0-migration-guide.md       # 21-section migration guide (hand-authored bootstrap)
│   ├── maf-1.4.0-migration-guide.md       # 1.3 → 1.4 delta (auto-generated by release-watcher)
│   └── maf-1.5.0-migration-guide.md       # 1.4 → 1.5 delta (auto-generated by release-watcher)
├── docs/
│   ├── maf-migration-toolkit-plan.md      # 🗺️ phased roadmap + full 68-row tracking table
│   ├── project-status-and-vision.md       # stage assessment + completeness audit
│   ├── compatibility-matrix.md            # MAF version ↔ dependency versions
│   └── setup.md                           # detailed install + configure
├── CONTRIBUTING.md                        # 4 contribution shapes + PR conventions
├── TROUBLESHOOTING.md                     # install / init / MCP / dotnet-inspect / CI failure modes
└── .maf-version                           # tracked version (for release watcher)
```

---

## Skill Orchestration — When to Call What

| You need to…                                      | Use skill                      |
|---------------------------------------------------|--------------------------------|
| Look up MAF API signatures or patterns            | `maf-migration-guide`          |
| Check if a NuGet type/member exists               | `dotnet-inspect`               |
| Detect and fix CS0618 obsolete warnings           | `cs0618-hunter`                |
| Look up the fix pattern for a specific CS0618     | `maf-obsolete-api-registry`    |
| Generate a migration plan for a new codebase      | `maf-migration-plan-creator`   |
| Validate fan-out/fan-in workflow structure        | `maf-fan-out-validator`        |
| Post-process NuGet diff into actionable report    | `nuget-diff-analyzer`          |
| Check for / respond to new MAF versions           | `maf-release-watcher`          |
| Generate smoke tests for migrated workflows       | `maf-workflow-smoke-tester`    |
| Learn from migration surprises, improve toolkit   | `maf-migration-retrospective`  |

> **Pin to `dotnet-inspect@0.7.8` or later.** v0.7.8 (2026-05-04) [closes the `[Obsolete]`-overload gap](https://github.com/richlander/dotnet-inspect/releases/tag/v0.7.8). For ground-truth on what your project actually triggers (transitive obsoletions, overload resolution, project-local `[Obsolete]`), `cs0618-hunter` (compiler-based) is still the canonical path.

---

## Typical Migration Workflow

1. **Audit** — open Copilot Chat → use the `maf-audit` prompt (or `@maf-auditor` agent) to scan your codebase and generate `src/docs/migration-plan.md`
2. **Migrate** — use the `maf-migrate` prompt (or `@maf-migration` agent) to execute the plan task-by-task with `dotnet build` verification after each step
3. **Hunt stragglers** — use the `maf-cs0618-hunt` prompt to catch any remaining CS0618 obsolete API warnings the build surfaces

The MCP server tools are called automatically by Copilot during steps 1–3 — you don't invoke them directly.

---

## Roadmap & Status

For an up-to-date, evidence-anchored view of what is done, what is in progress, and what is pending, see:

- **[`docs/maf-migration-toolkit-plan.md`](docs/maf-migration-toolkit-plan.md)** — phased roadmap (Phase A → B → C → D) + the 66-row tracking table (single source of truth)
- **[`docs/project-status-and-vision.md`](docs/project-status-and-vision.md)** — stage assessment, the broader vision beyond migration, completeness audit

> The README used to carry its own tick-list of features. It drifted from the plan twice in two months. The plan tracking table is now authoritative; the README sticks to "what this is" and "how to use it."

### Distribution status (2026-05-16)

- ✅ **NuGet** — [`1.0.0`](https://www.nuget.org/packages/maf-autopilot) on nuget.org. Supports MAF 1.0 / 1.2 / 1.3.
- ✅ **Docker GHCR** — multi-arch (amd64+arm64) container, semver tags
- ✅ **Roslyn analyzers** — `maf-autopilot.Analyzers` package ships 3 write-time rules
- ✅ **Multi-version migration paths** — `MafMigrationPath(currentVer, targetVer)` walks version-keyed guide metadata

---

## Acknowledgements

This toolkit relies heavily on **[dotnet-inspect](https://github.com/richlander/dotnet-inspect)** by [Rich Lander](https://github.com/richlander) — a powerful CLI for querying .NET API surfaces across NuGet packages, platform libraries, and local assemblies. The `nuget-diff-analyzer` skill and the `maf-release-watcher` workflow both use `dotnet-inspect` (via [dnx](https://github.com/filipw/dnx)) to diff MAF package versions and surface breaking API changes.

> ℹ️ As of `dotnet-inspect` **v0.7.8** (2026-05-04), `[Obsolete]` members are surfaced in listings ([PR #318](https://github.com/richlander/dotnet-inspect/pull/318) closes [issue #316](https://github.com/richlander/dotnet-inspect/issues/316)). Versions ≤ v0.7.7 missed `[Obsolete]` at the overload level — historic motivation for `cs0618-hunter` as a workaround. **Pin to v0.7.8 or later.** `cs0618-hunter` remains valuable as the compiler ground-truth path (catches transitive obsoletions, overload-resolution surprises, project-local `[Obsolete]`); `dotnet-inspect` is the static / pre-build path. They are complementary.

---

## Contributing

Whenever a migration surfaces a new surprise (unexpected breaking change, silent runtime failure, wrong pattern in the official docs), update:
1. `guides/maf-1.3.0-migration-guide.md` — add to "Known Misalignments" or create a new section. (For deltas between versions, edit the matching per-version file; the cumulative `maf-current-migration-guide.md` auto-regenerates from those.)
2. `.github/skills/maf-obsolete-api-registry/registry.yaml` — add the CS0618 entry
3. `.github/instructions/maf-constraints.instructions.md` — update the breaking changes table if needed
