<!--
Paste-able snippet for `.github/instructions/maf-doctor.instructions.md` in
any repo that uses the maf-doctor (MAF Doctor) MCP. This file is automatically
loaded by GitHub Copilot (VS Code, Copilot CLI) when present.

`maf-doctor init` writes this as a sidecar and OVERWRITES it on every re-run —
don't hand-edit the delivered copy. For durable, hand-authored instructions,
use your own `.github/copilot-instructions.md` instead (init never touches it).
-->

## Using maf-doctor for Microsoft Agent Framework code

This repository uses Microsoft Agent Framework. The maf-doctor MCP server
(also called "MAF Doctor") is installed and exposes tools for diagnosing,
fixing, and migrating MAF code.

**Before answering MAF questions or proposing changes:**

1. **Always call `MafDoctor` first** on the repo path to get the current
   health grade (A-F) and the top issues. Don't speculate about MAF
   quality without this baseline. If the report notes a **"Scan
   incomplete"** (or `scan_truncated` / `files_scanned` in `--json`), say
   so — the repo hit a size cap and the grade may not cover the whole
   codebase.

2. **Read `maf://constraints` before proposing any MAF code change.** These
   are the non-negotiable hard rules (e.g. never `DefaultAzureCredential`
   in production, never store session state in `AIContextProvider` /
   `ChatHistoryProvider` instance fields, never `[StreamsMessage]` /
   `[YieldsMessage]`) — check every suggestion against them, not just
   training-data intuition. Every MCP prompt below already reads this
   first; do the same for anything you answer outside of a prompt.

3. **For any `[Obsolete]` warning, `CS0618` / `CS0246` diagnostic, or build
   failure mentioning a MAF type** — call `MafRunCs0618Hunt` (full project
   scan) or `MafApiSafety` (single symbol) BEFORE suggesting a fix. The
   maf-doctor registry has curated fix recipes that supersede your
   training data — MAF ships breaking changes every minor version, so
   training data is likely outdated.

4. **To fix issues** — `MafAutoFixAll(repoPath)` previews (dryRun defaults to
   true), `MafAutoFixAll(repoPath, dryRun: false)` applies. Handles the
   *mechanical* rules deterministically (offer this first; the rewrites are
   tested). To fix **everything**, run the `maf-remediate` prompt (or just
   ask "fix all the issues maf-doctor found"): it grades → plans → autofixes
   → then works each semantic finding. Every finding carries a **`confidence`**
   (`certain` / `high` / `heuristic`); a **`heuristic`** finding may be a
   **false positive** — confirm it with `MafExplainFinding` before editing.
   Get the plan via `MafDoctor(format: "plan")` (human) or `--plan --json`
   (structured manifest); per-rule fix + false-positive guidance lives in the
   `maf-remediation-playbook` skill.

5. **When designing a new MAF agent or workflow** — call `MafNewAgent` /
   `MafNewExecutor` for scaffolds, or `MafSimulateWorkflow` for topology
   preview. Don't reconstruct patterns from memory.

6. **For deep architectural / security / migration questions, or onboarding
   a developer new to this codebase** — run the `maf-review`
   (best-practice audit), `maf-audit` (pre-migration scan + plan),
   `maf-migrate` (execute a migration-plan.md), `maf-debug` (diagnose a
   symptom), or `maf-onboarding` (guided first-day tour) MCP prompts —
   `init` wires these up automatically, no extra setup needed. If
   `.github/agents/` was also copied in from the maf-doctor source repo
   (GitHub Copilot only — see the toolkit's init-reference.md), the
   equivalent `@maf-best-practice-reviewer`, `@maf-auditor`,
   `@maf-migration`, `@maf-incident-responder`, or `@maf-onboarding`
   personas work the same way.

7. **To migrate FROM Semantic Kernel TO MAF** (a cross-framework port, NOT a
   MAF version bump) — call `MafDetectSourceFramework` (CLI:
   `maf-doctor migrate-scan`) to inventory SK usage and scope it, then run the
   `maf-migrate-from` prompt (or the `@maf-cross-migration` agent, if
   `.github/agents/` was also copied in). It scaffolds a **new MAF project
   beside the original** and ports it construct-by-construct,
   non-destructively. The mapping lives at `maf://migrate-from?source=semantic-kernel`.

8. **Before opening or reviewing a pull request that touches MAF code** —
   call `MafAuditPullRequest(repoPath, baseBranch)` to scope every scanner
   to just the files this branch changed, instead of a full-repo scan.

9. **Keep maf-doctor itself current.** MAF ships breaking changes often, so a
   stale install means stale fix recipes. Call `MafDoctorStatus` occasionally
   (start of a session, or if guidance seems off) — it reports whether the
   installed package is current and whether this workspace's steering needs a
   refresh. If a newer package exists, tell the user and offer
   `dotnet tool update -g maf-doctor` (a global, machine-wide change — confirm
   with the user first) followed by `maf-doctor init` (repo-scoped and
   idempotent — safe to just run). If only this workspace's init is stale,
   just re-run `maf-doctor init` yourself.

maf-doctor tools are MAF-version-aware via `applies_to_codebases` markers
in the registry — they know which fix applies to which MAF version. Defer to
the tools.

If none of the above fits, call `MafTour()` for the full capability
catalogue (every tool / prompt / resource / agent, one line each), or run
the `maf-help` prompt for guided 3-question triage.
