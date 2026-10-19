using System.Windows;
using Optimizer.App.ViewModels;
using Optimizer.Windows;

namespace Optimizer.App;

public partial class MainWindow : Window
{
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(
            new WmiComputerInventoryService(),
            new WmiDriverInventoryService());
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await ((MainWindowViewModel)DataContext).InitializeAsync();
    }
}
