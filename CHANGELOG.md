# Changelog

## v0.4.0

### Breaking changes

- **Tools now require .NET 10**: The global `binlog-insights-mcp` tool targets `net10.0`, which means a .NET 10 runtime must be installed to run this version. Users on .NET 8/9 need to install .NET 10 or continue using v0.3.x.
### Improvements

- **Upgraded to .NET 10** and updated all dependencies to their latest versions.

## v0.3.1

### Bug fixes

- **Improved relative binlog path error messages** (#10): When a relative path like `build.binlog` is passed and the file isn't found, the error now explains that it was resolved against the server's working directory and suggests using an absolute path or installing the Binlog Analyzer VS Code extension (which sets the current working directory (cwd) automatically). Previously this produced a confusing `FileNotFoundException` against an unexpected directory.

## v0.3.0

### New features

- **`--binlog` preload flag**: The MCP server accepts `--binlog <path>` (or `-b <path>`) to pre-load binlogs at startup, making subsequent tool calls instant. Supports multiple binlogs: `--binlog a.binlog --binlog b.binlog`.
- **`--version` flag**: `binlog-insights-mcp --version` prints the version string for deployment verification.
- **`binlog_expensive_analyzers` tool**: New MCP tool to identify slow Roslyn analyzers in a build.

### Bug fixes

- **Fixed `binlog_expensive_analyzers` returning empty results** (#5): The query was searching for raw `Message` nodes that no longer exist after `BuildAnalyzer.AnalyzeBuild()` restructures the tree. Rewritten to walk the structured `Folder`/`TimedMessage` tree.
- **Fixed `binlog_expensive_targets` / `binlog_expensive_tasks` response format**: Added `name` and `duration` fields to match the expected format for extension UI compatibility.
- **Fixed console logging corrupting MCP JSON-RPC transport**: Console logging is now redirected to stderr so it doesn't interfere with the stdio transport.

### Improvements

- **Graceful error handling for binlog load failures**: Invalid or missing binlog files now return structured MCP error responses with actionable messages instead of crashing the server.
- **NerdBank.GitVersioning**: Version is now computed from git history (unique per commit), eliminating NuGet-cache staleness during local development.
- **Rewritten `install.ps1`**: Reliable local deployment — resolves NBGV version, kills running instances, clears NuGet cache, installs with pinned version, verifies via `--version` and MCP initialize probe.

## v0.2.0

- Initial public release with CLI and MCP server.
- 26 MCP tools / 13 CLI commands for build investigation, performance analysis, and build comparison.
