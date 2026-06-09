#!/usr/bin/env pwsh
# End-to-end test: spawn multiple MCP instances, then drive ONE of them via
# stdio JSON-RPC to list and stop them all (others + itself).

$ErrorActionPreference = 'Stop'

$dll = (Resolve-Path "src/BinlogInsights.Mcp/bin/Debug/net10.0/BinlogInsights.Mcp.dll").Path
Write-Host "Using DLL: $dll"

function Start-McpInstance {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = "`"$dll`""
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    return $p
}

# Capture stderr from driver in background so we can see diagnostics
function Start-StderrReader($proc, [string]$label) {
    $action = {
        param($sender, $e)
        if ($null -ne $e.Data) { [Console]::Error.WriteLine("[$($Event.MessageData)] $($e.Data)") }
    }
    Register-ObjectEvent -InputObject $proc -EventName ErrorDataReceived -MessageData $label -Action $action | Out-Null
    $proc.BeginErrorReadLine()
}

function Send-JsonRpc($proc, [string]$json) {
    $proc.StandardInput.WriteLine($json)
    $proc.StandardInput.Flush()
}

function Read-JsonRpc($proc, [int]$timeoutMs = 5000) {
    $task = $proc.StandardOutput.ReadLineAsync()
    if (-not $task.Wait($timeoutMs)) { throw "Timed out waiting for response" }
    return $task.Result
}

# 1) Spawn 3 instances total
Write-Host "`n--- Spawning 3 MCP instances ---"
$bg1 = Start-McpInstance
$bg2 = Start-McpInstance
$driver = Start-McpInstance   # one we will drive via JSON-RPC
$bgPids = @($bg1.Id, $bg2.Id)
Write-Host ("Background PIDs: {0}, Driver PID: {1}" -f ($bgPids -join ', '), $driver.Id)

Start-StderrReader $driver "driver"

Start-Sleep -Milliseconds 800  # let them initialize

try {
    # 2) MCP initialize handshake on driver
    $init = @{
        jsonrpc = "2.0"; id = 1; method = "initialize"
        params = @{
            protocolVersion = "2024-11-05"
            capabilities = @{}
            clientInfo = @{ name = "stop-all-test"; version = "0.0.0" }
        }
    } | ConvertTo-Json -Compress -Depth 6
    Send-JsonRpc $driver $init
    $resp = Read-JsonRpc $driver
    Write-Host "initialize -> $resp"

    Send-JsonRpc $driver (@{ jsonrpc = "2.0"; method = "notifications/initialized" } | ConvertTo-Json -Compress)

    # 3) Call list_mcp_instances
    $listReq = @{
        jsonrpc = "2.0"; id = 2; method = "tools/call"
        params = @{ name = "list_mcp_instances"; arguments = @{} }
    } | ConvertTo-Json -Compress -Depth 6
    Send-JsonRpc $driver $listReq
    $listResp = Read-JsonRpc $driver
    Write-Host "`nlist_mcp_instances ->`n$listResp"

    $parsed = $listResp | ConvertFrom-Json
    $textBlock = $parsed.result.content[0].text
    Write-Host "`nDiscovered instances payload:`n$textBlock"
    $instances = $textBlock | ConvertFrom-Json

    $currentPidFromDriver = ($instances | Where-Object { $_.isCurrent }).pid
    $otherPids = ($instances | Where-Object { -not $_.isCurrent }).pid
    Write-Host ("`nDriver self-reported PID: {0} (actual: {1})" -f $currentPidFromDriver, $driver.Id)
    Write-Host ("Other PIDs reported: {0}" -f ($otherPids -join ', '))

    Write-Host ("`nExpected bg PIDs: {0}; bg1 alive: {1}; bg2 alive: {2}" -f ($bgPids -join ', '), (-not $bg1.HasExited), (-not $bg2.HasExited))

    Write-Host "`nWMI dump for bg PIDs:"
    foreach ($bp in $bgPids) {
        $ci = Get-CimInstance Win32_Process -Filter ("ProcessId = {0}" -f $bp) -ErrorAction SilentlyContinue
        if ($ci) {
            Write-Host ("  PID {0} Name={1} CmdLine={2}" -f $ci.ProcessId, $ci.Name, $ci.CommandLine)
        } else {
            Write-Host ("  PID {0} not found via WMI" -f $bp)
        }
    }

    # 4) Stop all others first
    $reqId = 3
    foreach ($instancePid in $otherPids) {
        $stopReq = @{
            jsonrpc = "2.0"; id = $reqId; method = "tools/call"
            params = @{ name = "stop_instance"; arguments = @{ pid = [int]$instancePid } }
        } | ConvertTo-Json -Compress -Depth 6
        Send-JsonRpc $driver $stopReq
        $r = Read-JsonRpc $driver
        Write-Host "stop_instance(pid=$instancePid) -> $r"
        $reqId++
    }

    # 5) Stop the driver itself last (graceful self-shutdown)
    $stopSelf = @{
        jsonrpc = "2.0"; id = $reqId; method = "tools/call"
        params = @{ name = "stop_instance"; arguments = @{ pid = [int]$currentPidFromDriver } }
    } | ConvertTo-Json -Compress -Depth 6
    Send-JsonRpc $driver $stopSelf
    try {
        $r = Read-JsonRpc $driver 3000
        Write-Host "stop_instance(self pid=$currentPidFromDriver) -> $r"
    } catch {
        Write-Host "stop_instance(self) returned no response (driver already exiting)."
    }

    # 6) Wait for processes to exit
    Start-Sleep -Milliseconds 1500

    $stillAlive = @()
    foreach ($p in @($bg1, $bg2, $driver)) {
        if (-not $p.HasExited) { $stillAlive += $p.Id }
    }

    Write-Host "`n--- Result ---"
    if ($stillAlive.Count -eq 0) {
        Write-Host "SUCCESS: all 3 instances stopped via a single MCP session."
        exit 0
    } else {
        Write-Host ("FAILURE: still alive: {0}" -f ($stillAlive -join ', '))
        exit 1
    }
}
finally {
    foreach ($p in @($bg1, $bg2, $driver)) {
        try { if (-not $p.HasExited) { $p.Kill() } } catch {}
    }
}
