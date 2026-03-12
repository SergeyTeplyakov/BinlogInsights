---
description: Investigate MSBuild build failures and performance using binlog-insights MCP tools
applyTo: "**/*.{csproj,sln,slnx,slnf,props,targets,binlog}"
---

# Build Investigation with binlog-insights

When a build fails or you need to investigate build behavior, use the
binlog-insights MCP tools to analyze `.binlog` files.

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

## Error investigation workflow

1. **Start with overview**: `binlog_overview` → build status, duration, project count
2. **Get errors**: `binlog_errors` → errors with project, file, line, target context
3. **Drill deeper based on error type**:
   - Missing types/namespaces → `binlog_items` with type `PackageReference` to check dependencies
   - Wrong property values → `binlog_properties` with a filter for the property name
   - Import/SDK problems → `binlog_imports` to see the full import chain
   - NuGet restore failures → `binlog_nuget` for restore diagnostics and feed issues
   - Compiler errors → `binlog_compiler` to see the full csc/vbc command line
4. **Free-text search**: `binlog_search` to find any message in the build log
5. **Effective project XML**: `binlog_preprocess` to see the fully expanded project after all imports

## Performance investigation workflow

1. **Slowest projects**: `binlog_expensive_projects` → find the bottleneck projects
2. **Target breakdown**: `binlog_project_target_times` → see which targets are slow in a project
3. **Global target hotspots**: `binlog_expensive_targets` → slowest targets across all projects
4. **Task hotspots**: `binlog_expensive_tasks` → slowest tasks across all projects
5. **Analyzer performance**: `binlog_expensive_analyzers` → check if Roslyn analyzers are a bottleneck
6. **Deep dive**: `binlog_tasks_in_target` and `binlog_task_details` for individual task inspection

## Build comparison workflow

When comparing two builds (e.g., different environments, before/after a change):

1. **Diff overview**: `binlog_compare` → property diffs, per-project package diffs, solution-wide package diffs
2. **Investigate property diffs**: `binlog_properties` on each binlog to see where a property is set
3. **Investigate package diffs**: `binlog_items` with type `PackageReference` or `PackageVersion` to trace versioning
4. **Compare compiler inputs**: `binlog_compiler` on both to diff references, defines, and flags

## Tips

- Use `--project` to filter results to a specific project (substring match)
- Use `--limit` and `--offset` for pagination on large result sets
- Property and item queries accept `--filter` for substring matching
- When comparing builds, always use the full path to both `.binlog` files
