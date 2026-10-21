using System.Windows;

namespace Optimizer.App.Views;

public partial class AppMessageDialog : Window
{
    public AppMessageDialog(string title, string message, bool isConfirmation)
    {
        InitializeComponent();
        Title = title;
        DialogTitleText.Text = title;
        DialogMessageText.Text = message;
        ConfirmButton.Content = isConfirmation ? "Sim, substituir" : "OK";
        CancelButton.Visibility = isConfirmation ? Visibility.Visible : Visibility.Collapsed;
        if (!isConfirmation)
            CancelButton.IsCancel = false;
    }

    public static bool Confirm(Window? owner, string title, string message)
    {
        var dialog = new AppMessageDialog(title, message, isConfirmation: true);
        if (owner is not null) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    public static void ShowInformation(Window? owner, string title, string message)
    {
        var dialog = new AppMessageDialog(title, message, isConfirmation: false);
        if (owner is not null) dialog.Owner = owner;
        _ = dialog.ShowDialog();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
