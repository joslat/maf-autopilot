# Next steps

> **Last refreshed:** 2026-05-12 — end of Phase Q (auto-update loop end-to-end-validated on real MAF 1.4 + 1.5 data).
> **Single source of truth for "what's next."** When this contradicts other docs, this wins.
> **For history of done work:** see [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md).

---

## Index

1. [Current state — one paragraph](#current-state)
2. [Pre-1.0 ship gate](#pre-10-ship-gate)
3. [Active backlog (top of queue)](#active-backlog)
4. [Deferred — with explicit rationale](#deferred)
5. [Acceptance criteria for 1.0 announcement](#acceptance-criteria-for-10-announcement)
6. [Post-1.0 roadmap (1.1+)](#post-10-roadmap)
7. [Out-of-scope](#out-of-scope)

---

## Current state

The toolkit ships **20 MCP tools, 7 agents (including `@maf` primary), 13 skills, 3 always-loaded instructions, 3 Roslyn analyzers (separate NuGet), 5 MCP resources, 7 MCP prompts, multi-arch Docker container, and 6 GitHub Actions workflows** (including the new `maf-ai-fill-todos.yml`). Test suite: **474 passing** (463 main + 11 analyzer). Plan tracking table: **124 items / 113 done / 5 intentionally deferred / 1 external-gated / 5 strategic-remaining**. Security review complete (Phase N). UX + product polish complete (Phase O). May-6 parallel sketch salvaged + tagged (Phase P). **Auto-update loop end-to-end-validated against real MAF 1.4 + 1.5 data with GitHub Copilot Coding Agent for AI fill (Phase Q).** Project is feature-complete for 1.0 — only the external validation gate (A.8) remains.

### What landed in Phase Q (2026-05-12 — auto-update loop validation + AI fill)

| ID | Item | Status |
|---|---|---|
| Q1 | `release.yml` trigger filter: accept both `v*` and `[0-9]*` shapes — alpha-4/5 tags without `v` now auto-fire | ✅ DONE |
| Q2 | `maf-release-watcher.yml` direct-to-main commits (replaced PR-based gate; user prefers main-trunk workflow for solo project) | ✅ DONE |
| Q3 | Replaced unlisted `dnx` NuGet with direct `dotnet-inspect` global install — fixed the watcher's cron failure | ✅ DONE |
| Q4 | `maf-ai-fill-todos.yml` — dispatches GitHub Copilot Coding Agent to fill TODO placeholders post-watcher | ✅ DONE |
| Q5 | GraphQL-based Copilot bot assignment (`addAssigneesToAssignable` with `BOT_kgDOC9w8XQ`) — gh CLI's REST `--assignee Copilot` doesn't work because bots aren't returned by the user-lookup endpoint | ✅ DONE |
| Q6 | Migration-guide banners (Option A) + cumulative `maf-current-migration-guide.md` (Option C) — fixes the "1.4/1.5 guides look thin vs 1.3's 2400 lines" mental-model mismatch | ✅ DONE |
| Q7 | TBD → N/A convention for `guide_section` on new-surface entries (Copilot was instructed "TBD" but it read as procrastination; "N/A" is explicit) | ✅ DONE |
| Q8 | Compat-matrix backfill for 1.4/1.5: pulled real `Microsoft.Extensions.AI` constraints from NuGet API; documented that MAF doesn't pin `Azure.AI.OpenAI` directly (BYO via IChatClient) | ✅ DONE |
| Q9 | `.github/skills/maf-release-watcher/SKILL.md` rewritten to document the 3-stage architecture (deterministic data → AI-fill dispatch → Copilot agent) + the Python helpers + the cumulative guide | ✅ DONE |
| Q10 | End-to-end validation: fired watcher against MAF 1.4 + 1.5; Copilot opened PR #15 with high-quality fills; #15 merged | ✅ DONE |

**The auto-update loop is now demonstrably functional** end-to-end: MAF version ships → watcher commits raw data with TODOs → AI-fill workflow dispatches → Copilot Coding Agent opens a PR with the filled values → human reviews + merges. Future MAF releases require **zero manual baseline-update effort**; the only human input is reviewing Copilot's PR.

### Immediate next moves

1. **Wait for Copilot's PR for MAF 1.5.0** (issue #16 is open and assigned; PR usually lands within ~5 min of issue creation). Review + merge same as #15.
2. **Triage 12 open Dependabot PRs** (#1–#12). See the triage table at the bottom of this document.
3. **(Tier 1) Spin up a real MAF 1.4 or 1.5 test project** to verify Copilot's `example_before`/`example_after` snippets actually compile against the live MAF surface. This is the final piece of "auto-update loop validation."

### What landed in Phase O (2026-05-12 late evening)

| ID | Item | Status |
|---|---|---|
| O1 | `@maf` primary agent for triage (no autonomous handoff — recommendation only) | ✅ DONE |
| O2 | `MafTour` MCP tool + `maf-help` prompt + `maf://help` resource (drift-tested) | ✅ DONE |
| O3 | `MafDraftIssue` MCP tool + `maf-issue-reporter` skill (no auto-post; user reviews + routes via github-mcp-server if installed) | ✅ DONE |
| O4 | Auto-update additivity engraved in code + docs + pinned by 2 new tests | ✅ DONE |
| O5 | Release-watcher re-review — caught + fixed 2 bugs (indent regression detected by O4 test; grep anchor follow-up); added diff-artefact upload | ✅ DONE |

**Bonus discovery (O4 + O5 together):** the additivity unit test surfaced a real bug — `RegistryExtractCommand.BuildYamlEntry` emitted draft entries at column 0, but the production `registry.yaml` indents entries by 2 spaces under `entries:`. The watcher's `cat $TMP_ENTRIES >> $REGISTRY` would have produced malformed YAML on its first real run. Fixed in `BuildYamlEntry` (indent by 2 spaces) + the grep check in the watcher updated to be indentation-tolerant. **The bug never shipped because the watcher hadn't fired against a real MAF release yet.** This is the value of pinning invariants in tests.

---

## Pre-1.0 ship gate

**Exactly one item is required to cut 1.0:**

| ID | Title | Owner | What unblocks it |
|---|---|---|---|
| **A.8** | Cut stable `1.3.0` to NuGet | maintainer | At least one **external** MAF migration validates the toolkit end-to-end against a real customer codebase. After that: `gh workflow run release.yml -F version=1.3.0`. |

The release workflow itself is ready: solution-level test gate, analyzer NuGet pack step, SHA-pinned third-party Actions. Nothing internal blocks A.8.

---

## Active backlog

Items that have measurable value, no external dependency, and are not yet shipped. Listed in recommended execution order.

### Tier A — small, high-leverage, do before announcement

| Order | Item | Effort | Why |
|---|---|---|---|
| ~~A1~~ | ~~Rename `/mcp/` → `/src/`~~ | ✅ DONE 2026-05-12 | Landed; all build/CI/doc paths updated. |
| A1 | **Fire `maf-release-watcher` against real MAF 1.4 / 1.5** | M (½ day, real upstream) | The watcher has shipped untested against an actual major release. Phase O5's bug-hunt cleared two regressions before firing; now run it. Confirms the auto-PR flow end-to-end against real upstream content. |
| A2 | **Verify SHA pins for third-party Actions** | S (15 min) | Three Actions are SHA-pinned with claimed-version comments. Run `gh api repos/<owner>/<repo>/git/refs/tags/<version> --jq .object.sha` for each. Dependabot will catch divergence on first scan too. |
| A3 | **Run external migration** (the A.8 unblocker) | L (1–3 days, external) | Pair-program a migration against a real MAF 1.2 → 1.3 customer codebase. Use the output to validate the toolkit, file any gaps, then cut 1.0. |

### Tier B — could land alongside A or in a 1.0.1 patch

| Order | Item | Effort | Why |
|---|---|---|---|
| B1 | **C.6 — full Roslyn data-flow `MafScanPromptInjection`** | L (1–2 days) | Currently covered by `MafLintAgentPrompt`'s PROMPT-004 (syntactic match). Data-flow would catch the case where untrusted input flows through a variable into `Instructions =` indirectly. |
| B2 | **C.7 — `maf_open_feedback_issue` MCP sampling tool** | M (½ day) | Closes the learning loop: when a scan fires no registry match, the tool opens a GitHub issue with the snippet so the registry can be expanded. Depends on host sampling support. |
| B3 | **C.8 — `maf_full_audit`, `maf_migration_suggest` sampling tools** | M (1 day) | Multi-step agentic chains. Depend on MCP host sampling. |
| B4 | **Top-level `permissions: {}` defaults in all 5 workflows** | S (15 min) | Defense-in-depth (Phase N B.M1 deferred). No exploit demonstrated; this is hygiene. |

### Tier C — strategic / multi-day

| Order | Item | Effort | Why |
|---|---|---|---|
| C1 | **D.4 — GitHub App `@maf-bot`** | L (3–5 days) | PR comments scoped to changed lines; org-wide adoption vector. Pairs with `MafAuditPullRequest`. Distinct codebase. |
| C2 | **D.5 — VS Code extension** | L (3–5 days) | One-click installer wrapping the MCP server; surfaces `MafDoctor` as a status-bar item. Distinct codebase. |
| C3 | **Release-watcher trust-chain redesign** (Phase N B.H2 deferred) | M (1 day) | Quarantine upstream NuGet content into a `drafts/` folder before merging to the embedded `registry.yaml`. Requires workflow rework. |

---

## Deferred

Items considered and *intentionally* not done, each with its rationale. Don't re-litigate without reading the reason.

| ID | Item | Why deferred |
|---|---|---|
| B.4 | Split `maf-auditor` into two agents | `maf-best-practice-reviewer` already exists as the sibling. Renaming `maf-auditor` → `maf-migration-planner` is cosmetic; defer until post-1.0. |
| B.8 | Project rename (`maf-autopilot` → `maf-keeper` / `maf-forge` / etc.) | Naming exercise produced `maf-forge` (Catchiness 9/10) as the leading recommendation. User decision deferred. Rename pre-1.0 is cheaper than post-1.0; if not decided before A.8, ship 1.0 as `maf-autopilot`. |
| M.4 | Mass-rename 6 unprefixed skills to `maf-*` | 38-file blast radius; creates double-prefixed URIs (`maf://skills?name=maf-fan-out-validator`); breaks any hardcoded URI. Current naming is principled (codified in CONTRIBUTING.md "Skill naming convention"). |
| M.5 | Rename `MafNewAgent` / `MafNewExecutor` → `MafScaffoldAgent` / `MafScaffoldExecutor` | "New" is the universal dev-tool verb (`dotnet new`, `cargo new`, `npm init`). CLI is `maf-autopilot new agent`. Breaking surface change for marginal accuracy gain. |
| N.12 (B.M3) | `${{ ... }}` → `env:` pattern enforcement in workflows | Defense-in-depth; no current `pull_request_target` workflow exists, so the bug class isn't reachable today. |
| N.12 (A.M3) | `IsValidVersion` / `IsValidPackageId` reject leading `-` | Defense-in-depth; `ArgumentList` boundary intact, no current escape path. |
| N.12 (others) | Rule-ID unification (`MAF001` ↔ `MAF-AP-SEC-001`), shared `netstandard2.0` `IsTestFile` project, IAnalyzer/IScanner interface | Cross-referenced in `maf://rules` catalog (good enough); renaming would break consumer projects pinning rule IDs in `.editorconfig`. |

---

## Acceptance criteria for 1.0 announcement

All of the following are **already true** unless marked otherwise:

- ✅ All confirmed security findings remediated. (Phase N)
- ✅ All 17 MCP tools have direct entry-method tests. (Phase G.3)
- ✅ Scaffolder output passes our own scanners (dogfood + compile-validation). (Phase F.4 + I.3)
- ✅ CHANGELOG complete and Keep-a-Changelog-formatted. (Phase G.1)
- ✅ README counts reconciled (17 tools, 12 skills, 6 agents, ~426 tests). (Phase H.1)
- ✅ `MafDoctor` output recommends only extant tools. (Phase H.2)
- ✅ Roslyn analyzer NuGet published path verified in release.yml. (Phase N.8)
- ✅ Container runs as non-root. (Phase N.5)
- ✅ Dependabot configured for NuGet + Actions + Docker. (Phase N.9)
- ⏳ **One external migration validates the toolkit end-to-end.** (gates A.8)

---

## Post-1.0 roadmap

Sketch of where 1.1 and 1.2 should focus, in priority order:

**1.1 — Production lifecycle deepening.** B1 (`MafScanPromptInjection` Roslyn data-flow), B2 (`maf_open_feedback_issue`), C3 (release-watcher trust-chain). The theme: close the production-incident loop that Phase J started with `maf-incident-responder`.

**1.2 — Adoption vectors.** C1 (`@maf-bot` GitHub App), C2 (VS Code extension). The theme: meet developers where they already are.

**1.3+ — Vision questions.** Multi-version migration paths (currently `MafMigrationPath` walks guide metadata; extend to actually scaffold the intermediate refactor steps), MAF-version-watching beyond 1.x (matrix entries for 2.x will need to land), Foundry-deployment skill (currently a gap — see Phase J residue).

---

## Annex — May-6 pre-Phase-O sketch (tag `pre-phase-o-may6-sketch`)

A parallel exploration of the same conceptual ground existed on `origin/main` from 2026-05-06, in snake_case `[McpServerTool(Name = "maf_xxx")]` / bundled-class style. After the Phase H-O work superseded most of it, the May-6 commits were preserved by **git tag `pre-phase-o-may6-sketch`** at SHA `4525ab8` (run `git checkout pre-phase-o-may6-sketch` to recover the full working tree).

### Salvaged into the current codebase

| From May-6 | Ported as | Where it lives now |
|---|---|---|
| `AnalysisTools.maf_executor_pattern_check` (regex search for legacy executor patterns) | Anti-pattern rule **`MAF-AP-EXEC-001`** (Roslyn syntax-scan, error severity) | `src/maf-autopilot/Tools/AntiPatternScannerTool.cs` |
| `AnalysisTools.maf_compatibility` (static MAF-version → deps matrix) | New MCP tool **`MafCompatibility`** + drift-tested against `docs/compatibility-matrix.md` | `src/maf-autopilot/Tools/CompatibilityTool.cs` |
| `MafPrompts.maf-review` | MCP prompt **`maf-review`** (PascalCase tool routing) | `src/maf-autopilot/Prompts/MafPrompts.cs` |
| `MafPrompts.maf-debug` | MCP prompt **`maf-debug`** (PascalCase tool routing) | `src/maf-autopilot/Prompts/MafPrompts.cs` |
| `MafPrompts.maf-scaffold` | MCP prompt **`maf-scaffold`** (routes to `MafNewAgent` / `MafNewExecutor`) | `src/maf-autopilot/Prompts/MafPrompts.cs` |

### Deferred to future Tier B work (good seed material at the tag)

| At the tag | Use when |
|---|---|
| `SamplingTools.maf_full_audit` (full repo audit via `server.SampleAsync()`) | MCP host sampling is widely supported. Sketch shows the prompt + flow shape. |
| `SamplingTools.maf_migration_suggest` (LLM-driven per-snippet migration suggestion) | Same — depends on host sampling. |
| `SamplingTools.maf_review_code` (LLM-powered code review) | Same. Complements `maf-review` prompt by delegating to LLM rather than routing to deterministic tools. |
| `SamplingTools.maf_explain_error` (LLM-driven error explanation) | Same. Complements `MafExplain` (which is deterministic). |
| `FeedbackTool.maf_open_feedback_issue` (sampling-based issue draft) | Same. Today's `MafDraftIssue` is deterministic; the sampling version would generate richer prose when host LLM is available. |
| `ScaffoldTool.maf_scaffold` `session-provider` + `a2a-server` templates | When demand surfaces for these scaffolds beyond `agent` + `executor`. ~1 hour each to port in local style. |

### How to recover the May-6 working tree

```powershell
# View the tag (annotated) — explains what's in it and what got salvaged
git show pre-phase-o-may6-sketch

# Check out the May-6 tree at a temp branch (won't affect main)
git checkout -b inspect-may6 pre-phase-o-may6-sketch

# When done, get back to main
git checkout main
git branch -D inspect-may6
```

The tag is pushed to origin, so it's recoverable from any clone.

---

## Open Dependabot PRs — triage table (2026-05-12)

| # | Change | Risk | My recommendation | Why |
|---|---|---|---|---|
| 1 | `docker dotnet/sdk` 9.0 → 10.0 | **HIGH (incompat)** | **Close** — re-open when bumping TFM | Our solution targets `net9.0`. Bumping the build-stage SDK image to 10.0 could change build defaults; not worth it without a TFM bump. |
| 2 | `docker dotnet/runtime` 9.0 → 10.0 | **HIGH (incompat)** | **Close** | Same reasoning — our runtime image must match the net9.0 TFM we publish. |
| 3 | `docker/setup-buildx-action` 3 → 4 | LOW | **Merge** | Docker actions are usually backward-compat. Low blast radius. |
| 4 | `docker/login-action` 3 → 4 | LOW | **Merge** | Same. |
| 5 | `actions/upload-artifact` 4 → 7 | MEDIUM | **Verify changelog, then merge** | v5 had a breaking artifact-name change. Check whether v7 has more. Used in release-watcher's diff-artefact upload. |
| 6 | `softprops/action-gh-release` 2.2.1 → 3.0.0 | MEDIUM | **Verify SHA in PR diff, then merge** | This action is SHA-pinned per Phase N's hardening (commit `c95fe14...`). Dependabot should have updated both the version AND the SHA — verify in the PR diff. |
| 7 | `actions/checkout` 4 → 6 | MEDIUM | **Verify changelog, then merge** | v5 had a Node.js bump; v6 may have more. Used everywhere — high blast radius if it breaks. |
| 8 | `coverlet.collector` 6.0.2 → 10.0.0 | MEDIUM | **Verify — run tests** | Major version bump 4 versions apart. Coverage might behave differently. |
| 9 | grouped `Microsoft.CodeAnalysis.Analyzers` + `CSharp` (Analyzers project) | **HIGH** | **Verify — run all analyzer tests** | The Analyzers project's SDK. Major bumps may break the analyzer or its test harness. |
| 10 | `Microsoft.CodeAnalysis.CSharp` 4.11.0 → 5.3.0 (Analyzers.Tests) | **HIGH** | **Verify — coordinate with #9** | Companion to #9. Run analyzer tests after merging both. |
| 11 | `Microsoft.Extensions.Hosting` 9.0.0 → 10.0.7 | **HIGH (incompat)** | **Close** | Forces .NET 10 ecosystem. We target net9.0. Re-open when bumping TFM. |
| 12 | `Microsoft.NET.Test.Sdk` 17.12.0 → 18.5.1 | MEDIUM | **Verify — run all tests** | Major bump. Usually backward-compat but worth a CI run. |

**Suggested execution order:**

1. **Close #1, #2, #11** (the three .NET-10-only bumps). Comment with "deferred until we bump TFM to net10.0."
2. **Merge #3, #4** (Docker actions — low risk).
3. **Open #6's diff** and verify the SHA was updated alongside the version. If yes, merge.
4. **Open #5's and #7's changelogs**, scan for breaking changes. If clean, merge.
5. **Merge #9 + #10 together** in a single combined branch. Run all analyzer tests after.
6. **Merge #12 last**, run all tests.
7. **Merge #8 last** (coverage infrastructure).

The audit failures on these PRs from May 11 were because the pinned `maf-autopilot` NuGet was `1.3.0-alpha-3` (no `doctor` CLI). With `1.3.0-alpha-5` now published (with the doctor CLI), re-running the audit on each PR should now succeed. To re-trigger: push an empty commit to the PR branch, or use the PR's "Re-run all jobs" button.

---

## Out-of-scope

Things this project has explicitly decided **not** to do:

- **Code execution / sandboxing.** The MCP server reads files, parses them with Roslyn syntax-only, and emits markdown / SARIF. It does NOT execute user code. `MafRunCs0618Hunt` shells out to `dotnet build` — that's compilation, not execution.
- **A general-purpose linter.** Anti-pattern scanner is MAF-specific. If a rule wouldn't bite a MAF user, it doesn't ship.
- **A migration tool for non-MAF agent frameworks.** The skill content, registry, and analyzer rules all target Microsoft Agent Framework. Other frameworks would need their own toolkits.
- **AI generation of fix patches.** Scanners report findings + cite the registry/constraint. The actual fix is the developer's decision (or Copilot Chat's, using the cited entry). The scanner does not auto-patch.
