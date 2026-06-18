# Changelog

All notable changes to **maf-autopilot (MAF Doctor)** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

## [1.2.10] - 2026-06-18

### Fixed

- **Flaky-test class hardened.** Every NonBacktracking regex carried a 100ms `MatchTimeout` that intermittently tripped on the engine's one-time DFA-build cost under CI load (`RegexMatchTimeoutException` — surfaced as `LlmFencingTests.Fence_StripsHtmlComments_Multiple`). Raised to **2s** across all ~17 sites. NonBacktracking is linear and inputs are length-bounded, so the timeout is only a cold-start backstop, not the primary ReDoS defense — raising it doesn't weaken safety.
- **Demo sample builds again.** `samples/maf-1.3-sample` pinned `maf-doctor.Analyzers` at `1.3.0-alpha-1` (never published — the rename left a dead version pin), so `dotnet restore` failed. Repinned to `1.2.9`.

### Added

- **README hero cast.** Added the truthful `install-cast` GIF (install → `maf-doctor doctor .` → grade F + the top fixes) below the intro, via absolute raw URL so it renders on GitHub and nuget.org.

## [1.2.9] - 2026-06-16

### Changed (branding)

- **README logo → white-background `MAFDoctorLogo.png`.** The transparent logo's dark artwork disappeared on dark backgrounds (GitHub dark theme); the white-background version renders consistently everywhere. Republished so the nuget.org package page (README is baked per-version) gets the fix too.

## [1.2.8] - 2026-06-16

### Added (branding)

- **README now leads with the MAF Doctor logo** (`assets/MAFDoctorLogoTransparent.png`, referenced by absolute raw URL so it renders on both GitHub and the nuget.org package page).
- **Both NuGet packages ship a package icon** (`MAFDoctorIcon.png`) — `maf-doctor` and `maf-doctor.Analyzers` now show the brand icon on nuget.org.
- **The MCP server advertises its icon** via the 2025-11 spec's `icons` field on `serverInfo` (`Implementation.Icons`, supported by ModelContextProtocol 1.3.0) — clients that support server icons can render it; older clients ignore it.

## [1.2.7] - 2026-06-16

### Added

- **`maf-doctor --version` / `-v` and `--help` / `-h`.** The CLI had no version or help command, so any non-subcommand argument fell through to MCP-server mode — meaning `maf-doctor --version` (which the quickstart told users to run) just hung on stdio. Both now print and exit cleanly.

### Fixed

- **Claude Code couldn't see the MCP server that `init` wired.** The `.mcp.json` (Claude Code) entry carried a `"type": "stdio"` field that some Claude Code builds don't recognize. Dropped `type` from the Claude entry — stdio is the implied default — while VS Code's `.vscode/mcp.json` keeps it. `init` now emits a `.mcp.json` Claude Code reads cleanly.
- Added a project-root `.mcp.json` to **this** repo (it previously had only the VS-Code-format `.vscode/mcp.json`), so Claude Code picks up maf-doctor when developing the toolkit itself.

## [1.2.6] - 2026-06-14

### Fixed (stop pinning MAF 1.3.0 in served text)

- **Removed hardcoded `MAF 1.3.0` version pins from the tool's user-facing output** — MCP prompt bodies, resource titles/descriptions, tool descriptions, and scaffolder comments said things like "Target MAF version: 1.3.0", "MAF 1.3.0 Hard Constraints", "scaffold MAF 1.3.0 boilerplate". Reworded to version-agnostic phrasing ("MAF", "the latest stable release") so they don't go stale as MAF ships. Audited all 19 served-source files (75 references); fixed 37 display/command pins, deliberately kept the ~38 that are historical facts ("removed in 1.3.0"), per-version data (the compatibility matrix's `1.3.0` row), registry version keys, or user-supplied version-argument examples (diff/migration-path tools).
- **`maf-doctor new agent` no longer prints stale install commands.** `NewCommand` told users to run `dotnet add package Microsoft.Agents.AI --version 1.3.0` (and the Workflows/Generators packages) — pinning a years-old MAF. Dropped the `--version 1.3.0` so NuGet installs the latest stable.
- All 749 + 15 tests pass across net8/9/10 (no behavior change — text only).

## [1.2.5] - 2026-06-14

### Changed (`init` steering layout — no longer merges into your own files)

- **`init` now writes its steering as overwrite-on-reinit sidecars in each tool's auto-loaded convention location, instead of appending into your hand-authored files:**
  - **Copilot** → `.github/instructions/maf-doctor.instructions.md` (with `applyTo: '**'` frontmatter). `init` no longer creates or touches `.github/copilot-instructions.md`.
  - **Claude Code** → `.claude/maf-doctor.md`, imported via a single stable `@.claude/maf-doctor.md` line in `CLAUDE.md` (added once; the rest of your `CLAUDE.md` is never touched).
  - **AGENTS.md** → a sentinel-delimited managed block (`<!-- BEGIN maf-doctor -->…<!-- END maf-doctor -->`) that is replaced in place; content outside the block is preserved.
  - **Cursor** (`--with-cursor`) → `.cursor/rules/maf-doctor.mdc`.
- **Why:** the previous append-into-existing behavior froze the guidance at first-init and (before 1.2.4) could stack duplicates. Sidecars are **overwritten on every `init`**, so re-running refreshes the guidance to the installed version, never duplicates, and never edits your own files. Added sidecar/managed-block tests (16 InitCommand tests green across net8/9/10).

## [1.2.4] - 2026-06-14

### Added

- **`init` now wires the MCP server for Claude Code too.** It writes `.mcp.json` at the repo root (top-level `mcpServers`) alongside the existing `.vscode/mcp.json` (top-level `servers`) — so the `maf-doctor` tools are available natively in Claude Code, not just VS Code / Copilot. Same merge-not-overwrite + legacy-key-recognition + JSONC-safe behavior as the VS Code path. (CLAUDE.md / AGENTS.md steering were already written.)

### Fixed

- **Stale `maf-autopilot` text in the `init`-emitted steering snippets.** The embedded snippets dropped into a user's repo (CLAUDE.md, AGENTS.md, .github/copilot-instructions.md, .cursorrules) still told assistants to "defer to maf-autopilot's tools" / referenced the "maf-autopilot registry". Renamed the prose to maf-doctor (the MCP tool names like `MafDoctor` were already correct).
- **Duplicate-append bug in `init`.** The merge idempotency marker (`## Using maf-doctor`) no longer matched the copilot snippet header after the rename, so re-running `init` appended a second steering section. Headers and markers are realigned, and the marker set now also covers the `.cursorrules` snippet. Added regression + Claude-path tests.

## [1.2.3] - 2026-06-14

### Changed (docs)

- **Toned down inline-code density in the README.** NuGet.org styles inline `code` spans as red-on-pink, so the previous README (~50 inline-code spans) rendered as a wall of red on the package page (it looks fine on GitHub). Rewrote prose to use plain/bold text, keeping backticks only where monospace genuinely helps — the literal API transformations in "Why This Exists" and the one registry path a contributor edits (6 spans total). Fenced command blocks are unchanged.

## [1.2.2] - 2026-06-14

### Fixed (NuGet package metadata)

- **Corrected the published package descriptions.** Both `maf-doctor` and `maf-doctor.Analyzers` still carried the pre-rename blurb on nuget.org ("MAF autopilot MCP server … MAF 1.3.0 migrations" / "Pairs with the maf-autopilot MCP server"). They now describe MAF Doctor accurately and version-agnostically.
- **Removed the README "Naming" note.** It explained the rename/deprecation of a package that was never publicly announced — pointless noise for a new reader. The intro now states plainly that `maf-doctor` is a **.NET global tool installed from NuGet** (`dotnet tool install -g maf-doctor`), with `maf-doctor.Analyzers` as a separate analyzer package.

## [1.2.1] - 2026-06-14

**First release published under the `maf-doctor` package id** (and `maf-doctor.Analyzers`) — this is the version where the rename below actually reaches NuGet.

### Changed (BREAKING — full rename to `maf-doctor`)

- **The NuGet package + CLI are renamed `maf-autopilot` → `maf-doctor`** (and `maf-autopilot.Analyzers` → `maf-doctor.Analyzers`). The brand, repo, MCP server id, and C# namespace were already MAF Doctor; this completes the rename so the package/command match. Done pre-public-announcement, so the break is acceptable.
  - **For users:** `dotnet tool uninstall -g maf-autopilot && dotnet tool install -g maf-doctor`; the command is now `maf-doctor …`. Re-run `maf-doctor init` (it recognizes a legacy `maf-autopilot` mcp.json server entry and won't duplicate). The Docker image runs `maf-doctor.dll`.
  - The old `maf-autopilot` package (≤ 1.2.0) will be **deprecated on nuget.org** with a pointer to `maf-doctor`.
  - The internal `src/maf-autopilot/` project folder + `.csproj`/`.sln` filenames are unchanged (invisible to users; avoids high-churn path rewrites).

### Docs

- **README leaned to the essentials** — removed the Project Structure tree, the Skill Orchestration catalog, and the dated distribution block; deduplicated the `dotnet-inspect` notes; trimmed the companion-MCP recommendation to **Microsoft Learn MCP** (we don't wire Context7/DeepWiki/Serena); added an **"It updates itself"** section explaining how and when the self-update watcher runs (Thursday 06:00 UTC; minors auto-commit, majors escalate); and dropped hardcoded version pins so the docs stop going stale as MAF ships.
- Fixed stale tool/resource/prompt counts and the `maf-autopilot.Analyzers` id in `docs/architecture.md`, and a stale "current release `1.3.0-alpha-5`" claim in `docs/setup.md`. The unrendered demo-cast `<img>` tags were removed (the `.tape` sources + `scripts/make-cast` remain — re-add once rendered).

## [1.2.0] - 2026-06-14

### Added

- **MAF 1.10.0 support** — 22 registry entries (obsolete/changed APIs), the compatibility-matrix row (`Microsoft.Extensions.AI >= 10.6.0`), and the `1.6.1 → 1.10.0` migration guide. Auto-detected by the now-fixed release watcher; migration content authored by the Copilot coding agent and verified by the registry gates. _(AI-authored migration guidance — review against the upstream `microsoft/agent-framework` changelog when convenient.)_

### Fixed (self-update reliability)

The scheduled **MAF Release Watcher** and **MAF Drift Detector** had failed on
**every** scheduled run since creation, for two unrelated reasons:
- **Watcher** — the NuGet version filter was an inline `python3 -c` whose code
  inherited the shell block's indentation → `IndentationError` on every
  scheduled run (only an explicit-version manual dispatch bypassed it). Extracted
  to `.github/scripts/get_latest_stable.py` (+ unit tests).
- **Drift detector** — `gh issue create --label maf-drift` hard-failed because
  the `maf-drift` / `needs-review` labels never existed. The workflow now
  self-provisions them idempotently (`gh label create --force`).
- **Observability** — both workflows open/de-dupe a `maf-release`-labelled
  tracking issue on failure (`if: failure()`), so a scheduled failure can no
  longer go unnoticed for weeks.
- **Engine** — major MAF bumps escalate to a review issue instead of
  auto-committing to `main`; registry append is idempotent; the doctor gained an
  opt-in `doctor … --exclude <substr>` so the drift grade reflects product code
  (samples / test fixtures excluded); the two crons are staggered with
  `concurrency:` guards; the watcher polls daily (was weekly). Plus a batch of
  latent fixes across the AI-fill / verify / semantic-review chain and the
  Python helpers (prerelease-safe guide gen, union guide-section validation,
  placeholder gate, sticky verify comment, non-fatal bot-id resolution, …).
- **CI** — `ci-invariants` gained a workflow-lint + helper-test gate
  (actionlint + `compileall` + `pytest`) so this class of breakage is caught
  pre-merge.

### Changed (brand — MAF Doctor)

- The MCP **server id** is now `maf-doctor` (the brand) in `.vscode/mcp.json`,
  the README / setup / workshop config snippets, and what `maf-autopilot init`
  writes into a user's `.vscode/mcp.json`. The **`command` stays `maf-autopilot`**
  (the frozen global-tool binary), so existing entries keep launching and
  `dotnet tool install --global maf-autopilot` is unchanged. `init` recognizes
  both the new `maf-doctor` and the legacy `maf-autopilot` server keys, so
  re-running it never creates a duplicate.
- **Maintainer follow-ups (post-merge, not in this PR):** rename the GitHub repo
  `joslat/maf-autopilot` → `joslat/maf-doctor` (GitHub auto-redirects old URLs),
  then flip the remaining `joslat/maf-autopilot` URL / `RepositoryUrl` / SARIF /
  OCI-label references to `joslat/maf-doctor` (deferred here to avoid a 404
  window). The GHCR image path follows the repo rename automatically; **new tags
  publish under `ghcr.io/joslat/maf-doctor`** — old `ghcr.io/joslat/maf-autopilot`
  tags stay pullable. The NuGet package id and CLI command are deliberately
  **not** renamed (immutable id; renaming would break every existing install).

### Security (security hardening release — branch `security-hardening-v1.1`)

Comprehensive hardening pass following a three-agent Opus security review.
Each phase carried its own Opus full-phase review + fixup commit; the final
comprehensive review returned **READY TO SHIP**. Anchored to the canonical
MCP / LLM / GHA security canon documented in
[`docs/security.md` "Canonical references"](docs/security.md#canonical-references).
The full attack-surface map + closure list is in
[`docs/security/threat-model.md`](docs/security/threat-model.md) §3–§5.

#### Critical-severity closures (5)
- **C1 — Path-escape via `AutoFix.specificFile`** — added `PathGuard.ValidateContainment`
  (canonicalize + jail + reparse-point reject at both leaf AND parent-chain).
  An LLM-supplied `specificFile = "C:\\Users\\victim\\..."` is now rejected
  with a non-leaky error message.
- **C2 — `MafRunCs0618Hunt` annotation drift** — was `[ReadOnly=true]` despite
  spawning `dotnet build` (executes MSBuild targets, source generators,
  NuGet post-install). Now `[ReadOnly=false, OpenWorld=true]`; MCP-spec-
  compliant clients prompt for consent before auto-invoke.
- **C3 — Code injection via `AgentScaffolder.projectNamespace`** — added
  `IsValidNamespace` (Roslyn `ParseName` + roundtrip equality) mirroring the
  existing `IsValidTypeExpression` defense applied to `inputType`/`outputType`.
- **C4 — Supply-chain prompt injection via upstream MAF release notes** —
  `gen_guide_section.py` now fences both `release-notes.txt` AND `diff-core.txt`
  via new shared `LlmFencing` helpers (HTML-comment strip + UTF-8 byte cap +
  random-sentinel BEGIN/END markers with "treat as data" framing). Same
  fence applied at every downstream hop (`build_ai_fill_issue_body.py`,
  `ai_review_build_prompt.py`, `RegistryExtractCommand.notes:` embed).
- **C5 — Workflow `${{ inputs.* }}` injection** — every `workflow_dispatch`
  workflow that accepts inputs gained a `validate-inputs` job (semver-regex /
  enum-check, `permissions: {}`); every `run:` block migrated to `env:`
  redirect pattern. CI invariant blocks regressions on `inputs.*`,
  `needs.*.outputs.*`, and `steps.*.outputs.*`.

#### High-severity closures (~11)
- **LLM-to-LLM prompt smuggling via `MafDraftIssue`** — every user-controlled
  field (symptom, expected, actual, snippet) wrapped in `LlmFencing.Fence`
  + `NeutralizeFences` + `StripHtmlComments` (snippet sits in a `csharp`
  code-block so triple-backtick neutralization is required).
- **`MafPrompts.Debug.errorOrSymptom`** — replaced single-line `>` blockquote
  bypass with full `LlmFencing.Fence` + `BoundedInput.Validate`.
- **`MafNewAgent` / `MafNewExecutor` annotation drift** — both `[Destructive=true]`
  now (they write new `.cs` files to the project tree).
- **`MafDiffPackage` / `MafPreUpgradeDryRun` annotation drift** — both
  `[OpenWorld=true]` now (they reach `api.nuget.org`).
- **`VerifyRegistryCommand` `pre-X.Y.Z` + `IsRawDraft` bypass** — pre-1.0 +
  raw-draft branches now run `CheckContentSafety` instead of skipping all
  checks. `IsRawDraft` tightened to require `// TODO` at line-start regex
  (substring-anywhere allowed `// TODO` inside string literals to re-qualify).
- **`SourceFileWalker` symlink-follow + `MakeRelative` info leak** —
  `EnumerationOptions.AttributesToSkip = ReparsePoint | Hidden | System`;
  `MakeRelative` throws on out-of-root instead of leaking the absolute path.
- **`MAF_REGISTRY_PATH` unguarded** — `ValidateOverridePath` rejects shell
  metachars + NUL/CR/LF + reparse-points; optional `MAF_REGISTRY_PATH_ROOTS`
  env-var allowlist.
- **`RegistryExtractCommand` persistence injection** — `notes:` field
  attacker-controllable via `dotnet-inspect diff` output is now fenced
  (8 KB cap because the value persists forever in `registry.yaml`).
- **`ai_review_build_prompt.py` PR-registry injection** — each changed
  registry entry now fenced before embedding in the GPT-4o review prompt.
- **`ExecutorSealedRewriter` produces uncompilable `abstract sealed class`** —
  abstract / static guard skips with leading-trivia warning comment.
  Idempotence via source-text dedup so repeat passes don't duplicate.
- **`SyncOverAsyncRewriter` produces uncompilable `await`** — lock-block /
  non-async-method / accessor / non-async-lambda guards. Same idempotence pattern.

#### G-tier gaps closed (added during the audit)
- **`.github/CODEOWNERS`** — `@joslat` required on security-critical paths
  (`src/maf-autopilot/Tools/`, `Scaffolding/`, `Commands/`, `Prompts/`,
  `Resources/`, `Data/`, `Analyzers/`, `.github/workflows/`, `.github/scripts/`,
  `docs/security*`, `private/`, root metadata like `Dockerfile` and
  `Directory.Build.props`).
- **Root `SECURITY.md`** — GitHub Security tab entry point; 72-hour
  acknowledgement + patch-release SLA + scope + recognition policy.
- **`.github/workflows/mcp-scanner.yml`** — Cisco mcp-scanner runs on every
  PR against `main` touching `src/maf-autopilot/**`, plus weekly Monday cron.
  Sticky PR comment with results. Advisory for the first 5 consecutive
  clean runs, then promote to hard-fail.
- **`maf-pr-audit.yml` compiler-bomb cap** — skip Roslyn-heavy audit when
  PR changes > 200 `.cs` files OR > 5 MB aggregate; `git diff -z` +
  `xargs -0` + `stat -c %s --` for filename-safety.

#### Defense-in-depth additions
- **`BoundedInput.Validate`** — single helper, UTF-8 byte count, six named
  cap classes (`PathBytes` / `IdentifierBytes` / `ShortTextBytes` /
  `SnippetBytes` / `InstructionsBytes` / `AggregateBytes`). Applied across
  every MCP tool's user-controlled string parameter.
- **`LlmFencing.Fence` + `LlmFencing.StripHtmlComments` (C# + Python parity)** —
  the reusable primitive for "user content destined for another LLM."
- **Annotation reflection tests (`McpToolAnnotationTests`)** — locks the
  `[McpServerTool]` contract for every tool whose annotation gates MCP-client
  auto-invocation behavior.
- **Rewriter idempotence property tests (`RewriterIdempotenceTests`)** —
  pass-1 must equal pass-2 for every rewriter; catches a regression class
  that would otherwise only surface in production.
- **`SensitiveDataAnalyzer` dictionary-form parity** — MAF003 now catches
  `["EnableSensitiveData"] = true` in addition to the bare-identifier and
  member-access forms; rewriter extended to remove the standalone statement
  shapes too.
- **Regex hygiene** — every `new Regex(` in `src/maf-autopilot/` carries
  `NonBacktracking + MatchTimeout = 100ms` (and the CI invariant now
  catches the target-typed `Regex X = new(...)` form too).
- **SHA-pinning** — 9 third-party actions across 23 call sites pinned to
  full SHAs with a trailing-comment specific tag; Dependabot's
  `github-actions` ecosystem updates both in lockstep.

#### CI invariants (`.github/workflows/ci-invariants.yml`)
Five hard-fail jobs that lock the invariants going forward:
1. `Process.Start` allowlist + unsafe `Arguments` string property block.
2. Regex hygiene (`NonBacktracking + MatchTimeout` required on every `new Regex(`).
3. `EnumerationOptions` on recursive `Directory.EnumerateFiles` walkers.
4. `${{ inputs.* | needs.*.outputs.* | steps.*.outputs.* }}` blocked inside `run:` blocks.
5. Explicit `permissions:` block on every workflow.

#### Documentation
- `docs/security.md` — six new "Known MCP attack classes" subsections with
  canonical-source citations + a new "Canonical references" section
  enumerating 13 sources (MCP spec, OWASP LLM / MCP / Agentic Top 10s,
  MITRE ATLAS, Cisco MCP scanner, Invariant Labs, Snyk, Keysight, GitHub
  Actions secure-use, NIST SP 800-218A, CSA mcpserver-audit).
- `docs/security/threat-model.md` — §3 expanded with 5 new attack-surface
  subsections (code injection via namespace, LLM-to-LLM smuggling,
  supply-chain PI via release notes, workflow input injection, third-party
  action supply chain); §4 split into "carried over from 1.0" + 18-item
  "Added in 1.1" list; §5 rewritten with 7 documented residual gaps each
  carrying a v1.2.x tracking note.
- `CONTRIBUTING.md` — new 7-item security rubric for MCP tool contributions
  (PathGuard containment, BoundedInput length caps, annotation correctness,
  ArgumentList discipline, LlmFencing on LLM-bound content, recursive walker
  safety, regex hygiene). CI invariants enforce most items mechanically.

### Deferred (1.2.0+)
- **`PullRequestAuditTool` inline `Process.Start`** → migrate to `ProcessRunner`
  (bypasses the 16 MB output cap today; larger refactor than v1.1 scope).
- **`ProcessRunner` streaming-cap-during-read + env scrubbing** (`MAF_FORWARD_ENV=*`
  opt-out) — both risk breaking `dotnet build` for users with custom env setups.
- **Semantic-model upgrade** for `DefaultAzureCredentialRewriter` /
  `FanInArgOrderRewriter` — pure-syntax matching can silently rewrite unrelated
  user types with name collisions. Bounded today by `--dry-run` default
  + idempotence tests.
- **Roslyn-analyzer NuGet signing posture** — undocumented today.
- **Commit-signing requirement** — maintainer choice.
- Full Roslyn data-flow `MafScanPromptInjection` (PROMPT-004 covers the syntactic case today).
- MCP sampling tools (`maf_full_audit`, `maf_migration_suggest`, `maf_open_feedback_issue`) — depend on host sampling support.
- GitHub App `@maf-bot` for org-wide PR comments — separate multi-day project.
- VS Code extension wrapping the MCP server — separate multi-day project.
- **Rung-3 of the AI trust ladder**: flip `COMMENT_ONLY_MODE = False` in `ai_review_post_comment.py` and add `MAF AI Semantic Review` as a required status check to enable AI-gated auto-merge of ai-fill PRs. Defer until 2–3 real MAF releases have validated semantic-review verdicts against manual judgment.

---

## [1.1.0] — 2026-05-18

Minor release focused on **hardening the AI-fill release-watcher pipeline end-to-end**. The watcher chain that auto-extracts breaking-change entries from new MAF NuGet releases is now production-ready, with a 3-layer trust ladder catching defects deterministically. **First MAF 1.6.1 registry data** added.

### Added

- **Rung-2 AI semantic review** (`.github/workflows/maf-ai-semantic-review.yml`) — calls GPT-4o via the **GitHub Models** inference endpoint on every AI-fill PR; per-entry PASS/WARN/FAIL JSON verdict posted as a PR review comment. $0 marginal cost (uses Copilot Pro+ subscription quota). Currently in COMMENT-ONLY mode; can be flipped to a hard gate after 2-3 release cycles of calibration.
- **Cross-file consistency check** (`.github/scripts/verify_crossfile_consistency.py`) — deterministic Python check catching defects the per-entry mechanical check misses: duplicate `**X.Y.Z**` rows in compat-matrix, stale `Current tracked version` line, stale `last-updated:` date, `guide_section` IDs that don't exist in the 1.3.0 migration guide.
- **MAF 1.6.1 registry data** — first entry (`MAF161-EXTENSIO-001` — `WorkflowEvaluationExtensions.EvaluateAsync` signature change adding `expectedOutput` parameter), accompanying migration guide, and compatibility-matrix row.
- **Hardened fill prompt template** (`.github/scripts/ai_fill_issue_prompt.md.tpl`) — restructured action-first / verify-second / detail-below. Includes a runnable bash post-implementation checklist that mirrors the CI gate exactly. 366 → 207 lines (43% smaller).

### Changed

- **Default fill bot is now Claude** (Anthropic Code Agent), previously Copilot Coding Agent. Empirically better at structured-instruction following for this task. Override via `gh workflow run maf-ai-fill-todos.yml -f bot=copilot|codex`.
- **Bot ID resolution is now dynamic** via the GraphQL `suggestedActors` query — no more hardcoded `BOT_kgDOC9w8XQ` constants that can rotate when GitHub re-provisions bot accounts.
- **Watcher pushes to main using a maintainer PAT** (`COPILOT_ASSIGN_PAT`) so it can bypass branch protection's required-status-check rule. Classic branch protection has no bypass-list (Rulesets-only feature); admin users do bypass when `enforce_admins=False`. Commit author identity unchanged — still `github-actions[bot]`.
- **`maf-ai-fill-verify.yml` always reports a status** on every PR (the previous `paths:` filter caused a branch-protection footgun where PRs not touching `registry.yaml` got blocked by missing-required-check). Out-of-scope PRs report success trivially.
- **`maf-pr-audit.yml` skips samples-only PRs** — `samples/find-the-bug/` and `samples/maf-1.2-sample/` are intentionally buggy demo corpora; auditing them produced a noisy F grade misrepresenting toolkit quality.

### Fixed

- **Bash heredoc → Python helpers** for the watcher commit message + AI-fill issue body. Eliminates a class of shell-escaping bugs (the 2026-05-17 backtick-as-command-substitution incident that aborted the watcher with exit 127).
- **`addAssigneesToAssignable` for Coding Agents** now uses a fine-grained PAT — `GITHUB_TOKEN` is categorically rejected by the Copilot Coding Agent assignment API by design (confirmed by upstream `gh-aw` issue #19765 + Copilot maintainer statement: "Copilot does not support bot accounts for issue assignment").
- **Cross-workflow dispatch** from watcher to `maf-ai-fill-todos.yml` needs `actions: write` permission (HTTP 403 "Resource not accessible by integration" without it).
- **Rung-2 trigger** dropped the `paths:` filter — `paths` matching is broken on `ready_for_review` events (state flips carry no commit deltas, so `paths` silently skips). Observed empirically on PR #26.
- 6 additional smaller fixes across the watcher / verify / semantic-review chain (see commit history).

### Dependency bumps (Dependabot)

- `Microsoft.SourceLink.GitHub` 8.0.0 → 10.0.300
- `ModelContextProtocol` 1.2.0 → 1.3.0
- `Microsoft.Extensions.Hosting` 10.0.7 → 10.0.8
- `actions/setup-dotnet` 4 → 5
- `actions/setup-python` 5 → 6
- `docker/metadata-action` 5 → 6
- `docker/build-push-action` 6 → 7
- `marocchino/sticky-pull-request-comment` (patch)

### Distribution

- 🟢 NuGet `maf-autopilot/1.1.0` (multi-targets net8.0/net9.0/net10.0)
- 🟢 NuGet `maf-autopilot.Analyzers/1.1.0` (netstandard2.0)
- 🟢 Docker GHCR `:1.1.0` `:1.1` `:1` `:latest`

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
