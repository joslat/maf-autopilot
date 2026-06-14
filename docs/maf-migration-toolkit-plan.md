# maf-autopilot — Full Plan (historical archive + tracking table)

> ## 🚀 Looking for "what's next?" → read [`docs/next-steps.md`](./next-steps.md)
>
> [`next-steps.md`](./next-steps.md) is the **single source of truth for active backlog, deferred items, ship gates, and post-1.0 roadmap**. This document does NOT replicate that content.
>
> ## 📚 What this document is, then
>
> This is the **historical archive** — every phase, every numbered tracking-table row, every decision narrative. Read this when you need to answer:
> - *"Why did we choose X?"* — see the phase section that landed X
> - *"When did Y land?"* — see the tracking table
> - *"What did we deliberately not do?"* — see the deferred / intentionally-rejected entries in each phase
>
> When this doc and `next-steps.md` disagree on what's pending, **`next-steps.md` wins** — this doc is updated less aggressively. See [`docs/index.md`](./index.md) for the full doc catalogue.
>
> **Last updated:** 2026-05-12 — Phase N (security review + remediation) shipped.

---

## 🗺️ Phased Roadmap (start here)

> The numerical tracking table below records every item ever planned (#1–66). This roadmap groups them by **implementation phase** — read this first to find what's next.
>
> Numbers in `(#NN)` refer to the detailed entries in the tracking table further down.

### Phase 0 — Foundation (completed)

Every core feature shipped between project inception and 2026-05-12 morning.

- **Agents & skills authored** (#1–14): three agents, 12 skills, instructions, registry skeleton.
- **Guide & matrix** (#16–19): 21-section migration guide, compatibility matrix, `.maf-version`, README.
- **Version-keyed sections** (#20).
- **MCP server primitives** (#21–24): Tools + Resources + Prompts + `init` command.
- **2026-05-11 sprint** (P0/P1 batch — #33–42): dotnet-inspect 0.7.8 bump, install command fix, test harness (151→197 tests), JSONC parser fix, inverted-index search, README/plan reconcile, CONTRIBUTING + TROUBLESHOOTING, 3 new executable tools (`MafRunCs0618Hunt`, `MafValidateFanOut`, `MafDiffPackage`), best-practice reviewer agent + `MafScanAntiPatterns`.
- **Magic-path sprint** (2026-05-11 night): `maf-new` scaffolder (#60), Roslyn analyzer companion (#29), `MafSimulateWorkflow` (#65), `MafExplain` (#64).
- **Post-magic Opus fixes** (2026-05-12 morning): variable-binding resolution in `MafSimulateWorkflow` (the silent-✅ failure mode), `UseOpenTelemetry(sourceName:)` in scaffolder, `..` traversal protection in `projectPath`, broader test-file heuristic (`.UnitTests`/`.IntegrationTests`/`/e2e`), `ExplainTool` allowlist tightening (drop noisy substring fallback). 201 tests across two projects.
- **Phase A.1–A.6** (2026-05-12): agents rewired to call MCP tools (#55); registry expanded from 10 → 13 entries + 4 new scanner rules `MAF-AP-AGENT-001` / `MAF-AP-WF-001` / `MAF-AP-MID-001` / `MAF-AP-DEVUI-001` covering Instructions placement, sealed-partial executors, middleware streaming, DevUI guard (#56); redundant SKILLs slimmed and fan-out duo merged (#57); SARIF v2.1.0 export tools `MafScanAntiPatternsSarif` + `MafValidateFanOutSarif` (#58); `MafDoctor` aggregator tool + CLI subcommand `maf-doctor doctor` (#59); release-watcher bugs all fixed — correct repo, dynamic version anchor, prerelease filter, retry, idempotency, per-version guide files (#46). **235 tests passing.**
- **Phase B.1, B.3, B.5, B.6** (2026-05-12 afternoon): `MafLintAgentPrompt` MCP tool with 4 rules — PROMPT-001 empty / PROMPT-002 token bloat / PROMPT-003 missing refusal pattern / PROMPT-004 untrusted-input concatenation (prompt injection); `init` refactored to write a thin pointer to `maf://constraints` (no more drift); major-version detection in watcher (escalates PR title + labels); smoke-tester skill slimmed to point at `MafNewExecutor` for the compile-validated fan-out shape pattern. **256 tests passing.**
- **Phase C.2, C.3, C.4, C.5** (2026-05-12 evening): four new MCP tools land — `MafAuditPullRequest` (PR-diff-scoped scan via `git diff`), `MafPreUpgradeDryRun` (combines `MafDiffPackage` + repo grep to surface user-specific breaking-change impact BEFORE upgrade), `MafEstimateCost` (token-cost auditor over every `RunAsync`/`RunStreamingAsync` site, flags missing `MaxOutputTokens`), and two new GitHub Actions workflows (`maf-drift-detector.yml` weekly + `maf-pr-audit.yml` per-PR). **292 tests across both projects.**
- **Phase C.1, D.1, D.2** (2026-05-12 late): registry auto-update closes the self-improvement loop — new CLI `maf-doctor registry-extract <pkg> <oldVer> <newVer>` emits draft YAML registry rows from a `dotnet-inspect diff`; wired into the watcher to auto-append to `registry.yaml` in the upgrade-PR. New `MafMigrationPath(currentVer, targetVer)` tool walks version-keyed guide metadata to plan multi-step migrations (1.0 → 1.3 instead of 1.2 → 1.3). Dockerfile + `docker-publish.yml` workflow ship a multi-arch (amd64+arm64) GHCR image with semver-keyed tags. **325 tests across both projects.**
- **Phase E — 1.0 polish sprint** (2026-05-12 final): post-4-Opus-review polish landing every HIGH-severity finding. `PathGuard` shared helper applied to all 8 `repoPath`/`projectPath` MCP tools (kills the path-traversal hole the MCP-surface review flagged). `SourceFileWalker` shared helper deduplicates 17 instances of `EnumerateScannableFiles` + `MakeRelative` across 7 tools (architecture-review finding). `MafDoctor` now aggregates **4 of 19 scanners** (anti-pattern + fan-out + prompt-lint + cost) and reports each in the metrics block. `init` CLI output now leads with `maf-doctor doctor .` + `maf-doctor new agent ChatBot` (closes the "no hello world" cliff from the usability review). README reconciled: tool count 3→19, skill count 11→12, agents 2→3, Docker/Analyzer marked DONE; new "First 5 minutes" section with three concrete commands. `NewCommand` next-steps include `dotnet add package` hints for greenfield. **csproj version reverted `1.3.0-alpha-5` → `1.3.0-alpha-3` to match NuGet reality** (csproj-vs-NuGet drift fix). **New `maf://rules` resource** auto-generated at runtime from `AntiPatternScannerTool.AllRules` + curated prompt-lint + analyzer rules — kills future doc drift structurally. Analyzer `helpLinkUri` anchors fixed (slugified fragments don't resolve on GitHub). TROUBLESHOOTING test count 116→325. **344 tests passing.**

### Phase E — Post-Opus polish (latest sprint, 2026-05-12 final)

After the 4-Opus parallel review of the magic-path + Phase C/D output, the toolkit landed a final round of HIGH-severity polish. All 6 items complete.

| Order | Item | Why | Status |
|---|---|---|---|
| E.1 | **#67** `PathGuard` applied to 8 tools | Path-traversal hole on every `repoPath` tool | ✅ DONE |
| E.2 | **#71** README reconcile + "First 5 minutes" | Usability tier-1 UX bug | ✅ DONE |
| E.3 | **#71** csproj version drift + TROUBLESHOOTING count fix | csproj `1.3.0-alpha-5` ≠ NuGet `1.3.0-alpha-3` | ✅ DONE |
| E.4 | **#69** `MafDoctor` aggregates 4 of 19 scanners | Doctor was overclaiming by name | ✅ DONE |
| E.5 | **#68** `SourceFileWalker` shared helper | 17 → 1 (file-walk duplication) | ✅ DONE |
| E.6 | **#70** + **#72** `maf://rules` resource + NewCommand pkg hints + analyzer anchor fixes | Doc drift killer + greenfield package hints + GitHub anchor resolution | ✅ DONE |

### Phase F — Post-final-review polish (post-3rd-Opus, 2026-05-12)

3 parallel Opus reviews (1.0-readiness, tool surface, code hygiene) landed concurrently. Phase F captures the 5 highest-leverage items found across them.

| Order | Item | Why | Status |
|---|---|---|---|
| F.1 | Delete redundant `maf-cs0618-hunt` prompt → rewrite to call `MafRunCs0618Hunt` directly | MCP review #5: prompt was instructing the LLM to run bash by hand, bypassing the deterministic tool | ✅ DONE |
| F.2 | SARIF-twin consolidation: `MafScanAntiPatternsSarif` + `MafValidateFanOutSarif` collapsed into `format: "sarif"` param on the parent tools | MCP review #1: 19 → 17 surface; one tool per scanner, two output formats | ✅ DONE |
| F.3 | `RegistryExtractCommand` uses YamlDotNet serializer | Arch review #4: string-concat YAML was fragile against any special character | ✅ DONE |
| F.4 | Compile-validation test for scaffolder output (`CSharpCompilation.Create` against MAF stub) | Tests review #1: dogfood only catches scanner-clean, not compilable | ✅ DONE — caught 2 real template bugs on first run |
| F.5 | Final 3-Opus parallel deep review (1.0-readiness + tool surface + code hygiene) | Pre-1.0 sign-off | ✅ DONE 2026-05-12 |

### Phase G — Verdict-driven gap closure (post-3rd-Opus, 2026-05-12)

After Phase F.5 landed the third Opus review, three review-flagged blockers became the top of the queue.

| Order | Item | Why | Status |
|---|---|---|---|
| G.1 | CHANGELOG.md (Keep-a-Changelog format, all 1.3.0-alpha versions) | 1.0-readiness review #2: missing changelog blocks announcement | ✅ DONE |
| G.2 | YAML `ScalarStyle` on `example_before`/`example_after`/`notes` (literal + folded) | Tool-surface review #1: auto-PRs visually diverged from existing registry style | ✅ DONE |
| G.3 | Direct MCP-method tests for `MafNewAgent` + `MafNewExecutor` | Tool-surface review #2: only "write-to-disk" tools without direct end-to-end coverage | ✅ DONE (14 new tests in `NewAgentToolMcpTests`) |
| G.4 | Stale doc counts (CONTRIBUTING 54→74, csproj/MafResources 11→12 skill, agent 8→10 rules) + maf-migrate prompt refresh | Doc drift sweep + maf-migrate prompt now invokes tools directly | ✅ DONE |

### Phase H — Third-Opus blocker fixes (1.0-announce gate, 2026-05-12)

The third Opus review surfaced 3 announcement-blocking regressions during the polish round. All fixed.

| Order | Item | Why | Status |
|---|---|---|---|
| H.1 | README inconsistencies (lines 21/151 still "19 tools" + listed removed SARIF twins; line 153 test count ~314 → ~354) | Public front door contradicted itself | ✅ DONE |
| H.2 | `DoctorTool.cs:240` still recommended `MafScanAntiPatternsSarif` / `MafValidateFanOutSarif` (removed in F.2) | Every `MafDoctor` user saw stale advice | ✅ DONE |
| H.3 | `AgentTestTemplate` missing `using System / System.Collections.Generic / System.Threading / System.Threading.Tasks` (same bug class F.4 caught — but in a sibling template, masked by test project's `<ImplicitUsings>enable</ImplicitUsings>`) | Would fail to build for any user project with implicit usings disabled | ✅ DONE — also extended `ScaffolderCompileValidationTests` to compile the test stubs alongside their sibling, reproducing the implicit-usings-disabled scenario |
| H.4 | Stale comments: csproj "11 skill" → 12, MafResources doc → 12, TROUBLESHOOTING count, agent file "8 rules" → 10 | Cosmetic doc drift | ✅ DONE |

### Phase I — Test-coverage deepening (post-1.0 prep, 2026-05-12)

Opus test-coverage audit identified the top 5 leverage gaps. All shipped — every test is hermetic (mocks / pure in-memory / single GUID temp dir), AAA-structured.

| Order | Item | Why | Status |
|---|---|---|---|
| I.1 | Extracted `PullRequestAuditTool.ParseGitDiffOutput` + 5 hermetic parser tests | Shell-out boundary; CRLF / bin-obj / case-insensitive .cs filter regressions hide here | ✅ DONE |
| I.2 | `Cs0618HuntTool.ResolveBuildTarget` made internal + 5 path-resolution tests (single-temp-dir IDisposable pattern) | Path resolution runs BEFORE `dotnet build`; silent miss looks identical to a build failure | ✅ DONE |
| I.3 | Scaffolder output now scanned against `MafScanAntiPatterns` + `MafValidateFanOut` in 3 new compile-validation tests | Generator contract "passes our own scanners" was asserted in docstring but unverified | ✅ DONE |
| I.4 | `DoctorTool.Grade` composition test wiring all 4 real scanners against a single source | Catches breakage in the 4-scanner fan-in; pure-core had unit coverage but not composition | ✅ DONE |
| I.5 | `ExplainTool` namespace-qualified type use test | Catches regression if `AddIdentifier` ever strips namespace prefixes or haystack search narrows | ✅ DONE |

### Phase J — Production-lifecycle agents (post-1.0, 2026-05-12)

The 3-agent surface was event-driven (`migrate`, `audit`, `best-practice`). Phase J adds the temporal/production-facing dimension.

| Order | Item | Why | Status |
|---|---|---|---|
| J.1 | `maf-incident-responder.agent.md` | Production failure → MAF pattern responsible → deterministic fix. Closes the gap "compiles clean, fails at 3am" | ✅ DONE |
| J.2 | `maf-rollback-agent.agent.md` | Inverse of migration. Surgical 1.3.0 → 1.2.0 retreat; preserves unrelated work that landed on top | ✅ DONE |
| J.3 | `maf-onboarding.agent.md` | New-dev codebase tour. Topology + top-touched files + dialect — personalised, not generic | ✅ DONE |

### Phase K — Auto-loaded instruction files (post-1.0, 2026-05-12)

Two new always-loaded instructions targeting `*Tests.cs` and host-process files. Net new value — neither overlaps with existing instructions.

| Order | Item | Why | Status |
|---|---|---|---|
| K.1 | `maf-testing.instructions.md` (`applyTo: "**/*Tests.cs"`) | Hermetic-test patterns: ScriptedChatClient, fan-out shape verification, AAA conventions, temp-dir pattern | ✅ DONE |
| K.2 | `maf-deployment.instructions.md` (`applyTo: Program.cs/Startup.cs/Hosting/*.cs/Infrastructure/*.cs`) | Production: ManagedIdentityCredential, MaxTokens caps, OTel wiring, secret handling, session storage | ✅ DONE |

### Phase L — Pull-forward from 1.1 backlog (2026-05-12)

Items previously deferred to 1.1.0+ that were cheap and high-value enough to land in the same sprint.

| Order | Item | Why | Status |
|---|---|---|---|
| L.1 | POSIX-correct `SourceFileWalker.MakeRelative` (OS-conditional path comparison) | Linux/macOS users silently got full paths instead of repo-relative; trivial fix | ✅ DONE |
| L.2 | Rule-ID cross-reference column in `maf://rules` (MAF001 ↔ MafValidateFanOut, MAF002 ↔ MAF-AP-SEC-001, MAF003 ↔ MAF-AP-SEC-003) | Reviewers flagged the analyzer / scanner / registry ID systems as confusing; documenting the mapping is the right unification (renaming would break the ecosystem) | ✅ DONE |

### Phase N — In-depth security review + fixes (2026-05-12)

Three Opus security reviewers covered (A) input attack surface, (B) supply chain + CI/CD, (C) code generation + parser safety. Synthesis identified **1 false positive** (A.H1 — `EscapeForCSharp` Unicode escape, traced to be safe by character-replacement ordering) and **10 real findings**. All real findings fixed, 43 regression tests added.

| Order | Item | Severity | Status |
|---|---|---|---|
| N.1 | `MafNewExecutor`: `inputType` / `outputType` flowed unvalidated into raw C# source. Added Roslyn `ParseTypeName` + roundtrip validator. | HIGH (C.H1 — confirmed exploit) | ✅ DONE |
| N.2 | `Cs0618HuntTool.ExtractCamelCaseIdentifiers` regex had nested-quantifier overlap → catastrophic backtracking (measured: n=30 → 9 sec). Replaced with overlap-free pattern + `RegexOptions.NonBacktracking` + 100ms timeout. | HIGH (C.H2 — measured ReDoS) | ✅ DONE |
| N.3 | `PullRequestAuditTool.IsSafeBranchName` accepted `--output=PATH` → `git diff --output=` is an arbitrary-write primitive. Reject leading `-`. | MEDIUM (A.M2 — confirmed primitive) | ✅ DONE |
| N.4 | `Cs0618HuntTool.ResolveBuildTarget` accepted `C:\\` and `/` → drive-root scan for any `.sln` + `dotnet build` it. Reject filesystem roots up-front. | MEDIUM (A.M1 — sharp edge) | ✅ DONE |
| N.5 | Docker container ran as UID 0 (root). Added dedicated `maf` user (UID 1001) + `USER maf`. | HIGH (B.H4 — hardening) | ✅ DONE |
| N.6 | `InitCommand` had no size cap on `.vscode/mcp.json` → multi-GB file → OOM. 1 MB cap with graceful backup-and-continue. | MEDIUM (C.M2 — hardening) | ✅ DONE |
| N.7 | `RegistryService` had no size cap on `MAF_REGISTRY_PATH` override → multi-GB file → OOM. 5 MB cap with clear error. | MEDIUM (C.M1 — hardening) | ✅ DONE |
| N.8 | `release.yml` tested only the main project, skipping the analyzer test project. Also missed packing the analyzer NuGet. Switched to solution-level `dotnet test` + added analyzer `dotnet pack` step. | HIGH (B.H1 — confirmed gap) | ✅ DONE |
| N.9 | Three third-party Actions (`softprops/action-gh-release`, `peter-evans/create-pull-request`, `marocchino/sticky-pull-request-comment`) pinned to floating major tags. Pinned all three to commit SHAs with verify-against-tag comment. Added `.github/dependabot.yml` to maintain SHA pins going forward. | HIGH (B.H3 — supply-chain hardening) | ✅ DONE |
| N.10 | Documented the `EscapeForCSharp` false positive in the source (Agent A's H1 was traced to be a non-issue — character-replacement ordering doubles backslashes BEFORE quotes, making `"` inputs roundtrip as literal text). Added pin-tests so a future change to the escaper that re-opens the (non-existent) hole fails loudly. | INFO (false-positive guard) | ✅ DONE |
| N.11 | 43 security regression tests added: 20 type-expression accept/reject, 2 executor injection, 8 EscapeForCSharp roundtrip + Unicode-attempt, 1 ReDoS timing pin, 3 root-path rejection, 10 branch-name accept/reject. | n/a | ✅ DONE |
| N.12 | Considered but deferred: release-watcher trust-chain redesign (B.H2 — significant workflow rework, defer to dedicated PR); top-level `permissions: {}` defaults across all workflows (B.M1 — defense-in-depth); inline `${{ … }}` → `env:` pattern enforcement (B.M3 — defense-in-depth, no current `pull_request_target`); `IsValidVersion`/`IsValidPackageId` leading-`-` rejection (A.M3 — defense-in-depth, no current escape path); rule-ID unification (out of scope). | n/a | 🚫 INTENTIONALLY DEFERRED |

### Phase M — Naming alignment audit (2026-05-12)

Cross-cut review of every name in the project (agents, instructions, skills, MCP tools, MCP resources, workflows, internal classes, CLI subcommands).

| Order | Item | Why | Status |
|---|---|---|---|
| M.1 | Rename `maf-rollback-agent.agent.md` → `maf-rollback.agent.md` | The `.agent.md` extension already marks the file as an agent; `-agent` in the stem was redundant | ✅ DONE |
| M.2 | Codify the skill-naming convention in `CONTRIBUTING.md` | The `maf-` prefix is principled, not universal: prefix when the unprefixed name reads as a general-CS term; don't prefix when the topic is unambiguous in the `.github/skills/` namespace (avoids double-prefixing under `maf://skills?name=...`). Documenting this prevents future drift | ✅ DONE |
| M.3 | Codify the agent-naming convention in `CONTRIBUTING.md` | No "-agent" stem on agent filenames; prefer noun-roles when settled, activity nouns when uncommon | ✅ DONE |
| M.4 | Considered but rejected: mass-renaming 6 skill dirs to `maf-*` | 38-file blast radius, breaks any hardcoded `maf://skills?name=...` URI consumer, creates redundant `maf://skills?name=maf-*` double-prefixing. The current inconsistency is principled (see M.2). | 🚫 INTENTIONALLY DEFERRED |
| M.5 | Considered but rejected: renaming `MafNewAgent` / `MafNewExecutor` → `MafScaffoldAgent` / `MafScaffoldExecutor` | "New" is the universal dev-tool verb (`dotnet new`, `npm init`, `cargo new`). Renaming would diverge from the CLI subcommand `maf-doctor new agent` AND introduce a breaking MCP-surface change for marginal accuracy gain. | 🚫 INTENTIONALLY DEFERRED |

### Phase A — Pre-1.0 critical path (next, in order)

Everything that needs to ship before cutting stable `1.3.0`. Do these in this order.

| Order | Item | Why now | Effort | Status |
|---|---|---|---|---|
| A.1 | **#55** Rewire 3 agents to invoke the 9+2 MCP tools | Biggest easy win from the completeness audit. Agents predated the tools — manual procedure steps replaced with tool calls. | S (1 day) | ✅ DONE 2026-05-11 |
| A.2 | **#56** Expand registry + scanner to 14 un-audited guide sections | Largest coverage gap. Migration is 10 fingers deep; best-practice review is 4 fingers wide — fill the gap. | M (3–5 days) | 🔄 **first wave done** (3 of ~6 rules + 3 registry entries shipped 2026-05-12): `MAF-AP-AGENT-001` Instructions-outside-ChatOptions (silent ignore), `MAF-AP-WF-001` Executor must be sealed-partial, `MAF-AP-MID-001` Use() missing runStreamingFunc. Registry now has 13 entries (was 10). Remaining: tool-approval (§12), DevUI guard (§14), structured-output null-check (§11). |
| A.3 | **#57** Slim redundant SKILLs + merge fan-out duo | Procedure SKILLs now duplicate tools. Burns context every load. | S (½ day) | ✅ DONE 2026-05-12 |
| A.4 | **#58** SARIF output for scanners | Tiny LOC, unlocks GitHub Advanced Security adoption. | S (½ day) | ✅ DONE 2026-05-12 |
| A.5 | **#59** `MafDoctor` aggregator tool | One-command A/B/C/F health letter; wraps existing tools. Quickest demo gain. | S (½ day) | ✅ DONE 2026-05-12 |
| A.6 | **#46** Fix release-watcher bugs | Wrong GitHub repo, hard-coded version regex, missing idempotency, no prerelease filter, no NuGet retry. | S (½ day) | ✅ DONE 2026-05-12 |
| A.7 | **#34** Stop drift in csproj version (re-verify after A.4–A.6) | Bump csproj `<Version>` to `1.3.0-alpha-5` and push. | S (10 min) |
| A.8 | **#50** Cut stable `1.3.0` release | Final gate. Requires Phase A.1–A.6 green + at least one external migration validation. | S (workflow_dispatch) |

### Phase B — Post-1.0 polish

Round out coverage and quality once 1.0 ships.

| Order | Item | Note |
|---|---|---|
| B.1 | **#44** Smoke-tester template fix | Slimmed to point at `MafNewExecutor` (compile-validated) for fan-out shape; manual templates retained for the 4 patterns the tools don't cover yet. | ✅ DONE 2026-05-12 |
| B.2 | **#47** Per-version guide files | Done in A.6 — `gen_guide_section.py` now writes `guides/maf-<NEW>-migration-guide.md` with AUTO/HUMAN markers, idempotent on re-run. | ✅ DONE 2026-05-12 (via A.6) |
| B.3 | **#48** Major-version detection / escalation in watcher | Watcher sets `is_major` by comparing semver MAJOR. _(Superseded 2026-06-14: the PR-title/label escalation described here was removed with the switch to direct-push-to-main; majors now skip the auto-commit and open a `maf-release,needs-review` tracking issue instead — see docs/self-update-remediation-plan.md task 3.2.)_ | ✅ DONE 2026-05-12 |
| B.4 | **#49** Split `maf-auditor` into two agents | Cosmetic — `maf-best-practice-reviewer` already exists as the sibling. Defer the rename of `maf-auditor` → `maf-migration-planner` until after 1.0 (with #51). | ⏸ DEFERRED |
| B.5 | **#66** Reconsider `init` two-file install | `copilot-instructions.md` now a thin pointer to `maf://constraints` + a table mapping each constraint to the tool that verifies it. No more drift. Unused `StripFrontmatter` / `ReadEmbedded` helpers removed. | ✅ DONE 2026-05-12 |
| B.6 | **#61** `MafLintAgentPrompt` | New MCP tool. 4 rules: `PROMPT-001` (empty), `PROMPT-002` (token bloat), `PROMPT-003` (missing refusal pattern), `PROMPT-004` (untrusted-input concat / prompt injection). 21 tests. | ✅ DONE 2026-05-12 |
| B.7 | **#43** Expand registry to 9 missing API areas | Largely covered by A.2 (4 new scanner rules + 3 new registry entries spanning §4, §8, §14, §15). Remaining gaps (§7 memory, §12 tool approval, §11 structured output) move to Phase C. | ⏸ MOSTLY DONE (via A.2) |
| B.8 | **#51** Decide on rename (`maf-autopilot` → `maf-keeper`?) | Defer until 1.0 ships. Renaming pre-1.0 is much cheaper than post-1.0. | ⏸ DEFERRED |

### Phase C — Strategic additions

Vision-completing work; ship as 1.1.0 / 1.2.0.

| Order | Item | Note |
|---|---|---|
| C.1 | **#30** registry.yaml CI auto-update | Now tractable with v0.7.8 surfacing `[Obsolete]`. Closes the self-improvement loop. | ✅ DONE 2026-05-12 |
| C.2 | **#52** `pr-diff-auditor` skill / tool | Scope scanners to files changed in current PR. Required for CI integration. | ✅ DONE 2026-05-12 |
| C.3 | **#53** `drift-detector` scheduled scan | Daily scan of user's repo; flags when newly-merged code regresses. | ✅ DONE 2026-05-12 |
| C.4 | **#54** `pre-upgrade-dry-run` skill | "What would break if I upgraded?" without modifying code. Built on `MafDiffPackage`. | ✅ DONE 2026-05-12 |
| C.5 | **#62** `MafEstimateCost` static token-cost auditor | Production cost governance — enterprise differentiator. | ✅ DONE 2026-05-12 |
| C.6 | **#63** `MafScanPromptInjection` | Roslyn data-flow analysis for untrusted-input → `Instructions =`. (Partially covered by `MafLintAgentPrompt`'s PROMPT-004; full data-flow remains.) | ⏸ PARTIAL |
| C.7 | **#31** `maf_open_feedback_issue` MCP sampling tool | Closes the learning loop in MCP binary mode. |
| C.8 | **#26** Sampling tools — `maf_full_audit`, `maf_migration_suggest` | Multi-step agentic chains inside the MCP server. |
| C.9 | **#27** Remaining 6 analysis tools | `maf_executor_pattern_check`, `maf_compatibility`, `maf_migration_path`, etc. |

### Phase D — Distribution & long-tail

| Order | Item | Note |
|---|---|---|
| D.1 | **#28** Docker distribution via GHCR | `docker pull ghcr.io/joslat/maf-doctor:latest`. Multi-arch amd64+arm64; semver-keyed tags; stable-only `latest`. | ✅ DONE 2026-05-12 |
| D.2 | **#32** Multi-version migration paths | `MafMigrationPath(currentVer, targetVer)` walks version-keyed guide metadata, returns ordered intermediate-step sections. Built on #20 metadata. | ✅ DONE 2026-05-12 |
| D.3 | **#45** Merge `fan-out-validator` + `fan-in-static-analyzer` | Cosmetic — done as part of A.3 skill slimming (`fan-in-static-analyzer` is now a redirect stub). | ✅ DONE 2026-05-12 (via A.3) |
| D.4 | GitHub App `@maf-bot` | PR comments scoped to changed lines; pairs with #52. Org-wide adoption vector. Not in current tracking table — file an issue. |
| D.5 | VS Code extension | One-click installer wrapping the MCP server, surfaces `MafDoctor` as a status-bar item. Not in tracking table. |

### Snapshot — completion by phase (synced 2026-05-12 late evening, post-Phase-M)

| Phase | Items | Done | Deferred | Gated | Remaining |
|---|---:|---:|---:|---:|---:|
| Phase 0 (Foundation) | 38 | 38 | 0 | 0 | 0 |
| Phase A (Pre-1.0) | 8 | 7 | 0 | 1 (A.8 stable release) | 0 |
| Phase B (Post-1.0 polish) | 8 | 7 | 1 (B.8 rename) | 0 | 0 |
| Phase C (Strategic) | 9 | 5 | 1 (C.6 — partial via PROMPT-004) | 0 | 3 |
| Phase D (Distribution) | 5 | 3 | 0 | 0 | 2 |
| Phase E (Post-Opus polish) | 6 | 6 | 0 | 0 | 0 |
| Phase F (Post-final-review) | 5 | 5 | 0 | 0 | 0 |
| Phase G (Verdict-driven gap closure) | 4 | 4 | 0 | 0 | 0 |
| Phase H (Third-Opus blockers) | 4 | 4 | 0 | 0 | 0 |
| Phase I (Test-coverage deepening) | 5 | 5 | 0 | 0 | 0 |
| Phase J (Production-lifecycle agents) | 3 | 3 | 0 | 0 | 0 |
| Phase K (Auto-loaded instructions) | 2 | 2 | 0 | 0 | 0 |
| Phase L (Pull-forward from 1.1 backlog) | 2 | 2 | 0 | 0 | 0 |
| Phase M (Naming alignment audit) | 5 | 3 | 2 (intentional) | 0 | 0 |
| Phase N (Security review + fixes) | 12 | 11 | 1 (intentional) | 0 | 0 |
| **Total** | **116**\* | **105** | **5** | **1** | **5** |

\* *107 not 105 because D.4 and D.5 are not yet in the numbered tracking table; file them as new rows when scheduled.*

**Phase A is done in code** — only A.8 (cut stable `1.3.0`) remains, and that's a one-line `gh workflow run release.yml -F version=1.3.0` once an external migration validates the toolkit. **Phase B is done in practice** — the two deferred items are cosmetic renames best held until after 1.0. **Phase C is now the next chord** — registry CI auto-update (#30), pr-diff-auditor (#52), drift-detector (#53), pre-upgrade dry-run (#54), cost auditor (#62), prompt-injection scanner (#63), feedback-issue sampling tool (#31), sampling tools (#26), remaining analysis tools (#27).

---

## Implementation Tracking Table

> **Audit date:** May 2026 (updated) — `maf-autopilot` MCP server renamed + SDK upgraded to 1.2.0 (sampling confirmed); Phase Review Protocol added.  
> **Legend:** Priority P1=Critical, P2=High, P3=Nice-to-have | Status: ✅ DONE / ⚠️ NEEDS WORK / 🔄 IN PROGRESS / ❌ NOT STARTED

| # | Name | Description | Priority | Status | % Done | % Reviewed | Notes / Issues |
|---|------|-------------|----------|--------|-------:|----------:|----------------|
| 1 | `maf-migration.agent.md` | Main migration orchestrator — full task loop, skill selector, phase gates | P1 | ✅ DONE | 100% | 95% | Complete. Full 5-phase workflow with build-verified task loop. |
| 2 | `maf-auditor.agent.md` | Pre-migration plan generator — scans codebase, runs diff, produces migration-plan.md | P1 | ✅ DONE | 100% | 90% | Complete. All 7 audit phases implemented. |
| 3 | `maf-constraints.instructions.md` | Always-loaded breaking changes table + hard constraints | P1 | ✅ DONE | 100% | 95% | Auto-loads in every conversation. Up to date for 1.3.0. |
| 4 | `cs0618-hunter` skill | Compiler-based CS0618 detection and fix workflow | P1 | ✅ DONE | 100% | 90% | Complete 5-step workflow. **Missing:** direct link to [dotnet-inspect #316](https://github.com/richlander/dotnet-inspect/issues/316) in SKILL.md — fix applied in this session. |
| 5 | `dotnet-inspect` skill | NuGet API surface inspection with ⚠️ [Obsolete] limitation warning | P1 | ✅ DONE | 100% | 95% | Has issue #316 link. Decision tree, key commands, limitation warning all present. |
| 6 | `obsolete-api-registry` skill + `registry.yaml` | Machine-readable CS0618 registry — 10 entries covering all known 1.3.0 obsolete patterns | P1 | ✅ DONE | 100% | 90% | 10 entries: FAN-IN-001, SESSION-001, THREAD-001, EXEC-001, A2A-001, A2A-002, STREAM-001, EVENT-001, ATTR-001, ATTR-002. Plan was out of date claiming only 1 entry. |
| 7 | `maf-migration-guide` skill | Smart section navigator for the 21-section guide | P1 | ✅ DONE | 100% | 90% | Full section index, quick lookup by symptom, line range guidance. |
| 8 | `migration-plan-creator` skill + `template.md` | Generates complete, ready-to-execute migration plans | P1 | ✅ DONE | 100% | 90% | Skill + template both present. Phase/task ID conventions documented. |
| 9 | `fan-out-validator` skill | Validates fan-out/fan-in executor topology — detects silent starvation | P1 | ✅ DONE | 100% | 85% | Full 5-step workflow, fix patterns, both overload forms covered. |
| 10 | `fan-in-static-analyzer` skill | Static code analysis — finds broken fan-out handlers without running code | P2 | ✅ DONE | 100% | 85% | Detailed step-by-step procedure, all 6 analysis steps. |
| 11 | `nuget-diff-analyzer` skill | Structured diff post-processing skill with risk categorization | P1 | ✅ DONE | 100% | 85% | Categories, risk table, registry cross-reference, output template all present. |
| 12 | `maf-release-watcher` skill | Detects new MAF releases, updates guide + registry, creates PR | P1 | ✅ DONE | 100% | 80% | Full workflow (Steps 1-7). PowerShell + bash variants. |
| 13 | `workflow-smoke-tester` skill | Generates post-migration structural smoke test stubs | P2 | ✅ DONE | 100% | 80% | 5 patterns covered: fan-out/in, session round-trip, streaming, structured output, tool invocation. |
| 14 | `migration-retrospective` skill | Post-migration learning loop — improves registry, guide, constraints | P2 | ✅ DONE | 100% | 80% | Full workflow Steps 1-7. Surprise categorization table. |
| 15 | `maf-release-watcher.yml` GitHub Actions | Weekly NuGet check → auto-diff → PR creation | P1 | ✅ DONE | 95% | 85% | Real YAML workflow with 3 jobs. Inline Python scripts (plan mentioned separate script files — inline is better). PR checklist is thorough. Missing: `registry.yaml` auto-update step still manual. |
| 16 | `guides/maf-1.3.0-migration-guide.md` | Full 21-section reference guide (2000+ lines) | P1 | ✅ DONE | 100% | 90% | Present and comprehensive. **Updated this session:** version-keyed metadata comments applied to all 25 sections. Section numbering slightly inconsistent (17.5, 17.6, etc.) — acceptable, documented in ToC. |
| 17 | `docs/compatibility-matrix.md` | MAF version ↔ dependency version table, auto-updated | P1 | ✅ DONE | 100% | 85% | Present. 4 MAF versions documented (1.0.0–1.3.0). Includes removed packages table, NuGet IDs. |
| 18 | `.maf-version` | Tracked version file for release watcher | P1 | ✅ DONE | 100% | 100% | Contains `1.3.0`. Simple, correct. |
| 19 | `README.md` | Project overview, structure, skill routing table, usage instructions | P1 | ✅ DONE | 100% | 85% | **Fixed this session:** Removed duplicate skill routing table rows + duplicate `> Never use...` callout. Clean now. |
| 20 | Version-keyed guide sections | `<!-- introduced: 1.3.0 \| applies-to: ... -->` metadata in guide headings | P2 | ✅ DONE | 100% | 85% | **Implemented in this session.** All 25 sections (1–21 + 17.5–17.8) tagged. `maf-migration-guide` SKILL.md updated with usage docs. Guide header updated. |
| 21 | `maf-autopilot` MCP server (MVP) | C# MCP server with `maf_api_safety` + `maf_registry_lookup` + `maf_registry_list` tools | P2 | ✅ DONE | 100% | 85% | **Done — May 2026.** Renamed from `maf-inspect` → `maf-autopilot` (matches repo, signals full scope). SDK upgraded `0.6.*` → `1.2.0` (stable). `dotnet build` passes clean. Sampling confirmed: `server.SampleAsync()` available in 1.2.0. `.vscode/mcp.json` updated. |
| 22 | `maf-autopilot` MCP Resources | All 11 skill docs + guide sections + registry exposed as `maf://skills/*`, `maf://guide/section/*`, `maf://registry` | P2 | ✅ DONE | 100% | 90% | **Done — May 5, 2026.** `Resources/MafResources.cs` (`[McpServerResourceType]`). Static: `maf://constraints`, `maf://registry`, `maf://guide`. Template: `maf://skills{?name}` (11 skills). All files embedded via `LogicalName` in csproj. `WithResourcesFromAssembly()` in `Program.cs`. Build clean. |
| 23 | `maf-autopilot` MCP Prompts | `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt` — slash-command workflows with embedded skill context | P2 | ✅ DONE | 100% | 90% | **Done — May 5, 2026.** `Prompts/MafPrompts.cs` (`[McpServerPromptType]`). Three prompts: `maf-audit` (repoPath, fromVersion), `maf-migrate` (taskIds, planPath), `maf-cs0618-hunt` (projectPath). All reference `maf://` resource URIs inline. `WithPromptsFromAssembly()` in `Program.cs`. Build clean. |
| 24 | `maf-doctor init` command | CLI scaffold: `maf-doctor init` writes `.vscode/mcp.json` + generates minimal `.github/copilot-instructions.md` | P2 | ✅ DONE | 100% | 90% | **Done — May 5, 2026.** `InitCommand.cs` intercepts `init` arg before MCP Host starts. Merges `.vscode/mcp.json` (global-tool entry, idempotent). Writes `.github/copilot-instructions.md` (MAF hard constraints, frontmatter-stripped) only if not exists. Smoke-tested: skip logic confirmed. Build clean. |
| 25 | NuGet publish (`dotnet tool install -g maf-doctor`) | Publish to NuGet.org as a global tool. Enables `dotnet tool install -g maf-doctor` | P1 | 🔄 IN PROGRESS | 60% | 50% | **Updated 2026-05-11.** 3 alphas live on nuget.org (`1.3.0-alpha-1/2/3`). Pipeline works (release.yml proven). Remaining: (a) plain `dotnet tool install --global maf-doctor` fails because all versions are prereleases — needs `--prerelease` flag OR a stable cut; (b) csproj `<Version>` is `1.3.0-tool.1` — drifted from reality (release.yml overrides via workflow input); (c) no stable 1.3.0 yet. Track stable cut as separate row #50. |
| 26 | `maf-autopilot` Sampling tools | Agentic tools using `server.SampleAsync()`: `maf_full_audit` (analyze→plan→generate→validate), `maf_migration_suggest` | P3 | ❌ NOT STARTED | 0% | 0% | Unlocked by SDK 1.2.0. Server calls host LLM (VS Code Copilot) with zero API keys. Multi-step reasoning chains entirely inside the MCP server. |
| 27 | `maf-autopilot` full analysis tools | Remaining 6 tools: `maf_detect_cs0618`, `maf_api_diff`, `maf_fan_out_validate`, `maf_executor_pattern_check`, `maf_compatibility`, `maf_migration_path` | P3 | ❌ NOT STARTED | 0% | 0% | Higher-effort tools requiring dotnet build invocation, Roslyn analysis, dotnet-inspect subprocess. See Section 10 tool specs. |
| 28 | Docker distribution via GHCR | `docker pull ghcr.io/joslat/maf-doctor:latest` | P3 | ❌ NOT STARTED | 0% | 0% | Depends on #25. Dockerfile + GHCR push in workflow. |
| 29 | Roslyn analyzer companion | Flags void fan-out handlers + insecure credentials + sensitive-data logging at write-time in the IDE | P1 | ✅ DONE | 100% | 85% | **Done 2026-05-11 (magic-path sprint).** New `src/maf-autopilot.Analyzers/` project targeting `netstandard2.0` (Roslyn requirement). Three rules: `MAF001` fan-out handler must return `Task<T>`/`ValueTask<T>` (Error), `MAF002` avoid `DefaultAzureCredential` outside tests (Warning), `MAF003` `EnableSensitiveData = true` outside tests (Warning). Each shipped with help-link URIs to the SKILL.md files. Analyzer release tracking files in place. 11 tests using `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit`. Will pack as `maf-autopilot.Analyzers` NuGet — separate package from the global tool. |
| 30 | `registry.yaml` auto-update in CI | Semantic step (Copilot Coding Agent or LLM API call) to auto-generate new registry entries from dotnet-inspect diff output — closes the last manual step in `maf-release-watcher.yml` | P2 | ❌ NOT STARTED | 0% | 0% | Pure scripts can't write meaningful YAML entries without semantic analysis. Target: hybrid Copilot Agent step in workflow (see Section 11). No external prerequisite — this is a CI-internal improvement to the release watcher itself. |
| 31 | `maf_open_feedback_issue` Sampling tool | After migration, uses `server.SampleAsync()` to analyze the migration log, then opens a structured GitHub Issue on `joslat/maf-doctor` with new CS0618 patterns and guide corrections | P2 | ❌ NOT STARTED | 0% | 0% | Closes the reflexive learning loop in MCP binary mode. Requires GitHub MCP access (already in VS Code Copilot via `mcp_github_*`). See Section 9.2. Prerequisite: #25 NuGet publish. |
| 32 | Multi-version migration paths | Support 1.1.0 → 1.3.0 in one pass (not step-by-step through intermediate versions) | P3 | ❌ NOT STARTED | 0% | 0% | Depends on version-keyed guide sections (#20 ✅). Migration agent stacks multiple version paths by loading sections tagged for each intermediate version. |
| 33 | **Bump `dotnet-inspect` 0.7.6 → 0.7.8 + fix attribution** | Pin update across 21 places. Author correction `filipw` → `richlander`. Rewrite "[Obsolete] caveat" paragraphs in README, `dotnet-inspect/SKILL.md`, `copilot-instructions.md`, `maf-constraints.instructions.md`, migration guide. | P1 | ✅ DONE | 100% | 85% | **Done 2026-05-11.** All 21 pinned references bumped. Attribution corrected. `cs0618-hunter` framing rewritten: dotnet-inspect@0.7.8 is the static / pre-build path; compiler stays authoritative for transitive / overload-resolution / project-local cases. Reviewed by Opus; minor housekeeping (cs0618 §"Why Not Use" + guide frontmatter) swept in follow-up commit. |
| 34 | Fix README install command + csproj version drift | README + setup.md install snippets must use `--prerelease` (or be pinned to `--version 1.3.0-alpha-3`) until stable ships. `maf-autopilot.csproj:15` `<Version>` bumped to next prerelease so csproj stops lying. | P1 | ✅ DONE | 100% | 85% | **Done 2026-05-11.** README + setup.md install snippets show `--prerelease` and link nuget.org/packages/maf-doctor. csproj `<Version>` bumped `1.3.0-tool.1` → `1.3.0-alpha-4`. Build clean. |
| 35 | MCP server test harness | `src/maf-autopilot.Tests/` xUnit project. Unit tests: `RegistryService` (load, `FindById` casing, every field `SearchByApiName` must touch, malformed YAML), `MafResources` (allowlist + path-traversal), `InitCommand` (idempotency, malformed `.vscode/mcp.json` must NOT clobber). Integration test: in-process stdio MCP transport — list tools/resources/prompts; invoke `MafApiSafety("AgentThread")` → assert UNSAFE. Goldens for embedded resource bodies. Wire `dotnet test` into `release.yml` before pack step. | P1 | ✅ DONE | 95% | 85% | **Done 2026-05-11.** 76 tests passing across RegistryService (28), MafResources (22+), ApiSafetyTool (5), RegistryLookupTool (8), InitCommand (6), FormatEntry (4). Wired into `release.yml` before pack. The harness caught the `MafResources` case-sensitivity bug (fixed in same batch) and the JSONC-comment silent-quarantine bug (fixed in same batch). **Still deferred:** in-process stdio integration test, file-body goldens — track in a follow-up issue. |
| 36 | Fix `init` JSON-clobber bug | `InitCommand.cs:64-69` — on malformed `.vscode/mcp.json` parse failure, current code wipes the file and writes a fresh `JsonObject`. Must back up to `.vscode/mcp.json.bak`, fail loudly with a clear message, and offer a `--force` flag for the rare case the user explicitly wants overwrite. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-11 (bundled into P0 housekeeping).** Malformed JSON triggers timestamped backup `mcp.json.bak.<UTC>` before recreate; warning printed to console; 4 InitCommand tests cover happy path / idempotency / preservation / backup. `--force` flag deferred (no current need since backup preserves data). |
| 37 | Expand `SearchByApiName` to all string fields + inverted index | Extend the OR-chain at `RegistryService.cs:47-56` to include `ExampleAfter`, `Package`, `Id`. Better: build a `Dictionary<string,HashSet<RegistryEntry>>` token index at `RegistryService` construction time so search becomes O(1) tokenised lookup. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-11.** Pre-built lowercase haystack per entry covering 12 fields (Id, Package, Type, Method, ObsoleteSignature, ReplacementSignature, FixDescription, ExampleBefore, ExampleAfter, CsWarning, GuideSection, Notes). Search is now O(entries × \|haystack\|) substring with a single source-of-truth `BuildHaystack` method — adding a new field is a one-line change. 11 new tests guard ExampleAfter / Package / Id / CsWarning coverage. The "missed field" bug class is structurally eliminated. |
| 38 | Reconcile README roadmap with plan tracking table | Either delete the README roadmap or autogenerate it from this table. Today the README under-reports open work (lists 4 pending; plan tracks 12+). | P1 | ✅ DONE | 100% | 85% | **Done 2026-05-11.** README roadmap replaced with a "Roadmap & Status" pointer to `maf-migration-toolkit-plan.md` (this file) and `project-status-and-vision.md`. Single source of truth restored. |
| 39 | Standardise MCP tool naming convention | `ApiSafetyTool.cs:84` and `MafPrompts.cs:39-40` reference snake_case (`maf_api_safety`, `maf_registry_lookup`); the SDK emits PascalCase (`MafApiSafety`, `MafRegistryLookup`). Pick one and align all references. | P2 | ✅ DONE | 100% | 80% | **Done 2026-05-11.** All references aligned to PascalCase (`MafApiSafety`, `MafRegistryLookup`, `MafRegistryList`) — what the SDK emits and what the model sees. Updated in source (Tools, Prompts), tests, README, setup.md. |
| 40 | `CONTRIBUTING.md` + `TROUBLESHOOTING.md` | Add both. CONTRIBUTING covers: how to add a skill, how to add a registry entry, PR conventions, "always check upstream release notes before designing workarounds". TROUBLESHOOTING covers: init fails, stdio handshake, NuGet rate-limit, MCP server doesn't appear in Copilot. | P2 | ✅ DONE | 100% | 80% | **Done 2026-05-11.** `CONTRIBUTING.md` (4 contribution shapes + PR conventions + lessons learned) and `TROUBLESHOOTING.md` (install + init + MCP visibility + dotnet-inspect + migration loop + release watcher + CI sections) added at repo root. |
| 41 | Wire 3 executable MCP tools | `MafRunCs0618Hunt(projectPath)` — shells `dotnet build`, parses CS0618/CS0246, joins to registry. `MafValidateFanOut(filePath\|repoPath)` — Roslyn-based static check for `void` handlers. `MafDiffPackage(pkg, oldVer, newVer)` — wraps `dnx dotnet-inspect@0.7.8 -- diff`, consumes new `[Obsolete]` annotations, returns categorised report. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-11.** Three new tool classes in `src/maf-autopilot/Tools/`: `FanOutValidatorTool` (pure Roslyn syntax walk, no semantic model), `Cs0618HuntTool` (shells `dotnet build`, regex-parses CS0618/CS0246, registry-joined), `DiffPackageTool` (shells `dnx dotnet-inspect@0.7.8 -- diff`, parses `+`/`-`/`~` markers, surfaces newly-obsoleted members). Each tool exposes a pure-function parser core for unit testing; the shell-out is the only non-unit-tested surface. Added `Microsoft.CodeAnalysis.CSharp 4.11.0` package dependency. **40 new tests** (FanOutValidator 13, Cs0618Hunt 11, DiffPackage 9, plus theories). MCP server now exposes **6 executable tools** (was 3). |
| 42 | `maf-best-practice-reviewer` agent + `maf-anti-pattern-scanner` skill + `MafScanAntiPatterns` MCP tool | New agent that audits clean 1.3.0 codebases for drift / anti-patterns. New skill provides scan rules. New executable MCP tool runs the rules. Output is `audit-report.md`, NOT `migration-plan.md`. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-11 — the identity unlock landed.** `.github/agents/maf-best-practice-reviewer.agent.md` orchestrates a 6-phase audit (sanity → automated scan → topology cross-check → manual review → report → handback). `.github/skills/maf-anti-pattern-scanner/SKILL.md` defines 8 rules (`MAF-AP-SEC-001`/002/003, `MAF-AP-CONC-001`/002, `MAF-AP-OBS-001`/002, `MAF-AP-ID-001`). `AntiPatternScannerTool.cs` implements 6 of them as executable regex/Roslyn rules. Skill is embedded in MCP binary and reachable via `maf://skills?name=maf-anti-pattern-scanner`. **Also bundled in this batch**: `DiffPackageTool` parser rewritten against the REAL `dotnet-inspect@0.7.8 -- diff` output format (was completely wrong before — line markers vs markdown sections). `ProcessRunner` extracted with 5-min timeout, async dual-stream drain (prevents pipe deadlock), `ArgumentList` (prevents command injection), 16 MB output cap. `Cs0618HuntTool.MatchToRegistry` now uses only the FIRST quoted segment from CS0618/CS0246 messages (no longer matches the replacement-instruction phrase). `FanOutValidatorTool` split `UnusualReturnType` → `LikelyInvalid` (raw types now flagged, not buried). **151 tests passing.** |
| 43 | Expand registry to 9 missing API areas | Today's `registry.yaml` covers ~10 patterns in 4 areas. Missing entries for: memory / context providers (guide §7), middleware (§8), tool approval (§12), observability `UseOpenTelemetry` (§13), DevUI/Hosting (§14), structured output `RunAsync<T>` (§11), source generator setup (§15), instructions placement (§4), agent creation `AsAIAgent()` (§3). Add `severity` (error\|warning\|info) and `category` (migration\|best-practice\|security\|observability) fields. | P2 | ❌ NOT STARTED | 0% | 0% | Half the guide's surface area is registry-invisible today. Pairs with #42 — anti-pattern scanner consumes the new entries. |
| 44 | Compile-validate `workflow-smoke-tester` templates | Templates reference `IChatClient`, `MockChatClient`, `DeserializeSessionAsync` — none have been compile-checked against MAF 1.3.0. Pin a reference sample project in this repo and CI-validate every template compiles against it. | P2 | ❌ NOT STARTED | 0% | 0% | Skill may ship dead code today. Pairs with the broader testing investment in #35. |
| 45 | Merge `fan-out-validator` + `fan-in-static-analyzer` | ~70% content overlap (silent-failure recap, return-type table, fix pattern). Combine into one skill with a "policy" section and a "procedure" section. Reduces context bloat and risk of divergence. | P3 | ❌ NOT STARTED | 0% | 0% | Cosmetic but discovered in the audit. |
| 46 | Fix release-watcher bugs | (a) Wrong GitHub repo. (b) Hard-coded `1.3.0` regex anchor. (c) No prerelease filter. (d) No NuGet retry. (e) No idempotency on guide append. (f) `maf-1.3.0-migration-guide.md` filename hardcoded. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase A.6).** (a) Repo corrected to `microsoft/agent-framework`; tag form `dotnet-X.Y.Z` tried first per canonical release links. (b) `update_compat_matrix.py` now finds the FIRST version row dynamically — no `1.3.0` anchor. (c) Prerelease filter regex (`-alpha\|-beta\|-rc\|-preview`) drops prereleases unless manual override. (d) NuGet curl retries 3× with backoff. (e) `gen_guide_section.py` writes per-version files with AUTO/HUMAN markers — human additions preserved across re-runs. (f) Per-version file `guides/maf-<NEW>-migration-guide.md`, not appending to 1.3.0. |
| 47 | Per-version guide files | `gen_guide_section.py` creates `guides/maf-$VERSION-migration-guide.md` rather than appending. Update `maf-migration-guide` skill to version-route lookups. | P2 | ❌ NOT STARTED | 0% | 0% | Pairs with #46. Foundation for #32 multi-version paths. |
| 48 | Major-version detection / escalation in watcher | Today the watcher treats `1.3.0 → 2.0.0` identically to `1.3.0 → 1.3.1`. Add: detect major-version bumps, escalate PR title/body, run extra analysis, require maintainer review. | P3 | ❌ NOT STARTED | 0% | 0% | Risk control. |
| 49 | Split `maf-auditor` into two agents | Rename current `maf-auditor` → `maf-migration-planner` (pre-migration scope). New `maf-best-practice-reviewer` (from #42) is the second agent. Materialises the "co-pilot, not just migration tool" identity in code. | P2 | ❌ NOT STARTED | 0% | 0% | Pairs with rename decision (#51). |
| 50 | Cut stable `1.3.0` (non-prerelease) NuGet release | Trigger `release.yml` via `workflow_dispatch` with `version=1.3.0` (or push `v1.3.0` git tag → also creates GitHub Release). Pre-conditions: P0 + P1 batches green, at least one external migration completed. | P1 | ❌ NOT STARTED | 0% | 0% | Final gate for "beta" status. Today only prereleases exist. |
| 51 | Decide on rename — `maf-autopilot` → `maf-keeper` / `maf-copilot` / `maf-steward`? | Project has outgrown the "autopilot" framing. Candidates: `maf-keeper` (recommended — maintenance scope), `maf-copilot` (Microsoft brand collision risk), `maf-steward` (less punchy). If renaming: package ID + repo + tool command. Provide a deprecated `maf-autopilot` shim package for one minor. **Decide before stable 1.3.0** — renaming after stable is much costlier. | P2 | ❌ NOT STARTED | 0% | 0% | Surfaced in 2026-05-11 project review (§5 of project-status-and-vision.md). |
| 52 | `pr-diff-auditor` skill | Audit only files changed in current PR/branch (not the whole repo). Unlocks CI integration: every PR opened against a MAF project gets an audit comment. | P3 | ❌ NOT STARTED | 0% | 0% | Strategic — required for CI integration and the future GitHub App vector. |
| 53 | `drift-detector` skill | Scheduled scan of user's repo flagging when newly-merged code regresses from MAF best practices. Counterpart to release-watcher but pointed inward. | P3 | ❌ NOT STARTED | 0% | 0% | Strategic — daily-use vector that turns the toolkit into a steady-state co-pilot. |
| 54 | `pre-upgrade-dry-run` skill | Given current MAF version and proposed target: simulate what would break before modifying any code. Pairs with the auto-PR from the watcher so users can preview. | P3 | ❌ NOT STARTED | 0% | 0% | Built on top of #41 `MafDiffPackage` and v0.7.8's new `[Obsolete]` surfacing. |
| 55 | **Rewire 3 agents to invoke the 9+2 MCP tools** | Update `maf-migration.agent.md`, `maf-auditor.agent.md`, and `maf-best-practice-reviewer.agent.md` to call MCP tools instead of "load skill / read_file / run by hand". Also update `maf-constraints.instructions.md` + `copilot-instructions.md` so the always-loaded rules cross-link to the tools that verify them. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-11 night (Phase A.1).** Migration agent: added MCP Tool Shortcuts table at top; Phase 0 now calls `MafDiffPackage` + `MafApiSafety`; Phase 3 now uses `MafRunCs0618Hunt` / `MafValidateFanOut` / `MafSimulateWorkflow` / `MafScanAntiPatterns` instead of manual loops; phase completion gate updated. Auditor agent: added MCP Tools table; Phase B/D/E/E.5/E.6 all use tools. Best-practice reviewer: Phase A.1 now uses `MafRunCs0618Hunt` (was raw `Select-String`); Phase C adds `MafSimulateWorkflow`; Phase D adds `MafExplain` for suspicious snippets. Constraints + copilot-instructions: added "How to verify each constraint" table mapping every hard rule to the tool that enforces it. Build clean. |
| 56 | **Expand registry + scanner to cover 14 un-audited guide sections** | Today only 3 of 21 guide sections have anti-pattern rules; 7 have registry entries. Add rules/entries for: memory/context providers (§7), middleware `.AsBuilder().Use().Build()` (§8), tool approval `ApprovalRequiredAIFunction` (§12), observability beyond `UseOpenTelemetry` presence (§13), DevUI `#if DEVUI_ENABLED` guard (§14), structured output `RunAsync<T>` null/exception checks (§11), source-generator `EmitCompilerGeneratedFiles` (§15), Instructions-placement-inside-`ChatOptions` (§4), agent-creation `.AsAIAgent()` patterns (§3). Each new rule: registry entry + scanner rule + test. | P1 | ❌ NOT STARTED | 0% | 0% | **The largest feature-completeness gap.** Without this the toolkit is "a migration tool with extras"; with it, the toolkit becomes the canonical MAF lint. Prerequisite for #29 Roslyn analyzer and SARIF output (#58). |
| 57 | Slim redundant procedure SKILLs to pointers + merge fan-out duo | `cs0618-hunter`, `nuget-diff-analyzer` → 10-line stubs pointing at `MafRunCs0618Hunt` / `MafDiffPackage`. `fan-in-static-analyzer` + `fan-out-validator` → merge into one "fan-out" skill with "policy" + "procedure" sections (#45). Each procedure skill currently burns ~150 lines of agent context every load. | P2 | ❌ NOT STARTED | 0% | 0% | Surfaced by the 2026-05-11 audit. Procedure prose duplicates tool implementations. |
| 58 | SARIF v2.1.0 output for `MafScanAntiPatterns` + `MafValidateFanOut` | Add `--format sarif` to each scanner tool. Unlocks GitHub code-scanning / Advanced Security ingestion + VS Code Problems panel. | P2 | ❌ NOT STARTED | 0% | 0% | Small LOC, large integration win. Required for CI gating. Depends on #56 (rules to enforce). |
| 59 | `MafDoctor` aggregator tool | Single-command repo health letter: A/B/C/F score + top 3 fixes + link to detail. Wraps `MafRunCs0618Hunt` + `MafValidateFanOut` + `MafScanAntiPatterns`. | P2 | ❌ NOT STARTED | 0% | 0% | Quickest demo gain in the audit ideas. Low effort, high "first impression" value. |
| 60 | `maf-new` agent / executor scaffolder | `MafNewAgent(projectPath, agentName, instructions?)` and `MafNewExecutor(projectPath, executorName, inputType?, outputType?)` MCP tools + CLI subcommand `maf-doctor new agent|executor <Name>`. Generates clean MAF 1.3.0 code from templates: ChatClientAgent + `UseOpenTelemetry` + `MaxOutputTokens` + `ManagedIdentityCredential` guidance + hermetic xUnit smoke test (ScriptedChatClient mock). For executors: `sealed partial class : Executor` + `[MessageHandler]` returning `Task<TOutput>` (fan-out-safe). | P1 | ✅ DONE | 100% | 85% | **Done 2026-05-11 (magic-path sprint).** Closes the greenfield pillar. Code in `src/maf-autopilot/Scaffolding/AgentScaffolder.cs` (templates) + `src/maf-autopilot/Tools/NewAgentTool.cs` (MCP) + `src/maf-autopilot/NewCommand.cs` (CLI). 19 tests including the **dogfood test**: generated agent code MUST pass `MafScanAntiPatterns`; generated executor MUST pass `MafValidateFanOut`. If the generator regresses, the scanner catches it. |
| 61 | `MafLintAgentPrompt(file)` tool | Inspect agent `Instructions` strings for: length (warn > N tokens), conflicting directives, untrusted-input concatenation without templating, missing refusal patterns, structured-output schema mismatches. | P2 | ❌ NOT STARTED | 0% | 0% | Nothing else in the .NET ecosystem does this. Agent quality is mostly prompt quality. |
| 62 | `MafEstimateCost(repoPath)` tool | Static token-cost auditor. For each `RunAsync` site: estimate input/output tokens, flag missing `MaxTokens`, flag no caching, flag no streaming. Production cost governance. | P3 | ❌ NOT STARTED | 0% | 0% | Enterprise-grade feature; unsexy but locks-in adoption. |
| 63 | `MafScanPromptInjection(repoPath)` tool | Roslyn data-flow analysis: untrusted input (controller params, HTTP body) flowing into `Instructions =` / `SystemPrompt =` without a templating function in between. | P3 | ❌ NOT STARTED | 0% | 0% | #1 LLM-app security failure class. Currently a "manual check" in the best-practice reviewer agent. |
| 64 | `MafExplain(snippet)` tool | Paste any MAF code snippet, get an annotated explanation linking to canonical guide sections. **Built as deterministic static analysis (not sampling-dependent) — works against any MCP host.** | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-11 (magic-path sprint).** `ExplainTool` walks the snippet syntax tree, identifies MAF API touchpoints (type refs, invocations, attributes), cross-references the obsolete-API registry, emits a per-line table with registry/guide citations + canonical notes per identifier. 8 tests. Onboarding gold; works offline; deterministic. |
| 65 | `MafSimulateWorkflow(repoPath)` — multi-agent simulation harness | Drive a workflow with synthetic inputs via mock `IChatClient`; assert topology completion; capture fan-in starvation at simulation time WITHOUT running the LLM. The closest thing to magic. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-11 (magic-path sprint).** `SimulateWorkflowTool` walks `WorkflowBuilder.AddEdge` / `AddFanOutEdge` / `AddFanInBarrierEdge` invocations via Roslyn; builds a directed executor graph; cross-checks every `[MessageHandler]` return type; emits a Mermaid diagram + per-edge verdict + overall completion forecast. 7 tests covering executor discovery, edge collection, void-source fan-in detection, unresolved executors. |
| 66 | Reconsider `init` two-file install | Currently writes `.github/copilot-instructions.md` as a copy of `maf-constraints.instructions.md`. The copy drifts the moment this repo updates. Option A: rely solely on `maf://constraints` resource for always-loaded rules. Option B: keep but auto-refresh on every `init` re-run. | P3 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase B.5).** `init` now writes a thin pointer table that maps every constraint to the verifying tool, no embedded constraint copy. `StripFrontmatter` + `ReadEmbedded` helpers removed (dead code). |
| 67 | `PathGuard` shared input validation (Phase E.1) | Path-traversal hole flagged by both architecture + MCP-surface reviews. Only `MafNewAgent`/`MafNewExecutor` had the `..` check; all other `repoPath`/`projectPath` tools were exposed. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase E.1, post-Opus review).** New `Tools/PathGuard.ValidateRepoPath` applied to 8 tools: `MafScanAntiPatterns`, `MafValidateFanOut`, `MafSimulateWorkflow`, `MafDoctor`, `MafEstimateCost`, `MafLintAgentPrompt`, `MafAuditPullRequest`, `MafPreUpgradeDryRun`, plus existing `MafNewAgent`/`MafNewExecutor`. `MafRunCs0618Hunt` uses a relaxed variant (path may be file OR directory). 6 dedicated tests. |
| 68 | `SourceFileWalker` shared helper (Phase E.5) | Architecture review flagged 17 instances of `EnumerateScannableFiles` + `MakeRelative` duplicated verbatim across 7 tool classes. Slow-drift risk — a `/bin/` exclusion fix would land in some tools but not others. | P2 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase E.5).** New `Tools/SourceFileWalker.{EnumerateCsFiles,MakeRelative}`. 17 → 1. Applied to 7 tools (`AntiPatternScannerTool`, `DoctorTool`, `EstimateCostTool`, `PromptLintTool`, `SarifExportTool`, `SimulateWorkflowTool`, `PreUpgradeDryRunTool`). |
| 69 | `MafDoctor` expanded to 4 scanners (Phase E.4) | MCP review #8: doctor was overclaiming by name while only running 2 of 19 scanners. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase E.4).** Doctor now aggregates `MafScanAntiPatterns` + `MafValidateFanOut` + `MafLintAgentPrompt` + `MafEstimateCost`. `DoctorSummary` record extended with `PromptErrors`/`PromptWarnings`/`UnboundedCostSites`/`AgentCallSitesChecked`. Grade incorporates prompt-injection errors. `Grade` signature backwards-compatible via optional params (existing tests unchanged). |
| 70 | `maf://rules` live resource (Phase E.6) | MCP review #4: README perpetually drifts from actual rule list; need a runtime catalog. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase E.6).** New `maf://rules` resource — auto-generated at runtime from `AntiPatternScannerTool.AllRules` + curated prompt-lint + analyzer rules. 4 new tests including a doc-↔-code drift property test (every scanner rule must appear in the catalog). Structurally kills the doc-drift bug class. |
| 71 | README reconcile + "First 5 minutes" + version drift fix (Phase E.2 + E.3) | Usability review's tier-1 UX bug: README listed 3 tools (reality 19), 11 skills (reality 12), Docker/Analyzer marked pending (both done); csproj `<Version>1.3.0-alpha-5</Version>` ≠ NuGet `1.3.0-alpha-3`; no "hello world" after init. | P1 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase E.2 + E.3).** README tool/skill/agent counts synced; Distribution status updated (Docker ✅, Analyzer ✅); new "First 5 minutes" section with three concrete commands; `init` CLI output leads with `maf-doctor doctor .`; csproj reverted to `1.3.0-alpha-3` (matches NuGet); TROUBLESHOOTING test count 116 → 325. |
| 72 | `NewCommand` package-add hints + analyzer anchor fix (Phase E.6) | Usability review §4: `maf-doctor new agent` doesn't tell greenfield users to `dotnet add package`. Architecture review §6: analyzer `helpLinkUri` slugified anchors don't resolve on GitHub. | P2 | ✅ DONE | 100% | 80% | **Done 2026-05-12 (Phase E.6).** `NewCommand.GenerateAgent` / `GenerateExecutor` next-steps output now lists the `dotnet add package` commands for `Microsoft.Agents.AI` / `Workflows.Generators` / `Extensions.AI` / `Azure.AI.OpenAI` / `Azure.Identity`. Analyzer `helpLinkUri` anchors trimmed to document root (slugified fragments fail on GitHub). |

### Summary — canonical row-count

Audited row-by-row through the 54-row tracking table on 2026-05-11 (post #41 batch):

| Status | Rows | Count |
|---|---|---:|
| ✅ DONE | 1–24, 29, 33–42, 60, 64–65 | 38 |
| 🔄 IN PROGRESS | 25 | 1 |
| ❌ NOT STARTED | 26–28, 30–32, 43–54, 55–59, 61–63, 66 | 27 |
| **Total** | — | **66** |

**Overall completion: 38/66 = 58%.** **197 tests passing across two projects** (186 in MCP server + 11 in Analyzers). The MCP server now exposes **9 executable tools** (`MafApiSafety`, `MafRegistryLookup`, `MafRegistryList`, `MafValidateFanOut`, `MafRunCs0618Hunt`, `MafDiffPackage`, `MafScanAntiPatterns`, `MafSimulateWorkflow`, `MafExplain`) plus **2 scaffolders** (`MafNewAgent`, `MafNewExecutor`) plus **CLI subcommands** (`maf-doctor init`, `maf-doctor new agent|executor <Name>`). Separate `maf-autopilot.Analyzers` package ships **3 Roslyn rules** (MAF001/002/003) for write-time enforcement.

### Magic-path sprint (2026-05-11 night, after the completeness audit)

The user picked the magic path over Path α (close coverage gap first). Result: 4 flagship features shipped instead of 14 incremental rules:

- **#60** `maf-new` scaffolder (greenfield pillar unlocked — dogfooded against own scanners)
- **#29** Roslyn analyzer companion (write-time red squigglies in the IDE — `MAF001`/002/003)
- **#65** `MafSimulateWorkflow` (in-vitro topology analysis + Mermaid diagram — proves workflow completion without LLM calls)
- **#64** `MafExplain` (paste MAF code, get annotated explanation citing canonical guide sections)

The "ongoing best-practice enforcement" pillar (audit verdict §3) is now real via the analyzer. The "implement greenfield" pillar (audit verdict §1) is now real via the scaffolder. The "audit existing code" pillar gained a flagship feature in `MafSimulateWorkflow`. The remaining audit-recommended work (#55 agent rewiring, #56 rule coverage, #58 SARIF, #59 MafDoctor, #61–#63 specialized scanners) is now incremental polish on top of the magic foundation.

### Recommended order of work after the 2026-05-11 sprint

**Pre-1.0 (gold path to a credible release):**
1. **#55** — rewire 3 agents to invoke the 7 MCP tools (1 day, highest leverage)
2. **#56** — expand registry + scanner to cover the 14 un-audited guide sections (3-5 days)
3. **#57** — slim redundant SKILLs + merge fan-out duo (half day)
4. **#46** — fix release-watcher bugs (half day)
5. **#50** — cut stable `1.3.0` once one external migration validates

**Post-1.0 (turn the toolkit from "lint we run" into "Roslyn-grade enforcement"):**
6. **#29** — Roslyn analyzer companion (write-time enforcement)
7. **#58** — SARIF output (GitHub Advanced Security integration)
8. **#52** + GitHub App `@maf-bot` — PR-diff scoped reviews
9. **#59** — `MafDoctor` one-command health letter
10. **#60** — `maf-new` scaffolder (greenfield support)
11. **#15** — auto-update registry from `migration-retrospective` (close the learning loop)

**Experimental / "magic":**
- **#65** — `MafSimulateWorkflow` (in-vitro test harness)
- **#64** — `MafExplain` (paste-MAF-code, get explanation)
- **#63** — `MafScanPromptInjection` (LLM security) Acceptance criteria checklist mirror lives in [`project-status-and-vision.md`](./project-status-and-vision.md) §9.

#### Completed in the 2026-05-11 batch
- P0: #33 (dotnet-inspect bump), #34 (install + version), #35 (test harness)
- P1: #36 (init JSON-clobber + JSONC parser), #37 (inverted-haystack search), #38 (README ↔ plan reconcile), #39 (PascalCase tool names), #40 (CONTRIBUTING + TROUBLESHOOTING), #41 (three executable MCP tools)
- Bonuses: FormatEntry tests, version pin tightening, `MafResources.GetSkill` case-sensitivity fix, `File.Copy` retry-on-collision

### Re-prioritised work order (post-review, 2026-05-11)

**P0 — blockers (must do first):**
1. **#33 Bump `dotnet-inspect` 0.7.6 → 0.7.8** + author attribution + caveat rewrite — foundational, blocks #30 redesign and #41 `MafDiffPackage`
2. **#34 Fix install command + csproj version drift** — first-time-user blocker today
3. **#35 MCP test harness** — would have caught the `AgentThread` regression in commit 393b7a2

**P1 — critical path to credible beta:**
4. **#36 Fix `init` JSON-clobber bug** — silent data loss
5. **#37 Expand `SearchByApiName` + inverted index** — closes a recurring bug class
6. **#38 Reconcile README roadmap with plan** — single source of truth
7. **#40 CONTRIBUTING.md + TROUBLESHOOTING.md** — adoption risk
8. **#41 Wire 3 executable MCP tools** — closes the "1 of 11 skills exposed" gap
9. **#42 `maf-best-practice-reviewer` + `maf-anti-pattern-scanner`** — the identity unlock

**P2 — close gap to README's promise & self-upgrade:**
10. **#43 Expand registry to 9 missing API areas**
11. **#39 Standardise tool naming**
12. **#44 Compile-validate workflow-smoke-tester**
13. **#46 Fix release-watcher bugs**
14. **#47 Per-version guide files**
15. **#30 `registry.yaml` auto-update** — now tractable with v0.7.8
16. **#49 Split `maf-auditor` into two agents**

**P3 — strategic / nice-to-have:**
17. #45 Merge fan-out-validator + fan-in-static-analyzer
18. #48 Major-version detection in watcher
19. #51 Rename decision
20. #52 `pr-diff-auditor`
21. #53 `drift-detector`
22. #54 `pre-upgrade-dry-run`
23. #28 Docker GHCR
24. #29 Roslyn analyzer companion
25. #32 Multi-version migration paths
26. #26 / #27 / #31 Sampling tools

**Final gate:** #50 Cut stable `1.3.0` release once P0+P1 are green and at least one external migration completes.

---

## Table of Contents

1. [Why This Exists — Current State and Its Limits](#1-why-this-exists--current-state-and-its-limits)
2. [Naming: maf-autopilot vs maf-migration-toolkit](#2-naming-maf-autopilot-vs-maf-migration-toolkit)
3. [Vision](#3-vision)
4. [Project Structure (Target)](#4-project-structure-target)
5. [Tier 1 — Fix the Obvious Gaps](#5-tier-1--fix-the-obvious-gaps)
6. [Tier 2 — Self-Updating Guide](#6-tier-2--self-updating-guide)
7. [Tier 3 — Auto-Generated Migration Plans](#7-tier-3--auto-generated-migration-plans)
8. [Tier 4 — Runtime Verification](#8-tier-4--runtime-verification)
9. [Tier 5 — Reflexive Learning](#9-tier-5--reflexive-learning)
10. [The Architectural Capstone — `maf-autopilot` MCP Server](#10-the-architectural-capstone--maf-autopilot-mcp-server)
11. [GitHub Agentic Workflow — Release-Triggered Automation](#11-github-agentic-workflow--release-triggered-automation)
12. [Distribution and Release Strategy](#12-distribution-and-release-strategy)
13. [Compatibility Matrix](#13-compatibility-matrix)
14. [Priority Roadmap](#14-priority-roadmap)
15. [Phase Review Protocol](#15-phase-review-protocol)
16. [MCP-First Adoption Architecture](#16-mcp-first-adoption-architecture)

---

## 1. Why This Exists — Current State and Its Limits

The first version of this toolkit — a single agent + one guide + one migration plan — worked. It successfully migrated `maf-claims-fraud-guardian` to MAF 1.3.0. But the process revealed structural weaknesses that are worth fixing before the next migration cycle.

### What it was

```
.github/agents/maf-migration.agent.md   ← one agent
src/docs/maf-1.3.0-migration-guide.md  ← one big guide
src/docs/migration-plan.md              ← one hand-crafted plan
```

### What went wrong (or was harder than it needed to be)

| Problem | Impact |
|---------|--------|
| **Static guide** — written once for 1.3.0, will be wrong the moment 1.4.0 ships | Toolkit expires on the next MAF release |
| **Build-blind tooling** — `dotnet-inspect` cannot surface `[Obsolete]` at the overload level (filed as issue #316) | CS0618 warnings were missed until `dotnet build` ran, late in the process |
| **Codebase-specific plan** — the migration plan was hand-crafted for one repo, not reusable | Every new migration is hours of manual work |
| **Single-pass value** — once migration is done, the tooling has no ongoing use | Zero retention of learned patterns across teams/projects |
| **Silent runtime failures** — fan-in starvation builds green but fails silently | `PropertyTheftFanOutExecutor` returned `ValueTask` (non-generic), starving the fan-in barrier — caught late |
| **Monolithic agent** — all knowledge embedded in one file, heavy context load | Hard to compose, update, or reuse individual pieces |

### What we built instead

`maf-autopilot`: a composable toolkit where knowledge lives in targeted skills, an auditor agent auto-generates migration plans, an MCP server closes the dotnet-inspect gap, and a GitHub Action keeps everything current when MAF releases a new version.

---

## 2. Naming: maf-autopilot vs maf-migration-toolkit

### `maf-migration-toolkit`

**Pros:** Descriptive, clear, searchable.  
**Cons:** Implies a one-time activity (migration). Doesn't fit ongoing improvement, release watching, or code quality scenarios. "Toolkit" is generic — it doesn't communicate that this is AI-powered.

### `maf-autopilot`

**Pros:**
- "Autopilot" implies ongoing guidance, not just a one-time migration event
- Works for migration AND for improving existing MAF code (structural audits, CS0618 cleanup, fan-out validation)
- Suggests automation and intelligence — AI is the mechanism, not just a helper
- The `maf-release-watcher` and `migration-retrospective` skills fit the "autopilot" metaphor perfectly: the system monitors, learns, and self-updates
- Catchier and more memorable — better for community adoption and sharing
- As MAF evolves past 1.3.0, `maf-migration-toolkit` ages poorly; `maf-autopilot` does not

### Decision: `maf-autopilot`

The name better reflects the project's full scope. A toolkit is a box of tools you pick up when you need them. An autopilot is always on, always watching, always adjusting. That's what this project aspires to be — not a one-time migration assistant, but a permanent co-pilot for MAF development.

The migration capabilities are a first-class use case, but they're one mode of operation. The release watcher, retrospective learner, and `maf-autopilot` MCP server are equally important parts of the same system.

---

## 3. Vision

> **maf-autopilot** is a reusable AI-powered toolkit that: migrates any codebase to the current version of Microsoft Agent Framework, keeps your migration knowledge current as new MAF versions ship, catches runtime failure patterns that build verification misses, and gets smarter with every migration it completes.

### Goals

1. **Reusable** — point it at any MAF codebase and it generates a migration plan in minutes, not hours
2. **Durable** — the guide self-updates when MAF releases a new version
3. **Reliable** — catches both CS0618 compiler warnings AND silent runtime failures (fan-in starvation)
4. **Reflexive** — learns from real migrations, writes its own release notes, improves its own knowledge base
5. **Composable** — knowledge in targeted skills, agents delegate to skills, no monolithic context blobs

---

## 4. Project Structure (Target)

```
maf-autopilot/
├── README.md
├── docs/
│   ├── maf-migration-toolkit-plan.md          ← this file
│   └── compatibility-matrix.md                ← MAF version ↔ dependency versions
├── guides/
│   └── maf-1.3.0-migration-guide.md           ← full guide (versioned, embedded in MCP binary at build)
├── mcp/
│   ├── maf-autopilot/                         ← C# MCP server project (SDK 1.2.0, sampling-ready)
│   │   ├── maf-autopilot.csproj
│   │   ├── Program.cs
│   │   ├── Tools/
│   │   │   ├── ApiSafetyTool.cs
│   │   │   ├── RegistryLookupTool.cs
│   │   │   ├── MigrationPathTool.cs
│   │   │   ├── FanOutValidatorTool.cs
│   │   │   ├── ExecutorPatternTool.cs
│   │   │   └── CompatibilityLookupTool.cs
│   │   └── Data/
│   │       └── (RegistryService + models)
│   └── maf-autopilot.sln
└── .github/
    ├── agents/
    │   ├── maf-migration.agent.md              ← orchestrator
    │   └── maf-auditor.agent.md                ← pre-migration plan generator
    ├── instructions/
    │   └── maf-constraints.instructions.md     ← always-loaded rules
    ├── skills/
    │   ├── dotnet-inspect/SKILL.md             ← enhanced with ⚠️ [Obsolete] limitation
    │   ├── maf-migration-guide/SKILL.md        ← guide section navigator
    │   ├── obsolete-api-registry/
    │   │   ├── SKILL.md
    │   │   └── registry.yaml                  ← machine-readable CS0618 data
    │   ├── migration-plan-creator/
    │   │   ├── SKILL.md
    │   │   └── template.md
    │   ├── cs0618-hunter/SKILL.md
    │   ├── fan-out-validator/SKILL.md
    │   ├── fan-in-static-analyzer/SKILL.md
    │   ├── nuget-diff-analyzer/SKILL.md
    │   ├── maf-release-watcher/SKILL.md
    │   ├── workflow-smoke-tester/SKILL.md
    │   └── migration-retrospective/SKILL.md
    └── workflows/
        └── maf-release-watcher.yml             ← GitHub Actions trigger on MAF release
```

### What lives where and why

| Location | What | Why |
|----------|------|-----|
| `.github/agents/` | Orchestrators | VS Code agent discovery convention |
| `.github/skills/` | On-demand knowledge modules | Loaded only when needed — saves context |
| `.github/instructions/` | Always-on rules | Loaded automatically into every conversation |
| `guides/` | Full reference guides | Large files — skills navigate to sections, agents don't load whole file |
| `.github/skills/obsolete-api-registry/` | Machine-readable CS0618 data | YAML is diffable, composable, LLM-parseable — co-located with its skill |
| `src/maf-autopilot/` | .NET MCP server | Standalone publishable project (SDK 1.2.0 + Sampling) |
| `.github/workflows/` | GitHub Actions | Release-triggered automation |
| `docs/` | Planning & strategy | Human-readable, not agent-consumed |

---

## 5. Tier 1 — Fix the Obvious Gaps

### 5.1 Skill: `cs0618-hunter` **✅ DONE**

**Problem it solves:** `dotnet-inspect` cannot detect `[Obsolete]` at the overload level (upstream issue #316 filed). CS0618 warnings were caught late — after coding, during build.

**What it does:**

1. Runs `dotnet build 2>&1 | Select-String "warning CS0618"`
2. Parses each warning: file path, line, member name, message
3. Looks up each member in `obsolete-api-registry.yaml`
4. For each match: outputs the exact replacement code block with guide section reference
5. For non-registry entries: escalates with a note to add the entry
6. Re-runs build to confirm zero CS0618 matches

**Output format:**

```yaml
- warning: CS0618
  file: src/Workflows/ClaimsFraudDetectionWorkflow.cs
  line: 312
  member: WorkflowBuilder.AddFanInBarrierEdge(ExecutorBinding, IEnumerable<ExecutorBinding>)
  registry_id: MAF130-FAN-IN-001
  fix: "Swap argument order: sources first, target last"
  guide_section: "9.3"
```

**Why not embed this in the main agent?** Because it has a specific looping protocol (build → parse → lookup → fix → rebuild) that should be invoked as a dedicated step, not prose scattered through a long agent.

---

### 5.2 Skill: `nuget-diff-analyzer` **✅ DONE**

**Problem it solves:** Raw `dotnet-inspect diff` output is hard to act on. It lists every changed member in a flat format that requires manual categorization.

**What it does:**

1. Runs `dnx dotnet-inspect@0.7.8 -- diff --package Microsoft.Agents.AI@<old>..<new>`
2. Categorizes the raw output into:
   - **Removed types** (CS0246 risk)
   - **Removed members** (CS0246 risk)
   - **Renamed types or members** (need find-and-replace)
   - **Signature changes** (compilation may pass but runtime behavior changes)
   - **New obsolete overloads** (CS0618 risk — BUT note: dotnet-inspect may not flag these, see `cs0618-hunter`)
   - **New types or members** (opportunities to adopt cleaner APIs)
3. Outputs a structured markdown report with per-category counts and migration guidance
4. Cross-references with `obsolete-api-registry.yaml` to flag any known patterns

**Note on [Obsolete] detection:** `dotnet-inspect diff` shares the same limitation as `member` — new obsolete overloads may not be flagged. Always run `cs0618-hunter` after package update, regardless of what `nuget-diff-analyzer` reports.

> **🔍 Phase Review Checkpoint — Tier 1:** Before proceeding, run the [Phase Review Protocol](#15-phase-review-protocol). Verify every Tier 1 skill's implementation matches its plan description, check all cross-references (skill → guide section numbers), and update the tracking table.

---

## 6. Tier 2 — Self-Updating Guide

This is the highest-leverage investment. The migration guide is the core knowledge asset. If it goes stale, everything built on top of it degrades.

### 6.1 Skill: `maf-release-watcher` **✅ DONE**

**Trigger:** Manual invocation, or automatically via GitHub Actions (see Section 11).

**What it does:**

1. Queries the MAF GitHub repository for new releases (via `mcp_github_list_releases` or `gh release list`)
2. Compares the latest release tag against the version recorded in the guide's metadata header
3. If a new version is detected:
   a. Downloads the release notes
   b. Runs `nuget-diff-analyzer` between old and new version
   c. Parses release notes with LLM to extract: breaking changes, newly obsolete APIs, removed APIs, renamed types, new recommended patterns
   d. Cross-references LLM findings with `dotnet-inspect diff` output (LLM says "X was renamed" → diff confirms old name is gone and new name exists)
   e. Updates `obsolete-api-registry.yaml` with any newly obsolete APIs
   f. Updates `compatibility-matrix.md` with new dependency versions
   g. Generates a new section in the guide: `## Section N — Changes in X.Y.Z → A.B.C`
   h. Creates a PR with all changes for human review

**Why human review before merge?** LLM analysis of release notes is good but not perfect. The PR creates a human-in-the-loop checkpoint before the guide is updated. Over time, as the LLM→diff cross-reference gets tighter, the review becomes a quick sanity check.

### 6.2 Version-keyed guide sections **✅ DONE**

Every section in the migration guide should carry a metadata comment:

```markdown
<!-- introduced: 1.3.0 | applies-to: 1.2.x → 1.3.x | deprecated-in: none -->
## Section 9 — Executor Migration
```

This enables the migration agent to load only sections relevant to the specific version path. Migrating 1.2.0 → 1.4.0 directly? Load sections tagged `1.3.0` and `1.4.0`, skip sections already superseded.

This also makes the guide self-documenting: you can grep for all changes introduced in a specific version, or all sections that still apply to a given migration path.

> **🔍 Phase Review Checkpoint — Tier 2:** Before proceeding, run the [Phase Review Protocol](#15-phase-review-protocol). Verify the release watcher workflow, guide currency, and version-keyed metadata are consistent. Confirm the compat matrix is up to date.

---

## 7. Tier 3 — Auto-Generated Migration Plans

The migration plan for `maf-claims-fraud-guardian` was hand-crafted. It took significant time to write: inventory all files, count occurrences, write before/after patterns for each one, build the tracking table.

That work should happen automatically.

### 7.1 Agent: `maf-auditor` **✅ DONE**

**Inputs:** A path to any MAF codebase + source MAF version + target MAF version.

**What it does:**

1. Scans all `.csproj` files → extracts current MAF package versions
2. Runs `nuget-diff-analyzer` for the version delta
3. Scans all `.cs` files for known old-API patterns (regex table from guide + registry):
   - `ReflectingExecutor<|IMessageHandler<` → executor migration needed
   - `AgentThread|GetNewThread` → session migration needed
   - `\.Deserialize<` → response deserialization migration needed
   - `\.StreamAsync\(\)` → streaming rename needed
   - `AgentRunUpdateEvent` → event rename needed
   - `AIAgentExtensions|app\.MapA2A\b` → A2A migration needed
   - Top-level `Instructions|Tools` in agent options → placement migration needed
4. Checks fan-out topology: finds `AddFanOutEdge` calls, traces handler return types, flags void/non-generic `ValueTask` handlers
5. Checks `AddFanInBarrierEdge` argument order (sources first?)
6. Cross-references all findings with `obsolete-api-registry.yaml`
7. Generates a complete `src/docs/migration-plan.md` using the `migration-plan-creator` skill's template:
   - Task IDs auto-assigned (T1.x packages, T2.x namespaces, etc.)
   - Before/after patterns auto-populated from real code
   - Risk register auto-populated based on patterns found
   - Occurrence counts and file paths filled in

**Output contract:**

```
src/docs/migration-plan.md — fully populated, ready for migration agent to execute
```

**Time saved:** What took hours of manual plan-writing becomes a few minutes of automated analysis. The plan is also more accurate than a hand-written one because it's based on actual code, not estimates.

> **🔍 Phase Review Checkpoint — Tier 3:** Before proceeding, run the [Phase Review Protocol](#15-phase-review-protocol). Confirm the migration-plan-creator template and skill produce valid, complete plans when invoked on a real codebase.

---

## 8. Tier 4 — Runtime Verification

`dotnet build` passing does not mean the code works correctly. The fan-in starvation bug was the canonical example: builds green, runs silently wrong, no error message.

### 8.1 Skill: `workflow-smoke-tester` **✅ DONE**

After migration, auto-generates minimal smoke tests for each workflow pattern found in the codebase.

**Patterns and what they verify:**

| Pattern | Smoke test checks |
|---------|-----------------|
| Fan-out + Fan-in | All N branches complete; aggregator receives exactly N messages |
| Session round-trip | Create session → send message → verify response → serialize → deserialize → send second message → verify continuity |
| Streaming | `RunStreamingAsync()` produces at least one `AgentResponseUpdate` with non-empty `Text` |
| Structured output | `RunAsync<T>()` returns a `T` that deserializes without error |
| Tool invocation | Workflow with registered tools executes a tool call and receives result |

These are **structural sanity checks**, not full integration tests. They run fast, require no production credentials (use mock agents), and catch the class of silent runtime failures that build verification misses entirely.

### 8.2 Skill: `fan-in-static-analyzer` **✅ DONE**

A sophisticated code analysis step that, without running the code, finds every fan-out pattern that will fail at runtime.

**What it does:**

1. Finds every `AddFanOutEdge` call in workflow builder code
2. For each: resolves the executor class and finds its `[MessageHandler]`-annotated handler
3. Checks the handler's return type:
   - `ValueTask<T>` where T is the message type → ✅ correct
   - `void`, `Task`, `ValueTask` (non-generic) → ❌ will silently starve fan-in barrier
4. Traces the fan-out target executor to the corresponding `AddFanInBarrierEdge` call
5. Verifies the fan-in sources list includes all fan-out branches
6. Verifies `AddFanInBarrierEdge` argument order: sources first, target last

**Why static analysis instead of a test?** Because it gives you the diagnosis before you run anything. The output is: "Line 312 — `PropertyTheftFanOutExecutor.HandleAsync` returns `ValueTask` (non-generic). This will silently produce no output. Change return type to `ValueTask<ValidationResult>`."

> **🔍 Phase Review Checkpoint — Tier 4:** Before proceeding, run the [Phase Review Protocol](#15-phase-review-protocol). Confirm smoke-test templates build against actual MAF 1.3.0 packages. Verify fan-in-static-analyzer detection examples are correct.

---

## 9. Tier 5 — Reflexive Learning

The most powerful long-term capability: the toolkit that improves itself after every use.

### 9.1 Skill: `migration-retrospective` **✅ DONE**

Invoked after every migration completes.

**What it does:**

1. Reads the completed `migration-plan.md` — specifically the Notes column of each task, where surprises were recorded
2. Reads the build error history from terminal output (if available)
3. Identifies errors or surprises **not predicted** by the migration plan
4. For each surprise:
   a. Determines the root cause (wrong API pattern in guide? Missing registry entry? New dotnet-inspect limitation?)
   b. Adds a new entry to `obsolete-api-registry.yaml` if it was a CS0618/CS0246 issue
   c. Adds a new entry to the "Known Misalignments" section of the guide
   d. Checks: could `dotnet-inspect` have predicted this? If not → queues a GitHub issue on `richlander/dotnet-inspect`
5. Updates `maf-constraints.instructions.md` with any new hard constraint discovered
6. Updates the agent's own instruction file with any new known-misalignment entry
7. Outputs a retrospective summary: N surprises found, M registry entries added, K guide sections updated

**What this creates over time:** The "Known Misalignments" section becomes the most battle-tested part of the guide — sourced from real migrations across real codebases, not theoretical docs. Each migration makes the next one more reliable.

**The compound effect:** After 10 migrations, the `obsolete-api-registry.yaml` will have dozens of entries covering the real-world CS0618 patterns that the docs miss. The `fan-in-static-analyzer` will know about failure modes that aren't documented anywhere. The toolkit genuinely gets smarter with use.

### 9.2 Reflexive Learning in MCP-First Context

When `maf-autopilot` is deployed as an installed binary (the MCP-first model), the retrospective skill can't directly write back to `registry.yaml` or the migration guide — those files are **embedded at build time** in the binary, not live on disk.

**Where learnings are stored depends on deployment mode:**

| Mode | Where learnings live | How they reach the central toolkit |
|------|---------------------|------------------------------------|
| File-copy mode (17 `.github/` files copied to target repo) | Updated in-place by retrospective skill directly | Manual PR from the updated local files to `joslat/maf-doctor` |
| MCP binary mode (`dotnet tool install -g maf-doctor`) | Cannot write embedded resources | **Via `maf_open_feedback_issue` Sampling tool** |

**The `maf_open_feedback_issue` tool (tracking row #31):**

After a migration completes, this MCP tool uses `server.SampleAsync()` to analyze the migration log (plan file, terminal history, or user-provided excerpt), then — if the user has [GitHub MCP](https://github.com/github/github-mcp-server) connected — opens a structured issue on `joslat/maf-doctor`:

```
Title: [Retrospective] CS0618 pattern not in registry: WorkflowBuilder.SomeMember
Body:
  Codebase: [user-provided or redacted]
  New CS0618 pattern: WorkflowBuilder.SomeMember(oldArgs) — not in registry.yaml
  Suggested registry entry: [YAML block auto-generated from the build warning + LLM]
  Related guide section: [extracted from migration plan]
  Build output: warning CS0618 ...
```

The loop closes: migration → GitHub issue → maintainer reviews → `registry.yaml` entry added → new release → all users benefit via `dotnet tool update -g maf-doctor`. GitHub MCP access is already available in VS Code Copilot (`mcp_github_*` tools) — no extra setup needed for the user.

> **🔍 Phase Review Checkpoint — Tier 5:** Before proceeding, run the [Phase Review Protocol](#15-phase-review-protocol). Confirm the retrospective skill actually produces new registry entries and guide updates when run against a test migration log. Verify the learning loop is closed.

---

## 10. The Architectural Capstone — `maf-autopilot` MCP Server

This is the tool that closes the structural gap at the root.

### The problem it solves

Every skill, every agent, every step in the current toolkit is working around a single fundamental limitation: **there is no single authoritative source that can answer "is this API safe to use in MAF version X?"**

- `dotnet-inspect` tells you what APIs exist but not which are obsolete at the overload level
- The compiler tells you about CS0618 but only after you've written the code
- The guide tells you the known breaking changes but it's English prose, not queryable data
- The registry YAML has machine-readable data but requires a separate tool to query

`maf-autopilot` is that single authoritative source. It combines all of these data sources into one MCP tool that any agent can query.

### Tool surface

The MCP server exposes the following tools:

#### `maf_api_safety`
```
Query: Is WorkflowBuilder.AddFanInBarrierEdge(target, sources) safe in MAF 1.3.0?
Answer: OBSOLETE — CS0618. Use AddFanInBarrierEdge(sources, target) (sources first). See registry: MAF130-FAN-IN-001. Guide section: 9.3.
```

#### `maf_migration_path`
```
Query: What do I need to change to migrate from MAF 1.2.0 to 1.3.0?
Answer: Structured list of required changes, ordered by risk/dependency, with before/after patterns for each.
```

#### `maf_breaking_changes`
```
Query: What breaking changes were introduced in MAF 1.3.0?
Answer: Categorized list: removed types, removed members, renamed members, signature changes, new obsolete overloads. Each with guide section reference.
```

#### `maf_fan_out_validate`
```
Query: Analyze this executor class — is the fan-out handler correct?
Input: C# class source text
Answer: Valid / Invalid + diagnosis. If invalid: exact fix with example.
```

#### `maf_executor_pattern_check`
```
Query: Does this class follow the correct MAF 1.3.0 executor pattern?
Input: C# class source text
Answer: Valid / Invalid. Lists: is it sealed? is it partial? does it have [MessageHandler]? are handler signatures correct? is the generator package referenced?
```

#### `maf_compatibility`
```
Query: What version of Microsoft.Extensions.AI does MAF 1.3.0 require?
Answer: From compatibility-matrix.md — MAF 1.3.0 requires Microsoft.Extensions.AI ≥ 10.5.0, .NET ≥ 8.0, Azure.AI.OpenAI ≥ 2.8.0-beta.1.
```

#### `maf_registry_lookup`
```
Query: Look up registry entry MAF130-FAN-IN-001
Answer: Full YAML entry — old API, new API, fix pattern, guide section, dotnet-inspect detectable (false), issue reference.
```

#### `maf_detect_cs0618`
```
Query: Run CS0618 detection on this solution path
Input: Solution or project path
Answer: Invokes dotnet build, parses output, maps each warning to registry, returns structured fix list. Same as cs0618-hunter skill but as a queryable tool.
```

#### `maf_api_diff`
```
Query: What changed between MAF 1.2.0 and 1.3.0?
Answer: Invokes dotnet-inspect diff internally, post-processes into structured categories, cross-references registry, returns actionable summary.
```

### Internal architecture

```
maf-autopilot MCP Server
├── Data Sources
│   ├── obsolete-api-registry.yaml          (bundled at build time)
│   ├── compatibility-matrix.md             (bundled at build time)
│   └── guides/maf-*.migration-guide.md     (bundled at build time, indexed)
├── External Calls
│   ├── dotnet-inspect CLI                  (spawned as child process)
│   ├── dotnet build                        (spawned, output parsed)
│   └── GitHub Releases API                 (for version checks)
├── Analysis Engine
│   ├── CS0618Parser.cs                     (parse dotnet build output)
│   ├── RegistryLookup.cs                   (YAML registry query)
│   ├── CompatibilityMatrix.cs              (markdown table parser)
│   ├── FanOutAnalyzer.cs                   (Roslyn-lite or regex-based)
│   └── ExecutorPatternAnalyzer.cs          (pattern validation)
└── MCP Transport
    ├── HTTP (StreamableHttp or SSE)
    └── stdio (for local VS Code use)
```

**Key design decisions:**
- The server bundles the registry and compatibility matrix at build time — no external file dependency
- `dotnet-inspect` is invoked as a child process (it's already a CLI tool), not re-implemented
- Fan-out and executor analysis are Roslyn-based for accuracy, with a regex fallback for speed
- The MCP transport supports both HTTP (for remote/container use) and stdio (for local VS Code `mcp.json` integration)

### VS Code MCP configuration

Once published, any project using `maf-autopilot` can add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "maf-autopilot": {
      "command": "dotnet",
      "args": ["tool", "run", "maf-autopilot"],
      "type": "stdio"
    }
  }
}
```

Or for the Docker version:

```json
{
  "servers": {
    "maf-autopilot": {
      "url": "http://localhost:7891/sse",
      "type": "sse"
    }
  }
}
```

### How this closes the dotnet-inspect gap

GitHub issue #316 was filed because `dotnet-inspect member` doesn't flag `[Obsolete]` on individual overloads. `maf-autopilot`'s `maf_api_safety` tool directly answers the question that motivated that issue. Instead of filing an upstream bug and waiting, we build the answer ourselves using the compiler output. If `richlander/dotnet-inspect` eventually adds `[Obsolete]` surfacing, `maf-autopilot` can delegate to it; until then, `maf-autopilot` is the authoritative answer.

---

## 11. GitHub Agentic Workflow — Release-Triggered Automation

### The vision

When Microsoft publishes a new MAF release, `maf-autopilot` automatically:
1. Detects the release
2. Analyzes what changed (breaking changes, obsolete APIs, new patterns)
3. Updates the migration guide, registry, and compatibility matrix
4. Rebuilds the `maf-autopilot` MCP server with the new data
5. Creates a PR for human review
6. Publishes the updated MCP server as a new release

A human reviews the PR, merges it, and the toolkit is current. The entire process — from MAF release to updated toolkit — takes minutes of human time.

### Trigger design

```yaml
# .github/workflows/maf-release-watcher.yml
name: MAF Release Watcher

on:
  schedule:
    - cron: '0 9 * * 1'   # every Monday 9 AM UTC (weekly check)
  workflow_dispatch:        # manual trigger anytime
    inputs:
      maf_version:
        description: 'Specific MAF version to process (leave blank for latest)'
        required: false

# Also: can be triggered by a repository_dispatch event
# if Microsoft publishes a webhook or if we use a separate
# GitHub App that watches the MAF NuGet feed
```

> **On direct release-event triggering:** GitHub Actions `on: release` only fires for releases in your own repository. Since MAF releases are published by Microsoft (likely in `microsoft/agents` or as NuGet packages), we can't directly subscribe to their release events. The options are:
> 1. **Weekly scheduled check** (safest, zero setup) — the workflow checks if there's a new version, exits early if not
> 2. **NuGet feed polling** — query `https://api.nuget.org/v3/registration5/microsoft.agents.ai/index.json`, compare latest version to pinned version in a `.maf-version` file
> 3. **GitHub repository watch + `repository_dispatch`** — a separate lightweight workflow watches the MAF repo releases and dispatches an event to `maf-autopilot`

**Recommended approach:** Start with the weekly scheduled check. It's simple, reliable, and requires no external setup.

### Trigger Option 4 — Dependabot (elegant, zero-maintenance)

Dependabot can monitor `Microsoft.Agents.AI` in `src/maf-autopilot/maf-autopilot.csproj` and create a version-bump PR the moment a new version ships. Your workflow fires on that PR event — no custom polling infrastructure needed.

**`dependabot.yml` config:**
```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/src/maf-autopilot"
    schedule:
      interval: daily
    open-pull-requests-limit: 1
    labels: ["maf-release-bump"]
```

**Workflow trigger addition:**
```yaml
on:
  pull_request:
    types: [opened]
```
Add a job condition: `if: contains(github.event.pull_request.labels.*.name, 'maf-release-bump')`.

**Verdict:** Dependabot handles detection with zero custom code — it's maintained infrastructure already trusted at org scale. The weekly schedule is the simpler starting point; switch to Dependabot once the analysis workflow is stable and proven.

### Workflow steps

```yaml
jobs:
  check-for-new-maf-release:
    runs-on: ubuntu-latest
    outputs:
      new_version: ${{ steps.version-check.outputs.new_version }}
      old_version: ${{ steps.version-check.outputs.old_version }}
      has_update: ${{ steps.version-check.outputs.has_update }}
    steps:
      - uses: actions/checkout@v4
      - name: Check NuGet for new MAF version
        id: version-check
        run: |
          # Query NuGet API for latest Microsoft.Agents.AI version
          LATEST=$(curl -s "https://api.nuget.org/v3-flatcontainer/microsoft.agents.ai/index.json" | jq -r '.versions[-1]')
          CURRENT=$(cat .maf-version)
          echo "old_version=$CURRENT" >> $GITHUB_OUTPUT
          echo "new_version=$LATEST" >> $GITHUB_OUTPUT
          echo "has_update=$([ "$LATEST" != "$CURRENT" ] && echo true || echo false)" >> $GITHUB_OUTPUT

  analyze-and-update:
    needs: check-for-new-maf-release
    if: needs.check-for-new-maf-release.outputs.has_update == 'true'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install dotnet-inspect
        run: dotnet tool install --global dotnet-inspect

      - name: Run nuget diff analysis
        id: diff
        run: |
          # Generates structured diff between old and new MAF versions
          dotnet-inspect diff \
            --package "Microsoft.Agents.AI@${{ needs.check-for-new-maf-release.outputs.old_version }}..${{ needs.check-for-new-maf-release.outputs.new_version }}" \
            --source https://api.nuget.org/v3/index.json \
            --output diff-output.json

      - name: Parse release notes
        # Uses GitHub Copilot / LLM API to analyze release notes
        # and cross-reference with dotnet-inspect diff
        uses: ./.github/actions/analyze-release
        with:
          old_version: ${{ needs.check-for-new-maf-release.outputs.old_version }}
          new_version: ${{ needs.check-for-new-maf-release.outputs.new_version }}
          diff_file: diff-output.json

      - name: Update obsolete-api-registry.yaml
        run: python scripts/update-registry.py --diff diff-output.json --version ${{ needs.check-for-new-maf-release.outputs.new_version }}

      - name: Update compatibility-matrix.md
        run: python scripts/update-compat-matrix.py --version ${{ needs.check-for-new-maf-release.outputs.new_version }}

      - name: Generate new guide section
        run: python scripts/generate-guide-section.py --from ${{ needs.check-for-new-maf-release.outputs.old_version }} --to ${{ needs.check-for-new-maf-release.outputs.new_version }}

      - name: Rebuild maf-autopilot MCP server
        run: |
          dotnet restore src/maf-autopilot/maf-autopilot.csproj
          dotnet build src/maf-autopilot/maf-autopilot.csproj --configuration Release
          dotnet publish src/maf-autopilot/maf-autopilot.csproj \
            --configuration Release \
            --output src/dist/

      - name: Update .maf-version
        run: echo "${{ needs.check-for-new-maf-release.outputs.new_version }}" > .maf-version

      - name: Create Pull Request
        uses: peter-evans/create-pull-request@v6
        with:
          token: ${{ secrets.GITHUB_TOKEN }}
          title: "chore: Update for MAF ${{ needs.check-for-new-maf-release.outputs.new_version }}"
          body: |
            ## Automated MAF release update

            MAF version: `${{ needs.check-for-new-maf-release.outputs.old_version }}` → `${{ needs.check-for-new-maf-release.outputs.new_version }}`

            ### Changes in this PR
            - [ ] `obsolete-api-registry.yaml` updated with new entries
            - [ ] `compatibility-matrix.md` updated
            - [ ] New guide section added: `Section N — Changes in X.Y → A.B`
            - [ ] `maf-autopilot` MCP server rebuilt

            ### Human review checklist
            - [ ] Verify LLM-generated guide section is accurate
            - [ ] Verify new registry entries have correct fix patterns
            - [ ] Spot-check compatibility matrix against official docs
            - [ ] Confirm MCP server build passes
          branch: "chore/maf-${{ needs.check-for-new-maf-release.outputs.new_version }}-update"
          commit-message: "chore: Update for MAF ${{ needs.check-for-new-maf-release.outputs.new_version }}"

  publish-mcp-release:
    needs: [check-for-new-maf-release, analyze-and-update]
    if: github.event_name == 'workflow_dispatch' && github.event.inputs.publish == 'true'
    # Only publish when explicitly requested — not on every PR
    # Separate job to require manual approval
    runs-on: ubuntu-latest
    environment: release  # requires manual approval in GitHub Environments settings
    steps:
      - name: Publish to NuGet
        run: dotnet nuget push src/dist/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json

      - name: Push Docker image
        run: |
          docker build -t ghcr.io/${{ github.repository_owner }}/maf-autopilot:${{ needs.check-for-new-maf-release.outputs.new_version }} src/maf-autopilot/
          docker push ghcr.io/${{ github.repository_owner }}/maf-autopilot:${{ needs.check-for-new-maf-release.outputs.new_version }}
```

### The Copilot Coding Agent alternative

Instead of the scripted analysis steps above, the workflow could invoke GitHub Copilot's coding agent:

```yaml
      - name: Run maf-auditor agent
        uses: github/copilot-coding-agent@v1  # hypothetical — replace with actual action when available
        with:
          prompt: |
            A new MAF release has been published: ${{ needs.check-for-new-maf-release.outputs.new_version }}.
            Run the maf-release-watcher skill to:
            1. Analyze the diff from ${{ needs.check-for-new-maf-release.outputs.old_version }}
            2. Update obsolete-api-registry.yaml
            3. Update compatibility-matrix.md
            4. Generate a new guide section
            Follow the maf-release-watcher skill instructions exactly.
```

This approach leverages the same agents and skills defined in `.github/agents/` and `.github/skills/` — the CI workflow and the VS Code agent share the same knowledge base. One source of truth.

### Workflow Approach Comparison

| | **A: Pure Scripts** | **B: Copilot Coding Agent** | **C: Hybrid** *(Recommended)* |
|---|---|---|---|
| Compat matrix update | ✅ Script (structured table edit) | ✅ Agent | ✅ Script |
| `.maf-version` bump | ✅ `echo "X.Y.Z" > .maf-version` | ✅ Agent | ✅ Script |
| `registry.yaml` new entries | ❌ Requires semantic analysis | ✅ Agent interprets diff | ✅ Agent |
| Guide section content | ❌ Template placeholders only | ✅ Agent writes real content | ✅ Agent |
| PR creation | ✅ `peter-evans/create-pull-request` | ✅ Agent | ✅ Script |
| Cost | Free | LLM tokens per run | Low (agent for semantic steps only) |
| Determinism | High | Low — human review required either way | Medium |
| Requires Copilot Enterprise license | No | Yes | Partial (semantic steps only) |
| **Current status** | Partially implemented | Not yet stable | **Target state** |

**The honest reality of Approach A:** The current workflow uses Python scripts (`update-registry.py`, `generate-guide-section.py`) that don't exist yet. Writing them to produce _meaningful_ guide sections and registry entries without an LLM is effectively impossible — you get mechanical placeholder text at best. Scripts are right for the structured/mechanical steps; semantic interpretation of an API diff requires a model.

**Why Hybrid (C) wins:**
- **Mechanical steps** (version check, compat matrix row, `.maf-version`, csproj bump, PR shell) — scripted, deterministic, free
- **Semantic steps** (guide section narrative, new registry entry with correct fix pattern, PR description with analysis) — Copilot Coding Agent, running the same `maf-release-watcher` skill used in VS Code chat

One source of truth. The CI job and the developer's VS Code chat share the same `.github/skills/maf-release-watcher/SKILL.md`. Two invocation modes, zero duplication.

**Recommended path:**
1. **Now** — implement the mechanical script steps; mark semantic steps as `# TODO: Copilot Agent` in the workflow
2. **When `github/copilot-coding-agent@v1` is stable** — add the hybrid agent step for guide + registry generation
3. **Long-term** — switch trigger from weekly schedule to Dependabot

---

## 12. Distribution and Release Strategy

### The `maf-autopilot` MCP server needs a distribution strategy

MCP servers are just processes that implement the [Model Context Protocol](https://modelcontextprotocol.io/). They can be distributed via any mechanism that gets a binary to the user's machine. Here are the options and tradeoffs:

### Quick Comparison

| | **Option 1: .NET Global Tool** | **Option 2: Docker** | **Option 3: Binary** | **Option 4: `dnx`** |
|---|---|---|---|---|
| Install | `dotnet tool install -g` | `docker pull` | Manual download | None |
| Startup | ~2s (JIT) | ~1s | ~0.1s | ~3s first run |
| Auto-update | `dotnet tool update -g` | `docker pull :latest` | Manual | Always latest |
| .NET SDK required | ✅ Yes | ❌ No | ❌ No | ✅ Yes |
| Docker required | ❌ No | ✅ Yes | ❌ No | ❌ No |
| `mcp.json` entry | `"command": "maf-doctor"` | `"url": "http://localhost:7891/mcp"` | `"command": "/path/maf-autopilot"` | `"command": "dnx", "args": [...]` |
| Audience fit | .NET developers ✅ | CI / non-.NET | Offline / version-pinned | dotnet-inspect-familiar |
| **Verdict** | **Primary** | **Secondary** | **Fallback** | **Optional** |

**Recommendation:** Option 1 as primary — the target audience is .NET developers who already have the SDK and understand `dotnet tool`. Add Option 3 binary downloads as GitHub Release assets for teams where global tool installation is restricted. Option 2 Docker as secondary for CI pipelines and non-.NET environments. Option 4 `dnx` is useful for one-off invocations without installation.

### Option 1: .NET Global Tool *(primary recommendation)*

Package `maf-autopilot` as a .NET global tool. Users install once:

```bash
dotnet tool install --global maf-doctor
```

**How to release:**
1. Add `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>maf-autopilot</ToolCommandName>` to the `.csproj`
2. `dotnet pack` produces a `.nupkg`
3. `dotnet nuget push` to NuGet.org
4. GitHub Actions workflow runs `dotnet nuget push` in the `publish-mcp-release` job

**VS Code `mcp.json` entry:**
```json
{
  "servers": {
    "maf-autopilot": {
      "command": "maf-doctor",
      "type": "stdio"
    }
  }
}
```

**Pros:** No Docker required, works on all platforms, version-managed via `dotnet tool update`, familiar workflow for .NET developers.  
**Cons:** Requires .NET SDK installed. Tool startup is slightly slower than a native binary.

### Option 2: Docker / GitHub Container Registry

```bash
docker pull ghcr.io/joslat/maf-doctor:latest
docker run -p 7891:7891 ghcr.io/joslat/maf-doctor:latest
```

**How to release:**
1. Add a `Dockerfile` to `src/maf-autopilot/`
2. GitHub Actions builds and pushes to `ghcr.io` on release

**VS Code `mcp.json` entry:**
```json
{
  "servers": {
    "maf-autopilot": {
      "url": "http://localhost:7891/mcp",
      "type": "http"
    }
  }
}
```

**Pros:** No .NET SDK required on the host, works in any environment, easy to run in CI.  
**Cons:** Docker required, higher memory overhead, HTTP transport slightly more setup.

### Option 3: GitHub Releases (binary download)

Publish self-contained binaries (`maf-autopilot-win-x64.exe`, `maf-autopilot-linux-x64`, `maf-autopilot-osx-arm64`) as GitHub Release assets.

```bash
dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Pros:** Works without any runtime installed, fast startup.  
**Cons:** Manual download, no auto-update mechanism, larger binary sizes.

### Option 4: `dnx` (same pattern as dotnet-inspect)

If published to NuGet, users can run without installing:

```bash
dnx maf-autopilot@latest -- api-safety "WorkflowBuilder.AddFanInBarrierEdge" --version 1.3.0
```

**Pros:** Zero installation, always latest version, matches the established dotnet-inspect UX pattern.  
**Cons:** Slower first run (downloads on demand), requires `dnx` installed.

### Versioning strategy

`maf-autopilot` version tracks MAF version with a tool revision suffix:

```
maf-autopilot 1.3.0-tool.1   (tracks MAF 1.3.0, tool revision 1)
maf-autopilot 1.3.0-tool.2   (same MAF data, bug fix in tool)
maf-autopilot 1.4.0-tool.1   (tracks MAF 1.4.0)
```

This makes it immediately clear what MAF version the tool's data covers, without tying the tool's patch releases to MAF's version scheme.

### Release checklist (for the GitHub Actions `publish-mcp-release` job)

```
[ ] Run dotnet build and tests — zero errors
[ ] Run dotnet pack — nupkg produced
[ ] Run dotnet nuget push — published to NuGet.org
[ ] Build Docker image — succeeds
[ ] Push to ghcr.io — succeeds  
[ ] Create GitHub Release with:
    - Changelog (generated from migration guide delta)
    - Binary assets (win-x64, linux-x64, osx-arm64)
    - Docker pull command in release notes
    - NuGet install command in release notes
[ ] Update .vscode/mcp.json example in README.md
```

---

## 13. Compatibility Matrix

The compatibility matrix lives at `docs/compatibility-matrix.md` and is auto-updated by the `maf-release-watcher` workflow.

```markdown
# MAF Compatibility Matrix

| MAF Version | Microsoft.Extensions.AI | .NET | Azure.AI.OpenAI | Generators Package |
|-------------|------------------------|------|-----------------|-------------------|
| 1.3.0 | ≥ 10.5.0 | ≥ 8.0 | ≥ 2.8.0-beta.1 | 1.3.0 (required) |
| 1.2.0 | ≥ 10.3.0 | ≥ 8.0 | ≥ 2.6.0 | N/A (not required) |
| 1.1.0 | ≥ 9.4.0 | ≥ 8.0 | ≥ 2.4.0 | N/A |
```

This is the file I have not seen anywhere else. Every team migrating MAF loses time finding these exact version constraints. Having them in one place, machine-readable, auto-updated, is a real time saver.

---

## 14. Priority Roadmap

Ordered by value-to-effort ratio. See the **Implementation Tracking Table** at the top for full status.

### ✅ Implemented (as of May 2026)

- [x] `maf-migration.agent.md` — migration orchestrator
- [x] `maf-auditor.agent.md` — pre-migration plan generator
- [x] `maf-constraints.instructions.md` — always-loaded breaking changes + hard rules
- [x] `cs0618-hunter` skill — compiler-based CS0618 detection (5-step workflow)
- [x] `fan-out-validator` skill — silent fan-in starvation detection
- [x] `fan-in-static-analyzer` skill — static code analysis of fan-out topology
- [x] `obsolete-api-registry` skill + `registry.yaml` — 10 entries covering all known 1.3.0 patterns
- [x] `migration-plan-creator` skill + `template.md` — complete plan generation
- [x] `maf-migration-guide` skill — 21-section guide navigator
- [x] `dotnet-inspect` skill — enhanced with ⚠️ [Obsolete] limitation warning + issue #316 link
- [x] `nuget-diff-analyzer` skill — structured diff post-processing with risk categorization
- [x] `maf-release-watcher` skill — full workflow: detect → diff → update → PR
- [x] `workflow-smoke-tester` skill — 5 pattern smoke test templates
- [x] `migration-retrospective` skill — post-migration learning loop
- [x] `guides/maf-1.3.0-migration-guide.md` — 21-section, 2000+ line reference guide
- [x] `docs/compatibility-matrix.md` — 4 MAF versions, removed packages table
- [x] `.github/workflows/maf-release-watcher.yml` — weekly NuGet check → diff → PR creation
- [x] `.maf-version` — tracks current version `1.3.0`
- [x] `guides/maf-1.3.0-migration-guide.md` — version-keyed metadata comments applied to all 25 sections
- [x] `README.md` — duplicate skill routing rows + duplicate callout removed

- [x] `maf-autopilot` MCP server MVP — `maf_api_safety`, `maf_registry_lookup`, `maf_registry_list` tools (SDK 1.2.0, sampling-ready)
  - *`src/maf-autopilot/` C# project, stdio transport, ModelContextProtocol 1.2.0, YamlDotNet, embedded `registry.yaml`. `dotnet build` passes clean.*
  - *3 tools: `maf_api_safety` (SAFE/UNSAFE verdict), `maf_registry_lookup` (full entry by ID), `maf_registry_list` (all IDs)*
  - *`server.SampleAsync()` available — enables future agentic tools. `.vscode/mcp.json` updated.*

- [x] `maf-autopilot` MCP Resources (#22) — `maf://constraints`, `maf://registry`, `maf://guide`, `maf://skills{?name}`
  - *`Resources/MafResources.cs` (`[McpServerResourceType]`). 4 resource handlers. 13 EmbeddedResource entries in csproj. `WithResourcesFromAssembly()` in `Program.cs`.*

- [x] `maf-autopilot` MCP Prompts (#23) — `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt` slash commands
  - *`Prompts/MafPrompts.cs` (`[McpServerPromptType]`). All prompts reference `maf://` URIs and inject constraint rules inline. `WithPromptsFromAssembly()` in `Program.cs`.*

- [x] `maf-doctor init` (#24) — one-command project onboarding
  - *`InitCommand.cs`. Intercepts `init` arg before MCP Host starts. Merges `.vscode/mcp.json` (idempotent). Writes `.github/copilot-instructions.md` (MAF hard constraints) only if not exists.*

### ❌ Not Started — Medium Priority (P2)

- [ ] #25 NuGet publish — `dotnet tool install -g maf-doctor` on NuGet.org
- [ ] #30 `registry.yaml` auto-update in CI — hybrid Copilot Agent step to close the last manual gap in the release watcher
- [ ] #31 `maf_open_feedback_issue` tool — reflexive learning via GitHub Issues for MCP binary mode

### ❌ Not Started — Long-term / Nice-to-have (P3)

- [ ] #26 Sampling tools — `maf_full_audit`, `maf_migration_suggest` using `server.SampleAsync()`
- [ ] #27 Full analysis tools — `maf_detect_cs0618`, `maf_api_diff`, `maf_fan_out_validate`, `maf_compatibility`, `maf_migration_path`
- [ ] #28 Docker distribution via GHCR — `ghcr.io/joslat/maf-doctor:latest`
- [ ] #29 Roslyn analyzer companion — flags void fan-out handlers at write-time in IDE
- [ ] #32 Multi-version migration paths — 1.1.0 → 1.3.0 in one pass without step-by-step

---

### What's Missing Most — Honest Gap Assessment

| Gap | Impact | Effort | Row | Recommendation |
|-----|--------|--------|-----|----------------|
| `maf-autopilot` MVP (3 tools) | ✅ Done | — | #21 | Shipped |
| MCP Resources + Prompts | ✅ Done | — | #22, #23 | Shipped May 5, 2026 |
| `maf-doctor init` | ✅ Done | — | #24 | Shipped May 5, 2026 |
| NuGet publish | High — zero-friction `dotnet tool install -g` adoption | Low | #25 | **Next** — requires NuGet API key secret in GitHub repo (see [docs/setup.md](setup.md)) |
| `registry.yaml` auto-update in CI | Medium — last manual step in release automation | Medium | #30 | Hybrid workflow: script mechanical, Copilot Agent for semantic |
| `maf_open_feedback_issue` | Medium — closes reflexive learning in binary mode | Low | #31 | After #25 (needs published binary first) |
| Full analysis tools (9 total) | High — covers diff, fan-out, compat | Large | #27 | After core MCP primitives (#22–25) ship |
| Sampling (agentic) tools | Medium — `SampleAsync()` already unlocked | Medium | #26 | After full analysis tools |
| Version-keyed guide sections | ✅ Done | — | #20 | Shipped |
| Roslyn analyzer | Low | Large | #29 | Defer — compile-time already catches the same issues |

---

*This document is maintained by the `maf-autopilot` project. Update it after each major design decision or migration completed.*

---

## 15. Phase Review Protocol

> **Why this exists:** The plan was written iteratively, and by the time Tier 1 was fully implemented, the Priority Roadmap was already wrong about what was done. Structured reviews after each phase prevent this drift from accumulating silently.

### What a Phase Review Does

A phase review answers five questions:

1. **Does the implementation match the plan?** Walk every task in the completed tier — compare the plan description against the actual file content.
2. **Are there gaps?** Skills that reference guide sections, agents that reference skills, workflows that reference scripts — are those cross-references intact and accurate?
3. **Are there divergences?** Did implementation choices differ from the plan? If yes, is the plan updated to reflect reality?
4. **Is the tracking table honest?** Every completed task must be ✅ DONE with a realistic `% Reviewed` value. `100% done` means no known gaps, not just "I wrote something".
5. **Do downstream artifacts need updating?** If the registry gained a new entry, does the skill navigator reference it? If a guide section changed number, do all skills pointing at it still have the right number?

### When to Run

| Trigger | Action |
|---------|--------|
| All tasks in a Tier are ✅ DONE | Run Phase Review before starting next Tier |
| A core component is updated (guide, registry, an agent) | Run targeted review of updated component + its dependents |
| After a real migration is completed with the toolkit | Run `migration-retrospective` skill + Phase Review of any skills that produced surprises |
| Starting a new session after a significant time gap | Full Phase Review of all components to find staleness |
| Before targeting a new MAF version | Full Phase Review to identify what needs updating |

### Checklist

**1. Plan vs. Implementation**
- [ ] Read each task description in the just-completed Tier — does the actual file match what the plan says?
- [ ] Are `% Done` values honest?
- [ ] Are all cross-references correct? (skill → guide section number, agent → skill name, etc.)

**2. Internal Consistency**
- [ ] Does `maf-migration-guide` SKILL.md section navigator reference all current guide sections?
- [ ] Do `maf-constraints.instructions.md` hard constraints match what the guide and registry say?
- [ ] Does `README.md` skill routing table list all skills with correct file paths?
- [ ] If `registry.yaml` changed: does `cs0618-hunter` SKILL.md reference new entry IDs?

**3. Quality Check**
- [ ] Scan each artifact for stale text: "TODO", "PLACEHOLDER", "TBD", wrong version numbers, dead links
- [ ] Do "Before" code examples show the actual broken pattern (not the fix)?
- [ ] Do all MAF 1.3.0 code examples use valid 1.3.0 APIs?

**4. Tracking Table Update**
- [ ] Update Status, `% Done`, and `% Reviewed` for all changed rows
- [ ] Add Notes for any discovered gaps or issues
- [ ] Update summary table totals
- [ ] Bump the "Audit date" in the tracking table header

### Who Runs It

- **During active development sessions:** The AI agent running the session runs the review as its final step before declaring a tier complete.
- **After a real migration:** `migration-retrospective` skill handles the learning loop; Phase Review updates the plan.
- **Periodic health check:** Run a full Phase Review at the start of any new session after a significant gap.

### Phase Review History

| Date | Scope | Findings | Actions |
|------|-------|----------|---------|
| May 4, 2026 | Full toolkit — all tiers | Plan roadmap ~50% stale; registry had 10 entries vs. 1 claimed; README had duplicate content; guide lacked version metadata | Added 25-row tracking table, fixed README duplicates, added version metadata to all 25 guide sections, rewrote Section 14 roadmap |
| May 4, 2026 | Session update | `maf-autopilot` MCP server: renamed from `maf-inspect`, SDK upgraded `0.6.*` → `1.2.0`. Sampling confirmed via `server.SampleAsync()`. | Row 21 ✅ DONE; plan fully updated; .sln, README, compat-matrix, workflow all cleaned up. |
| May 4, 2026 | Architectural review | User raised adoption friction: copying 17 files per repo is a barrier. MCP Resources + Prompts + `init` command identified as the solution. | Added Section 16 (MCP-First Architecture); expanded tracking table to 29 rows; rows 22–25 added as P2 adoption-layer tasks. |
| May 5, 2026 | MCP Resources + Prompts | Implemented rows 22 and 23. `Resources/MafResources.cs`: 4 resource handlers (`maf://constraints`, `maf://registry`, `maf://guide`, `maf://skills{?name}`). `Prompts/MafPrompts.cs`: 3 prompts (`maf-audit`, `maf-migrate`, `maf-cs0618-hunt`). 13 new EmbeddedResource entries in csproj. `dotnet build` clean. | Rows 22 ✅, 23 ✅; 24/32 = 75%. |
| May 5, 2026 | maf-release-watcher.yml fixes + init command | Fixed 3 CI bugs: removed wrong `dotnet-inspect` global install, upgraded .NET SDK to 9.0.x, changed `dotnet publish` to `dotnet pack`. Fixed plan row 30 self-referencing note. Implemented `maf-doctor init` (#24): `InitCommand.cs`, idempotent mcp.json merge, copilot-instructions.md generation. Smoke-tested. `dotnet build` clean. `.github/copilot-instructions.md` created from init. | Rows 24 ✅; 25/32 = 78%. |

---

## 16. MCP-First Adoption Architecture

### The Problem: 17 Files Per Repo

The current toolkit works well once installed. The friction is installation. A developer who wants to migrate their repo to MAF 1.3.0 today must:

1. Find the `maf-autopilot` repo
2. Copy 17 files into their project: 2 agents, 11 skills, 1 constraints file, 1 registry, 1 guide, 1 workflow, 1 README
3. Maintain those copies — when maf-autopilot improves, re-copy

That's a real barrier to adoption, especially for teams. Nobody copies 17 files for a migration tool.

### The Solution: MCP-First, 2 Generated Files

MCP has three primitives that together solve this completely:

| MCP Primitive | What it does | What it replaces |
|---------------|-------------|-----------------|
| **Tools** | Callable analysis functions | Already implemented (3 tools, 6 more planned) |
| **Resources** | Server-hosted readable content at stable URIs | All 11 SKILL.md files, the migration guide, registry YAML |
| **Prompts** | Slash-command templates that pre-load full context | The `.agent.md` agent workflow files |

**The key mechanism: MCP Prompts can embed Resources.** A `/maf-audit` prompt can return messages containing embedded resource content — the full agent instructions, constraints table, and all relevant skills — fully loaded in one shot, no local files required.

### Target Developer Experience

```bash
# One time per machine
dotnet tool install -g maf-doctor

# Per project (30 seconds)
cd my-legacy-maf-repo
maf-doctor init

# In VS Code — open Copilot Chat
/maf-audit    → full migration audit starts, context fully loaded
/maf-migrate  → step-by-step migration with build verification
```

Two generated files in the target repo, never manually maintained:

```
.vscode/mcp.json                        ← generated, points to global tool
.github/copilot-instructions.md         ← generated, 10 hard rules only
```

That's it. The 17 files that were previously copied are now bundled inside `maf-autopilot`.

### Why Keep a Local `copilot-instructions.md` at All?

The 10 hard constraint rules (`NEVER add [StreamsMessage]`, `NEVER store session state in instance fields`, etc.) must auto-load **before** any tool call fires. MCP Prompts only activate when explicitly invoked. If a developer starts typing code before running `/maf-migrate`, nothing catches them.

The generated `copilot-instructions.md` is the passive safety net. It's small (~20 lines), generated from the tool, and never needs manual updating (re-run `maf-doctor init` to regenerate).

### Resource URI Scheme

```
maf://skills/cs0618-hunter          → .github/skills/cs0618-hunter/SKILL.md content
maf://skills/fan-out-validator      → .github/skills/fan-out-validator/SKILL.md content
maf://skills/maf-migration-guide    → .github/skills/maf-migration-guide/SKILL.md content
maf://guide/section/9               → guides/maf-1.3.0-migration-guide.md section 9
maf://guide/full                    → full migration guide
maf://registry                      → .github/skills/obsolete-api-registry/registry.yaml
maf://constraints                   → .github/instructions/maf-constraints.instructions.md
maf://agent/maf-migration           → .github/agents/maf-migration.agent.md content
maf://agent/maf-auditor             → .github/agents/maf-auditor.agent.md content
```

All content is **embedded at build time** — the tool is self-contained, no external file access required.

### Prompt Design

| Prompt | Arguments | What it loads |
|--------|-----------|---------------|
| `/maf-audit` | `solutionPath` | Embeds `maf://agent/maf-auditor` + `maf://constraints` + `maf://registry` |
| `/maf-migrate` | `planPath` (optional) | Embeds `maf://agent/maf-migration` + `maf://constraints` + all skills |
| `/maf-cs0618-hunt` | `solutionPath` | Embeds `maf://skills/cs0618-hunter` + `maf://registry` |
| `/maf-fan-out-check` | `executorSourceCode` | Embeds `maf://skills/fan-out-validator` + relevant guide section |

### Architecture: maf-autopilot Repo as Source of Truth

The `.github/` files in `maf-autopilot` remain the canonical source. The MCP server is the packaging and delivery mechanism:

```
maf-autopilot repo (.github/*.md files)
          │
          │  dotnet build (EmbeddedResource)
          ▼
    maf-autopilot binary
    ├── Tools     (API safety checks, registry queries)
    ├── Resources (all skill/guide/agent docs at maf:// URIs)
    └── Prompts   (/maf-audit, /maf-migrate, etc.)
          │
          │  mcp.json → stdio transport
          ▼
    Any developer's repo
    (2 generated files only)
```

When the toolkit is updated (new MAF version, new skill), developers run `dotnet tool update -g maf-doctor` — everything updates. No file copying.

### Comparison to Current Approach

| | Current (file-copy) | MCP-first |
|--|---------------------|-----------|
| Files to add to repo | 17 | 2 (generated) |
| Maintenance effort | Re-copy on update | `dotnet tool update` |
| Works across all repos | No — per-repo copy | Yes — install once |
| Agent workflows available | In repo only | As slash commands anywhere |
| Skills available | In repo only | As MCP Resources anywhere |
| Hard constraints auto-load | Yes (via `.instructions.md`) | Yes (via generated minimal file) |

### Implementation Order (P2 priority, items 22–25)

1. **MCP Resources** (#22) — Embed all SKILL.md files and guide sections. Simple: read embedded resource, return as text. Low risk, high value.
2. **MCP Prompts** (#23) — Expose `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt`. Each prompt embeds the relevant Resources as context. Medium complexity.
3. **`maf-doctor init`** (#24) — CLI mode detection (when run without MCP arguments, scaffold files). Generates `.vscode/mcp.json` and minimal `copilot-instructions.md`. Low complexity.
4. **NuGet publish** (#25) — CI job to push the `.nupkg` to NuGet.org. Prerequisite: all of the above working. Infra work only.
