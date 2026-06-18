# `docs/assets/` — the README demo casts

This folder holds the terminal demo casts for the project:

| Source (`.tape`) | Renders to | Shows |
|---|---|---|
| `install-cast.tape` | `install-cast.gif` (committed, shown in the top-level `README.md`) | Install the tool + `maf-doctor doctor .` → grade **F** + top fixes |
| `migration-cast.tape` | _(not rendered/committed yet)_ | `maf-doctor autofix-all .` → deterministic Roslyn rewriters clear the **mechanical** anti-patterns; the **semantic** fan-out fixes (`ValueTask` → `Task<T>`) are left for the `@maf-migration` agent. It does **not** reach grade A on its own — that needs the agent pass. |

## What the `.tape` files are

They're **[VHS](https://github.com/charmbracelet/vhs)** scripts (by Charm). VHS reads a `.tape` file — a list of `Type`, `Enter`, `Sleep`, `Set` directives — drives a **headless terminal**, runs the commands for real, and records the session to a `.gif`. So the casts aren't hand-made screen recordings: they're reproducible scripts you re-run whenever the CLI output changes.

`install-cast.gif` is committed and embedded in the top-level README. `migration-cast.gif` is **not** rendered/committed yet — the agent-driven migration it should show needs a Copilot/Claude session, so it's deferred. Render any cast on demand from its `.tape`.

## How to render them ("auto-run")

**Prerequisites:** `vhs` (which pulls in `ttyd` + `ffmpeg`).

```bash
# macOS
brew install vhs ffmpeg ttyd
# Windows
winget install charmbracelet.vhs
# Linux → https://github.com/charmbracelet/vhs#installation
```

**Render** (the scripts resolve the repo root, so run from anywhere):

```bash
bash scripts/make-cast.sh             # render BOTH casts (default)
bash scripts/make-cast.sh install     # only install-cast.gif
bash scripts/make-cast.sh migration   # only migration-cast.gif
```

```powershell
pwsh scripts/make-cast.ps1            # PowerShell equivalent
```

Or invoke VHS directly on one tape:

```bash
vhs docs/assets/install-cast.tape     # → docs/assets/install-cast.gif
```

The script aborts with install hints if `vhs` isn't on `PATH`.

## After rendering

The GIFs land in `docs/assets/`. To show them in the README, commit them and add the `<img>` tags back:

```bash
git add docs/assets/*.gif && git commit -m "docs: render README casts"
```

## Editing a cast

Edit the `.tape` (typed commands, timings, theme via `Set …`) and re-render. The tapes run **real commands** against the bundled `samples/maf-1.3-sample/`, so after a recording reset the sample with `git restore samples/maf-1.3-sample/`. Keep the demo honest — if the CLI output changes, re-render rather than editing the GIF.
