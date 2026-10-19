using System.Windows;
using System.Windows.Controls;

namespace Optimizer.App.Views;

public partial class ProfilesPage : UserControl
{
    public ProfilesPage() => InitializeComponent();

    private async void HistoryTab_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ProfilesPageViewModel viewModel)
            await viewModel.LoadHistoryAsync();
    }
}
