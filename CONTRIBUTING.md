# Contributing to maf-autopilot

Thanks for considering a contribution. This guide covers the four most common contribution shapes — a registry entry, a skill, an agent change, and an MCP server tool — plus the project-wide conventions that keep them coherent.

## Before you start

1. Read **[`docs/maf-migration-toolkit-plan.md`](docs/maf-migration-toolkit-plan.md)** — the 74-row tracking table is the single source of truth for what is done, in progress, and pending. Find your work there or add a new row.
2. Read **[`docs/project-status-and-vision.md`](docs/project-status-and-vision.md)** — stage assessment + broader vision. New work should advance the prioritised plan, not start a parallel track.
3. **Check upstream before designing workarounds.** The single biggest project lesson (2026-05-11): `dotnet-inspect`'s `[Obsolete]`-overload limitation — the gap this toolkit was originally built to work around — was closed in upstream v0.7.8, and we kept pinning v0.7.6 for months. **Always check the upstream tool's release notes before designing a skill around a limitation.**

## Setup

- Any one of .NET 8 / .NET 9 / .NET 10 SDK. `global.json` pins `8.0.100` with `rollForward: latestMajor`, so any installed SDK ≥ 8.x will build the solution.
- For the multi-target test runs, you also need **runtimes for net8.0, net9.0, AND net10.0** installed (otherwise `dotnet test` skips TFMs that lack a runtime).
- Versions for every NuGet are centrally pinned in `/Directory.Packages.props` — when adding a `<PackageReference>` to any csproj, do NOT include `Version=` inline; add a `<PackageVersion>` to `Directory.Packages.props` instead.

```bash
git clone https://github.com/joslat/maf-autopilot
cd maf-autopilot
dotnet restore maf-autopilot.sln
dotnet build maf-autopilot.sln           # builds net8.0 + net9.0 + net10.0 + netstandard2.0 (analyzer)
dotnet test maf-autopilot.sln            # 474 tests × 3 TFMs = 1422 test executions
```

All test executions must pass. CI gates any PR that breaks them.

## Adding a registry entry (`.github/skills/obsolete-api-registry/registry.yaml`)

Each entry maps a real CS0618 / CS0246 / silent-runtime-failure pattern to a deterministic fix. The schema is enforced at runtime by `Data/RegistryModels.cs`.

1. **ID convention:** `MAF<version>-<AREA>-<NNN>` (e.g., `MAF130-FAN-IN-001`, `MAF130-THREAD-001`). Three digits, zero-padded.
2. **Populate every field** — `id, package, version_introduced, type, method, obsolete_signature, replacement_signature, argument_order_change, fix_description, example_before, example_after, cs_warning, guide_section, dotnet_inspect_detectable, notes`. The registry's value lives in completeness; partial entries silently regress.
3. **`cs_warning`:** `CS0618`, `CS0246`, or `RUNTIME_SILENT` (for fan-in starvation–class bugs).
4. **`dotnet_inspect_detectable`:** `true` if `dotnet-inspect@0.7.8+` flags this statically; `false` if only the compiler catches it (transitive obsoletions, overload-resolution surprises, project-local `[Obsolete]`).
5. **Test it.** The inverted-haystack in `RegistryService` indexes every string field. Add a `[Theory]` row to `src/maf-autopilot.Tests/RegistryServiceTests.cs` proving your new term is searchable.

## Adding a skill (`.github/skills/<name>/SKILL.md`)

Skills are markdown files Copilot Chat loads into the agent's context. Conventions:

1. **Frontmatter is mandatory:** `name`, `description`, optionally `version`. Description should be ≤ 2 sentences and start with a verb ("Detects…", "Validates…", "Generates…").
2. **Lead with a Quick Decision Tree.** Tell Copilot when to use this skill vs another. Examples: `dotnet-inspect/SKILL.md` decision tree.
3. **Cross-reference, don't duplicate.** If your skill depends on the registry, link to `obsolete-api-registry`; don't restate the entries.
4. **Embed in the MCP server.** Add an `<EmbeddedResource ... LogicalName="skills/<name>.md" />` line to `src/maf-autopilot/maf-autopilot.csproj`, add `<name>` to `AllowedSkillNames` in `Resources/MafResources.cs`, and add a test row to `MafResourcesTests.GetSkill_AllAllowlistedNames_Resolve`.
5. **The agents reference skills explicitly.** Add a row to the "Skill Selection Guide" table in `.github/agents/maf-migration.agent.md` (and `maf-auditor.agent.md` if pre-migration).

### Skill naming convention

The skill directory namespace is already MAF-scoped: it lives under `.github/skills/` AND is served via the `maf://skills?name=...` URI. **Do not double-prefix** with `maf-` when the topic is unambiguous in that namespace.

- **No prefix** when the skill is about a **complementary tool** applied to MAF (e.g., `dotnet-inspect`, `nuget-diff-analyzer`, `cs0618-hunter`). The unprefixed name is honest about the subject.
- **No prefix** when the skill name is **only meaningful inside the MAF context** in this repo (e.g., `obsolete-api-registry`, `migration-plan-creator`, `migration-retrospective`). There is no other obsolete-API registry / migration plan / retrospective in this namespace — the prefix would be redundant.
- **Prefix with `maf-`** when the skill is intrinsically about MAF surface or behaviour and the unprefixed name would read as a general-CS term (e.g., `maf-anti-pattern-scanner`, `maf-migration-guide`, `maf-release-watcher`). The prefix prevents misreading as a generic linter / guide / watcher.

The convention is **not** "every MAF-related skill gets `maf-`". It's "prefix when the unprefixed name is ambiguous outside this namespace; otherwise don't." Apply the same logic when adding new skills.

## Adding an agent (`.github/agents/<name>.agent.md`)

Agent files end with `.agent.md` — the extension already marks the file as an agent. **Do not** put `-agent` in the filename (e.g., `maf-foo-agent.agent.md` is redundant; use `maf-foo.agent.md`). Prefer noun-roles in the filename when the agent's job has a settled English noun (`maf-auditor`, `maf-reviewer`, `maf-responder`); use activity nouns when the role is uncommon (`maf-onboarding`, `maf-migration`).

## Changing an agent (`.github/agents/<name>.agent.md`)

Agents are GitHub Copilot Chat agents (Markdown + YAML frontmatter). The three existing agents — `maf-migration`, `maf-auditor`, and `maf-best-practice-reviewer` — implement a strict orchestrator pattern. When changing them:

1. **Preserve the build-verified loop.** `maf-migration.agent.md`'s "Tracking Table Process" is the contract with users. Never weaken: every task ends with a `dotnet build` green before mark-complete.
2. **Skill references must resolve.** If you change the skill name, update every agent that references it.
3. **`@maf-auditor` is pre-migration only.** `maf-best-practice-reviewer` (plan rows #42 + #49) handles steady-state audits on clean 1.3.0 codebases. Don't pile post-migration auditing into `maf-auditor` — keep them split.

## Adding an MCP server tool (`src/maf-autopilot/Tools/`)

1. Decorate with `[McpServerToolType]` on the class and `[McpServerTool]` on the method.
2. **Inject `RegistryService` via constructor** — it's a DI singleton.
3. **Description block is contract.** The `[Description("""...""")]` is what the model sees; write it like a function docstring for a foreign reader. Include input format, output format, and a "when to call this" sentence.
4. **Tool name convention.** The SDK emits PascalCase from the C# method name (`MafApiSafety` → exposed as `MafApiSafety`). Use PascalCase in docs.
5. **Tests.** Add `<ToolName>Tests.cs` in `src/maf-autopilot.Tests/`. Cover: empty input → friendly error; happy path; the regression case that motivated the tool.

## PR conventions

- **Conventional commits** (`feat:`, `fix:`, `docs:`, `chore:`, `test:`).
- **Update the plan tracking table** in the same commit. If your work fits an existing row, update its status. If not, add a new row.
- **Update the doc set:** every change to skills/agents/MCP code should at minimum sync the row in `maf-migration-toolkit-plan.md`. Larger changes also touch `project-status-and-vision.md`.
- **Tests must pass.** No `[Fact(Skip = ...)]` to bypass CI.
- **Don't bypass git hooks** (`--no-verify`, `--no-gpg-sign`, `--amend` on shared commits).

## Common pitfalls

- **Don't duplicate registry data.** Many entries can use the same NuGet package or guide section — that's a *reference*, not a *copy*.
- **Don't add a skill for a one-off pattern.** If it'll fire once per migration, put it in the registry. Skills are reusable procedures.
- **Don't relax the build-verified loop.** Every migration agent run ends green or stays open. Silent partial migrations were the original bug class that motivated this project.
- **Don't pin `dotnet-inspect` below v0.7.8.** Earlier versions miss `[Obsolete]` at the overload level — the closed gap is the whole story.

## Where to start

Open issues labeled `good-first-issue` (when they exist). Otherwise, scan the plan tracking table for `❌ NOT STARTED` rows tagged P2 or P3 — those are the lowest-risk on-ramps.
