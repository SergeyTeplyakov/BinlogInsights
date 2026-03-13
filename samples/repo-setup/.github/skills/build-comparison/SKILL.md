---
name: build-comparison
description: Compare two MSBuild builds and report differences using binlog-insights MCP tools. Use when comparing CI vs local builds, before/after a change, different build configurations, or migrating build systems (e.g., CoreXT to stock MSBuild). Also triggers for build diff, environment differences, and package version mismatch.
metadata:
  traits: MSBuild|DotNet|CSharp|NuGet|Build|Binlog
---

# Build Comparison

## Overview

Compare two `.binlog` files to find meaningful differences in properties, packages, and compiler inputs. Useful for build system migrations, CI vs local debugging, and verifying the effect of `.props`/`.targets` changes.

## Quick Start

```bash
dotnet build /bl:before.binlog
# make changes
dotnet build /bl:after.binlog
```

Then ask: *"Compare before.binlog and after.binlog — what changed?"*

## Workflow

1. Run `binlog_overview` on **both** binlogs to get build status, duration, and project counts
2. Run `binlog_compare` with both binlog paths to get:
   - Property differences — different MSBuild property values
   - Per-project package differences — packages with different versions in the same project
   - Solution-wide package differences — packages resolved to different versions anywhere
3. For significant property differences, use `binlog_properties` on each binlog to trace where the value is set
4. For package version differences, use `binlog_items` with type `PackageReference` or `PackageVersion` to understand why versions differ
5. If needed, use `binlog_compiler` on both binlogs for a specific project to compare actual compiler inputs (references, defines, flags)

```
Task Progress:
- [ ] Step 1: binlog_overview — both binlogs
- [ ] Step 2: binlog_compare — get all diffs
- [ ] Step 3: Investigate property differences
- [ ] Step 4: Investigate package differences
- [ ] Step 5: Compare compiler inputs if needed
```

## Tool Reference

| Tool | Purpose |
|------|---------|
| `binlog_overview` | Build status, duration, project count |
| `binlog_compare` | Diff two binlogs: properties, packages |
| `binlog_properties` | MSBuild property values (use `--filter`) |
| `binlog_items` | Item groups: PackageReference, PackageVersion, etc. |
| `binlog_compiler` | Full compiler command line for diffing references |
| `binlog_projects` | Project list with status and duration |

## Common Patterns

**Build system migration (e.g., CoreXT → stock MSBuild):**
Action: `binlog_compare` → check solution-wide package diffs → verify same packages resolve → `binlog_compiler` to compare reference lists

**CI vs local mismatch:**
Action: `binlog_compare` → check property diffs for `NuGetPackageRoot`, SDK paths, `Configuration` → `binlog_items` to check if different package versions resolve

**Before/after a .props change:**
Action: `binlog_compare` → check property diffs for the changed property → verify it propagated to the expected projects

## Troubleshooting

- **No binlog file**: Rebuild with `dotnet build /bl:build.binlog`
- **Tool not found**: Ensure `binlog-insights-mcp` is installed: `dotnet tool install -g BinlogInsights.Mcp`
- **Large result sets**: Use `--limit` and `--offset` for pagination
- **Filter to one project**: Use `--project <substring>` on any tool to narrow results
