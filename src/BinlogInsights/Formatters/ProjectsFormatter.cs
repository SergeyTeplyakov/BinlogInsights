namespace BinlogInsights.Cli.Formatters;

public static class ProjectsFormatter
{
    public static void Print(IReadOnlyList<string> projectPaths)
    {
        if (projectPaths.Count == 0)
        {
            Console.WriteLine("No projects found in the build.");
            return;
        }

        Console.WriteLine($"Projects ({projectPaths.Count}):");
        Console.WriteLine();

        foreach (var path in projectPaths)
        {
            Console.WriteLine($"  {path}");
        }
    }
}
