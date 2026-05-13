# Next steps

> **Last refreshed:** 2026-05-13 (later) — **Phase S + T.1-T.2.5 + T.6 landed** (multi-target nupkg, MAF 1.3 dogfood sample, workshop guide with tool-installer fence, 3 registry drift fixes); Dependabot #2 closed; **R.3 / S.8 already absorbed by Phase S** (Hosting 10.0.7 pinned centrally) so the original Phase R triage now skips #11 (closed automatically) and #1 (auto-resolved). Phase Q intact.
> **Single source of truth for "what's next."** When this contradicts other docs, this wins.
> **For history of done work:** see the **[`Done`](#-done--phases-a-through-q-history) section at the bottom** of this file, plus the full archive in [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md).

---

## Index

1. [Current state](#current-state)
2. [My recommendations — at a glance](#my-recommendations--at-a-glance)
3. [Active backlog — phased plan](#active-backlog--phased-plan)
   - [Phase R — in-flight + finishing touches](#phase-r--in-flight--finishing-touches-this-week)
   - [Phase S — Multi-target the nupkg](#phase-s--multi-target-the-nupkg-matching-agenteval-house-style)
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

**Current TFMs:** `maf-autopilot` + tests **multi-target `net8.0;net9.0;net10.0`** (Phase S, 2026-05-13). Analyzer on **`netstandard2.0`** (Roslyn requirement — locked). Central package management at `/Directory.Packages.props`; SDK pin at `/global.json` (`8.0.100` + `rollForward: latestMajor`).

**Pre-1.0 gate:** A.8 — "one external migration validates the toolkit end-to-end against a real customer codebase." Currently the only thing blocking the stable cut. **Phase T below proposes a way to unblock this without waiting for an external customer.**

---

## My recommendations — at a glance

1. **Multi-target the nupkg `net8.0;net9.0;net10.0` matching AgentEval's house style.** Adopt `Directory.Packages.props` (central package management) + `global.json` (SDK roll-forward) as the supporting infra. The Roslyn analyzer stays `netstandard2.0` (Roslyn requirement, locked). The **Dockerfile stays net8.0 LTS** (smallest runtime image — only the container needs to pick one). Consumers on net8/net9/net10 all install the matching TFM via NuGet's TFM resolution. ([Why this, not single-target net8 — corrected below](#tfm-strategy).)
2. **Build a MAF 1.3 sample inside `samples/maf-1.3-test-project/`** and dogfood the migration end-to-end (`@maf-auditor` → `@maf-migration` → `MafDoctor`). This **is** the A.8 unblocker — a synthetic-but-real validation we control, ready in 1-2 days instead of waiting for an external customer.
3. **Triage Dependabot PRs surgically.** Close 2 (Docker .NET 10 forcers — Dockerfile stays net8 LTS), **merge** the Hosting 10.0.7 bump (#11 is now wanted — 10.0.x line packs net8/9/10 TFMs in one nupkg), verify-and-merge 6 (low-risk Actions + Test.Sdk), pair-and-defer 2 (Roslyn 4→5 SDK bump — wait until after Phase T to bump anything that could break the analyzer).

Below is the phased plan, then the reasoning behind each call.

---

## Active backlog — phased plan

### Phase R — in-flight + finishing touches (this week)

The cleanup that's already running or close to running. Should land in the next day or two.

| ID | Item | Owner | Effort | Status |
|---|---|---|---|---|
| R.1 | Wait for Copilot's PR for MAF 1.5 (issue #16) and review + merge | YOU | S (5 min) | ⏳ Copilot working |
| R.2 | Close Dependabot #1 + #2 (Docker .NET 10 forcers — Dockerfile stays net8 LTS) | YOU or me (with consent) | S (5 min) | 📋 |
| R.3 | **Merge Dependabot #11 (Hosting 10.0.7)** — needed for Phase S multi-target. Was originally "close" — flipped after AgentEval cross-check. | YOU | S (5 min) | 📋 |
| R.4 | Merge low-risk Action bumps (#3, #4) after a sanity glance at each diff | YOU | S (10 min) | 📋 |
| R.5 | Verify + merge mid-risk Actions (#5 upload-artifact, #6 softprops, #7 checkout) — see [Dependabot triage](#dependabot-triage) | YOU | M (30 min) | 📋 |
| R.6 | Merge `Microsoft.NET.Test.Sdk` bump (#12) and re-run test suite | YOU or me | S (15 min) | 📋 |

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
| S.9 | Dockerfile: **stays on `net9.0` base** for now (still works; smallest LTS bump to `net8.0` recommended in a separate small PR). Dependabot #1 + #2 (Docker 9→10) ready to close. | ⚠️ Partial — Dockerfile bump to `net8.0` deferred to a follow-up |
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

2. **(10 min — human-driven publish) Cut stable `1.3.0` (T.7 → A.8 unblocked).** This is the **release cut**: `gh workflow run release.yml -f version=1.3.0` triggers the GitHub Actions release workflow which (a) runs `dotnet test` across all 3 TFMs, (b) `dotnet pack`s the multi-target nupkg + the analyzer nupkg, (c) pushes both to `nuget.org` with the `NUGET_API_KEY` secret, (d) builds + pushes the multi-arch Docker image to `ghcr.io/joslat/maf-autopilot:1.3.0`, (e) creates a tagged GitHub Release with auto-generated release notes. **"Announces" means: once `1.3.0` (without `-alpha-N`) is live on nuget.org, anyone running `dotnet tool install -g maf-autopilot` (no `--prerelease` flag needed) gets it.** That's the implicit announcement. An optional explicit announcement step would be a discussions post / blog / social — none required for the cut itself. This is a one-way publish to a public registry, so it stays human-driven.

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

#### 💡 Additional Ideas — post-1.0 backlog (with rich detail)

**Summary table** (sorted by potential impact × autonomy I can deliver). The detail subsections below give the full description, keypoints, and risks for each.

| Idea | Effort | Issues / risks | Potential impact | Can Claude do it alone? |
|---|---|---|---|---|
| V.1 `MafAutoFix` MCP tool | M (1-2 days) | Per-rule fix logic must be byte-perfect; bad fix = silent regression | 🟢🟢🟢🟢🟢 — the missing "do" half | ✅ Yes |
| V.2a `samples/maf-1.2-sample/` (1.2 → 1.3 migration) | M (4-8 hrs) | MAF 1.2.0 surface differs; need careful API recall vs. dotnet-inspect | 🟢🟢🟢🟢 — closes 5-6 registry-coverage gaps | ✅ Yes |
| V.2b `samples/maf-1.0-sample/` (1.0 → 1.x migration ladder) | M (4-8 hrs) | MAF 1.0 is older still; fewer real customers but huge migration-ladder demo value | 🟢🟢🟢 — bridges deepest historical gap + best demo | ✅ Yes |
| V.3 CI regression on `samples/*` | S (2-3 hrs) | Tests run a tool against a project; need careful timing + isolation | 🟢🟢🟢🟢 — workshop invariants pinned forever | ✅ Yes |
| V.4 "Find the bug in 30 seconds" challenge | S (2-4 hrs) | Picking 3 truly-subtle bugs that aren't obvious to a senior; slide assets | 🟢🟢🟢🟢🟢 — biggest workshop wow moment | ✅ Yes |
| V.5 Animated install cast (vhs `.tape` + GIF) | M (½ day) | I can write the `.tape` script + helper; recording itself needs vhs installed locally | 🟢🟢🟢🟢 — first-impression upgrade in README | 🟡 Source artifacts yes; rendering needs you (vhs/ffmpeg) |
| V.6 `applies_to_codebases` formalised in `RegistryModels.cs` | S (1-2 hrs) | Schema change; existing entries need migration; YAML loader must accept missing field | 🟢🟢🟢 — enables filtering by source-version era | ✅ Yes |
| V.7 `MafBeforeAfter(planPath)` — single diff per plan | S (3-4 hrs) | Plan format parsing; diff rendering | 🟢🟢🟢 — maintainer review UX | ✅ Yes |
| V.8 `MafHealthBadge` (shields.io output) | XS (1 hr) | Badge URL hosting (shields.io endpoint API) | 🟢🟢🟢 — viral marketing surface | ✅ Yes |
| V.9 Analyzer NuGet wired into the 1.3 sample (workshop step 5b) | XS (30 min) | Adds a `<PackageReference>` to the sample csproj; restore + rebuild | 🟢🟢 — shift-left demo | ✅ Yes |
| V.10 Migration retrospective via `@maf-best-practice-reviewer` (workshop step 9) | XS (15 min — doc-only) | Just adds a workshop step + screenshot | 🟢🟢 — multi-agent demo | ✅ Yes (doc only — agent invocation in workshop is yours) |
| V.11 `MafScoreMigrationRisk(repoPath)` | S (4 hrs) | Heuristic weighting — needs real-world calibration | 🟢🟢🟢 — sets expectations before fixing | ✅ Yes |
| V.12 `MafGenerateRegressionPlan(from, to)` | M (1 day) | Walks version graph; needs guide-section-link database | 🟢🟢🟢 — multi-version migration is the killer use case | ✅ Yes |

---

##### V.1 — `MafAutoFix(repoPath, ruleId, file?)` MCP tool

**Description.** Today the toolkit DETECTS anti-patterns (via `MafScanAntiPatterns`, `MafValidateFanOut`, `MafRunCs0618Hunt`) and the AGENT (e.g. `@maf-migration`) applies the fixes. There's no deterministic, AI-free "fix" half. `MafAutoFix` closes that loop for the mechanical rules:

**Keypoints.**
- One MCP method per rule family, dispatched off the `ruleId`.
- Strictly deterministic fixes only — no LLM call, no judgement. Examples:
  - `MAF-AP-SEC-001` → `new DefaultAzureCredential()` → `new ManagedIdentityCredential()` (1-line replace via Roslyn `SyntaxRewriter`).
  - `MAF-AP-WF-001` → add `sealed` to executor class declaration (Roslyn `AddModifier`).
  - `MAF130-FAN-IN-001` → swap `AddFanInBarrierEdge(target, sources)` → `(sources, target)` (Roslyn argument-list rewriter).
  - `MAF130-EXEC-001` → fan-out `ValueTask` → `ValueTask<T>` (needs the inferred `T` — return-expression analysis).
  - `MAF-AP-CONC-002` → `.Result` → `await` (with surrounding async-method propagation).
- Output: a SARIF report of what was fixed + a unified diff for review.
- CI integration: a bot can run `maf-autopilot autofix --rules MAF-AP-SEC-001,MAF-AP-WF-001 .` on every PR; no human in the loop for the mechanical cases.

**Issues / risks.** Per-rule fix logic must be byte-perfect: a bad rewrite is worse than no rewrite (introduces regressions silently). Mitigation: every fix has a paired unit test asserting before/after pair from the sample. Roslyn `SyntaxRewriter` is the right tool; needs careful trivia handling so we don't strip comments.

**Potential impact.** 🟢🟢🟢🟢🟢. Decouples detection from remediation. CI bots can fix-and-commit without an LLM in scope. Today the toolkit ships half a product; `MafAutoFix` ships the other half.

---

##### V.2a — `samples/maf-1.2-sample/` (1.2 → 1.3 migration)

**Description.** A second standalone sample pinned to **MAF 1.2.0** so it exercises the registry entries the 1.3-sample can't reach — the ones that describe **REMOVED-in-1.3 surfaces** (`MAF130-THREAD-001`, `MAF130-A2A-001/002`, `MAF130-STREAM-001`, `MAF130-EVENT-001`, `MAF130-ATTR-001/002`).

**Keypoints.**
- Same `FraudClaimsTriage` shape (re-uses the conceptual topology) but written against the 1.2-era API:
  - `agent.GetNewThread()` (the now-removed type).
  - `[StreamsMessage]` / `[YieldsMessage]` attributes on executors.
  - `services.AddA2AAgent(agent)` (the AIAgentExtensions surface).
  - `app.MapA2A("/agent")` (the removed endpoint mapping).
  - `InProcessExecution.StreamAsync(message)` (the removed streaming entry point).
  - `AgentRunUpdateEvent` (the renamed event type).
- Includes its own `MafSample.FraudClaims12.csproj` pinning `Microsoft.Agents.AI 1.2.0` exactly.
- Workshop bonus track: open the 1.2 sample → run `MafScanAntiPatterns` → see CS0246 entries flagged → migrate to 1.3 via `@maf-migration` → re-run the doctor.
- The same migration toolchain handles BOTH 1.2→1.3 AND 1.3→1.4 (chained migrations).

**Issues / risks.** MAF 1.2.0 NuGet must still be available on nuget.org (it is, as of 2026-05-13). The 1.2 API surface needs `dotnet-inspect` lookups to author authentically — same Phase T discipline as we used for 1.3. Risk: if Microsoft un-publishes 1.2, the sample becomes unbuildable; pin a snapshot if it matters.

**Potential impact.** 🟢🟢🟢🟢. Closes 5-6 registry-coverage gaps in one stroke. Validates the auto-update pipeline (1.3 ↔ 1.2 ↔ 1.4 chain). Demonstrates the toolkit handles BOTH "older customer codebase" and "younger codebase" migration directions.

---

##### V.2b — `samples/maf-1.0-sample/` (1.0 → 1.x migration ladder)

**Description.** The OLDEST sample — pinned to **MAF 1.0.0** (the initial release). Demonstrates a full multi-step migration ladder: 1.0 → 1.1 → 1.2 → 1.3 → 1.4 → 1.5.

**Keypoints.**
- The earliest MAF API surface, including `AgentThread`, the pre-`AgentSession` chat-history pattern, the early `[StreamsMessage]` shape, etc.
- Migrates THROUGH the registry entries that span versions (a single fix path doesn't suffice).
- Workshop super-stretch demo: "watch the toolkit migrate this codebase across 5 MAF versions in 10 minutes."
- Even if fewer real customers are on 1.0 today, the migration-ladder visualization is the **most powerful demonstration of the toolkit's reach**.

**Issues / risks.** MAF 1.0.0 surface is the most distant from current docs — recall is harder. Some 1.0-era APIs may have been removed even from current compat-matrix tracking. Requires careful `dotnet-inspect` recon. Lower customer-realism (most people aren't on 1.0 anymore).

**Potential impact.** 🟢🟢🟢. Killer demo for talks/workshops; less practical day-to-day. Bridges the deepest historical gap.

---

##### V.3 — CI regression on `samples/*`

**Description.** Add an integration test (or a dedicated workflow) that runs `MafDoctor` against each `samples/*` folder and asserts the expected grade. Pins the workshop's invariants: if a future toolkit change regresses the detection on the 1.3 sample (e.g. the fan-out validator stops finding `ValueTask` returns), CI fails BEFORE the regression ships.

**Keypoints.**
- New test project: `src/maf-autopilot.Samples.IntegrationTests/` or just a new `[Fact]` in the existing test suite.
- Per-sample expectations recorded in YAML: `samples/maf-1.3-sample/.expected-doctor.yaml` with `grade: F`, `anti_pattern_errors: 10`, `fanout_starvation_risks: 3`, etc.
- Test calls `MafDoctor` programmatically via `Tools.DoctorTool.MafDoctor(sampleRoot)` and asserts each expected metric.
- Optional second stage: apply the canned migration plan, re-build, re-doctor → assert grade A.

**Issues / risks.** Sample builds need MAF 1.3.0 on the CI runner. `dotnet restore samples/maf-1.3-sample/` adds ~20 seconds to CI. Need to ensure the build cache works across the main solution + the sample (different `Directory.Packages.props` opt-out). Risk: false positives if the toolkit's grading thresholds shift; mitigate by versioning the expectations file alongside the toolkit.

**Potential impact.** 🟢🟢🟢🟢. Permanent regression net. Every future MAF version's sample slots in with its own expectations. Workshop attendees can trust the demo works.

---

##### V.4 — "Find the bug in 30 seconds" challenge

**Description.** A workshop teaser BEFORE running the toolkit. Show 3 side-by-side code snippets — each ~20 lines — with one subtle MAF anti-pattern planted in each. Attendees have 30 seconds to spot the bugs. THEN run the toolkit → it finds them all in <1 second.

**Keypoints.**
- New folder: `samples/find-the-bug/`
  - `snippet-1.cs` — has top-level `Instructions` (pre-1.3 silently-ignored pattern).
  - `snippet-2.cs` — has fan-out `[MessageHandler]` returning `ValueTask` (not `ValueTask<T>`).
  - `snippet-3.cs` — has `AddFanInBarrierEdge(target, sources)` wrong-order overload.
- `samples/find-the-bug/README.md` — challenge framing + the answer key.
- Workshop slide template (markdown) with 3 columns, code-highlighted.
- Punchline: "even a senior MAF dev needs 5 minutes per file to spot these. The toolkit found all 3 in 800 ms."

**Issues / risks.** Picking bugs that are TRULY subtle to a senior reviewer — bugs that look right at a glance. The 3 listed above qualify (Instructions silently ignored is famously sneaky; fan-out `ValueTask` looks like normal async code; fan-in arg order looks symmetric). Visual presentation matters — needs syntax-highlighted markdown or screenshot.

**Potential impact.** 🟢🟢🟢🟢🟢. The single biggest "wow" moment in any toolkit demo. Sells the value proposition in 30 seconds.

---

##### V.5 — Animated install cast in README (with detailed HOW)

**Description.** A short animated demo embedded at the top of `README.md` showing `dotnet tool install -g maf-autopilot --prerelease` → `maf-autopilot --version` → `maf-autopilot doctor .` → grade output. ~20-30 seconds. First-impression upgrade.

**The recording toolchain — recommended:**

| Tool | What it does | Why |
|---|---|---|
| **`vhs`** by [Charmbracelet](https://github.com/charmbracelet/vhs) | Declarative terminal recorder. Reads a `.tape` script (your "demo screenplay") and produces a GIF / WebM / MP4 with simulated typing. | Best-in-class for repeatable demos. The `.tape` file goes in git; anyone can re-render. No flaky live recordings. |
| `asciinema` | Records a real terminal session into a `.cast` file. | Use only if you want to capture an actual interactive session. Less polished than vhs. |
| `agg` | Converts `.cast` files → GIF. | Companion to asciinema if you go that route. |
| `svg-term-cli` | Converts `.cast` → animated SVG. | Smaller file size than GIF, but SVG animation support is patchy. |

**My recommendation: vhs.** A 30-line `.tape` file produces a polished GIF that's reproducible and version-controlled.

**Sample `.tape` for our use case** (drop in `docs/assets/install-cast.tape`):

```bash
# docs/assets/install-cast.tape — render with: `vhs install-cast.tape`
Output docs/assets/install-cast.gif

Set FontSize 16
Set Width 1200
Set Height 700
Set Theme "Dracula"
Set Padding 20
Set TypingSpeed 50ms

Type "# Install maf-autopilot as a global .NET tool"
Enter
Sleep 800ms

Type "dotnet tool install -g maf-autopilot --prerelease"
Sleep 300ms
Enter
Sleep 3s

Type "maf-autopilot --version"
Sleep 200ms
Enter
Sleep 1500ms

Type "# Audit any MAF codebase — grade A through F"
Enter
Sleep 800ms

Type "cd samples/maf-1.3-sample"
Sleep 200ms
Enter
Sleep 500ms

Type "maf-autopilot doctor ."
Sleep 200ms
Enter
Sleep 4s

Type "# 🔴 grade F — the toolkit found every deliberate anti-pattern."
Enter
Sleep 2s
```

**Steps to render (one-time setup on the recording machine):**

```bash
# macOS
brew install vhs ffmpeg ttyd

# Windows (PowerShell)
winget install charmbracelet.vhs

# Render
vhs docs/assets/install-cast.tape
# → outputs docs/assets/install-cast.gif (~500 KB)
```

**Embed in README**, between the existing ASCII art header:

```markdown
<p align="center">
  <img src="docs/assets/install-cast.gif" alt="maf-autopilot in 30 seconds" />
</p>
```

**What Claude Code can deliver autonomously:**
- ✅ The `.tape` script (committed in git, version-controlled)
- ✅ The `make-cast.sh` / `make-cast.ps1` wrapper that calls `vhs`
- ✅ The README embed snippet
- ✅ The `docs/assets/` folder structure

**What needs a human (or a CI runner with vhs installed):**
- 🟡 The actual `vhs install-cast.tape` invocation — needs vhs + ffmpeg + ttyd installed locally
- 🟡 Any visual touch-ups beyond what `.tape` directives express (cropping, watermark, etc.)
- 🟡 Audio narration if desired (vhs doesn't do audio; would need post-production)

**Issues / risks.** The recording machine needs to have the dotnet tool pre-installed exactly the way the cast pretends. Embeds in README CI should be deterministic — re-render only when commands change. Animated GIFs can be 2-5 MB; if size matters, switch to WebM (`Output install-cast.webm` in the tape file) for ~10× smaller.

**Potential impact.** 🟢🟢🟢🟢. A 30-second animated demo at the top of README is the difference between "I'll come back to this" and "I'm trying it now."

---

##### V.6 — `applies_to_codebases` formalised in `RegistryModels.cs`

**Description.** Phase T introduced a new optional YAML field `applies_to_codebases: "pre-1.3.0"` on 2 registry entries (`MAF130-SESSION-001`, `MAF130-INSTRUCTIONS-001`) to mark them as pre-version migration patterns vs. in-version surface patterns. Today it's free-text. V.6 formalises the field in `Data/RegistryModels.cs` + uses it for filtering.

**Keypoints.**
- Add `string? AppliesToCodebases { get; init; }` to `RegistryEntry` record.
- YAML loader (YamlDotNet binding) ignores unknown fields by default, so this is backward-compat.
- Optional enum: `"pre-X.Y.Z"`, `"X.Y.Z+"`, `"X.Y.Z-only"`.
- `MafCompatibility(version)` learns to filter registry entries to those applicable to a codebase pinned at `version`.
- `MafScanAntiPatterns` can optionally accept `--for-codebase-version 1.3.0` and skip entries whose `applies_to_codebases` excludes that version.

**Issues / risks.** Schema migration: existing 2400-line registry.yaml has 19 entries; only 2 use the new field. Loader must default to "applies to any" when the field is missing. Backwards-compat for older `maf-autopilot` installs that don't know the field.

**Potential impact.** 🟢🟢🟢. Lets the toolkit cleanly distinguish "pre-migration pattern" from "in-version surface pattern" — exactly the confusion Phase T surfaced.

---

##### V.7 — `MafBeforeAfter(planPath)` — single diff per migration plan

**Description.** Given a `migration-plan.md`, produce a single unified diff showing all proposed changes across the whole codebase. Maintainer reviews ONE document and approves/rejects.

**Keypoints.**
- Input: path to a `migration-plan.md` written by `@maf-auditor`.
- Output: a markdown doc with per-file `diff --git` blocks, ordered by file, with each hunk labeled with the registry ID that triggered the change.
- Optional `--format sarif` for IDE integration.
- Optional `--apply` mode that uses `MafAutoFix` (V.1) to actually land the changes.

**Issues / risks.** Plan format isn't structured today — it's free-form markdown. V.7 needs a stable plan schema. Could couple with V.1 to share the underlying fix logic.

**Potential impact.** 🟢🟢🟢. Maintainer review UX. Today the migration plan is read prose; V.7 makes it a single-page diff review.

---

##### V.8 — `MafHealthBadge` (shields.io endpoint)

**Description.** Output a [shields.io](https://shields.io) endpoint URL from the doctor command so consumers can embed `[![MAF Health: A](https://...)]` in their README. Live badge driven by the latest `maf-autopilot doctor` run on `main`.

**Keypoints.**
- `maf-autopilot badge` subcommand emits a JSON payload conforming to shields.io's endpoint badge schema.
- Hosting: a tiny GitHub Pages or Cloudflare Worker that serves the latest JSON snapshot, refreshed by CI.
- Or simpler: the workflow uploads the JSON as a Gist that shields.io reads.

**Issues / risks.** Hosting infrastructure required (Worker / Gist / Pages). Badge URL stability matters — once embedded by consumers, it can't break.

**Potential impact.** 🟢🟢🟢. Viral marketing surface — every consumer codebase that adopts the toolkit gets a badge that links back. Low absolute traffic but high signaling value.

---

##### V.9 / V.10 / V.11 / V.12 — short briefs

- **V.9 (Analyzer NuGet in workshop)** — workshop step 5b: `dotnet add package maf-autopilot.Analyzers --prerelease` inside the sample. Watch squiggly red lines appear in VS Code. Shift-left demo. **30 min, doc + sample csproj edit.**

- **V.10 (Migration retrospective)** — workshop step 9: after fix-up, ask `@maf-best-practice-reviewer` to grade the result. Demonstrates the second-opinion agent. **15 min, doc-only.**

- **V.11 (`MafScoreMigrationRisk`)** — pre-migration risk verdict. Counts silent-runtime failures vs. CS0618 vs. CS0246; weighs fan-out coverage. Outputs HARD/MEDIUM/EASY. **4 hrs.**

- **V.12 (`MafGenerateRegressionPlan`)** — given `from=1.0` `to=1.5`, produce an ordered Mermaid roadmap of per-version migration steps with linked guide sections. Combines with V.2a/V.2b for the killer multi-step demo. **1 day.**

---

#### Pick-3 recommendation for the 1.1.x release

If I were to pick 3 of these to bundle as the first post-1.0 release:

1. **V.1 (MafAutoFix)** — the missing half of the toolkit. Highest absolute leverage.
2. **V.2a (`samples/maf-1.2-sample/`)** — closes registry coverage; combined with V.3 + V.4 it powers a polished workshop.
3. **V.5 (Animated install cast)** — single biggest perception upgrade. Sells the toolkit in 30 seconds.

Total ~3-4 days of focused work for a 1.1.x release that materially expands the product.

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
