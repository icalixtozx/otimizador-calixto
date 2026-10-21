using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Optimizer.Core.Inventory;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Profiles;
using Optimizer.Core.Consent;
using Optimizer.Core.Processes;
using Optimizer.Core.Recovery;
using Optimizer.Core.Registry;
using Optimizer.Core.Programs;
using Optimizer.Windows;

namespace Optimizer.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly string InformationalVersion =
        typeof(MainWindowViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
    private static readonly string SourceCommit = ReadSourceCommit(InformationalVersion);

    private readonly IComputerInventoryService _computerInventoryService;
    private readonly IPowerPlanInventoryService _powerPlanInventoryService;
    private object _currentPage;
    private string _selectedNavigationKey = "overview";
    private string _pageTitle = "Visão geral";
    private bool _isScanning;

    public string BuildVersionLabel => "PREVIEW R6 · v0.6.0 experimental";
    public string BuildCommitLabel => SourceCommit == "unknown" ? "commit indisponível" : $"commit {SourceCommit[..Math.Min(7, SourceCommit.Length)]}";
    public string BuildCommitFull => SourceCommit == "unknown" ? "Commit de origem não registrado nos metadados deste executável." : SourceCommit;
    public string WindowTitle => $"Calixto Optimizer · Preview R6 · {BuildCommitLabel}";

    public MainWindowViewModel(IComputerInventoryService computerInventoryService, IDriverInventoryService driverInventoryService, IPowerPlanInventoryService powerPlanInventoryService)
    {
        _computerInventoryService = computerInventoryService;
        _powerPlanInventoryService = powerPlanInventoryService;
        Overview = new OverviewPageViewModel();
        var historyDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CalixtoOptimizer",
            "History");
        var sharedJournalStore = new JsonOperationJournalStore(historyDirectory);
        var sharedConsent = new PlanConsentService();
        var sharedJournal = new OperationJournalCoordinator(sharedConsent);
        var driverSignatureVerifier = new WindowsDriverPackageSignatureVerifier();
        Drivers = new DriversPageViewModel(
            driverInventoryService,
            new LocalDriverPackageAnalyzer(driverSignatureVerifier, new WindowsSystemInfDependencyResolver()),
            new DriverPackageInstallationService(
                driverInventoryService,
                new WindowsDirectedDriverPackageInstaller(),
                driverSignatureVerifier,
                sharedJournalStore,
                sharedConsent,
                sharedJournal,
                new BlockedDriverPackageMutationPolicy("Staging e associação reais de drivers estão bloqueados nesta etapa; nenhuma API de mutação será chamada.")));
        Profiles = new ProfilesPageViewModel(
            OptimizerProfileCatalog.CreateDefault(),
            new AdjustmentCatalog([]),
            new WindowsVideoIdlePowerAdapter(),
            new WindowsPowerModeAdapter());
        Network = new NetworkPageViewModel(new Optimizer.Core.Network.NetworkDiagnosticsService(new WindowsNetworkDiagnosticsAdapter()));
        var processConsent = new PlanConsentService();
        var processJournal = new OperationJournalCoordinator(processConsent);
        Processes = new ProcessesPageViewModel(new ProcessPriorityOperationService(
            new WindowsProcessPriorityAdapter(),
            sharedJournalStore,
            processConsent,
            processJournal));
        var registryAdapter = new WindowsRegistryPreferenceAdapter();
        Registry = new RegistryPageViewModel(new RegistryPreferenceOperationService(
            registryAdapter,
            new WindowsRegistryManagementInspector(),
            sharedJournalStore,
            processConsent,
            processJournal));
        var programCatalog = ProgramCatalog.CreateDefault();
        Programs = new ProgramsPageViewModel(
            programCatalog,
            new WindowsInstalledProgramInventoryProvider(),
            new ProgramInstallService(
                programCatalog,
                new WindowsWinGetInstallRunner(),
                new BlockedProgramInstallExecutionPolicy("Instalações reais de programas estão bloqueadas nesta etapa; nenhum processo winget será iniciado.")),
            new WindowsWinGetSourcePackageStatusProvider());
        _currentPage = Overview;
        NavigationItems =
        [
            new("overview", "Visão geral", "◧", true, null, true),
            new("drivers", "Drivers", "▦", true, null),
            new("programs", "Programas", "□", true, null),
            new("profiles", "Planos", "◉", true, null),
            new("tweaks", "Ajustes", "⌁", false, "Depois"),
            new("games", "Jogos", "◇", false, "Depois"),
            new("network", "Rede", "⌘", true, null),
            new("processes", "Processos", "≋", true, null),
            new("registry", "Registro", "⌗", true, null),
            new("history", "Histórico", "↺", true, null),
            new("settings", "Configurações", "⚙", false, "Depois")
        ];
        NavigateCommand = new RelayCommand<string>(Navigate);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsScanning);
    }

    public OverviewPageViewModel Overview { get; }
    public DriversPageViewModel Drivers { get; }
    public ProfilesPageViewModel Profiles { get; }
    public NetworkPageViewModel Network { get; }
    public ProcessesPageViewModel Processes { get; }
    public RegistryPageViewModel Registry { get; }
    public ProgramsPageViewModel Programs { get; }
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
        else if (key is "programs")
        {
            SelectedNavigationKey = key;
            PageTitle = "Programas pós-formatação";
            CurrentPage = Programs;
            UpdateSelectedNavigation(key);
            _ = Programs.LoadWinGetSourceStatusAsync();
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
        else if (key is "network")
        {
            SelectedNavigationKey = key;
            PageTitle = "Rede";
            CurrentPage = Network;
            UpdateSelectedNavigation(key);
            _ = Network.LoadAdaptersAsync();
        }
        else if (key is "processes")
        {
            SelectedNavigationKey = key;
            PageTitle = "Processos";
            CurrentPage = Processes;
            UpdateSelectedNavigation(key);
            _ = Processes.LoadProcessesAsync();
            _ = Processes.LoadHistoryAsync();
        }
        else if (key is "registry")
        {
            SelectedNavigationKey = key;
            PageTitle = "Registro e políticas";
            CurrentPage = Registry;
            UpdateSelectedNavigation(key);
            _ = Registry.LoadHistoryAsync();
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
        Overview.DriverInventoryResult = null;
        Overview.PowerPlanInventoryResult = null;
        Overview.StatusText = "Analisando hardware e drivers localmente…";

        try
        {
            var computerTask = _computerInventoryService.CollectAsync();
            var driversTask = Drivers.LoadAsync();
            var powerPlansTask = Task.Run(() => _powerPlanInventoryService.Collect());
            await Task.WhenAll(computerTask, driversTask, powerPlansTask);

            var result = await computerTask;
            Overview.Inventory = result.Inventory;
            Overview.DriverInventoryResult = Drivers.LastResult;
            Overview.PowerPlanInventoryResult = await powerPlansTask;
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

    private static string ReadSourceCommit(string informationalVersion)
    {
        var separator = informationalVersion.LastIndexOf('+');
        if (separator < 0 || separator == informationalVersion.Length - 1) return "unknown";
        var revision = informationalVersion[(separator + 1)..];
        return revision.All(Uri.IsHexDigit) ? revision : "unknown";
    }
}
