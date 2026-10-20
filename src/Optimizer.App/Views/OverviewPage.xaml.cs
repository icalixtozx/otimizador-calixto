using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
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

    private void OpenGraphicsSettings_Click(object sender, RoutedEventArgs e) =>
        LaunchNativeTool("ms-settings:display-advancedgraphics", "Configurações de gráficos");

    private void OpenTaskManager_Click(object sender, RoutedEventArgs e) =>
        LaunchNativeTool("taskmgr.exe", "Gerenciador de Tarefas");

    private void LaunchNativeTool(string target, string displayName)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                $"Não foi possível abrir {displayName}. Você pode acessá-lo manualmente pelo Windows.\n\n{exception.Message}",
                "Calixto Optimizer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
