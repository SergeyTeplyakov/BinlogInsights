---
name: build-performance-analysis
description: Analyze MSBuild build performance and find bottlenecks using binlog-insights MCP tools. Use when a build is slow, you need to optimize build times, or identify expensive projects, targets, tasks, or Roslyn analyzers. Also triggers for build duration, build speed, and compilation time.
metadata:
  traits: MSBuild|DotNet|CSharp|Build|Binlog|Performance
---

# Build Performance Analysis

## Overview

Find what's making an MSBuild build slow by analyzing `.binlog` files. Identifies expensive projects, targets, tasks, and Roslyn analyzers using binlog-insights MCP tools.

## Quick Start

```bash
dotnet build /bl:build.binlog
```

Then ask: *"Why is my build slow? Analyze build.binlog performance."*

## Workflow

1. Run `binlog_overview` to get total build duration and project count
2. Run `binlog_expensive_projects` to find the slowest projects
3. For each slow project, run `binlog_project_target_times` to see which targets take the most time
4. Run `binlog_expensive_targets` for a build-wide view of the slowest targets
5. Run `binlog_expensive_tasks` to find the most expensive individual tasks
6. Run `binlog_expensive_analyzers` to check if Roslyn analyzers are a bottleneck
7. For suspicious targets, use `binlog_tasks_in_target` and `binlog_task_details` to inspect what they do

```
Task Progress:
- [ ] Step 1: binlog_overview — total duration
- [ ] Step 2: binlog_expensive_projects — slowest projects
- [ ] Step 3: binlog_project_target_times — target breakdown per project
- [ ] Step 4: binlog_expensive_targets — global target hotspots
- [ ] Step 5: binlog_expensive_tasks — global task hotspots
- [ ] Step 6: binlog_expensive_analyzers — analyzer impact
- [ ] Step 7: Deep dive into suspicious targets
```

## Tool Reference

| Tool | Purpose |
|------|---------|
| `binlog_overview` | Build status, duration, project count |
| `binlog_expensive_projects` | Slowest projects by duration |
| `binlog_project_target_times` | Target timing for a specific project |
| `binlog_expensive_targets` | Slowest targets across all projects |
| `binlog_expensive_tasks` | Slowest tasks across all projects |
| `binlog_expensive_analyzers` | Roslyn analyzer performance |
| `binlog_tasks_in_target` | Tasks within a specific target |
| `binlog_task_details` | Details of a specific task execution |
| `binlog_projects` | Project list with status and duration |

## Common Patterns

**Slow build — find the bottleneck:**
Action: `binlog_expensive_projects` → find 60s project → `binlog_project_target_times` → discover `ResolveAssemblyReference` takes 45s

**Analyzer overhead:**
Action: `binlog_expensive_analyzers` → find analyzer taking 15s per project → disable or move to CI-only

**Unexpected target running:**
Action: `binlog_expensive_targets` → spot `GenerateNuspec` running for 30 projects that don't ship packages → check `IsPackable` property

## Troubleshooting

- **No binlog file**: Rebuild with `dotnet build /bl:build.binlog`
- **Tool not found**: Ensure `binlog-insights-mcp` is installed: `dotnet tool install -g BinlogInsights.Mcp`
- **Filter to one project**: Use `--project <substring>` on any tool to narrow results
