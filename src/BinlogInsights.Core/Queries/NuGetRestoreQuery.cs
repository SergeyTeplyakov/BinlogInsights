using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class NuGetRestoreQuery
{
    public static NuGetRestoreResult Execute(Build build, string? projectFilter = null)
    {
        var packageReferences = new List<ItemResult>();
        var restoreMessages = new List<DiagnosticResult>();
        var restoreSucceeded = true;

        // Collect PackageReference items from evaluations
        var evaluations = new List<ProjectEvaluation>();
        build.VisitAllChildren<ProjectEvaluation>(eval =>
        {
            if (string.IsNullOrEmpty(projectFilter) ||
                (eval.ProjectFile != null &&
                 eval.ProjectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase)))
            {
                evaluations.Add(eval);
            }
        });

        foreach (var eval in evaluations)
        {
            eval.VisitAllChildren<AddItem>(addItem =>
            {
                if (!string.Equals(addItem.Name, "PackageReference", StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (var child in addItem.Children)
                {
                    if (child is Item item)
                    {
                        var metadata = new List<MetadataResult>();
                        foreach (var meta in item.Children.OfType<Metadata>())
                        {
                            metadata.Add(new MetadataResult(meta.Name, meta.Value));
                        }
                        packageReferences.Add(new ItemResult("PackageReference", item.Text ?? item.Name ?? "", metadata));
                    }
                }
            });

            // Also look for items in Folder named PackageReference
            eval.VisitAllChildren<Folder>(folder =>
            {
                if (!string.Equals(folder.Name, "PackageReference", StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (var item in folder.Children.OfType<Item>())
                {
                    var metadata = new List<MetadataResult>();
                    foreach (var meta in item.Children.OfType<Metadata>())
                    {
                        metadata.Add(new MetadataResult(meta.Name, meta.Value));
                    }
                    packageReferences.Add(new ItemResult("PackageReference", item.Text ?? item.Name ?? "", metadata));
                }
            });
        }

        // Find Restore target execution and collect errors/warnings
        build.VisitAllChildren<Target>(target =>
        {
            if (!string.Equals(target.Name, "Restore", StringComparison.OrdinalIgnoreCase) &&
                !target.Name.Contains("NuGet", StringComparison.OrdinalIgnoreCase) &&
                !target.Name.Contains("Restore", StringComparison.OrdinalIgnoreCase))
                return;

            var projectFile = (target.Parent as Project)?.ProjectFile;
            if (!string.IsNullOrEmpty(projectFilter) &&
                (projectFile == null || !projectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase)))
                return;

            if (!target.Succeeded)
                restoreSucceeded = false;

            target.VisitAllChildren<Error>(error =>
            {
                restoreMessages.Add(new DiagnosticResult(
                    Severity: "error",
                    Code: error.Code,
                    Message: error.Text ?? error.ToString(),
                    File: error.File,
                    Line: error.LineNumber > 0 ? error.LineNumber : null,
                    Column: null,
                    ProjectFile: projectFile,
                    TargetName: target.Name,
                    TaskName: null));
            });

            target.VisitAllChildren<Warning>(warning =>
            {
                restoreMessages.Add(new DiagnosticResult(
                    Severity: "warning",
                    Code: warning.Code,
                    Message: warning.Text ?? warning.ToString(),
                    File: warning.File,
                    Line: warning.LineNumber > 0 ? warning.LineNumber : null,
                    Column: null,
                    ProjectFile: projectFile,
                    TargetName: target.Name,
                    TaskName: null));
            });
        });

        // Collect relevant NuGet properties
        var nugetProperties = new List<PropertyResult>();
        foreach (var eval in evaluations)
        {
            eval.VisitAllChildren<Property>(prop =>
            {
                if (IsNuGetProperty(prop.Name))
                {
                    nugetProperties.Add(new PropertyResult(prop.Name, prop.Value));
                }
            });
        }

        return new NuGetRestoreResult(
            RestoreSucceeded: restoreSucceeded,
            PackageReferences: packageReferences,
            RestoreMessages: restoreMessages,
            NuGetProperties: nugetProperties.DistinctBy(p => p.Name).ToList());
    }

    private static bool IsNuGetProperty(string name) =>
        name.Contains("NuGet", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Restore", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "ManagePackageVersionsCentrally", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "CentralPackageVersionOverrideEnabled", StringComparison.OrdinalIgnoreCase);
}

public record NuGetRestoreResult(
    bool RestoreSucceeded,
    IReadOnlyList<ItemResult> PackageReferences,
    IReadOnlyList<DiagnosticResult> RestoreMessages,
    IReadOnlyList<PropertyResult> NuGetProperties);
