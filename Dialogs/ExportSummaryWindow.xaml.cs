using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CSharpApiExtractorGUI.Dialogs;

public partial class ExportSummaryWindow : Window
{
    private readonly string apiRefOutputPath;

    public ExportSummaryWindow(
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
        InitializeComponent();
        Owner = System.Windows.Application.Current?.MainWindow;
        this.apiRefOutputPath = apiRefOutputPath;
        DataContext = new ExportSummaryViewModel(
            projectTitle,
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
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowApiReferenceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (File.Exists(apiRefOutputPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{apiRefOutputPath}\"",
                UseShellExecute = true
            });
        }

        Close();
    }

    private sealed class ExportSummaryViewModel
    {
        public ExportSummaryViewModel(
            string projectTitle,
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
            ProjectTitle = projectTitle;
            NamespaceCount = namespaceCount;
            TypeCount = typeCount;
            MemberCount = memberCount;
            ClassCount = classCount;
            InterfaceCount = interfaceCount;
            StructCount = structCount;
            EnumCount = enumCount;
            RecordCount = recordCount;
            MissedItemCount = missedItemCount;
            OneFileDocsSyncMessage = oneFileDocsSyncMessage ?? string.Empty;
            SummarySubtitle = BuildSummarySubtitle(OneFileDocsSyncMessage);
        }

        public string ProjectTitle { get; }

        public int NamespaceCount { get; }

        public int TypeCount { get; }

        public int MemberCount { get; }

        public int ClassCount { get; }

        public int InterfaceCount { get; }

        public int StructCount { get; }

        public int EnumCount { get; }

        public int RecordCount { get; }

        public int MissedItemCount { get; }

        public string OneFileDocsSyncMessage { get; }

        public string SummarySubtitle { get; }

        private static string BuildSummarySubtitle(string oneFileDocsSyncMessage)
        {
            if (string.IsNullOrWhiteSpace(oneFileDocsSyncMessage))
            {
                return "Export completed";
            }

            if (oneFileDocsSyncMessage.StartsWith("Success", StringComparison.Ordinal))
            {
                return "Export and upload completed";
            }

            return "Export completed. Upload failed.";
        }
    }
}
