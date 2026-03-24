# Build Investigation with binlog-insights

This repo uses the `binlog-insights-mcp` MCP server for MSBuild binary log analysis.

## Architecture

The MCP server (`BinlogInsights.Mcp`) is designed to work in **two modes**:

1. **Standalone** — configured directly in `.vscode/mcp.json` or user-level `mcp.json`, without the VS Code extension. This is the primary mode for most users.
2. **With the extension** — the `vscode-binlog-analyzer` extension auto-configures the MCP server and provides a tree view UI, but the MCP server must work independently of it.

**Any feature or fix must work in standalone mode.** Do not rely on the extension to set `cwd`, pass extra arguments, or configure the server. The server should be self-sufficient when launched with just `"command": "binlog-insights-mcp"` and `"args": []`.

## Setup

1. Install the global tool: `dotnet tool install -g BinlogInsights.Mcp`
2. The `.vscode/mcp.json` in this repo configures the MCP server automatically.

## Generating a binlog

If no `.binlog` file exists yet, build the project with binary logging enabled:

```
dotnet build /bl:build.binlog
```

## Available MCP tools

See `.github/instructions/build-investigation.instructions.md` for the full
workflow — it auto-activates when editing build-related files.
| `nuget <binlog> [--project <filter>]` | NuGet restore diagnostics |
| `preprocess <binlog> --project <filter>` | Effective project XML after all imports |
| `compiler <binlog> [--project <filter>]` | Compiler invocation command line |
| `search <binlog> --query <text>` | Free-text search across build messages |

Common options: `--limit <n>` (default 50), `--offset <n>` (default 0), `--project <substring>`.
