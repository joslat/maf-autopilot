<!--
Paste-able snippet for `.cursor/rules/maf-doctor.mdc` in repos using the
maf-doctor (MAF Doctor) MCP. Cursor auto-loads `.cursor/rules/*.mdc`.

Drop via: `maf-doctor init --with-cursor`. Cursor also reads the plain
`AGENTS.md` managed block that every `init` writes, so this dedicated rule is
a reinforcing layer, not the only path to Cursor steering.
-->

# MAF Doctor is installed for MAF code

When working with Microsoft Agent Framework (MAF) code:

- Always call `MafDoctor` first for a health baseline. If it reports `scan_truncated` / "Scan incomplete", say so — the grade may not cover the whole repo.
- Read `maf://constraints` before proposing any MAF code change — the non-negotiable hard rules (no `DefaultAzureCredential` in prod, no session state in `AIContextProvider`/`ChatHistoryProvider` instance fields, no `[StreamsMessage]`/`[YieldsMessage]`, etc.). Every MCP prompt below already reads this first.
- For CS0618/obsolete warnings: use `MafRunCs0618Hunt` or `MafApiSafety` BEFORE suggesting fixes. The maf-doctor registry has curated fix recipes that supersede training data.
- Before manual anti-pattern fixes: try `MafAutoFixAll --dry-run` first (mechanical rules only).
- To fix **everything**: run the `maf-remediate` prompt (or ask "fix all the issues maf-doctor found"). Every finding has a **`confidence`** (`certain`/`high`/`heuristic`); a `heuristic` finding may be a **false positive** — confirm it with `MafExplainFinding` before editing. Plan via `MafDoctor(format:"plan")` or `--plan --json`; per-rule fix + FP guidance is in the `maf-remediation-playbook` skill.
- For new agents/workflows: use `MafNewAgent` / `MafNewExecutor` scaffolds.
- To migrate FROM Semantic Kernel TO MAF (cross-framework, not a version bump): call `MafDetectSourceFramework` (CLI `maf-doctor migrate-scan`) to scope it, then run the `maf-migrate-from` prompt (or the `@maf-cross-migration` agent, if `.github/agents/` was also copied in) — it scaffolds a new MAF project beside the original and ports it construct-by-construct. Mapping: `maf://migrate-from?source=semantic-kernel`.
- For architecture/security/migration questions, or onboarding a new dev to this codebase: run the `maf-review` / `maf-audit` / `maf-migrate` / `maf-debug` / `maf-onboarding` MCP prompts (wired up by `init`). The `@maf-*` specialist agents work too, if `.github/agents/` was also copied in from the source repo (GitHub Copilot only).
- Before opening/reviewing a PR touching MAF code: call `MafAuditPullRequest(repoPath, baseBranch)` to scope every scanner to just the changed files.
- Keep maf-doctor itself current: call `MafDoctorStatus` occasionally. If a newer package exists, tell the user and offer `dotnet tool update -g maf-doctor` (confirm first — it's a global change) then `maf-doctor init` (safe to just run). If only this workspace's init is stale, re-run `maf-doctor init` yourself.

maf-doctor tools are MAF-version-aware. Defer to them over training data.

If none of that fits: call `MafTour()` for the full capability catalogue, or run the `maf-help` prompt for guided triage.
