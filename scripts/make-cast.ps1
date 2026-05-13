# Render BOTH README casts (install + migration) from their .tape scripts.
#
# Requires vhs (https://github.com/charmbracelet/vhs) installed locally.
#   Windows: winget install charmbracelet.vhs
#   macOS:   brew install vhs ffmpeg ttyd
#
# Usage (from repo root or anywhere):
#   pwsh scripts/make-cast.ps1                # render both (default)
#   pwsh scripts/make-cast.ps1 install        # render only install-cast
#   pwsh scripts/make-cast.ps1 migration      # render only migration-cast

param(
    [ValidateSet('install', 'migration', 'both')]
    [string]$Target = 'both'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot/..").Path

if (-not (Get-Command vhs -ErrorAction SilentlyContinue)) {
    Write-Host "vhs not on PATH." -ForegroundColor Red
    Write-Host
    Write-Host "Install:"
    Write-Host "  Windows: winget install charmbracelet.vhs"
    Write-Host "  macOS:   brew install vhs ffmpeg ttyd"
    exit 2
}

Push-Location $repoRoot
try {
    switch ($Target) {
        'install' {
            vhs (Join-Path $repoRoot 'docs/assets/install-cast.tape')
            Write-Host "✓ Rendered docs/assets/install-cast.gif" -ForegroundColor Green
        }
        'migration' {
            vhs (Join-Path $repoRoot 'docs/assets/migration-cast.tape')
            Write-Host "✓ Rendered docs/assets/migration-cast.gif" -ForegroundColor Green
        }
        'both' {
            vhs (Join-Path $repoRoot 'docs/assets/install-cast.tape')
            vhs (Join-Path $repoRoot 'docs/assets/migration-cast.tape')
            Write-Host
            Write-Host "✓ Rendered both casts:" -ForegroundColor Green
            Write-Host "    docs/assets/install-cast.gif"
            Write-Host "    docs/assets/migration-cast.gif"
        }
    }
}
finally {
    Pop-Location
}

Write-Host
Write-Host "Commit with: git add docs/assets/*.gif && git commit"
