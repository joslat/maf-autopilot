# What `maf-doctor init` installs (and why)

`maf-doctor init`, run from a repo root, makes that repo **MAF-aware** for whichever AI coding tool you use. It does two kinds of thing:

1. **Wires the MCP server** into each tool's config, so the tool can call MAF Doctor's live tools/resources/prompts.
2. **Drops steering** (instruction snippets) so the assistant *defers to those tools* for MAF work instead of its training data.

It never touches your own hand-authored instruction files, and re-running it is safe — steering is **overwritten/refreshed**, MCP config is **merged**.

```bash
cd your-maf-project
maf-doctor init                 # everything below
maf-doctor init --with-cursor   # also the Cursor rule
```

## Files it writes

| File | Ecosystem | Key / shape | On re-`init` | What it does |
|---|---|---|---|---|
| `.vscode/mcp.json` | **VS Code / Copilot** | `servers` → `{ "type": "stdio", "command": "maf-doctor" }` | **merged** (your other servers preserved) | Registers the MCP server so Copilot Chat gets live tool calls. |
| `.mcp.json` | **Claude Code** | `mcpServers` → `{ "command": "maf-doctor" }` (no `type`) | **merged** | Same server, Claude Code's project-config format. |
| `.github/instructions/maf-doctor.instructions.md` | **Copilot** | front-matter `applyTo: '**'` | **overwritten** | Auto-loaded steering: "for MAF code, call `MafDoctor` first, use the registry before fixing CS0618, prefer `MafAutoFixAll --dry-run`…". |
| `.claude/maf-doctor.md` + one `@import` line in `CLAUDE.md` | **Claude Code** | sidecar file + import | sidecar **overwritten**; import added **once** | Same steering; Claude Code auto-loads `CLAUDE.md`, which pulls in the sidecar. Your own `CLAUDE.md` body is never edited. |
| `AGENTS.md` | **agentskills.io tools** | `<!-- BEGIN maf-doctor -->…<!-- END maf-doctor -->` managed block | block **replaced in place** | Steering for tools that read `AGENTS.md`. Content outside the block is left alone. |
| `.cursor/rules/maf-doctor.mdc` *(only with `--with-cursor`)* | **Cursor** | `.mdc` front-matter (`alwaysApply: true`) | **overwritten** | Cursor rules-dir steering. |

**Why sidecars (not appends):** each tool auto-loads a *specific* file/dir. By owning a dedicated file in that location and overwriting it on every `init`, the guidance **refreshes** with the installed `maf-doctor` version, can never stack duplicates, and never collides with instructions you wrote yourself.

## What it does **not** install

- **The 14 skills** — they're embedded in the `maf-doctor` binary and served live at `maf://skills?name=<name>`; nothing is written to your repo (so they update when you `dotnet tool update`).
- **The 8 specialist agents** (`@maf-auditor`, …) — these are GitHub-Copilot/Coding-Agent definitions that live in *this* toolkit's `.github/agents/`. `init` doesn't copy them; if you want the `@`-mention personas as files, use **Mode 2** (copy this repo's `.github/`). For most work you don't need them — the same workflows are exposed as MCP **prompts** (see [usage.md](./usage.md)).
- **No edits to your own `copilot-instructions.md` / `CLAUDE.md` body.**

## Safety / idempotency

- **MCP config is merge-not-overwrite:** init parses your existing `mcp.json` / `.mcp.json` (JSONC-tolerant — `//` comments + trailing commas allowed), adds only the `maf-doctor` entry, and preserves every other server and top-level key. It also recognizes a legacy `maf-autopilot` server key so a repo configured by a pre-rename build isn't double-entered.
- **Corruption-safe:** if an existing config can't be parsed, init backs it up to `*.bak.<timestamp>` before writing (and caps the read at 1 MB so a malformed file can't OOM the host).
- **Re-runnable:** run `init` again any time to refresh the steering to the installed version — sidecars are rewritten, the `CLAUDE.md` import and `AGENTS.md` block are upserted, never duplicated.

## After `init`

Restart / reload your editor so it picks up the new MCP config, then drive MAF Doctor with the prompts, tools, and agents — see **[usage.md](./usage.md)**.
