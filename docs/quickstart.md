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
```

> 🎙️ *"One global tool. It's an MCP server for Copilot/Claude/Cursor **and** a standalone CLI — we'll use the CLI."*

## Beat 2 — Drop it into a repo (20s)

```bash
cd samples/maf-1.3-sample      # or: cd your-own-maf-project
maf-doctor init
```

`init` writes `.vscode/mcp.json` (wires the MCP server for your IDE) and `.github/copilot-instructions.md` (MAF hard-constraint rules), and drops the skills/agents bundle. Idempotent — safe to re-run.

> 🎙️ *"`init` makes any repo MAF-aware: MCP config for the IDE, plus steering rules and 12 skills for Copilot's agent loop."*

## Beat 3 — Analyze: the health grade (30s)

```bash
maf-doctor doctor .
```

You'll see a **🔴 grade F** — with the top fixes. These include **silent fan-out/fan-in starvation** bugs: code that compiles clean and exits "successfully" but produces no output. No build warning, no exception — invisible without this tool.

> 🎙️ *"Grade F. Ten anti-pattern errors, three of them silent-starvation risks — they compile clean and break at runtime."*
>
> *Tip:* in a monorepo, scope the scan with `maf-doctor doctor . --exclude samples/ --exclude '**/*.Tests/**'`.

## Beat 4 — Fix the mechanical issues, deterministically (30s)

```bash
# Preview first (no writes)
maf-doctor autofix-all . --dry-run

# Apply — Roslyn rewriters, no LLM in this loop
maf-doctor autofix-all .

# Re-grade
maf-doctor doctor .
```

Grade climbs to **🟢 A** (or close). The fixes are deterministic Roslyn rewriters — the same machinery you'd trust in a Microsoft refactor extension, not guesses.

> 🎙️ *"Apply for real. Roslyn rewriters. Re-grade — F to A in under 30 seconds, deterministically."*

## Beat 5 — Upgrade to the latest MAF (60s)

The watcher keeps the toolkit's *knowledge* current; here's how you move *your code* to a newer MAF. Say you're on `Microsoft.Agents.AI` 1.6.x and want the latest:

```bash
# 1. See what a bump would break BEFORE you touch the csproj (ask Copilot, or):
#    MafPreUpgradeDryRun(repoPath, "Microsoft.Agents.AI", "1.6.1", "1.10.0")

# 2. Bump the package reference in your .csproj, then build to surface breaks:
dotnet add package Microsoft.Agents.AI --version 1.10.0
dotnet build         # CS0618 / CS0246 surface the changed/removed APIs

# 3. Let the toolkit map each break to its canonical fix:
maf-doctor doctor .          # re-grade against the new version
maf-doctor autofix-all .     # auto-apply the mechanical migrations

# 4. For anything left, the migration guide + agent drive it in Copilot Chat:
#    @maf-migration   →  reads guides/maf-<version>-migration-guide.md, fixes task-by-task
```

`maf-doctor` cross-references the per-version migration guide + the obsolete-API registry (every known `CS0618` → its exact replacement), so you get **named fixes, not hallucinations**.

> 🎙️ *"Bump the package, build to see the breaks, and the toolkit maps each one to its canonical fix — from the registry, deterministic. The `@maf-migration` agent handles the rest."*

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
