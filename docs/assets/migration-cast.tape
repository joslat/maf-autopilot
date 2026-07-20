# vhs script for the maf-doctor auto-fix demo.
#
# Honest companion to install-cast.tape. It shows the DETERMINISTIC auto-fix
# pass on a broken MAF 1.3.0 codebase — and is truthful that autofix clears the
# mechanical anti-patterns while the silent fan-out starvation bugs are left for
# the migration agent (flipping ValueTask -> Task<T> is a semantic change the
# rewriter won't guess). No `dotnet build` here: doctor/autofix are static
# Roslyn analysis, so the cast needs no package restore and stays fast.
#
# Render (Docker, no local vhs/ttyd) — see private/castforge/SKILL.md:
#   docker run --rm -v "$PWD:/vhs" -w /vhs vhs-dotnet docs/assets/migration-cast.tape
#
# Source: https://github.com/charmbracelet/vhs

Output docs/assets/migration-cast.gif

Set FontSize 15
Set Width 1280
Set Height 800
Set Theme "Dracula"
Set Padding 24
Set TypingSpeed 45ms
Set Shell "bash"

# ───────── beat 1: a broken MAF 1.3.0 codebase ─────────
Type "# A customer codebase pinned to MAF 1.3.0 — 10 anti-patterns, 3 of them silent"
Enter
Sleep 1s
Type "cd samples/maf-1.3-sample"
Sleep 200ms
Enter
Sleep 400ms

# ───────── beat 2: diagnose ─────────
Type "maf-doctor doctor ."
Sleep 200ms
Enter
Sleep 5s

# ───────── beat 3: the deterministic pass ─────────
Type "# Roslyn rewriters, no LLM — the deterministic mechanical fixes."
Enter
Sleep 700ms
Type "maf-doctor autofix-all . --apply"
Sleep 200ms
Enter
Sleep 4s

# ───────── beat 4: re-audit — honest about what remains ─────────
Type "maf-doctor doctor ."
Sleep 200ms
Enter
Sleep 5s

# ───────── beat 5: the honest punchline + agent handoff ─────────
Type "# 🟡 autofix cleared the mechanical anti-patterns (no LLM)."
Enter
Sleep 700ms
Type "# The 3 silent-starvation fan-out bugs remain on purpose: ValueTask -> Task<T>"
Enter
Sleep 500ms
Type "# is a semantic change autofix won't guess. Those go to @maf-migration."
Enter
Sleep 2800ms
