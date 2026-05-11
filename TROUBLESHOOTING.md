# Troubleshooting maf-autopilot

If you hit something that isn't here, please open an issue with the failing command and its output — the troubleshooting catalog grows from real reports.

## Installation

### `dotnet tool install --global maf-autopilot` fails with "no compatible packages"

All published versions are currently prereleases (`1.3.0-alpha-1` … `1.3.0-alpha-3`). NuGet's tool installer ignores prereleases by default. Use:

```bash
dotnet tool install --global maf-autopilot --prerelease
```

Once a stable `1.3.0` ships (plan row #50), the plain command will work.

### `dotnet tool install` succeeds but `maf-autopilot` is not on PATH

`dotnet` installs global tools to `~/.dotnet/tools` (Linux/macOS) or `%USERPROFILE%\.dotnet\tools` (Windows). Add that directory to `PATH`:

```bash
# Linux/macOS — add to ~/.bashrc or ~/.zshrc
export PATH="$PATH:$HOME/.dotnet/tools"

# Windows — PowerShell, persists across sessions
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";$env:USERPROFILE\.dotnet\tools", "User")
```

### Update or downgrade

```bash
dotnet tool update --global maf-autopilot --prerelease
dotnet tool uninstall --global maf-autopilot
```

## `maf-autopilot init`

### "⚠ .vscode/mcp.json exists but could not be parsed as JSON"

Your existing `.vscode/mcp.json` has a syntax error (or trailing comments, which are not valid JSON). `init` has backed it up to `.vscode/mcp.json.bak.<UTC-timestamp>`. Inspect the backup, fix the JSON manually, then re-run `maf-autopilot init`.

### "ℹ .github/copilot-instructions.md already exists — not overwriting"

`init` won't overwrite an existing file by design — you may have hand-edited it. To regenerate from scratch: delete the file and re-run `init`.

### `init` leaves stale `.bak.*` files

Currently `init` does not clean its own backups. Safe to delete them yourself once you've recovered any needed content.

### `mcp.json` had JSON-with-comments — was it backed up?

VS Code officially treats `mcp.json` as JSONC: `//` line comments, `/* */` block comments, and trailing commas are valid. As of the post-2026-05-11 build, `maf-autopilot init` uses lenient JSONC parsing — your comment-bearing file is preserved untouched, no `.bak` is created, and the `maf-autopilot` server entry is merged in. If you see a `.bak.*` despite using only valid JSONC, you're on an older alpha. Update with `dotnet tool update --global maf-autopilot --prerelease`.

## MCP server not visible in Copilot Chat

### "Server: maf-autopilot is not connected"

1. Open the **Output** panel → channel **MCP**. If startup failed, the stack trace is there.
2. Confirm `.vscode/mcp.json` lists `maf-autopilot` under `servers`. Run `maf-autopilot init` if not.
3. Confirm the command resolves: in a terminal, run `maf-autopilot --version` (or just `maf-autopilot` and Ctrl-C). If "command not found," go back to the PATH section above.
4. Reload VS Code (`Developer: Reload Window`). MCP servers are started on workspace load.

### Tools appear but resources don't (or vice versa)

Restart VS Code. The MCP host caches resource/tool descriptors aggressively; a stale cache survives the server process being restarted. A full window reload clears it.

### Tool name confusion: `maf_api_safety` vs `MafApiSafety`

The SDK emits PascalCase from C# method names. Some older doc snippets used snake_case (`maf_api_safety`). Both refer to the same tool. The PascalCase form is what the model sees and invokes — use it when writing prompts.

## `dotnet-inspect` issues

### `dnx dotnet-inspect@0.7.8 -- diff` fails with NuGet rate limit / 429

NuGet's flat-container API rate-limits aggressive callers. Retry after 60s. If it persists, `dnx` caches packages locally — clear the cache:

```bash
# Linux/macOS
rm -rf ~/.dotnet/toolResolverCache ~/.nuget/dnx-cache
# Windows
rmdir /S /Q %USERPROFILE%\.dotnet\toolResolverCache %USERPROFILE%\.nuget\dnx-cache
```

### `dotnet-inspect` doesn't show `[Obsolete]` overloads

**Pin to v0.7.8 or later.** v0.7.8 ([release notes](https://github.com/richlander/dotnet-inspect/releases/tag/v0.7.8)) closed [issue #316](https://github.com/richlander/dotnet-inspect/issues/316). Earlier versions miss obsoletions at the overload level. Check your version:

```bash
dnx dotnet-inspect --version
```

If you're using `dnx dotnet-inspect` (no version pin), it picks up the latest — usually fine, but pin to `@0.7.8` for reproducible CI.

### The compiler reports CS0618 but `dotnet-inspect` says the API is fine

This is expected and *not* a bug in either tool. Three cases where only the compiler sees obsoletion:

1. **Transitive** — your API calls something that calls something that's obsolete.
2. **Overload-resolution** — your literal types happened to bind to a more-specific, obsoleted overload.
3. **Project-local** — your team marked an internal type `[Obsolete]`. Static inspection of the NuGet package can't see this.

Use `cs0618-hunter` (the compiler-based skill) for runtime ground-truth. Use `dotnet-inspect` for pre-build static checks against published packages.

## Migration loop

### `@maf-migration` agent appears to hang on a task

Three usual causes:
1. **`dotnet build` is itself hung** (e.g., MSBuild lock contention with a running VS Code Build task). Open a terminal and try `dotnet build` yourself — if it hangs, the agent is correctly stuck waiting.
2. **A task hit a real blocker the agent doesn't know how to resolve.** Read the latest message in the chat; the agent will usually report what it tried. Override by editing `src/docs/migration-plan.md` (mark the task ❌ and add a note).
3. **You've exceeded a chat-session token limit.** Open a fresh chat — the agent reads the plan from disk on every turn, so it picks up where you left off.

### "Task marked verified but the build is still failing"

The agent has a strict golden rule: never mark verified without a passing build. If you see this, something edited the plan directly. Re-run `dotnet build`, and either fix the underlying problem or revert the verification mark.

### `dotnet-inspect` is not installed in the migration loop

The migration agent expects `dnx` to be available. Install it once globally:

```bash
dotnet tool install --global dnx
```

`dnx` auto-resolves `dotnet-inspect@0.7.8` on first use.

## Release watcher (GitHub Actions)

### Workflow runs but doesn't open a PR

1. The MAF version on NuGet hasn't moved — the watcher exits cleanly when `.maf-version` matches the latest NuGet release. Check the job log's "check-for-new-maf-release" step.
2. `continue-on-error: true` on the diff steps masks real failures — if you suspect a silent failure, manually re-run the workflow and inspect each step's logs.
3. The watcher writes to a branch `chore/maf-<new>-update`. If that branch already exists from a prior run, the `peter-evans/create-pull-request@v6` action will update it rather than open a new PR.

### "Release notes not available" in the PR

The watcher used to query `microsoft/agents` for release notes — wrong repo. The correct repo is `microsoft/agent-framework`. Plan row #46 fixes this.

## CI

### Tests fail locally but pass in CI (or vice versa)

Check the test SDK version drift — `mcp/maf-autopilot.Tests/maf-autopilot.Tests.csproj` pins specific versions for `xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector`. If `dotnet restore` resolves different versions locally, your `nuget.config` is overriding.

### The test suite (~365 tests as of 2026-05-12) times out on Windows

xUnit's parallel runner respects logical-CPU count. On constrained machines, force serial:

```bash
dotnet test mcp/maf-autopilot.Tests/maf-autopilot.Tests.csproj -- xunit.parallelizeAssembly=false xunit.parallelizeTestCollections=false
```
