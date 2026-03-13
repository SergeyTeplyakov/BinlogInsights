---
name: build-failure-analysis
description: Investigate MSBuild build failures using binlog-insights MCP tools. Use when a dotnet build or msbuild command fails, NuGet restore fails, or you see errors like CS0246, NU1101, MSB3644, MSB4019. Also triggers for missing type, missing package, import not found, and SDK resolution issues.
metadata:
  traits: MSBuild|DotNet|CSharp|NuGet|Build|Binlog
---

# Build Failure Analysis

## Overview

Diagnose MSBuild build failures by analyzing `.binlog` files. Drills from high-level build status down to specific root causes using binlog-insights MCP tools.

## Quick Start

```bash
dotnet build /bl:build.binlog
```

Then ask: *"My build failed. Investigate build.binlog and tell me what went wrong."*

## Workflow

1. Run `binlog_overview` to check build status, duration, and project count
2. Run `binlog_errors` to get all errors with project, file, line, and target context
3. Drill deeper based on error type:
   - **CS0246 / CS0234** (missing type or namespace) → `binlog_items` with type `PackageReference` to check if the dependency exists. Check `binlog_properties` for `TargetFramework` to rule out TFM mismatch
   - **NU1101 / NU1102 / NU1103** (NuGet resolution failure) → `binlog_nuget` for restore diagnostics and feed configuration
   - **MSB3644 / MSB3270** (reference assembly not found) → `binlog_compiler` for reference paths, `binlog_imports` for SDK resolution
   - **MSB4019 / MSB4057** (missing import or target) → `binlog_imports` to trace the import chain
   - **Other errors** → `binlog_search` with the error code or key terms
4. If needed, use `binlog_preprocess` to see the fully expanded project XML after all imports
5. Summarize: what failed, why, and what needs to change

```
Task Progress:
- [ ] Step 1: binlog_overview — get build status
- [ ] Step 2: binlog_errors — get all errors
- [ ] Step 3: Drill into each error type
- [ ] Step 4: Summarize root cause and fix
```

## Tool Reference

| Tool | Purpose |
|------|---------|
| `binlog_overview` | Build status, duration, project count |
| `binlog_errors` | Errors with project, file, line context |
| `binlog_warnings` | Warnings with context |
| `binlog_properties` | MSBuild property values (use `--filter`) |
| `binlog_items` | Item groups: PackageReference, Compile, etc. |
| `binlog_imports` | Full import chain for a project |
| `binlog_nuget` | NuGet restore diagnostics and feed issues |
| `binlog_compiler` | Full compiler command line |
| `binlog_search` | Free-text search across all messages |
| `binlog_preprocess` | Effective project XML after all imports |

## Common Patterns

**Missing NuGet package:**
Error: `NU1101: Unable to find package 'Foo.Bar'`
Action: `binlog_nuget` → check feed configuration → verify package name and source

**Missing type after upgrade:**
Error: `CS0246: The type or namespace name 'JsonConvert' could not be found`
Action: `binlog_items --type PackageReference --project MyApp` → verify `Newtonsoft.Json` is referenced → check `binlog_properties --filter TargetFramework`

**Missing import:**
Error: `MSB4019: The imported project "...\Microsoft.Cpp.Default.props" was not found`
Action: `binlog_imports --project MyApp` → trace the import chain → check SDK installation

## Troubleshooting

- **No binlog file**: Rebuild with `dotnet build /bl:build.binlog`
- **Tool not found**: Ensure `binlog-insights-mcp` is installed: `dotnet tool install -g BinlogInsights.Mcp`
- **Large result sets**: Use `--limit` and `--offset` for pagination
- **Filter to one project**: Use `--project <substring>` on any tool to narrow results
