using System.Collections.ObjectModel;

namespace CSharpApiExtractorGUI.Models;

public sealed class ProjectCatalog
{
    public ObservableCollection<ProjectDefinition> Projects { get; set; } = new();
}
