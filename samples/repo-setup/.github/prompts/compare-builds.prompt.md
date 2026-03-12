---
description: "Compare two builds and report all differences"
mode: "agent"
tools: ["binlog-insights"]
---

# Compare Two Builds

Compare two `.binlog` files and report all meaningful differences.

## Steps

1. Use `binlog_overview` on **both** binlogs to get build status, duration, and project counts.
2. Use `binlog_compare` with both binlog paths to get:
   - **Property differences** — different MSBuild property values
   - **Per-project package differences** — packages with different versions in the same project
   - **Solution-wide package differences** — packages resolved to different versions anywhere in the build
3. For significant property differences, use `binlog_properties` on each binlog to trace where the value is set.
4. For package version differences, use `binlog_items` with type `PackageReference` or `PackageVersion` to understand why versions differ.
5. If needed, use `binlog_compiler` on both binlogs for a specific project to compare the actual compiler inputs (references, defines).

## Output format

Provide a structured comparison:
- **Build summary**: status, duration, project count for each build
- **Property differences**: table of property name, value in A, value in B
- **Package differences**: table of package name, version in A, version in B, affected projects
- **Impact assessment**: which differences are benign (e.g., different paths) vs. which could cause behavior changes (e.g., different package versions)
- **Recommendations**: what to investigate further or fix
