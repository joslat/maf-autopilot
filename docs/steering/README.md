# Steering snippets

These three files contain the same canonical guidance for AI assistants
working on Microsoft Agent Framework code. Each is paste-able into a
different convention:

| File | Paste into | Auto-loaded by |
|---|---|---|
| `copilot-instructions.md` | `.github/instructions/maf-doctor.instructions.md` | GitHub Copilot (VS Code / Web / CLI) |
| `claude-instructions.md` | `./CLAUDE.md` (or `.claude/instructions.md`) | Claude Code |
| `agents.md` | `./AGENTS.md` | Cursor + any agentskills.io-respecting tool |
| `cursor-rules.md` | `./.cursorrules` | Cursor (alternative format) |

`maf-doctor init` drops these into your repo automatically (and wires the MCP
server into both `.vscode/mcp.json` for VS Code/Copilot and `.mcp.json` for
Claude Code). These are managed **sidecars**: init *overwrites* them on every
re-run (and upserts the one-line `CLAUDE.md` import + `AGENTS.md` managed block)
rather than merging into — or ever editing — your own instruction files.

If you use multiple AI assistants, keep all three files — they don't
collide.
