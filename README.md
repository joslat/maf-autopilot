<p align="center">
  <img src="https://raw.githubusercontent.com/joslat/maf-doctor/main/assets/MAFDoctorLogo.png" alt="MAF Doctor" width="320" />
</p>

# MAF Doctor — a toolkit for Microsoft Agent Framework

> Diagnose, explain, prescribe, verify — for MAF agents and workflows.

**MAF Doctor** is a **.NET global tool** (installed from NuGet) that does three things in one install:

1. **An MCP server** — exposes 27 executable tools, 7 resources, and 10 prompts to GitHub Copilot / Claude Code / Cursor.
2. **A CLI** — **doctor** for an A–F health letter, **autofix-all** for deterministic rewriters, **new agent** to scaffold, **init** to wire up your repo, and more.
3. **A plugin** — init drops steering snippets and wires the MCP server into both VS Code and Claude Code, so Copilot's and Claude's agentic loops gain MAF-specific knowledge (14 bundled skills + 8 specialist agents).

Plus a separate **maf-doctor.Analyzers** NuGet package with 3 Roslyn analyzers (MAF001 / MAF002 / MAF003) for IDE write-time enforcement, and a curated, version-keyed **obsolete-API registry** that maps every known CS0618 warning to its exact replacement.

> 📦 **[Latest release on NuGet →](https://www.nuget.org/packages/maf-doctor)** — and the toolkit keeps its own MAF knowledge current automatically. See [**It updates itself**](#it-updates-itself).

<p align="center">
  <img src="https://raw.githubusercontent.com/joslat/maf-doctor/main/docs/assets/install-cast.gif" alt="maf-doctor: install, then audit a MAF codebase — grade F + the top fixes" width="760" />
  <br/>
  <em>Install, then run maf-doctor doctor on any MAF codebase — an A–F health letter + the top fixes, in seconds.</em>
</p>

### 🆕 New in 1.5.0 — migrate **FROM Semantic Kernel** to MAF

A cross-framework migration assistant for porting an existing **Semantic Kernel (C#)** app *to* Microsoft Agent Framework — distinct from the MAF *version* upgrade path:

- **`maf-doctor migrate-scan`** (CLI) / **`MafDetectSourceFramework`** (MCP tool) — inventories your SK usage (csproj package refs + a Roslyn construct scan) and tags every construct by migration **strategy**: 🌉 *bridgeable* (reuse via the `KernelFunction.AsAIFunction()` / `IChatCompletionService.AsChatClient()` interop shims), 🔁 *rewrite* (mechanical SK→MAF API swap), or 🏗 *re-architect* (group chat / planners / prompt templates / vector stores) — plus an **EASY / MEDIUM / HARD** verdict. `--json` for automation.
- **`maf-migrate-from`** prompt + **`@maf-cross-migration`** agent — the conductor: detect → plan → **scaffold a NEW MAF project beside the original** (non-destructive, side-by-side) → port construct-by-construct with `dotnet build` gates → validate with `MafDoctor`.
- **`maf://migrate-from?source=semantic-kernel`** resource + **`maf-from-semantic-kernel`** skill — the construct-by-construct mapping (+ interop bridges), reachable with or without the MCP server. Try it on the bundled **`samples/sk-sample/`** fixture. ([full notes →](CHANGELOG.md#150---2026-06-25))

### 🆕 New in 1.4.0 — diagnose → triage → fix, end to end

Findings you can act on, with the false positives flagged before you touch code:

- **`autofix-all` is human-readable by default** — a per-rule breakdown + the changed files + the next step (add `--json` for the machine-readable shape). The `0 files changed` case explains *why* and *what to do next*.
- **Every finding carries a `confidence`** — `certain` / `high` / `heuristic`. The `heuristic` ones (name/text-based) are flagged **"⚠ verify it's real first"** — your false-positive triage signal, surfaced in `doctor`, `--plan`, and `--json`.
- **`maf-remediate` — the fix-it-all conductor** (MCP prompt): grade → plan → `autofix-all` → work each semantic finding, **confirming heuristic ones before editing** → build → re-grade. Backed by the new **`maf-remediation-playbook`** skill. Just ask *"fix all the issues maf-doctor finds."*
- **`doctor --plan --json`** — a structured, phased remediation manifest (per-finding `confidence` + `verify_first`) an automated loop can iterate.
- **Fewer false positives** — a hardening sweep across the detectors (supported `Hosting.A2A` packages, MediatR `IMessageHandler<T>`, `app.RunAsync()` host calls, `AgentResponse<T>.Result`, `#if DEBUG` guards, … no longer mis-flagged). ([full notes →](CHANGELOG.md#140---2026-06-24))

### What makes it worth trying

- 🔍 **Catches the bugs that compile clean** — detects fan-out/fan-in silent failures where a workflow exits successfully but produces no output. Runtime-only, zero build signal, invisible without this tool.
- 🩺 **Compiler ground-truth for CS0618** — surfaces obsolete-API usage the build would hit, including transitive obsoletions and overload-resolution surprises, and maps each to its canonical fix.
- ✅ **Best-practice auditing, not just migration** — reviews your agents and workflows against current MAF patterns, catching drift and anti-patterns before they become production bugs.
- 🌉 **Cross-framework migration** — coming from **Semantic Kernel**? `migrate-scan` inventories your SK constructs, tags each 🌉 bridge / 🔁 rewrite / 🏗 re-architect, scores the effort, and the `maf-migrate-from` flow ports it into a new MAF project beside the original — non-destructively.
- 🤖 **Fully agentic loop** — Copilot audits your codebase, generates a tracked migration plan, and executes it task-by-task, building after every step.
- 🧰 **Deterministic fixes, not guesses** — the obsolete-API registry maps every known CS0618 warning to its exact replacement pattern. No hallucinated fixes.
- 📖 **It updates itself** — as new MAF versions ship, the toolkit refreshes its own migration knowledge automatically. [How →](#it-updates-itself)
- 🔐 **Hardened against the named MCP attack lattice** — a comprehensive security pass closes the critical- and high-tier MCP attack classes (path-escape, annotation drift, scaffold code-injection, prompt-injection via release notes, workflow-dispatch input injection), with defense-in-depth helpers and CI-enforced invariants. Cisco mcp-scanner: all tools SAFE, 0 findings. See [SECURITY.md](SECURITY.md) and [docs/security.md](docs/security.md).

## It updates itself

Migration tooling is only useful if it knows about the MAF version you're on. MAF Doctor keeps its own knowledge base current instead of asking you to wait for a maintainer release:

**How** — a GitHub Actions watcher checks NuGet for a new Microsoft.Agents.AI release, diffs the API surface against the last known version, and updates the per-version migration guide, the compatibility matrix, and the obsolete-API registry:

- **Minor / patch bumps** are committed straight to the main branch — the guide, matrix, and tracked version move forward on their own.
- **Major bumps** don't auto-merge; they open a human-gated issue so a breaking release gets eyes before anything lands.
- New registry entries are drafted by an AI-fill loop and **PR-verified** — a registry-verification check runs on the fill PR (and can gate the merge when required-status-checks is enabled), so the deterministic fix data stays trustworthy.

**When** — every **Thursday 06:00 UTC** (~08:00 Europe/Zurich), aligned with MAF's .NET Thursday-morning ship cadence. Off-cadence releases are covered by a manual dispatch trigger.

The practical upshot: you don't pin to a MAF version in these docs, and you don't wait on us. Re-run the doctor after any MAF bump and it cross-references the freshest guide automatically.

## How It Works

MAF Doctor is a **[Model Context Protocol (MCP)](https://modelcontextprotocol.io) server** packaged as a .NET global tool. With no subcommand it runs in MCP mode and exposes:

- **28 executable tools**, each annotated with MCP behavior hints (read-only / destructive / idempotent / open-world) so clients can auto-classify them — registry lookup, code scanning, build-verified CS0618 hunts, NuGet diffing, workflow simulation, scaffolding, cross-framework (Semantic Kernel → MAF) migration scanning, PR-scoped audits, version planning, MCP/update status, and a single-command health letter. The anti-pattern and fan-out scanners emit SARIF for GitHub Advanced Security; the two destructive tools (auto-fix and auto-fix-all) support a dry-run.
- **7 resources** — the migration guide, constraints, registry, rules, help, the Semantic Kernel → MAF mapping, and per-name skills — readable on demand.
- **10 prompts** — audit, migrate, remediate, migrate-from, cs0618-hunt, review, debug, explain-finding, scaffold, and help.
- **3 Roslyn analyzers** (in the separate maf-doctor.Analyzers package) — MAF001 (fan-out), MAF002 (DefaultAzureCredential), MAF003 (EnableSensitiveData). Write-time enforcement.

With a subcommand, the same binary runs as a CLI tool — see Quick Start below.

## Quick Start

> 🎬 **New here?** The [**3-minute quickstart**](docs/quickstart.md) walks install → init → analyze → auto-fix → upgrade-to-latest-MAF end-to-end (the demo-video script).

### ⭐ Mode 1: MCP Server (recommended — full agentic power)

```powershell
# 1. Install the MCP server as a .NET global tool
dotnet tool install --global maf-doctor

# 2. In your MAF project — wires MCP + steering for VS Code, Copilot & Claude Code
cd your-maf-project
maf-doctor init
```

`init` is **non-destructive — it attaches and updates only, never overwriting your files.** It adds its own MCP server entry to both **VS Code** (.vscode/mcp.json) and **Claude Code** (.mcp.json), and drops auto-loaded steering as self-contained sidecars in each tool's convention dir — .github/instructions/ for Copilot, .claude/ plus a one-line CLAUDE.md import for Claude Code, and an AGENTS.md managed block. These sidecars are refreshed on every re-run and never merged into your hand-authored files, so re-running `init` after an upgrade is always safe. Your assistant picks the server up automatically and gains live tool calls, resource reads, and structured prompts.

#### Keeping it up to date

When the MCP server starts, it checks NuGet for a newer `maf-doctor` package using a short, cached, non-blocking advisory check. It also detects the separate case where the installed tool is current but this workspace's MCP config or steering sidecars were initialized by an older `maf-doctor`. You can ask for the same report any time with the `MafDoctorStatus` MCP tool.

```powershell
dotnet tool update --global maf-doctor   # upgrade to the latest release
maf-doctor --version                      # confirm
cd your-maf-project && maf-doctor init    # re-run init to refresh the steering sidecars
```

> ⚠️ **"The file is locked by another process" on update?** Your editor keeps the MCP server (`maf-doctor`) **running**, which locks the tool binary — so `dotnet tool update` can't replace it. Stop every running instance first, then update:
>
> ```powershell
> # Windows: kill all running instances, then update
> Get-Process maf-doctor -ErrorAction SilentlyContinue | Stop-Process -Force
> dotnet tool update --global maf-doctor
> ```
> ```bash
> # macOS / Linux
> pkill -f maf-doctor; dotnet tool update --global maf-doctor
> ```
> If you have **both** VS Code and Claude Code open, each holds its **own** lock — the commands above kill every instance (more reliable than closing editors one by one). Alternatively, close **all** editors running the server (or disable it in each) → update → reopen.
>
> **After updating:** re-run `maf-doctor init` to refresh the steering, **and reload the editor / MCP server** so it loads the new binary — VS Code: `Developer: Reload Window`; Claude Code: reload the project. (A bare restart isn't enough — the MCP host caches tool/resource descriptors, so without a full reload you'll keep talking to the old version.)

> 📖 **Going deeper:** [what `init` installs (and why)](docs/init-reference.md) · [using MAF Doctor — prompts, tools, agents & natural-language steering](docs/usage.md) · [hands-on workshop](samples/workshop.md).

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

Same tools, skills, and resources — isolated in a container. Multi-arch (amd64 + arm64).

### Mode 2: Skills + Agents only (no MCP server)

Copy this repo's .github folder into your .NET repository root for the agents and skills via VS Code's instruction/agent system — no live tool calls, but no install either.

```powershell
git clone https://github.com/joslat/maf-doctor tmp-maf
Copy-Item tmp-maf/.github -Destination .github -Recurse
Remove-Item tmp-maf -Recurse
```

Then use the @maf-migration or @maf-auditor agents in Copilot Chat.

## Recommended companion MCP

MAF Doctor is laser-focused on **diagnose + fix** for Microsoft Agent Framework. For documentation lookup, pair it with the **[Microsoft Learn MCP](https://learn.microsoft.com/api/mcp)** — live, version-aware MAF docs and code samples, no auth required. Docs lookup stays in Learn's lane; audit + fix + migration stays in ours.

## Why This Exists

The .NET upgrade-assistant handles package version bumps. MAF Doctor handles everything else:

- MAF-specific executor pattern migration (`ReflectingExecutor` → `partial class : Executor` + `[MessageHandler]`)
- Session API changes (`AgentThread` → `AgentSession`)
- Fan-out/fan-in silent failure detection — a class of bug that builds green but runs wrong
- Compiler ground-truth CS0618 detection (transitive obsoletions, overload-resolution surprises, project-local Obsolete attributes)
- Build-verified, task-by-task execution with a full tracking table
- A machine-readable obsolete-API registry so fixes are deterministic, not guessed

## Typical Migration Workflow

1. **Audit** — open Copilot Chat → use the audit prompt (or the @maf-auditor agent) to scan your codebase and generate a tracked migration plan.
2. **Migrate** — use the migrate prompt (or the @maf-migration agent) to execute the plan task-by-task, building after each step.
3. **Hunt stragglers** — use the cs0618-hunt prompt to catch any remaining CS0618 warnings the build surfaces.

The MCP tools are called automatically by Copilot during these steps — you don't invoke them directly.

## Distribution

Distribution: **NuGet** (maf-doctor + maf-doctor.Analyzers), **Docker GHCR** (multi-arch amd64 + arm64, semver tags), and the self-updating knowledge base described above. Security model: [docs/security.md](docs/security.md).

## Acknowledgements

This toolkit relies on **[dotnet-inspect](https://github.com/richlander/dotnet-inspect)** by [Rich Lander](https://github.com/richlander) — a CLI for querying .NET API surfaces across NuGet packages and assemblies. The release watcher uses it (via [dnx](https://github.com/filipw/dnx)) to diff MAF package versions and surface breaking API changes. **Pin to v0.7.8 or later** — it surfaces Obsolete members at the overload level ([PR #318](https://github.com/richlander/dotnet-inspect/pull/318)); the compiler-based cs0618-hunter path stays complementary for transitive and project-local obsoletions.

## Contributing

When a migration surfaces a new surprise (unexpected breaking change, silent runtime failure, wrong pattern in the docs), update:

1. The matching per-version guide in the guides folder (the cumulative guide auto-regenerates from those).
2. The obsolete-API registry at `.github/skills/maf-obsolete-api-registry/registry.yaml` — add the CS0618 entry.
3. The MAF constraints instructions — update the breaking-changes table if needed.

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution shapes + PR conventions.
