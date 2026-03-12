---
description: "Investigate a build failure from a binlog file"
mode: "agent"
tools: ["binlog-insights"]
---

# Investigate Build Failure

A build has failed. Analyze the `.binlog` file and determine the root cause.

## Steps

1. Use `binlog_overview` to check the build status, duration, and project list.
2. Use `binlog_errors` to get all errors with project and target context.
3. For each distinct error, drill deeper:
   - **CS0246 / CS0234** (missing type or namespace): Use `binlog_items` with type `PackageReference` on the failing project to check if the dependency is present. Check `binlog_properties` for `TargetFramework` to see if it's a TFM mismatch.
   - **NU1101 / NU1102 / NU1103** (NuGet resolution failure): Use `binlog_nuget` to see restore diagnostics and feed configuration.
   - **MSB3644 / MSB3270** (reference assembly not found): Use `binlog_compiler` to check the reference paths. Use `binlog_imports` to verify SDK resolution.
   - **MSB4019 / MSB4057** (missing import or target): Use `binlog_imports` to trace the import chain and find what's missing.
   - **Other errors**: Use `binlog_search` with the error code or key terms to find related messages.
4. Summarize your findings: what failed, why, and what needs to change.

## Output format

Provide a clear summary with:
- **Root cause**: What specifically is wrong
- **Affected projects**: Which projects are impacted
- **Fix**: What needs to change (property, package reference, import, etc.)
