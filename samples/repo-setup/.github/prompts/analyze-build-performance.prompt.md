---
description: "Analyze build performance and find bottlenecks"
mode: "agent"
tools: ["binlog-insights"]
---

# Analyze Build Performance

Analyze a `.binlog` file to identify what's making the build slow.

## Steps

1. Use `binlog_overview` to get total build duration and project count.
2. Use `binlog_expensive_projects` to find the slowest projects — these are the top candidates for optimization.
3. For each slow project, use `binlog_project_target_times` to see which targets take the most time.
4. Use `binlog_expensive_targets` for a build-wide view of the slowest targets.
5. Use `binlog_expensive_tasks` to find the most expensive individual tasks.
6. Use `binlog_expensive_analyzers` to check if Roslyn analyzers are contributing to slow compilation.
7. For suspicious targets, use `binlog_tasks_in_target` and `binlog_task_details` to inspect what they do.

## Output format

Provide a performance report:
- **Total build time** and project count
- **Top 5 slowest projects** with durations
- **Top bottleneck targets/tasks** across the build
- **Analyzer impact** — total analyzer time and slowest analyzers
- **Recommendations** — specific, actionable suggestions (e.g., "disable analyzer X", "parallelize project Y", "investigate target Z which runs for N seconds")
