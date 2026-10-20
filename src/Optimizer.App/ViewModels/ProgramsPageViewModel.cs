using System.Collections.ObjectModel;
using Optimizer.Core.Programs;

namespace Optimizer.App.ViewModels;

public sealed class ProgramCatalogRowViewModel : ObservableObject
{
    private bool _isSelected;
    private InstalledProgramMatch _installedState = new(
        InstalledProgramStatus.CheckIncomplete,
        "Ainda não consultado",
        null,
        null,
        ["A leitura local somente leitura ainda não foi solicitada."]);

    public ProgramCatalogRowViewModel(ProgramPackageDefinition package, Action selectionChanged)
    {
        Package = package;
        SelectionChanged = selectionChanged;
    }

    private Action SelectionChanged { get; }
    public ProgramPackageDefinition Package { get; }
    public string PackageId => Package.PackageId;
    public string DisplayName => Package.DisplayName;
    public string Publisher => Package.Publisher;
    public string SourceLabel => $"winget · manifesto revisado v{Package.VerifiedManifestVersion}";
    public string LicenseLabel => $"{Package.LicenseName} · {Package.LicenseUrl}";
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            SelectionChanged();
        }
    }

    public InstalledProgramMatch InstalledState
    {
        get => _installedState;
        set
        {
            if (!SetProperty(ref _installedState, value)) return;
            OnPropertyChanged(nameof(InstalledLabel));
            OnPropertyChanged(nameof(InstalledDetails));
        }
    }

    public string InstalledLabel => InstalledState.Version is null
        ? InstalledState.Label
        : $"{InstalledState.Label} · versão {InstalledState.Version}";
    public string InstalledDetails => InstalledState.Scope is null
        ? string.Join(" ", InstalledState.Reasons)
        : $"Escopo observado: {InstalledState.Scope}. {string.Join(" ", InstalledState.Reasons)}";
}

public sealed record ProgramReviewRow(
    string PackageId,
    string DisplayName,
    string Source,
    string Action,
    string Scope,
    string Agreements,
    string Permissions,
    string Restart,
    string ManifestCaution);

public sealed record ProgramResultRow(string PackageId, string Outcome, string Summary);

public sealed class ProgramsPageViewModel : ObservableObject
{
    private readonly IInstalledProgramInventoryProvider _inventoryProvider;
    private readonly ProgramInstallService _installService;
    private bool _isLoading;
    private bool _isCheckingBlock;
    private string _statusMessage = "Selecione programas e revise a operação. Nenhuma instalação será iniciada nesta etapa.";
    private string? _inventoryWarning;

    public ProgramsPageViewModel(
        ProgramCatalog catalog,
        IInstalledProgramInventoryProvider inventoryProvider,
        ProgramInstallService installService)
    {
        _inventoryProvider = inventoryProvider;
        _installService = installService;
        Packages = new ObservableCollection<ProgramCatalogRowViewModel>(catalog.Packages.Select(package =>
            new ProgramCatalogRowViewModel(package, SelectionChanged)));
        Reviews = [];
        Results = [];
        RefreshInstalledCommand = new AsyncRelayCommand(RefreshInstalledAsync, () => !IsLoading);
        CheckBlockedStatusCommand = new AsyncRelayCommand(CheckBlockedStatusAsync, () => HasSelection && !IsCheckingBlock);
    }

    public ObservableCollection<ProgramCatalogRowViewModel> Packages { get; }
    public ObservableCollection<ProgramReviewRow> Reviews { get; }
    public ObservableCollection<ProgramResultRow> Results { get; }
    public AsyncRelayCommand RefreshInstalledCommand { get; }
    public AsyncRelayCommand CheckBlockedStatusCommand { get; }
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) RefreshInstalledCommand.NotifyCanExecuteChanged(); } }
    public bool IsCheckingBlock { get => _isCheckingBlock; private set { if (SetProperty(ref _isCheckingBlock, value)) CheckBlockedStatusCommand.NotifyCanExecuteChanged(); } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? InventoryWarning { get => _inventoryWarning; private set { if (SetProperty(ref _inventoryWarning, value)) OnPropertyChanged(nameof(HasInventoryWarning)); } }
    public bool HasInventoryWarning => !string.IsNullOrWhiteSpace(InventoryWarning);
    public int SelectedCount => Packages.Count(package => package.IsSelected);
    public string SelectionSummary => $"Selecionados: {SelectedCount}";
    public bool HasSelection => SelectedCount > 0;
    public bool HasNoSelection => !HasSelection;
    public bool HasReviews => Reviews.Count > 0;
    public bool HasResults => Results.Count > 0;
    public bool HasNoResults => !HasResults;

    public async Task RefreshInstalledAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        InventoryWarning = null;
        StatusMessage = "Lendo somente as entradas locais de Programas e Recursos; nenhuma chamada ao WinGet nem instalação será iniciada.";
        try
        {
            var inventory = await Task.Run(_inventoryProvider.ReadInstalledPrograms);
            foreach (var row in Packages)
            {
                row.InstalledState = InstalledProgramMatcher.Match(row.Package, inventory);
            }

            InventoryWarning = inventory.Warnings.Count == 0 ? null : string.Join(" ", inventory.Warnings);
            StatusMessage = $"Leitura local concluída: {inventory.Entries.Count} entradas consultadas. Ausência de correspondência exata não comprova que um programa esteja ausente.";
        }
        catch (Exception exception)
        {
            InventoryWarning = $"A leitura local falhou ({exception.GetType().Name}); o estado instalado permanece desconhecido.";
            StatusMessage = "Verificação incompleta. Nenhuma instalação foi iniciada.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CheckBlockedStatusAsync()
    {
        if (!HasSelection || IsCheckingBlock) return;
        IsCheckingBlock = true;
        try
        {
            var requests = Packages.Where(package => package.IsSelected)
                .Select(package => new ProgramInstallRequest(package.PackageId, null))
                .ToArray();
            var results = await _installService.ExecuteAsync(requests);
            ApplyResults(results);
            StatusMessage = "Bloqueio verificado por pacote. A política padrão impediu qualquer chamada ao executável winget.exe.";
        }
        finally
        {
            IsCheckingBlock = false;
        }
    }

    public void ApplyResults(IEnumerable<ProgramInstallResult> results)
    {
        Results.Clear();
        foreach (var result in results)
        {
            Results.Add(new ProgramResultRow(result.PackageId, FormatOutcome(result.Outcome), result.Summary));
        }
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNoResults));
    }

    private void SelectionChanged()
    {
        RebuildReviews();
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSelection));
        CheckBlockedStatusCommand.NotifyCanExecuteChanged();
    }

    private void RebuildReviews()
    {
        Reviews.Clear();
        foreach (var row in Packages.Where(package => package.IsSelected))
        {
            var package = row.Package;
            Reviews.Add(new ProgramReviewRow(
                package.PackageId,
                package.DisplayName,
                $"{package.SourceName} · repositório público de manifests Windows Package Manager",
                $"Instalar a versão que o WinGet resolver; o catálogo foi conferido em {package.ManifestReviewedOn:yyyy-MM-dd} (manifesto v{package.VerifiedManifestVersion}).",
                $"O manifesto conferido declara escopo {package.DeclaredInstallScope}; o aplicativo não passa --scope e o instalador/WinGet pode determinar o destino conforme os metadados atuais.",
                $"Licença do pacote: {package.LicenseName} ({package.LicenseUrl}). Acordo da origem e termos do pacote não foram carregados/aceitos nesta revisão; precisam ser apresentados e revisados na operação futura. A aceitação WinGet não cobre software opcional agrupado.",
                package.ElevationNote,
                package.RestartNote,
                "Manifesto é uma evidência datada do catálogo, não prova de versão mais recente, disponibilidade ou compatibilidade atual. O catálogo local precisa ser revalidado pelo WinGet antes de qualquer execução."));
        }
        OnPropertyChanged(nameof(HasReviews));
    }

    private static string FormatOutcome(ProgramInstallOutcome outcome) => outcome switch
    {
        ProgramInstallOutcome.ExecutionBlocked => "BLOQUEADA",
        ProgramInstallOutcome.UnknownPackageBlocked => "PACOTE FORA DO CATÁLOGO",
        ProgramInstallOutcome.ConsentRequired => "REVISÃO/ACORDOS PENDENTES",
        ProgramInstallOutcome.ManagerUnavailable => "WINGET INDISPONÍVEL",
        ProgramInstallOutcome.NetworkUnavailable => "WINGET REPORTOU FALTA DE CONECTIVIDADE",
        ProgramInstallOutcome.PackageNotFound => "ID NÃO ENCONTRADO NA ORIGEM",
        ProgramInstallOutcome.ReportedSuccessUnverified => "SUCESSO REPORTADO · NÃO VERIFICADO",
        ProgramInstallOutcome.Failed => "FALHA",
        ProgramInstallOutcome.CancelledOutcomeUnknown => "CANCELADO · RESULTADO PARCIAL POSSÍVEL",
        ProgramInstallOutcome.CancelledBeforeStart => "CANCELADO ANTES DE INICIAR",
        ProgramInstallOutcome.RebootRequired => "REINÍCIO NECESSÁRIO · PENDENTE",
        ProgramInstallOutcome.RebootInitiated => "WINGET REPORTOU REINÍCIO INICIADO",
        ProgramInstallOutcome.NotRunRestartPending => "NÃO INICIADO · REINÍCIO PENDENTE",
        _ => "NÃO EXECUTADO"
    };
}
