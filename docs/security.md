# Security — how MAF Doctor (`maf-autopilot`) is hardened

> **Audience:** end users evaluating whether MAF Doctor is safe to install in their environment, and maintainers verifying mitigations stay in place.
>
> **For the deep technical breakdown** of attack surfaces, mitigations, and acknowledged gaps, read [`docs/security/threat-model.md`](security/threat-model.md). This file is the high-level overview.

---

## How we tackle security

### At the MCP-protocol level

| Mechanism | What it does | Where to verify |
|---|---|---|
| **MCP tool annotations** (`ReadOnly` / `Destructive` / `Idempotent` / `OpenWorld`) | Every one of the 25 `[McpServerTool]` decorations declares its behavior class. Well-behaved clients (Claude Code, VS Code Copilot) use this to decide which tools auto-invoke vs need user confirmation — preventing prompt-driven exfiltration via tools the user didn't expect to fire. | `src/maf-autopilot/Tools/*.cs` — see annotation classification in [`CHANGELOG.md`](../CHANGELOG.md) under 1.0.0 breaking changes |
| **Structured outputs** (JSON / SARIF) | `MafDoctor`, `MafScanAntiPatterns`, `MafValidateFanOut` etc. emit machine-readable schemas — no free-form prose that could embed prompt-injection payloads back into the LLM context | [`docs/output-schemas.md`](output-schemas.md) |
| **No generic shell tool** | We do NOT expose `execute_command(string)`. Everything is a narrow, purpose-built tool (`MafRunCs0618Hunt` shells `dotnet build` and parses output; never executes arbitrary commands) | grep for `Process.Start` — only in `src/maf-autopilot/Tools/ProcessRunner.cs`, which only launches `dotnet`/`dotnet-inspect` |
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

## See also

- [`docs/security/threat-model.md`](security/threat-model.md) — full attack-surface analysis, mitigations, and acknowledged gaps
- [`docs/output-schemas.md`](output-schemas.md) — the JSON schema for tool outputs (CI integration)
- [`CONTRIBUTING.md`](../CONTRIBUTING.md) §"Adding an MCP server tool" — security rubric for new tools
- [`AGENTS.md`](../AGENTS.md) — repo layout + AI-fill loop description
