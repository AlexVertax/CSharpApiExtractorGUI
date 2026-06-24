using System.Collections.ObjectModel;
using System.Linq;
using CSharpApiExtractor;
using CSharpApiExtractorGUI.Infrastructure;

namespace CSharpApiExtractorGUI.Models;

public sealed class ProjectDefinition : BindableBase
{
    private string id = string.Empty;
    private string title = "New Project";
    [NonSerialized]
    private string listTitle = "New Project";
    private string apiRefOutputPath = string.Empty;
    private string? missingItemsOutputPath;
    private bool includeInternalMembers = true;
    private bool includeMissedItems = true;

    public string Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    public string ListTitle
    {
        get => listTitle;
        set => SetProperty(ref listTitle, value);
    }

    public ObservableCollection<PathEntry> ExportPaths { get; set; } = new();

    public ObservableCollection<PathEntry> IgnorePaths { get; set; } = new();

    public List<string>? ExtractPaths
    {
        set => ExportPaths = CreatePathEntries(value);
    }

    public List<string>? ExcludePaths
    {
        set => IgnorePaths = CreatePathEntries(value);
    }

    public string ApiRefOutputPath
    {
        get => apiRefOutputPath;
        set => SetProperty(ref apiRefOutputPath, value);
    }

    public string? MissingItemsOutputPath
    {
        get => missingItemsOutputPath;
        set => SetProperty(ref missingItemsOutputPath, value);
    }

    public bool IncludeInternalMembers
    {
        get => includeInternalMembers;
        set => SetProperty(ref includeInternalMembers, value);
    }

    public bool IncludeMissedItems
    {
        get => includeMissedItems;
        set => SetProperty(ref includeMissedItems, value);
    }

    public ExtractorOptions ToExtractorOptions()
    {
        ExtractorOptions options = new()
        {
            IncludeInternalMembers = IncludeInternalMembers,
            IncludeMissedItems = IncludeMissedItems
        };

        foreach (string path in ExportPaths.Select(item => item.Value).Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            options.SourcePaths.Add(path);
        }

        foreach (string path in IgnorePaths.Select(item => item.Value).Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            options.ExcludedPaths.Add(path);
        }

        return options;
    }

    public void Normalize()
    {
        ExportPaths ??= new ObservableCollection<PathEntry>();
        IgnorePaths ??= new ObservableCollection<PathEntry>();
        title ??= "New Project";
        listTitle = title;
        id ??= string.Empty;
        apiRefOutputPath ??= string.Empty;
    }

    private static ObservableCollection<PathEntry> CreatePathEntries(IEnumerable<string>? values)
    {
        ObservableCollection<PathEntry> entries = new();
        if (values is null)
        {
            return entries;
        }

        foreach (string value in values)
        {
            entries.Add(new PathEntry { Value = value ?? string.Empty });
        }

        return entries;
    }
}
