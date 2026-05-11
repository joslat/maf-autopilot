---
name: fan-in-static-analyzer
description: "Static analysis of fan-out / fan-in workflow topology. Merged with fan-out-validator — both now point at MafValidateFanOut and MafSimulateWorkflow."
---

# fan-in-static-analyzer — see `fan-out-validator`

This skill has been merged with **[`fan-out-validator`](../fan-out-validator/SKILL.md)** and the procedural walk is now automated by the MCP tools:

- **`MafValidateFanOut(repoPath)`** — Roslyn syntax walk over every `[MessageHandler]` method.
- **`MafSimulateWorkflow(repoPath)`** — full topology graph + Mermaid diagram + per-edge completion forecast.

Load `fan-out-validator/SKILL.md` for the canonical rules, verdict table, and write-time analyzer (`MAF001`) information.
