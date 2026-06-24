using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CSharpApiExtractorGUI.ViewModels;

namespace CSharpApiExtractorGUI;

public partial class ExtractorWindow : Window
{
    public ExtractorWindow()
    {
        InitializeComponent();
        ProjectSettingsScrollViewer.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(ProjectSettingsScrollViewer_OnPreviewMouseWheel), true);
        DataContext = new MainViewModel();
    }

    private void ProjectSettingsScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
