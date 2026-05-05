# maf-autopilot

> AI-powered toolkit for Microsoft Agent Framework (MAF) migrations — agents, skills, MCP server, and self-updating guide.

**maf-autopilot** gives GitHub Copilot everything it needs to migrate .NET codebases to MAF 1.3.0 — and stay current as new versions ship — reliably, build-verified, and with zero silent failures.

## Why This Exists

`dotnet upgrade-assistant` handles package version bumps. maf-autopilot handles everything else:

- MAF-specific executor pattern migration (`ReflectingExecutor` → `partial class : Executor` + `[MessageHandler]`)
- Session API changes (`AgentThread` → `AgentSession`)
- Fan-out/fan-in silent failure detection — a class of bug that builds green but runs wrong
- CS0618 obsolete API detection that `dotnet-inspect` alone misses at the overload level
- Build-verified, task-by-task execution with a full tracking table
- A machine-readable Obsolete API Registry so fixes are deterministic, not guessed

---

## Project Structure

```
maf-autopilot/
├── .github/
│   ├── agents/
│   │   ├── maf-migration.agent.md       # Main migration orchestrator
│   │   └── maf-auditor.agent.md         # Audits a codebase → generates migration plan
│   ├── instructions/
│   │   └── maf-constraints.instructions.md  # Always-loaded: breaking changes, constraints
│   ├── skills/
│   │   ├── dotnet-inspect/              # NuGet API surface inspection (+ Obsolete caveat)
│   │   ├── maf-migration-guide/         # Full MAF 1.3.0 guide, navigated by section
│   │   ├── obsolete-api-registry/       # Machine-readable CS0618 fix registry
│   │   ├── migration-plan-creator/      # Generates migration plans with tracking tables
│   │   ├── cs0618-hunter/               # Detects + fixes obsolete API warnings
│   │   ├── fan-out-validator/           # Validates fan-out/fan-in workflow topology (conceptual)
│   │   ├── fan-in-static-analyzer/      # Static code analysis of fan-out topology (code walking)
│   │   ├── nuget-diff-analyzer/         # Post-processes dotnet-inspect diff into actionable report
│   │   ├── maf-release-watcher/         # Detects new MAF releases, updates guide + registry
│   │   ├── workflow-smoke-tester/       # Generates structural smoke tests for workflow patterns
│   │   └── migration-retrospective/     # Post-migration learning → improves the toolkit
│   └── workflows/
│       └── maf-release-watcher.yml      # GitHub Actions: weekly MAF version check + PR
├── guides/
│   └── maf-1.3.0-migration-guide.md    # Source-of-truth MAF 1.3.0 migration guide (21 sections)
├── docs/
│   ├── maf-migration-toolkit-plan.md   # Full project plan and design decisions
│   └── compatibility-matrix.md         # MAF version ↔ dependency version table
└── .maf-version                         # Tracks current MAF version (for release watcher)
```

---

## Skill Orchestration — When to Call What

| You need to…                                      | Use skill                      |
|---------------------------------------------------|--------------------------------|
| Look up MAF API signatures or patterns            | `maf-migration-guide`          |
| Check if a NuGet type/member exists               | `dotnet-inspect`               |
| Detect and fix CS0618 obsolete warnings           | `cs0618-hunter`                |
| Look up the fix pattern for a specific CS0618     | `obsolete-api-registry`        |
| Generate a migration plan for a new codebase      | `migration-plan-creator`       |
| Validate fan-out/fan-in workflow structure        | `fan-out-validator`            |
| Static code analysis of fan-out handler types     | `fan-in-static-analyzer`       |
| Post-process NuGet diff into actionable report    | `nuget-diff-analyzer`          |
| Check for / respond to new MAF versions           | `maf-release-watcher`          |
| Generate smoke tests for migrated workflows       | `workflow-smoke-tester`        |
| Learn from migration surprises, improve toolkit   | `migration-retrospective`      |

> **Never** use `dotnet-inspect` alone to check for obsolete APIs — it does not flag `[Obsolete]` at the individual overload level. Use `cs0618-hunter` (compiler-based) for that.

---

## How to Use

### Option A — Migrate an existing codebase

1. Copy `.github/` into your .NET repository root
2. Add a `migration-plan.md` (use `@maf-auditor` to generate one, or copy from `skills/migration-plan-creator/template.md`)
3. Open Copilot Chat in VS Code
4. `@workspace /maf-migration` — the agent reads both reference documents, audits your code, and migrates task-by-task with build verification after every step

### Option B — Audit first, plan, then migrate

Use `@maf-auditor` on any MAF codebase:
1. It scans your `.csproj` files and `.cs` source for current API patterns
2. Runs `dotnet-inspect diff` for the version delta
3. Cross-references the Obsolete API Registry
4. Produces a complete `src/docs/migration-plan.md` ready for `@maf-migration` to execute

---

## Roadmap

- [x] `maf-migration.agent.md` — full migration orchestrator with build-verified task loop
- [x] `maf-auditor.agent.md` — pre-migration plan generator
- [x] `maf-constraints.instructions.md` — always-loaded breaking changes + hard rules
- [x] `cs0618-hunter` — compiler-based CS0618 detection
- [x] `fan-out-validator` — silent fan-in starvation detection
- [x] `obsolete-api-registry` — machine-readable CS0618 registry (10 entries)
- [x] `migration-plan-creator` — plan template + generation instructions
- [x] `maf-migration-guide` — 21-section guide navigator
- [x] `dotnet-inspect` — enhanced with ⚠️ [Obsolete] limitation warning
- [x] `guides/maf-1.3.0-migration-guide.md` — full 21-section reference guide
- [x] `docs/compatibility-matrix.md` — MAF version ↔ dependency versions
- [x] `nuget-diff-analyzer` — structured diff post-processing skill
- [x] `maf-release-watcher` skill — manual + automated update workflow
- [x] `.github/workflows/maf-release-watcher.yml` — GitHub Actions weekly check
- [x] `fan-in-static-analyzer` — static code analysis of fan-out topology
- [x] `workflow-smoke-tester` — post-migration structural smoke test generation
- [x] `migration-retrospective` — post-migration learning loop
- [x] `maf-autopilot` MCP server — MVP with `maf_api_safety`, `maf_registry_lookup`, `maf_registry_list` tools (SDK 1.2.0, sampling-ready)
- [ ] Full `maf-autopilot` — all 9 tools + Resources + Prompts, NuGet published as global tool
- [ ] Docker distribution via GHCR
- [ ] Roslyn analyzer companion — flags fan-out void handlers at write-time
- [ ] Version-keyed guide sections — load only sections relevant to specific migration path
- [ ] Multi-version migration paths (e.g., 1.1.0 → 1.3.0 in one pass)

---

## Contributing

Whenever a migration surfaces a new surprise (unexpected breaking change, silent runtime failure, wrong pattern in the official docs), update:
1. `guides/maf-1.3.0-migration-guide.md` — add to "Known Misalignments" or create a new section
2. `.github/skills/obsolete-api-registry/registry.yaml` — add the CS0618 entry
3. `.github/instructions/maf-constraints.instructions.md` — update the breaking changes table if needed
