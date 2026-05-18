# Changelog

All notable changes to **maf-autopilot (MAF Doctor)** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Deferred (1.1.0+)
- Full Roslyn data-flow `MafScanPromptInjection` (PROMPT-004 covers the syntactic case today).
- MCP sampling tools (`maf_full_audit`, `maf_migration_suggest`, `maf_open_feedback_issue`) — depend on host sampling support.
- GitHub App `@maf-bot` for org-wide PR comments — separate multi-day project.
- VS Code extension wrapping the MCP server — separate multi-day project.

---

## [1.0.1] — 2026-05-17

Same-day stabilization patch following 1.0.0. **No functional code changes.** NuGet binary content is byte-identical to 1.0.0 for both packages (`maf-autopilot` + `maf-autopilot.Analyzers`). Diff vs v1.0.0:

```
.dockerignore |  2 ++
Dockerfile    | 37 ++++++++++++++++++++++++++-----------
```

The version bump exists to attach a single tag covering 4 post-1.0.0 Docker pipeline fixes so the `docker-publish.yml` workflow rebuilds the GHCR images cleanly via tag push.

### Fixed

- **Dockerfile** — bumped SDK base image `8.0` → `10.0` (the csproj multi-targets `net8.0;net9.0;net10.0` and SDK 8.0 failed NETSDK1045). Added explicit `--framework net10.0` to `dotnet publish` (required for multi-TFM csprojs). Bumped runtime image to match.
- **Dockerfile** — added `COPY Directory.Packages.props Directory.Build.props global.json` to the build stage. The csproj uses Central Package Management; without these the restore failed NU1015 ("PackageReference items do not have a version specified").
- **Dockerfile** — replaced per-folder source COPYs with single `COPY src/maf-autopilot/` (the prior enumeration missed `Commands/` — added during 1.0.0 prep for `init`, `verify-registry`, `registry-extract`, and `badge` subcommands).
- **.dockerignore** — un-excluded `docs/steering/` (4 steering snippets are `EmbeddedResource` items in the csproj since 1.0.0 — they must be in the Docker build context).

### Distribution

- ✅ NuGet `maf-autopilot/1.0.1` republished (binary identical to 1.0.0)
- ✅ NuGet `maf-autopilot.Analyzers/1.0.1` republished (binary identical to 1.0.0)
- ✅ Docker GHCR `:1.0.1` `:1.0` `:1` `:latest` rebuilt with the 4 fixes baked in

---

## [1.0.0] — 2026-05-17

First stable release. Public API committed. **MAF Doctor** brand introduced.

### Highlights

- **25 curated MCP tools** for diagnosing, fixing, and migrating Microsoft Agent Framework code, **all annotated with MCP behavior hints** (`readOnlyHint` / `destructiveHint` / `idempotentHint` / `openWorldHint`) so well-behaved clients can auto-classify them
- **Curated obsolete-API registry** with `applies_to_codebases` markers — version-aware migration paths for MAF 1.0 / 1.2 / 1.3
- **Deterministic Roslyn rewriters** with `--dry-run` and `--all` orchestration modes
- **`MafDoctor`** — A-F holistic health verdict in one call; supports `format=markdown` (default) and `format=json` (7 discrete fields for CI/dashboards)
- **`MafAutoFixAll`** — dependency-ordered fix-everything pass
- **`MafRunCs0618Hunt`** — compiler ground truth + registry join
- **Roslyn analyzer NuGet** (`MAF001` / `002` / `003`) for write-time enforcement
- **AI-fill maintenance loop** — release-watcher + registry-extract + Copilot Coding Agent + verify-registry PR gate
- **Multi-version sample regression CI** — proves fixes don't break older codebases
- **12 skills + 7 specialist agents** for GitHub Copilot Coding Agent
- **7 MCP prompts** + **6 MCP resources** (constraints, registry, guide, rules, help, skills)
- **Plugin packaging via `maf-autopilot init`** — drops `.vscode/mcp.json` + 3 steering snippets (`.github/copilot-instructions.md`, `CLAUDE.md`, `AGENTS.md`) with merge-not-overwrite semantics; optional `--with-cursor` for `.cursorrules`
- Built on **dotnet-inspect v0.7.8** (which surfaces `[Obsolete]` natively); we add MAF-specific curated fix recipes + version-aware filtering on top
- **Security model documented** in `docs/security.md` (user-facing overview) + `docs/security/threat-model.md` (deep technical detail). MCP-server scanned with **Cisco mcp-scanner v4.6.0** (yara analyzer, fully local): **25/25 tools SAFE, 0 findings**.

### Versioning note for alpha users

Previous releases were tagged `1.3.0-alpha-1` through `1.3.0-alpha-3` to track the MAF target version. 1.0.0 decouples our versioning from MAF's. **maf-autopilot 1.0.0 supports MAF 1.0 / 1.2 / 1.3 simultaneously.** Existing alpha users upgrade with:

```bash
dotnet tool update -g maf-autopilot --version 1.0.0
```

### Recommended companion MCPs

For best results, pair maf-autopilot with:

- **[Microsoft Learn MCP](https://learn.microsoft.com/api/mcp)** — live MAF documentation
- **[Context7](https://github.com/upstash/context7)** — version-specific NuGet docs
- **[DeepWiki MCP](https://mcp.deepwiki.com/mcp)** — query `microsoft/agent-framework` source
- **[Serena](https://github.com/oraios/serena)** — semantic IDE-shape navigation across 40+ languages

### Breaking changes from 1.3.0-alpha-3

- Skill `fan-in-static-analyzer` removed (was a redirect; use `maf-fan-out-validator` directly).
- 5 skills renamed with `maf-` prefix for namespace clarity: `fan-out-validator` → `maf-fan-out-validator`, `migration-plan-creator` → `maf-migration-plan-creator`, `migration-retrospective` → `maf-migration-retrospective`, `obsolete-api-registry` → `maf-obsolete-api-registry`, `workflow-smoke-tester` → `maf-workflow-smoke-tester`. Skills without the prefix (`cs0618-hunter`, `dotnet-inspect`, `nuget-diff-analyzer`) are unchanged — those are generic .NET concepts, not MAF-specific.
- `MafDoctor` JSON output re-split into 7 discrete fields (`rule_id`, `severity`, `file`, `line`, `issue`, `fix_description`, `auto_fixable`) — CI dashboards can now link directly to file:line without parsing concatenated strings.
- Tool descriptions tightened (~30% rewritten for verb-first wording, when-to-use clarity, neighbor disambiguation). Existing prompt scripts referencing tools by name still work; descriptions are server-side only.
- `MafDoctor` now supports `format=json` parameter for CI-friendly structured output (default remains `markdown`).
- `dotnet-inspect` dependency bumped from `0.7.6` to `0.7.8` across all 21 pin sites (workflows, agents, skills, tools, tests, guides). v0.7.8 surfaces `[Obsolete]` natively — we add the MAF-specific curated fix recipes + version-aware filtering on top.
- **MCP tool annotations** added to all 25 [McpServerTool] decorations (readOnlyHint / destructiveHint / idempotentHint / openWorldHint). Well-behaved clients (Claude Code, VS Code Copilot) will no longer show "may modify system" confirmation prompts for read-only scanners. Classification: 18 read-only local + 3 read-only open-world (shells dotnet-inspect/build) + 2 destructive writers + 2 idempotent scaffolders. Adopts the pattern from microsoft/skills/mcp-builder.

### Documentation

- New: `AGENTS.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `LICENSE`
- New: `docs/steering/` (paste-able snippets for `.github/copilot-instructions.md`, `CLAUDE.md`, `AGENTS.md`)
- New: `docs/security/threat-model.md`
- New: `docs/output-schemas.md` (JSON schema for `MafDoctor` and `MafAuditPullRequest`)

---

## Pre-1.0 history

### [1.3.0-alpha-3] — 2026-05-12

This release captures the full toolkit after the magic-path sprint, the multi-Opus review round, and the Phase E/F polish.

### Added
- **Greenfield scaffolding** (Phase #60 / Track 1):
  - `MafNewAgent(projectPath, agentName, instructions?)` MCP tool — generates a clean `ChatClientAgent`-based class with `UseOpenTelemetry` wired, `MaxOutputTokens` cap, and a hermetic xUnit smoke test.
  - `MafNewExecutor(projectPath, executorName, inputType?, outputType?)` MCP tool — generates a `sealed partial class : Executor` with a fan-out-safe `Task<T>` handler and a reflection-based shape-verification test.
  - CLI: `maf-autopilot new agent <Name>` and `maf-autopilot new executor <Name>`.
- **Roslyn analyzer companion** (Phase #29 / Track 2): a separate `maf-autopilot.Analyzers` NuGet package ships **MAF001** (fan-out handler must return `Task<T>`/`ValueTask<T>`), **MAF002** (avoid `DefaultAzureCredential` outside test code), **MAF003** (`EnableSensitiveData = true` outside test code). Write-time IDE enforcement.
- **Workflow simulation** (Phase #65 / Track 3): `MafSimulateWorkflow(repoPath)` — static topology graph + Mermaid diagram + per-edge completion forecast. Resolves variable-bound `AddEdge(reviewer, finalizer)` patterns via local-scope type maps.
- **Code explanation** (Phase #64 / Encore): `MafExplain(snippet)` — annotated per-line breakdown of MAF API touchpoints, cross-referenced against the obsolete-API registry + guide sections.
- **Health letter** (Phase #59 / E.4): `MafDoctor(repoPath)` MCP tool + `maf-autopilot doctor .` CLI subcommand — single-command A/B/C/F grade. Aggregates 4 scanners (anti-pattern + fan-out + prompt-lint + cost).
- **PR-scoped audit** (Phase #52): `MafAuditPullRequest(repoPath, baseBranch?)` — runs every scanner only on files changed in this branch vs base.
- **Pre-upgrade dry-run** (Phase #54): `MafPreUpgradeDryRun(repoPath, packageId, currentVersion, targetVersion)` — combines `MafDiffPackage` with codebase grep to surface user-specific breaking-change impact BEFORE any code change.
- **Cost auditor** (Phase #62): `MafEstimateCost(repoPath)` — flags every `RunAsync`/`RunStreamingAsync` site missing a `MaxOutputTokens` cap; estimates input-token cost from nearest `Instructions` literal.
- **Prompt linter** (Phase #61): `MafLintAgentPrompt(repoPath)` — 4 rules covering empty `Instructions`, token bloat, missing refusal patterns, untrusted-input concatenation (prompt-injection risk).
- **Multi-version migration path** (Phase #32): `MafMigrationPath(currentVersion, targetVersion)` — walks the version-keyed guide metadata, returns the ordered set of intermediate-step sections.
- **Anti-pattern scanner** (Phase #56): `MafScanAntiPatterns(repoPath, format?)` — 9 rules across security, concurrency, observability, identity, topology. `format: "markdown" | "sarif"` for CI integration.
- **Fan-out validator** (Phase #41): `MafValidateFanOut(path, format?)` — Roslyn syntax-tree walk for silent fan-in starvation.
- **CS0618 hunter** (Phase #41): `MafRunCs0618Hunt(projectPath)` — shells `dotnet build`, parses diagnostics, joins to registry.
- **NuGet diff** (Phase #41): `MafDiffPackage(packageId, oldVersion, newVersion)` — wraps `dotnet-inspect@0.7.8 diff`.
- **Best-practice reviewer agent** (Phase #42): `maf-best-practice-reviewer.agent.md` — steady-state audit on clean 1.3.0 codebases, produces `audit-report.md`.
- **Docker GHCR distribution** (Phase #28): multi-arch (`linux/amd64`, `linux/arm64`) container, semver-keyed tags, stable-only `latest`.
- **Live rule catalog** (Phase E.6): `maf://rules` resource, auto-generated at runtime from the actual rule set. Kills doc drift structurally.
- **CI/CD workflows**: weekly drift detector, per-PR audit, weekly MAF version watcher with auto-registry-extract via `maf-autopilot registry-extract <pkg> <oldVer> <newVer>` CLI subcommand.

### Changed
- **Tool surface consolidation** (Phase F.2): `MafScanAntiPatternsSarif` + `MafValidateFanOutSarif` removed from the public surface; reach SARIF output via `format="sarif"` parameter on the main tools. 19→17 tools.
- **`MafDoctor` now aggregates 4 scanners** (Phase E.4) — was 2. Includes anti-pattern + fan-out + prompt-lint + cost.
- **`init` now writes a thin pointer** (Phase B.5) to `maf://constraints` instead of an embedded copy. No more drift between this repo and downstream consumer copies.
- **README reconciled with reality** (Phase E.2): tool counts, skill counts, distribution status all synced. New "First 5 minutes" section with three concrete starter commands.
- **`PathGuard` shared helper** (Phase E.1) applied to all 8 `repoPath`/`projectPath` tools. Universal `..` traversal + shell-metacharacter guard.
- **`SourceFileWalker` shared helper** (Phase E.5) deduplicates the 17-place `EnumerateScannableFiles` + `MakeRelative` copy-paste pattern.
- **`RegistryExtractCommand` uses YamlDotNet** (Phase F.3) instead of string-concat. Eliminates YAML-special-character fragility.
- **`MafSimulateWorkflow` resolves variable-bound edges** (Phase A.1 follow-up): previously only matched `AddEdge(TypeName, TypeName)` shapes; now resolves `var reviewer = new ReviewerExecutor()` patterns via local-scope type maps.
- **Scaffolder templates use `UseOpenTelemetry(sourceName: ...)`** instead of `loggerFactory:` — matches the canonical migration-guide form.
- **All 3 agents (`maf-migration`, `maf-auditor`, `maf-best-practice-reviewer`) and `maf-constraints.instructions.md` rewired** to invoke the MCP tools (Phase A.1) instead of "load skill / run by hand."

### Fixed
- **Path traversal**: `MafScanAntiPatterns`, `MafValidateFanOut`, `MafSimulateWorkflow`, `MafDoctor`, `MafEstimateCost`, `MafLintAgentPrompt`, `MafAuditPullRequest`, `MafPreUpgradeDryRun` all gained the `..`-segment guard that was previously only on `MafNewAgent` / `MafNewExecutor`.
- **csproj `<Version>` drift**: was `1.3.0-alpha-5` while NuGet only had `1.3.0-alpha-3`; reverted csproj to match NuGet reality.
- **TROUBLESHOOTING test count**: was stale at "116 tests"; current count is ~365.
- **Generated scaffolder code**: now includes `using System.Threading;` and `using System.Threading.Tasks;` (caught by Phase F.4 compile-validation test).
- **Generated executor template**: `return default!;` changed to `return default(TOutput)!;` to avoid CS8716 in generic contexts.
- **Analyzer help-link anchors**: GitHub-slugified fragments don't resolve reliably; linked to doc root instead.
- **MCP server response on case-insensitive resource lookup**: `MafResources.GetSkill("CS0618-HUNTER")` previously crashed with file-not-found because the embedded-resource lookup was case-sensitive; now normalizes via `HashSet.TryGetValue` canonical form.
- **`InitCommand` malformed-JSON handling**: backs up `.vscode/mcp.json` to `.vscode/mcp.json.bak.<timestamp>-<guid>` on parse failure instead of silently overwriting. Lenient JSONC parsing (comments + trailing commas) is now the default.
- **`MafSimulateWorkflow` "silently returns ✅" failure mode**: variable-binding resolution + per-argument unresolved tracking.
- **`Cs0618HuntTool.MatchToRegistry` false positives**: only extracts the first quoted segment from a CS0618 message (the obsolete symbol), not the replacement hint.
- **`FanOutValidatorTool.ClassifyReturnType`**: `IAsyncEnumerable<T>` correctly classified `Ok`; raw types (`int`, `string`) now flagged `LikelyInvalid` instead of "review manually".

### Test infrastructure
- **0 → 365 xUnit tests** across two test projects (354 main + 11 analyzer).
- **Dogfood pattern**: scaffolder output is regression-tested against `MafScanAntiPatterns` and `MafValidateFanOut`.
- **Compile-validation pattern** (Phase F.4): scaffolder output additionally compiled via `CSharpCompilation.Create` against MAF surface stubs. Caught two real template bugs on first run.
- **CI**: `dotnet test` gates `dotnet pack` in `release.yml`.
- **xUnit + analyzer-testing pins** for deterministic CI (`xunit 2.9.2`, `Microsoft.NET.Test.Sdk 17.12.0`, `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit 1.1.2`).

### Upstream alignment
- **`dotnet-inspect` bumped 0.7.6 → 0.7.8** across all 21 pin sites. v0.7.8 ([release notes](https://github.com/richlander/dotnet-inspect/releases/tag/v0.7.8)) surfaces `[Obsolete]` members in listings — the gap the toolkit was originally built to work around.
- **Author attribution corrected**: `dotnet-inspect` is by [@richlander](https://github.com/richlander), not `filipw` (which is the separate `dnx` tool).

---

## [1.3.0-alpha-2] — 2026-05-01

Internal alpha; superseded by `1.3.0-alpha-3`. Not announced.

---

## [1.3.0-alpha-1] — 2026-04-30

Initial MCP server prototype. Three tools (`MafApiSafety`, `MafRegistryLookup`, `MafRegistryList`), 11 skills, 2 agents, 10 registry entries. Validated against one real migration (`maf-claims-fraud-guardian` 1.2.0 → 1.3.0). Internal alpha; not announced.

[Unreleased]: https://github.com/joslat/maf-autopilot/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/joslat/maf-autopilot/releases/tag/v1.0.0
[1.3.0-alpha-3]: https://www.nuget.org/packages/maf-autopilot/1.3.0-alpha-3
[1.3.0-alpha-2]: https://www.nuget.org/packages/maf-autopilot/1.3.0-alpha-2
[1.3.0-alpha-1]: https://www.nuget.org/packages/maf-autopilot/1.3.0-alpha-1
