---
name: maf-release-watcher
description: "Detects new MAF releases, analyzes breaking changes, updates the migration guide and registry, and creates a PR with all changes. Invoke manually when you want to check for MAF updates, or automatically via the GitHub Actions workflow. This skill is the foundation of the self-updating guide system."
---

# maf-release-watcher

## Purpose

When Microsoft ships a new MAF version, this skill automates the update cascade:
1. Detect new version
2. Analyze what changed (diff, release notes)
3. Update registry, compatibility matrix, and guide
4. Produce a PR for human review

This keeps the toolkit current across MAF versions without manual guide maintenance.

---

## When to Use This Skill

- Weekly (via GitHub Actions schedule — `.github/workflows/maf-release-watcher.yml`)
- Manual invocation: "Check if there's a new MAF version"
- Before starting any new migration — verify toolkit is current

---

## The Workflow

### Step 1 — Check for new version

```bash
# Query NuGet API for latest Microsoft.Agents.AI version
curl -s "https://api.nuget.org/v3-flatcontainer/microsoft.agents.ai/index.json" | \
  python3 -c "import sys,json; versions=json.load(sys.stdin)['versions']; print(versions[-1])"
```

Or in PowerShell:
```powershell
$response = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/microsoft.agents.ai/index.json"
$latestVersion = $response.versions[-1]
$currentVersion = Get-Content ".maf-version" -Raw | ForEach-Object { $_.Trim() }

if ($latestVersion -ne $currentVersion) {
    Write-Host "New version detected: $currentVersion -> $latestVersion"
} else {
    Write-Host "No update. Current version $currentVersion is latest."
    exit 0
}
```

### Step 2 — Download and read release notes

```bash
# GitHub CLI (if MAF is on GitHub)
gh release view v<new_version> --repo microsoft/agents --json body --jq .body

# Or fetch NuGet package README/release notes via API
curl -s "https://api.nuget.org/v3/registration5/microsoft.agents.ai/<new_version>.json" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('releaseNotes', 'No release notes'))"
```

### Step 3 — Run nuget-diff-analyzer

Load `.github/skills/nuget-diff-analyzer/SKILL.md` and run it for the version delta:

```bash
dnx dotnet-inspect@0.7.8 -y --source https://api.nuget.org/v3/index.json -- diff \
  --package Microsoft.Agents.AI@<old_version>..<new_version> \
  --source https://api.nuget.org/v3/index.json
```

### Step 4 — LLM analysis of release notes

Parse the release notes to extract:
- Breaking changes (type removals, method renames, interface changes)
- Newly obsolete APIs (will NOT be in the diff — document separately)
- New recommended patterns
- New package requirements

Cross-reference LLM findings against the diff output:
- "LLM says `Foo` was renamed to `Bar`" → diff should show `- type Foo` and `+ type Bar` ✅
- "LLM says `Baz` is now obsolete" → diff will NOT show this ⚠️ — add to registry manually

### Step 5 — Update obsolete-api-registry.yaml

For each newly obsolete API found (from release notes or dotnet build output):

Add a new entry to `.github/skills/obsolete-api-registry/registry.yaml` following the schema:

```yaml
  - id: MAF<version_nodots>-<AREA>-<NNN>
    package: Microsoft.Agents.AI[.Workflows]
    version_introduced: "<new_version>"
    type: <FullyQualifiedTypeName>
    method: <MethodName>
    obsolete_signature: "<OldSignature>"
    replacement_signature: "<NewSignature>"
    argument_order_change: false
    fix_description: "<clear description of what to change>"
    example_before: |
      <code snippet>
    example_after: |
      <code snippet>
    cs_warning: CS0618
    guide_section: "<section number in new guide>"
    dotnet_inspect_detectable: false
    notes: >
      <any additional context>
```

### Step 6 — Update compatibility-matrix.md

Add a new row to `docs/compatibility-matrix.md`:

```markdown
| **<new_version>** | `≥ <Extensions.AI version>` | `≥ <dotnet version>` | `≥ <Azure.AI.OpenAI version>` | `<Generators version>` | <brief notes> |
```

Verify dependency versions from:
1. The NuGet package metadata (dependency graph)
2. Release notes
3. `dotnet-inspect depends --package Microsoft.Agents.AI@<new_version>`

### Step 7 — Generate new guide section

Add a new section to `guides/maf-1.3.0-migration-guide.md` (or create a new guide file for major version jumps):

```markdown
## Section N — Changes in <old_version> → <new_version>

<!-- introduced: <new_version> | applies-to: <old>.x → <new>.x | deprecated-in: none -->

### Breaking changes summary
[from diff + release notes]

### New patterns
[from release notes LLM analysis]

### Obsolete APIs added in this version
[from registry update + cs0618-hunter findings]

### Known misalignments
[from any docs vs. reality gaps found]
```

### Step 8 — Update .maf-version

```bash
echo "<new_version>" > .maf-version
```

### Step 9 — Create PR

Create a pull request with:
- Title: `chore: Update for MAF <new_version>`
- All modified files: `registry.yaml`, `compatibility-matrix.md`, guide, `.maf-version`
- PR checklist for human reviewer (see PR template in `.github/workflows/maf-release-watcher.yml`)

---

## Human Review Checklist (for the PR)

- [ ] Verify LLM-extracted breaking changes match dotnet-inspect diff
- [ ] Verify new registry entries have correct before/after examples
- [ ] Spot-check compatibility matrix against official NuGet package dependencies
- [ ] Verify any new guide section is accurate (test patterns against a sample project if possible)
- [ ] Confirm `.maf-version` is updated to the new version

---

## Output Contract

After this skill completes:
1. `registry.yaml` — new entries added for any new CS0618 patterns
2. `compatibility-matrix.md` — new row added
3. `guides/maf-1.3.0-migration-guide.md` — new section added (or new guide file created)
4. `.maf-version` — updated to new version
5. A PR is created (or a summary of changes for manual PR creation)

---

## Notes on Automation Limitations

- LLM analysis of release notes is good but imperfect — always human-review before merging
- `dotnet-inspect diff` (v0.7.8+) surfaces `[Obsolete]` at the overload level, but the compiler is still ground-truth — also run `cs0618-hunter` against the new version (catches transitive / overload-resolution / project-local obsoletions)
- If no release notes are available (NuGet-only release), rely entirely on the diff output + dotnet build CS0618 check
