---
name: maf-obsolete-api-registry
description: "Machine-readable registry of known CS0618 obsolete API warnings for MAF migrations. Each entry maps an obsolete method signature to its replacement, with the exact fix pattern. Prefer the MCP tool MafRegistryLookup (by ID) or MafApiSafety (by symbol) for one-shot queries; this skill is for understanding the registry schema and contributing new entries. Load this skill when cs0618-hunter has identified a CS0618 warning and you need the deterministic fix."
---

# Obsolete API Registry

This registry is the source of truth for all known `[Obsolete]` APIs in MAF 1.3.0.

## How to Use This Skill

1. A `cs0618-hunter` scan has produced one or more CS0618 warning lines
2. Extract the **obsolete method name** from the warning text
3. Look it up in `registry.yaml` (in this directory)
4. Apply the `replacement_pattern` exactly as documented
5. If the warning is NOT in the registry — add it (see "Adding New Entries" below)

## registry.yaml Format

```yaml
- id: <unique identifier>
  package: <NuGet package name>
  version_introduced: <MAF version where this became obsolete>
  type: <fully-qualified type name>
  method: <method name>
  obsolete_signature: <exact C# signature of the obsolete overload>
  replacement_signature: <exact C# signature of the correct overload>
  argument_order_change: <true|false>
  fix_description: <one-line description of what to change>
  example_before: |
    <exact code to find>
  example_after: |
    <exact replacement code>
  cs_warning: CS0618
  guide_section: <section number in maf-migration-guide>
  notes: <any additional context>
```

## Adding New Entries

When `cs0618-hunter` finds a CS0618 warning that is NOT in `registry.yaml`:

1. Open `registry.yaml`
2. Add a new entry using the format above
3. Fill in the obsolete and replacement signatures from the compiler warning message
4. Test the fix — confirm the warning disappears after applying it
5. Commit the updated `registry.yaml` alongside your migration changes

This is how the registry grows over time — each migration run that finds a new obsolete API enriches the registry for future runs.

---

## Current Registry Contents

See `registry.yaml` for the full machine-readable data. Current entries:

| ID | Method | Obsolete signature | Replacement |
|----|--------|--------------------|-------------|
| `MAF130-FAN-IN-001` | `WorkflowBuilder.AddFanInBarrierEdge` | `(ExecutorBinding target, IEnumerable<ExecutorBinding> sources)` | `(IEnumerable<ExecutorBinding> sources, ExecutorBinding target)` |
