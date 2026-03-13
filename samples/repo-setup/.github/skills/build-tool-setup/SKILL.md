---
name: build-tool-setup
description: Install or update the binlog-insights MCP server tool. Use when binlog-insights-mcp is not found, tool installation fails due to NuGet configuration, or the tool needs updating. Also triggers for dotnet tool install errors, NuGet source problems, and MCP server not starting.
metadata:
  traits: MSBuild|DotNet|NuGet|MCP|Build|Binlog
---

# Build Tool Setup

## Overview

Install or update the `BinlogInsights.Mcp` global tool. Handles the normal `dotnet tool` flow and provides fallbacks when NuGet configuration issues block installation.

## Workflow: Install

### Step 1 — Try normal install

```bash
dotnet tool install -g BinlogInsights.Mcp
```

If this succeeds, verify with:

```bash
binlog-insights-mcp --help
```

Done. Skip the remaining steps.

### Step 2 — Diagnose NuGet issues

If Step 1 fails (common in corporate environments with restricted NuGet feeds), check the NuGet configuration:

```bash
dotnet nuget list source
```

Common problems:
- **nuget.org not listed or disabled** — the tool is published on nuget.org
- **Authenticated feed requires credentials** — may block fallthrough to nuget.org
- **Package source mapping** excludes nuget.org for this package

### Step 3 — Install with explicit source

Try installing with an explicit nuget.org source to bypass local NuGet config:

```bash
dotnet tool install -g BinlogInsights.Mcp --add-source https://api.nuget.org/v3/index.json
```

### Step 4 — Manual download fallback

If all `dotnet tool install` attempts fail:

1. Check the latest version at: https://www.nuget.org/packages/BinlogInsights.Mcp
2. Download the `.nupkg` directly:
   ```bash
   # Replace {version} with the latest version from NuGet (e.g., 0.1.0)
   Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/BinlogInsights.Mcp/{version}" -OutFile "BinlogInsights.Mcp.nupkg"
   ```
3. Install from the local file:
   ```bash
   dotnet tool install -g BinlogInsights.Mcp --add-source .
   ```
4. Clean up the downloaded file after installation.

```
Task Progress:
- [ ] Step 1: dotnet tool install -g BinlogInsights.Mcp
- [ ] Step 2: Diagnose NuGet issues (if Step 1 failed)
- [ ] Step 3: Install with --add-source nuget.org (if Step 2 identified feed issues)
- [ ] Step 4: Manual download from nuget.org (last resort)
```

## Workflow: Update

### Step 1 — Try normal update

```bash
dotnet tool update -g BinlogInsights.Mcp
```

If this succeeds, done.

### Step 2 — Update with explicit source

If the update fails due to NuGet configuration:

```bash
dotnet tool update -g BinlogInsights.Mcp --add-source https://api.nuget.org/v3/index.json
```

### Step 3 — Uninstall and reinstall

If update still fails:

```bash
dotnet tool uninstall -g BinlogInsights.Mcp
```

Then follow the Install workflow above (Steps 1–4).

### Step 4 — Check latest version on NuGet

If you need to verify what the latest version is:

- NuGet page: https://www.nuget.org/packages/BinlogInsights.Mcp
- Check currently installed version: `dotnet tool list -g | Select-String BinlogInsights`

## Workflow: Verify Installation

After install or update, confirm everything works:

1. Check the tool is installed:
   ```bash
   dotnet tool list -g | Select-String BinlogInsights
   ```
2. Check the tool runs:
   ```bash
   binlog-insights-mcp --help
   ```
3. Check VS Code MCP config exists (`.vscode/mcp.json` should have the `binlog-insights` server entry)

## Troubleshooting

- **"Tool 'binloginsights.mcp' is already installed"** — use `dotnet tool update` instead of `install`
- **"Unable to resolve package"** — NuGet source issue, use `--add-source https://api.nuget.org/v3/index.json`
- **"Permission denied" on install path** — the default global tools path is `~/.dotnet/tools` — check it's writable and on PATH
- **MCP server doesn't start in VS Code** — verify `.vscode/mcp.json` has the correct config and restart VS Code
- **Old version after update** — check `dotnet tool list -g` to confirm, try uninstall + reinstall
