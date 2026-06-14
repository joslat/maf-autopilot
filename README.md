# MAF Doctor — a toolkit for Microsoft Agent Framework

> Diagnose, explain, prescribe, verify — for MAF agents and workflows.

> **Naming:** the brand is **MAF Doctor**; the NuGet package + CLI are **`maf-doctor`**. (Pre-rename this shipped as `maf-autopilot` ≤ 1.2.0 — that package is deprecated in favor of `maf-doctor`. The internal `src/maf-autopilot/` project folder keeps its name; it's invisible to users.)

**MAF Doctor** is a .NET global tool that does three things in one install:

1. **An MCP server** — exposes 25 executable tools, 6 resources, and 7 prompts to GitHub Copilot / Claude Code / Cursor via `.vscode/mcp.json`.
2. **A CLI** — `maf-doctor doctor` (A–F health letter), `maf-doctor autofix-all` (deterministic rewriters), `maf-doctor new agent <Name>`, `maf-doctor init` (drops MCP config + steering snippets into your repo), and more.
3. **A plugin** — `init` also drops 12 specialised skills, 7 specialist agents, and steering snippets so GitHub Copilot's agentic loops gain MAF-specific knowledge.

Plus a separate **`maf-doctor.Analyzers`** NuGet with 3 Roslyn analyzers (`MAF001`/`MAF002`/`MAF003`) for IDE write-time enforcement, and a curated, version-keyed **obsolete-API registry** that maps every known `CS0618` to its exact replacement.

> 📦 **[Latest release on NuGet →](https://www.nuget.org/packages/maf-doctor)** — and the toolkit keeps its own MAF knowledge current automatically. See [**It updates itself**](#it-updates-itself).

### What makes it worth trying

- 🔍 **Catches the bugs that compile clean** — detects fan-out/fan-in silent failures where a workflow exits successfully but produces no output. Runtime-only, zero build signal, invisible without this tool.
- 🩺 **Compiler ground-truth for CS0618** — surfaces obsolete-API usage the build would hit, including transitive obsoletions and overload-resolution surprises, and maps each to its canonical fix.
- ✅ **Best-practice auditing, not just migration** — reviews your agents and workflows against current MAF patterns, catching drift and anti-patterns before they become production bugs.
- 🤖 **Fully agentic loop** — Copilot audits your codebase, generates a tracked migration plan, executes it task-by-task with `dotnet build` verification after every step, and updates the plan as it goes.
- 🧰 **Deterministic fixes, not guesses** — the obsolete-API registry maps every known `CS0618` to its exact replacement pattern. No hallucinated fixes.
- 📖 **It updates itself** — as new MAF versions ship, the toolkit refreshes its own migration knowledge automatically. [How →](#it-updates-itself)
- 🔐 **Hardened against the named MCP attack lattice** — a comprehensive security pass closes the critical- and high-tier MCP attack classes (path-escape, annotation drift, scaffold code-injection, prompt-injection via release notes, `workflow_dispatch` input injection), with defense-in-depth helpers and CI-enforced invariants. Cisco mcp-scanner: 25/25 tools SAFE, 0 findings. See [`SECURITY.md`](SECURITY.md) + [`docs/security.md`](docs/security.md).

## It updates itself

Migration tooling is only useful if it knows about the MAF version you're on. MAF Doctor keeps its own knowledge base current instead of asking you to wait for a maintainer release:

**How** — a GitHub Actions watcher checks NuGet for a new `Microsoft.Agents.AI` release, diffs the API surface against the last known version, and updates the per-version migration guide, the compatibility matrix, and the obsolete-API registry:

- **Minor / patch bumps** are committed straight to `main` — the guide, matrix, and `.maf-version` move forward on their own.
- **Major bumps** don't auto-merge; they open a human-gated issue so a breaking release gets eyes before anything lands.
- New registry entries are drafted by an AI-fill loop and **PR-verified** — `verify-registry` runs on the fill PR (and can gate the merge when required-status-checks is enabled), so the deterministic fix data stays trustworthy.

**When** — every **Thursday 06:00 UTC** (~08:00 Europe/Zurich), aligned with MAF's .NET Thursday-morning ship cadence. Off-cadence releases are covered by a manual `workflow_dispatch` trigger.

The practical upshot: you don't pin to a MAF version in these docs, and you don't wait on us. Re-run `maf-doctor doctor` after any MAF bump and it cross-references the freshest guide automatically.

## How It Works

`maf-doctor` is a **[Model Context Protocol (MCP)](https://modelcontextprotocol.io) server** packaged as a .NET global tool. With no subcommand it runs in MCP mode and exposes:

- **25 executable tools**, each annotated with MCP behavior hints (`readOnlyHint` / `destructiveHint` / `idempotentHint` / `openWorldHint`) so clients can auto-classify them — registry lookup, code scanning, build-verified CS0618 hunts, NuGet diffing, workflow simulation, scaffolding, PR-scoped audits, version planning, and the single-command health letter `MafDoctor`. Anti-pattern + fan-out scanners support `format: "sarif"` for GitHub Advanced Security; the 2 destructive tools (`MafAutoFix`, `MafAutoFixAll`) support `dryRun`.
- **6 resources** — `maf://guide`, `maf://constraints`, `maf://registry`, `maf://rules`, `maf://help`, `maf://skills?name=...` — the full knowledge base, readable on demand.
- **7 prompts** — `maf-audit`, `maf-migrate`, `maf-cs0618-hunt`, `maf-review`, `maf-debug`, `maf-scaffold`, `maf-help`.
- **3 Roslyn analyzers** (separate `maf-doctor.Analyzers` NuGet) — `MAF001` fan-out, `MAF002` `DefaultAzureCredential`, `MAF003` `EnableSensitiveData`. Write-time enforcement.

With a subcommand, the same binary runs as a CLI tool — see Quick Start below.

## Quick Start

> 🎬 **New here?** The [**3-minute quickstart**](docs/quickstart.md) walks install → init → analyze → auto-fix → upgrade-to-latest-MAF end-to-end (the demo-video script).

### ⭐ Mode 1: MCP Server (recommended — full agentic power)

```powershell
# 1. Install the MCP server as a .NET global tool
dotnet tool install --global maf-doctor

# 2. In your MAF project — writes .vscode/mcp.json + .github/copilot-instructions.md
cd your-maf-project
maf-doctor init
```

VS Code picks up `.vscode/mcp.json` automatically and starts the MCP server; Copilot Chat gains live tool calls, resource reads, and structured prompts.

**First three commands to try:**

```powershell
maf-doctor doctor .            # instant A/B/C/F health grade + top fixes
maf-doctor autofix-all .       # apply every deterministic Roslyn rewriter
maf-doctor new agent ChatBot   # scaffold an anti-pattern-clean MAF agent + smoke test
```

Then, in Copilot Chat:

```
@maf-auditor                   # full pre-migration audit + plan
@maf-best-practice-reviewer    # steady-state best-practice review
```

### ⭐ Mode 1b: Docker container (no .NET SDK on the host)

```bash
docker pull ghcr.io/joslat/maf-doctor:latest
```

```json
{
  "servers": {
    "maf-doctor": {
      "type": "stdio",
      "command": "docker",
      "args": ["run", "--rm", "-i", "ghcr.io/joslat/maf-doctor:latest"]
    }
  }
}
```

Same tools, skills, and resources — isolated in a container. Multi-arch (`amd64` + `arm64`).

### Mode 2: Skills + Agents only (no MCP server)

Copy `.github/` from this repo into your .NET repository root for the agents and skills via VS Code's instruction/agent system — no live tool calls, but no install either.

```powershell
git clone https://github.com/joslat/maf-doctor tmp-maf
Copy-Item tmp-maf/.github -Destination .github -Recurse
Remove-Item tmp-maf -Recurse
```

Then use `@maf-migration` or `@maf-auditor` in Copilot Chat.

## Recommended companion MCP

MAF Doctor is laser-focused on **diagnose + fix** for Microsoft Agent Framework. For documentation lookup, pair it with the **[Microsoft Learn MCP](https://learn.microsoft.com/api/mcp)** — live, version-aware MAF docs and code samples (`https://learn.microsoft.com/api/mcp`, no auth). Docs lookup stays in Learn's lane; audit + fix + migration stays in ours.

## Why This Exists

`dotnet upgrade-assistant` handles package version bumps. MAF Doctor handles everything else:

- MAF-specific executor pattern migration (`ReflectingExecutor` → `partial class : Executor` + `[MessageHandler]`)
- Session API changes (`AgentThread` → `AgentSession`)
- Fan-out/fan-in silent failure detection — a class of bug that builds green but runs wrong
- Compiler ground-truth CS0618 detection (transitive obsoletions, overload-resolution surprises, project-local `[Obsolete]`)
- Build-verified, task-by-task execution with a full tracking table
- A machine-readable obsolete-API registry so fixes are deterministic, not guessed

## Typical Migration Workflow

1. **Audit** — open Copilot Chat → use the `maf-audit` prompt (or `@maf-auditor` agent) to scan your codebase and generate a tracked `migration-plan.md`.
2. **Migrate** — use the `maf-migrate` prompt (or `@maf-migration` agent) to execute the plan task-by-task with `dotnet build` verification after each step.
3. **Hunt stragglers** — use the `maf-cs0618-hunt` prompt to catch any remaining CS0618 warnings the build surfaces.

The MCP tools are called automatically by Copilot during these steps — you don't invoke them directly.

## Roadmap & Status

For an evidence-anchored view of what's done, in progress, and pending:

- **[`docs/maf-migration-toolkit-plan.md`](docs/maf-migration-toolkit-plan.md)** — phased roadmap + the tracking table (single source of truth)
- **[`docs/project-status-and-vision.md`](docs/project-status-and-vision.md)** — stage assessment + the broader vision

Distribution: **NuGet** (`maf-doctor` + `maf-doctor.Analyzers`), **Docker GHCR** (multi-arch `amd64`+`arm64`, semver tags), and the self-updating knowledge base described above. Security model: [`docs/security.md`](docs/security.md).

## Acknowledgements

This toolkit relies on **[dotnet-inspect](https://github.com/richlander/dotnet-inspect)** by [Rich Lander](https://github.com/richlander) — a CLI for querying .NET API surfaces across NuGet packages and assemblies. The release watcher uses it (via [dnx](https://github.com/filipw/dnx)) to diff MAF package versions and surface breaking API changes. **Pin to v0.7.8 or later** — it surfaces `[Obsolete]` members at the overload level ([PR #318](https://github.com/richlander/dotnet-inspect/pull/318)); the compiler-based `cs0618-hunter` path stays complementary for transitive/project-local obsoletions.

## Contributing

When a migration surfaces a new surprise (unexpected breaking change, silent runtime failure, wrong pattern in the docs), update:

1. The matching per-version guide in `guides/` (the cumulative `maf-current-migration-guide.md` auto-regenerates from those).
2. `.github/skills/maf-obsolete-api-registry/registry.yaml` — add the `CS0618` entry.
3. `.github/instructions/maf-constraints.instructions.md` — update the breaking-changes table if needed.

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for contribution shapes + PR conventions.
