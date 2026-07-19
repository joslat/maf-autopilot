# Steering snippets

These four files contain the same canonical guidance for AI assistants
working on Microsoft Agent Framework code (`cursor-rules.md` is a condensed
version, not identical prose). Each is paste-able into a different
convention:

| File | Paste into | Auto-loaded by |
|---|---|---|
| `copilot-instructions.md` | `.github/instructions/maf-doctor.instructions.md` | GitHub Copilot (VS Code / Copilot CLI) |
| `claude-instructions.md` | `.claude/maf-doctor.md` (sidecar) + a one-line `@.claude/maf-doctor.md` import in `CLAUDE.md` | Claude Code |
| `agents.md` | `./AGENTS.md` (sentinel-delimited managed block) | GitHub Copilot coding agent, Cursor, and any other agentskills.io-respecting tool |
| `cursor-rules.md` | `.cursor/rules/maf-doctor.mdc` | Cursor (only with `maf-doctor init --with-cursor`) |

As of 2025-08, GitHub Copilot's coding agent and Cursor both read `AGENTS.md`
natively — so `agents.md` alone already gives those two tools baseline
steering with no flag needed. `.github/instructions/...` and
`.cursor/rules/...` are dedicated, more specific reinforcements on top of
that (and `--with-cursor` is opt-in because it wasn't always redundant with
`AGENTS.md`).

`maf-doctor init` drops these into your repo automatically (and wires the MCP
server into both `.vscode/mcp.json` for VS Code/Copilot and `.mcp.json` for
Claude Code). These are managed **sidecars**: init *overwrites* them on every
re-run (and upserts the one-line `CLAUDE.md` import + `AGENTS.md` managed block)
rather than merging into — or ever editing — your own instruction files.

If you use multiple AI assistants, keep all four files — they don't
collide.
