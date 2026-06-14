---
title: Project Status, Vision & Implementation Plan
last-reviewed: 2026-05-11 (snapshot — 4-Opus initial scan)
last-amended: 2026-05-12 (post-Phase-N — current-state pointer updated; body preserved as snapshot)
reviewers: 4 × Opus deep-review agents (initial scan) + 3 × Opus post-batch reviews + 3 × Opus security reviewers (Phase N)
supersedes: ad-hoc README roadmap (which understates open work)
status: strategy + vision snapshot — the BODY is frozen at 2026-05-11; current state lives in next-steps.md
---

# maf-autopilot — Where We Are, What's Next, and What Just Changed

> ## 📌 Read this first — this doc is a SNAPSHOT
>
> The body of this document is the **strategy + vision snapshot** from the 2026-05-11 4-Opus audit. It captures *what we thought when the sprint started*. The conclusions stand; the **status data is outdated by ~14 phases of work**.
>
> ### For current state — see these instead:
>
> - 🚀 **What's next, deferred, gated?** → [`docs/next-steps.md`](./next-steps.md) — single source of truth.
> - 📋 **Historical phase log + tracking table** → [`docs/maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md) — every phase A-N, every decision narrative.
> - 🏛️ **What are the components?** → [`docs/architecture.md`](./architecture.md) — component map, dependency graph, structural recommendations.
> - 📂 **Full doc catalogue** → [`docs/index.md`](./index.md).
>
> ### What this doc IS still good for
>
> - The vision rationale (§3) — *why* a "permanent co-pilot beyond migration" was the right framing.
> - The naming exercise (§2) — the original `maf-autopilot` choice + the deferred rename decision (now tracked as B.8 in `next-steps.md`).
> - The 5-tier framework (§§5–9) — the design lens that shaped the agents, skills, tools, and analyzers.
> - The architectural capstone reasoning (§10) — why the MCP server became the centrepiece.
>
> Don't trust the "status" / "counts" lines in the body below — they're frozen at **2026-05-11** ("7 executable MCP tools, 151 tests"). Current numbers as of **2026-05-12 evening**: 17 MCP tools, 6 agents, 12 skills, 426 tests, 105/116 plan items done.

---

## 🗺️ Where are the next steps? (canonical answer)

**[`docs/next-steps.md`](./next-steps.md)** is the canonical source. Do not navigate to `maf-migration-toolkit-plan.md § Phased Roadmap` to plan work — that section is a historical archive now.

The 2026-05-11 framing of "Phase A-D as next" was correct at the time but has since been **extended through Phase N**:

- Phase 0 (Foundation) ✅ 38/38 done
- Phase A (Pre-1.0) ✅ 7/8 done — only A.8 (cut stable 1.3.0) remains, gated on external migration validation
- Phase B (Post-1.0 polish) ✅ 7/8 done — 1 intentional deferral (B.8 rename)
- Phase C (Strategic) — 5/9 done — 3 remaining (C.6 full Roslyn data-flow, C.7-C.9 sampling tools)
- Phase D (Distribution) — 3/5 done — 2 remaining (GitHub App, VS Code extension — both multi-day separate projects)
- Phases E-N (Post-Opus polish through security review) ✅ all complete

See `next-steps.md` for the full breakdown.

---

## ⚡ Headline findings (initial scan, 2026-05-11 morning) — both since RESOLVED

Two findings drove the priority order for the sprint that followed. Both **resolved** in subsequent phases. Kept for the historical record:

1. ✅ **`richlander/dotnet-inspect` v0.7.8 ships "Surface [Obsolete] members in listings"** — the exact limitation that motivated half this toolkit's architecture. Was pinned to v0.7.6 in 21 places. **Resolved P0.1** — all 21 references bumped to v0.7.8, attribution corrected, "[Obsolete] caveat" text rewritten across 10 files to reframe as "static vs compiler ground-truth, both have value."

2. ✅ **NuGet was published as prereleases only** — `1.3.0-alpha-1/2/3` live; plain install command would have failed. **Resolved P0.2** — README + setup.md show `--prerelease`. (Subsequent: csproj `<Version>` aligned to `1.3.0-alpha-3` to match NuGet reality, per Phase E.3.)

---

## TL;DR (SNAPSHOT — accurate as of 2026-05-11, NOT current)

> ⚠️ **The status numbers below are frozen.** For current numbers see [`next-steps.md` § Current state](./next-steps.md#current-state).

- **Stage (2026-05-11):** **Late alpha — most of the heavy lifting is done.** 34 of 54 tracked items (63%) implemented; the toolkit now has 3 agents (migration / pre-migration auditor / best-practice reviewer), 12 skills, **7 executable MCP tools**, and **151 passing tests**. NuGet alpha-3 ships; alpha-4 is staged in csproj.
- **Stage (2026-05-12 — current):** **Feature-complete for 1.0, gated on external validation.** 105 of 116 tracked items done; 17 MCP tools; 6 agents (3 production-lifecycle agents added in Phase J); 12 skills + 3 auto-loaded instruction files; 426 passing tests; 3 Roslyn analyzers shipping as a separate NuGet; multi-arch Docker image on GHCR; full security audit complete (Phase N).
- **What works (2026-05-11 framing — still true):** the migration loop is mature, the **best-practice reviewer is real** (no longer aspirational — `MafScanAntiPatterns` runs 6 Roslyn/regex rules covering security, concurrency, observability), `dotnet-inspect` is on 0.7.8 with `[Obsolete]` surfacing, the `release.yml` NuGet pipeline is proven, the test harness has already caught 4 latent bugs (case-sensitivity, JSONC silent-quarantine, `DiffPackageTool` parser, `MatchToRegistry` false positives).
- **What was still aspirational at 2026-05-11 (NOW DONE):** stable 1.3.0 release (still gated on external migration validation — A.8); CI auto-update of `registry.yaml` (#30 — done in Phase C.1); the release-watcher's known bugs (done in A.6); 9 missing registry areas (closed in A.2 + later); the rename decision (deferred to B.8 / 1.0.x — naming exercise returned `maf-forge` 9/10).
- **Identity:** the project HAS outgrown "just a migration tool" — and now the *code* matches the *prose*. Rename decision remains deferred (`next-steps.md` B.8). The current name's only baggage is the slight negative of "autopilot" (failure mode connotations); the rename will be a focused PR pre-1.0 if it happens.

---

## 1. Plan-stage assessment

The canonical truth is `docs/maf-migration-toolkit-plan.md` (32-row tracking table), not the README's tick-list.

| Tier | Plan items | Status | Evidence |
|---|---|---|---|
| **Tier 1** — Fix obvious gaps (cs0618-hunter, nuget-diff-analyzer) | #4, #11 | **DONE** | Both SHIPPABLE-grade |
| **Tier 2** — Self-updating guide (release-watcher, version-keyed sections, workflow YAML) | #12, #15, #20 | **DONE** | Workflow runs Mondays 09:00 UTC; 25 sections tagged |
| **Tier 3** — Auto-generated plans (auditor, plan-creator) | #2, #8 | **DONE** | Pre-migration only; see §4 |
| **Tier 4** — Runtime verification (smoke-tester, fan-in static analyzer) | #9, #10, #13 | **DONE** *(caveats)* | Smoke-tester templates not yet compile-verified against MAF 1.3.0 |
| **Tier 5** — Reflexive learning | #14 | **DONE** | `migration-retrospective` skill |
| Tier 5 — `maf_open_feedback_issue` tool | #31 | **PENDING** | Not started |
| **MCP core** — MVP + resources + prompts + init | #21–#24 | **DONE** | 3 tools, 4 resources, 3 prompts, working `init` |
| MCP — **NuGet publish (prerelease)** | #25 | **PARTIAL** ✅ | 3 alphas live (`1.3.0-alpha-1/2/3`); pipeline proven; csproj `<Version>` stale; no stable release yet |
| MCP — sampling tools | #26 | **PENDING** | Not started |
| MCP — remaining six analysis tools | #27 | **PENDING** | Major gap |
| **CI** — registry.yaml auto-update | #30 | **PENDING** | Largest gap in the self-upgrade story |
| Distribution — Docker GHCR | #28 | **PENDING** | Not started |
| Roslyn analyzer companion | #29 | **PENDING** | Not started |
| Multi-version migration paths | #32 | **PENDING** | Not started |

**Overall plan completion: ~78% (25/32 rows).** **Functional completion of the README's promised behaviour: ~70%** — migration story is genuinely strong; co-pilot / audit / self-upgrade story is partial.

### README ↔ plan contradictions (must fix)

1. **Install command is wrong for prereleases.** README lines 5 + 155 + 31–40 show `dotnet tool install --global maf-doctor`. NuGet has only prereleases (`1.3.0-alpha-3` latest). Plain command **fails** without `--prerelease` flag or explicit `--version 1.3.0-alpha-3`. First user following Mode 1 hits an "no compatible packages" error.
2. **csproj version drift.** `maf-autopilot.csproj:15` reads `<Version>1.3.0-tool.1</Version>` — never actually pushed. Real history on nuget.org is `1.3.0-alpha-1/2/3`. release.yml's `workflow_dispatch` input overrides the csproj, masking the drift.
3. **Roadmap divergence.** README ticks "version-keyed guide sections" inconsistently; plan + guide confirm done.
4. **Under-reported open work.** README's roadmap lists 4 pending items; plan tracks 7+ (missing: #25 stable release, #26, #27, #30, #31).
5. **.NET SDK mismatch.** `setup.md` says .NET 9; `maf-release-watcher.yml` uses `dotnet-version: '10.0.x'`; `release.yml` uses `9.0.x`. Pick one.
6. **Wrong upstream attribution.** README acknowledges `filipw/dotnet-inspect` — actual repo is `richlander/dotnet-inspect`.
7. **Wrong GitHub repo in watcher.** `gen_guide_section.py` queries `microsoft/agents` for release notes; should be `microsoft/agent-framework`. Hidden by `continue-on-error: true`.

---

## 2. What works (verified)

| Component | Verdict |
|---|---|
| MCP server build + DI + embedded-resource packaging | Clean. `PackAsTool=true`, `PackageReadmeFile`, version `1.3.0-tool.1` |
| `MafApiSafety` / `MafRegistryLookup` / `MafRegistryList` tools | Functional; sub-millisecond lookups |
| `maf://constraints` / `maf://guide` / `maf://registry` / `maf://skills?name=` | All four resource endpoints serve embedded content |
| Three prompts (`maf-audit`, `maf-migrate`, `maf-cs0618-hunt`) | Wired and discoverable |
| `init` command | Polished UX, idempotent for happy path |
| `maf-migration` agent build-verified loop | Disciplined `dotnet build` → fix → repeat with tracking-table update after every task |
| `maf-auditor` → `migration-plan-creator` | Contract is tight; auditor output consumed verbatim by migration agent |
| Skills as documents | 10 of 11 SHIPPABLE; no dead cross-references; YAML frontmatter valid |
| `obsolete-api-registry/registry.yaml` | 10 well-formed entries, 15 fields each, all populated |
| `maf-migration-guide` 21-section navigator | Smart index pattern keeps context lean |
| Release-watcher workflow | Runs weekly, opens a PR (with caveats below) |
| `release.yml` | NuGet push wired; version override via input/tag works |

---

## 3. What v0.7.8 of `dotnet-inspect` changes (foundational)

### The fact
- **v0.7.8 (2026-05-04)**: `Surface [Obsolete] members in listings (#316)`.
- **v0.7.7 (2026-05-04)**: `Fix #310: skip non-HTTP NuGet sources instead of crashing` — also worth picking up.
- **v0.7.5 (2026-03-31)**: documents `--mermaid` flag for diagram output (`depends` command renders Mermaid).
- This repo pins **0.7.6** in **21 places**: `.github/workflows/maf-release-watcher.yml`, `.github/agents/maf-auditor.agent.md`, the dotnet-inspect skill, nuget-diff-analyzer skill, maf-release-watcher skill, the migration guide, and the plan doc.

### Direct consequences in this toolkit

| Today (pre-bump) | After bumping to 0.7.8 |
|---|---|
| `cs0618-hunter` is the **only** way to detect obsolete APIs; documented as a workaround for the `dotnet-inspect` gap | `cs0618-hunter` becomes the *runtime/compiler ground-truth* path; `dotnet-inspect` becomes a viable *pre-build static* path. Both keep value (different use cases) |
| README, dotnet-inspect SKILL.md, copilot-instructions, migration guide all carry the "⚠ never use dotnet-inspect alone for obsolete" warning | The warning text must now read **"as of v0.7.7 and earlier"** and explain v0.7.8 closes the gap |
| `nuget-diff-analyzer` post-processes a diff that *misses* per-overload obsoletions | Diff signal becomes richer; `nuget-diff-analyzer` should consume the new annotations |
| **#30** registry.yaml auto-update requires synthesizing a test project + running `dotnet build` to harvest CS0618 — complex CI work | **#30 becomes much simpler**: ask `dotnet-inspect` for all `[Obsolete]` members in the new MAF version, diff against the registry, append YAML rows. No build needed |
| `maf-visualize` topology diagrams ("gold" idea, see §8.4) requires custom Mermaid emission from `fan-in-static-analyzer` | `dotnet-inspect depends --mermaid` already gives package-level diagrams; combine with our static analyzer for executor-level diagrams |
| Maf-release-watcher silently masks `dotnet-inspect` crashes via `continue-on-error: true` (the 0.7.7 fix to crash on non-HTTP NuGet sources implies prior versions did crash) | Crash class fixed; `continue-on-error` can be tightened to surface real failures |

### What you must do
1. Bump every pinned reference to `dotnet-inspect@0.7.8` (or unpin to `@latest` for skills, keep a pin only in CI).
2. Fix author attribution in `README.md` and the dotnet-inspect skill (`filipw` → `richlander`).
3. Rewrite the "Obsolete caveat" paragraph in: `README.md` (~line 122 + ~line 167), `.github/skills/dotnet-inspect/SKILL.md`, `.github/copilot-instructions.md`, `.github/instructions/maf-constraints.instructions.md`, `guides/maf-1.3.0-migration-guide.md`.
4. Re-architect **#30** around `dotnet-inspect`'s new capability instead of synth-build approach.
5. Add a "Read upstream release notes before designing workarounds" line to the project memory / contributing guide. This entire toolkit was built around a gap that the upstream tool then closed in two minor releases.

---

## 4. What does not work (or is misleading)

| Issue | Impact | File:Line |
|---|---|---|
| Install command in README will fail (prereleases need `--prerelease`); csproj `<Version>` stale | Mode 1 quick-start errors out on the very first command | `README.md:31-40,155`; `maf-autopilot.csproj:15` |
| `init` wipes unknown servers from `.vscode/mcp.json` on parse failure | Silent data loss for users with other MCP servers | `InitCommand.cs:64-69` |
| `RegistryService.SearchByApiName` does not search `ExampleAfter`, `Package`, or `Id` | Searches like "CreateSessionAsync" or "Microsoft.Agents.AI.Workflows" miss legitimate hits | `RegistryService.cs:47-56` |
| Zero tests | The recent `AgentThread` returning SAFE bug shipped without detection (fixed in 393b7a2) | repo-wide |
| Release watcher queries wrong GitHub repo | Release-notes always empty, silently | `maf-release-watcher.yml` |
| `update_compat_matrix.py` regex anchored to `**1.3.0**` | Second future run will mis-order rows | `.github/scripts/update_compat_matrix.py` |
| `gen_guide_section.py` produces a stub (dumps first 50 diff lines + four TODO sections) | Self-upgrade output is a template, not a guide | `.github/scripts/gen_guide_section.py` |
| Watcher *appends* to `maf-1.3.0-migration-guide.md` without idempotency | Repeat runs duplicate sections; 1.4.0 guide is still named 1.3.0 | `maf-release-watcher.yml` |
| `registry.yaml` is **not** auto-updated | The most important file is fully manual | watcher PR body lists as manual TODO |
| `maf-auditor` only finds *pre-1.3.0* patterns | Pointed at a clean 1.3.0 codebase reports "nothing to migrate" — no drift detection | `maf-auditor.agent.md` Phase C grep list |
| 1 of 11 skills (9%) is executable as an MCP tool | The other 10 require the LLM to do all the work | `src/maf-autopilot/Tools/` |
| Three "hard rules" in constraints have no enforcement skill | `DefaultAzureCredential`, `EnableSensitiveData=true`, instance state in `AIContextProvider` | `maf-constraints.instructions.md` |
| Tool names mixed snake/PascalCase across docs | `MafApiSafety` (real) vs `maf_api_safety` (docs) | `ApiSafetyTool.cs:84`, `MafPrompts.cs:39-40` |
| 9 of 21 guide sections have no registry entries | Memory, middleware, tool-approval, observability, DevUI, structured output, source-gen, instructions placement, agent creation | `registry.yaml` |
| `fan-out-validator` ↔ `fan-in-static-analyzer` overlap ~70% | Two skills, one concept | `.github/skills/` |
| `workflow-smoke-tester` templates not compile-verified | May ship dead code | `workflow-smoke-tester/SKILL.md` |

---

## 5. The vision drift — and the name

The README itself names the drift:

> *"Not just a migration tool — your permanent AI co-pilot for MAF."*
> *"Best-practice auditing, not just migration — reviews your MAF agents and workflows against current patterns after migration too."*

But every skill, every agent prompt, every tracking-table row is migration-flavoured. The steady-state identity exists only on the marketing surface.

### Naming

`maf-autopilot` is **defensible but no longer the most accurate**. "Autopilot" suggests autonomous execution; reality is that GitHub Copilot does the flying and this tool is the chart, the radar, ATC, and the maintenance handbook.

| Candidate | Rationale | Risk |
|---|---|---|
| **`maf-keeper`** | Captures permanent-maintenance scope: keeps code current, guide current, registry honest. Best fit for "post-migration drift detection + self-update". | Slightly less catchy. |
| **`maf-copilot`** | Matches reality — it augments Copilot. Honest about human-in-the-loop. | Brand collision with Microsoft / GitHub "Copilot". |
| **`maf-steward`** | Implies ongoing custodianship: audit + migrate + maintain. | Less punchy. |

If the project keeps `maf-autopilot`, the README needs one sentence: *"The name means 'MAF on autopilot with Copilot at the controls,' not 'fully autonomous migration.'"*

### Agent renaming

Split `maf-auditor` into two:
- **`maf-migration-planner`** (today's behaviour — finds *old* patterns, writes migration plan).
- **`maf-best-practice-reviewer`** (new — assumes code is on 1.3.0, finds drift / anti-patterns / security / observability gaps; writes `audit-report.md` not `migration-plan.md`).

This single split materializes the "co-pilot, not just migration tool" identity in code, not just prose.

---

## 6. All pending tasks (consolidated, with priority)

### P0 — blockers, must do before anything else

- **P0.1 — Bump `dotnet-inspect` 0.7.6 → 0.7.8 in 21 places.** Fix author attribution (`filipw` → `richlander`). Rewrite the "Obsolete caveat" paragraphs everywhere. Treated in §3. *This is the single highest-leverage change in the whole plan — it changes the design of `cs0618-hunter`, `nuget-diff-analyzer`, the watcher's registry-update step (#30), and the `maf-visualize` "gold" idea (which becomes free via `--mermaid`).*
- **P0.2 — Fix the install command + version drift.** README's `dotnet tool install --global maf-doctor` fails today because all three published versions are alphas. Two parallel actions: (a) update README install command to `dotnet tool install --global maf-doctor --prerelease` (and link the [nuget.org/packages/maf-doctor/1.3.0-alpha-3](https://www.nuget.org/packages/maf-doctor/1.3.0-alpha-3) page); (b) bump `maf-autopilot.csproj:15` `<Version>` to the next release (`1.3.0-alpha-4` or `1.3.0`) so the csproj stops lying about what's been shipped.
- **P0.3 — Add a tests project for the MCP server.** Cover the field-by-field bugs that have already shipped (the `AgentThread` regression in commit `393b7a2` would have been caught by one test).

### P1 — critical path to credible beta
- **P1.1** — Fix the `init` JSON-clobber bug (`InitCommand.cs:64-69`). Back up to `.vscode/mcp.json.bak`, fail loudly, offer `--force`.
- **P1.2** — Expand `SearchByApiName` to include `ExampleAfter`, `Package`, `Id`. Better: build an inverted-index of all string fields at startup.
- **P1.3** — Reconcile README roadmap with plan tracking table (generate README from plan, or delete README roadmap).
- **P1.4** — Add CONTRIBUTING.md and TROUBLESHOOTING.md.
- **P1.5** — Standardize tool names (PascalCase everywhere or snake_case everywhere — pick one and align docs).
- **P1.6** — Fix .NET SDK version mismatch (setup.md vs workflow YAML).

### P2 — close the gap to the README's promise
- **P2.1** — Wire at least three skills as executable MCP tools:
  - `MafRunCs0618Hunt(projectPath)` — shells `dotnet build`, parses warnings, cross-references registry.
  - `MafValidateFanOut(filePath | repoPath)` — Roslyn-based static check for `void` handlers in fan-out executors.
  - `MafDiffPackage(packageId, oldVersion, newVersion)` — wraps `dnx dotnet-inspect@0.7.8 -- diff` and post-processes via `nuget-diff-analyzer` logic.
- **P2.2** — Add `maf-best-practice-reviewer` agent + `maf-anti-pattern-scanner` skill. Cover `DefaultAzureCredential`, `EnableSensitiveData=true`, instance state in `AIContextProvider`, missing `UseOpenTelemetry`, missing `ManagedIdentityCredential`. These already exist as bullets in constraints — make them *runnable*.
- **P2.3** — Expand `registry.yaml` to cover the 9 missing API areas (memory, middleware, tool approval, observability, DevUI, structured output, source-gen, instructions placement, agent creation).
- **P2.4** — Compile-validate `workflow-smoke-tester` templates against a real pinned MAF 1.3.0 reference project in this repo.
- **P2.5** — Merge `fan-out-validator` + `fan-in-static-analyzer` into one skill with a "policy" + "procedure" structure.

### P3 — close the gap to "the repo upgrades itself"
- **P3.0** — Cut the first stable `1.3.0` (non-prerelease) NuGet release once P0 + P1 + P2 are green. Today the only versions on NuGet are alphas; until a stable ships, the README's plain `--global maf-doctor` install will keep failing.
- **P3.1** — Implement plan #30 (registry.yaml auto-update). With v0.7.8 this is now a 1-day job: ask `dotnet-inspect` to list `[Obsolete]` members in the new MAF version, diff against the existing registry, append YAML rows.
- **P3.2** — Fix the watcher: wrong GitHub repo (`microsoft/agents` → `microsoft/agent-framework`); hard-coded `1.3.0` regex anchor in `update_compat_matrix.py`; missing prerelease filter; missing rate-limit retry; missing idempotency on guide appends.
- **P3.3** — Per-version guide files. Stop appending to `maf-1.3.0-migration-guide.md`; create `guides/maf-1.4.0-migration-guide.md` per new minor; update the migration-guide skill to version-route lookups.
- **P3.4** — Major-version detection / escalation path. Today `1.3.0 → 2.0.0` is treated identically to `1.3.0 → 1.3.1`.

### P4 — strategic additions that match the broader vision
- **P4.1** — `pr-diff-auditor` skill: audit only files changed in current PR/branch.
- **P4.2** — `drift-detector`: scheduled scan of user's repo flagging when newly-merged code regresses from MAF best practices.
- **P4.3** — `pre-upgrade-dry-run`: simulate what would break before any code is modified.
- **P4.4** — `maf_open_feedback_issue` tool (plan #31).
- **P4.5** — Docker GHCR distribution (plan #28).
- **P4.6** — Roslyn analyzer companion (plan #29).
- **P4.7** — Multi-version migration paths (plan #32).

---

## 7. Implementation plan — two-week sprint

**Day-by-day, owner-agnostic, runnable from this document.**

### Day 1 — Foundations (P0.1 + P0.2)
1. **Bump dotnet-inspect 0.7.6 → 0.7.8** in all 21 references (P0.1):
   - `.github/workflows/maf-release-watcher.yml` (2 lines)
   - `.github/agents/maf-auditor.agent.md` (2 lines)
   - `.github/skills/dotnet-inspect/SKILL.md` (7 lines)
   - `.github/skills/maf-release-watcher/SKILL.md` (1 line)
   - `.github/skills/nuget-diff-analyzer/SKILL.md` (3 lines)
   - `guides/maf-1.3.0-migration-guide.md` (5 lines)
   - `docs/maf-migration-toolkit-plan.md` (1 line)
2. **Fix author attribution.** README §Acknowledgements: `filipw/dotnet-inspect` → `richlander/dotnet-inspect`.
3. **Rewrite "Obsolete caveat" text** in README, dotnet-inspect SKILL.md, copilot-instructions.md, maf-constraints.instructions.md, migration guide. Reframe as: "as of `dotnet-inspect` v0.7.8 the gap is closed; `cs0618-hunter` is retained as the compiler ground-truth path."
4. **Fix install command + version drift** (P0.2):
   - README: change `dotnet tool install --global maf-doctor` → `dotnet tool install --global maf-doctor --prerelease` and note "Currently published as alpha; stable 1.3.0 coming once the P0–P2 items below are green." Link `https://www.nuget.org/packages/maf-doctor`.
   - `maf-autopilot.csproj:15`: bump `<Version>` to `1.3.0-alpha-4` (next prerelease) so csproj stops lying.
   - Also update the install snippet in `docs/setup.md`.
5. **Reconcile README roadmap with plan.** Either delete the README roadmap or regenerate it from the plan's tracking table.

### Day 2 — Cut alpha-4 + identity
6. **Cut `1.3.0-alpha-4` to NuGet.** Trigger `release.yml` via `workflow_dispatch` with `version=1.3.0-alpha-4`. Verify `dotnet tool install --global maf-doctor --prerelease` works on a clean machine.
7. **Decide on rename.** If yes: rename to `maf-keeper` (recommended) — package ID + repo + tool command. Provide a deprecated `maf-autopilot` shim package that points users to the new ID for one minor version. **If renaming, do it before stable 1.3.0** — much cheaper than renaming after.

### Day 3–4 — Tests + bug fixes
7. **Add `src/maf-autopilot.Tests/` project** (P0.3). 20 tests minimum:
   - Registry deserialization happy path.
   - Every field `SearchByApiName` must touch (Notes, FixDescription, ExampleBefore, ExampleAfter, Package, Id).
   - `FindById` casing (exact and case-insensitive).
   - `MafResources.GetSkill` allowlist (valid + invalid + path traversal attempts).
   - Malformed YAML at startup — must return a graceful error, not crash on first tool call.
   - `InitCommand` idempotency.
   - `InitCommand` against a malformed `.vscode/mcp.json` — must **NOT** clobber other servers' entries; must back up + fail loud.
8. **Fix `init` JSON-clobber bug** (P1.1).
9. **Expand `SearchByApiName`** to all string fields (P1.2). Build an inverted-index at construction time for `O(1)` token lookups.
10. **Add CONTRIBUTING.md + TROUBLESHOOTING.md** (P1.4).

### Day 5–6 — Three executable MCP tools
11. **`MafRunCs0618Hunt(projectPath)`** (P2.1.a). ~150 LOC. Shells `dotnet build`, parses `warning CS0618` / `error CS0246`, joins to registry by signature match, returns structured `[{file, line, code, registryId?, fixPattern?}]`.
12. **`MafValidateFanOut(filePath | repoPath)`** (P2.1.b). Roslyn SyntaxTree walk; identifies `[MessageHandler]` methods on fan-out executors; reports any returning `void` or `Task` (instead of `Task<T>`).
13. **`MafDiffPackage(packageId, oldVersion, newVersion)`** (P2.1.c). Wraps `dnx dotnet-inspect@0.7.8 -- diff`; consumes the new `[Obsolete]` annotations; returns categorised + registry-cross-referenced report.

### Day 7–8 — Best-practice reviewer (the identity unlock)
14. **`maf-best-practice-reviewer.agent.md`** (P2.2). Phase A: detect anti-patterns from constraint list. Phase B: produce `audit-report.md` (not migration-plan.md). Phase C: categorize findings (critical / important / nice-to-have).
15. **`maf-anti-pattern-scanner` skill**. Concrete grep + Roslyn scan list. Outputs file-line-pattern triples that the agent uses for the report.
16. **Add anti-pattern entries to `registry.yaml`.** New `severity` field (`error | warning | info`) and `category` field (`migration | best-practice | security | observability`). Backfill 9 missing guide sections (P2.3).

### Day 9 — Self-upgrade hardening
17. **Implement plan #30 (registry auto-update)** (P3.1). With v0.7.8 in place: in the release-watcher workflow, after detecting a new MAF version, run `dotnet-inspect type --package Microsoft.Agents.AI@$NEW --members-only` and filter for `[Obsolete]`; for each new obsoletion not already in `registry.yaml`, append a draft YAML row with `verified: false`. The Coding Agent step fills in `fix_description` and `example_after`.
18. **Fix the watcher bugs** (P3.2): wrong repo, hard-coded `1.3.0` regex, missing prerelease filter, missing rate-limit retry, missing guide-append idempotency.
19. **Per-version guide files** (P3.3): `gen_guide_section.py` should create `guides/maf-$VERSION-migration-guide.md` rather than append to the 1.3.0 file. Update the `maf-migration-guide` skill to version-route lookups.

### Day 10 — Verify end-to-end + cut stable
20. **Run the full loop on a synthetic MAF 1.3.1-preview.1 release** (push a row to a test branch of `.maf-version`). Verify: watcher detects, opens PR, registry has new draft rows, compat matrix has a new row, new guide file is created (not appended), MCP server tests still pass.
21. **Capture findings into `migration-retrospective`** and the plan.
22. **Cut stable `1.3.0` (not alpha) NuGet release** if all 10 acceptance-criteria boxes in §9 are ticked. Triggers `release.yml` via `workflow_dispatch` with `version=1.3.0` — or, preferred, push a `v1.3.0` git tag so the workflow also creates a GitHub Release.

---

## 8. Out-of-the-box: features that would be gold

These go beyond the current plan. Pick 3.

1. **`maf-doctor` subcommand.** `maf-doctor doctor` runs every analyzer and emits a single health-score (`A`/`B`/`C`/`F`) with the top three improvements. One command, instant value, perfect demo.

2. **`maf-new` — agent/workflow scaffolder.** `maf-doctor new agent ChatBot` generates a clean MAF 1.3.0 agent with executor class, `[MessageHandler]`, fan-in/fan-out, sample smoke test, OTel wired, `ManagedIdentityCredential` defaulted. Today the toolkit assumes legacy code exists; this addresses greenfield projects.

3. **Roslyn analyzer companion** (plan #29 — promote priority). Flags fan-out `void` handlers at write-time in the IDE. Live red-squigglies for the bug class the README highlights as the killer feature.

4. **`maf-visualize` — leveraging v0.7.5's `--mermaid` flag.** Combine `dotnet-inspect depends --mermaid` (package-level) with our `fan-in-static-analyzer` (executor-level) to render full workflow topology. Free upstream + small custom layer.

5. **`maf-cost`.** Token-cost auditor. For each `RunAsync` call: estimate input/output token cost; flag agents with no caching, no `MaxTokens`, no token limits. Production-cost governance is the unsexy enterprise lock-in feature.

6. **`maf-eval` — semantic test scaffolding.** Distinct from structural smoke tests: golden inputs, expected response shape, regression tests against model output drift. The missing piece between "compiles green" and "behaves correctly."

7. **`maf-explain`** — paste any MAF code snippet, get an annotated explanation referencing the canonical guide section. Reverse of the current flow; useful for onboarding and PR comprehension.

8. **VS Code extension shell.** Wraps the MCP server with a one-click installer, surfaces `maf-doctor` as a status-bar item, opens findings in the Problems panel. MCP-over-stdio is solved; UX is the gap.

9. **`maf-incident-replay`** — given a fan-in silent-failure incident (logs + topology), replay against the static analyzer and prove which return type would have caught it at build time. Turns post-mortems into registry entries automatically.

10. **GitHub App** — `@maf-bot` PR comments. Every PR opened against a MAF project gets an automatic audit comment (diff-scoped). Pairs with `pr-diff-auditor`. Org-wide adoption vector.

11. **Telemetry / accuracy dashboard.** The toolkit makes claims ("catches silent failures"); zero data backs them up today. Capture (opt-in) findings per repo, false-positive rate, time-to-migrate, fix-acceptance rate. Use it to prove the claims and prioritize.

12. **`maf-onboard`** — 5-minute interactive tutorial. Clones a sample MAF project, walks new contributors through one fix using the toolkit, leaves a working dev loop.

---

## 9. Acceptance criteria — when can we call this "beta"?

- [x] `dotnet-inspect` bumped to 0.7.8 everywhere; "Obsolete caveat" text rewritten; upstream author attribution corrected *(P0.1)*
- [x] **NuGet pipeline proven** (3 alphas live; `release.yml` works)
- [ ] **Stable `1.3.0` (non-prerelease) published** — gated on at least one external migration
- [x] csproj `<Version>` matches the latest published version (no drift) *(P0.2 — bumped to 1.3.0-alpha-4)*
- [ ] At least one external user has successfully migrated a real project
- [x] MCP server has tests; CI green on PRs *(P0.3 + #41/#42 batches — **151 tests passing**, wired into release.yml. Harness has caught 4 latent bugs: case-sensitive resource lookup, JSONC silent-quarantine, `DiffPackageTool` parser format wrong, `MatchToRegistry` matching replacement text.)*
- [x] `init` no longer clobbers existing `.vscode/mcp.json` entries *(plan #36 — timestamped backup + JSONC-tolerant parser + retry-on-collision)*
- [x] README roadmap matches plan tracking table *(plan #38 — README defers to plan as SSOT)*
- [x] At least three skills are executable as MCP tools, not just documents *(plan #41 — **7 executable tools now**: `MafApiSafety`, `MafRegistryLookup`, `MafRegistryList`, `MafValidateFanOut`, `MafRunCs0618Hunt`, `MafDiffPackage`, `MafScanAntiPatterns`)*
- [x] `maf-best-practice-reviewer` exists and can audit a clean 1.3.0 codebase *(plan #42 — agent + skill + executable scanner tool)*
- [ ] Release-watcher has run end-to-end against at least one new MAF release and produced a PR a human would merge
- [x] CONTRIBUTING.md and TROUBLESHOOTING.md exist *(plan #40)*

**9 of 12 ticked → beta-ready in code; gated only on stable release + one external migration + release-watcher real-world run.**

**Progress as of 2026-05-11 (evening):** 9 of 12 ticked. The three remaining boxes are all "validate against the real world" gates, not "build more code" gates. The toolkit is functionally beta — what's missing is operational maturity (one external migration, one watcher cycle), not features.

---

## 10. What landed in the 2026-05-11 sprint

A single multi-batch session, driven by the four parallel deep-review audits in §§1–6 of this document. Each batch was followed by an Opus review; review findings folded forward.

### Batch P0 — foundations (blockers)
- **#33** dotnet-inspect 0.7.6 → 0.7.8 in 21 places; author attribution corrected; "[Obsolete] caveat" text rewritten in 10 files to reframe as static-vs-compiler complementary paths.
- **#34** README install command adds `--prerelease`; csproj `<Version>` bumped to `1.3.0-alpha-4`.
- **#35** New xUnit test harness (`src/maf-autopilot.Tests/`). Started at 48 tests; grew to 151 across the sprint. Wired into `release.yml` before pack step.

### Batch P1 — bug class elimination + agentic surface expansion
- **#36** `InitCommand` JSON-clobber fix: VS Code's `mcp.json` is JSONC (comments + trailing commas) — lenient parser; on true parse failure, timestamped + GUID-suffixed backup with retry-on-collision. 6 tests guard the contract.
- **#37** `RegistryService.SearchByApiName` rewritten as pre-built inverted-haystack over 12 string fields. Adding a new field is a one-line change in `BuildHaystack`. Eliminates the "missing-field-in-OR-chain" bug class. 11 new tests guard the coverage of `ExampleAfter`, `Package`, `Id`, `CsWarning`, `GuideSection`.
- **#38** README roadmap deleted; replaced with pointers to plan + status doc as single source of truth.
- **#39** Tool naming normalized to PascalCase (`MafApiSafety` etc.) across source, tests, README, setup.md.
- **#40** `CONTRIBUTING.md` + `TROUBLESHOOTING.md` added at repo root.
- **#41** **Three executable MCP tools** added — `MafValidateFanOut` (pure Roslyn syntax walk, no semantic model), `MafRunCs0618Hunt` (shells `dotnet build`, parses CS0618/CS0246, registry-joined), `MafDiffPackage` (shells `dnx dotnet-inspect@0.7.8 -- diff`). Each tool exposes a pure-function parser core for unit testing.
- **#42** **The identity unlock** — `maf-best-practice-reviewer.agent.md` (6-phase audit orchestrator), `maf-anti-pattern-scanner/SKILL.md` (8 rules canonical), `MafScanAntiPatterns` MCP tool (6 of the rules implemented as executable regex/Roslyn scanners). Output: `audit-report.md`, NOT `migration-plan.md`. The README's "best-practice auditing beyond migration" promise is now real code.

### Post-review correctness fixes (folded forward from Opus reviews)
- `MafResources.GetSkill` — case-insensitive allowlist + case-sensitive resource lookup bug fixed by normalising to canonical name via `HashSet.TryGetValue`.
- `DiffPackageTool.ParseDiffOutput` — **completely rewritten** against real `dotnet-inspect@0.7.8 -- diff` output format (markdown `## Breaking Changes` / `## Additive Changes` sections, not line-prefix markers). Original parser produced empty results on real input.
- `Cs0618HuntTool.MatchToRegistry` — now extracts only the FIRST quoted segment (the obsolete symbol). Previously matched against the replacement-hint phrase, causing false positives.
- `FanOutValidatorTool.ClassifyReturnType` — split `UnusualReturnType` → `LikelyInvalid`; `IAsyncEnumerable<T>` correctly classified as `Ok`; raw types (e.g. `int`) now flagged as `LikelyInvalid` instead of buried as "review manually."
- `ProcessRunner` extracted: 5-min timeout with process-tree kill, async dual-stream drain (prevents pipe-buffer deadlock), `ArgumentList` argument passing (prevents command injection), 16 MB output cap.
- `Microsoft.Extensions.Hosting` + `YamlDotNet` pinned (were floating `9.*` / `16.*`).
- 4 `FormatEntry` tests pin the markdown report structure.

### Counts at end of sprint

| Surface | Start | End |
|---|---:|---:|
| Plan rows completed | 25 / 32 | 34 / 54 |
| Tests passing | 0 | **151** |
| Executable MCP tools | 3 | **7** |
| MCP-served skills | 11 | 12 |
| Agents | 2 (both migration-flavoured) | **3** (added best-practice reviewer) |
| Latent bugs caught by harness | n/a | 4 |
| Opus deep reviews | 1 (4 parallel) | 1 + 3 sequential |

### What this sprint did NOT do (intentional deferrals)
- `#30` registry.yaml CI auto-update — tractable now that v0.7.8 surfaces `[Obsolete]`, but unimplemented.
- `#43` expand registry to 9 missing API areas (memory, middleware, tool approval, observability, DevUI, structured output, source gen, instructions placement, agent creation).
- `#44` compile-validate `workflow-smoke-tester` templates against a real reference project.
- `#46` release-watcher bug fixes (wrong GitHub repo, hard-coded version regex, missing idempotency).
- `#47` per-version guide files.
- `#50` cut stable `1.3.0` — gated on external migration.
- `#51` rename decision (`maf-autopilot` → `maf-keeper`?).

---

## 11. Feature-completeness audit (2026-05-11 evening — Opus)

A dedicated multi-thousand-word audit by Opus against the four user-facing pillars. Full output captured in the session transcript; the headline:

### Verdict per pillar

| Pillar | Verdict | Reason |
|---|---|---|
| **1. Implement (greenfield)** | ❌ **NO** | No scaffolder, no codegen, no agent-prompt linter. Toolkit assumes legacy code already exists. |
| **2. Audit existing code** | ⚠️ **MOSTLY** | Real tools exist, but rules cover only **3 of 21 guide sections (14%)**. |
| **3. Enforce ongoing best practices** | ❌ **NO** | Has the scanner; lacks every delivery vehicle (Roslyn analyzer, PR-diff comments, SARIF, IDE squigglies, pre-commit hook, CI Action). |
| **4. Migrate to new MAF version** | ⚠️ **MOSTLY** | Mature for the proven 1.2→1.3 path; release-watcher would produce an un-mergeable PR on a real new MAF release today (wrong GH repo, stub guide generator, no registry auto-update). |

**Overall:** ALMOST feature-complete. Migration is genuinely beta; the other three pillars have core pieces but lack coverage or delivery.

### The single biggest gap

**Coverage.** The migration guide has 21 sections. The registry covers 7. The anti-pattern scanner covers 3. **14 of 21 guide sections have ZERO automation** — memory providers, middleware, tool approval, observability beyond §13, DevUI guarding, structured output, source-generator setup, instructions placement, agent creation patterns.

Today the toolkit is "4 fingers wide and 10 fingers deep on migration; 1 finger wide and 4 fingers deep on best-practice review." The user's vision requires inverting that: same depth, much wider coverage.

### The biggest easy win

**The 3 agents don't know about the 7 new MCP tools.** `maf-migration.agent.md` still tells Copilot to "load `cs0618-hunter` skill and run it" — should now be "call `MafRunCs0618Hunt`". `maf-auditor.agent.md` Phase B says "run dotnet-inspect commands" — should call `MafDiffPackage`. Only `maf-best-practice-reviewer.agent.md` partially knows; misses `MafApiSafety`, `MafRunCs0618Hunt`, `MafDiffPackage`. Rewiring this is one day of work; it converts roughly 20 manual procedure steps across the three agents into 4 tool calls.

### Skills audit — cuts and merges (from the same audit)

- **4 skills are now mostly redundant with tools** and should be slimmed to 10-line pointers: `cs0618-hunter`, `fan-in-static-analyzer`, `fan-out-validator`, `nuget-diff-analyzer`.
- **2 of those (`fan-in-static-analyzer` + `fan-out-validator`) should be merged** — 70% content overlap, one concept.
- **`workflow-smoke-tester` is risky** — templates not compile-validated; either fix (#44) or replace with a codegen tool that emits compilable tests by Roslyn-analysing the actual project.

### Out-of-the-box "gold" features to consider after 1.0 ships

Ranked by leverage (full justification in the session transcript):

1. **Roslyn analyzer companion** (already plan #29, but **elevate to gold**) — write-time red squigglies for fan-out void handlers. The killer demo.
2. **SARIF v2.1.0 output** for the scanners — unlocks GitHub Advanced Security adoption. Small LOC, big integration win.
3. **`maf-new` agent scaffolder** — closes the greenfield gap. As MAF matures past 1.3.0, brownfield migration demand drops; greenfield rises.
4. **GitHub App `@maf-bot`** — PR-diff scoped anti-pattern comments. Org-wide adoption vector.
5. **`MafExplain(snippet)`** — paste MAF code, get annotated explanation linking to guide sections. Reverse of the current direction. Onboarding gold.
6. **Auto-update registry from `migration-retrospective`** — close the self-improvement loop. Tractable now that v0.7.8 surfaces `[Obsolete]`.
7. **`MafLintAgentPrompt`** — agent `Instructions` quality checker (length, conflicting directives, untrusted-input handling). Nothing else in the .NET ecosystem does this for MAF.
8. **`MafEstimateCost`** — static token-cost estimator. The unsexy enterprise lock-in feature.
9. **`MafScanPromptInjection`** — Roslyn data-flow from controllers into `Instructions =`. #1 LLM-app security failure class.
10. **`MafDoctor`** — single-command A/B/C/F health letter. Wraps existing tools. Quickest demo gain.
11. **`MafSimulateWorkflow`** — in-vitro multi-agent simulation harness. The "test before deploy" for agent workflows. Closest to magic; experimental priority.

### Brutal-honesty cuts for 1.0

1. Slim 4 procedure-heavy SKILL.md files (`cs0618-hunter`, `fan-in-static-analyzer`, `fan-out-validator`, `nuget-diff-analyzer`) to pointers — they duplicate tools now and burn context budget every load.
2. Merge `fan-out-validator` + `fan-in-static-analyzer` (#45).
3. Reconsider `init`'s two-file install (`.github/copilot-instructions.md` is a copy of `maf-constraints.instructions.md` that drifts the moment this repo updates).
4. The 3 MCP prompts (`maf-audit`, `maf-migrate`, `maf-cs0618-hunt`) are thin wrappers over agent invocations — consider dropping.
5. Defer the rename decision (#51) until after stable 1.0 — much cheaper to ship `maf-autopilot 1.0` then rename for 2.0.

---

## Appendix A — Documentation hygiene to-do

- Regenerate README roadmap from plan table (single source of truth).
- Add CONTRIBUTING.md (how to add a skill, how to add a registry entry, PR conventions, "check upstream release notes before designing workarounds").
- Add TROUBLESHOOTING.md (init fails, stdio handshake, NuGet rate-limit, MCP server doesn't appear in Copilot).
- Add `examples/` directory with one canned before/after sample repo.
- Fix .NET SDK version inconsistency (setup.md vs csproj vs release-watcher workflow).
- Clean duplicate canonicity between `.github/copilot-instructions.md` and `.github/instructions/maf-constraints.instructions.md`.
- Add CHANGELOG.md (seeded from plan's "Phase Review History").
- Add architecture diagram to README: `agents → skills → MCP server → consumer repo` + the self-update loop.
- Add an "audit on 1.3.0" walkthrough — the differentiator the README claims but never shows.

---

## Appendix B — Verdict by area (one-line each)

**Verdicts pre-sprint:**

- **Vision:** ambitious, articulate, outpaces implementation by ~30%.
- **MCP server code:** MVP-quality, clean DI, zero tests.
- **Skills (as MCP tools):** 1 of 11 exposed — the single biggest delta vs the README.
- **Agents:** migration agent disciplined; auditor agent is pre-migration-only and needs a sibling.
- **Distribution:** prerelease pipeline proven; no stable.

**Verdicts post-sprint (2026-05-11 evening):**

- **Vision:** code now matches prose. Best-practice review beyond migration is real, not aspirational.
- **MCP server code:** ~mid-beta. 151 tests, ProcessRunner with timeout/async/safe args, JSONC-tolerant init, inverted-haystack search. Three Opus reviews caught all the real-world bugs.
- **Skills (as MCP tools):** 7 of 12 exposed (58%). The remaining 5 are inherently agent-orchestrator skills (`migration-plan-creator`, `migration-retrospective`, etc.) — making them tools wouldn't add value.
- **Agents:** three agents, each with a distinct purpose: pre-migration audit (`@maf-auditor`), migration execution (`@maf-migration`), steady-state review (`@maf-best-practice-reviewer`). Identity unlock complete.
- **Distribution:** alpha-3 live, alpha-4 staged in csproj. Stable release blocked only on external validation.
- **Identity:** "autopilot" / "keeper" / "steward" — now a cosmetic naming call, not a structural blocker.
- **Upstream awareness:** the dotnet-inspect lesson is encoded in `CONTRIBUTING.md`. Future contributors will not repeat the v0.7.6 mistake.
- **Self-upgrade:** still aspirational. Registry auto-update (#30) is tractable but unimplemented. Release-watcher bugs (#46) remain.

---

*Regenerate after each plan phase or major-feature merge. The four underlying review reports (plan/docs, MCP server, skills, agents/workflows) can be reproduced via parallel deep-review agents — the synthesis schema is the structure of this document.*
