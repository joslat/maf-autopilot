---
name: nuget-diff-analyzer
description: "Runs dotnet-inspect diff between two MAF package versions and post-processes the raw output into a structured, actionable migration report. Use this skill when starting a migration to understand what changed between source and target versions. Note: does NOT detect [Obsolete] overloads — always pair with cs0618-hunter."
---

# nuget-diff-analyzer

## Purpose

`dotnet-inspect diff` outputs a flat, hard-to-act-on list of API changes. This skill wraps it with:
1. Categorized output (removed types, renamed members, signature changes, etc.)
2. Risk classification (CS0246 risk, CS0618 risk, silent runtime risk)
3. Cross-reference with `obsolete-api-registry.yaml` for known patterns
4. Actionable migration guidance per change

## ⚠️ Critical Limitation

`dotnet-inspect diff` shares the same `[Obsolete]` detection gap as `member`. **Newly obsolete overloads will NOT appear in the diff output.** Always run `cs0618-hunter` skill after any package update, regardless of what this skill reports.

---

## The Workflow

### Step 1 — Run the diff

```bash
# Core framework diff
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package Microsoft.Agents.AI@<old_version>..<new_version> \
  --source https://api.nuget.org/v3/index.json

# Workflows package diff (if applicable)
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package Microsoft.Agents.AI.Workflows@<old_version>..<new_version> \
  --source https://api.nuget.org/v3/index.json
```

Example (1.2.0 → 1.3.0):
```bash
dnx dotnet-inspect@0.7.6 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package Microsoft.Agents.AI@1.2.0..1.3.0 \
  --source https://api.nuget.org/v3/index.json
```

### Step 2 — Categorize the output

Classify each item in the diff output into one of these categories:

| Category | Indicator | Compiler impact | Risk |
|----------|-----------|----------------|------|
| **Removed type** | `- type TypeName` | CS0246 | High |
| **Removed member** | `- member TypeName.MethodName` | CS0246 | High |
| **Renamed type** | `- type OldName` + `+ type NewName` | CS0246 | High |
| **Renamed member** | `- member Old` + `+ member New` | CS0246 | Medium |
| **Signature change** | `~ member Method(old sig)` | CS0246 or runtime | Medium |
| **New obsolete overload** | (NOT visible in diff — see warning above) | CS0618 | High |
| **New type or member** | `+ type/member` | None — opportunity | Low |

### Step 3 — Build the structured report

Output a markdown report with this structure:

```markdown
## NuGet Diff Report: Microsoft.Agents.AI <old_version> → <new_version>

### Summary
| Category | Count |
|----------|-------|
| Removed types | N |
| Removed members | N |
| Renamed types/members | N |
| Signature changes | N |
| New additions | N |
| Obsolete overloads (NOT in diff — run cs0618-hunter) | ⚠️ Unknown |

### Removed Types (CS0246 risk)
| Type | Package | Registry entry | Migration |
|------|---------|---------------|-----------|
| `AgentThread` | Microsoft.Agents.AI | MAF130-THREAD-001 | → `AgentSession` via `CreateSessionAsync()` |
| `InProcessExecution` | Microsoft.Agents.AI | MAF130-STREAM-001 | → `agent.RunStreamingAsync()` |
| `ReflectingExecutor<T>` | Microsoft.Agents.AI.Workflows | — | → `sealed partial class : Executor` + `[MessageHandler]` |
| `AIAgentExtensions` | Microsoft.Agents.AI | MAF130-A2A-001 | → `services.AddA2AServer()` |
| `StreamsMessageAttribute` | Microsoft.Agents.AI.Workflows | MAF130-ATTR-001 | → delete attribute |
| `YieldsMessageAttribute` | Microsoft.Agents.AI.Workflows | MAF130-ATTR-002 | → delete attribute |
| `AgentRunUpdateEvent` | Microsoft.Agents.AI | MAF130-EVENT-001 | → `AgentResponseUpdateEvent` |

### Removed Members (CS0246 risk)
| Member | Registry entry | Migration |
|--------|---------------|-----------|
| `ChatClientAgent.GetNewThread()` | MAF130-THREAD-001 | → `await agent.CreateSessionAsync(ct)` |
| `AgentResponse.Deserialize<T>()` | — | → `agent.RunAsync<T>()` + `.Result` |
| `app.MapA2A(path)` | MAF130-A2A-002 | → `app.MapA2AHttpJson(path)` |

### Renamed Members
| Old | New | Registry entry |
|-----|-----|---------------|
| `InProcessExecution.StreamAsync()` | `agent.RunStreamingAsync()` | MAF130-STREAM-001 |
| `AgentRunUpdateEvent` | `AgentResponseUpdateEvent` | MAF130-EVENT-001 |

### New Additions (adoption opportunities)
| Item | Notes |
|------|-------|
| `AgentSession` | Replaces `AgentThread` — richer API |
| `A2AServerRegistrationOptions` | New DI registration with AgentCard metadata |
| `AgentResponseUpdateEvent` | Renamed from AgentRunUpdateEvent |

### ⚠️ Not Visible in Diff — Run cs0618-hunter
The following patterns are [Obsolete] but will NOT appear above:
- `WorkflowBuilder.AddFanInBarrierEdge(target, sources)` — see MAF130-FAN-IN-001
- `ChatClientAgent.SerializeSession(session)` — see MAF130-SESSION-001

Always run: `dotnet build 2>&1 | Select-String "warning CS0618"` after package update.
```

### Step 4 — Cross-reference registry

For each removed/renamed item in the diff output, check `.github/skills/obsolete-api-registry/SKILL.md`:
- If there is a registry entry → note it in the report (provides fix pattern)
- If there is NO registry entry → flag it for manual investigation; consider adding an entry

### Step 5 — Estimate migration effort

For each category, estimate files affected using:
```powershell
# Example for AgentThread (adjust pattern per finding)
Select-String -Path "src/**/*.cs" -Pattern "AgentThread|GetNewThread" -Recurse | Measure-Object -Line
```

Add the occurrence counts to the structured report. This feeds directly into the `migration-plan-creator` skill (task population and effort estimation).

---

## Output Contract

Produce:
1. A markdown diff report (paste into the migration plan or save as `src/docs/diff-report.md`)
2. A list of registry IDs that apply to this migration (for `obsolete-api-registry` lookups)
3. A reminder: **run `cs0618-hunter` next** — the diff cannot see obsolete overloads

---

## Integration with Other Skills

| After this skill... | Run next |
|--------------------|---------|
| Always | `cs0618-hunter` — catches what diff missed |
| If executors found | `fan-out-validator` — validate topology |
| If plan needed | `migration-plan-creator` — populate with diff findings |
| If unknown changes | `dotnet-inspect` `member` — inspect individual types |
