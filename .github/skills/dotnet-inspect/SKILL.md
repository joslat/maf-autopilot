---
name: dotnet-inspect
version: 0.7.6
description: "Query .NET APIs across NuGet packages, platform libraries, and local files. Search for types, list API surfaces, compare and diff versions, find extension methods and implementors. Use whenever you need to answer questions about .NET library contents. IMPORTANT: Does NOT detect [Obsolete] attributes at the overload level — use cs0618-hunter for obsolete API detection."
---

# dotnet-inspect

Query .NET library APIs — types, members, diffs, extensions, implementations, dependencies, and source links.

## ⚠️ Critical Limitation: [Obsolete] Overload Detection

**dotnet-inspect does NOT flag `[Obsolete]` attributes at the individual overload level.**

When `member WorkflowBuilder` lists multiple overloads for `AddFanInBarrierEdge`, **neither overload is marked as deprecated** in the output. Both appear identical. The `[Obsolete]` attribute is only visible in the single-overload `--index` detail view — but you must already know which overload to suspect before you can drill into it.

**For obsolete API detection: use `cs0618-hunter` skill, not this skill.**

This limitation is filed as [richlander/dotnet-inspect #316](https://github.com/richlander/dotnet-inspect/issues/316).

---

## When to Use This Skill (and When NOT to)

| Use dotnet-inspect when… | Do NOT use dotnet-inspect when… |
|--------------------------|----------------------------------|
| Checking if a type exists in a package | Checking if a method is `[Obsolete]` |
| Listing members of a type | Detecting CS0618 warnings |
| Diffing breaking changes between versions | Verifying which overload is deprecated |
| Finding extension methods | Confirming obsolete replacement patterns |
| Looking up method signatures | — |

---

## Quick Decision Tree

- **Code broken after upgrade?** → `diff --package Foo@old..new` first, then `member`
- **What types exist in a package?** → `type --package Foo`
- **What members does a type have?** → `member Type --package Foo`
- **What changed between versions?** → `diff --package Foo@1.2.0..1.3.0`
- **What does this type depend on?** → `depends`
- **Where is the source code?** → `source Type --package Foo`
- **Are there obsolete APIs?** → **Use `cs0618-hunter` skill, not this tool**

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

> This single-overload view WILL show `[Obsolete]` in the Custom Attributes section — but only if you already know which overload to inspect. This is why `cs0618-hunter` (compiler-based) is the reliable detection method.

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
