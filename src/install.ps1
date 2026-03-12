<#
.SYNOPSIS
    Builds and installs (or updates) the binlog-insights dotnet tool globally.

.DESCRIPTION
    Packs the BinlogInsights project into a NuGet package and installs it as a
    global dotnet tool. If already installed, it updates to the freshly-built version.

    After running this script, 'binlog-insights' will be available on your PATH.

.EXAMPLE
    .\install.ps1

.EXAMPLE
    # Force a clean rebuild
    .\install.ps1 -Clean
#>
[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$srcDir = $PSScriptRoot
$repoRoot = Split-Path $srcDir -Parent
$projectPath = Join-Path $srcDir 'BinlogInsights' 'BinlogInsights.csproj'
$artifactsDir = Join-Path $repoRoot 'artifacts'

Write-Host "=== BinlogInsights Installer ===" -ForegroundColor Cyan

# Clean artifacts if requested
if ($Clean -and (Test-Path $artifactsDir)) {
    Write-Host "Cleaning artifacts..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $artifactsDir
}

# Pack
Write-Host "Packing..." -ForegroundColor Yellow
dotnet pack $projectPath --output $artifactsDir -c Release /p:NoWarn=NU5105
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet pack failed with exit code $LASTEXITCODE"
    exit 1
}

# Find the .nupkg
$nupkg = Get-ChildItem -Path $artifactsDir -Filter '*.nupkg' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $nupkg) {
    Write-Error "No .nupkg found in $artifactsDir"
    exit 1
}
Write-Host "Built: $($nupkg.Name)" -ForegroundColor Green

# Install or update the global tool
Write-Host "Installing global tool..." -ForegroundColor Yellow

# Try update first (works if already installed); fall back to install
dotnet tool update --global --add-source $artifactsDir BinlogInsights 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet tool install --global --add-source $artifactsDir BinlogInsights
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet tool install failed with exit code $LASTEXITCODE"
        exit 1
    }
}

Write-Host ""
Write-Host "Done! 'binlog-insights' is now available on your PATH." -ForegroundColor Green
Write-Host "Try: binlog-insights --help" -ForegroundColor Cyan
