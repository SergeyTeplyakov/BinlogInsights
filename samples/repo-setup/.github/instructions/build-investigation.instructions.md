---
description: Investigate MSBuild build failures and performance using binlog-insights MCP tools
applyTo: "**/*.{csproj,sln,slnx,slnf,props,targets,binlog}"
---

# Build Investigation with binlog-insights

When a build fails or you need to investigate build behavior, use the
binlog-insights MCP tools to analyze `.binlog` files.

## Prerequisite — ensure binlog-insights-mcp is installed

Before doing any build investigation, verify the tool is available:

```
binlog-insights-mcp --version
```

If the command is not found or returns an error, invoke the `build-tool-setup`
skill to install it. **Do not proceed with any binlog analysis until the tool is
confirmed working.**

## When to use

- A `dotnet build`, `dotnet restore`, or `msbuild` command fails
- You see MSBuild errors (CS0246, NU1101, MSB3644, etc.)
- You need to understand why a build is slow
- You need to compare two build configurations or environments
- You need to trace how a property or item is set

## Generating a binlog

If no `.binlog` file exists, rebuild with binary logging:

```
dotnet build /bl:build.binlog
```

## Skills for detailed workflows

Use these skills for step-by-step investigation guidance:

| Scenario | Skill |
|----------|-------|
| Install or update binlog-insights-mcp | `build-tool-setup` |
| Build errors, missing types, NuGet failures | `build-failure-analysis` |
| Slow builds, expensive targets/tasks/analyzers | `build-performance-analysis` |
| Comparing two builds, CI vs local, migrations | `build-comparison` |

## Quick reference

- `binlog_overview` — start here: build status, duration, project count
- `binlog_errors` / `binlog_warnings` — diagnostics with project and file context
- `binlog_properties --filter <name>` — check MSBuild property values
- `binlog_items --type <type>` — check item groups (PackageReference, Compile, etc.)
- `binlog_imports` — trace the import chain
- `binlog_nuget` — NuGet restore diagnostics
- `binlog_search --query <text>` — free-text search across all messages
- `binlog_compare` — diff two binlogs
- Use `--project <substring>` to filter to a specific project
- Use `--limit` and `--offset` for pagination on large result sets
