# MAF Doctor — 3-Minute Quickstart

> **Goal:** from zero to a graded, auto-fixed, upgraded MAF codebase in one sitting — all from the CLI, no Copilot Chat required. This is the script we record for the demo video.
>
> **You need:** .NET 8, 9, **or** 10 SDK (`dotnet --version`).

The fastest reproducible demo runs against the bundled sample (`samples/maf-1.3-sample/`) — a *deliberately* broken MAF 1.3 fraud-claims workflow. Every command below works just as well on **your own** MAF project; just `cd` into it instead.

---

## Beat 1 — Install (30s)

```bash
# Install the global tool (published to NuGet as `maf-doctor`)
dotnet tool install --global maf-doctor

# Verify it's on PATH
maf-doctor --version

# Later, to upgrade to the newest release:
dotnet tool update --global maf-doctor
```

> 🎙️ *"One global tool. It's an MCP server for Copilot/Claude/Cursor **and** a standalone CLI — we'll use the CLI."*
>
> When the MCP server starts, it now performs a short cached advisory check. If a newer `maf-doctor` package exists, it tells you to run `dotnet tool update --global maf-doctor` and then `maf-doctor init`; if only this repo's generated MCP/steering files are stale, it asks only for `maf-doctor init`. You can ask for the same report from chat with `MafDoctorStatus`.
>
> ⚠️ **Upgrade says "file is locked by another process"?** Your editor keeps the MCP server (`maf-doctor`) running, which locks the binary. Kill **every** instance first (both VS Code and Claude Code if open) — `Get-Process maf-doctor | Stop-Process -Force` (Windows) / `pkill -f maf-doctor` (macOS/Linux) — then update. **After upgrading**, re-run `maf-doctor init` and **reload the editor** (VS Code: `Developer: Reload Window`) so it loads the new binary; a bare restart keeps the cached old descriptors. (Full guide: [TROUBLESHOOTING.md](../TROUBLESHOOTING.md).)

## Beat 2 — Drop it into a repo (20s)

```bash
cd samples/maf-1.3-sample      # or: cd your-own-maf-project
maf-doctor init
```

`init` wires the MCP server for VS Code (`.vscode/mcp.json`) and Claude Code (`.mcp.json`), and drops auto-loaded steering sidecars (Copilot / Claude / Cursor / AGENTS.md) — refreshed on re-run, never merged into your own files. The 15 skills are served by the MCP server, not written as files. Idempotent — safe to re-run. (details: [init-reference.md](./init-reference.md)).

> 🎙️ *"`init` makes any repo MAF-aware: MCP config for the IDE, plus steering rules and 15 skills for Copilot's agent loop."*

## Beat 3 — Analyze: the health grade (30s)

```bash
maf-doctor doctor .
```

You'll see a **🔴 grade F** — with the top fixes. These include **silent fan-out/fan-in starvation** bugs: code that compiles clean and exits "successfully" but produces no output. No build warning, no exception — invisible without this tool.

> 🎙️ *"Grade F. Seven anti-pattern errors, plus three silent-starvation risks — they compile clean and break at runtime."*
>
> *Tip:* in a monorepo, scope the scan with `maf-doctor doctor . --exclude samples/ --exclude '**/*.Tests/**'`.

> 💡 **Act on it:** every finding shows a one-line **Why** + **Fix** and a **`confidence`** (`certain`/`high`/`heuristic` — *heuristic* ones are flagged "⚠ verify it's real first", your false-positive signal). Add `--all` to list them grouped by rule, `--plan` for a checkboxed remediation plan (`--plan --json` for a structured manifest). In an MCP client (Copilot / Claude / Cursor), ask *"explain the finding at `Executors/OsintInvestigator.cs:25`"* — it calls **`MafExplainFinding`** to return that code in context + why + fix, using your host model (no extra LLM cost).

## Beat 4 — Fix the mechanical issues, deterministically (30s)

```bash
# Preview by default (no writes)
maf-doctor autofix-all .

# Apply — Roslyn rewriters, no LLM in this loop
maf-doctor autofix-all . --apply

# Re-grade
maf-doctor doctor .
```

`autofix-all` prints a **human-readable summary** by default — a per-rule breakdown, the files it would change, and the next step. Add **`--json`** if you want the machine-readable output for CI/scripts. (And `0 files changed` is normal: it only means none of the 5 *mechanical* rules matched — your semantic findings are still there; run `doctor` to see them.)

The grade stays **🔴 F** — but **three mechanical errors are gone (7 → 4)**, deterministically, in under 30 seconds, no LLM in the loop. The rest are *semantic* (a hard-coded key, shared provider state, the fan-out starvation bugs) and need judgment — that's the hand-off to the `@maf-migration` agent, which is what drives the grade to A. The fixes are deterministic Roslyn rewriters — the same machinery you'd trust in a Microsoft refactor extension, not guesses.

> 🎙️ *"Apply for real. Roslyn rewriters — three mechanical errors gone, 7 down to 4, deterministically, no LLM. The rest are semantic; the agent takes those the rest of the way to A."*

> 💡 **Fix *everything* (in an MCP client — Copilot / Claude / Cursor):** just ask *"analyze this repo and fix all the issues maf-doctor finds."* The model runs the `maf-remediate` loop: it grades, gets a plan where **every finding carries a `confidence`** (`certain` / `high` / `heuristic`), applies the mechanical fixes, then works each semantic finding — **auto-triaging the `heuristic` ones (likely false positives), confirming each before it edits** — building after every step until the grade stops climbing, and reports what it fixed vs skipped-as-false-positive. The per-rule fixes + false-positive rules come from the bundled **`maf-remediation-playbook`** skill. Prefer a checklist? `maf-doctor doctor . --plan` (human) or `--plan --json` (a structured manifest for automation).

## Beat 5 — Upgrade to the latest MAF (60s)

The watcher keeps the toolkit's *knowledge* current; here's how you move *your code* to a newer MAF. Say you're on `Microsoft.Agents.AI` 1.6.x and want the latest:

```bash
# 1. See what a bump would break BEFORE you touch the csproj (ask Copilot, or):
#    MafPreUpgradeDryRun(repoPath, "Microsoft.Agents.AI", "1.6.1", "1.10.0")

# 2. Bump the package reference in your .csproj, then build to surface breaks:
dotnet add package Microsoft.Agents.AI --version 1.10.0
dotnet build         # CS0618 / CS0246 surface the changed/removed APIs

# 3. Let the toolkit map each break to its canonical fix:
maf-doctor doctor .              # re-grade against the new version
maf-doctor autofix-all . --apply # apply the mechanical migrations

# 4. For anything left, the migration guide + agent drive it in Copilot Chat:
#    @maf-migration   →  reads guides/maf-<version>-migration-guide.md, fixes task-by-task
```

`maf-doctor` cross-references the per-version migration guide + the obsolete-API registry (every known `CS0618` → its exact replacement), so you get **named fixes, not hallucinations**.

> 🎙️ *"Bump the package, build to see the breaks, and the toolkit maps each one to its canonical fix — from the registry, deterministic. The `@maf-migration` agent handles the rest."*

## Bonus — coming from Semantic Kernel? (30s)

Not on MAF yet but on **Semantic Kernel**? Scope the port before you write a line:

```bash
# Inventory SK usage + score the migration (try the bundled SK fixture)
maf-doctor migrate-scan samples/sk-sample
```

You'll get every SK construct tagged by strategy — 🌉 **bridge** (reuse via interop shims), 🔁 **rewrite** (mechanical API swap), 🏗 **re-architect** (group chat / planners / vector stores) — plus an **EASY / MEDIUM / HARD** verdict (the sample is **HARD**). Add `--json` for automation.

> 🎙️ *"Coming from Semantic Kernel? `migrate-scan` inventories your SK code and scores the move."*
>
> 💡 **Do the port (in an MCP client):** ask *"migrate my Semantic Kernel app to MAF"* — the `maf-migrate-from` prompt (or the `@maf-cross-migration` agent) plans it, **scaffolds a new MAF project beside the original** (non-destructive), and ports it construct-by-construct with build gates. The mapping lives at `maf://migrate-from?source=semantic-kernel`.

## Reset (for re-recording)

```bash
git restore samples/maf-1.3-sample/
```

---

## Where to go next

- **Full hands-on workshop** (~50 min, agent-driven): [`samples/workshop.md`](../samples/workshop.md)
- **Setup & usage modes** (MCP server, Docker, plugin): [`docs/setup.md`](./setup.md)
- **Architecture**: [`docs/architecture.md`](./architecture.md)
- **Troubleshooting**: [`../TROUBLESHOOTING.md`](../TROUBLESHOOTING.md)
