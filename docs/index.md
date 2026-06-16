# /docs — Index

> **Audience:** new contributors orienting on the project, returning maintainers checking "where did we put X?".
>
> **Last refreshed:** 2026-05-13 — Phase S landed: nupkg now multi-targets `net8.0;net9.0;net10.0`; central package management via `Directory.Packages.props` + `Directory.Build.props` + `global.json`. See [`architecture.md` → Multi-targeting](./architecture.md#multi-targeting).

Every document below is canonical for one slice of the project. **One concept, one home.** If you find yourself writing the same explanation in two docs, fix it by linking instead of duplicating.

---

## Quick navigation

| If you want to… | Read |
|---|---|
| Understand what `maf-autopilot` *is* and how the .NET projects fit together | [`architecture.md`](./architecture.md) |
| Look up MAF version ↔ dependency versions | [`compatibility-matrix.md`](./compatibility-matrix.md) |
| Install + configure the toolkit | [`setup.md`](./setup.md) |
| Install / run troubleshooting | [`/TROUBLESHOOTING.md`](../TROUBLESHOOTING.md) (repo root) |
| Contribution shapes + conventions | [`/CONTRIBUTING.md`](../CONTRIBUTING.md) (repo root) |
| Version history | [`/CHANGELOG.md`](../CHANGELOG.md) (repo root) |

---

## Document catalogue

### 🏛️ [`architecture.md`](./architecture.md)

**Purpose.** Component map. What is `maf-autopilot` vs `maf-autopilot.Analyzers` vs the two test projects? How do they relate? What does each one ship? Plus the multi-targeting strategy (`net8.0;net9.0;net10.0` nupkg + `Directory.Packages.props` CPM), and the rationale for the `/src/` layout and `.github/scripts/` placement (under "Structural decisions — closed").

**Status.** Current as of 2026-05-13 (Phase S landed).

**Audience.** New contributors. Maintainers reasoning about packaging / distribution.

---

### 🧮 [`compatibility-matrix.md`](./compatibility-matrix.md)

**Purpose.** MAF version ↔ Microsoft.Extensions.AI / .NET / Azure.AI.OpenAI / Generators package version compatibility. Single source of truth referenced by the `MafCompatibility` tool and the `maf-release-watcher` workflow.

**Status.** Auto-updated by `.github/workflows/maf-release-watcher.yml` on every new MAF release. Last manual update: 2026-05-04.

**Audience.** Anyone installing / upgrading MAF; the `maf-release-watcher` workflow.

---

### 🛠️ [`setup.md`](./setup.md)

**Purpose.** Detailed install + configure instructions. The README has the 5-minute path; this doc covers all integration modes (NuGet tool / Docker / Skills-only), prerequisites, troubleshooting per mode.

**Status.** Current. Updated as new distribution channels land.

**Audience.** Anyone installing the toolkit for the first time.

---

## Documents that live elsewhere

These are referenced by `/docs/` but live at the repo root for tooling-convention reasons:

| Doc | Location | Why not in `/docs/`? |
|---|---|---|
| README | `/README.md` | GitHub renders it on the repo home page |
| CHANGELOG | `/CHANGELOG.md` | Keep-a-Changelog convention is repo-root |
| CONTRIBUTING | `/CONTRIBUTING.md` | GitHub auto-links it from the PR template |
| TROUBLESHOOTING | `/TROUBLESHOOTING.md` | Same — high-discoverability |
| LICENSE | `/LICENSE` | OSI convention |
| Migration guide | `/guides/maf-1.3.0-migration-guide.md` | One file per MAF version; separate dir for clarity |

---

## What does NOT belong in `/docs/`

- **Skill content** lives in `.github/skills/<name>/SKILL.md`. Don't move skill files into `/docs/`.
- **Agent personas** live in `.github/agents/<name>.agent.md`. Same.
- **Instructions auto-loaded by Copilot Chat** live in `.github/instructions/<name>.instructions.md`. Same.
- **CI workflow scripts** (Python helpers) live in `.github/scripts/`. Same.

If you're tempted to write a "how the X skill works" doc in `/docs/`, instead make the SKILL.md self-contained.
