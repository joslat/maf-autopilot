# vhs script for the maf-autopilot install + audit demo.
#
# Render locally:
#   bash scripts/make-cast.sh           # produces docs/assets/install-cast.gif
#   OR
#   pwsh scripts/make-cast.ps1
#
# Render manually:
#   vhs docs/assets/install-cast.tape
#
# Source: https://github.com/charmbracelet/vhs

Output docs/assets/install-cast.gif

Set FontSize 16
Set Width 1200
Set Height 720
Set Theme "Dracula"
Set Padding 24
Set TypingSpeed 50ms
Set Shell "bash"

# ───────── beat 1: install ─────────
Type "# Install maf-autopilot as a global .NET tool"
Enter
Sleep 800ms

Type "dotnet tool install -g maf-doctor --prerelease"
Sleep 300ms
Enter
Sleep 3s

# ───────── beat 2: version check ─────────
Type "maf-doctor --version"
Sleep 200ms
Enter
Sleep 1500ms

# ───────── beat 3: audit a real codebase ─────────
Type "# Audit ANY MAF codebase — grade A through F + top fixes"
Enter
Sleep 800ms

Type "cd samples/maf-1.3-sample"
Sleep 200ms
Enter
Sleep 400ms

Type "maf-doctor doctor ."
Sleep 200ms
Enter
Sleep 5s

# ───────── beat 4: punchline ─────────
Type "# 🔴 Grade F — toolkit found every deliberate anti-pattern."
Enter
Sleep 800ms

Type "# 10 anti-pattern errors + 3 silent-starvation risks in under 1 sec."
Enter
Sleep 2500ms
