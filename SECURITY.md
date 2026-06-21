# Security Policy

`maf-autopilot` ("MAF Doctor") is an open-source MCP server + Roslyn analyzer
toolkit. This page is the entry point users find via GitHub's Security tab; the
substantive content lives in [`docs/security.md`](docs/security.md) (user-facing
overview) and [`docs/security/threat-model.md`](docs/security/threat-model.md)
(technical attack-surface map).

## Reporting a vulnerability

**Do not open a public GitHub issue.** Email the maintainer (see the GitHub
profile contact on [@joslat](https://github.com/joslat)) with:

- A clear description of the issue.
- Steps to reproduce.
- Suggested mitigation if you have one.
- Whether you'd like credit in the release notes for the fix.

We aim to acknowledge within **72 hours** and ship a fix in the next patch
release (or sooner if the issue is critical). Critical issues — anything that
could let an attacker execute code on a user's machine, exfiltrate registry
data, or poison the AI-fill loop — get an immediate point-release.

## Scope

In scope:

- The MCP server (`src/maf-autopilot/`) — all 25 tools + the prompt/resource
  surface.
- The Roslyn analyzer NuGet (`src/maf-autopilot.Analyzers/`).
- The GitHub Actions workflows under `.github/workflows/`.
- The AI-fill loop (`maf-release-watcher.yml` → Copilot Coding Agent → PR).
- The registry data (`registry.yaml` and its persistence path through
  `MafRegistryLookup`).
- Supply chain — NuGet packages produced by `release.yml`, GHCR image
  produced by `docker-publish.yml`.

Out of scope (in this repo's threat model):

- Vulnerabilities in user code we scan — that's the user's repo.
- Issues in dependencies (file those upstream; we'll bump Dependabot).
- A malicious maintainer of THIS repo — the maintainer can already publish
  anything to NuGet via `release.yml`.

## What we've hardened against

See [`docs/security.md`](docs/security.md) for the user-facing summary of
named attack-class coverage:

- Command injection via tool arguments (Keysight 2026)
- LLM-to-LLM prompt smuggling via tool outputs
- Supply-chain prompt injection via upstream release notes
- Workflow input injection (`workflow_dispatch` templating)
- Path-escape via secondary path parameters
- Code injection via scaffold parameters
- MCP annotation drift / tool poisoning

The MCP-server security scan is in
[`docs/security.md` § "Latest scan results"](docs/security.md#latest-scan-results)
(zero findings on the most recent Cisco mcp-scanner pass).

## Recognition

Researchers who report a valid vulnerability are credited in:

- The release notes of the patch that ships the fix.
- A `THANKS.md` entry (if added by the time of the next release).

If you'd prefer to stay anonymous, say so in your report — we'll honor it.
