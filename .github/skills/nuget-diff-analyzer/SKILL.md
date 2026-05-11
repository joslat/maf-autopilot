---
name: nuget-diff-analyzer
description: "Post-processes dotnet-inspect@0.7.8 -- diff output into a categorised report (breaking / additive / newly-obsolete) with each finding cross-referenced to the MAF obsolete-API registry."
---

# nuget-diff-analyzer — structured NuGet version-diff reports

> **⚡ Prefer the MCP tool.** The `maf-autopilot` MCP server exposes **`MafDiffPackage(packageId, oldVersion, newVersion)`** — it wraps `dotnet-inspect@0.7.8 -- diff`, parses the markdown output into a structured `DiffParseResult` (Breaking / Additive / NewlyObsolete lists), and cross-references each finding against the obsolete-API registry. The procedural walkthrough below is preserved for when you need to run the steps manually.

## Why this exists

Raw `dotnet-inspect diff` output is markdown-pretty but unstructured — the LLM has to re-parse it on every call. The MCP tool does the parsing once and returns structured findings that downstream tools (best-practice reviewer, migration auditor) can consume directly.

## Manual fallback workflow

```bash
# Step 1: emit the raw diff
dnx dotnet-inspect@0.7.8 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package Microsoft.Agents.AI@1.2.0..1.3.0 --source https://api.nuget.org/v3/index.json

# Step 2: read the output. It has the shape:
#   # API Diff: <package>
#   | Versions | **1.2.0** -> **1.3.0** |
#   | Summary  | N breaking, M additive across K types |
#
#   ## Breaking Changes
#   ### <Type>
#   - Member '...' signature changed: `oldSig` -> `newSig`
#   - Member '...' was removed
#
#   ## Additive Changes
#   ### <Type>
#   - Type '...' was added
#   - Member '...' was added

# Step 3: for each breaking change, look up the registry:
#   MafApiSafety("<symbol-name>")
# or browse .github/skills/obsolete-api-registry/registry.yaml
```

## Output categories (what `MafDiffPackage` produces)

| Category         | Origin in diff output                              | Action                              |
|------------------|----------------------------------------------------|-------------------------------------|
| Breaking         | `## Breaking Changes` section                      | Add to migration plan; high risk    |
| Additive         | `## Additive Changes` section                      | Inform future patterns; not urgent  |
| NewlyObsolete    | Any entry whose body contains `[Obsolete...]`      | Inspect; likely a deprecation flag  |

## Pin reminder

**Use `dotnet-inspect@0.7.8` or later.** Earlier versions miss `[Obsolete]` at the overload level — see issue [#316](https://github.com/richlander/dotnet-inspect/issues/316). The pinned dnx invocation in the manual fallback above already reflects this.
