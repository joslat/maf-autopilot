# maf-autopilot threat model (v1.0.0)

> **Audience:** users deciding whether to install maf-autopilot in a security-sensitive context. Maintainers verifying mitigations are in place.
>
> **Scope:** what maf-autopilot reads, what it writes, what attack surfaces exist, what we mitigate, what we don't.

## 1. Data read

| Source | Trust level | Read by |
|---|---|---|
| User repo source files (`.cs`, `.csproj`) | **Untrusted** — may contain prompt injection in comments/strings | Most scanners (`MafScanAntiPatterns`, `MafValidateFanOut`, `MafEstimateCost`, etc.) |
| `MAF_REGISTRY_PATH` env-overridden registry YAML | Semi-trusted | `RegistryService.Load()` — capped at 5 MB |
| Embedded registry YAML (built into the nupkg) | Trusted | `RegistryService.Load()` (default path) |
| `dotnet build` output | Trusted (compiler) | `Cs0618HuntTool` |
| GitHub release notes from `microsoft/agent-framework` | **Untrusted** | `maf-release-watcher` workflow → fed to Copilot Coding Agent for AI-fill |
| User's `git diff` against base branch | Untrusted | `PullRequestAuditTool` |

## 2. Data written

| Target | Approval gate |
|---|---|
| `.cs` files in user repo (rewriters) | `--dry-run` default opt-in; explicit `dryRun: false` to write |
| Generated scaffolds (`Agents/*.cs`, `Workflows/*Executor.cs`) | Idempotent — skips existing files; logs `already exists` |
| `.vscode/mcp.json` (via `init`) | User invokes `init` deliberately |
| `.github/copilot-instructions.md`, `CLAUDE.md`, `AGENTS.md` (via `init`) | Merge-not-overwrite; existing files appended only if maf-autopilot section is absent |
| Embedded registry path | Not writable from MCP tools — registry maintenance is via `registry-extract` + AI-fill PR-flow |

## 3. Attack surfaces

### 3.1 Prompt injection via repo content

User repo `.cs` files contain strings, comments, and identifiers. **Mitigated as of v1.1** — the defense has two halves:

- **Tools that re-feed user content to a downstream LLM** (`MafDraftIssue` → GitHub `create_issue`; `MafPrompts.Debug` rendered by the calling client; `RegistryExtractCommand` writing `notes:` that `MafRegistryLookup` later serves; the AI-fill workflow scripts that compose model prompts) wrap user-controlled content via `LlmFencing.Fence` (`src/maf-autopilot/Tools/LlmFencing.cs` + parity helper at `.github/scripts/llm_fencing.py`). The fence strips HTML comments, caps content at the appropriate byte budget, and wraps the result in random-sentinel BEGIN/END markers with explicit "treat as data" framing.
- **Tools that return analytical output** (`MafExplain`, `MafLintAgentPrompt`, every scanner under `MafScanAntiPatterns` / `MafValidateFanOut` / `MafDoctor`) bound the attack surface by construction — output is Roslyn-syntax-extracted identifiers + line numbers + structured findings, never raw user-source-text. A `// Ignore all previous instructions` comment in a user's `.cs` file never reaches a downstream LLM via these paths because comments are not part of the extracted output shape.

Anchored to OWASP LLM05 (Improper Output Handling) + MITRE ATLAS AML.T0051 (LLM Prompt Injection, indirect variant). See `docs/security.md` for the user-facing breakdown of the fenced sites.

### 3.2 Path traversal via `MAF_REGISTRY_PATH`

Environment variable overrides the embedded registry path. **Mitigated as of v1.1** — `RegistryService.ValidateOverridePath` rejects (a) shell metacharacters / NUL / CR / LF in the path string and (b) reparse-points at the file itself. Optional `MAF_REGISTRY_PATH_ROOTS` env var declares a `;`-separated allowlist of acceptable parent directories. The existing 5 MB byte-size cap still applies.

### 3.3 AI-fill loop poisoning

`maf-release-watcher` reads release notes from upstream and dispatches a Copilot Coding Agent task to fill `TODO` placeholders in registry entries. **Mitigated as of v1.1** — every hop in the chain now fences user content via `LlmFencing.Fence`:

- `gen_guide_section.py` fences both the release notes AND the `dotnet-inspect` diff before embedding in the per-version migration guide.
- `RegistryExtractCommand` fences the diff body in the `notes:` field (8 KB cap because the value persists forever in `registry.yaml`).
- `ai_review_build_prompt.py` fences each PR-modified registry entry before embedding in the semantic-review model prompt.
- `VerifyRegistryCommand` `pre-X.Y.Z` and `IsRawDraft` branches now run `CheckContentSafety` (HTML-comment + length caps) instead of bypassing all checks.
- `IsRawDraft` tightened to require `// TODO` at line start via `(?m)^\s*//\s*TODO\b` regex (string literals containing `// TODO` no longer re-qualify as raw drafts).
- `BraceBalanceFailure` uses a bracket stack instead of delta-counting — catches `}{` and `({)}` patterns that v1's count-only check missed.
- `FormatEntry.NeutralizeFences` inserts zero-width spaces between triple backticks in registry examples so they cannot break out of the surrounding `csharp` markdown fence.

### 3.4 Rewriter correctness bugs

A Roslyn rewriter bug could corrupt user `.cs` files. **Mitigations** (strengthened in v1.1):

- `--dry-run` is default-aware on every writer.
- Multi-version regression CI runs every rewriter against `samples/maf-1.0-sample/`, `1.2-sample/`, `1.3-sample/` and verifies the output compiles.
- Corruption guards on the two highest-risk rewriters: `ExecutorSealedRewriter` skips `abstract` / `static` classes (preventing `sealed abstract` compile errors); `SyncOverAsyncRewriter` skips `await`-inside-lock and `await`-in-non-async (preventing CS1996 / CS0117).
- Idempotence property tests (`RewriterIdempotenceTests`) lock the contract that running a rewriter twice produces the same output as once — applies on both happy and skip paths.

### 3.5 `dotnet build` shell-out

`MafRunCs0618Hunt` shells `dotnet build`, which executes the user's project file. If the project has malicious MSBuild targets, those run. **Acceptable, but explicitly annotated** (v1.1) — `[McpServerTool(ReadOnly = false, OpenWorld = true)]` so MCP-spec-compliant clients prompt the user for consent before invoking. The tool description warns about untrusted repos. Users running maf-autopilot on untrusted projects should sandbox the process.

### 3.6 Type-expression input in `MafNewExecutor`

`inputType` and `outputType` parameters are Roslyn-parsed and roundtrip-validated. v1.1 added `projectNamespace` to the same defense (`AgentScaffolder.IsValidNamespace`) — pre-fix the namespace flowed verbatim into the generated `namespace {{ns}};` declaration and a payload like `MyApp; class Evil { static int x = Process.Start("calc"); }` produced a compilable file with attacker-controlled types. Security-pinned by `ScaffolderSecurityTests`.

### 3.7 Command injection via tool arguments (Keysight, 2026)

**Reference:** [Keysight blog, 12 Jan 2026](https://www.keysight.com/blogs/en/tech/nwvs/2026/01/12/mcp-command-injection-new-attack-vector). The attack class: an MCP tool concatenates LLM-supplied arguments into a shell command string; the model is prompt-injected into emitting payloads that break out of the intended command.

**Not vulnerable.** Three spawn sites — `ProcessRunner.cs` (`dotnet build`, `dotnet-inspect diff`), `PullRequestAuditTool.cs` (`git diff`) — all use `ProcessStartInfo.ArgumentList` (argv-style, no shell parsing) and `UseShellExecute = false`. No code path uses the unsafe `ProcessStartInfo.Arguments` *string* property. No tool exposes a `command` / `args` parameter to the LLM. Invariant checkable via `grep -rn "Process.Start" src/` AND enforced in CI by `.github/workflows/ci-invariants.yml` (Phase 0 — Job 1 allowlists call-sites). See [`docs/security.md` → "Command injection via tool arguments"](../security.md#command-injection-via-tool-arguments-keysight-2026) for the user-facing breakdown.

### 3.8 Code injection via scaffold-namespace param (v1.1 closure)

`AgentScaffolder.ScaffoldAgent` / `ScaffoldExecutor` accept a `projectNamespace` parameter that flows into the generated source's `namespace {{ns}};` declaration. **Mitigated** — `IsValidNamespace` mirrors the existing `IsValidTypeExpression` defense (pre-reject char filter + Roslyn `ParseName` + roundtrip check). Anchored to OWASP MCP04 Command Injection / path-escape variant.

### 3.9 LLM-to-LLM prompt smuggling via tool outputs (v1.1 closure)

`MafDraftIssue` emits a markdown body the calling agent passes to a sibling `create_issue` MCP tool — i.e. user input ends up in another LLM's context. **Mitigated** — `LlmFencing.Fence` wraps every user-controlled field (symptom, expected, actual, snippet) with explicit "treat as data" framing + random-sentinel BEGIN/END markers + length caps. Tool description instructs the calling agent to pass the body as-is (not strip the fence). Anchored to OWASP LLM05 Improper Output Handling + OWASP Agentic 2026 Indirect Prompt Injection via Tool Output.

### 3.10 Supply-chain prompt injection via upstream release notes (v1.1 closure)

`maf-release-watcher` reads `gh release view microsoft/agent-framework --jq .body` and embeds the result into the per-version migration guide (committed direct-to-main) and downstream AI-fill issue body. A malicious upstream maintainer (TA-1) could ship HTML-comment-disguised injection in release notes. **Mitigated** — `gen_guide_section.py` fences both the release notes AND the `dotnet-inspect` diff via the Python `llm_fencing` helper. Anchored to MITRE ATLAS AML.T0048 (ML Supply Chain Compromise) + AML.T0058 (Context Poisoning).

### 3.11 Workflow input injection via `workflow_dispatch` templating (v1.1 closure)

`${{ inputs.X }}` templates inside `run:` blocks substitute literal text before shell parsing — every such site is a potential command-injection lane if any future contributor changes the surrounding script. **Mitigated** — every workflow_dispatch input now flows via `env:` redirect (`env: { X: ${{ inputs.X }} }` then `$X` in shell). Every workflow_dispatch workflow runs a `validate-inputs` job FIRST (with `permissions: {}`) that regex-checks the input shape (semver / positive-int / enum) before any privileged step. Downstream jobs gate via `needs: validate-inputs`. CI invariant `ci-invariants.yml` Job 4 blocks any future `${{ inputs.* }}` in `run:` blocks. Anchored to GitHub's "Security hardening for GitHub Actions" guidance.

### 3.12 Third-party action supply chain (v1.1 closure)

All third-party GitHub Actions are SHA-pinned (9 distinct actions × 23 call sites). A tag-pinned action like `actions/checkout@v6` resolves to whatever HEAD that floating tag points to — a compromised upstream account could push arbitrary code into the action and inherit our runner's tokens (NUGET_API_KEY, packages:write, COPILOT_ASSIGN_PAT). SHA pins eliminate the lane; Dependabot is configured to bump both SHA and tag-comment in lockstep.

## 4. What we mitigate

### Carried over from 1.0

- ✅ `PathGuard.ValidateRepoPath` validates repo-path inputs across tools.
- ✅ 5 MB cap on env-overridden registry.
- ✅ `--dry-run` default-aware on all writers.
- ✅ `verify-registry` PR-gate on AI-fill output (structural checks).
- ✅ Type-expression Roslyn-parsing on `MafNewExecutor` inputs.
- ✅ Multi-version regression CI on rewriters.

### Added in 1.1 (security hardening release)

- ✅ **Path-escape via secondary path parameters** — `PathGuard.ValidateContainment` jails resolved paths inside `repoPath`, rejecting absolute-path bypass, `..`-segment escape, and symlink redirect (including file-level symlinks).
- ✅ **Annotation correctness** — MCP-spec annotations (`ReadOnly`/`Destructive`/`Idempotent`/`OpenWorld`) audited and corrected: `MafRunCs0618Hunt` (`ReadOnly=false, OpenWorld=true`), `MafNewAgent`/`MafNewExecutor` (`Destructive=true`), `MafDiffPackage`/`MafPreUpgradeDryRun` (`OpenWorld=true`). Reflection tests pin the contract.
- ✅ **Scaffold namespace validation** — `AgentScaffolder.IsValidNamespace` blocks code-injection via `projectNamespace`.
- ✅ **LLM data-fencing** — `LlmFencing.Fence` wraps user-controlled content destined for any downstream LLM, with HTML-comment strip + UTF-8 byte cap + random-sentinel BEGIN/END markers.
- ✅ **Workflow input validation** — every `workflow_dispatch` workflow has a `validate-inputs` job (regex/enum check before any privileged step) and routes inputs via `env:` redirect, not `${{ }}` templating in `run:` blocks.
- ✅ **Workflow least-privilege** — explicit `permissions:` block on every workflow; `validate-inputs` jobs run with `permissions: {}`.
- ✅ **Third-party action SHA-pinning** — 9 actions × 23 call sites; Dependabot keeps current.
- ✅ **Rewriter corruption guards** — `ExecutorSealedRewriter` skips abstract/static; `SyncOverAsyncRewriter` skips lock/non-async. Idempotence property tests across all 5 rewriters.
- ✅ **Analyzer false-negative reduction** — `SensitiveDataAnalyzer` handles `ImplicitElementAccessSyntax` + `ElementAccessExpressionSyntax` (parity with rewriter); generated-code flags consistent across MAF001/002/003.
- ✅ **Bounded-input length caps** — `BoundedInput.Validate` on every user-controlled string input to MCP tools.
- ✅ **Symlink-aware recursive walks** — `SourceFileWalker` sets `AttributesToSkip = ReparsePoint | Hidden | System`.
- ✅ **Path leakage closed** — `SourceFileWalker.MakeRelative` throws on out-of-root rather than leaking the absolute path.
- ✅ **`MAF_REGISTRY_PATH` containment** — `ValidateOverridePath` rejects metachars + reparse-points; optional `MAF_REGISTRY_PATH_ROOTS` allowlist.
- ✅ **Regex hygiene** — `NonBacktracking + MatchTimeout=100ms` on every scanner regex.
- ✅ **VerifyRegistry tightening** — `pre-X.Y.Z` + `IsRawDraft` bypass closed; nesting-aware brace balance; `CheckContentSafety` runs even on skipped entries; `FormatEntry` neutralizes triple-backticks.
- ✅ **Persistence-layer fencing** — `RegistryExtractCommand.cs` fences `notes:` content (8 KB cap, lives forever in `registry.yaml`).
- ✅ **PR-audit compiler-bomb cap** — `maf-pr-audit.yml` skips Roslyn audit when changed `.cs` files > 200 OR > 5 MB.
- ✅ **CI invariants** — `ci-invariants.yml` blocks future regressions: `Process.Start` allowlist, regex hygiene, `EnumerationOptions` on recursive walkers, no `${{ inputs.* }}` in `run:` blocks, `permissions:` block required.
- ✅ **CODEOWNERS** — explicit pins for security-critical paths (Tools/, Scaffolding/, Commands/, Analyzers/, .github/, docs/security*, etc.).

## 5. Acknowledged residual gaps after 1.1

- ❌ **No sandbox around `dotnet build`** — `MafRunCs0618Hunt` now correctly annotated `ReadOnly=false, OpenWorld=true` so MCP-spec-compliant clients prompt before invoking. True OS sandboxing remains out of scope for an OSS .NET CLI. Users running maf-autopilot on untrusted repos should sandbox externally (devcontainer, AppArmor, etc.).
- ❌ **Direct-to-main commits by `maf-release-watcher.yml`** — intentional design tradeoff documented at watcher line 249 (dated 2026-05-12). Acceptable for solo-maintainer state; revisit if external maintainers join.
- ❌ **Rewriter pure-syntax matching** — `DefaultAzureCredentialRewriter`/`FanInArgOrderRewriter` semantic-model upgrade deferred to v1.2.x (significant refactor; bounded by `--dry-run` default + idempotence tests).
- ❌ **`ProcessRunner` env scrubbing + streaming output cap** — deferred to v1.2.x. 5 MB post-allocation cap remains.
- ❌ **`PullRequestAuditTool` inline `Process.Start`** — bypasses `ProcessRunner`'s caps; refactor to use `ProcessRunner` deferred to v1.2.x.
- ❌ **Roslyn-analyzer NuGet signing posture** — not yet documented; track for v1.2.x.
- ❌ **Commit signing requirement** — maintainer's choice; not enforced today.

## 6. AGT alignment

[Microsoft Agent Governance Toolkit (AGT)](https://learn.microsoft.com/en-us/agent-framework/agent-governance) published a 26.67% policy-violation rate for red-team prompts against prompt-only-safety systems. We do not rely on prompt-only safety: detection is deterministic (Roslyn syntax trees + curated registry + compiler diagnostics), and writes are gated by `--dry-run` + structural validators (`verify-registry`). The LLM-facing surface (`MafExplain`, Copilot Coding Agent AI-fill) is acknowledged as a residual exposure in §5.

## 7. MCP security scans

**Policy:** we only depend on scanners that are **fully open-source, fully local (no phone-home), and require no third-party account**. This keeps the supply chain small and avoids vendor lock-in.

> **For the user-facing overview** of how we tackle security at the MCP + repo level, see [`docs/security.md`](../security.md). This file is the deeper technical record.

### Note: these are CLI tools, not VS Code extensions

The scanner below is a Python CLI tool installed via `pipx` or `uv`. It is NOT in the VS Code extension marketplace. Looking for it there will find nothing — install via Python tooling as documented below.

### Primary scanner — Cisco mcp-scanner (yara analyzer)

**[Cisco mcp-scanner](https://github.com/cisco-ai-defense/mcp-scanner)** — YARA-based pattern matching for known threat patterns (prompt injection, tool poisoning, credential harvesting, code execution).

- **Maintained by:** Cisco AI Defense
- **License:** Apache-2.0
- **Reach:** 926+ stars, v4.6.0 (April 2026)
- **PyPI:** `cisco-ai-mcp-scanner`
- **Local-only in `yara` mode:** the `api` and `llm` analyzers need API keys (Cisco AI Defense / OpenAI) — we don't use those; the `yara` analyzer runs entirely on-disk

```bash
# Install (requires Python 3.10+ and pipx)
python -m pip install --user pipx
python -m pipx ensurepath
pipx install cisco-ai-mcp-scanner

# Scan our server (yara analyzer only — fully local, no API keys)
mcp-scanner --analyzers yara --format summary --hide-safe \
  stdio --stdio-command=dotnet \
  --stdio-arg=run --stdio-arg=--project --stdio-arg=src/maf-autopilot \
  --stdio-arg=--framework --stdio-arg=net10.0 --stdio-arg=--no-build \
  --stderr-file=/tmp/maf-server.log
```

### Optional cross-check — CSA mcpserver-audit

**[ModelContextProtocol-Security/mcpserver-audit](https://github.com/ModelContextProtocol-Security/mcpserver-audit)** — part of the **Cloud Security Alliance** Model Context Protocol Security initiative. Helps audit MCP servers before adoption. Useful as an independent cross-check when comparing scanner results.

```bash
# See upstream README for install + usage
```

### Recurrence cadence

- **Pre-tag** (every major + minor release): run Cisco mcp-scanner; record results below.
- **CI** (every push): consider Cisco mcp-scanner as a GitHub Action if upstream ships one.

### Last scan results

**2026-05-17 — Cisco mcp-scanner v4.6.0 (yara analyzer)**

```
=== Scan Statistics ===
Total tools: 25
Safe tools: 25
Unsafe tools: 0
Severity breakdown: HIGH: 0, UNKNOWN: 0, MEDIUM: 0, LOW: 0, SAFE: 25
Analyzer stats: yara_analyzer: 25/25 scanned, 0 findings
```

**Triage notes:** zero findings. All 25 MCP tools enumerated cleanly via stdio. No prompt injection / tool poisoning / credential harvesting patterns matched.

### Tools we deliberately do NOT use

We evaluated these and chose not to depend on them. The reasoning is explicit so users know the supply chain stays small:

| Tool | Why excluded |
|---|---|
| **[Snyk agent-scan](https://github.com/invariantlabs-ai/mcp-scan)** (originally Invariant Labs mcp-scan, now Snyk) | Requires a `SNYK_TOKEN` from a Snyk account. README confirms it "validates the components, both with local checks **and by invoking the Agent Scan API**" — not local-only; phones home to Snyk's servers. Currently in "Open Preview" (free), but Snyk's commercial tier is paid ($52-$98/dev/month) — post-GA pricing uncertain. Account requirement = vendor lock-in regardless. |
| **[kapilduraphe/mcp-watch](https://github.com/kapilduraphe/mcp-watch)** | Single-maintainer hobby project. Quality may be fine, but a security tool from an unvetted source is itself a supply-chain risk. |
| **Commercial-only scanners** (Pangea, Enkrypt AI, AQtive Guard) | Fine if your org has a license, but we don't gate the open-source release on them. |
| **"MCP Scanner" / "mcp-scan" VS Code extensions** | None of the established scanners ship as VS Code extensions. If you see one in the marketplace, verify the publisher carefully before installing. |
