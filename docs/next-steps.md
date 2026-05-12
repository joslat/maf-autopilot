# Next steps

> **Last refreshed:** 2026-05-12 — end of Phase Q (auto-update loop end-to-end validated on real MAF 1.4 + 1.5 data).
> **Single source of truth for "what's next."** When this contradicts other docs, this wins.
> **For history of done work:** see the **[`Done`](#-done--phases-a-through-q-history) section at the bottom** of this file, plus the full archive in [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md).

---

## Index

1. [Current state](#current-state)
2. [My recommendations — at a glance](#my-recommendations--at-a-glance)
3. [Active backlog — phased plan](#active-backlog--phased-plan)
   - [Phase R — in-flight + finishing touches](#phase-r--in-flight--finishing-touches-this-week)
   - [Phase S — TFM decision](#phase-s--tfm-decision-the-non-obvious-call)
   - [Phase T — MAF 1.3 sample + migration dogfood](#phase-t--maf-13-sample--migration-dogfood-the-a8-unblocker)
   - [Phase U — 1.0 polish (post-A.8)](#phase-u--10-polish-post-a8)
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

**Current TFMs:** `maf-autopilot` + tests on **`net9.0`**. Analyzer on **`netstandard2.0`** (Roslyn requirement — locked).

**Pre-1.0 gate:** A.8 — "one external migration validates the toolkit end-to-end against a real customer codebase." Currently the only thing blocking the stable cut. **Phase T below proposes a way to unblock this without waiting for an external customer.**

---

## My recommendations — at a glance

1. **Drop TFM from `net9.0` → `net8.0` LTS.** The simplest, broadest, future-proof move. Skips the dying `net9.0` (STS, EOL ~Nov 2026) without paying the multi-target build-time cost. Lets you close 3 Dependabot PRs immediately (#1, #2, #11 — all of which forced .NET 10 ecosystem on us).
2. **Build a MAF 1.3 sample inside `samples/maf-1.3-test-project/`** and dogfood the migration end-to-end (`@maf-auditor` → `@maf-migration` → `MafDoctor`). This **is** the A.8 unblocker — a synthetic-but-real validation we control, ready in 1-2 days instead of waiting for an external customer.
3. **Triage Dependabot PRs surgically.** Close 3 (the .NET 10 forcers), verify-and-merge 6 (low-risk Actions + Test.Sdk), pair-and-defer 2 (Roslyn 4→5 SDK bump — wait until after Phase T to bump anything that could break the analyzer).

Below is the phased plan, then the reasoning behind each call.

---

## Active backlog — phased plan

### Phase R — in-flight + finishing touches (this week)

The cleanup that's already running or close to running. Should land in the next day or two.

| ID | Item | Owner | Effort | Status |
|---|---|---|---|---|
| R.1 | Wait for Copilot's PR for MAF 1.5 (issue #16) and review + merge | YOU | S (5 min) | ⏳ Copilot working |
| R.2 | Close Dependabot PRs #1, #2, #11 (.NET 10 forcers) — they'll be re-openable after Phase S | YOU or me (with consent) | S (5 min) | 📋 |
| R.3 | Merge low-risk Action bumps (#3, #4) after a sanity glance at each diff | YOU | S (10 min) | 📋 |
| R.4 | Verify + merge mid-risk Actions (#5 upload-artifact, #6 softprops, #7 checkout) — see [Dependabot triage](#dependabot-triage) | YOU | M (30 min) | 📋 |
| R.5 | Merge `Microsoft.NET.Test.Sdk` bump (#12) and re-run test suite | YOU or me | S (15 min) | 📋 |

### Phase S — TFM decision (the non-obvious call)

The right time to make this is **before Phase T** (so the sample project picks up the new TFM) and **before cutting 1.0** (so consumers don't have to upgrade through a TFM change later).

| ID | Item | Owner | Effort | Decision needed |
|---|---|---|---|---|
| S.0 | **Decide:** single-target `net8.0`, or multi-target `net8.0;net10.0`, or stay `net9.0` | YOU | — | See [TFM strategy](#tfm-strategy) below |
| S.1 | Bump `src/maf-autopilot/maf-autopilot.csproj` TFM | me | S (5 min) | Depends on S.0 |
| S.2 | Bump test csproj TFMs to match | me | S (5 min) | Same |
| S.3 | Bump `Microsoft.Extensions.Hosting` package version to LTS line (`8.0.x`) | me | S (5 min) | |
| S.4 | Update Dockerfile base images (`mcr.microsoft.com/dotnet/sdk:<NEW>` + `runtime:<NEW>`) | me | S (5 min) | |
| S.5 | Run full test suite locally; CI confirms; cut `1.3.0-alpha-6` with new TFM | me + YOU | M (30 min) | |
| S.6 | After S.5, **re-open and merge** Dependabot #11 (Hosting bump) if you went multi-target with net10; otherwise leave closed | YOU | — | |

### Phase T — MAF 1.3 sample + migration dogfood (the A.8 unblocker)

Currently A.8 says "wait for an external customer migration." That's an indefinite wait. **A synthetic-but-real sample is the same validation in 1-2 days of work I can do autonomously.**

| ID | Item | Owner | Effort | What it proves |
|---|---|---|---|---|
| T.1 | Create `samples/maf-1.3-sample/` — a real .NET project pinned to MAF 1.3.0 | me | S (30 min) | Skeleton |
| T.2 | Hand-author content that uses **every canonical 1.3 obsolete pattern** — `ReflectingExecutor<T>`, `IMessageHandler<TIn,TOut>`, `[StreamsMessage]`/`[YieldsMessage]`, `AddFanInBarrierEdge(target, sources)`, `GetNewThread()`, `SerializeSession`, `DefaultAzureCredential`, instance state on `AIContextProvider`, top-level `Instructions`, etc. | me | M (1-2 hr) | Coverage — the sample tickles every registry entry |
| T.3 | Run `@maf-auditor` against the sample to generate `samples/maf-1.3-sample/docs/migration-plan.md` | YOU (assigned to Copilot Chat) | S (10 min interactive) | Plan generation works on a real codebase |
| T.4 | Run `@maf-migration` against the plan, task-by-task, with `dotnet build` gates | YOU | M (1 hr interactive) | The whole migration loop works |
| T.5 | After T.4, sample should be at MAF 1.4 then 1.5 baseline — verify all tests still pass | me | S (15 min) | Migration is correct end-to-end |
| T.6 | File any gaps in the toolkit (missing registry entries, wrong fix descriptions, broken examples) | me | depends | Toolkit hardening from real-world signal |
| T.7 | **Cut stable `1.3.0` to NuGet** (A.8 unblocked by T) | YOU | S (10 min) | 1.0 ships |

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

### TFM strategy

**Current:** `net9.0` everywhere (except the analyzer on `netstandard2.0`).

**Why we shouldn't stay on `net9.0`:** .NET 9 is an STS (Standard Term Support) release with **18 months of support — ending ~Nov 2026**. We're in May 2026. `net9.0` is roughly six months from EOL. Any consumer installing `maf-autopilot` next year needs to be on a runtime that's about to lose security patches.

**Three options:**

| Option | TFM change | Pros | Cons | My take |
|---|---|---|---|---|
| **A. Drop to `net8.0`** (single target, LTS) | `net9.0 → net8.0` | Broadest consumer reach. `net8.0` LTS until Nov 2026 (matches `net9.0`'s actual EOL — same support window) but `net8.0` continues with extended support whereas `net9.0` is hard EOL. Net8 widely deployed already (the LTS line installed on most build machines). Simple — no multi-target complexity. | "Downgrading" feels backwards. A few .NET 9 APIs would need a `#if NET9_0_OR_GREATER` removal — but we don't use any net9-specific APIs (we checked: just `IHostedService`, `Host.CreateDefaultBuilder`, both stable since net6). | **My recommendation.** Cleanest. |
| **B. Multi-target `net8.0;net10.0`** | `net9.0 → net8.0;net10.0` | Max reach across LTS-to-LTS. Forward-compatible. Lets net10-only consumers pick the net10 flavor. | Build time roughly 3x. Occasional `#if` for divergent APIs. Adds complexity to release.yml's pack step. | Right call **if** you expect non-trivial net10-only consumer base. For a tool, probably overkill. |
| **C. Bump to `net10.0`** (single target, upcoming LTS) | `net9.0 → net10.0` | Newest LTS, supported until Nov 2028. Lets us close fewer Dependabot PRs. | Loses net8 / net9 / et al users. Consumers must have net10 SDK installed. .NET 10 is brand-new — possible early-life bugs. | Premature. Net10 has only been GA a few months. |

**Recommendation: Option A — drop to `net8.0`.**

After this:
- **Close** Dependabot #1 (`docker/sdk 9→10`), #2 (`docker/runtime 9→10`), #11 (`Hosting 9→10`). They're all forcing the .NET 10 ecosystem.
- **`Microsoft.Extensions.Hosting`** stays on `8.0.x` (LTS line). Dependabot will start tracking that line.
- **Dockerfile** changes to `mcr.microsoft.com/dotnet/sdk:8.0` + `runtime:8.0`.

If, post-1.0, the project gets traction and you want multi-target, that's a clean follow-up. The change `net8.0 → net8.0;net10.0` is additive.

### Dependabot triage

The 12 open Dependabot PRs split into three buckets:

**Bucket 1 — Close right now** (`.NET 10` ecosystem forcers, incompatible with net8/net9):

| # | What | Why close |
|---|---|---|
| 1 | `docker dotnet/sdk` 9.0 → 10.0 | We'll target net8 (Phase S). Re-openable when bumping TFM if we ever go to .NET 10. |
| 2 | `docker dotnet/runtime` 9.0 → 10.0 | Same. |
| 11 | `Microsoft.Extensions.Hosting` 9.0.0 → 10.0.7 | Same. After Phase S we'll be on Hosting 8.0.x. |

**Bucket 2 — Verify + merge** (low/medium risk, no incompat):

| # | What | Verification |
|---|---|---|
| 3 | `docker/setup-buildx-action` 3 → 4 | Glance at changelog. Used only in `docker-publish.yml`. Merge. |
| 4 | `docker/login-action` 3 → 4 | Same. |
| 5 | `actions/upload-artifact` 4 → 7 | **Has known breaking changes between v4 and v5** (artifact-name defaults). Check changelog for v5/v6/v7 specifically. Used in `maf-release-watcher.yml` and CI runs. |
| 6 | `softprops/action-gh-release` 2.2.1 → 3.0.0 | Phase N **SHA-pinned this to `c95fe14…` with the `v2.2.1` comment**. Dependabot should have updated both the SHA and the comment to a `v3.x` SHA. **Verify both in the PR diff before merging.** |
| 7 | `actions/checkout` 4 → 6 | Scan v5+v6 changelogs. Used in every workflow. High blast radius if it breaks. |
| 12 | `Microsoft.NET.Test.Sdk` 17.12.0 → 18.5.1 | Major bump. Usually backward-compat for xUnit + standard test discovery. Run full test suite after merge. |

**Bucket 3 — Defer to Phase U** (test infra major bumps, risky during validation):

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
