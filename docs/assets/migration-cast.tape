# vhs script for the maf-autopilot migration demo.
#
# Companion to install-cast.tape. While that cast shows install + audit,
# THIS cast shows the auto-fix loop: a broken codebase migrates to
# grade A in ~30 seconds, deterministically.
#
# Render locally:
#   bash scripts/make-cast.sh           # produces docs/assets/migration-cast.gif
#   pwsh scripts/make-cast.ps1
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

# ───────── beat 1: arrive at a broken sample ─────────
Type "# A real customer codebase, pinned to MAF 1.3.0 with 10 anti-patterns"
Enter
Sleep 1s

Type "cd samples/maf-1.3-sample"
Sleep 200ms
Enter
Sleep 400ms

# ───────── beat 2: prove it's broken ─────────
Type "dotnet build  # 1 CS0618 + 3 MAF001 warnings — analyzer + compiler agree"
Sleep 200ms
Enter
Sleep 4s

# ───────── beat 3: get the diagnosis ─────────
Type "maf-autopilot doctor .  # full health report"
Sleep 200ms
Enter
Sleep 5s

# ───────── beat 4: fix everything fixable ─────────
Type "# One command. Every mechanical rule. Deterministic — no LLM."
Enter
Sleep 800ms

Type "maf-autopilot autofix-all ."
Sleep 200ms
Enter
Sleep 4s

# ───────── beat 5: prove it's fixed ─────────
Type "maf-autopilot doctor .  # re-audit"
Sleep 200ms
Enter
Sleep 4s

# ───────── beat 6: clean build ─────────
Type "dotnet build  # 0 warnings, 0 errors"
Sleep 200ms
Enter
Sleep 3s

# ───────── beat 7: punchline ─────────
Type "# 🟢 Grade A. Migrated in ~30 seconds. Deterministically."
Enter
Sleep 2500ms
