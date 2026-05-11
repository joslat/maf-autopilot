# /docs — Index

> **Audience:** new contributors orienting on the project, returning maintainers checking "where did we put X?".
>
> **Last refreshed:** 2026-05-12 — generated at the end of the Phase N security sprint.

Every document below is canonical for one slice of the project. **One concept, one home.** If you find yourself writing the same explanation in two docs, fix it by linking instead of duplicating.

---

## Quick navigation

| If you want to… | Read |
|---|---|
| Understand what `maf-autopilot` *is* and how the .NET projects fit together | [`architecture.md`](./architecture.md) |
| See what work is **next** (active, deferred, gated) | [`next-steps.md`](./next-steps.md) |
| See the *full historical* implementation plan + tracking table | [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md) |
| See the strategy + vision snapshot from 2026-05-11 | [`project-status-and-vision.md`](./project-status-and-vision.md) |
| Look up MAF version ↔ dependency versions | [`compatibility-matrix.md`](./compatibility-matrix.md) |
| Install + configure the toolkit | [`setup.md`](./setup.md) |
| Install / run troubleshooting | [`/TROUBLESHOOTING.md`](../TROUBLESHOOTING.md) (repo root) |
| Contribution shapes + conventions | [`/CONTRIBUTING.md`](../CONTRIBUTING.md) (repo root) |
| Version history | [`/CHANGELOG.md`](../CHANGELOG.md) (repo root) |

---

## Document catalogue

### 🚀 [`next-steps.md`](./next-steps.md)

**Purpose.** The single page that tells you what's next. Active work, deferred items (with rationale), pre-1.0 ship gates, post-1.0 roadmap.

**Status.** Current as of 2026-05-12 evening. Re-generate when phases land.

**Audience.** Anyone deciding "what should I work on?" or "is feature X done?".

---

### 🏛️ [`architecture.md`](./architecture.md)

**Purpose.** Component map. What is `maf-autopilot` vs `maf-autopilot.Analyzers` vs the two test projects? How do they relate? What does each one ship? Includes the structural recommendation (`/mcp/` → `/src/` rename — under "Open structural questions").

**Status.** Current as of 2026-05-12.

**Audience.** New contributors. Maintainers reasoning about packaging / distribution.

---

### 📋 [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md)

**Purpose.** The *historical* full plan + implementation tracking table. Every phase (Phase 0 through N), every numbered item (#1 through current), every decision made along the way. **This is a living archive, not a forward-looking doc** — for that, see `next-steps.md`.

**Status.** Up to date through Phase N. Future phases land here when shipped.

**Audience.** Anyone asking "why did we choose X?" or "when did Y land?". The tracking table is the audit trail.

**Length.** ~130 KB. Skim by phase header.

---

### 🧭 [`project-status-and-vision.md`](./project-status-and-vision.md)

**Purpose.** Strategy + vision snapshot from the 2026-05-11 4-Opus audit that started the current sprint. Headline findings, TL;DR, stage assessment, the broader "permanent co-pilot" vision.

**Status.** Snapshot — deliberately frozen at 2026-05-11. The "current state" sections (§10, Headline) have been amended to point forward to `next-steps.md`.

**Audience.** Anyone asking "where is this project going?". Read alongside `next-steps.md` for execution detail.

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
