using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using CSharpApiExtractor;
using CSharpApiExtractorGUI.Infrastructure;
using CSharpApiExtractorGUI.Models;
using CSharpApiExtractorGUI.Services;

namespace CSharpApiExtractorGUI.ViewModels;

public sealed class MainViewModel : BindableBase
{
    private static readonly HttpClient httpClient = new();
    private readonly ProjectCatalogStore store = new();
    private readonly LastActiveProjectStore lastActiveProjectStore = new();
    private readonly PathDialogService pathDialogService = new();
    private readonly DialogService dialogService = new();
    private readonly RelayCommand removeProjectCommand;
    private readonly RelayCommand saveCommand;
    private readonly RelayCommand exportCommand;
    private ProjectCatalog catalog = new();
    private ProjectDefinition? selectedProject;
    private CancellationTokenSource? saveFeedbackCancellationTokenSource;
    private string saveFeedbackMessage = string.Empty;
    private string statusMessage = string.Empty;
    private string validationMessage = string.Empty;

    public MainViewModel()
    {
        removeProjectCommand = new RelayCommand(parameter => RemoveProject(parameter as ProjectDefinition), parameter => parameter is ProjectDefinition);
        saveCommand = new RelayCommand(_ => Save(), _ => CanSave());
        exportCommand = new RelayCommand(async _ => await ExportAsync(), _ => CanExport());

        AddProjectCommand = new RelayCommand(_ => AddProject());
        RemoveProjectCommand = removeProjectCommand;
        AddExportPathRowCommand = new RelayCommand(_ => AddPathRow(SelectedProject?.ExportPaths), _ => HasSelection);
        AddIgnorePathRowCommand = new RelayCommand(_ => AddPathRow(SelectedProject?.IgnorePaths), _ => HasSelection);
        BrowseExportPathCommand = new RelayCommand(parameter => BrowseFolder(parameter as PathEntry), _ => HasSelection);
        BrowseIgnorePathCommand = new RelayCommand(parameter => BrowseFolder(parameter as PathEntry), _ => HasSelection);
        RemoveExportPathCommand = new RelayCommand(parameter => RemovePath(SelectedProject?.ExportPaths, parameter as PathEntry, "export paths"), parameter => parameter is PathEntry);
        RemoveIgnorePathCommand = new RelayCommand(parameter => RemovePath(SelectedProject?.IgnorePaths, parameter as PathEntry, "ignore paths"), parameter => parameter is PathEntry);
        BrowseApiRefOutputPathCommand = new RelayCommand(_ => BrowseApiRefOutputPath(), _ => HasSelection);
        BrowseMissingItemsOutputPathCommand = new RelayCommand(_ => BrowseMissingItemsOutputPath(), _ => HasSelection);
        OpenOneFileDocsCommand = new RelayCommand(_ => OpenOneFileDocs(), _ => true);
        SaveCommand = saveCommand;
        ExportCommand = exportCommand;

        Reload();
    }

    public ObservableCollection<ProjectDefinition> Projects => catalog.Projects;

    public ICollectionView ProjectsView => CollectionViewSource.GetDefaultView(Projects);

    public ProjectDefinition? SelectedProject
    {
        get => selectedProject;
        set
        {
            if (!SetProperty(ref selectedProject, value))
            {
                return;
            }

            Validate();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsEmptyStateVisible));
            lastActiveProjectStore.Save(selectedProject?.Id);
            RefreshCommands();
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string SaveFeedbackMessage
    {
        get => saveFeedbackMessage;
        private set
        {
            if (!SetProperty(ref saveFeedbackMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSaveFeedback));
        }
    }

    public bool HasSaveFeedback => !string.IsNullOrWhiteSpace(SaveFeedbackMessage);

    public string ValidationMessage
    {
        get => validationMessage;
        private set
        {
            if (!SetProperty(ref validationMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasSelection => SelectedProject is not null;

    public bool IsEmptyStateVisible => !HasSelection;

    public bool HasExportPathEntries => SelectedProject?.ExportPaths.Count > 0;

    public bool HasIgnorePathEntries => SelectedProject?.IgnorePaths.Count > 0;

    public ICommand AddProjectCommand { get; }

    public ICommand RemoveProjectCommand { get; }

    public ICommand AddExportPathRowCommand { get; }

    public ICommand AddIgnorePathRowCommand { get; }

    public ICommand BrowseExportPathCommand { get; }

    public ICommand BrowseIgnorePathCommand { get; }

    public ICommand RemoveExportPathCommand { get; }

    public ICommand RemoveIgnorePathCommand { get; }

    public ICommand BrowseApiRefOutputPathCommand { get; }

    public ICommand BrowseMissingItemsOutputPathCommand { get; }

    public ICommand OpenOneFileDocsCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand ExportCommand { get; }

    private void Reload()
    {
        UnsubscribeFromCatalog();
        catalog = store.Load();
        SubscribeToCatalog();
        ConfigureProjectView();

        OnPropertyChanged(nameof(Projects));
        OnPropertyChanged(nameof(ProjectsView));
        string? lastActiveProjectId = lastActiveProjectStore.Load();
        SelectedProject = Projects.FirstOrDefault(project =>
                               string.Equals(project.Id, lastActiveProjectId, StringComparison.OrdinalIgnoreCase)) ??
                           Projects.Cast<ProjectDefinition>()
                               .OrderBy(project => project.Title, StringComparer.CurrentCultureIgnoreCase)
                               .FirstOrDefault();
        StatusMessage = $"Configuration loaded from {store.GetFilePath()}";
    }

    private void ConfigureProjectView()
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(Projects);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(nameof(ProjectDefinition.ListTitle), ListSortDirection.Ascending));
    }

    private async void Save()
    {
        try
        {
            store.Save(catalog);
            CommitProjectTitles();
            StatusMessage = $"Saved to {store.GetFilePath()}";
            await ShowSaveFeedbackAsync("Saved successfully.");
        }
        catch (Exception exception)
        {
            dialogService.ShowError("Save Error", exception.Message);
        }
    }

    private async Task ExportAsync()
    {
        if (SelectedProject is null)
        {
            return;
        }

        if (!Validate())
        {
            dialogService.ShowError("Export Error", ValidationMessage);
            return;
        }

        try
        {
            ProjectDefinition project = SelectedProject;
            EnsureDirectoryForFile(project.ApiRefOutputPath);
            if (!string.IsNullOrWhiteSpace(project.MissingItemsOutputPath))
            {
                EnsureDirectoryForFile(project.MissingItemsOutputPath);
            }

            Extractor extractor = new(project.ToExtractorOptions());
            ApiDocument document = await extractor.ExtractAsync();
            string apiRefJson = document.ToJsonString(pretty: true);

            File.WriteAllText(project.ApiRefOutputPath, apiRefJson);

            if (!string.IsNullOrWhiteSpace(project.MissingItemsOutputPath))
            {
                File.WriteAllLines(project.MissingItemsOutputPath, document.MissedItems);
            }

            string syncStatus = string.Empty;
            try
            {
                syncStatus = await TrySyncOneFileDocsAsync(project.OneFileDocsSyncUrl, apiRefJson);
            }
            catch (Exception exception)
            {
                syncStatus = $"Failed: {exception.Message}";
            }

            dialogService.ShowExportSummary(
                project.Title,
                project.ApiRefOutputPath,
                document.Namespaces.Count,
                document.Namespaces.Sum(item => item.Types.Count),
                document.Namespaces.Sum(item => item.Types.Sum(type => type.Members.Count)),
                document.Namespaces.Sum(item => item.Types.Count(type => string.Equals(type.Kind, "class", StringComparison.OrdinalIgnoreCase))),
                document.Namespaces.Sum(item => item.Types.Count(type => string.Equals(type.Kind, "interface", StringComparison.OrdinalIgnoreCase))),
                document.Namespaces.Sum(item => item.Types.Count(type => string.Equals(type.Kind, "struct", StringComparison.OrdinalIgnoreCase))),
                document.Namespaces.Sum(item => item.Types.Count(type => string.Equals(type.Kind, "enum", StringComparison.OrdinalIgnoreCase))),
                document.Namespaces.Sum(item => item.Types.Count(type => string.Equals(type.Kind, "record", StringComparison.OrdinalIgnoreCase))),
                document.MissedItems.Count,
                syncStatus);

            StatusMessage = string.IsNullOrWhiteSpace(syncStatus)
                ? $"Export completed for \"{project.Title}\"."
                : $"Export completed for \"{project.Title}\". {syncStatus}";
        }
        catch (Exception exception)
        {
            dialogService.ShowError("Export Error", exception.Message);
        }
    }

    private static void EnsureDirectoryForFile(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private bool CanSave()
    {
        return Projects.Count > 0;
    }

    private bool CanExport()
    {
        return SelectedProject is not null;
    }

    private void AddProject()
    {
        string id = CreateProjectId();
        string title = CreateProjectTitle();
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        ProjectDefinition project = new()
        {
            Id = id,
            Title = title,
            ListTitle = title
        };

        Projects.Add(project);
        SelectedProject = project;
        ProjectsView.Refresh();
        StatusMessage = $"Added project \"{project.Title}\".";
    }

    private void RemoveProject(ProjectDefinition? project)
    {
        if (project is null)
        {
            return;
        }

        if (!dialogService.ConfirmDeleteProject(project.Title))
        {
            return;
        }

        Projects.Remove(project);
        SelectedProject = Projects.Cast<ProjectDefinition>()
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
        ProjectsView.Refresh();
        Validate();
        StatusMessage = $"Deleted project \"{project.Title}\".";
    }

    private static void AddPathRow(ObservableCollection<PathEntry>? collection)
    {
        collection?.Add(new PathEntry());
    }

    private void BrowseFolder(PathEntry? pathEntry)
    {
        if (pathEntry is null)
        {
            return;
        }

        string? selectedPath = pathDialogService.BrowseFolder(pathEntry.Value);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            pathEntry.Value = selectedPath;
        }
    }

    private void RemovePath(ObservableCollection<PathEntry>? collection, PathEntry? pathEntry, string scope)
    {
        if (collection is null || pathEntry is null)
        {
            return;
        }

        if (!dialogService.ConfirmDeletePath(pathEntry.Value, scope))
        {
            return;
        }

        collection.Remove(pathEntry);
        Validate();
        StatusMessage = $"Deleted path from {scope}.";
    }

    private void BrowseApiRefOutputPath()
    {
        if (SelectedProject is null)
        {
            return;
        }

        string? selectedPath = pathDialogService.BrowseSaveFile(
            "Select API reference output file",
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            ".json",
            SelectedProject.ApiRefOutputPath);

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            SelectedProject.ApiRefOutputPath = selectedPath;
            Validate();
        }
    }

    private void BrowseMissingItemsOutputPath()
    {
        if (SelectedProject is null)
        {
            return;
        }

        string? selectedPath = pathDialogService.BrowseSaveFile(
            "Select missing items output file",
            "Text files (*.txt)|*.txt|JSON files (*.json)|*.json|All files (*.*)|*.*",
            ".txt",
            SelectedProject.MissingItemsOutputPath);

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            SelectedProject.MissingItemsOutputPath = selectedPath;
            Validate();
        }
    }

    private static async Task<string> TrySyncOneFileDocsAsync(string? syncUrl, string apiRefJson)
    {
        if (string.IsNullOrWhiteSpace(syncUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(syncUrl.Trim(), UriKind.Absolute, out Uri? endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("OneFileDocs Sync URL must be a valid absolute HTTP or HTTPS URL.");
        }

        using StringContent content = new(
            JsonSerializer.Serialize(new { content = apiRefJson }),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await httpClient.PostAsync(endpoint, content);
        if (response.IsSuccessStatusCode)
        {
            return $"Success ({(int)response.StatusCode}).";
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"Failed ({(int)response.StatusCode} {response.ReasonPhrase}).{Environment.NewLine}{responseBody}");
    }

    private static void OpenOneFileDocs()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://onefiledocs.com",
            UseShellExecute = true
        });
    }

    private string CreateProjectId()
    {
        const string baseId = "project";
        int index = 1;

        while (Projects.Any(project => string.Equals(project.Id, $"{baseId}-{index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"{baseId}-{index}";
    }

    private string CreateProjectTitle()
    {
        const string baseTitle = "New Project";
        int index = 1;

        while (Projects.Any(project => string.Equals(project.Title, $"{baseTitle} {index}", StringComparison.CurrentCultureIgnoreCase)))
        {
            index++;
        }

        return $"{baseTitle} {index}";
    }

    private void CommitProjectTitles()
    {
        foreach (ProjectDefinition project in Projects)
        {
            project.ListTitle = project.Title;
        }

        ProjectsView.Refresh();
    }

    private bool Validate()
    {
        OnPropertyChanged(nameof(HasExportPathEntries));
        OnPropertyChanged(nameof(HasIgnorePathEntries));

        if (SelectedProject is not null)
        {
            if (string.IsNullOrWhiteSpace(SelectedProject.Id))
            {
                ValidationMessage = "Project ID is required.";
                RefreshCommands();
                return false;
            }

            bool duplicateId = Projects.Any(project =>
                !ReferenceEquals(project, SelectedProject) &&
                string.Equals(project.Id.Trim(), SelectedProject.Id.Trim(), StringComparison.OrdinalIgnoreCase));

            if (duplicateId)
            {
                ValidationMessage = "Project ID must be unique.";
                RefreshCommands();
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedProject.Title))
            {
                ValidationMessage = "Project title is required.";
                RefreshCommands();
                return false;
            }

            if (!SelectedProject.ExportPaths.Any(path => !string.IsNullOrWhiteSpace(path.Value)))
            {
                ValidationMessage = "At least one export path is required.";
                RefreshCommands();
                return false;
            }

            if (string.IsNullOrWhiteSpace(SelectedProject.ApiRefOutputPath))
            {
                ValidationMessage = "API reference output file is required.";
                RefreshCommands();
                return false;
            }
        }

        ValidationMessage = string.Empty;
        RefreshCommands();
        return true;
    }

    private void SubscribeToCatalog()
    {
        Projects.CollectionChanged += Projects_CollectionChanged;
        foreach (ProjectDefinition project in Projects)
        {
            SubscribeToProject(project);
        }
    }

    private void UnsubscribeFromCatalog()
    {
        Projects.CollectionChanged -= Projects_CollectionChanged;
        foreach (ProjectDefinition project in Projects)
        {
            UnsubscribeFromProject(project);
        }
    }

    private void Projects_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ProjectDefinition project in e.NewItems)
            {
                SubscribeToProject(project);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (ProjectDefinition project in e.OldItems)
            {
                UnsubscribeFromProject(project);
            }
        }

        ProjectsView.Refresh();
        Validate();
        RefreshCommands();
    }

    private void SubscribeToProject(ProjectDefinition project)
    {
        project.PropertyChanged += Project_PropertyChanged;
        project.ExportPaths.CollectionChanged += ProjectPaths_CollectionChanged;
        project.IgnorePaths.CollectionChanged += ProjectPaths_CollectionChanged;

        foreach (PathEntry path in project.ExportPaths)
        {
            path.PropertyChanged += Path_PropertyChanged;
        }

        foreach (PathEntry path in project.IgnorePaths)
        {
            path.PropertyChanged += Path_PropertyChanged;
        }
    }

    private void UnsubscribeFromProject(ProjectDefinition project)
    {
        project.PropertyChanged -= Project_PropertyChanged;
        project.ExportPaths.CollectionChanged -= ProjectPaths_CollectionChanged;
        project.IgnorePaths.CollectionChanged -= ProjectPaths_CollectionChanged;

        foreach (PathEntry path in project.ExportPaths)
        {
            path.PropertyChanged -= Path_PropertyChanged;
        }

        foreach (PathEntry path in project.IgnorePaths)
        {
            path.PropertyChanged -= Path_PropertyChanged;
        }
    }

    private void Project_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectDefinition.ListTitle))
        {
            ProjectsView.Refresh();
        }

        if (ReferenceEquals(sender, SelectedProject))
        {
            OnPropertyChanged(nameof(SelectedProject));
        }

        Validate();
    }

    private void ProjectPaths_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (PathEntry path in e.NewItems)
            {
                path.PropertyChanged += Path_PropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (PathEntry path in e.OldItems)
            {
                path.PropertyChanged -= Path_PropertyChanged;
            }
        }

        OnPropertyChanged(nameof(SelectedProject));
        OnPropertyChanged(nameof(HasExportPathEntries));
        OnPropertyChanged(nameof(HasIgnorePathEntries));
        Validate();
    }

    private async Task ShowSaveFeedbackAsync(string message)
    {
        saveFeedbackCancellationTokenSource?.Cancel();
        saveFeedbackCancellationTokenSource?.Dispose();

        CancellationTokenSource cancellationTokenSource = new();
        saveFeedbackCancellationTokenSource = cancellationTokenSource;
        SaveFeedbackMessage = message;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);
            SaveFeedbackMessage = string.Empty;
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(saveFeedbackCancellationTokenSource, cancellationTokenSource))
            {
                saveFeedbackCancellationTokenSource.Dispose();
                saveFeedbackCancellationTokenSource = null;
            }
        }
    }

    private void Path_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasExportPathEntries));
        OnPropertyChanged(nameof(HasIgnorePathEntries));
        Validate();
    }

    private void RefreshCommands()
    {
        removeProjectCommand.RaiseCanExecuteChanged();
        saveCommand.RaiseCanExecuteChanged();
        exportCommand.RaiseCanExecuteChanged();
        if (AddExportPathRowCommand is RelayCommand addExportPathRowCommand)
        {
            addExportPathRowCommand.RaiseCanExecuteChanged();
        }

        if (AddIgnorePathRowCommand is RelayCommand addIgnorePathRowCommand)
        {
            addIgnorePathRowCommand.RaiseCanExecuteChanged();
        }

        if (BrowseApiRefOutputPathCommand is RelayCommand browseApiRefOutputPathCommand)
        {
            browseApiRefOutputPathCommand.RaiseCanExecuteChanged();
        }

        if (BrowseMissingItemsOutputPathCommand is RelayCommand browseMissingItemsOutputPathCommand)
        {
            browseMissingItemsOutputPathCommand.RaiseCanExecuteChanged();
        }
    }
}
