# MAF Doctor

**One install — a .NET global tool, an MCP server, and an IDE plugin — for Microsoft Agent Framework (MAF).**

Diagnose, explain, prescribe, and verify MAF agents and workflows: an A–F health letter, deterministic Roslyn auto-fixes, fan-out/fan-in silent-failure detection, compiler-ground-truth CS0618 hunts, and a self-updating obsolete-API registry.

![Install, then run maf-doctor doctor on any MAF codebase — an A–F health letter plus the top fixes, in seconds.](https://raw.githubusercontent.com/joslat/maf-doctor/main/docs/assets/install-cast.gif)

## Install

```
dotnet tool install --global maf-doctor     # first time
dotnet tool update  --global maf-doctor     # upgrade later
```

Then, in any MAF repo:

```
maf-doctor doctor .          # instant A–F health grade + the top fixes (add --all for every finding)
maf-doctor autofix-all .     # apply every deterministic Roslyn rewriter
maf-doctor init              # wire the MCP server + MAF steering into this repo
```

## `init` is non-destructive — it attaches and updates only

Running `maf-doctor init` **never overwrites or merges into your own files.** It only:

- **Adds its own MCP server entry** to **VS Code** (`.vscode/mcp.json`) and **Claude Code** (`.mcp.json`) — existing entries are left untouched.
- **Drops MAF steering as self-contained sidecars** in each tool's convention directory — `.github/instructions/` (Copilot), `.claude/` plus a one-line import in `CLAUDE.md` (Claude Code), and a clearly-delimited managed block in `AGENTS.md`.
- These sidecars are **regenerated on every re-run**, so re-initializing after an upgrade is always safe and idempotent — your hand-authored instructions are never edited.

Uninstall is just as clean: `dotnet tool uninstall --global maf-doctor`, then delete the sidecars.

## What you get

- **MCP server** — 26 tools, 6 resources, and 9 prompts for GitHub Copilot / Claude Code / Cursor.
- **CLI** — `doctor`, `autofix-all`, `new agent`, `init`, and more.
- **Plugin** — 13 bundled skills + 7 specialist agents wired into Copilot's and Claude's agentic loops.
- A companion **[maf-doctor.Analyzers](https://www.nuget.org/packages/maf-doctor.Analyzers)** package — 3 Roslyn analyzers (MAF001 / MAF002 / MAF003) for IDE write-time enforcement.

## Highlights

- 🔍 **Catches bugs that compile clean** — fan-out/fan-in silent failures where a workflow exits successfully but produces no output. Zero build signal.
- 🩺 **Compiler ground-truth for CS0618** — obsolete-API usage mapped to its exact replacement pattern. Deterministic fixes, not hallucinated ones.
- 🆕 **`doctor --all`** — list *every* finding (not just the top 3), grouped by rule and ordered by impact, each with a one-line *why* and the offending source line. Full triage in one pass.
- 🆕 **`doctor --plan`** — turn the findings into an ordered, checkboxed **remediation plan** (Phase 1: one `autofix-all` for the mechanical fixes; Phase 2: the semantic fixes as impact-ordered tasks) you can drop straight into a GitHub issue.
- 📖 **It updates itself** — a weekly GitHub Actions watcher refreshes the migration guide, compatibility matrix, and obsolete-API registry as new MAF versions ship, so you don't pin to a MAF version or wait on a maintainer release.
- 🔐 **Security-hardened** — closes the named MCP attack lattice (path-escape, annotation drift, scaffold code-injection, prompt-injection, workflow-dispatch input injection). Cisco mcp-scanner: all tools SAFE, 0 findings.

## Full docs

📖 **[github.com/joslat/maf-doctor](https://github.com/joslat/maf-doctor)** — 3-minute quickstart, `init` reference, usage guide (prompts / tools / agents + natural-language steering), hands-on workshop, and security docs.

_MIT licensed._
