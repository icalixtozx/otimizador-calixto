using System.Windows;
using System.Windows.Controls;
using Optimizer.App.ViewModels;

namespace Optimizer.App.Views;

public partial class OverviewPage : UserControl
{
    public OverviewPage() => InitializeComponent();

    private void PreviewDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OverviewPageViewModel viewModel) return;
        var preview = new DiagnosticExportPreviewWindow(viewModel.SanitizedDiagnosticJson)
        {
            Owner = Window.GetWindow(this)
        };
        preview.ShowDialog();
    }
}
