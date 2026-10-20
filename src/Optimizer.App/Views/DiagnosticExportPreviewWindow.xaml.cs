using System.IO;
using System.Windows;
using Microsoft.Win32;
using Optimizer.Core.Inventory;

namespace Optimizer.App.Views;

public partial class DiagnosticExportPreviewWindow : Window
{
    public DiagnosticExportPreviewWindow(string json)
    {
        InitializeComponent();
        ReportText.Text = json;
        ReportText.CaretIndex = 0;
        ReportText.ScrollToHome();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        FeedbackText.Text = string.Empty;
        string? overwriteConfirmedPath = null;
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            FileName = $"diagnostico-{DateTime.Now:yyyy-MM-dd}.json",
            Filter = "Relatório JSON (*.json)|*.json",
            Title = "Salvar relatório diagnóstico revisado",
            CheckPathExists = true,
            OverwritePrompt = false,
            ValidateNames = true
        };
        dialog.FileOk += (_, eventArgs) =>
        {
            string selectedPath;
            try
            {
                selectedPath = Path.GetFullPath(dialog.FileName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                eventArgs.Cancel = true;
                FeedbackText.Text = $"Destino inválido ({exception.GetType().Name}); nenhum arquivo foi gravado.";
                return;
            }

            if (!File.Exists(selectedPath))
            {
                overwriteConfirmedPath = null;
                return;
            }

            var confirmation = MessageBox.Show(
                this,
                "Este arquivo já existe. Deseja substituí-lo pelo conteúdo que está visível na prévia?",
                "Confirmar substituição",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation == MessageBoxResult.Yes)
            {
                overwriteConfirmedPath = selectedPath;
                return;
            }

            eventArgs.Cancel = true;
            FeedbackText.Text = "Substituição cancelada; o arquivo existente foi preservado.";
        };

        if (dialog.ShowDialog(this) != true)
        {
            if (string.IsNullOrWhiteSpace(FeedbackText.Text))
                FeedbackText.Text = "Salvamento cancelado; nenhum arquivo foi gravado.";
            return;
        }

        var destinationPath = Path.GetFullPath(dialog.FileName);
        var result = new DiagnosticReportFileSaver().Save(
            destinationPath,
            ReportText.Text,
            overwriteConfirmed: string.Equals(destinationPath, overwriteConfirmedPath, StringComparison.OrdinalIgnoreCase));
        FeedbackText.Text = result.Message;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
