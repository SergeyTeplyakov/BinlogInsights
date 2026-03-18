<#
.SYNOPSIS
    Builds and installs the binlog-insights-mcp dotnet tool globally.

.DESCRIPTION
    Uses Nerdbank.GitVersioning to compute a unique version per commit, packs the
    tool, kills any running instances, and installs the freshly-built package.

    The NBGV-computed version (e.g. 0.3.1-alpha-g860a5c547e) is unique per
    commit, which eliminates NuGet-cache staleness problems.

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
$projectPath = Join-Path $srcDir 'BinlogInsights.Mcp' 'BinlogInsights.Mcp.csproj'
$artifactsDir = Join-Path $repoRoot 'artifacts'
$toolName = 'BinlogInsights.Mcp'

Write-Host "=== BinlogInsights MCP Installer ===" -ForegroundColor Cyan

# --- Resolve version via NBGV ---
try {
    Push-Location $repoRoot
    $nbgvVersion = & nbgv get-version -v NuGetPackageVersion 2>$null
} catch {
    Write-Error "nbgv not found. Install it with: dotnet tool install -g nbgv"
    exit 1
} finally {
    Pop-Location
}
if ($LASTEXITCODE -ne 0 -or -not $nbgvVersion) {
    Write-Error "nbgv get-version failed. Is Nerdbank.GitVersioning installed? Run: dotnet tool install -g nbgv"
    exit 1
}
Write-Host "Version: $nbgvVersion" -ForegroundColor Cyan

# --- Clean artifacts ---
if (Test-Path $artifactsDir) {
    Remove-Item -Recurse -Force $artifactsDir
}

if ($Clean) {
    Write-Host "Clean build requested..." -ForegroundColor Yellow
    dotnet clean $projectPath -c Release --nologo -v q 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet clean failed with exit code $LASTEXITCODE"
        exit 1
    }
}

# --- Pack ---
Write-Host "Packing..." -ForegroundColor Yellow
dotnet pack $projectPath --output $artifactsDir -c Release --nologo /p:NoWarn=NU5105
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet pack failed with exit code $LASTEXITCODE"
    exit 1
}

# Verify expected .nupkg exists
$expectedNupkg = Join-Path $artifactsDir "$toolName.$nbgvVersion.nupkg"
if (-not (Test-Path $expectedNupkg)) {
    $actual = Get-ChildItem -Path $artifactsDir -Filter '*.nupkg' | Select-Object -ExpandProperty Name
    Write-Error "Expected $toolName.$nbgvVersion.nupkg but found: $($actual -join ', ')"
    exit 1
}
Write-Host "Built: $toolName.$nbgvVersion.nupkg" -ForegroundColor Green

# --- Kill running instances ---
$running = Get-Process -Name 'binlog-insights-mcp' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping $($running.Count) running instance(s)..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

# --- Uninstall previous version ---
$installed = dotnet tool list -g 2>$null | Select-String -SimpleMatch $toolName
if ($installed) {
    Write-Host "Uninstalling previous version..." -ForegroundColor Yellow
    dotnet tool uninstall -g $toolName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet tool uninstall failed with exit code $LASTEXITCODE"
        exit 1
    }
}

# --- Clear NuGet cache for this package ---
$globalPackagesDir = & dotnet nuget locals global-packages --list 2>$null
if ($globalPackagesDir -match 'global-packages:\s*(.+)') {
    $pkgCacheDir = Join-Path $Matches[1].Trim() $toolName.ToLowerInvariant()
    if (Test-Path $pkgCacheDir) {
        Write-Host "Clearing NuGet cache for $toolName..." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $pkgCacheDir
    }
}

# --- Install ---
Write-Host "Installing $toolName $nbgvVersion..." -ForegroundColor Yellow
dotnet tool install -g --add-source $artifactsDir --version $nbgvVersion $toolName
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet tool install failed with exit code $LASTEXITCODE"
    exit 1
}

# --- Verify ---
$reportedVersion = & binlog-insights-mcp --version 2>$null
Write-Host ""
Write-Host "Installed successfully!" -ForegroundColor Green
Write-Host "  Tool version:  $reportedVersion" -ForegroundColor Cyan
Write-Host "  NBGV version:  $nbgvVersion" -ForegroundColor Cyan

# --- MCP initialize probe ---
Write-Host ""
Write-Host "Verifying MCP server responds..." -ForegroundColor Yellow
$initMsg = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"verify","version":"1.0"}}}'
$psi = [System.Diagnostics.ProcessStartInfo]::new("binlog-insights-mcp")
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$proc = [System.Diagnostics.Process]::Start($psi)
try {
    $proc.StandardInput.WriteLine($initMsg)
    $proc.StandardInput.Flush()
    $readTask = $proc.StandardOutput.ReadLineAsync()
    if ($readTask.Wait(5000) -and $readTask.Result) {
        $json = $readTask.Result | ConvertFrom-Json -ErrorAction Stop
        $serverVersion = $json.result.serverInfo.version
        Write-Host "  MCP version:   $serverVersion" -ForegroundColor Cyan
    } else {
        Write-Warning "MCP server did not respond to initialize within 5 seconds."
    }
} catch {
    Write-Warning "MCP probe failed: $_"
} finally {
    if (-not $proc.HasExited) { $proc.Kill() }
    $proc.Dispose()
}
Write-Host ""
