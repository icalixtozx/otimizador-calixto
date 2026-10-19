using System.Collections.ObjectModel;
using Optimizer.Core.Inventory;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Profiles;
using Optimizer.Windows;

namespace Optimizer.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IComputerInventoryService _computerInventoryService;
    private object _currentPage;
    private string _selectedNavigationKey = "overview";
    private string _pageTitle = "Visão geral";
    private bool _isScanning;

    public MainWindowViewModel(IComputerInventoryService computerInventoryService, IDriverInventoryService driverInventoryService)
    {
        _computerInventoryService = computerInventoryService;
        Overview = new OverviewPageViewModel();
        Drivers = new DriversPageViewModel(driverInventoryService);
        Profiles = new ProfilesPageViewModel(
            OptimizerProfileCatalog.CreateDefault(),
            new AdjustmentCatalog([]),
            new WindowsVideoIdlePowerAdapter());
        _currentPage = Overview;
        NavigationItems =
        [
            new("overview", "Visão geral", "◧", true, null, true),
            new("drivers", "Drivers", "▦", true, null),
            new("profiles", "Planos", "◉", true, null),
            new("tweaks", "Ajustes", "⌁", false, "Depois"),
            new("games", "Jogos", "◇", false, "Depois"),
            new("network", "Rede", "⌘", false, "Depois"),
            new("history", "Histórico", "↺", true, null),
            new("settings", "Configurações", "⚙", false, "Depois")
        ];
        NavigateCommand = new RelayCommand<string>(Navigate);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsScanning);
    }

    public OverviewPageViewModel Overview { get; }
    public DriversPageViewModel Drivers { get; }
    public ProfilesPageViewModel Profiles { get; }
    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public RelayCommand<string> NavigateCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public object CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }
    public string PageTitle { get => _pageTitle; private set => SetProperty(ref _pageTitle, value); }
    public string SelectedNavigationKey { get => _selectedNavigationKey; private set => SetProperty(ref _selectedNavigationKey, value); }
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                Overview.IsLoading = value;
            }
        }
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
        await Profiles.LoadHistoryAsync();
    }

    private void Navigate(string? key)
    {
        if (key is "overview")
        {
            SelectedNavigationKey = key;
            PageTitle = "Visão geral";
            CurrentPage = Overview;
            UpdateSelectedNavigation(key);
        }
        else if (key is "drivers")
        {
            SelectedNavigationKey = key;
            PageTitle = "Drivers";
            CurrentPage = Drivers;
            UpdateSelectedNavigation(key);
        }
        else if (key is "profiles" or "history")
        {
            SelectedNavigationKey = key;
            Profiles.SelectedSectionIndex = key == "history" ? 1 : 0;
            PageTitle = key == "history" ? "Histórico" : "Planos";
            CurrentPage = Profiles;
            UpdateSelectedNavigation(key);
            _ = Profiles.LoadHistoryAsync();
        }
    }

    private void UpdateSelectedNavigation(string key)
    {
        foreach (var item in NavigationItems) item.IsSelected = item.Key == key;
    }

    private async Task RefreshAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        Overview.ErrorMessage = null;
        Overview.Inventory = null;
        Overview.StatusText = "Analisando hardware e drivers localmente…";

        try
        {
            var computerTask = _computerInventoryService.CollectAsync();
            var driversTask = Drivers.LoadAsync();
            await Task.WhenAll(computerTask, driversTask);

            var result = await computerTask;
            Overview.Inventory = result.Inventory;
            Overview.ErrorMessage = result.ErrorMessage;
            Overview.StatusText = result.Inventory is null
                ? "A análise terminou sem dados de hardware."
                : result.Inventory.Warnings.Count > 0
                    ? "Análise concluída com alguns campos indisponíveis. Nenhuma configuração foi alterada."
                    : "Análise concluída. Nenhuma configuração foi alterada.";
        }
        catch (Exception exception)
        {
            Overview.ErrorMessage = $"Não foi possível concluir a análise ({exception.GetType().Name}).";
            Overview.StatusText = "A análise não foi concluída.";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
