using System.IO;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CSharpApiExtractorGUI.Services;

public sealed class PathDialogService
{
    public string? BrowseFolder(string? initialPath = null)
    {
        using Forms.FolderBrowserDialog dialog = new();
        dialog.ShowNewFolderButton = true;

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    public string? BrowseSaveFile(string title, string filter, string defaultExtension, string? initialPath = null)
    {
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            string? directory = Path.GetDirectoryName(initialPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }

            string? fileName = Path.GetFileName(initialPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                dialog.FileName = fileName;
            }
        }

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }
}
