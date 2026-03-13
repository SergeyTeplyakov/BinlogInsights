using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using BinlogInsights.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// Redirect console logging to stderr so it doesn't corrupt the MCP JSON-RPC stdio transport.
builder.Services.Configure<ConsoleLoggerOptions>(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<BinlogCache>();

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new() { Name = "binlog-insights", Version = "0.1.0" };
    options.ServerInstructions = """
        You are an MSBuild build investigation assistant. Use these tools to analyze .binlog files.
        
        Investigation workflow:
        1. Start with binlog_overview to understand the build status
        2. If the build failed, use binlog_errors to see what went wrong
        3. Drill deeper based on errors:
           - Missing types/namespaces → binlog_items to check PackageReference
           - Property issues → binlog_properties to inspect values
           - Import problems → binlog_imports to see the import chain
           - NuGet failures → binlog_nuget for restore diagnostics
           - General investigation → binlog_search for free-text search
        4. Use binlog_compare to diff two builds when comparing configurations
        
        Performance investigation workflow:
        1. Use binlog_expensive_projects to find the slowest projects
        2. Drill into a project with binlog_project_target_times to see target-level timing
        3. Use binlog_expensive_targets or binlog_expensive_tasks for build-wide hotspots
        4. Use binlog_tasks_in_target to see what a specific target does
        5. Use binlog_expensive_analyzers to check if Roslyn analyzers are slow
        
        Evaluation & files:
        - binlog_evaluations lists project evaluations (property/item resolution phase)
        - binlog_list_files and binlog_get_file access source files embedded in the binlog
        """;
})
.WithStdioServerTransport()
// Diagnostics & investigation
.WithTools<OverviewTool>()
.WithTools<ErrorsTool>()
.WithTools<WarningsTool>()
.WithTools<PropertiesTool>()
.WithTools<ImportsTool>()
.WithTools<ItemsTool>()
.WithTools<ItemTypesTool>()
.WithTools<NuGetTool>()
.WithTools<CompilerTool>()
.WithTools<SearchTool>()
.WithTools<ProjectsTool>()
.WithTools<PreprocessTool>()
.WithTools<CompareTool>()
// Performance analysis
.WithTools<ExpensiveTargetsTool>()
.WithTools<SearchTargetsTool>()
.WithTools<ProjectTargetsTool>()
.WithTools<ExpensiveTasksTool>()
.WithTools<SearchTasksTool>()
.WithTools<TasksInTargetTool>()
.WithTools<TaskDetailsTool>()
.WithTools<ExpensiveProjectsTool>()
.WithTools<ProjectTargetTimesTool>()
// Evaluations
.WithTools<EvaluationsTool>()
.WithTools<EvaluationGlobalPropertiesTool>()
.WithTools<EvaluationPropertiesTool>()
// Analyzer performance
.WithTools<ExpensiveAnalyzersTool>()
// Embedded files
.WithTools<ListFilesTool>()
.WithTools<GetFileTool>();

await builder.Build().RunAsync();
