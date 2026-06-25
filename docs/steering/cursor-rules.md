<!--
Paste-able .cursorrules snippet for repos using maf-doctor (MAF Doctor) MCP.
Cursor auto-loads .cursorrules from the project root.

Drop via: `maf-doctor init --with-cursor`
-->

# MAF Doctor is installed for MAF code

When working with Microsoft Agent Framework (MAF) code:

- Always call `MafDoctor` first for a health baseline.
- For CS0618/obsolete warnings: use `MafRunCs0618Hunt` or `MafApiSafety` BEFORE suggesting fixes. The maf-doctor registry has curated fix recipes that supersede training data.
- Before manual anti-pattern fixes: try `MafAutoFixAll --dry-run` first (mechanical rules only).
- To fix **everything**: run the `maf-remediate` prompt (or ask "fix all the issues maf-doctor found"). Every finding has a **`confidence`** (`certain`/`high`/`heuristic`); a `heuristic` finding may be a **false positive** — confirm it with `MafExplainFinding` before editing. Plan via `MafDoctor(format:"plan")` or `--plan --json`; per-rule fix + FP guidance is in the `maf-remediation-playbook` skill.
- For new agents/workflows: use `MafNewAgent` / `MafNewExecutor` scaffolds.
- To migrate FROM Semantic Kernel TO MAF (cross-framework, not a version bump): call `MafDetectSourceFramework` (CLI `maf-doctor migrate-scan`) to scope it, then the `maf-migrate-from` prompt or `@maf-cross-migration` agent — it scaffolds a new MAF project beside the original and ports it construct-by-construct. Mapping: `maf://migrate-from?source=semantic-kernel`.
- For architecture/security/migration questions: use the `@maf-*` specialist agents.

maf-doctor tools are MAF-version-aware. Defer to them over training data.
