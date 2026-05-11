# Next steps

> **Last refreshed:** 2026-05-12 late evening — end of Phase O (UX + product polish + watcher hardening).
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

The toolkit ships **19 MCP tools, 7 agents (including `@maf` primary), 13 skills, 3 always-loaded instructions, 3 Roslyn analyzers (separate NuGet), 5 MCP resources (including `maf://help`), 4 MCP prompts (including `maf-help`), multi-arch Docker container, and 5 GitHub Actions workflows**. Test suite: **456 passing** (445 main + 11 analyzer). Plan tracking table: **121 items / 110 done / 5 intentionally deferred / 1 external-gated / 5 strategic-remaining**. Security review complete (Phase N). UX + product polish complete (Phase O): single primary `@maf` agent, capability tour, issue-drafter, additivity engraved + watcher hardened. Project is feature-complete for 1.0 — only the external validation gate (A.8) remains.

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

## Out-of-scope

Things this project has explicitly decided **not** to do:

- **Code execution / sandboxing.** The MCP server reads files, parses them with Roslyn syntax-only, and emits markdown / SARIF. It does NOT execute user code. `MafRunCs0618Hunt` shells out to `dotnet build` — that's compilation, not execution.
- **A general-purpose linter.** Anti-pattern scanner is MAF-specific. If a rule wouldn't bite a MAF user, it doesn't ship.
- **A migration tool for non-MAF agent frameworks.** The skill content, registry, and analyzer rules all target Microsoft Agent Framework. Other frameworks would need their own toolkits.
- **AI generation of fix patches.** Scanners report findings + cite the registry/constraint. The actual fix is the developer's decision (or Copilot Chat's, using the cited entry). The scanner does not auto-patch.
