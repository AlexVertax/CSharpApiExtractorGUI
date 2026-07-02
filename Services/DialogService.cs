using System.Windows;
using CSharpApiExtractorGUI.Dialogs;

namespace CSharpApiExtractorGUI.Services;

public sealed class DialogService
{
    public void ShowError(string title, string message)
    {
        System.Windows.MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public bool ConfirmDeleteProject(string title)
    {
        MessageBoxResult result = System.Windows.MessageBox.Show(
            $"Delete project \"{title}\"?",
            "Delete Project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmDeletePath(string path, string scope)
    {
        string label = string.IsNullOrWhiteSpace(path) ? "this row" : $"\"{path}\"";
        MessageBoxResult result = System.Windows.MessageBox.Show(
            $"Delete {label} from {scope}?",
            "Delete Path",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    public void ShowExportSummary(
        string projectTitle,
        string apiRefOutputPath,
        int namespaceCount,
        int typeCount,
        int memberCount,
        int classCount,
        int interfaceCount,
        int structCount,
        int enumCount,
        int recordCount,
        int missedItemCount,
        string? oneFileDocsSyncMessage)
    {
        ExportSummaryWindow window = new(
            projectTitle,
            apiRefOutputPath,
            namespaceCount,
            typeCount,
            memberCount,
            classCount,
            interfaceCount,
            structCount,
            enumCount,
            recordCount,
            missedItemCount,
            oneFileDocsSyncMessage);
        window.ShowDialog();
    }
}
