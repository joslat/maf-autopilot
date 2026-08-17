---
name: cs0618-hunter
description: "Detects and fixes CS0618 obsolete API warnings in .NET builds. The compiler is the authoritative source for what your project actually triggers — it catches transitive obsoletions, overload-resolution surprises, and project-local [Obsolete] attributes that static inspection cannot see. Pair with the repository-pinned dotnet-inspect@0.9.1 for the static / pre-build view."
---

# cs0618-hunter — compiler ground-truth for obsolete API usage

> **⚡ Prefer the MCP tool.** The `maf-autopilot` MCP server exposes **`MafRunCs0618Hunt(projectPath)`** — it shells `dotnet build`, parses every CS0618 / CS0246 diagnostic, joins each one to its canonical fix in the obsolete-API registry, and returns a structured report. The procedural walkthrough below is preserved for when you need to run the steps manually (offline, debugging the tool itself, etc.).

## Why the compiler stays authoritative

`dotnet-inspect@0.7.8` was the first release to surface `[Obsolete]` in member listings (PR [#318](https://github.com/richlander/dotnet-inspect/pull/318) closes issue [#316](https://github.com/richlander/dotnet-inspect/issues/316)); this repository pins 0.9.1. That's an excellent pre-build static check, but **three classes of obsoletion remain compiler-only**:

1. **Transitive obsoletions** — the API your code calls isn't obsolete, but it forwards to one that is.
2. **Overload-resolution surprises** — a more-specific overload was added in a minor release and your literal types now bind to it.
3. **Project-local `[Obsolete]`** — types your team marked obsolete in this solution. Static inspection of NuGet packages can't see those.

For all three, `dotnet build | Select-String "warning CS0618"` is the only authoritative answer.

## Manual fallback workflow

When `MafRunCs0618Hunt` is unavailable:

```powershell
# Step 1: get the baseline
dotnet build 2>&1 | Select-String "warning CS0618"

# Step 2: for each warning, look up the canonical fix in the registry
# (the LLM should call MafRegistryLookup with the matching entry ID,
#  or open .github/skills/maf-obsolete-api-registry/registry.yaml)

# Step 3: apply the fix; re-run the build; confirm the warning is gone

# Step 4: move on to the next warning. Never batch.

# Step 5: declare done only when:
dotnet build 2>&1 | Select-String "warning CS0618" | Measure-Object | Select-Object -ExpandProperty Count
# returns 0.
```

## Quick reference

| Command                                        | Purpose                              |
|------------------------------------------------|--------------------------------------|
| `MafRunCs0618Hunt(projectPath)` (MCP)          | Full automated CS0618/CS0246 hunt    |
| `MafApiSafety(apiName)` (MCP)                  | Is a single API safe? (registry only)|
| `MafRegistryLookup(entryId)` (MCP)             | Full fix details for entry ID        |
| `dotnet build 2>&1 \| Select-String "CS0618"` | Manual baseline scan                  |
