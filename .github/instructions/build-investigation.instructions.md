---
description: Investigate MSBuild build failures and performance using binlog-insights MCP tools
applyTo: "**/*.{csproj,sln,slnx,props,targets,binlog}"
tools:
  - binlog_overview
  - binlog_errors
  - binlog_warnings
  - binlog_properties
  - binlog_imports
  - binlog_items
  - binlog_nuget
  - binlog_compiler
  - binlog_search
  - binlog_expensive_targets
  - binlog_expensive_tasks
  - binlog_expensive_projects
  - binlog_expensive_analyzers
---

# Build Investigation with binlog-insights

When a `dotnet build` fails or you need to investigate build performance, use
the binlog-insights MCP tools to analyze `.binlog` files.

## When to use

- A `dotnet build`, `dotnet restore`, or `msbuild` command fails
- You see MSBuild errors like CS0246, NU1101, MSB3644, etc.
- You need to understand why a build is slow
- You need to compare two build configurations

## Building

Always pass `/bl` when building so a binlog is available for investigation:

```
dotnet build /bl
```

This writes `msbuild.binlog` in the current directory.

## Error investigation

1. `binlog_overview` → understand build status, duration, project list
2. `binlog_errors` → get errors with project/target/task context
3. Drill deeper:
   - Missing types → `binlog_items` (check PackageReference)
   - Property issues → `binlog_properties`
   - Import problems → `binlog_imports`
   - NuGet restore failures → `binlog_nuget`
   - Compiler issues → `binlog_compiler`

## Performance investigation

1. `binlog_expensive_projects` → slowest projects
2. `binlog_expensive_targets` → target hotspots
3. `binlog_expensive_tasks` → task hotspots
4. `binlog_expensive_analyzers` → slow Roslyn analyzers
