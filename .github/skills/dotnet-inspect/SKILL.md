---
name: dotnet-inspect
version: 0.7.6
description: "Query .NET APIs across NuGet packages, platform libraries, and local files. Search for types, list API surfaces, compare and diff versions, find extension methods and implementors. [Obsolete] attributes are now surfaced at the overload level (issue #316 resolved). For project-wide CS0618 scanning, prefer cs0618-hunter (one compiler pass covers all files)."
---

# dotnet-inspect

Query .NET library APIs — types, members, diffs, extensions, implementations, dependencies, and source links.

## [Obsolete] Attribute Detection — Issue #316 Resolved

**`dotnet-inspect` now surfaces `[Obsolete]` attributes at the individual overload level** — [richlander/dotnet-inspect #316](https://github.com/richlander/dotnet-inspect/issues/316) has been resolved.

The standard `member` listing marks obsolete members, and the `--index` detail view shows full `[Obsolete]` metadata including the replacement message.

**Recommendation for CS0618 detection:**
- **Project-wide scan** (find all warnings across a solution): use `cs0618-hunter` — one `dotnet build | Select-String CS0618` pass covers everything.
- **Targeted lookup** (is this specific overload obsolete?): use `dotnet-inspect member ... --index` — now reliable.

---

## When to Use This Skill (and When NOT to)

| Use dotnet-inspect when… | Consider cs0618-hunter instead when… |
|--------------------------|--------------------------------------|
| Checking if a type exists in a package | Scanning an entire solution for all CS0618 warnings |
| Listing members of a type | You want a one-command project-wide report |
| Diffing breaking changes between versions | — |
| Finding extension methods | — |
| Checking if a specific overload is `[Obsolete]` | — |
| Looking up method signatures | — |

---

## Quick Decision Tree

- **Code broken after upgrade?** → `diff --package Foo@old..new` first, then `member`
- **What types exist in a package?** → `type --package Foo`
- **What members does a type have?** → `member Type --package Foo`
- **What changed between versions?** → `diff --package Foo@1.2.0..1.3.0`
- **What does this type depend on?** → `depends`
- **Where is the source code?** → `source Type --package Foo`
- **Are there obsolete APIs?** → Project-wide: `cs0618-hunter` (one build pass). Targeted per-member: `dotnet-inspect member ... --index`

---

## Installation and Invocation

```bash
# Install (once, global)
dotnet tool install -g dnx

# Standard pattern — BOTH --source flags are required for NuGet packages
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- <command> \
  --package <PackageName>@<version> --source https://api.nuget.org/v3/index.json
```

> The `--source https://api.nuget.org/v3/index.json` flag must appear on **both** the `dnx` invocation and the inner command.

---

## Key Commands for MAF Migrations

### Diff breaking changes between MAF versions

```bash
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package Microsoft.Agents.AI@1.2.0..1.3.0 --source https://api.nuget.org/v3/index.json

dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package Microsoft.Agents.AI.Workflows@1.2.0..1.3.0 --source https://api.nuget.org/v3/index.json
```

### List types in a MAF package

```bash
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- type \
  --package Microsoft.Agents.AI.Workflows@1.3.0 --source https://api.nuget.org/v3/index.json
```

### Inspect members of a specific type

```bash
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- member ChatClientAgent \
  --package Microsoft.Agents.AI@1.3.0 --source https://api.nuget.org/v3/index.json

dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- member WorkflowBuilder \
  --package Microsoft.Agents.AI.Workflows@1.3.0 --source https://api.nuget.org/v3/index.json
```

### Inspect a specific method (single overload detail view — DOES show custom attributes)

```bash
# Use --params to select a specific overload, --index for full detail including custom attributes
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- member WorkflowBuilder \
  --package Microsoft.Agents.AI.Workflows@1.3.0 --source https://api.nuget.org/v3/index.json \
  -m AddFanInBarrierEdge --params "ExecutorBinding,IEnumerable<ExecutorBinding>" --index
```

> Since issue #316 was resolved, this view reliably shows `[Obsolete]` in the Custom Attributes section including the replacement message. For project-wide scanning across many files, `cs0618-hunter` (one `dotnet build` pass) is still faster.

---

## General Usage Patterns

```bash
# List members of a type
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- member JsonSerializer \
  --package System.Text.Json --source https://api.nuget.org/v3/index.json

# What changed between versions
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package System.CommandLine@2.0.0-beta4.22272.1..2.0.3 --source https://api.nuget.org/v3/index.json

# Check latest version
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- \
  Microsoft.Agents.AI --latest-version --source https://api.nuget.org/v3/index.json
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
