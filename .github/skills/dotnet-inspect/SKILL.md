---
name: dotnet-inspect
version: 0.9.1
description: "Query .NET APIs across NuGet packages, platform libraries, and local files. Search for types, list API surfaces, compare and diff versions, find extension methods and implementors. Use whenever you need to answer questions about .NET library contents. Commands pin v0.9.1; pair with cs0618-hunter (compiler-based) for runtime ground-truth."
---

# dotnet-inspect

Query .NET library APIs — types, members, diffs, extensions, implementations, dependencies, and source links.

## ℹ️ [Obsolete] Detection — Status as of v0.7.8

**Closed gap (good news):** As of [richlander/dotnet-inspect v0.7.8 (2026-05-04)](https://github.com/richlander/dotnet-inspect/releases/tag/v0.7.8), the tool now **surfaces `[Obsolete]` members in listings** (PR #318, closes issue #316). The historical blind spot — where overloads of `AddFanInBarrierEdge` appeared identical even when one was deprecated — is fixed in this version.

**`dotnet-inspect` and `cs0618-hunter` are now complementary, not workaround-vs-truth:**

| Use… | When… |
|------|-------|
| **`dotnet-inspect` (this skill)** | Pre-build static inspection — "what is deprecated in `Microsoft.Agents.AI@1.3.0`?" without compiling. Fast, scriptable, works against any NuGet package. |
| **`cs0618-hunter` skill** | Compiler ground-truth — "what is my code actually triggering today?" Catches transitive obsoletions, overload-resolution surprises, and project-specific `[Obsolete]` definitions that aren't in NuGet. |

Both keep value. Earlier versions (≤ v0.7.7) required `cs0618-hunter` as the only reliable path; v0.7.8 makes static inspection a viable pre-build option.

> **Pin to exact v0.9.1.** Versions ≤ v0.7.7 miss `[Obsolete]` annotations, and v0.7.8 is affected by the Linux redirected-output corruption seen in the release watcher.

---

## When to Use This Skill (and When NOT to)

| Use dotnet-inspect when… | Use `cs0618-hunter` instead when… |
|--------------------------|------------------------------------|
| Checking if a type exists in a package | You need the compiler's view of CS0618 warnings in your project |
| Listing members of a type (incl. `[Obsolete]` as of v0.7.8) | You suspect transitive or overload-resolution-dependent obsoletions |
| Diffing breaking changes between versions | A project-local `[Obsolete]` attribute (not in NuGet) is in play |
| Finding extension methods | — |
| Looking up method signatures | — |
| Pre-build "would this break if I upgraded?" check | Build verification after edits |

---

## Quick Decision Tree

- **Code broken after upgrade?** → `diff --package Foo@old..new` first, then `member`
- **What types exist in a package?** → `type --package Foo`
- **What members does a type have?** → `member Type --package Foo` (now flags `[Obsolete]`)
- **What changed between versions?** → `diff --package Foo@1.2.0..1.3.0`
- **What does this type depend on?** → `depends` (try `--mermaid` for diagrams — v0.7.5+)
- **Where is the source code?** → `source Type --package Foo`
- **What does my project actually trigger?** → `cs0618-hunter` (compiler ground-truth)

---

## Installation and Invocation

```bash
# Install the exact repository-supported release (once, global)
dotnet tool install --global dotnet-inspect --version 0.9.1

# Standard pattern for NuGet packages
dotnet-inspect <command> \
  --package <PackageName>@<version> --source https://api.nuget.org/v3/index.json
```

> The explicit `--source https://api.nuget.org/v3/index.json` keeps package resolution deterministic in workspaces with custom feeds.

---

## Key Commands for MAF Migrations

### Diff breaking changes between MAF versions

```bash
dotnet-inspect diff \
  --package Microsoft.Agents.AI@1.2.0..1.3.0 --source https://api.nuget.org/v3/index.json

dotnet-inspect diff \
  --package Microsoft.Agents.AI.Workflows@1.2.0..1.3.0 --source https://api.nuget.org/v3/index.json
```

### List types in a MAF package

```bash
dotnet-inspect type \
  --package Microsoft.Agents.AI.Workflows@1.3.0 --source https://api.nuget.org/v3/index.json
```

### Inspect members of a specific type

```bash
dotnet-inspect member ChatClientAgent \
  --package Microsoft.Agents.AI@1.3.0 --source https://api.nuget.org/v3/index.json

dotnet-inspect member WorkflowBuilder \
  --package Microsoft.Agents.AI.Workflows@1.3.0 --source https://api.nuget.org/v3/index.json
```

### Inspect a specific method (single overload detail view — DOES show custom attributes)

```bash
# Use --params to select a specific overload, --index for full detail including custom attributes
dotnet-inspect member WorkflowBuilder \
  --package Microsoft.Agents.AI.Workflows@1.3.0 --source https://api.nuget.org/v3/index.json \
  -m AddFanInBarrierEdge --params "ExecutorBinding,IEnumerable<ExecutorBinding>" --index
```

> The single-overload `--index` view has always shown `[Obsolete]` in the Custom Attributes section. As of v0.7.8 the multi-overload listing also surfaces `[Obsolete]`, so you no longer need to know which overload to suspect in advance.

---

## General Usage Patterns

```bash
# List members of a type
dotnet-inspect member JsonSerializer \
  --package System.Text.Json --source https://api.nuget.org/v3/index.json

# What changed between versions
dotnet-inspect diff \
  --package System.CommandLine@2.0.0-beta4.22272.1..2.0.3 --source https://api.nuget.org/v3/index.json

# Check latest version
dotnet-inspect Microsoft.Agents.AI --latest-version \
  --source https://api.nuget.org/v3/index.json
```

## Key Syntax Rules

- **Generic types need quotes**: `'Option<T>'`, `'IEnumerable<T>'`
- **Diff ranges use `..`**: `--package Foo@1.2.0..1.3.0`
- **`-m`** filters by member name: `-m AddFanInBarrierEdge`
- **`--params`** selects a specific overload by parameter types
- **`--index`** shows full detail including custom attributes (but only for single-overload view)
- Do NOT pipe output through `head`/`tail` — use `--head N` / `--tail N` built-in flags

## Command Quick Reference

| Command | Purpose |
|---------|---------|
| `type` | Discover types in a package |
| `member` | Inspect members of a type |
| `diff` | Compare API surfaces between versions |
| `find` | Search for types by glob/fuzzy match |
| `extensions` | Find extension methods for a type |
| `implements` | Find types implementing an interface |
| `depends` | Walk dependency graphs |
| `source` | SourceLink URLs, fetch source content |
| `package` | Package metadata and versions |
