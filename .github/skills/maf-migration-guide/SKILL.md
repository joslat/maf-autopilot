---
name: maf-migration-guide
description: "Version-aware navigator for Microsoft Agent Framework migrations. Load this skill for ordered upgrade paths, exact package versions, breaking API recipes, behavioral changes, or guide citations across every tracked MAF release."
---

# MAF Migration Guide — Version-Aware Navigator

MAF migrations are cumulative. Never jump directly to the newest guide and
assume it contains earlier changes. Determine the source and target versions,
then apply every intervening per-version guide in ascending order.

## Start Here

1. Read `maf://constraints` before proposing code changes.
2. Identify the exact installed MAF package versions from the project and its
   resolved dependency graph. Do not infer them from a solution name or a bare
   release-train number.
3. Call `MafMigrationPath(currentVer, targetVer)`. Its ordered result is the
   authoritative set of per-version guide sections for the migration.
4. Read each returned `guides/maf-X.Y.Z-migration-guide.md` file in order. For a
   single-step upgrade, read that target version's file directly.
5. Use `MafRegistryLookup` or `MafRunCs0618Hunt` for an exact symbol recipe, then
   restore, build, and run the relevant behavior tests.

The tracked target is recorded in `.maf-version`; do not hard-code the newest
version into automation. `guides/maf-current-migration-guide.md` is the generated
cumulative reading copy. Edit a per-version source guide, never the cumulative
file, and regenerate it with:

```text
python .github/scripts/gen_guide_section.py --cumulative-only
```

## Guide Inventory

| Target version | Source guide | Status / primary purpose |
|---|---|---|
| 1.3.0 | `guides/maf-1.3.0-migration-guide.md` | Complete foundational guide: agents, sessions, executors, workflows, streaming, hosting, and source generation. |
| 1.4.0 | `guides/maf-1.4.0-migration-guide.md` | Draft evidence; verify unfilled human analysis before relying on it. |
| 1.5.0 | `guides/maf-1.5.0-migration-guide.md` | Draft evidence; verify unfilled human analysis before relying on it. |
| 1.6.1 | `guides/maf-1.6.1-migration-guide.md` | Reviewed 1.5-to-1.6.1 delta. |
| 1.10.0 | `guides/maf-1.10.0-migration-guide.md` | Reviewed 1.6.1-to-1.10 delta. |
| 1.11.0 | `guides/maf-1.11.0-migration-guide.md` | Reviewed 1.10-to-1.11 delta. |
| 1.11.1 | `guides/maf-1.11.1-migration-guide.md` | Reviewed servicing delta. |
| 1.12.0 | `guides/maf-1.12.0-migration-guide.md` | Reviewed 1.11.1-to-1.12 delta. |
| 1.13.0 | `guides/maf-1.13.0-migration-guide.md` | Reviewed file-store and file-access migration. |
| 1.14.0 | `guides/maf-1.14.0-migration-guide.md` | Reviewed agent/session, approval, Harness, AG-UI, Copilot, and Shell migration. |
| 1.15.0 | `guides/maf-1.15.0-migration-guide.md` | Reviewed Hosting session-store and workflow behavior migration. |
| 1.16.0 | `guides/maf-1.16.0-migration-guide.md` | Reviewed VectorData 10.7, Magentic, persistence, hosting, and security-boundary migration. |
| 1.17.0 | `guides/maf-1.17.0-migration-guide.md` | Reviewed declarative failure/redaction and durable-extension lifecycle migration. |

If a requested path crosses 1.4 or 1.5, surface the draft status and verify those
steps against the exact assemblies and official tag before applying them. Do
not silently omit those versions.

## How Each Per-Version Guide Is Organized

Every watcher-generated guide has protected package/release evidence followed
by four reviewed analysis sections:

- **Breaking Changes** — source, binary, dependency, and behavior compatibility
  work required for that step.
- **New Patterns** — exact package versions, lifecycle transitions, and current
  usage recipes.
- **Obsolete APIs Added** — new compiler-warning migrations for that release.
- **Known Misalignments** — release-note, package, source, or documentation gaps
  that an API diff alone cannot explain.

Treat package maturity as part of the contract. Stable, preview, release
candidate, and alpha packages in one MAF train can have different exact
versions. A package split, externalization, or independent release cadence is
not permission to invent a coordinated version or rename a package reference.

## Route by Question

| Question or symptom | Use |
|---|---|
| “What do I read from A to B?” | `MafMigrationPath(A, B)`, then the returned guides in order. |
| CS0618, CS0246, CS1503, or a removed member | `MafRunCs0618Hunt`, then `MafRegistryLookup` for the matching entry. |
| Exact package/framework compatibility | `MafCompatibility(targetVersion)` plus `docs/compatibility-matrix.md`. |
| Public API change between two artifacts | `MafDiffPackage` with the exact published versions. |
| Empty response, hung workflow, lost state, or changed failure behavior | The target guide's Breaking Changes, New Patterns, and Known Misalignments; add a runtime regression test. |
| A security-sensitive fix | `maf://constraints`, `docs/security.md`, and the relevant guide's trust-boundary notes. |
| A 1.2-to-1.3 executor/session migration | The full 1.3 guide's sections 6, 9, 15, 17, and 20. |

## Current High-Risk Checkpoints

- **1.14:** agent/session lifecycle changes, approval semantics, explicit Harness
  file/shell composition, AG-UI package split, Copilot function declarations,
  and the binary-breaking `ShellPolicy` constructor.
- **1.15:** preview Hosting `conversationId:` named arguments become
  `sessionStoreId:`, custom stores implement `DeleteSessionAsync`, and session
  snapshots/checkpoints have stronger isolation and ordering contracts.
- **1.16:** the VectorData floor moves from 9.7 to 10.7; first-party MAF API
  diffs remain mostly additive, but transitive source/provider migrations and
  several persistence/hosting behavior checks are required.
- **1.17:** public APIs and dependency floors are clean, while declarative agent
  errors become terminal and Foundry failure detail remains subject to host
  redaction. Durable Task and Azure Functions retain their package IDs but move
  to an independently versioned extension repository.

## Verification Contract

- Pin repository-supported `dotnet-inspect` exactly as required by
  `maf://constraints`; reject truncated or structurally invalid diff output.
- Build the real solution with warnings visible. The compiler is authoritative
  for overload resolution and project-local or transitive obsolete usage.
- Run targeted runtime tests for session continuity, checkpoint restore,
  streaming completion/error paths, tool approval, fan-out/fan-in, and hosting
  boundaries touched by the selected guides.
- Re-run MAF Doctor after edits and report `scan_truncated` or other incomplete
  evidence rather than presenting a partial scan as clean.
- Keep before/after recipes scoped to the exact release where they apply. A
  correct 1.3 replacement can itself require migration in a later step.
