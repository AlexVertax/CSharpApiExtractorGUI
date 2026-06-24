using System.IO;
using CSharpApiExtractorGUI.Models;
using SilkJson;

namespace CSharpApiExtractorGUI.Services;

public sealed class ProjectCatalogStore
{
    private readonly string filePath;

    public ProjectCatalogStore()
    {
        string directoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CSharpApiExtractorGUI");

        Directory.CreateDirectory(directoryPath);
        filePath = Path.Combine(directoryPath, "projects.json");
    }

    public ProjectCatalog Load()
    {
        if (!File.Exists(filePath))
        {
            return CreateDefaultCatalog();
        }

        string json = File.ReadAllText(filePath);
        ProjectCatalog? catalog = Json.To<ProjectCatalog>(json);
        if (catalog is null)
        {
            return CreateDefaultCatalog();
        }

        NormalizeCatalog(catalog);
        return catalog;
    }

    public void Save(ProjectCatalog catalog)
    {
        string json = Json.Prettify(Json.From(catalog));
        File.WriteAllText(filePath, json);
    }

    public string GetFilePath()
    {
        return filePath;
    }

    private static ProjectCatalog CreateDefaultCatalog()
    {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string defaultSourcePath = documentsPath;

        return new ProjectCatalog
        {
            Projects =
            [
                new ProjectDefinition
                {
                    Id = "project-1",
                    Title = "New Project",
                    ExportPaths = new()
                    {
                        new PathEntry { Value = defaultSourcePath }
                    },
                    ApiRefOutputPath = Path.Combine(documentsPath, "api-ref.json"),
                    MissingItemsOutputPath = Path.Combine(documentsPath, "missing-items.txt")
                }
            ]
        };
    }

    private static void NormalizeCatalog(ProjectCatalog catalog)
    {
        catalog.Projects ??= new();

        foreach (ProjectDefinition? project in catalog.Projects)
        {
            project?.Normalize();
        }
    }
}
