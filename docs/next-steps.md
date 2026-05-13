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

2. **(10 min — human-driven publish) Cut stable `1.3.0` (T.7 → A.8 unblocked).** `gh workflow run release.yml -f version=1.3.0`. Publishes the multi-target nupkg (net8/9/10) + analyzer nupkg + Docker image. This is a one-way action — no autonomous launch.

3. **(parallel — interleaves anywhere)** **Continue Phase R Dependabot triage** — `#2` already closed (Docker runtime 9→10 forcer). Remaining open PRs as of 2026-05-13:
   - **Merge** #3 (docker/setup-buildx-action 3→4), #4 (docker/login-action 3→4) — low-risk action bumps.
   - **Verify+merge** #5 (upload-artifact 4→7 — known v4→v5 breaking on artifact-name defaults), #6 (softprops/action-gh-release 2.2.1→3.0.0 — verify the SHA pin + comment both got updated), #7 (actions/checkout 4→6 — high blast radius, scan v5+v6 changelogs).
   - **Defer to Phase U** #8 (coverlet.collector 6→10), #17 (grouped Roslyn 4→5 SDK bump).

Wall-clock from here to 1.0 publishable: **~60 min of human work** (steps 1 + 2) plus ~20 min interleaved Dependabot.

---

#### 💡 Creative ideas — where the toolkit could go next (post-1.0, brainstorm)

Things I'd love to see — sorted by leverage / wow-factor / "shows off MCP power":

##### MCP tool surface — new capabilities

- **`MafAutoFix(repoPath, ruleId, file?)`** — the MISSING half. Today the toolkit DETECTS but the AGENT applies fixes. A direct deterministic auto-fixer for the unambiguous rules (`MAF-AP-SEC-001` → `DefaultAzureCredential` → `ManagedIdentityCredential` is a one-line replacement; `MAF-AP-WF-001` → add `sealed` is mechanical; `MAF130-FAN-IN-001` → swap arg order is mechanical). Would let CI bots fix-and-commit without invoking an LLM. **Biggest leverage by far.**
- **`MafBeforeAfter(repoPath, planPath)`** — produce a single before/after diff for an entire migration plan, so a maintainer can review the change-set in one document before approving.
- **`MafExplainRule(ruleId)`** — registry-driven deep-dive on any rule with linked guide section + working examples. Already partially supported by `MafExplain`; this would specialise for rule IDs.
- **`MafScoreMigrationRisk(repoPath)`** — heuristically estimate how risky the migration is (count of silent-runtime failures vs. CS0618 vs. CS0246; coverage of `[Obsolete]` paths; how many fan-out edges; etc.). Produces a "this migration is HARD/MEDIUM/EASY" verdict.
- **`MafGenerateRegressionPlan(repoPath, fromVersion, toVersion)`** — given the current MAF version pin and target version, walk the version graph and emit a Mermaid step-by-step migration roadmap.
- **MCP sampling support** — the May-6 sketch had `SamplingTools.*` (full audit, code review). When MCP host sampling becomes widely deployed, plug these in so the toolkit can call back to the host LLM for fuzzy judgements (e.g. "is this prompt likely to refuse?").

##### Workshop — make the demo unforgettable

- **Step 0: animated install demo** — a `cast` recording of `dotnet tool install -g maf-autopilot --prerelease` → `maf-autopilot --version` → `maf-autopilot doctor .`. 30 seconds total. Embed in README.
- **A "find the bug in 30 seconds" challenge** — show attendees three side-by-side files with one subtle bug planted. Without running the toolkit, see who can spot the silently-ignored `Instructions` or the wrong-order `AddFanInBarrierEdge`. Then run the toolkit — it finds them in seconds. Massive impact.
- **CI-integration step** — after the migration, drop `.github/workflows/maf-pr-audit.yml` into the sample folder. Show the workflow running against a forked repo. Demonstrates the toolkit's CI surface, not just the chat surface.
- **Analyzer demo** — install `maf-autopilot.Analyzers` into the sample. Watch the squiggly red lines appear in VS Code on the un-fixed file. That's the "shift-left" angle — the analyzer catches bugs at *write* time, not just at scan time.
- **Migration retrospective with `@maf-best-practice-reviewer`** — after the migration, ask the second-opinion agent to grade the result. Demonstrates the multi-agent surface.
- **`MafTour` opener** — start the workshop with `@maf` invoking `MafTour` so attendees see the full toolkit catalogue before any specific tool is run. Sets the "this is a SUITE not a single check" frame.
- **Reset script** — `samples/workshop-reset.ps1` / `.sh` that `git stash`-es all migration changes so the workshop can be re-run instantly for the next attendee.
- **A second migration target** — once the 1.3.0 sample is migrated, the workshop's last 10 minutes could re-run the toolkit AGAINST the SAME sample but now targeting MAF 1.4 / 1.5 (after `dotnet add package Microsoft.Agents.AI --version 1.5.0`). Shows the auto-update loop's pay-off in real time.

##### Sample coverage — close the gaps

- **`samples/maf-1.2-sample/`** — covers the 3 registry IDs the 1.3 sample can't (`MAF130-THREAD-001`, `MAF130-A2A-001/002`, `MAF130-STREAM-001`, `MAF130-EVENT-001`, `MAF130-ATTR-001/002`). Pins MAF 1.2.0 + uses the now-removed types. Migration TO 1.3 demonstrates the dropped-surface migrations. **Highest-leverage Phase V item.**
- **`samples/maf-1.5-sample/`** — opposite end. Pins 1.5.0; gets compared against a hypothetical 1.6 (when it ships). Future regression coverage.
- **CI test that re-runs the workshop** — `dotnet test` integration: build the sample, run `maf-autopilot doctor`, assert grade F. Then run the canned migration plan, rebuild, assert grade A. Pins the workshop's invariants — if a future toolkit change regresses the doctor verdict, the test fails.
- **Sample seed templates via `MafNewSample(version, antiPatternIds[])`** — an MCP tool that scaffolds a new sample folder with the requested anti-pattern set. Useful for testing newly-added registry entries.

##### Toolkit improvements

- **`MafHealthBadge`** — emit a `[![MAF Health: A](https://shields.io/...)]` badge URL from the doctor output. README badge for any consumer codebase.
- **Telemetry that doesn't suck** — anonymous opt-in metrics: which rules fire most often, what migration paths users follow, what the typical doctor-grade distribution looks like. Helps prioritise rule investment without spying.
- **The `applies_to_codebases` field is a new precedent** — once we've added it to 3 entries (this Phase T pass), it should land in `Data/RegistryModels.cs` formally + flow through `MafCompatibility`. Today it's a free-text marker; making it structured unlocks better filtering.

##### Storytelling / community

- **A 5-minute screencast** — "this is the toolkit fixing 13 anti-patterns in a real codebase, watch." Embed in `README.md` between ASCII art.
- **A blog post template** — "how we built a self-updating MAF migration toolkit." 1500 words. Drafts already half-written across `docs/maf-migration-toolkit-plan.md` and the next-steps history.
- **`samples/showcase.md`** — gallery of every rule firing on a real example with the toolkit's verdict next to it. Marketing surface.

---

#### Phase V — post-1.0 roadmap (sequenced by leverage)

| ID | Item | Why it matters | Effort |
|---|---|---|---|
| V.1 | `MafAutoFix` MCP tool — deterministic per-rule auto-fixers for the mechanical rules | Decouples detection from remediation; CI bots can auto-fix without an LLM | M (1-2 days) |
| V.2 | `samples/maf-1.2-sample/` — covers the registry IDs the 1.3 sample can't | Closes registry coverage; tests pre-1.3 migration | M (4-8 hrs) |
| V.3 | CI regression: `samples/*` tested as part of `dotnet test` | Pins the workshop's invariants | S (2-3 hrs) |
| V.4 | `applies_to_codebases` field formalised in `RegistryModels.cs` | Lets `MafCompatibility` filter by source-codebase era | S (1-2 hrs) |
| V.5 | `MafBeforeAfter(planPath)` — single diff for an entire migration plan | Maintainer review surface | S (3-4 hrs) |
| V.6 | Analyzer NuGet installed into the 1.3 sample for the workshop demo | Demonstrates write-time enforcement | XS (30 min) |
| V.7 | Migration retrospective integrated into the workshop final step | Demonstrates `@maf-best-practice-reviewer` agent | XS (15 min) |
| V.8 | Screencast / animated install demo in README | Marketing | M (½ day to record + edit) |

Pick 2-3 from V.1-V.8 to bundle as the 1.1.x release after 1.0 ships.

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
