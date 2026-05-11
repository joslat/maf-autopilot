# Architecture

> **Last updated:** 2026-05-12

This document explains what each piece of `maf-autopilot` is, how the pieces fit together, and where the structural choices were deliberate (vs. accidental).

For *what's pending*, see [`next-steps.md`](./next-steps.md). For history of decisions, see [`maf-migration-toolkit-plan.md`](./maf-migration-toolkit-plan.md).

---

## What is `maf-autopilot`?

A toolkit that turns **GitHub Copilot Chat into a Microsoft Agent Framework (MAF) expert** for the lifetime of a .NET project.

It ships through **four channels**:

1. **MCP server (NuGet tool)** — the main product. 17 [Model Context Protocol](https://modelcontextprotocol.io/) tools that Copilot can invoke during chats: anti-pattern scanner, fan-out validator, workflow simulator, scaffolders, PR auditor, etc.
2. **MCP server (Docker)** — same product, containerised on GHCR (multi-arch amd64+arm64), for users without a .NET SDK on the host.
3. **Roslyn analyzer (separate NuGet)** — `maf-autopilot.Analyzers`. Ships 3 write-time rules (MAF001–003) into any consumer project's compiler. Independent of the MCP server.
4. **Skills + agents (GitHub-native)** — 12 SKILL.md files + 6 agent personas + 3 always-loaded instruction files. Loaded by GitHub Copilot Chat directly from `.github/`.

The MCP server, the analyzer NuGet, and the skill bundle are independently shippable. A user can adopt any one, two, or all three.

---

## Component map

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          maf-autopilot repo                              │
│                                                                          │
│  ┌─────────────────────────┐   ┌────────────────────────────────────┐   │
│  │  /mcp/maf-autopilot/    │   │  /mcp/maf-autopilot.Analyzers/    │   │
│  │  ── MCP server          │   │  ── Roslyn analyzers              │   │
│  │  ── .NET 9 console      │   │  ── netstandard2.0 (compiler host)│   │
│  │  ── NuGet: maf-autopilot│   │  ── NuGet: maf-autopilot.Analyzers│   │
│  │  ── Docker: GHCR        │   │  ── 3 rules (MAF001/002/003)      │   │
│  └────────┬────────────────┘   └────────┬──────────────────────────┘   │
│           │ ProjectReference (none)     │ ProjectReference (none)        │
│           │ EmbeddedResource:           │ AdditionalFiles:               │
│           │  ../../.github/skills/*.md  │  AnalyzerReleases.Shipped.md   │
│           │  ../../.github/instructions/                                  │
│           │  ../../guides/maf-1.3.0…    │                                │
│           │                                                              │
│  ┌────────▼────────────────┐   ┌────────▼──────────────────────────┐   │
│  │  /mcp/maf-autopilot.Tests│  │ /mcp/maf-autopilot.Analyzers.Tests│   │
│  │  ── 415 xUnit tests      │  │ ── 11 xUnit tests                 │   │
│  │  ── ProjectReference     │  │ ── ProjectReference               │   │
│  │     maf-autopilot.csproj │  │    maf-autopilot.Analyzers.csproj │   │
│  └─────────────────────────┘   └────────────────────────────────────┘   │
│                                                                          │
│  /.github/                                                               │
│  ├── agents/        6 agent personas (Copilot Chat loads via @-mention) │
│  ├── instructions/  3 auto-loaded instruction files (applyTo: globs)    │
│  ├── skills/        12 skill docs (SKILL.md per dir)                    │
│  ├── scripts/       2 Python CI helpers (called by workflows)           │
│  ├── workflows/     5 GitHub Actions                                    │
│  └── dependabot.yml security: pin SHAs, update weekly                   │
│                                                                          │
│  /docs/             this folder — index.md catalogues it                │
│  /guides/           per-version migration guides (1.3.0, future: 1.4.0) │
│  /Dockerfile        multi-stage build for the GHCR image                │
│  /maf-autopilot.sln solution file referencing all 4 projects            │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## What each .NET project IS

### `maf-autopilot` (the MCP server)

**Path:** `/mcp/maf-autopilot/`
**Target framework:** .NET 9
**Output:** a single executable (`maf-autopilot.dll`) packaged as a dotnet global tool + a Docker image.
**Dependencies:** `ModelContextProtocol` 1.2.0, `Microsoft.CodeAnalysis.CSharp` 4.11.0, `YamlDotNet` 16.2.1, `Microsoft.Extensions.Hosting` 9.0.

**What it does:**
- Speaks MCP over stdio (when run as a tool) or stdio-in-container (when run via Docker).
- Exposes **17 tools** (`[McpServerTool]`-decorated methods under `Tools/*.cs`).
- Exposes **4 resources** (`maf://constraints`, `maf://guide`, `maf://registry`, `maf://rules`, `maf://skills?name=...`).
- Exposes **3 prompts** (`maf-audit`, `maf-migrate`, `maf-cs0618-hunt`).
- Provides **CLI subcommands**: `init` (wires `.vscode/mcp.json`), `doctor` (health grade), `new agent` / `new executor` (scaffolders), `registry-extract` (CI helper).

**Embeds at build time** (via `<EmbeddedResource>`): the 12 SKILL.md files, the 3 instruction files, the obsolete-API registry YAML, the migration guide. This is why the runtime container only needs a single .dll — everything ships inline.

### `maf-autopilot.Analyzers` (the Roslyn analyzer NuGet)

**Path:** `/mcp/maf-autopilot.Analyzers/`
**Target framework:** **netstandard2.0** (mandatory — Roslyn analyzers load into the compiler host, which targets netstandard2.0).
**Output:** an analyzer NuGet that drops a DLL into consumer projects' `analyzers/dotnet/cs/`.
**Dependencies:** `Microsoft.CodeAnalysis.CSharp` 4.11.0, `Microsoft.CodeAnalysis.Analyzers` 3.11.0 (both `PrivateAssets="all"`).

**What it does:** ships 3 diagnostic rules that fire at write-time in any consumer project:
- `MAF001` — fan-out handler must return `Task<T>` / `ValueTask<T>` / `IAsyncEnumerable<T>` (else silent fan-in starvation).
- `MAF002` — avoid `DefaultAzureCredential` in production code.
- `MAF003` — avoid `EnableSensitiveData = true` outside test code.

**Critically: it has ZERO dependencies on the MCP server.** A consumer can install just the analyzers without ever touching `maf-autopilot` the tool.

### `maf-autopilot.Tests`

**Path:** `/mcp/maf-autopilot.Tests/`
**Target framework:** .NET 9
**Output:** an xUnit test assembly (not packed; never published).
**Dependencies:** `ProjectReference` to `maf-autopilot`; `xunit` 2.9.2; `Microsoft.NET.Test.Sdk` 17.12.0; `Microsoft.CodeAnalysis.CSharp` 4.11.0 (for the compile-validation tests).

**Current size:** 415 tests as of Phase N. Coverage spans every MCP tool (entry + pure-core), every scaffolder template, security regression pins, and registry haystack search.

### `maf-autopilot.Analyzers.Tests`

**Path:** `/mcp/maf-autopilot.Analyzers.Tests/`
**Target framework:** .NET 9 (test host can target net9.0 even though the analyzer itself is netstandard2.0).
**Output:** an xUnit test assembly.
**Dependencies:** `ProjectReference` to `maf-autopilot.Analyzers`; `xunit` 2.9.2; `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit` 1.1.2.

**Current size:** 11 tests — one happy-path + one negative case per rule, plus rule-metadata validation.

---

## Naming conventions

| Surface | Convention | Example |
|---|---|---|
| MCP tools | `Maf<Verb><Object>` (PascalCase) | `MafScanAntiPatterns`, `MafDoctor` |
| MCP resources | `maf://<concept>` | `maf://constraints` |
| MCP prompts | `maf-<topic>` (hyphenated) | `maf-audit` |
| Analyzer rules | `MAF<NNN>` (three digits) | `MAF001` |
| Anti-pattern scanner rules | `MAF-AP-<AREA>-<NNN>` | `MAF-AP-SEC-001` |
| Registry entries | `MAF<MAJ><MIN><PATCH>-<AREA>-<NNN>` | `MAF130-FAN-IN-001` |
| Agents | `maf-<role>.agent.md` (no `-agent` stem) | `maf-incident-responder.agent.md` |
| Instructions | `maf-<topic>.instructions.md` | `maf-constraints.instructions.md` |
| Skills | mixed — `maf-` prefix when the unprefixed name is general-CS; no prefix when context-unambiguous | `maf-anti-pattern-scanner` vs `obsolete-api-registry` |
| CLI subcommands | natural English | `init`, `doctor`, `new agent`, `registry-extract` |

See [`/CONTRIBUTING.md`](../CONTRIBUTING.md) §"Skill naming convention" + "Adding an agent" for the rationale.

---

## Build & dependency graph

```
maf-autopilot.sln
    │
    ├── /mcp/maf-autopilot                    (.NET 9 console / NuGet tool)
    │       └── embeds: .github/skills/*.md, .github/instructions/*.md,
    │                   guides/maf-1.3.0-migration-guide.md,
    │                   .github/skills/obsolete-api-registry/registry.yaml
    │
    ├── /mcp/maf-autopilot.Analyzers          (netstandard2.0 / NuGet)
    │       └── (no project references — fully standalone)
    │
    ├── /mcp/maf-autopilot.Tests              (.NET 9 / xUnit)
    │       └── ProjectReference: maf-autopilot
    │
    └── /mcp/maf-autopilot.Analyzers.Tests    (.NET 9 / xUnit)
            └── ProjectReference: maf-autopilot.Analyzers
```

`dotnet build maf-autopilot.sln` builds all four. `dotnet test maf-autopilot.sln` runs all 426 tests in both test projects. `release.yml` packs `maf-autopilot` and `maf-autopilot.Analyzers` separately; `docker-publish.yml` packages only the MCP server.

---

## Distribution model

| Channel | What ships | Who consumes | Where |
|---|---|---|---|
| NuGet (server) | `maf-autopilot` .NET global tool | Developers installing via `dotnet tool install` | `nuget.org/packages/maf-autopilot` |
| NuGet (analyzer) | `maf-autopilot.Analyzers` analyzer | Consumer .csproj `<PackageReference>` | `nuget.org/packages/maf-autopilot.Analyzers` |
| Docker | Multi-arch image (amd64+arm64) | Developers without .NET SDK on host | `ghcr.io/joslat/maf-autopilot:<semver>` |
| Skills-only | The `.github/` directory copied into a consumer repo | Users wanting agents+skills without the MCP server | `git clone` |

---

## Open structural questions

### Should `/mcp/` rename to `/src/`?

**Status: open. Recommendation: yes, before cutting 1.0 stable.**

The current `/mcp/` directory groups the 4 .NET projects. The name is non-standard — the dominant .NET convention (used by `dotnet/runtime`, `dotnet/aspnetcore`, `dotnet/efcore`) is `/src/` for source projects, optionally with `/tests/` for test projects.

**Pros of renaming:**
- Conventional .NET layout. New contributors find the source immediately.
- The `/mcp/` name implies "this is the MCP code" — but the analyzer NuGet is NOT an MCP server. The current name misrepresents the analyzer's identity.
- Pre-1.0 is the cheapest time to rename: the URL shape isn't yet ossified in third-party docs / blog posts.

**Cons / costs:**
- ~30–50 file edits across: `maf-autopilot.sln`, `Dockerfile`, all 5 workflow YAMLs, `.dockerignore`, `.vscode/mcp.json`, README + every doc that mentions a `/mcp/` path.
- Project relative paths in `<EmbeddedResource>` use `..\..\.github\...` — these survive a one-level rename intact (depth unchanged), so no edits needed there.
- One disruptive commit in git history.

**Recommendation:** rename `/mcp/` → `/src/` as a focused PR before A.8 (cut stable 1.3.0). Keep the 4 projects flat under `/src/` — splitting into `/src/` + `/tests/` adds an extra directory level for negligible benefit at this scale.

**Migration checklist** (when executed):
1. `git mv mcp src`
2. Update `maf-autopilot.sln` project paths
3. Update `Dockerfile` COPY paths
4. Update `.github/workflows/release.yml`, `docker-publish.yml`, `maf-pr-audit.yml`, `maf-drift-detector.yml`, `maf-release-watcher.yml`
5. Update `.dockerignore`
6. Update `.vscode/mcp.json`
7. Update path references in README.md, CONTRIBUTING.md, TROUBLESHOOTING.md, CHANGELOG.md, all 4 `/docs/` files
8. Update path references in agent files under `.github/agents/`
9. Run `dotnet build maf-autopilot.sln && dotnet test maf-autopilot.sln`
10. Verify `docker build .` still succeeds
11. Verify embedded resources still resolve (check `MafResourcesTests.GetSkill_AllAllowlistedNames_Resolve`)

This is tracked as a candidate next-step in [`next-steps.md`](./next-steps.md).

### Should `.github/scripts/` move to `/scripts/`?

**Status: open. Recommendation: keep where it is.**

The 2 Python scripts (`gen_guide_section.py`, `update_compat_matrix.py`) are CI-only — called exclusively by `.github/workflows/maf-release-watcher.yml`. Putting them next to the workflow that calls them keeps the relationship discoverable.

Moving to a top-level `/scripts/` would:
- Suggest they're general-purpose dev scripts (they aren't).
- Decouple them from the workflow that owns them.
- Add a top-level directory for ~70 lines of Python.

**Not a skill, either.** Skills are markdown knowledge files loaded into Copilot's context. These are executable Python. Wrapping them as skills would be a category error.
