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

        if (dialog.ShowDialog(this) != true)
        {
            FeedbackText.Text = "Salvamento cancelado; nenhum arquivo foi gravado.";
            return;
        }

        string destinationPath;
        try
        {
            destinationPath = Path.GetFullPath(dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            FeedbackText.Text = $"Destino inválido ({exception.GetType().Name}); nenhum arquivo foi gravado.";
            return;
        }

        var saver = new DiagnosticReportFileSaver();
        var destinationExists = File.Exists(destinationPath);
        var overwriteConfirmed = destinationExists
            && AppMessageDialog.Confirm(
                this,
                "Confirmar substituição",
                "Este arquivo já existe. Deseja substituí-lo pelo conteúdo que está visível na prévia?");
        if (destinationExists && !overwriteConfirmed)
        {
            FeedbackText.Text = "Substituição cancelada; o arquivo existente foi preservado.";
            return;
        }

        var result = saver.Save(destinationPath, ReportText.Text, overwriteConfirmed);
        if (result.State == DiagnosticReportSaveState.OverwriteConfirmationRequired
            && AppMessageDialog.Confirm(
                this,
                "Confirmar substituição",
                "O arquivo de destino apareceu durante o salvamento. Deseja substituí-lo pelo conteúdo que está visível na prévia?"))
        {
            result = saver.Save(destinationPath, ReportText.Text, overwriteConfirmed: true);
        }
        else if (result.State == DiagnosticReportSaveState.OverwriteConfirmationRequired)
        {
            FeedbackText.Text = "Substituição cancelada; o arquivo existente foi preservado.";
            return;
        }

        FeedbackText.Text = result.Message;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
