using System.IO;

namespace CSharpApiExtractorGUI.Services;

public sealed class LastActiveProjectStore
{
    private readonly string filePath;

    public LastActiveProjectStore()
    {
        string directoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CSharpApiExtractorGUI");

        Directory.CreateDirectory(directoryPath);
        filePath = Path.Combine(directoryPath, "last-active-project.txt");
    }

    public string? Load()
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string value = File.ReadAllText(filePath).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Save(string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return;
        }

        File.WriteAllText(filePath, projectId);
    }
}
