# BinlogInsights

MSBuild binary log (`.binlog`) analysis tools — a CLI and an MCP server for AI-assisted build investigation.

## Why?

MSBuild binary logs capture everything about a build: properties, items, imports, resolved packages, compiler invocations, targets, tasks, and timing.
But navigating a binlog is tedious — they can be huge, deeply nested, and full of irrelevant noise.

**BinlogInsights** exposes structured queries over binlogs through two interfaces:

| Interface | Use case |
|---|---|
| **CLI** (`binlog-insights`) | Quick terminal investigation, scripting, CI pipelines |
| **MCP server** (`binlog-insights-mcp`) | AI-assisted investigation in VS Code / Copilot Chat |

The MCP server is particularly powerful: point Copilot at a failing binlog and let it drive the investigation — drilling from overview → errors → properties → imports → NuGet diagnostics automatically, following the same workflow a build engineer would.

## Features

### 26 MCP Tools / 13 CLI Commands

**Build investigation:**
- `overview` — Build status, duration, project count, top-level results
- `errors` / `warnings` — Diagnostics with project, file, line info
- `properties` — MSBuild property values (with optional filter)
- `imports` — Full import chain for a project
- `items` — Item groups (PackageReference, Compile, etc.)
- `nuget` — NuGet restore diagnostics
- `compiler` — Full compiler command line (references, defines, flags)
- `search` — Free-text search across all build messages
- `projects` — Project list with status and duration
- `preprocess` — Effective project XML after all imports
- `compare` — Diff two binlogs: properties, packages, references

**Performance analysis (MCP only):**
- `expensive_projects` / `project_target_times` — Slowest projects and their target breakdown
- `expensive_targets` / `search_targets` / `project_targets` — Target-level timing
- `expensive_tasks` / `search_tasks` / `tasks_in_target` / `task_details` — Task-level timing
- `expensive_analyzers` — Roslyn analyzer performance

**Evaluation & files (MCP only):**
- `evaluations` / `evaluation_properties` / `evaluation_global_properties` — Property/item resolution phase
- `list_files` / `get_file` — Access source files embedded in the binlog

## Installation

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Install the MCP server (recommended)

```bash
dotnet tool install -g BinlogInsights.Mcp
```

### Install the CLI

```bash
dotnet tool install -g BinlogInsights
```

### Build from source

```bash
git clone https://github.com/SergeyTeplyakov/BinlogInsights.git
cd BinlogInsights/src
dotnet build
# Install the MCP server as a global tool:
./install.ps1
```

## Setup for VS Code / Copilot

Add the MCP server to your workspace. Create `.vscode/mcp.json`:

```json
{
  "servers": {
    "binlog-insights": {
      "type": "stdio",
      "command": "binlog-insights-mcp",
      "args": []
    }
  }
}
```

Once configured, Copilot Chat automatically gains access to all 26 binlog analysis tools.

## Usage

### Generate a binlog

Build your project with binary logging enabled:

```bash
dotnet build /bl:build.binlog
```

Or for MSBuild:

```bash
msbuild /bl:build.binlog
```

### CLI examples

```bash
# Build overview
binlog-insights overview build.binlog

# Show errors
binlog-insights errors build.binlog

# Check properties for a specific project
binlog-insights properties build.binlog --project MyApp --filter TargetFramework

# Search build messages
binlog-insights search build.binlog --query "PackageReference"

# Compare two builds
binlog-insights compare before.binlog after.binlog

# NuGet restore diagnostics
binlog-insights nuget build.binlog

# Compiler command line
binlog-insights compiler build.binlog --project MyApp
```

### AI-assisted investigation

With the MCP server configured, ask Copilot Chat:

> *"My build failed. Investigate build.binlog and tell me what went wrong."*

Copilot will automatically:
1. Call `overview` to check build status
2. Call `errors` to see what failed
3. Drill deeper based on error type:
   - Missing types → `items` to check PackageReference
   - Property issues → `properties` to inspect values
   - Import problems → `imports` to trace the chain
   - NuGet failures → `nuget` for restore diagnostics
4. Use `search` for free-text investigation
5. Use `compare` to diff two builds

> *"Why is my build slow? Analyze build.binlog performance."*

Copilot will:
1. Call `expensive_projects` to find the slowest projects
2. Drill into targets with `project_target_times`
3. Check `expensive_analyzers` for slow Roslyn analyzers
4. Identify bottleneck tasks with `expensive_tasks`

> *"I have two binlogs from different build configurations. Compare them and tell me what's different."*

Copilot will:
1. Call `compare` to diff properties, per-project packages, and solution-wide packages
2. Report property differences (e.g., `NuGetPackageRoot`, SDK paths)
3. Show per-project package version mismatches
4. List solution-wide package diffs — packages resolved to different versions across all projects
5. Drill into specifics with `properties`, `items`, or `compiler` to explain *why* versions differ

This is especially useful for:
- **Migrating build systems** (e.g., CoreXT → stock MSBuild) — verify that the new build resolves the same packages
- **Debugging CI vs local** — find property or package differences between environments
- **Before/after changes** — confirm a `.props` or `.targets` change had the intended effect

## Architecture

```
BinlogInsights.Core        — Query engine (MSBuild.StructuredLogger)
├── BinlogInsights          — CLI tool (System.CommandLine)
└── BinlogInsights.Mcp      — MCP server (ModelContextProtocol SDK)
```

- **Core** parses binlogs via [MSBuild.StructuredLogger](https://github.com/KirillOsenkov/MSBuildStructuredLog) and exposes typed query classes
- **CLI** wraps queries with `System.CommandLine` for terminal use
- **MCP** wraps queries as [Model Context Protocol](https://modelcontextprotocol.io/) tools for AI agents

## License

MIT
