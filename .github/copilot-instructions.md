# Build Investigation with binlog-insights

When investigating MSBuild build failures, use `binlog-insights` — a CLI tool
that extracts detailed diagnostics from MSBuild binary log (.binlog) files.

## Generating a binlog

If no `.binlog` file exists yet, build the project with binary logging enabled:

```
dotnet build /bl:build.binlog
```

## Investigation workflow

1. **Start with an overview** to understand the build status:
   ```
   binlog-insights overview <binlog>
   ```

2. **Get the errors** with full project/target/task context:
   ```
   binlog-insights errors <binlog>
   ```

3. **Drill deeper** based on what the errors suggest:

   - **CS0246 / missing type or namespace** → Check what packages are actually referenced:
     ```
     binlog-insights items <binlog> --project <proj> --type PackageReference
     ```

   - **Unexpected property values or import-driven issues** → Check the import chain and properties:
     ```
     binlog-insights imports <binlog> --project <proj>
     binlog-insights properties <binlog> --project <proj>
     ```
     Look for `Directory.Build.props`, missing imports (`[MISSING]`), and overridden properties.

   - **NuGet restore failures (NU1101, NU1102, etc.)** → Get full restore diagnostics:
     ```
     binlog-insights nuget <binlog>
     ```

   - **Warnings promoted to errors** → Check if `TreatWarningsAsErrors` is set:
     ```
     binlog-insights properties <binlog> --project <proj> --filter TreatWarningsAsErrors
     ```

   - **Compiler-level issues** → See the exact csc/vbc command line:
     ```
     binlog-insights compiler <binlog> --project <proj>
     ```

   - **General investigation** → Search all build messages:
     ```
     binlog-insights search <binlog> --query "<text>"
     ```

4. **Inspect specific properties** when you suspect a configuration issue:
   ```
   binlog-insights properties <binlog> --project <proj> --filter TargetFramework,OutputPath,LangVersion
   ```

5. **After fixing**, rebuild with `/bl` and re-analyze if it still fails:
   ```
   dotnet build /bl:build.binlog
   binlog-insights overview build.binlog
   ```

## Available commands

| Command | Purpose |
|---------|---------|
| `overview <binlog>` | Build status, duration, project list, error/warning counts |
| `errors <binlog> [--project <filter>]` | Errors with project/target/task context |
| `warnings <binlog> [--project <filter>] [--code <code>]` | Warnings with context |
| `imports <binlog> --project <filter>` | Full import tree (Directory.Build.props, SDK, etc.) |
| `properties <binlog> --project <filter> [--filter <names>]` | Evaluated MSBuild properties |
| `items <binlog> --project <filter> --type <type>` | MSBuild items (Compile, PackageReference, etc.) |
| `nuget <binlog> [--project <filter>]` | NuGet restore diagnostics |
| `preprocess <binlog> --project <filter>` | Effective project XML after all imports |
| `compiler <binlog> [--project <filter>]` | Compiler invocation command line |
| `search <binlog> --query <text>` | Free-text search across build messages |

Common options: `--limit <n>` (default 50), `--offset <n>` (default 0), `--project <substring>`.
