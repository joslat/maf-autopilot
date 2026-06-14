# Next steps

> **Last refreshed:** 2026-05-13 (status check #4 — Phase W mostly done) — **8 phases landed**: ✅ Phase Q (auto-update), ✅ Phase S (multi-target nupkg + net8 Dockerfile), ✅ Phase T (1.3 sample + workshop + registry drift fixes), ✅ Phase R (Dependabot — 5 merged), ✅ Phase W.A (foundation + 3 samples 1.0/1.2/1.3 + CI regression), ✅ Phase W.A cleanups (registry chronology + compat-matrix pins), ✅ Phase W.B (4 workshop-polish items: analyzer in sample + retrospective + find-the-bug + animated cast scripts), ✅ Phase W.C mostly done (W.6 MafAutoFix + W.7 MafBeforeAfter + W.13 MafHealthBadge subcommand + W.14 snupkg+SourceLink). Test surface: **524 unique tests × 3 TFMs = 1572 executions, all green.** Phase W remainders: 🟡 W.11 MafScoreMigrationRisk, 🟡 W.12 MafGenerateRegressionPlan. Path to 1.0: 🟡 [Phase X.1 + X.2](#phase-x--user-checkpoints) (run workshop, cut 1.3.0 — needs you).
> **Single source of truth for "what's next."** When this contradicts other docs, this wins.
> **For history of done work:** see the **[`Done`](#-done--phases-a-through-q-history) section at the bottom** of this file, plus the full archive in [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md).

---

## Index

1. [Current state](#current-state)
2. [My recommendations — at a glance](#my-recommendations--at-a-glance)
3. [Active backlog — phased plan](#active-backlog--phased-plan)
   - ✅ [Phase R — Dependabot triage + finishing touches](#phase-r--dependabot-triage--finishing-touches--done-2026-05-13)
   - ✅ [Phase S — Multi-target the nupkg](#phase-s--multi-target-the-nupkg-matching-agenteval-house-style--landed-2026-05-13)
   - 🟡 [Phase T — MAF 1.3 sample + migration dogfood](#phase-t--maf-13-sample--migration-dogfood-the-a8-unblocker) (T.1-T.2.5+T.6 ✅; T.3-T.5 + T.7 → Phase X)
   - 🟡 [Phase W — Claude-deliverable post-1.0 items](#phase-w--claude-deliverable-post-10-items) (13 items, full implementation detail)
   - 🟡 [Phase X — User checkpoints](#phase-x--user-checkpoints) (7 items requiring you)
   - 🟡 [Phase U — 1.0 polish (post-A.8)](#phase-u--10-polish-post-a8)
4. [In-depth recommendations (my reasoning)](#in-depth-recommendations-my-reasoning)
   - [TFM strategy](#tfm-strategy)
   - [Dependabot triage](#dependabot-triage)
   - [MAF 1.3 sample as the A.8 unblocker](#maf-13-sample-as-the-a8-unblocker)
   - [Analyzer test infra (Roslyn 4→5)](#analyzer-test-infra-roslyn-4--5)
   - [Agents audit](#agents-audit)
5. [Pre-1.0 ship gate](#pre-10-ship-gate)
6. [Acceptance criteria for 1.0 announcement](#acceptance-criteria-for-10-announcement)
7. [Deferred — with rationale](#deferred--with-rationale)
8. [Out-of-scope](#out-of-scope)
9. [Annex — May-6 pre-Phase-O sketch](#annex--may-6-pre-phase-o-sketch)
10. [✅ Done — Phases A through Q (history)](#-done--phases-a-through-q-history)

---

## Current state

The toolkit ships **20 MCP tools, 7 agents (including `@maf` primary), 13 skills, 3 always-loaded instructions, 3 Roslyn analyzers (separate NuGet `maf-autopilot.Analyzers`), 5 MCP resources, 7 MCP prompts, multi-arch Docker container, and 6 GitHub Actions workflows** (release, release-watcher, ai-fill-todos, pr-audit, drift-detector, docker-publish). Test suite: **474 passing** (463 main + 11 analyzer), 0 warnings, 0 errors. **`maf-autopilot 1.3.0-alpha-5`** is published to NuGet (current latest).

**The auto-update loop is end-to-end validated** against real MAF 1.4 + 1.5 data. PR #15 (1.4 fills by Copilot Coding Agent) merged. PR for 1.5 (issue #16) in flight at time of writing.

**Current TFMs:** `maf-autopilot` + tests **multi-target `net8.0;net9.0;net10.0`** (Phase S, 2026-05-13). Analyzer on **`netstandard2.0`** (Roslyn requirement — locked). Central package management at `/Directory.Packages.props`; SDK pin at `/global.json` (`8.0.100` + `rollForward: latestMajor`).

**Pre-1.0 gate:** A.8 — "one external migration validates the toolkit end-to-end against a real customer codebase." Currently the only thing blocking the stable cut. **Phase T below proposes a way to unblock this without waiting for an external customer.**

---

## My recommendations — at a glance

The original 3 recommendations are all **done** ✅:

1. ✅ **Multi-target the nupkg `net8.0;net9.0;net10.0` matching AgentEval's house style** — landed in Phase S (Directory.Packages.props + Directory.Build.props + global.json all in place; Dockerfile now on net8.0 LTS too). See [NuGet packaging alignment](#nuget-packaging-alignment-with-agenteval).
2. ✅ **Build a MAF 1.3 sample inside `samples/maf-1.3-sample/`** — landed in Phase T.1+T.2 (10 anti-pattern errors + 3 silent-starvation risks correctly detected by the toolkit). Workshop authored in T.2.5.
3. ✅ **Triage Dependabot PRs surgically** — landed in Phase R (5 PRs merged, 2 deferred, 2 closed).

**Current recommendations going forward:**

1. 🟡 **Run the workshop end-to-end ([X.1](#x1--run-the-workshop-end-to-end-the-a8-unblocker))** — drives `@maf-auditor` + `@maf-migration` in Copilot Chat. Produces the A.8 evidence. Needs you (~50 min).
2. 🟡 **Cut stable 1.3.0 ([X.2](#x2--cut-stable-130))** — one-way publish to NuGet + GHCR. Needs you (~10 min).
3. 🟡 **Phase W A — Samples-first execution** — do W.1 (foundation) → W.2 (1.2 sample) → W.3 (1.0 sample) → W.4 (CI regression) **together as one focused batch**. Can be done by Claude autonomously (~1-2 days). See [Phase W execution order](#phase-w--claude-deliverable-post-10-items).

---

## Active backlog — phased plan

### Phase R — Dependabot triage + finishing touches ✅ DONE 2026-05-13

All 7 dependabot PRs triaged. 5 merged, 2 closed (Docker .NET 10 forcers), 2 deferred to Phase U.

| ID | Item | Status |
|---|---|---|
| R.1 | Wait for Copilot's PR for MAF 1.5 (issue #16) and review + merge | ⏳ Independent — Copilot working separately |
| R.2 | Close Dependabot #1 + #2 (Docker .NET 10 forcers — Dockerfile stays net8 LTS) | ✅ Closed by Claude with explanatory comment citing Phase S decision |
| R.3 | Merge Dependabot #11 (Hosting 10.0.7) | ✅ Auto-resolved — `Microsoft.Extensions.Hosting 10.0.7` was pinned in `Directory.Packages.props` during Phase S |
| R.4 | Merge low-risk Action bumps #3 (docker/setup-buildx-action 3→4) + #4 (docker/login-action 3→4) | ✅ Both squash-merged by Claude |
| R.5 | Verify + merge mid-risk Actions: #5 (upload-artifact 4→7), #6 (softprops/action-gh-release 2.2.1→3.0.0), #7 (actions/checkout 4→6) | ✅ All three squash-merged by Claude after diff verification |
| R.6 | Merge `Microsoft.NET.Test.Sdk` bump | ✅ Auto-resolved — no PR currently open (Test.Sdk is at the intended version in `Directory.Packages.props`) |
| R.7 | Defer to Phase U: #8 (coverlet 6→10), #17 (Microsoft.CodeAnalysis 4.11→5.3) | 🟡 Comments left on both PRs; tracked in Phase U / Phase X |

### Phase S — Multi-target the nupkg (matching AgentEval house style) ✅ LANDED 2026-05-13

**Verdict from cross-checking AgentEval (your sibling project):** multi-target `net8.0;net9.0;net10.0`, not single-target. Reasoning in [TFM strategy](#tfm-strategy) below.

| ID | Item | Status |
|---|---|---|
| S.1 | Add `Directory.Packages.props` at repo root (mirror AgentEval) with `ManagePackageVersionsCentrally=true` + `CentralPackageTransitivePinningEnabled=true`. Move all `Version=` pins from the 4 csprojs into it. | ✅ |
| S.2 | Add `global.json` at repo root with `sdk.version=8.0.100` + `rollForward: latestMajor` + `allowPrerelease: true` (mirror AgentEval). | ✅ |
| S.3 | Add `Directory.Build.props` with shared compiler settings (`LangVersion=latest`, `ImplicitUsings=enable`, `Nullable=enable`, `TreatWarningsAsErrors=false`). | ✅ |
| S.4 | `src/maf-autopilot/maf-autopilot.csproj`: `<TargetFramework>net9.0</TargetFramework>` → `<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>`. Removed inline `Version=` from all package refs. | ✅ |
| S.5 | `src/maf-autopilot.Tests/maf-autopilot.Tests.csproj`: same TFM change. | ✅ |
| S.6 | `src/maf-autopilot.Analyzers.Tests/maf-autopilot.Analyzers.Tests.csproj`: same TFM change. | ✅ |
| S.7 | `src/maf-autopilot.Analyzers/maf-autopilot.Analyzers.csproj`: kept `netstandard2.0` (Roslyn requirement). Removed inline `Version=`. | ✅ |
| S.8 | Bumped `Microsoft.Extensions.Hosting` from `9.0.0` → `10.0.7` in `Directory.Packages.props`. The 10.0.x line internally ships net8 + net9 + net10 TFMs in one nupkg. (Equivalent to merging Dependabot #11.) | ✅ |
| S.9 | Dockerfile bumped to `mcr.microsoft.com/dotnet/sdk:8.0` + `runtime:8.0` (net8.0 LTS — smallest LTS image, longest support window, matches the lowest TFM the nupkg supports). Container ships one TFM regardless of the multi-target nupkg, and net8 is the right choice. | ✅ DONE 2026-05-13 (status check #2) |
| S.10 | Build + test suite run locally across all 3 TFMs: `dotnet build` (3.69 s for 4 projects + 4 TFMs) + `dotnet test` — **463 tests × 3 TFMs = 1389 main-test executions + 11 × 3 = 33 analyzer-test executions, all passing** (one pre-existing `CompatibilityToolTests` failure for missing 1.4/1.5 rows fixed in the same change). | ✅ |
| S.11 | CI workflow `release.yml` updated to install all 3 runtimes (`dotnet-version: 8.0.x \| 9.0.x \| 10.0.x`). Other install-only workflows (pr-audit, drift-detector, release-watcher) dropped to `8.0.x` LTS. **Next:** cut `1.3.0-alpha-6` and verify the published `nupkg`'s `tools/` folder contains `net8.0`, `net9.0`, `net10.0` subdirectories. | 🟡 Pending alpha-6 release |

### Phase T — MAF 1.3 sample + migration dogfood (the A.8 unblocker)

Currently A.8 says "wait for an external customer migration." That's an indefinite wait. **A synthetic-but-real sample is the same validation in 1-2 days of work I can do autonomously.**

| ID | Item | Owner | Effort | What it proves |
|---|---|---|---|---|
| T.1 | Create `samples/maf-1.3-sample/MafSample.FraudClaims.csproj` — pinned to MAF 1.3.0, target `net8.0`, builds clean (0 errors, 1 expected CS0618 warning). | ✅ |
| T.2 | Hand-authored the FraudClaimsTriage workflow (intake → fan-out 3 investigators → fan-in aggregator → decision → notification). **10 anti-pattern errors + 3 warnings + 3 silent-starvation risks** detected by `maf-autopilot doctor`. Validated coverage: `MAF-AP-SEC-001/002/003`, `MAF-AP-OBS-001`, `MAF-AP-CONC-001/002`, `MAF-AP-WF-001`, `MAF130-EXEC-001` / `MAF001` × 3, `MAF130-FAN-IN-001`, `MAF130-MIDDLEWARE-001`. Style inspired by `AgentEval.TravelDemo`. | ✅ |
| T.2.5 | Authored [`samples/workshop.md`](../samples/workshop.md) — a ~50-min hands-on walkthrough: open the sample standalone in VS Code Insiders, wire `.vscode/mcp.json` to the global `maf-autopilot` tool, ask Copilot Chat to drive `MafDoctor` → `MafScanAntiPatterns` → `MafValidateFanOut` → `MafSimulateWorkflow` → `MafRunCs0618Hunt`, then `@maf-auditor` → `@maf-migration` → verify. Updated the sample README with an ASCII WARNING banner: **intentional anti-pattern fixture**. | ✅ |
| T.3 | Run `@maf-auditor` against the sample to generate `samples/maf-1.3-sample/docs/migration-plan.md` (covered by workshop Step 6) | 🟡 Next — YOU run in Copilot Chat |
| T.4 | Run `@maf-migration` against the plan, task-by-task, with `dotnet build` gates (workshop Step 7) | 🟡 |
| T.5 | After T.4, sample should reach grade A/B with the deliberate patterns fixed (workshop Step 8) | 🟡 |
| T.6 | **Registry drift fixes landed.** `registry.yaml` updated for `MAF130-SESSION-001` (added `applies_to_codebases: pre-1.3.0`, rewrote `fix_description` + `example_before`/`example_after` for the AgentThread→AgentSession reality, expanded `notes` with the Phase T correction), `MAF130-INSTRUCTIONS-001` (same reframing — top-level property is REMOVED on 1.3.0, not silently ignored), `MAF130-MIDDLEWARE-001` (corrected examples to use positional `.Use(<run>, <stream>)` — named params don't match the real `ChatClientBuilder.Use` surface). `cs_warning` + `dotnet_inspect_detectable` fields preserved so the existing `Cs0618HuntTool.MatchToRegistry` tests still pass; all 1422 test executions green. | ✅ |
| T.7 | **Cut stable `1.3.0` to NuGet** (A.8 unblocked by T) | ⏳ Gated on T.4-T.5 |

**Validation of T.1+T.2 (2026-05-13):** `dotnet run --project src/maf-autopilot --framework net10.0 -- doctor samples/maf-1.3-sample` reports grade **F** with 10 anti-pattern errors + 3 silent-starvation risks correctly detected. The toolkit finds the deliberate-anti-patterns end-to-end. This is the core Phase T proof-of-concept.

---

#### 🎯 What I recommend next (ordered)

The previous "(1) fix registry drift" item is **done** — landed in the same session. What remains in the path to 1.0:

1. **(50 min — must be human) Run the workshop end-to-end (T.3 → T.4 → T.5).** Follow [`samples/workshop.md`](../samples/workshop.md). This drives `@maf-auditor` → `@maf-migration` via Copilot Chat in VS Code Insiders — the agents themselves are a GitHub Copilot client feature, not something Claude Code can invoke. The migration log + final `MafDoctor` grade A/B IS the A.8 evidence to point at when announcing 1.0.

2. **(10 min — human-driven publish) Cut stable `1.3.0` (T.7 → A.8 unblocked).** This is the **release cut**: `gh workflow run release.yml -f version=1.3.0` triggers the GitHub Actions release workflow which (a) runs `dotnet test` across all 3 TFMs, (b) `dotnet pack`s the multi-target nupkg + the analyzer nupkg, (c) pushes both to `nuget.org` with the `NUGET_API_KEY` secret, (d) builds + pushes the multi-arch Docker image to `ghcr.io/joslat/maf-doctor:1.3.0`, (e) creates a tagged GitHub Release with auto-generated release notes. **"Announces" means: once `1.3.0` (without `-alpha-N`) is live on nuget.org, anyone running `dotnet tool install -g maf-autopilot` (no `--prerelease` flag needed) gets it.** That's the implicit announcement. An optional explicit announcement step would be a discussions post / blog / social — none required for the cut itself. This is a one-way publish to a public registry, so it stays human-driven.

3. **(done by Claude Code, 2026-05-13)** Phase R Dependabot triage:
   - ✅ Closed #1, #2 (Docker .NET 10 forcers — Dockerfile stays net8 LTS)
   - ✅ #11 auto-resolved by Phase S central pinning at `Microsoft.Extensions.Hosting 10.0.7`
   - ✅ Merged #3, #4 (docker/setup-buildx + login 3→4 — single-line bumps, scoped to docker-publish.yml)
   - ✅ Merged #6 (softprops/action-gh-release 2.2.1→3.0.0 — SHA + comment both updated correctly by Dependabot)
   - ✅ Merged #5 (actions/upload-artifact 4→7 — verified our 2 usages pass unique artifact names so the v5 uniqueness break doesn't bite)
   - ✅ Merged #7 (actions/checkout 4→6 — pure version bump, no `with:` changes in 6 workflows)
   - 🟡 Left open with deferred-to-Phase-U comments: #8 (coverlet.collector 6→10 — 4 majors, test infra), #17 (Microsoft.CodeAnalysis.* 4.11→5.3 — Roslyn SDK major, defer until Phase T.5 workshop validation completes)

Wall-clock from here to 1.0 publishable: **~60 min of human work** (steps 1 + 2). Dependabot triage done.

---

### Phase W — Claude-deliverable post-1.0 items

These are items I (Claude Code or a peer like Sonnet 4.6) can deliver **without human intervention**. Each item below has a self-contained implementation subsection with goal, files to touch, algorithm, tests, acceptance criteria, and pitfalls.

**Reordered for samples-first execution.** The natural execution sequence groups into three sub-phases. Implementation-detail subsections keep their existing W.N IDs for stability — the "Step" column gives the recommended order.

#### Phase W.A — Foundation + samples-all-at-once ✅ DONE 2026-05-13

All 4 items landed. Phase W.A delivered:
- Typed `applies_to_codebases` field in `RegistryModels.cs` + `GetEntriesForCodebaseVersion` filter helper.
- 3 version-parity samples in `samples/maf-{1.0,1.2,1.3}-sample/`, each pinned to its MAF version, each builds clean, each `doctor`-grades **F** with 10/3/3 anti-patterns/warnings/silent-starvation risks (identical detection across 1.0 / 1.2 / 1.3).
- CI regression test (`src/maf-autopilot.Tests/SampleRegressionTests.cs`) + per-sample `.expected-doctor.yaml` files locking the doctor verdicts.

| Step | ID | Item | Effort | Status |
|---|---|---|---|---|
| 1 | [**W.1**](#w1--formalise-applies_to_codebases-in-registrymodelscs) | Formalise `applies_to_codebases` in `RegistryModels.cs` + filter helper | S (1-2 hrs) | ✅ DONE 2026-05-13 |
| 2 | [**W.8**](#w8--samplesmaf-12-sample) | `samples/maf-1.2-sample/` — MAF 1.2.0 version-parity fixture (doctor: F, 10/3/3) | M | ✅ DONE 2026-05-13 |
| 3 | [**W.10**](#w10--samplesmaf-10-sample) | `samples/maf-1.0-sample/` — MAF 1.0.0 version-parity fixture (doctor: F, 10/3/3) | M | ✅ DONE 2026-05-13 |
| 4 | [**W.9**](#w9--ci-regression-on-samples) | CI regression test class + per-sample `.expected-doctor.yaml` — 6 tests × 3 TFMs = 18 new test executions | S | ✅ DONE 2026-05-13 |

**Phase W.A side findings** (both ✅ FIXED 2026-05-13 — full detail in per-sample READMEs):

- ✅ **Registry chronology gap — FIXED.** Verified via `dotnet-inspect` that `AgentThread`, `MapA2A`, `RegisterA2AAgent`, `InProcessExecution`, `AgentRunUpdateEvent`, `[StreamsMessage]`, `[YieldsMessage]` do NOT appear in any public MAF NuGet package (checked 1.0.0/1.1.0/1.2.0/1.3.0 main + `Microsoft.Agents.AI.DevUI`/`.Hosting` preview-channel packages 1.0–1.5). The registry inherited these patterns from pre-public-release / SemanticKernel-Process-era MAF code. **Fix applied:** added `applies_to_codebases: "pre-1.0.0"` markers + Phase W.A correction paragraphs to 7 affected entries (`MAF130-THREAD-001`, `MAF130-A2A-001/002`, `MAF130-STREAM-001`, `MAF130-EVENT-001`, `MAF130-ATTR-001/002`). Entries kept for historical documentation but will not fire on any modern customer codebase pinned to MAF 1.0+.
- ✅ **Compat-matrix corrections — FIXED.** `docs/compatibility-matrix.md` + `src/maf-autopilot/Tools/CompatibilityTool.cs` (static `Matrix`) both updated in lockstep. MAF 1.0.0 / 1.1.0 / 1.2.0 rows now show their actual transitive minimums: `Microsoft.Extensions.AI 10.5.0` + `Azure.AI.OpenAI 2.8.0-beta.1`. The "Removed Packages by Version" table also corrected — DevUI + Hosting are NOT removed; they're still on NuGet as preview-only channels (latest `1.5.0-preview.260507.1`). 2 drift tests updated to assert the new (corrected) content; all 1464 test executions green.

#### Phase W.B — Workshop polish ✅ DONE 2026-05-13

| Step | ID | Item | Effort | Status |
|---|---|---|---|---|
| 5 | [**W.2**](#w2--wire-analyzer-nuget-into-the-13-sample) | Wired `maf-autopilot.Analyzers 1.3.0-alpha-1` into the 1.3 sample csproj + added `.editorconfig` demoting MAF001/2/3 to warnings + workshop Step 5b explains the squigglies | XS | ✅ |
| 6 | [**W.3**](#w3--workshop-retrospective-step) | Added workshop Step 9 (retrospective via `@maf-best-practice-reviewer`) + Step 10 (MafAutoFix demo) | XS | ✅ |
| 7 | [**W.5**](#w5--find-the-bug-in-30-seconds-workshop-challenge) | `samples/find-the-bug/` with 3 subtle-bug snippets + `README.md` (facilitator script) + `answer-key.md`. Doctor verified: grade C, 2/3 bugs detected by AST scanners (the FAN-IN bug needs `MafRunCs0618Hunt` against a real csproj, documented in the key). | S | ✅ |
| 8 | [**W.4**](#w4--animated-install-cast-source-artifacts-only) | `docs/assets/install-cast.tape` (4-beat vhs script) + `scripts/make-cast.{sh,ps1}` wrappers + README embed snippet. Rendering itself is Phase X.3. | M | ✅ (source artifacts) |

#### Phase W.C — New MCP tools (5 items + 1 polish, ~3-4 days)

Tool-surface expansion. **W.6 (MafAutoFix) is the biggest leverage** — the missing "do" half of the toolkit.

| Step | ID | Item | Effort | Depends on | Status |
|---|---|---|---|---|---|
| 9 | [**W.6**](#w6--mafautofix-mcp-tool-the-missing-do-half) | `MafAutoFix` MCP tool (THE missing "do" half) | M (1-2 days) | W.1 | ✅ DONE 2026-05-13 |
| 10 | [**W.7**](#w7--mafbeforeafter-mcp-tool) | `MafBeforeAfter(repoPath, ruleIds[])` MCP tool — unified-diff preview of every change a rule-set would make; uses W.6 rewriters in dry-run; 7 tests | S (3-4 hrs) | W.6 | ✅ DONE 2026-05-13 |
| 11 | [**W.11**](#w11--mafscoremigrationriskrepopath-mcp-tool) | `MafScoreMigrationRisk(repoPath)` MCP tool — weighted-score → HARD/MEDIUM/EASY verdict + per-category breakdown + recommended approach; 6 tests | S | ✅ DONE 2026-05-13 |
| 12 | [**W.12**](#w12--mafgenerateregressionplanfrom-to-mcp-tool) | `MafGenerateRegressionPlan(from, to)` MCP tool — Mermaid flowchart roadmap across MAF versions + per-step registry-entry breakdown + execution instructions; 11 tests | M (1 day) | W.1, W.8, W.10 | ✅ DONE 2026-05-13 |
| 13 | [**W.13**](#w13--mafhealthbadge-subcommand) | `maf-autopilot badge` CLI subcommand emits shields.io endpoint-badge JSON; 11 tests (theory + integration against 1.3 sample) | XS | ✅ DONE 2026-05-13 |
| 14 | **W.14** | NuGet polish — added `IncludeSymbols=true` + `SymbolPackageFormat=snupkg` + `Microsoft.SourceLink.GitHub` to BOTH the main nupkg and the analyzer nupkg. AgentEval-parity achieved. | XS | ✅ DONE 2026-05-13 |

**Why this order (instead of the original V.x order):**

- **W.A samples first** triples MafAutoFix's test surface — when W.6 lands, it has 3 samples to validate against instead of 1.
- **W.4 (CI regression) lands inside W.A** because once we have ≥2 samples, the regression test is meaningful. Doing it later wastes the early signal.
- **Workshop polish (W.B) is mostly doc + small csproj edits** — perfect quick wins between heavyweight sample work and the MCP tool buildout.
- **MCP tools (W.C) come last** because they're the largest individual lift; doing them with a polished workshop + 3 samples behind them means every new tool can be demoed against multiple real codebases on day 1.

**Pick-4 for 1.1.x release (recommended bundle):** W.1 + W.8 + W.10 + W.9 (Phase W.A end-to-end) — about 2 days, leaves the toolkit with full multi-version sample coverage + CI regression net. Then ship 1.1, queue W.B for 1.1.1 and W.C for 1.2.

---

#### W.1 — Formalise `applies_to_codebases` in `RegistryModels.cs`

**Goal.** Make the YAML field that Phase T introduced (`applies_to_codebases: "pre-1.3.0"` on `MAF130-SESSION-001` and `MAF130-INSTRUCTIONS-001`) into a typed, queryable property on `RegistryEntry`.

**Dependencies.** None. Foundation for W.6 and W.12.

**Effort.** 1-2 hours.

**Files to modify.**
- `src/maf-autopilot/Data/RegistryModels.cs` — add the new property to the `RegistryEntry` record.
- `src/maf-autopilot/Data/RegistryService.cs` — extend `BuildHaystack` if you want the field searchable (probably yes); add a helper `GetEntriesForCodebaseVersion(string version)`.
- `src/maf-autopilot.Tests/RegistryServiceTests.cs` — round-trip + filter tests.

**Algorithm.**

1. In `RegistryEntry`:
   ```csharp
   public sealed record RegistryEntry
   {
       // ... existing fields ...
       [YamlMember(Alias = "applies_to_codebases", ApplyNamingConventions = false)]
       public string? AppliesToCodebases { get; init; }
   }
   ```
2. YamlDotNet deserialisers ignore unknown fields by default — verify with `IgnoreUnmatchedProperties()` is set in `RegistryService`. The 2 existing entries with this field load cleanly.
3. Add `public IEnumerable<RegistryEntry> GetEntriesForCodebaseVersion(string version)` on `RegistryService`. v1 logic: parse `AppliesToCodebases` as one of `null` / `"pre-X.Y.Z"` / `"X.Y.Z+"` / `"X.Y.Z-only"`. Use `System.Version.Parse` for comparison.
4. Add a Markdown rendering hook in `RegistryService.FormatEntryAsMarkdown` to surface the field when non-null.

**Tests.**
- `RegistryEntry_WithAppliesToCodebases_RoundTrips` — write the YAML, parse it back, assert `AppliesToCodebases == "pre-1.3.0"`.
- `RegistryEntry_WithoutAppliesToCodebases_LoadsAsNull` — backward compat.
- `RegistryService_GetEntriesForCodebaseVersion_FiltersByPreVersion` — `pre-1.3.0` entries apply to a 1.2.x codebase but NOT a 1.3.x codebase.
- `RegistryService_GetEntriesForCodebaseVersion_NullAppliesToAny` — entries without the field apply to all versions.

**Acceptance.**
- `dotnet build maf-autopilot.sln -c Release` — 0 errors, 0 warnings.
- `dotnet test maf-autopilot.sln` — all 1422 existing executions still pass, plus 4 new tests pass on every TFM.
- The 2 existing `applies_to_codebases: "pre-1.3.0"` entries in `registry.yaml` parse correctly.

**Pitfalls.**
- Don't bake in a strict semver enum; keep it as a typed string for v1, parse on demand. Premature schema rigidity blocks rapid registry iteration.
- YamlDotNet's case-sensitivity: use `[YamlMember(Alias = "applies_to_codebases")]` explicitly — the camelCase auto-convention will NOT match snake_case YAML keys.
- The matcher `Cs0618HuntTool.MatchToRegistry` uses `RegistryService.SearchByApiName` which scans haystacks. Decide whether to include `AppliesToCodebases` in the haystack — likely NO (the string "pre-1.3.0" isn't an API name).

---

#### W.2 — Wire analyzer NuGet into the 1.3 sample

**Goal.** Demonstrate write-time enforcement in the workshop. After `dotnet add package maf-autopilot.Analyzers --prerelease`, VS Code shows squiggly red lines on the sample's broken code via `MAF001`/`MAF002`/`MAF003`.

**Dependencies.** None.

**Effort.** 30 minutes.

**Files to modify.**
- `samples/maf-1.3-sample/MafSample.FraudClaims.csproj` — add `<PackageReference Include="maf-autopilot.Analyzers" Version="1.3.0-alpha-1">` with `<PrivateAssets>all</PrivateAssets>` and `<IncludeAssets>analyzers; build</IncludeAssets>`.
- `samples/workshop.md` — insert "Step 5b — Watch write-time enforcement" between Step 5 and Step 6.

**Algorithm.**

1. Update the csproj:
   ```xml
   <ItemGroup>
     <PackageReference Include="maf-autopilot.Analyzers" Version="1.3.0-alpha-1">
       <IncludeAssets>analyzers; build</IncludeAssets>
       <PrivateAssets>all</PrivateAssets>
     </PackageReference>
   </ItemGroup>
   ```
2. Run `dotnet restore` + `dotnet build` from the sample folder; expect `MAF001`/`MAF002`/`MAF003` warnings inline. Confirm they reference the correct files/lines:
   - `MAF001` — fan-out non-`Task<T>` return — fires on the 3 investigators.
   - `MAF002` — `DefaultAzureCredential` — fires in `ChatClientFactory.cs`.
   - `MAF003` — `EnableSensitiveData=true` — fires in `ChatClientFactory.cs`.
3. Add Step 5b to `samples/workshop.md`:
   ```markdown
   ## Step 5b — Watch the analyzer fire at write-time

   Add the analyzer to the project:
       dotnet add package maf-autopilot.Analyzers --prerelease

   Open Executors/OsintInvestigator.cs in VS Code. You should see a
   red squiggle under the `ValueTask` return type with a hover
   message: "MAF001: fan-out handler must return Task<T>".

   That's the shift-left half of the toolkit — the same rules the
   doctor runs at scan-time fire at WRITE time inside the editor.
   ```

**Tests.** None added (the sample is itself the fixture).

**Acceptance.**
- `dotnet build samples/maf-1.3-sample/` reports MAF001 + MAF002 + MAF003 warnings.
- Sample still compiles (these are warnings, not errors).
- Workshop Step 5b reads cleanly between Step 5 and Step 6.

**Pitfalls.**
- The analyzer version pin must match a published prerelease. As of 2026-05-13, `maf-autopilot.Analyzers 1.3.0-alpha-1` is the latest published version. If unreleased, use a `<ProjectReference>` instead (relative path).
- `<PrivateAssets>all</PrivateAssets>` prevents the analyzer from leaking to downstream consumers — critical for samples that might one day get packaged.
- VS Code may need a reload for new analyzers to show; the build CLI will surface the warnings either way.

---

#### W.3 — Workshop retrospective step

**Goal.** Demonstrate the multi-agent surface by having `@maf-best-practice-reviewer` grade the post-migration result.

**Dependencies.** None (pure doc edit).

**Effort.** 15 minutes.

**Files to modify.**
- `samples/workshop.md` — add Step 9 between current Step 8 (verify) and the Cleanup/Reset section.

**Algorithm.** Insert this Markdown block after Step 8:

```markdown
## Step 9 — Migration retrospective (multi-agent surface demo)

After Step 8 confirms grade A/B, ask the second-opinion agent:

    @maf-best-practice-reviewer review the migrated codebase and tell me what's still suboptimal

Expected: the reviewer flags `good-but-could-be-better` patterns the
registry doesn't cover (e.g., executor IDs are too generic, agents
lack explicit token caps, OpenTelemetry traces missing custom tags).
This demonstrates the multi-agent surface — the migration agent
fixed the registry-driven issues; the reviewer catches the next
tier of polish.

> Workshop bonus: ask the reviewer for THREE concrete improvements,
> pick one, and have @maf-migration apply it. That's a complete
> "improvement loop" demonstration in 5 minutes.
```

**Tests.** None.

**Acceptance.**
- `samples/workshop.md` now has 9 numbered steps + cleanup.
- The retrospective wording references the right agent name (`@maf-best-practice-reviewer`).

**Pitfalls.**
- Keep this step short — total workshop time is already ~50 minutes; Step 9 should add at most 5 minutes.
- The `@maf-best-practice-reviewer` agent's persona is in `.github/agents/maf-best-practice-reviewer.agent.md`; verify it routes to the right tool set.

---

#### W.4 — Animated install cast (source artifacts only)

**Goal.** Commit a vhs `.tape` script + render helpers so a maintainer (or CI) can produce `docs/assets/install-cast.gif`. The README will embed the GIF between the existing ASCII art header.

**Dependencies.** None.

**Effort.** 4 hours for source artifacts. Rendering itself is **Phase X.3** (human-driven, needs vhs/ffmpeg/ttyd locally).

**Files to create.**
- `docs/assets/install-cast.tape` — vhs declarative script.
- `scripts/make-cast.sh` — bash wrapper around `vhs`.
- `scripts/make-cast.ps1` — PowerShell equivalent.
- `docs/assets/.gitkeep` — placeholder so the directory exists pre-render.

**Files to modify.**
- `README.md` — add `<p align="center"><img src="docs/assets/install-cast.gif"></p>` between the existing ASCII art and the TOC, with a fallback note "GIF not yet rendered; see `scripts/make-cast.sh` to produce it locally."
- `docs/architecture.md` — note that the cast is rendered from `.tape` + commit pattern.

**Algorithm.**

1. Write `docs/assets/install-cast.tape`:
   ```
   Output docs/assets/install-cast.gif

   Set FontSize 16
   Set Width 1200
   Set Height 700
   Set Theme "Dracula"
   Set Padding 20
   Set TypingSpeed 50ms

   Type "# Install maf-autopilot — global .NET tool"
   Enter
   Sleep 800ms

   Type "dotnet tool install -g maf-autopilot --prerelease"
   Sleep 300ms; Enter; Sleep 3s

   Type "maf-autopilot --version"
   Sleep 200ms; Enter; Sleep 1500ms

   Type "# Audit any MAF codebase — grade A through F"
   Enter; Sleep 800ms

   Type "cd samples/maf-1.3-sample && maf-autopilot doctor ."
   Sleep 200ms; Enter; Sleep 4s

   Type "# 🔴 Grade F — toolkit found every deliberate anti-pattern"
   Enter; Sleep 2s
   ```

2. Write `scripts/make-cast.sh`:
   ```bash
   #!/usr/bin/env bash
   # Render docs/assets/install-cast.gif via vhs.
   # Requires: vhs (brew install vhs / winget install charmbracelet.vhs)
   set -euo pipefail
   if ! command -v vhs >/dev/null 2>&1; then
     echo "vhs not on PATH. Install via:"
     echo "  brew install vhs ffmpeg ttyd       # macOS"
     echo "  winget install charmbracelet.vhs   # Windows"
     exit 2
   fi
   vhs docs/assets/install-cast.tape
   echo "✓ rendered docs/assets/install-cast.gif"
   ```

3. `scripts/make-cast.ps1` — same logic in PowerShell with `Get-Command vhs -ErrorAction SilentlyContinue`.

4. README embed snippet (between the existing ASCII art at top of README.md):
   ```html
   <p align="center">
     <img src="docs/assets/install-cast.gif"
          alt="maf-autopilot — install + audit in 30 seconds" />
   </p>
   ```

**Tests.** None.

**Acceptance.**
- `.tape` file is well-formed (vhs parses it without error — verified locally by anyone with vhs installed).
- `scripts/make-cast.sh` is executable + has the install-instruction fallback.
- README has the embed; the missing GIF degrades gracefully (alt text).

**Pitfalls.**
- The cast's commands must actually work — `samples/maf-1.3-sample/` must exist (it does, ✅ Phase T.1).
- The output GIF can be 500KB-3MB; for repo size discipline, prefer `Output install-cast.webm` (~10× smaller) and embed via `<video>` instead of `<img>`. README compatibility on GitHub: both work.
- Don't commit a half-baked GIF. Either Phase X.3 renders it, or it stays missing with the alt text as graceful degradation.

---

#### W.5 — "Find the bug in 30 seconds" workshop challenge

**Goal.** A pre-toolkit teaser that sells the value proposition in 30 seconds. Show 3 side-by-side ~20-line C# snippets, each containing one subtle MAF anti-pattern. Attendees have 30 seconds to spot the bugs. Then the toolkit finds them all in <1 second.

**Dependencies.** None.

**Effort.** 2-4 hours.

**Files to create.**
- `samples/find-the-bug/README.md` — challenge framing.
- `samples/find-the-bug/snippet-1.cs` — top-level `Instructions` silently-ignored pattern (with a `ChatClientAgentOptions { Instructions = "..." }` block in a `partial class` so it COMPILES on .NET 8 even without MAF — the file isn't actually built, it's read).
- `samples/find-the-bug/snippet-2.cs` — fan-out `[MessageHandler]` returning `ValueTask` (not `ValueTask<T>`).
- `samples/find-the-bug/snippet-3.cs` — `AddFanInBarrierEdge(target, sources)` obsolete arg order.
- `samples/find-the-bug/answer-key.md` — per-bug explanation + the maf-autopilot rule ID + the verdict the toolkit produces.

**Files to modify.**
- `samples/workshop.md` — add a "Step 0 — Find the bug (icebreaker)" section before Step 1.

**Algorithm.**

1. Each snippet is a STANDALONE `.cs` file (not part of a project). Self-contained — uses `using` statements + plausibly-named types so the reader sees realistic MAF code.
2. Bugs to plant:
   - **Snippet 1 (top-level Instructions):** ~20 lines wrapping a `new ChatClientAgent(client, new ChatClientAgentOptions { Name = "Bot", Instructions = "...", Description = "..." })`. Looks innocuous; the bug is `Instructions` should be nested in `ChatOptions = new() { Instructions = ... }`.
   - **Snippet 2 (fan-out void):** A `public sealed partial class MyExecutor : Executor` with `[MessageHandler] public async ValueTask HandleAsync(ClaimInput claim, IWorkflowContext ctx, CancellationToken ct) { var f = await ComputeAsync(); }`. Looks fine; bug is `ValueTask` should be `ValueTask<InvestigationFinding>`.
   - **Snippet 3 (fan-in arg order):** A few lines of `WorkflowBuilder` usage with `builder.AddFanInBarrierEdge(aggregator, new[] { osint, history })`. Looks symmetric; bug is the obsolete overload is being selected.
3. `samples/find-the-bug/README.md` rules:
   ```markdown
   # Find the bug in 30 seconds

   Open each snippet. You have 30 seconds total. No scrolling, no
   compiling, no Googling. Find the MAF anti-pattern in each file.

   Then run:    maf-autopilot doctor samples/find-the-bug/

   Compare. How many did you catch? How long did the toolkit take?
   ```
4. `answer-key.md`:
   ```markdown
   # Answers

   - snippet-1.cs:  top-level Instructions (MAF130-INSTRUCTIONS-001 — silently ignored)
   - snippet-2.cs:  fan-out ValueTask (MAF001 / MAF130-EXEC-001 — silent fan-in starvation)
   - snippet-3.cs:  obsolete AddFanInBarrierEdge overload (MAF130-FAN-IN-001 — CS0618)

   Toolkit verdict: 3 errors + 2 silent-starvation risks in 850ms.
   ```

**Tests.** None.

**Acceptance.**
- 3 snippets each have exactly 1 subtle bug + no other bugs.
- Each bug fires a known maf-autopilot rule.
- `samples/find-the-bug/README.md` is workshop-ready.

**Pitfalls.**
- Don't make the bugs obvious. Add ~15 lines of plausible context around each. Match real MAF idioms.
- Avoid syntax errors — the snippets must LOOK like valid code at a glance even though they aren't compiled.
- Avoid bugs that are domain-obvious (e.g., wrong variable names). The bugs should require MAF knowledge to spot.

---

#### W.6 — `MafAutoFix` MCP tool (the missing "do" half)

**Goal.** A deterministic per-rule auto-fixer. Decouples detection (what the doctor does) from remediation (what the migration agent does today via LLM). Lets CI bots fix-and-commit the mechanical rules without an LLM in scope.

**Dependencies.** **W.1** (for `applies_to_codebases` filtering — recommended but not strictly required).

**Effort.** 1-2 days.

**Files to create.**
- `src/maf-autopilot/Tools/AutoFixTool.cs` — main MCP tool class.
- `src/maf-autopilot/Tools/Rewriters/IRuleRewriter.cs` — interface.
- `src/maf-autopilot/Tools/Rewriters/DefaultAzureCredentialRewriter.cs` — for `MAF-AP-SEC-001` / `MAF002`.
- `src/maf-autopilot/Tools/Rewriters/EnableSensitiveDataRewriter.cs` — for `MAF-AP-SEC-003` / `MAF003`.
- `src/maf-autopilot/Tools/Rewriters/ExecutorSealedRewriter.cs` — for `MAF-AP-WF-001`.
- `src/maf-autopilot/Tools/Rewriters/FanInArgOrderRewriter.cs` — for `MAF130-FAN-IN-001`.
- `src/maf-autopilot/Tools/Rewriters/SyncOverAsyncRewriter.cs` — for `MAF-AP-CONC-002`.
- `src/maf-autopilot.Tests/AutoFixToolTests.cs` — comprehensive test file (one [Theory] per rewriter + end-to-end).

**Files to modify.**
- `src/maf-autopilot/Tools/DoctorTool.cs` — at the end of the report, add a line: "Want to auto-fix the mechanical findings? Run `MafAutoFix(repoPath, '<ruleId>')`."

**Algorithm.**

```csharp
namespace MafAutopilot.Tools;

[McpServerToolType]
public sealed class AutoFixTool
{
    private readonly Dictionary<string, Func<IRuleRewriter>> _factories = new()
    {
        ["MAF-AP-SEC-001"]    = () => new DefaultAzureCredentialRewriter(),
        ["MAF002"]            = () => new DefaultAzureCredentialRewriter(),
        ["MAF-AP-SEC-003"]    = () => new EnableSensitiveDataRewriter(),
        ["MAF003"]            = () => new EnableSensitiveDataRewriter(),
        ["MAF-AP-WF-001"]     = () => new ExecutorSealedRewriter(),
        ["MAF130-FAN-IN-001"] = () => new FanInArgOrderRewriter(),
        ["MAF-AP-CONC-002"]   = () => new SyncOverAsyncRewriter(),
    };

    [McpServerTool]
    [Description("""
        Deterministically apply the fix for a single registry rule across a codebase.
        Returns a JSON summary of files touched + rewrites applied. Set dryRun=true to
        preview without writing.
        """)]
    public string MafAutoFix(
        [Description("Absolute or relative path to the repo or project to fix.")] string repoPath,
        [Description("Rule ID, e.g. 'MAF-AP-SEC-001' or 'MAF130-FAN-IN-001'.")] string ruleId,
        [Description("Optional: limit fixes to a single file.")] string? specificFile = null,
        [Description("If true, don't write changes — just return what would change.")] bool dryRun = false)
    {
        // 1. PathGuard.Validate(repoPath)  — reject empty / drive-root / outside-allowed-roots
        // 2. if (!_factories.TryGetValue(ruleId, out var factory)) return "{\"error\":\"unknown rule\"}"
        // 3. var rewriter = factory();
        // 4. var files = SourceFileWalker.EnumerateCsFiles(repoPath);
        //    if (specificFile != null) files = files.Where(f => f.EndsWith(specificFile));
        // 5. var fixedFiles = new List<string>();
        // 6. foreach (var file in files) {
        //      var src = File.ReadAllText(file);
        //      var tree = CSharpSyntaxTree.ParseText(src);
        //      var newRoot = rewriter.Visit(tree.GetRoot());
        //      if (newRoot != tree.GetRoot()) {
        //        if (!dryRun) File.WriteAllText(file, newRoot.ToFullString());
        //        fixedFiles.Add(file);
        //      }
        //    }
        // 7. return JsonSerializer.Serialize(new { ruleId, dryRun, fixedFiles, count = fixedFiles.Count });
    }
}

public interface IRuleRewriter
{
    string RuleId { get; }
    SyntaxNode Visit(SyntaxNode root);
}

// Example rewriter
public sealed class DefaultAzureCredentialRewriter : CSharpSyntaxRewriter, IRuleRewriter
{
    public string RuleId => "MAF-AP-SEC-001";

    public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        if (node.Type is IdentifierNameSyntax id && id.Identifier.ValueText == "DefaultAzureCredential")
        {
            // Preserve trivia: leading/trailing whitespace + comments above the line
            var replacement = SyntaxFactory.IdentifierName("ManagedIdentityCredential")
                .WithLeadingTrivia(id.GetLeadingTrivia())
                .WithTrailingTrivia(id.GetTrailingTrivia());
            return node.WithType(replacement);
        }
        return base.VisitObjectCreationExpression(node);
    }
}
```

Same pattern for each rewriter:
- `EnableSensitiveDataRewriter` — find `AssignmentExpressionSyntax` where `.Left.ToString() == "EnableSensitiveData"` and `.Right.ToString() == "true"`. Remove the assignment.
- `ExecutorSealedRewriter` — find `ClassDeclarationSyntax` deriving from `Executor` (substring match on `.BaseList`) and missing the `sealed` modifier. Add `SyntaxFactory.Token(SyntaxKind.SealedKeyword)` to `.Modifiers`.
- `FanInArgOrderRewriter` — find `InvocationExpressionSyntax` where `.Expression.ToString().EndsWith(".AddFanInBarrierEdge")`. Check arg count = 2. If `args[0]` is NOT collection-typed (heuristic: not `new[]` / not `IEnumerable<...>` cast — check `args[0].Expression.Kind()`), swap the two arguments.
- `SyncOverAsyncRewriter` — find `MemberAccessExpressionSyntax` where `.Name.Identifier.ValueText == "Result"` AND parent is invocation result. Replace with `AwaitExpressionSyntax(parent.Expression)`. Also: walk up to the containing method and ensure it has `async` modifier — if not, also add it (return-type rewrite from `T` to `Task<T>` is the cherry on top; for v1 just emit a comment `// TODO: make this method async`).

**Tests.**
- `AutoFixTool_UnknownRule_ReturnsError` — invalid ruleId → JSON error.
- `AutoFixTool_PathInjection_RejectedByPathGuard` — `"/"`, `"C:\"`, empty.
- `DefaultAzureCredentialRewriter_RewritesToManagedIdentity` — input `new DefaultAzureCredential()` → output `new ManagedIdentityCredential()`.
- `DefaultAzureCredentialRewriter_PreservesComments` — leading `// XXX` comment survives.
- `EnableSensitiveDataRewriter_DropsAssignment` — input has `EnableSensitiveData = true,` → output doesn't.
- `ExecutorSealedRewriter_AddsSealedToPartialClass` — input `public partial class X : Executor` → output `public sealed partial class X : Executor`.
- `ExecutorSealedRewriter_NoOpIfAlreadySealed` — already-sealed class unchanged.
- `FanInArgOrderRewriter_SwapsArgs` — input `builder.AddFanInBarrierEdge(target, new[]{a,b})` → output `builder.AddFanInBarrierEdge(new[]{a,b}, target)`.
- `FanInArgOrderRewriter_NoOpIfAlreadyCorrect` — sources-first call unchanged.
- `SyncOverAsyncRewriter_ReplacesResultWithAwait` — input `var x = Foo().Result` → output `var x = await Foo()`.
- `EndToEnd_FixSampleAntiPatterns_ProducesValidCsCode` — apply each rewriter to the 1.3 sample, assert the resulting code compiles via `CSharpSyntaxTree.ParseText` + check for `Diagnostic` errors.

**Acceptance.**
- All 1422 existing test executions pass.
- 11+ new tests pass on every TFM (so 33+ new executions).
- Running `MafAutoFix("samples/maf-1.3-sample", "MAF-AP-SEC-001")` rewrites the `ChatClientFactory.cs` line, sample still builds, doctor grade improves by 1 issue.

**Pitfalls.**
- **Trivia preservation is the #1 trap.** `SyntaxRewriter` strips leading/trailing trivia by default. Always use `.WithLeadingTrivia(originalNode.GetLeadingTrivia())` and `.WithTrailingTrivia(originalNode.GetTrailingTrivia())` when constructing replacements.
- **Multi-line arg lists** — fan-in calls may span 3+ lines with comments between args. Use `node.WithArgumentList(node.ArgumentList.WithArguments(...))` to preserve formatting.
- **Aliased types** — `using DefaultAzureCredential = Azure.Identity.DefaultAzureCredential;` defeats name-based detection. Document the limitation; v2 could use symbol resolution via a `CSharpCompilation`.
- **Atomic writes** — write to `file + ".tmp"` and `File.Move` over the target. Partial writes are evil.
- **Idempotency** — running the same rewriter twice must produce the same output. The "already correct" branches above pin this.

---

#### W.7 — `MafBeforeAfter` MCP tool

**Goal.** Given a list of rule IDs, render a single unified diff showing all proposed changes across the codebase. Lets a maintainer review the whole change-set in one document before approving.

**Dependencies.** **W.6** (shares rewriter logic — call AutoFixTool with `dryRun=true`).

**Effort.** 3-4 hours.

**Files to create.**
- `src/maf-autopilot/Tools/BeforeAfterTool.cs`
- `src/maf-autopilot.Tests/BeforeAfterToolTests.cs`

**Algorithm.**

```csharp
[McpServerTool]
public string MafBeforeAfter(
    string repoPath,
    string[] ruleIds)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Migration Diff Report\n");

    foreach (var ruleId in ruleIds)
    {
        // 1. Run rewriter in dry-run mode (W.6's AutoFixTool with dryRun=true)
        //    + capture (file, originalSource, rewrittenSource) tuples.
        // 2. For each (file, orig, new), generate a git-style unified diff.
        //    Use a simple line-based diff (no need for DiffPlex): split into
        //    lines, find the first/last differing line, emit ±lines as
        //    `+`/`-` prefixes with 3 lines of context.
        // 3. Render as:
        //    ## Rule: <ruleId>
        //    ### File: <relativeFile>
        //    ```diff
        //    @@ -L,N +L,N @@
        //    -old
        //    +new
        //    ```
    }

    return sb.ToString();
}
```

For line-based diffs in v1, use this minimal algorithm — no external diff library needed:
```csharp
static string SimpleDiff(string orig, string updated, string filePath)
{
    var origLines = orig.Split('\n');
    var newLines = updated.Split('\n');
    // For v1: just emit a block diff (full -old / +new) if any line differs.
    // For v2: use System.Text.Json's diff or import DiffPlex.
}
```

**Tests.**
- `BeforeAfter_NoChanges_ReturnsEmptyReport` — codebase with no triggers → empty report header only.
- `BeforeAfter_OneRule_OneFile_RendersDiff` — known-broken file → diff block with `+` and `-` lines.
- `BeforeAfter_MultiRule_MultiFile_AllInReport` — apply 3 rules, verify all sections present.

**Acceptance.**
- All existing tests pass.
- 3+ new tests pass.
- Running `MafBeforeAfter("samples/maf-1.3-sample", new[] { "MAF-AP-SEC-001", "MAF-AP-WF-001" })` produces a 2-section report with the expected diff for each.

**Pitfalls.**
- Don't write anything to disk — this is a preview tool.
- Diff format must be parseable by IDE diff viewers — use `diff --git` style.
- Avoid loading entire massive codebases into memory; stream files.

---

#### W.8 — `samples/maf-1.2-sample/`

**Goal.** A second standalone sample pinned to **MAF 1.2.0** exercising the registry IDs the 1.3 sample can't reach — the `REMOVED-in-1.3` surfaces (`MAF130-THREAD-001`, `MAF130-A2A-001/002`, `MAF130-STREAM-001`, `MAF130-EVENT-001`, `MAF130-ATTR-001/002`).

**Dependencies.** None. (W.6's `MafAutoFix` is a nice-to-have for end-to-end validation.)

**Effort.** 4-8 hours.

**Files to create.** Parallel structure to `samples/maf-1.3-sample/`:

- `samples/maf-1.2-sample/MafSample.FraudClaims12.csproj` — `<PackageReference Include="Microsoft.Agents.AI" Version="1.2.0">` (and matching pins for `Microsoft.Agents.AI.Workflows`, `Microsoft.Extensions.AI 10.3.0`, `Azure.AI.OpenAI 2.6.0`). Target `net8.0`. `ManagePackageVersionsCentrally=false`.
- `samples/maf-1.2-sample/Program.cs` — entry point with banner.
- `samples/maf-1.2-sample/Models.cs` — same shape as 1.3 sample.
- `samples/maf-1.2-sample/Agents/IntakeAgent.cs` — uses top-level `Instructions` (legit pre-1.3 pattern; here it WORKS because 1.2 still has the property).
- `samples/maf-1.2-sample/Executors/OsintInvestigator.cs` — uses `[StreamsMessage]` and `[YieldsMessage(typeof(...))]` attributes (removed in 1.3).
- `samples/maf-1.2-sample/Executors/HistoryInvestigator.cs` — same.
- `samples/maf-1.2-sample/Executors/TransactionInvestigator.cs` — same.
- `samples/maf-1.2-sample/Workflows/FraudClaimsWorkflow.cs` — uses `InProcessExecution.StreamAsync(message)` (removed in 1.3).
- `samples/maf-1.2-sample/Sessions/SessionPersister.cs` — uses `agent.GetNewThread()` returning `AgentThread` + sync `agent.SerializeSession(thread)` (both removed in 1.3).
- `samples/maf-1.2-sample/A2A/A2ARegistration.cs` — uses `services.AddA2AAgent(agent)` + `app.MapA2A("/agent")` (both removed in 1.3).
- `samples/maf-1.2-sample/README.md` — same ASCII WARNING banner style as 1.3 sample, listing the additional registry coverage.

**Algorithm.**

1. **Surface recon first.** Run `dotnet-inspect type --package Microsoft.Agents.AI@1.2.0` and `dotnet-inspect type --package Microsoft.Agents.AI.Workflows@1.2.0` to confirm which types exist + their signatures. Same discipline as Phase T used for 1.3. Do NOT guess.
2. **Verify NuGet availability.** `nuget.org/packages/Microsoft.Agents.AI/1.2.0` must still be downloadable. If unpublished, this idea is blocked — abort + escalate.
3. **Bone-by-bone copy** the 1.3 sample's structure, swapping each file's content for the 1.2-era pattern.
4. **Build verification.** `dotnet build samples/maf-1.2-sample/` must succeed (modulo expected `[Obsolete]` CS0618 warnings for sync session APIs).
5. **Doctor verification.** `dotnet run --project src/maf-autopilot --framework net10.0 -- doctor samples/maf-1.2-sample/` should detect the additional registry IDs — at minimum 5-6 new fires.
6. README with the WARNING banner + table of which anti-patterns this sample covers (focus on the gap vs. the 1.3 sample).

**Tests.** None in the toolkit. (CI regression on this sample is **W.9**.)

**Acceptance.**
- `dotnet build samples/maf-1.2-sample/` returns 0.
- `maf-autopilot doctor` finds at least 5 additional registry IDs.
- The README lists every registry ID this sample exercises.

**Pitfalls.**
- **MAF 1.2 API surface differs significantly from 1.3** — `dotnet-inspect` recon is mandatory before each file is authored. Don't assume any 1.3 API exists in 1.2.
- The 1.2 sample lives in its **own folder with its own csproj** — never share state with the 1.3 sample.
- Some 1.2-era code may emit `[Obsolete]` warnings on 1.2 itself — that's fine; the maf-autopilot CS0618 hunter should find them.
- Do NOT include the 1.2 sample in the main `.sln`. It's a standalone fixture.

---

#### W.9 — CI regression on `samples/*`

**Goal.** Pin workshop invariants. If a future toolkit change regresses detection on any sample, CI fails BEFORE the regression ships.

**Dependencies.** **W.8** (or any second sample beyond `maf-1.3-sample/`).

**Effort.** 2-3 hours.

**Files to create.**
- `samples/maf-1.3-sample/.expected-doctor.yaml` — declarative expectations.
- `samples/maf-1.2-sample/.expected-doctor.yaml` — same.
- `src/maf-autopilot.Tests/SampleRegressionTests.cs` — new test class.

**Files to modify.**
- `src/maf-autopilot.Tests/maf-autopilot.Tests.csproj` — add `<Content Include="../../samples/**/*" Link="samples/%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />` so the samples are accessible at test runtime.

**Algorithm.**

1. Each sample's `.expected-doctor.yaml`:
   ```yaml
   # samples/maf-1.3-sample/.expected-doctor.yaml
   grade: F
   anti_pattern_errors: 10
   anti_pattern_warnings: 3
   silent_starvation_risks: 3
   prompt_lint_errors: 0
   ```
2. Test code:
   ```csharp
   public class SampleRegressionTests
   {
       [Theory]
       [InlineData("samples/maf-1.3-sample")]
       [InlineData("samples/maf-1.2-sample")]
       public async Task Doctor_GradesMatch_ExpectedYaml(string samplePath)
       {
           // 1. Walk up from AppContext.BaseDirectory to find repo root
           // 2. Resolve samplePath
           // 3. Parse .expected-doctor.yaml
           // 4. Run DoctorTool.MafDoctor(samplePath) — invoke the actual MCP method
           // 5. Parse the result's markdown for the grade + counters
           // 6. Assert each expected value
       }
   }
   ```
3. The test should be tagged `[Trait("Category", "Sample")]` so it can be filtered out for fast CI.

**Tests.**
- `Doctor_Grade_MatchesExpectedYaml` — theory data per sample.
- `Sample_HasExpectedYamlFile` — every `samples/*-sample/` folder has `.expected-doctor.yaml`.

**Acceptance.**
- New tests pass on every TFM (6 + executions for 2 samples × 3 TFMs).
- CI runs them on every PR.
- Updating a sample's anti-pattern coverage → update its `.expected-doctor.yaml` in the same PR (locks the expectations in lockstep with the sample).

**Pitfalls.**
- Sample build at test time can be slow. Use `<Content Include>` to COPY pre-built outputs (or pre-doctor-cached results) rather than rebuilding inside the test.
- Path resolution: the `Tests/bin/<TFM>/` directory walks up to find the repo root.
- The doctor's grade calculation can change. When it does, update the YAMLs in the same commit.

---

#### W.10 — `samples/maf-1.0-sample/`

**Goal.** Third sample, oldest. Pinned to **MAF 1.0.0** for the deepest migration ladder demo (1.0 → 1.1 → 1.2 → 1.3 → 1.4 → 1.5).

**Dependencies.** **W.8** (same recipe; do 1.2 first to validate the pattern works).

**Effort.** 4-8 hours.

**Files to create.** Same structure as W.8 but pinned to MAF 1.0.0. Per-file content uses the 1.0-era API surface.

**Algorithm.** Same as W.8:
1. `dotnet-inspect type --package Microsoft.Agents.AI@1.0.0` — confirm the 1.0 surface.
2. Author each file targeting 1.0 idioms.
3. Build + doctor verification.

**Tests.** Add a new `[InlineData("samples/maf-1.0-sample")]` row to W.9's theory.

**Acceptance.** Same as W.8.

**Pitfalls.**
- MAF 1.0.0 is older still — confirm it's still on nuget.org.
- Some 1.0-era APIs may have been removed even from `Microsoft.Extensions.AI` (the 9.0.x line). The csproj may need older transitive pins.
- Some 1.0 anti-patterns may not be in the registry at all — opportunity to ADD entries with appropriate `applies_to_codebases: "pre-1.1.0"`.

---

#### W.11 — `MafScoreMigrationRisk(repoPath)` MCP tool

**Goal.** Pre-migration risk verdict (HARD / MEDIUM / EASY) for any codebase. Sets expectations before a migration starts.

**Dependencies.** None (uses existing scanners).

**Effort.** 4 hours.

**Files to create.**
- `src/maf-autopilot/Tools/MigrationRiskScoreTool.cs`
- `src/maf-autopilot.Tests/MigrationRiskScoreToolTests.cs`

**Algorithm.**

```csharp
[McpServerTool]
public string MafScoreMigrationRisk(string repoPath)
{
    // 1. Run all 4 doctor scanners (anti-pattern, fan-out, prompt-lint, token-cap)
    // 2. Compute weighted score:
    //    - silent_starvation × 3
    //    - cs0246_count       × 2
    //    - cs0117_count       × 2
    //    - cs0618_count       × 1
    //    - anti_pattern_error × 1
    // 3. Thresholds:
    //    - score < 5   → EASY    (1-2 hour migration, low risk)
    //    - 5 ≤ score < 15 → MEDIUM (½ day migration, watch tests)
    //    - score ≥ 15  → HARD    (1+ day migration, expect rework)
    // 4. Return JSON: { verdict: "MEDIUM", score: 12, breakdown: {...}, recommendation: "..." }
}
```

**Tests.**
- `RiskScore_EmptyCodebase_ReturnsEasy`
- `RiskScore_SampleMaf13_ReturnsMediumOrHard` — against the 1.3 sample.
- `RiskScore_Breakdown_IncludesEveryCategory`

**Acceptance.**
- Tests pass on every TFM.
- Running against `samples/maf-1.3-sample/` returns MEDIUM or HARD (10 errors + 3 silent risks → score ~22 → HARD).

**Pitfalls.**
- The weights need real-world calibration. Pick conservative initial values; document the formula in the tool's `[Description]`.
- Don't double-count — a single anti-pattern shouldn't fire multiple categories.

---

#### W.12 — `MafGenerateRegressionPlan(from, to)` MCP tool

**Goal.** Given (fromVersion, toVersion), produce an ordered Mermaid migration roadmap covering every intermediate version's registry entries.

**Dependencies.** **W.1** (uses `applies_to_codebases` for filtering) + **W.8** + **W.10** (samples to test against).

**Effort.** 1 day.

**Files to create.**
- `src/maf-autopilot/Tools/RegressionPlanTool.cs`
- `src/maf-autopilot.Tests/RegressionPlanToolTests.cs`

**Algorithm.**

```csharp
[McpServerTool]
public string MafGenerateRegressionPlan(string fromVersion, string toVersion)
{
    // 1. Validate fromVersion < toVersion (SemVer comparison via System.Version)
    // 2. Walk the compatibility matrix to find every minor version between
    //    from and to (inclusive of `to`, exclusive of `from`).
    // 3. For each intermediate version, collect:
    //    - Registry entries with version_introduced == that version
    //    - Registry entries with applies_to_codebases applicable here
    // 4. Emit Mermaid diagram with one node per version step:
    //    flowchart LR
    //      v10[1.0.0] --> v11[1.1.0]
    //      v11 --> v12[1.2.0]
    //      v12 --> v13[1.3.0]
    //      click v13 callback "12 changes — see guides/maf-1.3.0-migration-guide.md"
    // 5. Below the diagram, a per-step markdown summary table.
    // 6. Return: markdown string.
}
```

**Tests.**
- `RegressionPlan_OneStep_OneVersion`
- `RegressionPlan_FullLadder_AllVersionsPresent` — from 1.0 to 1.5 → 5 nodes.
- `RegressionPlan_FromEqualsTo_ReturnsEmptyPlan`

**Acceptance.**
- Tests pass.
- Running `MafGenerateRegressionPlan("1.0", "1.5")` produces 5 nodes + click callbacks referencing the per-version guide files in `/guides/`.

**Pitfalls.**
- Version comparison: use `System.Version.Parse` not `string.Compare`. Otherwise `"1.10.0"` < `"1.2.0"` falsely.
- `applies_to_codebases` may use formats like `"pre-1.3.0"` or `"1.4.0+"` — parse correctly.
- Some intermediate versions may have no registry entries — show as "0 changes" in the Mermaid.

---

#### W.13 — `MafHealthBadge` (subcommand)

**Goal.** A `maf-autopilot badge` subcommand emits shields.io-endpoint JSON. Consumers embed `[![MAF Health: A](https://img.shields.io/endpoint?url=...)]` in their README.

**Dependencies.** None for the subcommand. (Hosting infra is **X.4**.)

**Effort.** 1 hour.

**Files to create.**
- `src/maf-autopilot/Commands/BadgeCommand.cs` — new CLI subcommand.
- `src/maf-autopilot.Tests/BadgeCommandTests.cs`

**Files to modify.**
- `src/maf-autopilot/Program.cs` — register the new subcommand.

**Algorithm.**

```csharp
public sealed class BadgeCommand
{
    public string Execute(string repoPath)
    {
        // 1. Run DoctorTool.MafDoctor(repoPath) to get the grade
        // 2. Build shields.io endpoint payload:
        //    {
        //      "schemaVersion": 1,
        //      "label": "MAF Health",
        //      "message": "A",
        //      "color": "brightgreen"  // map: A→brightgreen, B→green, C→yellow, D→orange, F→red
        //    }
        // 3. Print JSON to stdout
    }
}
```

**Tests.**
- `Badge_GradeA_EmitsBrightgreen`
- `Badge_GradeF_EmitsRed`
- `Badge_OutputIsValidJson`

**Acceptance.** Tests pass.

**Pitfalls.**
- shields.io endpoint schema is well-defined — follow it exactly.
- Hosting the JSON (so shields.io can fetch it on demand) is **Phase X.4** — out of scope here. The subcommand just emits the JSON to stdout.

---

### Phase X — Things only YOU can do (follow-along guide)

> ✋ **This is the consolidated human-intervention checklist.** Every step is detailed enough to follow verbatim. The workshop itself lives in [`samples/workshop.md`](../samples/workshop.md) — this section points to it but doesn't duplicate the 10-step playbook.

#### 📋 Quick reference card

| When | Item | Status | Time | Sub-section |
|---|---|---|---|---|
| **Critical** — before 1.0 announce | **X.1** Run the workshop end-to-end | 🔴 must-do | ~50 min | [X.1 below](#x1--run-the-workshop-end-to-end-the-a8-unblocker) |
| **Critical** — depends on X.1 | **X.2** Cut stable 1.3.0 (nuget.org + GHCR publish) | 🔴 must-do | ~10 min | [X.2 below](#x2--cut-stable-130) |
| **Polish** — recommended before X.2 | **X.3** Render the README animated casts (install + migration) | 🟡 recommended | ~15 min | [X.3 below](#x3--render-the-readme-animated-casts-both-install-cast--migration-cast) |
| Post-1.0, optional | **X.4** MafHealthBadge hosting infra | 🟢 optional | ~30 min | [X.4 below](#x4--mafhealthbadge-hosting) |
| Post-1.0, optional | **X.5** Phase U Dependabot sweep (#8 + #17) | 🟢 optional | ~30 min | [X.5 below](#x5--phase-u-dependabot-sweep) |
| Post-1.0, optional | **X.6** 1.0 announcement post (blog / discussions / social) | 🟢 optional | ~30 min | [X.6 below](#x6--10-announcement-optional-explicit) |
| As-needed | **X.7** Surgical registry edits (escalations) | ⚪ rare | varies | [X.7 below](#x7--surgical-registry-edits-as-needed) |

**Total time to ship 1.0** = ~75 minutes of your time across X.1 + X.2 + X.3.

**Suggested order:** X.1 → X.3 (render casts so the README looks great on launch) → X.2 (cut). Then post-launch: any of X.4 / X.5 / X.6 / X.7 whenever you feel like them.

#### X.1 — Run the workshop end-to-end (the A.8 unblocker)

**What.** Follow [`samples/workshop.md`](../samples/workshop.md). The 10 steps drive the toolkit from install → audit → plan → execute → verify → retrospective → auto-fix demo.

**Why you (not Claude).** The `@maf-auditor` and `@maf-migration` agents are GitHub Copilot Chat features, loaded by VS Code from `.github/agents/`. Claude Code can't invoke them. You hold the chat session.

**Pre-flight checklist (5 min).**

- [ ] **VS Code Insiders installed** (regular VS Code also works, but Insiders has the freshest MCP support).
- [ ] **GitHub Copilot subscription active** (any tier — needed for `@maf-*` agents).
- [ ] **.NET 8 SDK or later** on PATH (`dotnet --version`).
- [ ] **`maf-autopilot` global tool installed** — `dotnet tool install -g maf-autopilot --prerelease` (Step 0 of the workshop now auto-installs if missing).
- [ ] _(optional)_ Azure OpenAI deployment configured if you want the `--run` mode to actually call the LLM (the workshop doesn't require it — dry-run is the default).

**Exact prompt sequence to use in Copilot Chat.**

The workshop's Steps 4–10 each have one canonical prompt. Copy these verbatim:

| Step | Prompt to send in Copilot Chat |
|---|---|
| 4 | `@maf doctor — what's the health grade of this codebase?` |
| 5 (a) | `show me every anti-pattern in this code` |
| 5 (b) | `check the fan-out executors for silent-starvation risks` |
| 5 (c) | `diagram the workflow topology` |
| 5 (d) | `find any CS0618 warnings the compiler reports` |
| 5b | _(no prompt — just observe the IDE squigglies after `dotnet build`)_ |
| 6 | `@maf-auditor scan this codebase and write a migration plan to docs/migration-plan.md` |
| 7 | `@maf-migration execute the plan in docs/migration-plan.md, with dotnet build gates between each task` |
| 8 | `@maf doctor — what's the health grade now?` |
| 9 | `@maf-best-practice-reviewer review the migrated codebase and flag what's still suboptimal` |
| 10 | `use MafAutoFix to fix the MAF-AP-SEC-001 finding in this codebase` |

**Expected outcome.**
- `samples/maf-1.3-sample/docs/migration-plan.md` produced by Step 6 — a structured markdown plan with one section per finding.
- After Step 7, `dotnet build` is green and the deliberate anti-patterns are gone.
- Step 8's grade is **A** (or **B** if any of the registry-drift "phantom" entries trigger false-positive cleanup advice).
- The migration log (`samples/maf-1.3-sample/docs/migration-plan.md`) + the final grade pair = the **A.8 evidence** to point at when announcing 1.0.

**Reset between attempts** (e.g. for re-running the workshop or sharing it with a colleague).

```bash
# From the repo root, NOT from inside the sample
git restore samples/maf-1.3-sample/
rm -rf samples/maf-1.3-sample/.vscode
rm -f  samples/maf-1.3-sample/docs/migration-plan.md
```

**If something breaks.** File the issue as Phase T.6 expansion (registry drift / scanner gap). Claude can fix the toolkit-side issue then. Most likely failure modes:
- `@maf-auditor` produces a plan that references a registry entry marked `applies_to_codebases: "pre-1.0.0"` (the Phase W.A phantom entries) — ignore those rows. They won't fire on the actual sample's code; the auditor sometimes surfaces them as "consider these patterns too" guidance.
- `@maf-migration` makes a rewrite that breaks `dotnet build` — gate is restorative; revert that file with `git checkout HEAD -- <file>` and ask the agent to try a different approach for that finding.

#### X.2 — Cut stable 1.3.0

**What.** `gh workflow run release.yml -f version=1.3.0` from the repo root.

**What that triggers.**
1. `dotnet test maf-autopilot.sln` across all 3 TFMs (1422 executions).
2. `dotnet pack src/maf-autopilot/maf-autopilot.csproj` — produces a single multi-TFM nupkg.
3. `dotnet pack src/maf-autopilot.Analyzers/maf-autopilot.Analyzers.csproj` — produces the analyzer nupkg.
4. `dotnet nuget push *.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate`.
5. Docker multi-arch image built + pushed to `ghcr.io/joslat/maf-doctor:1.3.0` + `:latest`.
6. `softprops/action-gh-release` creates a tagged GitHub Release with auto-generated notes.

**Why you (not Claude).** One-way publish to a public registry. Once `1.3.0` (no `-alpha-N`) is live, anyone running `dotnet tool install -g maf-autopilot` (no `--prerelease`) gets it. That's the implicit announcement.

**Pre-flight checklist (~5 min — DO ALL BEFORE TRIGGERING).**

- [ ] **`NUGET_API_KEY` secret is present** in GitHub repo settings (`Settings → Secrets and variables → Actions`). Without it, the `dotnet nuget push` step fails with a confusing 403.
- [ ] **X.1 completed AT LEAST ONCE** and produced grade A/B evidence. This is the A.8 acceptance criterion; cutting 1.0 without it ships unverified code.
- [ ] **Branch protection on `main`** allows the release workflow to push tags (the workflow needs `contents: write` permission, which is already declared in `release.yml`).
- [ ] **No uncommitted changes** on `main` — `git status` should be clean. The release pipeline assumes a clean tree.
- [ ] **`Version` in `src/maf-autopilot/maf-autopilot.csproj`** matches the version you're cutting (e.g. `<Version>1.3.0</Version>`). The release workflow overrides this from `-f version=...` but it's safer to commit the right value first.
- [ ] **Sanity check the multi-target nupkg shape locally:** `dotnet pack src/maf-autopilot/maf-autopilot.csproj -c Release` — confirm the produced nupkg's `tools/` folder contains `net8.0`, `net9.0`, `net10.0` subdirectories.
- [ ] **`NUGET_API_KEY` has package-push permission for `maf-autopilot` and `maf-autopilot.Analyzers`** — check at https://www.nuget.org/account/apikeys.

**Exact commands.**

```bash
# Option A — from local clone (recommended).
gh workflow run release.yml -f version=1.3.0 --ref main

# Option B — via GitHub UI.
# Actions → "Release maf-autopilot" → Run workflow → version=1.3.0

# Watch progress (Option A):
gh run watch
```

**Post-flight check (~5 min after the workflow completes).**

- [ ] **Package live on nuget.org:** https://www.nuget.org/packages/maf-autopilot/1.3.0 should resolve within ~5 minutes.
- [ ] **Analyzer package live:** https://www.nuget.org/packages/maf-autopilot.Analyzers/1.3.0.
- [ ] **GitHub Release page** has been created with auto-generated notes from `softprops/action-gh-release`.
- [ ] **Docker image** pushed: `docker pull ghcr.io/joslat/maf-doctor:1.3.0` succeeds.
- [ ] **A clean install works:** in a fresh shell, `dotnet tool install -g maf-autopilot` (NO `--prerelease`) gets you 1.3.0 stable.

**Rollback (if the publish goes wrong).**

NuGet packages CAN be unlisted (hidden from search but still installable by version pin) — they cannot be deleted. Steps:

```
# On nuget.org → My Account → Package Manager → maf-autopilot → 1.3.0 → "Unlist"
# Same for maf-autopilot.Analyzers/1.3.0.
```

You can then publish `1.3.1` with the fix. Avoid this path by completing the pre-flight checklist.

#### X.3 — Render the README animated casts (both install-cast + migration-cast)

**What.** The repo ships **two** `.tape` source scripts. The actual animated GIFs are rendered locally (vhs needs a real terminal) and committed once. After X.3, the README's two top images come to life.

**Why before X.2 (recommended).** The 1.0 announcement (X.2's implicit publish + X.6's optional explicit post) is when the README gets the most eyeballs. Ship the casts BEFORE that. ~15 min of your time.

**Why you (not Claude).** I can write the `.tape` scripts (W.4 + #8 source artifacts already shipped) and the wrapper scripts but I can't run vhs in this environment. The actual GIF rendering needs the toolchain locally.

**The two casts.**

| File | What it shows | Source |
|---|---|---|
| `docs/assets/install-cast.gif` | 30s: install + audit demo → grade F | `docs/assets/install-cast.tape` (W.4) |
| `docs/assets/migration-cast.gif` | 30s: auto-fix loop → grade A | `docs/assets/migration-cast.tape` (#8) |

##### Step 1 — Install vhs + companion tools (one-time setup, ~5 min)

**macOS:**
```bash
brew install vhs ffmpeg ttyd
```

**Windows (PowerShell):**
```powershell
winget install charmbracelet.vhs
# ffmpeg + ttyd ship inside the vhs install on Windows
```

**Linux:** see [vhs installation docs](https://github.com/charmbracelet/vhs#installation). Typically:
```bash
# Debian/Ubuntu
sudo apt install ffmpeg ttyd
# Then download vhs from the GitHub releases page
```

**Verify install:**
```bash
vhs --version           # should print a version
ffmpeg -version | head -1
ttyd --version
```

##### Step 2 — Render both casts (~5 min)

The repo ships a wrapper script that renders both:

**bash / zsh / WSL:**
```bash
bash scripts/make-cast.sh           # render BOTH (default)
# OR render individually:
bash scripts/make-cast.sh install
bash scripts/make-cast.sh migration
```

**PowerShell:**
```powershell
pwsh scripts/make-cast.ps1          # render BOTH (default)
pwsh scripts/make-cast.ps1 install
pwsh scripts/make-cast.ps1 migration
```

**Expected output:**
```
✓ Rendered both casts:
    docs/assets/install-cast.gif
    docs/assets/migration-cast.gif

Commit with: git add docs/assets/*.gif && git commit
```

Each rendering takes ~30 seconds. The wrapper script complains loudly if vhs isn't on PATH.

##### Step 3 — Verify the casts look right (~3 min)

Open each GIF in a viewer (or right-click → "Open with browser" works fine):

- **install-cast.gif** — should show the install + audit demo finishing with a 🔴 grade F summary.
- **migration-cast.gif** — should show the auto-fix loop finishing with a 🟢 grade A summary.

If the timing feels off or commands look truncated, edit the corresponding `.tape` file (`docs/assets/{install,migration}-cast.tape`) and re-render. The `.tape` scripts are heavily commented and use named beats.

##### Step 4 — Commit (~2 min)

```bash
git add docs/assets/install-cast.gif docs/assets/migration-cast.gif
git commit -m "docs(readme): render animated install + migration casts"
git push origin main
```

After push, refresh the README on GitHub. The two `<img>` blocks at the top should now show the live animations.

##### Troubleshooting

| Symptom | Fix |
|---|---|
| `vhs: command not found` | Re-run the Step 1 install for your OS. On Windows, `winget` adds vhs to PATH on next-shell-restart. |
| The cast renders but text is cut off | Adjust `Set Width` / `Set Height` in the `.tape` file. Current setting: 1200×720 (install) and 1280×800 (migration). |
| The cast renders but a command in the script doesn't actually exist (e.g. `maf-autopilot autofix-all` fails) | You need `maf-autopilot 1.3.0-alpha-6+` installed locally (`dotnet tool install -g maf-autopilot --prerelease`). The cast assumes the tool is on PATH. |
| GIF size is too big for GitHub (≥ 25 MB) | Switch `Output ... .gif` to `Output ... .webm` in the `.tape` file; smaller + GitHub renders WebM via `<video>`. |

#### X.4 — MafHealthBadge hosting

**What.** The toolkit ships a `maf-autopilot badge` CLI subcommand (W.13) that emits shields.io-endpoint-badge JSON. To turn that into a live `[![MAF Health: A](...)]` badge in your README, **the JSON needs to be hosted at a stable URL** that shields.io can fetch on demand.

**Why you (not Claude).** Needs your account on Cloudflare / GitHub Gist / etc. Hosting choice is yours; ~30 min once you pick.

##### Recommended option — GitHub Gist (~10 min, free, no infra)

1. Run `maf-autopilot badge .` against your repo. Copy the JSON output (~6 lines).
2. Create a new **public gist** at https://gist.github.com — filename `maf-autopilot-health.json`, paste the JSON.
3. Hit "Create public gist." Note the gist's raw URL (looks like `https://gist.githubusercontent.com/<user>/<gist-id>/raw/maf-autopilot-health.json`).
4. The shield URL becomes:
   ```
   https://img.shields.io/endpoint?url=https%3A%2F%2Fgist.githubusercontent.com%2F<user>%2F<gist-id>%2Fraw%2Fmaf-autopilot-health.json
   ```
5. Add `[![MAF Health](https://img.shields.io/endpoint?url=...)](https://github.com/joslat/maf-doctor)` to your README.
6. **Wire CI to refresh the gist** — add a step to `.github/workflows/maf-drift-detector.yml` (or release.yml) that updates the gist via `gh gist edit` after each release.

##### Alternative — Cloudflare Worker (~30 min, free tier, more flexible)

1. Sign up at https://workers.cloudflare.com if you don't have an account.
2. Create a Worker with the source from this gist template: https://gist.github.com/jose/maf-autopilot-worker-template _(template not yet published; see [W.13 detail](#w13--mafhealthbadge-subcommand) for the JSON schema)_.
3. The Worker reads from a Worker KV store (or just returns a static value); CI updates the KV value via the Cloudflare API token.
4. The shield URL becomes: `https://img.shields.io/endpoint?url=https://<your-worker>.workers.dev/maf-autopilot/health.json`.

##### Sanity check after setup

```bash
curl https://<your-hosting>/maf-autopilot/health.json
# Expected output:
# {"schemaVersion":1,"label":"MAF Health","message":"A","color":"brightgreen"}
```

If shields.io shows "endpoint not found" or "invalid JSON," double-check the URL-encoding in the `?url=` query parameter — special characters need percent-encoding.

#### X.5 — Phase U Dependabot sweep

**What.** Revisit the 2 PRs deferred in Phase R after 1.0 ships:
- **#8** `coverlet.collector 6.0.2 → 10.0.0` — 4 majors. Verify coverage still works post-merge.
- **#17** `Microsoft.CodeAnalysis.* 4.11.0 → 5.3.0` (grouped) — Roslyn SDK major. Verify both the analyzer and its tests still build + pass. May need code changes for renamed Roslyn APIs.

**Why you (not Claude).** Could be done by Claude after the workshop runs green, but the timing is a judgement call you should make.

##### Step-by-step for each PR

For **#8 (coverlet)**:
```bash
gh pr checkout 8
dotnet test maf-autopilot.sln -c Release --collect:"XPlat Code Coverage"
# Verify the coverage XML output is non-empty and parseable.
# If green: gh pr merge 8 --squash --delete-branch
# If broken: file an issue with the breakage detail; ask Claude to fix
```

For **#17 (Roslyn SDK)**:
```bash
gh pr checkout 17
dotnet build maf-autopilot.sln -c Release
# The analyzer + analyzer.Tests projects both depend on Microsoft.CodeAnalysis.*
# If build fails: read the error; Roslyn API renames between major versions are
# common (e.g. some Workspace methods got new overloads). Document the breaks.
# If green:
dotnet test maf-autopilot.sln -c Release
# If both green: gh pr merge 17 --squash --delete-branch
```

##### If both succeed, you're done with Phase U. If either breaks

File the failure detail as a follow-up issue. Claude can fix the toolkit-side compatibility issues (W.14-style refactor for renamed APIs) in a separate session.

#### X.6 — 1.0 announcement (optional explicit)

**What.** Write a discussions post / blog / social media announcement.

**Why you (not Claude).** Voice + audience are yours.

##### Suggested talking points (refresh against current counts before posting)

- The toolkit's reach: **22 MCP tools, 7 agents, 13 skills, 7 GitHub Actions workflows, 3 Roslyn analyzers, 3 multi-version samples (1.0/1.2/1.3)**.
- **1734 test executions** (578 unique × 3 TFMs) pinning every detection rule + every auto-fix rewriter + the live registry's integrity.
- The A.8 evidence (workshop migration log from X.1 + final grade A/B).
- The registry coverage (~17 entries spanning fan-in, sessions, executors, attributes, A2A, middleware — with 7 entries explicitly marked pre-1.0.0 documentation after the Phase W.A chronology audit).
- The Phase Q auto-update validation (MAF 1.4 + 1.5 surfaces ingested without manual help).
- A pointer to [`samples/workshop.md`](../samples/workshop.md) for hands-on adoption.
- A pointer to the [Top-5 must-do-now blueprints](#-top-5-must-do-now--implementation-blueprints-2026-05-13) so readers know what's coming in 1.1.x.

##### Suggested channels (any subset)

- **GitHub Discussions** on `joslat/maf-doctor` — long-form, evergreen, indexable.
- **A Medium / dev.to blog post** — for narrative + screenshots of the casts (X.3 produces them).
- **Twitter/X thread** — 5-tweet outline: install → audit → grade F → auto-fix → grade A. The cast GIFs from X.3 are tweet-shaped.
- **LinkedIn post** — tag Microsoft Agent Framework team for visibility.
- **Reddit r/dotnet** — short post linking to the README.

##### Template opening lines (steal these)

> "Just shipped maf-autopilot 1.0 — an MCP toolkit that turns GitHub Copilot Chat into a permanent Microsoft Agent Framework expert. Audits your MAF code for the silent fan-out/fan-in bugs that compile clean but break at runtime, plus a deterministic auto-fixer (no LLM) that closes the detection→remediation loop. Install: `dotnet tool install -g maf-autopilot`. Try it: `maf-autopilot doctor .`."

---

#### X.7 — Surgical registry edits (as-needed)

**What.** Rare cases where a registry entry's content exceeds Claude's scope:
- Legal / branding review of a `fix_description` string.
- Customer-specific guidance (e.g. "your team's pattern is X; the registry says Y; resolve").
- Coordinated edits across registry + guides + workflow YAML that need a single human owner.

**Why you (not Claude).** Judgement call territory.

##### When this fires

Typically: a customer files a `maf-release` PR that the AI-fill verifier (X.3) flags as borderline, OR a registry entry's `notes:` field needs a Phase T-correction-style update after upstream MAF behaviour changes.

##### How to make the edit safely

1. Edit `.github/skills/obsolete-api-registry/registry.yaml` directly on a branch.
2. Run `maf-autopilot verify-registry` locally — fails if your edit broke the structural invariants.
3. Run `dotnet test maf-autopilot.sln --filter "FullyQualifiedName~Registry"` — passes if the matcher / drift tests still work.
4. PR with hand-written description (NOT `ai-fill` label — that triggers the bot verification path).

##### When to escalate to Claude

If the registry edit touches MORE than `notes:` field (e.g. needs new `example_before`/`example_after` snippets, or new entries), ask Claude — it has the toolkit's full context + can author the Roslyn-validated examples.

---

## 💡 Lateral Thinking & Wow Ideas (post-1.0 brainstorm — added 2026-05-13)

> **Why this section exists.** Phase W landed 13/13 items. The toolkit is structurally complete for migration assistance. But: "structurally complete" ≠ "no more value to mine." This section is the **structured ideation backlog** — applied with five different thinking patterns — so the next maintainer (Claude or human) doesn't have to re-invent it. Pick a row from the table, build it, ship it.

### How these were generated — five thinking patterns

1. **Incremental** — _What if we 10x an existing feature?_ (e.g. `MafAutoFix` works on one file → run it on every file in CI with dependency-aware ordering.)
2. **Optimization** — _What's slow, lossy, redundant, or could be cached?_ (e.g. doctor rescans whole repos every time; could be incremental.)
3. **Adversarial** — _What could break us; what classes of bug aren't we catching?_ (e.g. security findings beyond DefaultAzureCredential; performance anti-patterns; conflicting agent instructions.)
4. **Wow-factor** — _What makes an audience visibly react?_ (e.g. browser playground, GitHub bot replies, X-ray mode in VS Code.)
5. **Lateral** — _What's the COMPLETELY different angle?_ (e.g. tool that translates SemanticKernel → MAF; toolkit-as-a-MAF-agent; auto-CHANGELOG generator for MAF itself.)

### Scoring rubric

Each idea gets four 1-10 scores + a final score (visual `🟢×N + ⚪×(10−N)`).

| Score column | Meaning | Higher = better? |
|---|---|---|
| **Difficulty** | 1 = an afternoon's work; 10 = a month's work for a senior | ⬇ inverted in final |
| **Coolness** | 1 = boring CI plumbing; 10 = "no one else has this" | ⬆ |
| **Wow on demo** | 1 = audience checks phone; 10 = audible gasp | ⬆ |
| **Usefulness** | 1 = nice-to-have polish; 10 = customers would pay for it | ⬆ |
| **FINAL** | `round( (Cool + Wow + Useful) / 3 − 0.3 × Difficulty )`, clamped 1-10 | ⬆ |

---

### Idea table (sorted by FINAL score, descending)

| # | Idea | Category | Description (1-2 sentences) | Diff | Cool | Wow | Useful | FINAL |
|---|---|---|---|---|---|---|---|---|
| **1** | **VS Code "X-ray mode"** | Wow | Squiggly underline + hover tooltip + lightbulb code-fix for every analyzer rule, with the registry entry surfaced inline. Today the analyzer ships diagnostics; we'd ship a real VS Code extension wrapping them with rich UI (snippets, before/after preview, click-to-apply MafAutoFix). | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢🟢 (10) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ **8** |
| **2** | **Browser playground (WASM)** | Wow | Web app: paste MAF code, hit Audit, see grade + findings live. The analyzer compiles to WASM, runs entirely in the browser; no server, no install. Try-before-clone hook. | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢🟢 (10) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢🟢 (10) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **3** | **`MafAutoFix --all` (dependency-aware batch fix)** | Incremental | Single command that applies every applicable rule in the right topological order (e.g. `sealed` modifier BEFORE fan-in arg swap, because the latter binds against the former). Plus `--dry-run` for the full preview. Closes the last bit of friction between detection and remediation. | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ (3) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢🟢 (10) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **4** | **GitHub bot — `@maf-autopilot doctor`** | Wow | Mention the bot in any issue or PR comment, get an audit reply within 30 seconds. Bot runs the doctor on the head commit + posts the markdown verdict + offers AutoFix follow-up. Distributes the tool to anyone who Cmd-clicks a GitHub mention. | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **5** | **AI-powered registry auto-mining** | Lateral | Feed MAF source-code diffs (between two NuGet versions) through an LLM to auto-propose new registry entries with `fix_description` + `example_before`/`example_after`. Reduces the maintainer burden of keeping the registry current as MAF evolves. Pairs with the existing `maf-release-watcher`. | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **6** | **Security-anti-pattern bundle** | Adversarial | Expand the security scanners beyond `DefaultAzureCredential`. Add detectors for: secrets logged via `Console.WriteLine`, JWT-token persistence in chat history, prompt-injection from untrusted inputs, missing token caps on streaming calls, AKS-friendly identity patterns. Currently a thin slice; this is where customers actually get burned. | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢🟢 (10) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **7** | **`MafSecondPass` — conflicting agent instructions** | Adversarial | When a workflow has multiple agents whose `Instructions` literals contradict each other (e.g. one says "always confirm before action," another says "act decisively without confirmation"), flag the conflict. Cross-prompt analysis nobody else does. | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **8** | **Migration time-lapse video** | Wow | Automated vhs recording of the workshop's full migration flow: doctor → plan → execute → final grade A. Posted as an animated GIF in the README + as a 2-min video on social. Zero attendees attended; everyone sees the demo. | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **9** | **MAF certification badge** | Wow | Shields.io badge on consumer READMEs: "MAF-verified by maf-autopilot v1.3.0 / Grade A / 2026-05-13". Builds on the W.13 `MafHealthBadge` subcommand we already have; needs X.4 hosting infra. Viral marketing surface — every adopting repo backlinks to us. | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ **7** |
| **10** | **VS Code Code Fix provider** | Incremental | Surface each `MafAutoFix` rewriter as a VS Code lightbulb action. Today: user has to invoke MCP via chat; ideal: hover the squiggle, hit Cmd+. → "Apply MafAutoFix: rename DefaultAzureCredential → ManagedIdentityCredential". Native IDE UX. | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **11** | **Cross-framework migrator: SK → MAF** | Lateral | A SemanticKernel codebase passes through a translator that emits MAF code with the canonical 1.5.0 patterns. Same Roslyn machinery, different rewriters. Opens a huge market segment that doesn't even know MAF yet. | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **12** | **Performance anti-pattern bundle** | Adversarial | Detect heavy LLM calls inside loops, missing streaming on long-running invocations, missing token caps already detected partially. Cost-aware analyzer. Customers care MORE about this than about credentials hygiene. | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **13** | **Incremental scanning (cache by file hash)** | Optimization | Doctor today rescans the whole repo every call. Add a `.maf-cache/findings.json` per-file-hash cache so re-runs after a small edit are <100 ms instead of ~1 s. Big repos win disproportionately. | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **14** | **`maf-autopilot scaffold-new` — full project starter** | Lateral | `dotnet new maf-app --topic 'fraud-claims'` emits a complete MAF project with samples, CI, telemetry, README, the analyzer wired in, agents pre-scaffolded. The toolkit's first impression is the BIRTH of a project, not the rescue of a broken one. | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **15** | **A2A maf-autopilot — toolkit AS a MAF agent** | Lateral | Spawn the toolkit as a hosted MAF agent itself. Other agents in a workflow can call `@maf-autopilot` to audit a code snippet they're about to compile. Self-hosting: the toolkit audits the toolkit. Meta-cool. | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢🟢 (10) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **16** | **`MafFuzzScanner` — adversarial input testing** | Adversarial | Generate ~1000 randomly mutated `.cs` inputs + run them through every scanner. Expect: no crashes, no infinite loops, no memory blowups. Find the inputs that produce false positives. Surface them as test fixtures. Hardens the toolkit against real-world weird code. | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **17** | **Real-time status-bar grade (VS Code)** | Incremental | Persistent status-bar item: `🟢 MAF: A · 0 errors` updated on every save. Goes red the moment a regression is introduced. Continuous awareness rather than periodic check. | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **18** | **`samples/find-the-bug/` browser game version** | Wow | The find-the-bug-30s challenge as an interactive web game. Audience scans QR code, plays on their phone, leaderboard shows top scorer per snippet. Workshop-killer. | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢⚪ (9) | 🟢🟢🟢🟢🟢🟢🟢🟢🟢🟢 (10) | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ **6** |
| **19** | **Parallel rewriter execution** | Optimization | `MafAutoFix --all` runs rewriters serially today. Make file rewrites concurrent (`Task.WhenAll` across files) so big repos finish in <1s instead of ~10s. Mostly mechanical. | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ (3) | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ (3) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ **5** |
| **20** | **Skill marketplace** | Lateral | Third parties contribute custom anti-pattern rules + registry entries via a simple `.maf-skill/manifest.yaml`. The toolkit auto-loads installed skills. Network-effects: community-driven coverage. | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ **5** |
| **21** | **Auto-CHANGELOG from doctor diffs** | Lateral | Run the doctor on `main` HEAD vs the previous tag. Diff the findings. Auto-generate a customer-facing CHANGELOG entry describing what improved between releases. Customers love this; competitors don't ship it. | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ **5** |
| **22** | **Pre-commit hook with `MafAutoFix --all --dry-run`** | Incremental | Drop a pre-commit hook + `.husky/maf-precheck.sh` into any consumer repo via `maf-autopilot init --hooks`. Catches regressions before they leave the developer's laptop. | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ (3) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ **5** |
| **23** | **Telemetry-free benchmarks** | Optimization | Built-in `--bench` that runs the doctor + autofix against the 3 samples N times, reports p50/p99 wall-clock. Lets us advertise "audits a 100-file MAF codebase in 850 ms" with evidence. Self-marketing. | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ (3) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ (5) | 🟢🟢🟢🟢🟢⚪⚪⚪⚪⚪ **5** |
| **24** | **Signed registry.yaml + supply-chain hardening** | Adversarial | Sign `registry.yaml` with a maintainer key. The toolkit verifies the signature on load + refuses to run if mismatched. Prevents a compromised dependency from poisoning the auto-fix flow. | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢⚪⚪⚪⚪⚪⚪ (4) | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ (3) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ **3** |
| **25** | **"MAF detective" terminal game** | Lateral | ncurses-style interactive puzzle: 10 levels, each presents broken MAF code, player has 60s to spot the bug. Teaches the anti-patterns by play. Workshop-replacement for self-paced learning. | 🟢🟢🟢🟢🟢🟢⚪⚪⚪⚪ (6) | 🟢🟢🟢🟢🟢🟢🟢🟢⚪⚪ (8) | 🟢🟢🟢🟢🟢🟢🟢⚪⚪⚪ (7) | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ (3) | 🟢🟢🟢⚪⚪⚪⚪⚪⚪⚪ **3** |

### My top 5 picks (highest leverage × shippable in 1-2 weeks)

If we shipped these as **maf-autopilot 1.1.0** they'd materially differentiate the toolkit:

1. **#3 — `MafAutoFix --all`** (the "fix everything fixable" command). 1 day. The natural sequel to W.6.
2. **#4 — GitHub bot** (`@maf-autopilot` mentions in PRs/issues). 2-3 days. Single biggest distribution win.
3. **#1 — VS Code "X-ray mode"** with rich Code Fix UI. 1 week. The bridge from "tool you invoke" → "tool that lives where you code."
4. **#5 — AI registry auto-mining**. 3-5 days. Solves the registry-maintenance bottleneck as MAF evolves.
5. **#8 — Migration time-lapse video**. ½ day. Pure marketing win; rides on existing infra.

Total: ~2-2.5 focused weeks. Would 5x the toolkit's surface visibility.

---

### 🚀 Top-5 Must-Do-Now — Implementation Blueprints (2026-05-13)

Per the maintainer's "examine how to implement them in the best possible way" prompt, here's the per-idea blueprint with cost, deployment impact, and a **can-do-RIGHT-NOW** verdict. Sorted by "ship-now feasibility" rather than score.

#### #3 — `MafAutoFix --all` ✅ DONE 2026-05-13

**Verdict.** ✅ **ALREADY LANDED.** Zero new deployment, ~2 hours of work. The single fastest win in this list.

**Why it was the only one doable right-now.**
- Pure extension of existing `AutoFixTool` — same MCP surface, same nupkg, same CLI.
- No new dependencies, no new APIs to learn.
- Test surface grows but no test infra changes.

**Implementation that shipped.**
- Refactored `AutoFixTool.MafAutoFix`'s inner loop into a private `ApplyRewriterToRepo(rewriter, repoPath, specificFile, dryRun)` helper returning a `RewriterRunResult` record. Shared between `MafAutoFix` and `MafAutoFixAll`.
- New `[McpServerTool] MafAutoFixAll(repoPath, specificFile?, dryRun?)` method. Iterates over a hard-coded `orderedRules` array (NOT all keys of `_factories` — that array deliberately omits the alias IDs `MAF002` / `MAF003` so each rewriter runs ONCE per pass).
- Dependency-safe order encoded with inline rationale comments:
  1. `MAF-AP-WF-001` (sealed) — purely additive; never affects others.
  2. `MAF-AP-SEC-003` (EnableSensitiveData) — removes an initializer entry.
  3. `MAF-AP-SEC-001` (DefaultAzureCredential) — type-name swap.
  4. `MAF130-FAN-IN-001` — argument-position swap; runs AFTER 1-3.
  5. `MAF-AP-CONC-002` (.Result/.Wait()) — runs LAST in case any prior rule altered the wrapped invocation.
- Aggregate JSON: `dryRun`, `orderOfExecution`, `totalDistinctFilesChanged` (set-union across rules), `affectedFiles` (sorted), `perRule` breakdown.
- 5 new tests added: empty path, all-rules-applied, dry-run-leaves-disk, order-of-execution invariant pinned (sealed-before-fan-in-before-sync), no-op repo.
- TourTool catalogue extended with `MafAutoFixAll` entry.

**Cost.** ~2 hours actual; estimated 4. Build time impact: negligible (~+10 KB to the nupkg).

---

#### #4 — GitHub bot (`@maf-autopilot` in PR/issue comments) ❌ NEEDS NEW INFRA

**Verdict.** ❌ **Defer until 1.1.x infra session.** ~2-3 days of work, but the blocker is hosting + GitHub App registration.

**What it would look like.**

```
Bob:  Can someone @maf-autopilot doctor this branch?
Bot:  🔴 MAF health grade F. 7 anti-pattern errors + 2 silent-starvation
      risks. Top fixes:
      1. HandleAsync at OsintInvestigator.cs:19 returns ValueTask...
      Want me to apply MafAutoFix --all? Reply with `@maf-autopilot fix`.

Bob:  @maf-autopilot fix
Bot:  ✅ Applied 5 rules across 8 files. Grade now: A.
      [opened PR #42 against the branch]
```

**Implementation breakdown.**

| Component | Stack | Cost |
|---|---|---|
| GitHub App registration | github.com/settings/apps — 10 min UI work | 10 min |
| Webhook handler | Cloudflare Worker / Azure Function / AWS Lambda — JS or .NET | 1 day |
| Auth: GitHub App private key + installation tokens | `Octokit.Net` — well-trodden | 2 hrs |
| Runner: spawn `maf-autopilot` as subprocess OR pre-install into the Worker | Both work; subprocess is simpler | 4 hrs |
| Comment parsing: detect `@maf-autopilot <verb>` mentions | Regex on `issue_comment.created` payload | 2 hrs |
| Cloning the branch into a workspace for the doctor to read | `git clone --depth 1` in the Worker | 2 hrs |
| Idempotency / replay protection | Use webhook `delivery-id` as a cache key | 2 hrs |
| Posting the reply | `POST /repos/<owner>/<repo>/issues/<n>/comments` | 1 hr |

**Hosting decision matters.** Cheapest: Cloudflare Workers (free tier covers small audiences; need ~256 MB to clone+scan a small MAF repo so might need a Durable Object). Easiest for .NET-native: Azure Functions (consumption plan; we already use Azure ecosystem). Open question for the maintainer.

**Recommended next move.** Spike a 1-day proof-of-concept on Cloudflare Workers: webhook → simple `gh pr diff` → reply "Hello world, I'd audit this if I knew how." Once green, build out.

---

#### #5 — AI-powered registry auto-mining 🟡 PARTIALLY DOABLE NOW

**Verdict.** 🟡 **Basic version doable today (1-2 days) — full version is the bigger lift.**

**What "AI-powered registry auto-mining" actually means.**

Today's workflow (`.github/workflows/maf-release-watcher.yml`):
1. Cron: weekly, checks NuGet for new MAF stable.
2. Runs `dotnet-inspect diff Microsoft.Agents.AI@old..new`.
3. Captures the raw diff (structural — types added/removed, method signatures changed).
4. Runs `python3 .github/scripts/gen_guide_section.py` to write a per-version migration guide stub.
5. Runs `maf-autopilot registry-extract` to emit DRAFT registry entries (with TODO placeholders for the `fix_description`, `example_before`, `example_after` fields).
6. Dispatches `maf-ai-fill-todos.yml` which opens a GitHub issue assigned to Copilot Coding Agent. Copilot fills the TODOs.
7. Copilot opens a PR. Maintainer reviews and merges.

The toolkit ALREADY has a primitive auto-mining loop. The gap is **how well** Copilot fills the TODOs. Reading the existing PRs (e.g. #15 in this repo's history), the fill quality is good but inconsistent — Copilot sometimes confuses behavioural changes with type-renames, etc.

**"AI-powered" improvement (basic — DOABLE TODAY).**

1. **Refine the Copilot prompt** in `maf-ai-fill-todos.yml`. Add stricter rubric: "For each draft entry, identify exactly ONE breaking change category: TYPE-REMOVED, METHOD-RENAMED, SIGNATURE-CHANGED, BEHAVIOR-CHANGED, OR ATTRIBUTE-REMOVED. Use the matching template." Inline templates in the prompt.

2. **Enrich the diff context.** Currently we pass `dotnet-inspect diff` output (structural). Add: the release notes from GitHub (we already fetch them in `release-notes.txt`), plus a snippet of the MAF source PR description if `gh pr view` finds one for the bumped version.

3. **Add a self-verification step.** After Copilot fills the TODOs, run `MafExplain` against each `example_before` + `example_after` snippet. If MafExplain returns ERROR (e.g. the example doesn't parse), reject the PR and ask Copilot to re-do.

Cost for the basic version: ~1-2 days. Same CI workflow, same Copilot Coding Agent, just better prompt + a verification gate.

**"AI-powered" improvement (advanced — week-long lift).**

1. **Mine MAF source repo PRs directly.** GitHub API: fetch every PR merged between two MAF tags, summarise each via an LLM call with the prompt "Did this PR break customer code? If yes, what's the migration?". Aggregate findings into proposed registry entries.

2. **Cross-reference with `dotnet-inspect`.** A PR that removes a type AND ships in the same release as a `[Obsolete]` warning is a high-confidence registry entry. A PR that's behavioural-only (e.g. "default value changed from X to Y") is detectable ONLY this way — `dotnet-inspect` won't catch it.

3. **Score each candidate entry by confidence.** "This PR changed `AddFanInBarrierEdge` and the docstring uses the phrase 'breaking change' → confidence 0.95" vs. "This PR has a vague title and unclear scope → confidence 0.4". Below a threshold, skip.

Cost for the advanced version: ~1 week. Needs a one-time API key for the LLM (Anthropic or OpenAI), a new Python script in `.github/scripts/`, and additional secrets in the GitHub repo.

---

#### #1 — VS Code "X-ray mode" ❌ DIFFERENT LANGUAGE + DEPLOY TARGET

**Verdict.** ❌ **~1 week of focused work. New TypeScript project. New Marketplace publish.** High-leverage but not a "right now" item.

**What it would look like.** Open the workshop sample in VS Code. Every executor file has red squigglies under every fan-out `ValueTask`. Hover → tooltip shows the full registry entry's `fix_description` + the link to the migration guide. Cmd+. opens a lightbulb menu with "Apply MafAutoFix: rename to ValueTask<T>". Click → file rewritten + diff shown in a side panel.

**Implementation.**

| Component | Stack | Cost |
|---|---|---|
| VS Code extension scaffold | `yo code` | 1 hr |
| MCP client wrapper (talks to `maf-autopilot` over stdio) | TypeScript — use the official `@modelcontextprotocol/sdk` | 1 day |
| Code Fix provider per analyzer rule (MAF001/2/3) | VS Code `CodeActionProvider` API | 1 day |
| Hover provider for analyzer diagnostics | `HoverProvider` API; pull text from `MafRegistryLookup` MCP tool | ½ day |
| Webview panel for the doctor verdict | VS Code Webview API + simple HTML | ½ day |
| Marketplace metadata + first publish | `vsce publish` | 1 day |

Total: ~1 week. Then ongoing minor maintenance.

**Bridge alternative (DOABLE NOW, partial benefit).** The analyzer we shipped in W.2 already produces squigglies via MAF001/2/3. What's MISSING is: (a) the rich hover with the registry entry, and (b) the lightbulb Code Fix. We could add Code Fix providers TO THE ANALYZER itself (it's already loaded in the IDE) without writing a separate VS Code extension. That's ~2 days of work and would deliver 70% of the X-ray benefit.

---

#### #8 — Migration time-lapse video 🟡 ½-DAY EXTENSION DOABLE NOW

**Verdict.** 🟡 **Half-day. Extends the existing `install-cast.tape` from W.4.** Rendering still needs vhs locally.

**Implementation.**

1. New `.tape` script `docs/assets/migration-cast.tape` (alongside the existing install-cast.tape). Beats:
   - Beat 1: clone the sample (`git clone... && cd ...`)
   - Beat 2: build (`dotnet build` → show CS0618 warning)
   - Beat 3: doctor (`maf-autopilot doctor .` → 🔴 F)
   - Beat 4: auto-fix (`maf-autopilot autofix-all .` — once we expose the CLI subcommand, see below)
   - Beat 5: re-doctor (🟢 A)
   - Beat 6: build (`dotnet build` → 0 warnings 0 errors)

2. To make `maf-autopilot autofix-all .` a real CLI command (not just an MCP tool), add the subcommand to `Program.cs` parallel to `doctor` / `badge`. ~30 min.

3. Render `migration-cast.gif`, embed in README as a SECOND animated demo.

**Caveat.** The full `@maf-migration` agent flow (the ChatGPT/Copilot session) cannot be vhs'd — it requires a live Copilot session. The migration-cast covers the deterministic auto-fix flow only.

---

#### #15 — A2A maf-autopilot (the crazy bet) 🔮 ~1 WEEK

**Verdict.** 🔮 **The riskiest + highest-ceiling pick.** Doable but not "right now".

**What it means concretely.**

Today, `maf-autopilot` runs OUTSIDE MAF. It's a `dotnet tool` you invoke from your shell, or an MCP server VS Code talks to. The tools are reachable only from those entry points.

In an A2A world, `maf-autopilot` becomes a MAF agent ITSELF — a `ChatClientAgent` with the toolkit's MCP tools wired as MAF function-call tools. Then ANY MAF workflow can include the autopilot agent as a node:

```csharp
var autopilotAgent = MafAutopilotAgent.Create(chatClient);
// = ChatClientAgent { Name = "maf-autopilot",
//                     ChatOptions.Tools = [MafApiSafety, MafDoctor, MafAutoFix, ...] }

var workflow = new WorkflowBuilder(myDevAgent)
    .AddEdge(myDevAgent, autopilotAgent)    // dev agent emits code → autopilot audits it
    .AddEdge(autopilotAgent, deployAgent)   // only deploy if autopilot grades A/B
    .Build();
```

**Why this is "wow".** Three reasons:

1. **The toolkit auditing the toolkit.** Recursive self-validation. Demo-able by spawning two maf-autopilot agents and having them review each other's output.
2. **Lives in the workflow that needs it.** Today a customer pipes their code OUT to the toolkit. In A2A, the toolkit is just another agent in their existing workflow.
3. **Opens "AI auditing AI" framing.** The single most ChatGPT-able sentence in this whole brainstorm: "My AI agent has a code-quality AI agent reviewing its output before commit." That's a tweet you write.

**Implementation breakdown.**

| Component | Stack | Cost |
|---|---|---|
| New NuGet: `Microsoft.Agents.AI.AutopilotAgent` (or similar) | New csproj in `src/`, multi-targets like the main nupkg | ½ day |
| MCP-tool-to-MAF-function-tool adapter | `Microsoft.Extensions.AI.AIFunctionFactory.Create(method)` per MCP tool | 1 day |
| `MafAutopilotAgent.Create(chatClient)` factory | Returns a `ChatClientAgent` with all the tools wired | ½ day |
| Integration test: spin up the agent in an in-process workflow, audit the 1.3 sample via the agent path | New xUnit project or extension to existing | 1 day |
| Sample: `samples/a2a-demo/` showing two agents (dev + autopilot) in a real workflow | Cloned from 1.3 sample, swap topology | 1 day |
| Docs: new page `docs/a2a-mode.md` explaining the use case | Markdown | ½ day |

Total: ~5 days realistic + a stretch day for polish. Risk: the function-call adapter might need MAF-version-specific tweaks (e.g. if MAF 1.5's tool-call schema differs from 1.3).

---

### Recommended order of execution (if greenlit beyond this session)

```
DAY 0  (today)         ✅ #3 MafAutoFix --all — DONE (already in this session)
DAY 1                  #8 Time-lapse extension (½ day) + autofix-all CLI command (¼ day)
DAY 2-3                #5 AI registry auto-mining BASIC version (refined prompt + verification gate)
DAY 4-5                #4 GitHub bot PoC (Cloudflare Worker spike, hello-world → audit-and-reply)
DAY 6-10               #1 VS Code X-ray mode (BRIDGE version — Code Fix providers on existing analyzer)
DAY 11-15              #15 A2A maf-autopilot (the crazy bet)
DAY 16+                #5 AI registry auto-mining ADVANCED (PR mining)
```

Total: ~3 focused weeks for the full top-5 + the crazy bet, shipped as `maf-autopilot 1.1.0` → `1.2.0` → `1.3.0`.

### My "if I had to pick ONE crazy bet" pick

**#15 — A2A maf-autopilot**. Reasoning: the toolkit currently sits OUTSIDE the MAF runtime, auditing code at rest. Spawning it as a MAF agent that lives INSIDE the workflow inverts that — every other agent gets a self-audit channel. "AI auditing AI" is the most ChatGPT-able framing in this whole list, and it lands directly on the framework's strengths. Risky (~1 week), niche (utility score 5), but the wow + cool ceiling are 10s.

### Adversarial section — things we are NOT doing (deliberately surfaced)

Per the user's "what aren't we doing?" prompt — here's the explicit list of GAPS, separate from the wishlist above:

- ❌ **No runtime instrumentation.** The toolkit reads source. It doesn't ATTACH to a running workflow to catch live silent-starvation. We tell customers "this looks like a fan-in barrier risk" — we don't tell them "this barrier just received 2/3 expected messages."
- ❌ **No localization.** All output is English. A Japanese / German / Spanish maintainer reads the same text. Low priority but invisible until someone asks.
- ❌ **No measurement of saved developer-time.** We have no telemetry, intentionally — but we also have no anecdotal "X customers saved Y hours" stat for marketing.
- ❌ **No "explain like a senior / junior" mode.** Every finding gets one description, regardless of who's reading. A junior might want "what's a fan-in barrier?"; a senior might want "give me the line, I'll handle it."
- ❌ **No conflict detection between agents** (covered as #7 above — not yet done).
- ❌ **No security findings beyond `DefaultAzureCredential` + `EnableSensitiveData`** (covered as #6).
- ❌ **No performance findings beyond token-cap** (covered as #12).
- ❌ **No fuzz testing** of the parsers against weird inputs (covered as #16).
- ❌ **No way for customers to extend the registry** without forking us (covered as #20 — skill marketplace).
- ❌ **No replay / time-travel debugging.** "Show me what this file looked like at MAF 1.2 vs now" isn't a thing we offer.
- ❌ **No what-if migration planning** for unreleased MAF versions (could pair with the watcher).
- ❌ **No analyzer false-positive sweep** against `dotnet/runtime` / `dotnet/aspnetcore` — we've never run our analyzer against large non-MAF codebases to see how it behaves.

Most of these are TODOs the brainstorm table above proposes solutions for. The remaining ones (localization, time-travel debugging) are deliberate non-goals for now.

---

### Phase U — 1.0 polish (post-A.8)

After A.8 ships, the toolkit's stable. These are nice-to-haves for 1.0.x patches.

| ID | Item | Effort | Why |
|---|---|---|---|
| U.1 | Top-level `permissions: {}` defaults in all 6 workflows (Phase N B.M1 deferred) | S (15 min) | Defense-in-depth |
| U.2 | `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: true` in workflows (Node 20 deprecation hits 2026-09-16) | S (15 min) | Forward compat |
| U.3 | Trust-chain quarantine for release-watcher (Phase N B.H2 deferred) — `drafts/` folder + 2-human review on `maf-release` label | M (1 day) | Only material if maf-autopilot ever gets external maintainers; for solo project deferred indefinitely |
| U.4 | Merge `Microsoft.CodeAnalysis.CSharp` bumps (#9, #10) — Roslyn 4 → 5 | M (½ day) | After Phase T proves the analyzer works on real data; bumping infra during validation is risky |
| U.5 | Merge `coverlet.collector` bump (#8) | S (15 min) | Coverage tooling; run tests after |

---

## In-depth recommendations (my reasoning)

### NuGet packaging alignment with AgentEval

The maf-autopilot nupkg's packaging now mirrors the sibling **`C:\git\joslat\AgentEval`** project's house style. This is what's the same, and what's intentionally different:

#### Substantive parallels (✅ all in place)

| Element | maf-autopilot | AgentEval | Same? |
|---|---|---|---|
| TargetFrameworks | `net8.0;net9.0;net10.0` | `net8.0;net9.0;net10.0` | ✅ |
| Central Package Management (CPM) | `/Directory.Packages.props` with `ManagePackageVersionsCentrally=true` + `CentralPackageTransitivePinningEnabled=true` | same | ✅ |
| Shared compiler settings | `/Directory.Build.props` with `LangVersion=latest`, `ImplicitUsings=enable`, `Nullable=enable`, `TreatWarningsAsErrors=false` | same set | ✅ |
| SDK roll-forward | `/global.json` with `sdk.version=8.0.100` + `rollForward: latestMajor` + `allowPrerelease: true` | identical | ✅ |
| Analyzer carve-out | `maf-autopilot.Analyzers.csproj` stays on `netstandard2.0` (Roslyn host requirement, locked) | AgentEval has no analyzer in this project — N/A | ✅ (locked) |
| `<PackageId>`, `<Authors>`, `<RepositoryUrl>`, `<PackageReadmeFile>`, `<PackageLicenseExpression>` | inline in `maf-autopilot.csproj` | mostly in `Directory.Build.props` | ✅ functionally; different placement |
| Container image TFM | `mcr.microsoft.com/dotnet/sdk:8.0` + `runtime:8.0` (net8.0 LTS — smallest LTS image) | AgentEval has no Docker target | N/A |

#### Stylistic differences (NOT applied to maf-autopilot, but could be later)

| Feature | Why AgentEval has it | maf-autopilot status | Recommended? |
|---|---|---|---|
| `<IncludeSymbols>true</IncludeSymbols>` + `<SymbolPackageFormat>snupkg</SymbolPackageFormat>` | Publishes a `.snupkg` alongside the `.nupkg` so consumers can step into source while debugging. | Not yet | Yes — small win for tool-debugger UX. **W-candidate**. |
| `<PublishRepositoryUrl>true</PublishRepositoryUrl>` + `<EmbedUntrackedSources>true</EmbedUntrackedSources>` + `<PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />` | SourceLink integration — debugger can jump to the exact commit's source on GitHub. | Not yet | Yes — same UX benefit. **W-candidate**. |
| `IncludeSubProjectDlls` MSBuild target | AgentEval bundles 6 sub-projects' DLLs INTO the single `AgentEval` nupkg (they're internal, not separately published). | N/A — maf-autopilot is a single-project nupkg | Not applicable. |
| Package metadata in `Directory.Build.props` | DRY across multiple shippable nupkgs in one solution. | maf-autopilot ships TWO nupkgs (server + analyzer); could centralise common fields. | Optional — minor maintenance win. |

**Verdict:** the **substantive** refactor (multi-target + CPM + Directory.Build.props + global.json) is **done**, and that's what makes the project follow the AgentEval pattern. The two stylistic additions (`snupkg` symbols + SourceLink) are easy follow-ups — added as candidate **W.14** below.

---

### TFM strategy

**Previous state:** `net9.0` everywhere (except the analyzer on `netstandard2.0`).
**Current state (post-Phase S):** `net8.0;net9.0;net10.0` multi-target; analyzer stays `netstandard2.0`; Dockerfile on `net8.0` LTS base.

**Why we shouldn't stay on `net9.0`:** .NET 9 is an STS (Standard Term Support) release with **18 months of support — ending ~Nov 2026**. We're in May 2026. `net9.0` is roughly six months from EOL. Any consumer installing `maf-autopilot` next year needs to be on a runtime that's about to lose security patches.

**The corrected recommendation (after cross-checking AgentEval).** My first pass recommended single-target `net8.0`. After auditing AgentEval (your sibling project, same maintainer) I'm revising to **multi-target `net8.0;net9.0;net10.0`**. AgentEval already does this end-to-end and the patterns are proven. Here's the comparison:

| Option | TFM change | Pros | Cons | Verdict |
|---|---|---|---|---|
| **A. Single-target `net8.0` LTS** (my original pick) | `net9.0 → net8.0` | Simple. Broadest minimum-version compat. Smallest container. | Forces net10 consumers to run net8 binaries (missing JIT improvements, missing net9/net10 BCL features). Diverges from AgentEval house style. | ❌ Rejected. |
| **B. Multi-target `net8.0;net9.0;net10.0`** | `net9.0 → net8.0;net9.0;net10.0` | **NuGet picks best TFM at install time** — net8 consumers get net8 binaries, net10 consumers get net10. **Matches AgentEval house style** (proven, validated). When net9 EOLs, drop the middle target — additive, non-breaking. Future-proof. | ~3x build time (~5s → ~15s — negligible). Requires `Directory.Packages.props` (one-time setup). | ✅ **Pick this.** |
| **C. Multi-target `net8.0;net10.0`** (skip net9) | `net9.0 → net8.0;net10.0` | Drops the dying STS preemptively. Still future-proof. | Forces today's net9 users to install net8 runtime to use the tool. AgentEval keeps net9 in the matrix — match that. | Plausible, but unnecessarily divergent. |
| **D. Single-target `net10.0`** | `net9.0 → net10.0` | Newest LTS. Smallest matrix. | Loses every net8/net9 user. .NET 10 is brand-new (~6 months GA). | Premature + restrictive. |

**Recommendation: Option B — multi-target `net8.0;net9.0;net10.0`.**

#### Precedent from AgentEval

The pattern is already field-tested in `C:\git\joslat\AgentEval`:

```xml
<!-- Every csproj — main package, sub-projects, CLI, tests -->
<PropertyGroup>
  <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
</PropertyGroup>
```

Supporting infrastructure (which is what makes multi-target painless):

- **`global.json`** at repo root with `sdk.version=8.0.100` + `rollForward: latestMajor` + `allowPrerelease: true`. Any installed SDK ≥ 8.x builds the project.
- **`Directory.Build.props`** at repo root for shared compiler settings (`LangVersion=preview`, `ImplicitUsings=enable`, `Nullable=enable`, NuGet metadata, repository URL).
- **`Directory.Packages.props`** at repo root with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` + `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>`. **Every `<PackageReference>` in every csproj drops its `Version=`** — versions pin in this one file.
- **Per-TFM conditional references** when a sub-project or package is TFM-locked. AgentEval's `AgentEval.MissionControl` is net10-only (uses Hot Chocolate 16 + `MapStaticAssets`). The CLI references it inside `<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">` only, and the `mc serve` subcommand prints "requires .NET 10" + exits 2 on net8/net9 builds. Same pattern for `Microsoft.AspNetCore.Mvc.Testing` 10.0 in tests.
- **Hosting package lives on the 10.0.x line** — pinned through `Microsoft.Extensions.Hosting.Abstractions 10.0.3` per AgentEval's Directory.Packages.props, with the comment *"Abstractions packages target netstandard2.0/net8.0 — safe for net8/9/10 TFMs."* The 10.0.x nupkg internally ships all three TFMs.

#### What's the same for maf-autopilot

- Roslyn analyzer **stays `netstandard2.0`** — Roslyn requirement, locked. Multi-target doesn't touch it.
- Dockerfile **stays on net8.0 LTS** base images — only the container picks one TFM, and net8 is the smallest LTS image. NuGet consumers still get net8/net9/net10 from the multi-target nupkg.
- All current packages (`ModelContextProtocol 1.2.0`, `YamlDotNet 16.x`, `Microsoft.CodeAnalysis.CSharp 4.11.0`) ship multi-TFM nupkgs that work across net8/9/10. Only the Hosting reference needs the version bump (Dependabot #11).

#### Knock-on Dependabot changes

- **#11 (`Microsoft.Extensions.Hosting 9.0.0 → 10.0.7`)** — was "close" under single-target net8. **Now: MERGE.** The 10.0.x line is exactly what multi-target needs (one nupkg, all 3 TFMs).
- **#1 (`docker dotnet/sdk 9 → 10`)** + **#2 (`docker dotnet/runtime 9 → 10`)** — Dockerfile stays net8 LTS, so these still close. (Alternative posture: bump Dockerfile to net10 to match AgentEval more closely; trade-off is larger container.)

### Dependabot triage

The 12 open Dependabot PRs split into four buckets:

**Bucket 1 — Close** (Dockerfile stays net8 LTS post-Phase-S):

| # | What | Why close |
|---|---|---|
| 1 | `docker dotnet/sdk` 9.0 → 10.0 | Dockerfile stays on net8 LTS (smallest runtime image). Container only picks one TFM. |
| 2 | `docker dotnet/runtime` 9.0 → 10.0 | Same. |

**Bucket 2 — MERGE** (multi-target needs the 10.0.x Hosting line):

| # | What | Why merge |
|---|---|---|
| 11 | `Microsoft.Extensions.Hosting` 9.0.0 → 10.0.7 | The 10.0.x line packs net8 + net9 + net10 TFMs in one nupkg. Exactly what Phase S's multi-targeting needs. AgentEval pins the abstractions on the same 10.0.x line. Note: was "close" in my first pass, **now flipped to merge** after AgentEval cross-check. |

**Bucket 3 — Verify + merge** (low/medium risk, no incompat):

| # | What | Verification |
|---|---|---|
| 3 | `docker/setup-buildx-action` 3 → 4 | Glance at changelog. Used only in `docker-publish.yml`. Merge. |
| 4 | `docker/login-action` 3 → 4 | Same. |
| 5 | `actions/upload-artifact` 4 → 7 | **Has known breaking changes between v4 and v5** (artifact-name defaults). Check changelog for v5/v6/v7 specifically. Used in `maf-release-watcher.yml` and CI runs. |
| 6 | `softprops/action-gh-release` 2.2.1 → 3.0.0 | Phase N **SHA-pinned this to `c95fe14…` with the `v2.2.1` comment**. Dependabot should have updated both the SHA and the comment to a `v3.x` SHA. **Verify both in the PR diff before merging.** |
| 7 | `actions/checkout` 4 → 6 | Scan v5+v6 changelogs. Used in every workflow. High blast radius if it breaks. |
| 12 | `Microsoft.NET.Test.Sdk` 17.12.0 → 18.5.1 | Major bump. Usually backward-compat for xUnit + standard test discovery. Run full test suite after merge. |

**Bucket 4 — Defer to Phase U** (test infra major bumps, risky during validation):

| # | What | Why defer |
|---|---|---|
| 8 | `coverlet.collector` 6.0.2 → 10.0.0 | Four major versions skipped. Coverage tooling. Defer until after Phase T (MAF 1.3 sample) is green — no reason to disrupt coverage during validation. |
| 9 | grouped `Microsoft.CodeAnalysis.Analyzers + CSharp` in analyzer project | Roslyn SDK 4.11 → 5.3. Major version bump — could break the analyzer or its test harness. **See [Analyzer test infra](#analyzer-test-infra-roslyn-4--5) below.** |
| 10 | `Microsoft.CodeAnalysis.CSharp` 4.11.0 → 5.3.0 in Analyzers.Tests | Pairs with #9. Same logic. |

### MAF 1.3 sample as the A.8 unblocker

**The current A.8 wording is too restrictive.** "Wait for an external customer migration" is an indefinite gate — could be weeks, could be never.

**Reframe A.8 as: "validated against ONE real MAF 1.3 codebase end-to-end."** A synthetic sample we control is the same evidence as an external customer's project — provided the sample exercises every canonical 1.3-era anti-pattern that the registry knows about.

**Why this works:**

- A synthetic sample lets us **deliberately tickle every registry entry** (`MAF130-FAN-IN-001`, `MAF130-SESSION-001`, `MAF130-THREAD-001`, `MAF130-EXEC-001`, `MAF130-A2A-001/002`, `MAF130-STREAM-001`, `MAF130-EVENT-001`, `MAF130-ATTR-001/002`, `MAF130-INSTRUCTIONS-001`, `MAF130-EXEC-002`, `MAF130-MIDDLEWARE-001` — all 13 entries).
- An external customer codebase tickles whatever it tickles — coverage is opportunistic.
- The sample lives in the repo as a permanent regression artefact — if Phase T proves migration works against the sample, every future MAF version can be regression-tested by re-running the migration on the same sample with the new toolkit.

**Phase T workflow:**

1. **`samples/maf-1.3-sample/`** — new directory. Contains:
   - `MyApp.csproj` pinned to `Microsoft.Agents.AI 1.3.0` exactly (not `>= 1.3.0`)
   - `Program.cs` — wires up agents using `DefaultAzureCredential` (the wrong way — triggers `MAF-AP-SEC-001`)
   - `Agents/LegacyExec.cs` — uses `ReflectingExecutor<>` + `IMessageHandler<>` + `[StreamsMessage]` (triggers `MAF130-EXEC-001`, `MAF130-ATTR-001`, `MAF130-ATTR-002`)
   - `Workflows/BadTopology.cs` — uses `AddFanInBarrierEdge(target, sources)` wrong-arg-order (triggers `MAF130-FAN-IN-001`)
   - `Sessions/OldSession.cs` — uses `agent.GetNewThread()` + `SerializeSession` (triggers `MAF130-THREAD-001`, `MAF130-SESSION-001`)
   - `Providers/MyContext.cs` — instance fields on `AIContextProvider` (triggers `MAF-AP-CONC-001`)
   - `Tests/` — xUnit tests that the migration must preserve

2. **Phase T.3:** ask Copilot Chat in this repo: `@maf-auditor scan samples/maf-1.3-sample/`. Should produce `samples/maf-1.3-sample/docs/migration-plan.md` with every anti-pattern listed.

3. **Phase T.4:** `@maf-migration execute samples/maf-1.3-sample/docs/migration-plan.md`. Watch each task land. `dotnet build` must be green between tasks.

4. **Phase T.5:** at the end, sample should target MAF 1.4 or 1.5 with the migrations applied. All tests pass. `MafDoctor` returns A or B grade.

5. **Phase T.6:** if anything failed, the gap is in the toolkit (a registry entry's `fix_description` was wrong, or a scanner missed something). Fix in this repo, re-run.

6. **Phase T.7:** the working migration log + green tests = A.8 unblocked. `gh workflow run release.yml -f version=1.3.0` cuts stable.

### Analyzer test infra (Roslyn 4 → 5)

Dependabot #9 + #10 propose `Microsoft.CodeAnalysis.* 4.11.0 → 5.3.0` — a major version bump of the Roslyn SDK that the analyzer NuGet is built against.

**Why defer this to Phase U (after Phase T):**

1. Phase T relies on the analyzer working correctly to validate `MAF001` (fan-out return type), `MAF002` (`DefaultAzureCredential`), `MAF003` (`EnableSensitiveData`). If we bump Roslyn 4→5 first, any analyzer test breakage is intermingled with migration-loop testing — hard to isolate.
2. Roslyn API renames between major versions are common — `SyntaxTree`, `SymbolKind` etc. may need callsite updates.
3. The 11 analyzer tests are good coverage but won't catch *every* API drift.
4. Once Phase T is green, we know the analyzer works on real data. Then we bump the Roslyn SDK and re-run; any breakage is now isolated.

**The right execution order:** Phase R → S → T → cut 1.0 → Phase U (and only then merge #9, #10).

### Agents audit

You flagged that agent changes might not be captured in `next-steps.md`. I checked all 7 agent files (`maf.agent.md`, `maf-migration`, `maf-auditor`, `maf-best-practice-reviewer`, `maf-incident-responder`, `maf-rollback`, `maf-onboarding`) — they're all up-to-date and **the `@maf` primary + 6 specialists architecture is captured in Phase O** (rows O1–O5 in the Done section).

One observation worth flagging: **`maf-migration.agent.md` has a much bigger `tools:` list** than the other agents (it has the full `vscode/*` + `github/*` toolsets; the others have a basic subset). This is intentional — the migration agent needs to push commits, create branches, run notebooks, etc. — but the discrepancy means certain tasks can only be done from `@maf-migration`, not from the `@maf` primary. **If the user wants the primary to route to migration tasks, the user explicitly @-mentions `@maf-migration` to get the bigger toolset.** Per Phase O's design ("no autonomous handoff — primary recommends, user routes"), this is correct.

No agent-level work is needed for 1.0. Agent-level evolution is post-1.0 Phase V territory.

---

## Pre-1.0 ship gate

**Exactly one item is required to cut 1.0:**

| ID | Title | Owner | What unblocks it |
|---|---|---|---|
| **A.8** | Cut stable `1.3.0` to NuGet | maintainer | **Phase T** (MAF 1.3 sample dogfood) provides the validation. After Phase T, run `gh workflow run release.yml -f version=1.3.0`. |

---

## Acceptance criteria for 1.0 announcement

All of the following are **already true** unless marked otherwise:

- ✅ All confirmed security findings remediated. (Phase N)
- ✅ All 17 MCP tools have direct entry-method tests. (Phase G.3)
- ✅ Scaffolder output passes our own scanners (dogfood + compile-validation). (Phase F.4 + I.3)
- ✅ CHANGELOG complete and Keep-a-Changelog-formatted. (Phase G.1)
- ✅ README counts reconciled. (Phase H.1)
- ✅ `MafDoctor` output recommends only extant tools. (Phase H.2)
- ✅ Roslyn analyzer NuGet published path verified in `release.yml`. (Phase N.8)
- ✅ Container runs as non-root. (Phase N.5)
- ✅ Dependabot configured for NuGet + Actions + Docker. (Phase N.9)
- ✅ Auto-update loop validated against real MAF 1.4 + 1.5 data. (Phase Q)
- ⏳ **Phase T green — MAF 1.3 sample successfully migrates end-to-end with `MafDoctor` A/B grade.** (replaces "external migration" — Phase T below)

---

## Deferred — with rationale

Items considered and *intentionally* not done, each with its rationale. Don't re-litigate without reading the reason.

| ID | Item | Why deferred |
|---|---|---|
| B.4 | Split `maf-auditor` into two agents | `maf-best-practice-reviewer` already exists as the sibling. Cosmetic rename of `maf-auditor` → `maf-migration-planner`; defer until post-1.0. |
| B.8 | Project rename (`maf-autopilot` → `maf-keeper` / `maf-forge` / etc.) | Naming exercise produced `maf-forge` (Catchiness 9/10) as leading recommendation. User decision deferred. Rename pre-1.0 is cheaper than post-1.0; if not decided before A.8, ship 1.0 as `maf-autopilot`. |
| M.4 | Mass-rename 6 unprefixed skills to `maf-*` | 38-file blast radius; creates double-prefixed URIs (`maf://skills?name=maf-fan-out-validator`); breaks any hardcoded URI. Current naming is principled (codified in CONTRIBUTING.md "Skill naming convention"). |
| M.5 | Rename `MafNewAgent` / `MafNewExecutor` → `MafScaffoldAgent` / `MafScaffoldExecutor` | "New" is the universal dev-tool verb (`dotnet new`, `cargo new`, `npm init`). CLI is `maf-autopilot new agent`. Breaking surface change for marginal accuracy gain. |
| N.12 (B.M3) | `${{ ... }}` → `env:` pattern enforcement in workflows | Defense-in-depth; no current `pull_request_target` workflow exists. Phase U.1 candidate. |
| N.12 (A.M3) | `IsValidVersion` / `IsValidPackageId` reject leading `-` | Defense-in-depth; `ArgumentList` boundary intact. |
| N.12 (B.H2) | Trust-chain quarantine for release-watcher (`drafts/` folder + 2-human review) | The watcher commits MAF upstream content directly to main. Risk: a compromised MAF NuGet upstream could land malicious content in the embedded `registry.yaml`. **For a solo project, acceptable.** Revisit if maf-autopilot ever gets external maintainers. Phase U.3 candidate. |

---

## Out-of-scope

Things this project has explicitly decided **not** to do:

- **Code execution / sandboxing.** The MCP server reads files, parses them with Roslyn syntax-only, and emits markdown / SARIF. It does NOT execute user code. `MafRunCs0618Hunt` shells out to `dotnet build` — that's compilation, not execution.
- **A general-purpose linter.** Anti-pattern scanner is MAF-specific. If a rule wouldn't bite a MAF user, it doesn't ship.
- **A migration tool for non-MAF agent frameworks.** Other frameworks would need their own toolkits.
- **AI generation of fix patches.** Scanners report findings + cite the registry/constraint. The actual fix is the developer's decision (or Copilot Chat's, using the cited entry).

---

## Annex — May-6 pre-Phase-O sketch

A parallel exploration of the same conceptual ground existed on `origin/main` from 2026-05-06, in snake_case `[McpServerTool(Name = "maf_xxx")]` / bundled-class style. After the Phase H-O work superseded most of it, the May-6 commits were preserved by **git tag `pre-phase-o-may6-sketch`** at SHA `4525ab8`.

### Salvaged into the current codebase (Phase P)

| From May-6 | Ported as |
|---|---|
| `AnalysisTools.maf_executor_pattern_check` | Anti-pattern rule **`MAF-AP-EXEC-001`** |
| `AnalysisTools.maf_compatibility` | New MCP tool **`MafCompatibility`** + drift-tested |
| `MafPrompts.maf-review` / `maf-debug` / `maf-scaffold` | Ported in local PascalCase tool-routing style |

### Deferred to Tier B work (seed material at the tag)

`SamplingTools.*` (full audit, migration suggest, code review, error explain) and `FeedbackTool.maf_open_feedback_issue` — sampling-dependent. Wait until MCP host sampling is widely supported.

### Recover the May-6 tree

```powershell
git checkout -b inspect-may6 pre-phase-o-may6-sketch
# ... explore ...
git checkout main
git branch -D inspect-may6
```

---

## ✅ Done — Phases A through Q (history)

Everything shipped to date, in reverse-chronological order (latest first).

### Phase Q — Auto-update loop validation + AI fill (2026-05-12)

The auto-update loop is **end-to-end validated** on real MAF 1.4 + 1.5 data with GitHub Copilot Coding Agent for AI fill.

| ID | Item |
|---|---|
| Q1 | `release.yml` trigger filter accepts both `v*` and `[0-9]*` shapes |
| Q2 | `maf-release-watcher.yml` direct-to-main commits (replaced PR-based gate) |
| Q3 | Replaced unlisted `dnx` NuGet with direct `dotnet-inspect` global install |
| Q4 | `maf-ai-fill-todos.yml` — dispatches GitHub Copilot Coding Agent to fill TODO placeholders |
| Q5 | GraphQL-based Copilot bot assignment (`addAssigneesToAssignable` with `BOT_kgDOC9w8XQ`) — gh CLI's REST path doesn't work for bots |
| Q6 | Migration-guide banners (Option A) + cumulative `maf-current-migration-guide.md` (Option C) |
| Q7 | `TBD` → `N/A` convention for `guide_section` on new-surface entries |
| Q8 | Compat-matrix backfill for 1.4/1.5: real `Microsoft.Extensions.AI` constraints from NuGet API; documented BYO Azure.AI.OpenAI |
| Q9 | `.github/skills/maf-release-watcher/SKILL.md` rewritten for 3-stage architecture |
| Q10 | End-to-end validation: watcher fired against MAF 1.4 + 1.5; Copilot PR #15 (1.4 fills) merged; PR for 1.5 in flight |

### Phase P — May-6 sketch salvage (2026-05-12)

Cherry-picked the May-6 sketch into the current codebase (`MAF-AP-EXEC-001`, `MafCompatibility`, 3 new prompts); preserved the rest at tag `pre-phase-o-may6-sketch`.

### Phase O — UX + product polish (2026-05-12 late evening)

| ID | Item |
|---|---|
| O1 | `@maf` primary agent for triage (recommendation only — no autonomous handoff) |
| O2 | `MafTour` MCP tool + `maf-help` prompt + `maf://help` resource (drift-tested) |
| O3 | `MafDraftIssue` MCP tool + `maf-issue-reporter` skill (no auto-post) |
| O4 | Auto-update additivity engraved in code + docs + pinned by 2 new tests |
| O5 | Release-watcher re-review — caught 2 bugs (indent regression, grep anchor) |

**Bonus from O4:** the additivity test surfaced a real correctness bug — `RegistryExtractCommand.BuildYamlEntry` emitted column-0 entries that would have malformed `registry.yaml` on first real append. Fixed before shipping.

### Phase N — In-depth security review + fixes (2026-05-12)

Three Opus security reviewers covered input attack surface, supply chain + CI/CD, and code generation + parser safety. **1 false positive caught during synthesis** (EscapeForCSharp Unicode-escape — character-replacement ordering proves it safe). **10 confirmed findings fixed**: scaffolder type-expr injection, ReDoS in ExtractCamelCaseIdentifiers, git-option injection via leading-dash, drive-root traversal, container-as-root, size caps on mcp.json + MAF_REGISTRY_PATH, release.yml test-gate gap, SHA-pin Actions + dependabot.yml. **43 security regression tests** added.

### Phase M — Naming alignment audit (2026-05-12)

Renamed `maf-rollback-agent.agent.md` → `maf-rollback.agent.md` (dropped redundant suffix). Codified skill-naming convention in CONTRIBUTING.md. Considered + rejected: mass-rename 6 unprefixed skills (would create double-prefixed URIs); rename `MafNewAgent`/`MafNewExecutor` → `MafScaffold*` (breaking + marginal gain).

### Phase L — Pull-forward from 1.1 backlog (2026-05-12)

POSIX-correct `SourceFileWalker.MakeRelative` + rule-ID cross-reference in `maf://rules`.

### Phase K — Auto-loaded instructions (2026-05-12)

`maf-testing.instructions.md` (auto-loads on `*Tests.cs`) + `maf-deployment.instructions.md` (auto-loads on `Program.cs` / `Startup.cs` / `Hosting/*.cs` / `Infrastructure/*.cs`).

### Phase J — Production-lifecycle agents (2026-05-12)

`maf-incident-responder` (runtime failure → MAF pattern → deterministic fix), `maf-rollback` (surgical 1.3.0 → 1.2.0 retreat), `maf-onboarding` (personalized codebase tour).

### Phase I — Test-coverage deepening (2026-05-12)

5 hermetic, AAA-structured tests covering the top-leverage gaps: `ParseGitDiffOutput` parser tests, `ResolveBuildTarget` path-resolution tests, scaffolder self-consistency, Doctor 4-scanner composition, Explain namespace-qualified type-use.

### Phase H — Third-Opus blocker fixes (2026-05-12)

3 announcement-blocking regressions surfaced + fixed: README inconsistencies (19→17 tools + SARIF twins), `DoctorTool.cs:240` stale tool reference, `AgentTestTemplate` missing usings.

### Phases F + G — Verdict-driven gap closure + tracking (2026-05-12)

F: SARIF twin consolidation, YamlDotNet refactor, compile-validation test pin (caught 2 real bugs). G: CHANGELOG.md, YAML scalar styles, direct MCP tests for NewAgent/NewExecutor, stale-doc-counts sweep + maf-migrate prompt refresh.

### Phase E — Post-Opus polish (2026-05-12 final)

`PathGuard` shared helper applied to 8 tools, `SourceFileWalker` deduplicates 17 file-walk sites, `MafDoctor` aggregates 4 scanners, README reconciled, `maf://rules` resource auto-generated.

### Phases A through D — Pre-1.0 + post-1.0 polish + strategic + distribution

The original phase plan from the start of the multi-Opus sprint. Most items shipped; rest in [Deferred](#deferred--with-rationale) section above. Detail in [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md).

### Phase 0 — Foundation

3 agents, 12 skills (now 13), instructions, registry skeleton, 21-section migration guide, compatibility matrix, `.maf-version`, README. MCP server primitives (Tools + Resources + Prompts + `init`). 2026-05-11 sprint adding tests, JSONC parser, inverted-index search, 3 new executable tools, best-practice reviewer agent + `MafScanAntiPatterns`. Magic-path sprint: `MafNewAgent`/`MafNewExecutor` scaffolder, Roslyn analyzer companion, `MafSimulateWorkflow`, `MafExplain`. Detail in plan archive.
