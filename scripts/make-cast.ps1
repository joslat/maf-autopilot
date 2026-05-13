# Render docs/assets/install-cast.gif from the vhs .tape script.
#
# Requires vhs (https://github.com/charmbracelet/vhs) installed locally.
#   Windows: winget install charmbracelet.vhs
#   macOS:   brew install vhs ffmpeg ttyd
#
# Usage (from repo root or anywhere):
#   pwsh scripts/make-cast.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$tape = Join-Path $repoRoot 'docs/assets/install-cast.tape'

if (-not (Get-Command vhs -ErrorAction SilentlyContinue)) {
    Write-Host "vhs not on PATH." -ForegroundColor Red
    Write-Host
    Write-Host "Install:"
    Write-Host "  Windows: winget install charmbracelet.vhs"
    Write-Host "  macOS:   brew install vhs ffmpeg ttyd"
    exit 2
}

if (-not (Test-Path $tape)) {
    Write-Host "Tape file not found: $tape" -ForegroundColor Red
    exit 2
}

Push-Location $repoRoot
try {
    vhs $tape
}
finally {
    Pop-Location
}

Write-Host
Write-Host "✓ Rendered docs/assets/install-cast.gif" -ForegroundColor Green
Write-Host "  Commit with:  git add docs/assets/install-cast.gif && git commit"
