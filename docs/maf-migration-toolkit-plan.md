# maf-autopilot — Full Plan

> **Status:** Living document — reflects design decisions made during the MAF 1.3.0 migration of `maf-claims-fraud-guardian` and subsequent brainstorming sessions.  
> **Last updated:** May 2026

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
| 5 | `dotnet-inspect` skill | NuGet API surface inspection — `[Obsolete]` now properly surfaced (issue #316 resolved) | P1 | ✅ DONE | 100% | 95% | Has issue #316 link. Decision tree updated: issue resolved, per-member [Obsolete] lookup now works. `cs0618-hunter` still preferred for project-wide scan (faster). |
| 6 | `obsolete-api-registry` skill + `registry.yaml` | Machine-readable CS0618 registry — 10 entries covering all known 1.3.0 obsolete patterns | P1 | ✅ DONE | 100% | 90% | 10 entries: FAN-IN-001, SESSION-001, THREAD-001, EXEC-001, A2A-001, A2A-002, STREAM-001, EVENT-001, ATTR-001, ATTR-002. Plan was out of date claiming only 1 entry. |
| 7 | `maf-migration-guide` skill | Smart section navigator for the 29-section guide | P1 | ✅ DONE | 100% | 90% | Full section index, quick lookup by symptom, line range guidance. |
| 8 | `migration-plan-creator` skill + `template.md` | Generates complete, ready-to-execute migration plans | P1 | ✅ DONE | 100% | 90% | Skill + template both present. Phase/task ID conventions documented. |
| 9 | `fan-out-validator` skill | Validates fan-out/fan-in executor topology — detects silent starvation | P1 | ✅ DONE | 100% | 85% | Full 5-step workflow, fix patterns, both overload forms covered. |
| 10 | `fan-in-static-analyzer` skill | Static code analysis — finds broken fan-out handlers without running code | P2 | ✅ DONE | 100% | 85% | Detailed step-by-step procedure, all 6 analysis steps. |
| 11 | `nuget-diff-analyzer` skill | Structured diff post-processing skill with risk categorization | P1 | ✅ DONE | 100% | 85% | Categories, risk table, registry cross-reference, output template all present. |
| 12 | `maf-release-watcher` skill | Detects new MAF releases, updates guide + registry, creates PR | P1 | ✅ DONE | 100% | 80% | Full workflow (Steps 1-7). PowerShell + bash variants. |
| 13 | `workflow-smoke-tester` skill | Generates post-migration structural smoke test stubs | P2 | ✅ DONE | 100% | 80% | 5 patterns covered: fan-out/in, session round-trip, streaming, structured output, tool invocation. |
| 14 | `migration-retrospective` skill | Post-migration learning loop — improves registry, guide, constraints | P2 | ✅ DONE | 100% | 80% | Full workflow Steps 1-7. Surprise categorization table. |
| 15 | `maf-release-watcher.yml` GitHub Actions | Weekly NuGet check → auto-diff → PR creation | P1 | ✅ DONE | 95% | 85% | Real YAML workflow with 3 jobs. Inline Python scripts (plan mentioned separate script files — inline is better). PR checklist is thorough. Missing: `registry.yaml` auto-update step still manual. |
| 16 | `guides/maf-1.3.0-migration-guide.md` | Full 29-section reference guide (2000+ lines) | P1 | ✅ DONE | 100% | 90% | Present and comprehensive. **Updated this session:** version-keyed metadata comments applied to all 29 sections. Sections 17.5–17.12 are subsections of section 17 — documented in ToC. |
| 17 | `docs/compatibility-matrix.md` | MAF version ↔ dependency version table, auto-updated | P1 | ✅ DONE | 100% | 85% | Present. 4 MAF versions documented (1.0.0–1.3.0). Includes removed packages table, NuGet IDs. |
| 18 | `.maf-version` | Tracked version file for release watcher | P1 | ✅ DONE | 100% | 100% | Contains `1.3.0`. Simple, correct. |
| 19 | `README.md` | Project overview, structure, skill routing table, usage instructions | P1 | ✅ DONE | 100% | 85% | **Fixed this session:** Removed duplicate skill routing table rows + duplicate `> Never use...` callout. Clean now. |
| 20 | Version-keyed guide sections | `<!-- introduced: 1.3.0 \| applies-to: ... -->` metadata in guide headings | P2 | ✅ DONE | 100% | 85% | **Implemented in this session.** All 29 sections (1–21 + 17.5–17.12) tagged. `maf-migration-guide` SKILL.md updated with usage docs. Guide header updated. |
| 21 | `maf-autopilot` MCP server (MVP) | C# MCP server with `maf_api_safety` + `maf_registry_lookup` + `maf_registry_list` tools | P2 | ✅ DONE | 100% | 85% | **Done — May 2026.** Renamed from `maf-inspect` → `maf-autopilot` (matches repo, signals full scope). SDK upgraded `0.6.*` → `1.2.0` (stable). `dotnet build` passes clean. Sampling confirmed: `server.SampleAsync()` available in 1.2.0. `.vscode/mcp.json` updated. |
| 22 | `maf-autopilot` MCP Resources | All 11 skill docs + guide sections + registry exposed as `maf://skills/*`, `maf://guide/section/*`, `maf://registry` | P2 | ✅ DONE | 100% | 90% | **Done — May 5, 2026.** `Resources/MafResources.cs` (`[McpServerResourceType]`). Static: `maf://constraints`, `maf://registry`, `maf://guide`. Template: `maf://skills{?name}` (11 skills). All files embedded via `LogicalName` in csproj. `WithResourcesFromAssembly()` in `Program.cs`. Build clean. |
| 23 | `maf-autopilot` MCP Prompts | `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt` — slash-command workflows with embedded skill context | P2 | ✅ DONE | 100% | 90% | **Done — May 5, 2026.** `Prompts/MafPrompts.cs` (`[McpServerPromptType]`). Three prompts: `maf-audit` (repoPath, fromVersion), `maf-migrate` (taskIds, planPath), `maf-cs0618-hunt` (projectPath). All reference `maf://` resource URIs inline. `WithPromptsFromAssembly()` in `Program.cs`. Build clean. |
| 24 | `maf-autopilot init` command | CLI scaffold: `maf-autopilot init` writes `.vscode/mcp.json` + generates minimal `.github/copilot-instructions.md` | P2 | ✅ DONE | 100% | 90% | **Done — May 5, 2026.** `InitCommand.cs` intercepts `init` arg before MCP Host starts. Merges `.vscode/mcp.json` (global-tool entry, idempotent). Writes `.github/copilot-instructions.md` (MAF hard constraints, frontmatter-stripped) only if not exists. Smoke-tested: skip logic confirmed. Build clean. |
| 25 | NuGet publish (`dotnet tool install -g maf-autopilot`) | Publish to NuGet.org as a global tool. Enables `dotnet tool install -g maf-autopilot` | P2 | ✅ DONE | 100% | 90% | **Done.** Published to NuGet.org. Release pipeline works. `dotnet tool install -g maf-autopilot` functional. |
| 26 | `maf-autopilot` Sampling tools | Agentic tools using `server.SampleAsync()`: `maf_full_audit` (analyze→plan→generate→validate), `maf_migration_suggest` | P3 | ✅ DONE | 100% | 80% | **Done — May 6, 2026.** `Tools/SamplingTools.cs`. Repo scanning + LLM-driven plan generation + targeted migration suggestions. Method-param injection of IMcpServer. |
| 27 | `maf-autopilot` full analysis tools | 7 tools: `maf_health_check`, `maf_detect_cs0618`, `maf_api_diff`, `maf_fan_out_validate`, `maf_executor_pattern_check`, `maf_compatibility`, `maf_migration_path` | P3 | ✅ DONE | 100% | 80% | **Done — May 6, 2026.** `Tools/AnalysisTools.cs`. CS0618 via subprocess, API diff via dnx, fan-out/executor static scan, compat matrix, migration path. |
| 28 | Docker distribution via GHCR | `docker pull ghcr.io/joslat/maf-autopilot:latest` | P3 | ❌ NOT STARTED | 0% | 0% | Depends on #25. Dockerfile + GHCR push in workflow. |
| 29 | Roslyn analyzer companion | Flags void fan-out handlers at write-time in the IDE | P3 | ❌ NOT STARTED | 0% | 0% | Nice-to-have. Requires Roslyn analyzer project. |
| 30 | `registry.yaml` auto-update in CI | Semantic step (Copilot Coding Agent or LLM API call) to auto-generate new registry entries from dotnet-inspect diff output — closes the last manual step in `maf-release-watcher.yml` | P2 | ❌ NOT STARTED | 0% | 0% | Pure scripts can't write meaningful YAML entries without semantic analysis. Target: hybrid Copilot Agent step in workflow (see Section 11). No external prerequisite — this is a CI-internal improvement to the release watcher itself. |
| 31 | `maf_open_feedback_issue` Sampling tool | After migration, uses `server.SampleAsync()` to analyze the migration log, then returns a structured GitHub Issue draft on `joslat/maf-autopilot` with new CS0618 patterns and guide corrections | P2 | ✅ DONE | 100% | 80% | **Done — May 6, 2026.** `Tools/FeedbackTool.cs`. Sampling-driven issue generation. Returns formatted markdown ready to post. GitHub PAT optional for auto-post. |
| 32 | Multi-version migration paths | Support 1.1.0 → 1.3.0 in one pass (not step-by-step through intermediate versions) | P3 | ❌ NOT STARTED | 0% | 0% | Depends on version-keyed guide sections (#20 ✅). Migration agent stacks multiple version paths by loading sections tagged for each intermediate version. |

### Summary

| Tier | Items | Done | Needs Work | Not Started |
|------|------:|-----:|-----------:|------------:|
| Core infrastructure | 8 | 8 | 0 | 0 |
| Tier 1 — Obvious gaps | 3 | 3 | 0 | 0 |
| Tier 3 — Auto-generated plans | 2 | 2 | 0 | 0 |
| Tier 4 — Runtime verification | 3 | 3 | 0 | 0 |
| Tier 5 — Reflexive learning | 2 | 2 | 0 | 0 |
| Architectural capstone | 9 | 8 | 0 | 1 |
| CI automation | 1 | 0 | 0 | 1 |
| Multi-version support | 1 | 0 | 0 | 1 |
| **Total** | **32** | **29** | **0** | **3** |

**Overall completion: 29/32 = 91% — Sampling tools, analysis tools, feedback tool, NuGet publish all done. Remaining: CI registry auto-update (#30), Docker (#28), multi-version (#32) — all deferred.**

**Next priorities (deferred):** `registry.yaml` CI auto-update (#30, needs Copilot Enterprise) → Docker/GHCR (#28) → Multi-version migration paths (#32).

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
| **Build-blind tooling** — `dotnet-inspect` previously could not surface `[Obsolete]` at the overload level (issue #316, now resolved — `member --index` now shows it) | CS0618 warnings were missed until `dotnet build` ran, late in the process |
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
| `mcp/maf-autopilot/` | .NET MCP server | Standalone publishable project (SDK 1.2.0 + Sampling) |
| `.github/workflows/` | GitHub Actions | Release-triggered automation |
| `docs/` | Planning & strategy | Human-readable, not agent-consumed |

---

## 5. Tier 1 — Fix the Obvious Gaps

### 5.1 Skill: `cs0618-hunter` **✅ DONE**

**Problem it solves:** CS0618 warnings were caught late — after coding, during build. `dotnet-inspect` (issue #316 now resolved) can confirm per-member `[Obsolete]` status, but a full project-wide compiler scan is faster.

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
   - **New obsolete overloads** (CS0618 risk — dotnet-inspect can now detect [Obsolete] at the overload level (issue #316 resolved), but cs0618-hunter compiler scan is still faster for project-wide detection)
   - **New types or members** (opportunities to adopt cleaner APIs)
3. Outputs a structured markdown report with per-category counts and migration guidance
4. Cross-references with `obsolete-api-registry.yaml` to flag any known patterns

**Note on [Obsolete] detection:** `dotnet-inspect` (issue #316 resolved) can now detect `[Obsolete]` at the overload level via `member ... --index`. For project-wide scanning, always pair with `cs0618-hunter` — one compiler build pass covers every call site across all files.

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
| File-copy mode (17 `.github/` files copied to target repo) | Updated in-place by retrospective skill directly | Manual PR from the updated local files to `joslat/maf-autopilot` |
| MCP binary mode (`dotnet tool install -g maf-autopilot`) | Cannot write embedded resources | **Via `maf_open_feedback_issue` Sampling tool** |

**The `maf_open_feedback_issue` tool (tracking row #31):**

After a migration completes, this MCP tool uses `server.SampleAsync()` to analyze the migration log (plan file, terminal history, or user-provided excerpt), then — if the user has [GitHub MCP](https://github.com/github/github-mcp-server) connected — opens a structured issue on `joslat/maf-autopilot`:

```
Title: [Retrospective] CS0618 pattern not in registry: WorkflowBuilder.SomeMember
Body:
  Codebase: [user-provided or redacted]
  New CS0618 pattern: WorkflowBuilder.SomeMember(oldArgs) — not in registry.yaml
  Suggested registry entry: [YAML block auto-generated from the build warning + LLM]
  Related guide section: [extracted from migration plan]
  Build output: warning CS0618 ...
```

The loop closes: migration → GitHub issue → maintainer reviews → `registry.yaml` entry added → new release → all users benefit via `dotnet tool update -g maf-autopilot`. GitHub MCP access is already available in VS Code Copilot (`mcp_github_*` tools) — no extra setup needed for the user.

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

GitHub issue #316 was filed because `dotnet-inspect member` didn't flag `[Obsolete]` on individual overloads. `maf-autopilot`'s `maf_api_safety` tool directly answers the question that motivated that issue — using the compiler for authoritative project-wide detection. Issue #316 has since been resolved: `dotnet-inspect member ... --index` now surfaces `[Obsolete]` at the overload level. The two tools complement each other: `dotnet-inspect` for targeted per-member lookup, `maf-autopilot` for project-wide call-site scanning in one `dotnet build` pass.

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

Dependabot can monitor `Microsoft.Agents.AI` in `mcp/maf-autopilot/maf-autopilot.csproj` and create a version-bump PR the moment a new version ships. Your workflow fires on that PR event — no custom polling infrastructure needed.

**`dependabot.yml` config:**
```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/mcp/maf-autopilot"
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
          dotnet restore mcp/maf-autopilot/maf-autopilot.csproj
          dotnet build mcp/maf-autopilot/maf-autopilot.csproj --configuration Release
          dotnet publish mcp/maf-autopilot/maf-autopilot.csproj \
            --configuration Release \
            --output mcp/dist/

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
        run: dotnet nuget push mcp/dist/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json

      - name: Push Docker image
        run: |
          docker build -t ghcr.io/${{ github.repository_owner }}/maf-autopilot:${{ needs.check-for-new-maf-release.outputs.new_version }} mcp/maf-autopilot/
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
| `mcp.json` entry | `"command": "maf-autopilot"` | `"url": "http://localhost:7891/mcp"` | `"command": "/path/maf-autopilot"` | `"command": "dnx", "args": [...]` |
| Audience fit | .NET developers ✅ | CI / non-.NET | Offline / version-pinned | dotnet-inspect-familiar |
| **Verdict** | **Primary** | **Secondary** | **Fallback** | **Optional** |

**Recommendation:** Option 1 as primary — the target audience is .NET developers who already have the SDK and understand `dotnet tool`. Add Option 3 binary downloads as GitHub Release assets for teams where global tool installation is restricted. Option 2 Docker as secondary for CI pipelines and non-.NET environments. Option 4 `dnx` is useful for one-off invocations without installation.

### Option 1: .NET Global Tool *(primary recommendation)*

Package `maf-autopilot` as a .NET global tool. Users install once:

```bash
dotnet tool install --global maf-autopilot
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
      "command": "maf-autopilot",
      "type": "stdio"
    }
  }
}
```

**Pros:** No Docker required, works on all platforms, version-managed via `dotnet tool update`, familiar workflow for .NET developers.  
**Cons:** Requires .NET SDK installed. Tool startup is slightly slower than a native binary.

### Option 2: Docker / GitHub Container Registry

```bash
docker pull ghcr.io/joslat/maf-autopilot:latest
docker run -p 7891:7891 ghcr.io/joslat/maf-autopilot:latest
```

**How to release:**
1. Add a `Dockerfile` to `mcp/maf-autopilot/`
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
  - *`mcp/maf-autopilot/` C# project, stdio transport, ModelContextProtocol 1.2.0, YamlDotNet, embedded `registry.yaml`. `dotnet build` passes clean.*
  - *3 tools: `maf_api_safety` (SAFE/UNSAFE verdict), `maf_registry_lookup` (full entry by ID), `maf_registry_list` (all IDs)*
  - *`server.SampleAsync()` available — enables future agentic tools. `.vscode/mcp.json` updated.*

- [x] `maf-autopilot` MCP Resources (#22) — `maf://constraints`, `maf://registry`, `maf://guide`, `maf://skills{?name}`
  - *`Resources/MafResources.cs` (`[McpServerResourceType]`). 4 resource handlers. 13 EmbeddedResource entries in csproj. `WithResourcesFromAssembly()` in `Program.cs`.*

- [x] `maf-autopilot` MCP Prompts (#23) — `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt` slash commands
  - *`Prompts/MafPrompts.cs` (`[McpServerPromptType]`). All prompts reference `maf://` URIs and inject constraint rules inline. `WithPromptsFromAssembly()` in `Program.cs`.*

- [x] `maf-autopilot init` (#24) — one-command project onboarding
  - *`InitCommand.cs`. Intercepts `init` arg before MCP Host starts. Merges `.vscode/mcp.json` (idempotent). Writes `.github/copilot-instructions.md` (MAF hard constraints) only if not exists.*

### ❌ Not Started — Medium Priority (P2)

- [ ] #25 NuGet publish — `dotnet tool install -g maf-autopilot` on NuGet.org
- [ ] #30 `registry.yaml` auto-update in CI — hybrid Copilot Agent step to close the last manual gap in the release watcher
- [x] #31 `maf_open_feedback_issue` tool — reflexive learning via GitHub Issues for MCP binary mode *(code complete; requires GitHub token at runtime)*

### ❌ Not Started — Long-term / Nice-to-have (P3)

- [x] #26 Sampling tools — `maf_full_audit`, `maf_migration_suggest`, `maf_review_code`, `maf_explain_error` *(implemented in SamplingTools.cs)*
- [x] #27 Full analysis tools — `maf_detect_cs0618`, `maf_api_diff`, `maf_fan_out_validate`, `maf_executor_pattern_check`, `maf_compatibility`, `maf_migration_path`, `maf_health_check` *(implemented in AnalysisTools.cs)*
- [ ] #28 Docker distribution via GHCR — `ghcr.io/joslat/maf-autopilot:latest`
- [ ] #29 Roslyn analyzer companion — flags void fan-out handlers at write-time in IDE
- [ ] #32 Multi-version migration paths — 1.1.0 → 1.3.0 in one pass without step-by-step

---

### What's Missing Most — Honest Gap Assessment

| Gap | Impact | Effort | Row | Recommendation |
|-----|--------|--------|-----|----------------|
| `maf-autopilot` MVP (3 tools) | ✅ Done | — | #21 | Shipped |
| MCP Resources + Prompts | ✅ Done | — | #22, #23 | Shipped May 5, 2026 |
| `maf-autopilot init` | ✅ Done | — | #24 | Shipped May 5, 2026 |
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
| May 5, 2026 | maf-release-watcher.yml fixes + init command | Fixed 3 CI bugs: removed wrong `dotnet-inspect` global install, upgraded .NET SDK to 9.0.x, changed `dotnet publish` to `dotnet pack`. Fixed plan row 30 self-referencing note. Implemented `maf-autopilot init` (#24): `InitCommand.cs`, idempotent mcp.json merge, copilot-instructions.md generation. Smoke-tested. `dotnet build` clean. `.github/copilot-instructions.md` created from init. | Rows 24 ✅; 25/32 = 78%. |

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
| **Tools** | Callable analysis functions | Implemented: 16 total (3 registry/safety • 7 analysis • 4 sampling • 1 scaffold • 1 feedback) |
| **Resources** | Server-hosted readable content at stable URIs | All 11 SKILL.md files, the migration guide, registry YAML |
| **Prompts** | Slash-command templates that pre-load full context | The `.agent.md` agent workflow files |

**The key mechanism: MCP Prompts can embed Resources.** A `/maf-audit` prompt can return messages containing embedded resource content — the full agent instructions, constraints table, and all relevant skills — fully loaded in one shot, no local files required.

### Target Developer Experience

```bash
# One time per machine
dotnet tool install -g maf-autopilot

# Per project (30 seconds)
cd my-legacy-maf-repo
maf-autopilot init

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

The generated `copilot-instructions.md` is the passive safety net. It's small (~20 lines), generated from the tool, and never needs manual updating (re-run `maf-autopilot init` to regenerate).

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

When the toolkit is updated (new MAF version, new skill), developers run `dotnet tool update -g maf-autopilot` — everything updates. No file copying.

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
3. **`maf-autopilot init`** (#24) — CLI mode detection (when run without MCP arguments, scaffold files). Generates `.vscode/mcp.json` and minimal `copilot-instructions.md`. Low complexity.
4. **NuGet publish** (#25) — CI job to push the `.nupkg` to NuGet.org. Prerequisite: all of the above working. Infra work only.
