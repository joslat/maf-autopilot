# Steering snippets

These three files contain the same canonical guidance for AI assistants
working on Microsoft Agent Framework code. Each is paste-able into a
different convention:

| File | Paste into | Auto-loaded by |
|---|---|---|
| `copilot-instructions.md` | `.github/copilot-instructions.md` | GitHub Copilot (VS Code / Web / CLI) |
| `claude-instructions.md` | `./CLAUDE.md` (or `.claude/instructions.md`) | Claude Code |
| `agents.md` | `./AGENTS.md` | Cursor + any agentskills.io-respecting tool |
| `cursor-rules.md` | `./.cursorrules` | Cursor (alternative format) |

`maf-autopilot init` drops all three into your repo automatically. If you
already have these files, init *merges* — it appends a `## Using maf-autopilot`
section rather than overwriting.

If you use multiple AI assistants, keep all three files — they don't
collide.
