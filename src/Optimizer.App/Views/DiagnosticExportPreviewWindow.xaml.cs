using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

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
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            FileName = $"diagnostico-{DateTime.Now:yyyy-MM-dd}.json",
            Filter = "Relatório JSON (*.json)|*.json",
            Title = "Salvar relatório diagnóstico revisado"
        };

        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, ReportText.Text, new UTF8Encoding(false));
            FeedbackText.Text = "Arquivo salvo no local escolhido. Revise antes de compartilhar.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            FeedbackText.Text = $"Não foi possível salvar o arquivo ({exception.GetType().Name}).";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
