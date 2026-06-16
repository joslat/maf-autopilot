# `docs/assets/` — the README demo casts

This folder holds the two animated terminal demos shown in the top-level `README.md`:

| Source (`.tape`) | Renders to | Shows |
|---|---|---|
| `install-cast.tape` | `install-cast.gif` | Install the tool + `maf-doctor doctor .` → grade **F** + top fixes |
| `migration-cast.tape` | `migration-cast.gif` | `maf-doctor autofix-all .` → grade **F → A**, deterministic |

## What the `.tape` files are

They're **[VHS](https://github.com/charmbracelet/vhs)** scripts (by Charm). VHS reads a `.tape` file — a list of `Type`, `Enter`, `Sleep`, `Set` directives — drives a **headless terminal**, runs the commands for real, and records the session to a `.gif`. So the casts aren't hand-made screen recordings: they're reproducible scripts you re-run whenever the CLI output changes.

The `.gif` outputs are **not committed** — they're build artifacts you render on demand (which is why the README's `<img>` tags were removed until someone renders + commits them).

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
