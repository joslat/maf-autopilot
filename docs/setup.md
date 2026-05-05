# maf-autopilot — Setup Guide

> Last updated: May 5, 2026

This guide explains what you need to do — as a human — to use and maintain `maf-autopilot`. It covers three usage modes, repository setup, the CI workflow, and answers the "Copilot Coding Agent vs. shell scripts" architecture question.

---

## What's Already Done vs. What You Need to Do

The toolkit code is fully implemented through step #24. Here is the current state:

| Step | What | Status | Who does it |
|------|------|--------|-------------|
| #1–#20 | All agents, skills, guide, constraints | ✅ Done | — (shipped) |
| #21 | MCP server MVP (3 tools) | ✅ Done | — |
| #22 | MCP Resources (`maf://constraints`, etc.) | ✅ Done | — |
| #23 | MCP Prompts (`/maf-audit`, `/maf-migrate`, etc.) | ✅ Done | — |
| #24 | `maf-autopilot init` CLI command | ✅ Done | — |
| **#25** | **NuGet publish** | **❌ You must do this** | See [Section 3](#3-publishing-to-nugetorg-step-25) |
| #26–#29 | Sampling tools, full analysis tools, Docker | ❌ Future work | — |
| **#30** | **`registry.yaml` CI auto-update** | **❌ You must do this** | See [Section 4](#4-enabling-registry-auto-update-step-30) |
| **#31** | **Feedback issue tool** | **❌ Prerequisite: #25** | After #25 |
| #32 | Multi-version migration paths | ❌ Future work | — |

**Steps 22–24 are fully implemented and build-verified.** Steps 25 and beyond are not started because they require external setup (NuGet account, GitHub repo secrets, organizational Copilot access) that only you can provide.

---

## Three Usage Modes

### Mode A — VS Code Copilot Chat (no repo needed)

Use the agents, skills, and guide directly in VS Code without running the MCP server.

**Prerequisites:** VS Code + GitHub Copilot (any plan)

**Steps:**
1. Clone or fork this repo (or copy `.github/` into your project)
2. Open the folder in VS Code
3. In Copilot Chat, agent mode is automatically aware of `.github/agents/`, `.github/skills/`, and `.github/instructions/`
4. Use `@maf-migration` to start a migration, `@maf-auditor` to audit a codebase

**What you get:**
- All 11 skills loaded on demand
- Always-on constraint rules from `maf-constraints.instructions.md`
- The full 21-section migration guide navigated by `maf-migration-guide` skill
- No MCP server needed

---

### Mode B — MCP Server (local, development build)

Run the MCP server locally to expose `maf://` resources and `/maf-*` prompts in Copilot Chat.

**Prerequisites:** .NET 9 SDK, VS Code + GitHub Copilot

**Steps:**
1. Clone this repo
2. Verify `.vscode/mcp.json` already points to the local build:
   ```json
   "maf-autopilot": {
     "type": "stdio",
     "command": "dotnet",
     "args": ["run", "--project", "mcp/maf-autopilot/maf-autopilot.csproj", "--"]
   }
   ```
3. Open VS Code — the MCP server starts automatically when Copilot needs it
4. In Copilot Chat, reference `maf://constraints` or use `/maf-audit` slash command

**What you get additionally over Mode A:**
- `maf://constraints`, `maf://registry`, `maf://guide`, `maf://skills?name=*` as referenceable resources
- `/maf-audit`, `/maf-migrate`, `/maf-cs0618-hunt` as slash commands
- `maf_api_safety`, `maf_registry_lookup`, `maf_registry_list` tools

---

### Mode C — MCP Server (global tool, any project)

Install `maf-autopilot` as a global .NET tool so any project can run `maf-autopilot init` and use the MCP server without cloning this repo.

**Prerequisites:** .NET 9 SDK, `maf-autopilot` published to NuGet (#25 must be done first)

**Steps (after #25 is published):**
```bash
# Install once, globally
dotnet tool install -g maf-autopilot

# In any MAF project folder:
maf-autopilot init
```

`maf-autopilot init` will:
- Add the MCP server entry to `.vscode/mcp.json` (idempotent — safe to re-run)
- Write `.github/copilot-instructions.md` with the MAF hard constraint rules (skips if already exists)

**Then open VS Code** — the MCP server starts automatically.

---

## Creating the GitHub Repository

### Repository details

| Field | Value |
|-------|-------|
| **Name** | `maf-autopilot` |
| **Owner** | your GitHub username or org (e.g. `joslat`) |
| **Visibility** | **Private** (start here — you can make it public later) |
| **Description** | AI-powered toolkit for Microsoft Agent Framework (MAF) migrations — agents, skills, MCP server, and self-updating guide |
| **Tagline** (for topics) | `maf` `dotnet` `migration` `copilot` `mcp-server` `ai-agents` |

### Steps

**Option 1 — GitHub CLI (fastest):**
```bash
cd C:\git\joslat\maf-autopilot

# Create private repo on GitHub and push
gh repo create joslat/maf-autopilot \
  --private \
  --description "AI-powered toolkit for Microsoft Agent Framework (MAF) migrations — agents, skills, MCP server, and self-updating guide" \
  --source . \
  --remote origin \
  --push
```

**Option 2 — GitHub web UI + manual push:**
1. Go to [github.com/new](https://github.com/new)
2. Repository name: `maf-autopilot`
3. Description: `AI-powered toolkit for Microsoft Agent Framework (MAF) migrations — agents, skills, MCP server, and self-updating guide`
4. Visibility: **Private**
5. Leave "Initialize this repository" unchecked (you already have local files)
6. Click **Create repository**
7. In your local terminal:
   ```bash
   git remote add origin https://github.com/joslat/maf-autopilot.git
   git branch -M main
   git push -u origin main
   ```

### After the repo is created

- **Add topics** (GitHub repo page → gear icon next to About): `maf`, `dotnet`, `migration`, `copilot`, `mcp-server`, `ai-agents`
- **Enable Actions:** Settings → Actions → General → Allow all actions → Save
- **Verify the workflow is visible:** Actions tab → you should see "MAF Release Watcher" listed (no runs yet — that's expected)
- **Add `NUGET_API_KEY` secret** when ready to publish (see [Publishing to NuGet.org](#publishing-to-nugetorg-step-25))

> **When to go public:** Once `maf-autopilot init` is end-to-end testable (after #25 NuGet publish), flip visibility to public under Settings → Danger Zone → Change repository visibility.

---

## Setting Up the GitHub Repository

To use the release watcher workflow and eventually publish the NuGet package, you need a GitHub repository.

### Option A — Use this repo directly

If you are **joslat** (the repo owner): the repository is `github.com/joslat/maf-autopilot`. The workflow is already present. Skip to [adding secrets](#adding-github-secrets).

### Option B — Fork for your own use

1. Fork `joslat/maf-autopilot` on GitHub
2. In your fork: **Settings → Actions → General → Allow all actions** (ensure Actions are enabled)
3. Change the `.maf-version` file if you want to track a different baseline version

### Option C — New repo (bring your own guide)

1. `gh repo create your-org/maf-autopilot --private` (or public)
2. Push this codebase: `git remote add origin <url> && git push -u origin main`
3. Enable Actions (Settings → Actions → General)

---

### Adding GitHub Secrets

The release watcher workflow needs one secret **if you want to publish the NuGet package** (step #25):

| Secret | Where to get it | Required for |
|--------|-----------------|-------------|
| `NUGET_API_KEY` | [nuget.org → API Keys](https://www.nuget.org/account/apikeys) | NuGet publish (#25) |

**How to add:**
1. GitHub repo → **Settings → Secrets and variables → Actions → New repository secret**
2. Name: `NUGET_API_KEY`
3. Value: your NuGet.org API key (scope: push, package: `maf-autopilot`, or all packages)

> The `GITHUB_TOKEN` secret is automatically provided by GitHub Actions — no setup needed.

---

### Enabling the Workflow

The file `.github/workflows/maf-release-watcher.yml` is already in the repo. GitHub Actions will pick it up automatically once the repo is on GitHub.

**What the workflow does:**

1. **Runs weekly** (Monday 9 AM UTC) — or manually via **Actions → MAF Release Watcher → Run workflow**
2. **`check-for-new-maf-release`** — queries NuGet for the latest `Microsoft.Agents.AI` version, compares to `.maf-version`
3. If a new version is found: **`analyze-and-update`** — runs `dotnet-inspect diff`, updates `compatibility-matrix.md`, appends a draft section to the migration guide, creates a PR for human review
4. **`publish-mcp-release`** (optional, manual trigger only) — `dotnet pack` + `dotnet nuget push` to NuGet.org

**Manual trigger with version override:**
- Go to Actions → MAF Release Watcher → Run workflow
- Optionally specify a version (e.g., `1.4.0`) to force processing a specific release
- Set `publish: true` to also push the NuGet package (requires `NUGET_API_KEY` secret)

---

## Publishing to NuGet.org (Step #25)

This is the next required user action. Once done, anyone can `dotnet tool install -g maf-autopilot`.

### Prerequisites
- NuGet.org account (free at [nuget.org](https://www.nuget.org/users/account/LogOn))
- `NUGET_API_KEY` secret added to GitHub repo (see above)

### Steps

**Option 1 — Via GitHub Actions (recommended for ongoing releases):**
1. Add `NUGET_API_KEY` to GitHub secrets
2. Go to Actions → MAF Release Watcher → Run workflow
3. Set `publish: true`
4. Click **Run workflow**

**Option 2 — Manual first publish (bootstraps NuGet listing):**
```powershell
cd mcp/maf-autopilot
dotnet pack -c Release -o ./nupkg
dotnet nuget push ./nupkg/maf-autopilot.*.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```

### After publishing
- The package appears on NuGet.org within ~15 minutes
- Users can then `dotnet tool install -g maf-autopilot` and `maf-autopilot init`
- The workflow's `publish-mcp-release` job handles all future releases automatically

---

## Enabling Registry Auto-Update in CI (Step #30)

The release watcher currently leaves `registry.yaml` as a manual step — human must review the diff and write new CS0618 entries. Step #30 closes this gap with a Copilot Coding Agent step.

This is discussed below in the architecture comparison.

---

## Architecture: Copilot Coding Agent vs. Shell Scripts

The current workflow (`analyze-and-update` job) uses **pure shell scripts** (bash + python3 + dnx). An alternative is to replace some or all of the scripted logic with a **Copilot Coding Agent** step (`uses: github/copilot-coding-agent@v1`).

Here is an honest comparison:

### Current approach — Shell Scripts

```yaml
- name: Generate draft guide section
  run: |
    printf '\n\n---\n\n' >> guides/maf-1.3.0-migration-guide.md
    cat >> guides/maf-1.3.0-migration-guide.md << EOF
    ## Section AUTO — ...
    EOF
```

**Pros:**
- No Copilot Enterprise required — works with any GitHub Actions plan
- Fast (seconds, not minutes)
- Deterministic — same input always produces same output
- No token cost
- Draft sections are clearly templated — reviewers know exactly what is auto-generated vs. human-written

**Cons:**
- `registry.yaml` entries cannot be auto-written without LLM reasoning — pure scripts don't know what `AddFanInBarrierEdge` changing signatures means semantically
- Guide section quality is template-based — placeholder text, not analysis
- Human must still write the real content (but the PR creates a structured checkpoint)

---

### Alternative — Copilot Coding Agent

```yaml
- name: Update registry and guide with Copilot
  uses: github/copilot-coding-agent@v1
  with:
    prompt: |
      Read diff-core.txt. For each removed or renamed member that relates to
      Microsoft.Agents.AI workflows, add an entry to .github/skills/obsolete-api-registry/registry.yaml
      following the existing format. ...
```

**Pros:**
- Can write **meaningful** `registry.yaml` entries (semantic understanding of what changed)
- Can write **meaningful** guide section content (not just placeholders)
- Can reason about breaking changes in natural language

**Cons:**
- Requires **GitHub Copilot Enterprise** or Teams with Coding Agent enabled — not available on free/Pro plans
- Slower — agent may take 5–30 minutes
- Non-deterministic — output quality varies; human review is even more critical
- Token costs apply
- Agent may hallucinate API names — the PR review step is not optional

---

### Recommendation: Hybrid approach (the current design)

**Use shell scripts for everything that is deterministic:**
- NuGet version check (pure HTTP)
- `dotnet-inspect diff` execution (CLI invocation)
- Compatibility matrix row insertion (regex replacement)
- `.maf-version` update (string write)
- PR creation with templated checklist

**Use Copilot Coding Agent ONLY for `registry.yaml` semantic update (step #30):**
- The diff is already available as an artifact (`diff-core.txt`)
- The agent prompt can be tightly constrained: "read this diff, find CS0618-risk patterns, add entries matching this YAML schema"
- The PR review checklist already asks reviewers to verify registry entries — the agent output gets the same human check

**Why this is better than all-scripts OR all-agent:**

| Task | Scripts | Agent | Best choice |
|------|---------|-------|-------------|
| Version check | ✅ Perfect | Overkill | Scripts |
| Diff generation | ✅ Perfect | Can't (CLI) | Scripts |
| Compat matrix row | ✅ Good enough | Unnecessary | Scripts |
| Guide section template | ✅ Placeholder is fine | Better prose | Agent (optional) |
| `registry.yaml` entries | ❌ Cannot do semantically | ✅ Ideal fit | **Agent (recommended)** |
| PR creation | ✅ Perfect | Not needed | Scripts |

**Bottom line:** The current all-scripts approach was the right call to get to a working, low-dependency workflow. Step #30 adds the Copilot Agent only where it provides irreplaceable value (semantic YAML generation). You do NOT need to rewrite the whole workflow.

---

## Prerequisite Chain Summary

```
You want: dotnet tool install -g maf-autopilot
  └─ Requires: #25 NuGet publish
       └─ Requires: NuGet.org account + API key
            └─ Action: Add NUGET_API_KEY to GitHub repo secrets

You want: registry.yaml auto-update in CI
  └─ Requires: #30 Copilot Agent step in workflow
       └─ Requires: GitHub Copilot Enterprise or Teams (for copilot-coding-agent@v1)
            └─ Alternative: Use OpenAI/Azure OpenAI API key instead

You want: maf_open_feedback_issue tool
  └─ Requires: #31 tool implementation
       └─ Prerequisite: #25 NuGet publish (so binary users can trigger it)
```

**No prerequisites** (works right now):
- Mode A (VS Code Copilot Chat agents/skills)
- Mode B (local MCP server via `dotnet run`)
- The `maf-release-watcher.yml` workflow itself (just needs the repo on GitHub)

---

## Quick Reference: What Runs Where

| Component | Runs where | Needs |
|-----------|-----------|-------|
| Agents / skills / instructions | VS Code Copilot Chat (host-side) | VS Code + Copilot |
| MCP server (local dev) | Your machine | .NET 9 SDK |
| MCP server (global tool) | Your machine | `dotnet tool install -g maf-autopilot` (#25) |
| Release watcher workflow | GitHub Actions | GitHub repo |
| NuGet publish | GitHub Actions | `NUGET_API_KEY` secret |
| Registry auto-update | GitHub Actions | Copilot Enterprise or LLM API key (#30) |
