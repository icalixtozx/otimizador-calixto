using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Inventory;
using Optimizer.Core.Recovery;

namespace Optimizer.App.ViewModels;

public sealed class SignatureFilterOption(string label, DriverSignatureFilter value)
{
    public string Label { get; } = label;
    public DriverSignatureFilter Value { get; } = value;
    public override string ToString() => Label;
}

public sealed class DriversPageViewModel : ObservableObject, IDisposable
{
    private readonly IDriverInventoryService _inventoryService;
    private readonly LocalDriverPackageAnalyzer _packageAnalyzer;
    private readonly DriverPackageInstallationService _packageInstallationService;
    private IReadOnlyList<DriverInfo> _allDrivers = Array.Empty<DriverInfo>();
    private string _searchText = string.Empty;
    private SignatureFilterOption _selectedFilter;
    private string _state = "NotStarted";
    private string _statusMessage = "Pronto para analisar os drivers deste computador.";
    private string? _errorMessage;
    private string? _exportFeedback;
    private string? _packageImportFeedback;
    private bool _isLoading;
    private bool _isImportingPackage;
    private bool _showCandidateReview;
    private DateTimeOffset? _collectedAtUtc;
    private LocalDriverPackageAnalysisResult? _currentPackageAnalysis;
    private DriverPackageInstallReview? _selectedPackageReview;
    private string? _packageAnalysisSummary;
    private string? _installCaptureFeedback;
    private bool _driverInstallConsequencesAccepted;

    public DriversPageViewModel(
        IDriverInventoryService inventoryService,
        LocalDriverPackageAnalyzer packageAnalyzer,
        DriverPackageInstallationService packageInstallationService)
    {
        _inventoryService = inventoryService;
        _packageAnalyzer = packageAnalyzer;
        _packageInstallationService = packageInstallationService;
        FilterOptions =
        [
            new("Todos", DriverSignatureFilter.All),
            new("Assinados", DriverSignatureFilter.Signed),
            new("Sem assinatura", DriverSignatureFilter.Unsigned),
            new("Assinatura desconhecida", DriverSignatureFilter.Unknown)
        ];
        _selectedFilter = FilterOptions[0];
        ReloadCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        ExportCommand = new RelayCommand(ExportFilteredDrivers, () => FilteredDrivers.Count > 0);
        ToggleCandidateReviewCommand = new RelayCommand(ToggleCandidateReview);
        ImportLocalPackageCommand = new AsyncRelayCommand(ImportLocalPackageAsync, () => !IsImportingPackage);
        OpenOfficialSourceCommand = new RelayCommand<DriverPackageAssessment>(OpenOfficialSource);
        PreparePackageInstallPreviewCommand = new RelayCommand<DriverPackageInfCandidate>(PreparePackageInstallPreview);
        RecordPackageInstallConsentAndCaptureCommand = new AsyncRelayCommand(
            RecordPackageInstallConsentAndCaptureAsync,
            () => !IsImportingPackage && SelectedPackageReview is not null && DriverInstallConsequencesAccepted);
    }

    public DriverInventoryResult? LastResult { get; private set; }
    public ObservableCollection<SignatureFilterOption> FilterOptions { get; }
    public ObservableCollection<DriverInfo> FilteredDrivers { get; } = [];
    public ObservableCollection<DriverPackageAssessment> CandidateReviews { get; } = [];
    public ObservableCollection<DriverPackageInfCandidate> LocalPackageCandidates { get; } = [];
    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ToggleCandidateReviewCommand { get; }
    public AsyncRelayCommand ImportLocalPackageCommand { get; }
    public RelayCommand<DriverPackageAssessment> OpenOfficialSourceCommand { get; }
    public RelayCommand<DriverPackageInfCandidate> PreparePackageInstallPreviewCommand { get; }
    public AsyncRelayCommand RecordPackageInstallConsentAndCaptureCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public SignatureFilterOption SelectedFilter
    {
        get => _selectedFilter;
        set { if (SetProperty(ref _selectedFilter, value)) ApplyFilter(); }
    }

    public string State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsEmptyState));
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(ShowDriverGrid));
                OnPropertyChanged(nameof(ShowInventoryError));
                OnPropertyChanged(nameof(ShowInventoryNoMatches));
                OnPropertyChanged(nameof(ShowInventoryEmptyState));
                OnPropertyChanged(nameof(ShowInventoryGrid));
                OnPropertyChanged(nameof(CandidateInventoryStatus));
            }
        }
    }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ShowInventoryError));
            }
        }
    }
    public string? ExportFeedback { get => _exportFeedback; private set => SetProperty(ref _exportFeedback, value); }
    public string? PackageImportFeedback
    {
        get => _packageImportFeedback;
        private set
        {
            if (SetProperty(ref _packageImportFeedback, value))
                OnPropertyChanged(nameof(HasPackageImportFeedback));
        }
    }
    public bool HasPackageImportFeedback => !string.IsNullOrWhiteSpace(PackageImportFeedback);
    public string? PackageAnalysisSummary
    {
        get => _packageAnalysisSummary;
        private set
        {
            if (SetProperty(ref _packageAnalysisSummary, value))
                OnPropertyChanged(nameof(HasPackageAnalysisSummary));
        }
    }
    public bool HasPackageAnalysisSummary => !string.IsNullOrWhiteSpace(PackageAnalysisSummary);
    public DriverPackageInstallReview? SelectedPackageReview
    {
        get => _selectedPackageReview;
        private set
        {
            if (SetProperty(ref _selectedPackageReview, value))
            {
                OnPropertyChanged(nameof(HasSelectedPackageReview));
                OnPropertyChanged(nameof(CanRecordPackageInstallConsentAndCapture));
                OnPropertyChanged(nameof(SelectedPackageReviewTarget));
                OnPropertyChanged(nameof(SelectedPackageReviewCurrentDriver));
                OnPropertyChanged(nameof(SelectedPackageReviewPackage));
                RecordPackageInstallConsentAndCaptureCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public bool HasSelectedPackageReview => SelectedPackageReview is not null;
    public bool CanRecordPackageInstallConsentAndCapture => !IsImportingPackage &&
        SelectedPackageReview is not null && DriverInstallConsequencesAccepted;
    public string SelectedPackageReviewTarget => SelectedPackageReview is null
        ? string.Empty : SelectedPackageReview.Candidate.TargetSummary;
    public string SelectedPackageReviewCurrentDriver => SelectedPackageReview is null
        ? string.Empty : $"{SelectedPackageReview.CurrentDriver.Provider ?? "Fornecedor não informado"} · {SelectedPackageReview.CurrentDriver.Version ?? "versão não informada"} · INF {SelectedPackageReview.CurrentDriver.InfName ?? "não informado"}";
    public string SelectedPackageReviewPackage => SelectedPackageReview is null
        ? string.Empty : $"{SelectedPackageReview.Candidate.SourceFileName} · {SelectedPackageReview.Candidate.InfName} · versão declarada {SelectedPackageReview.Candidate.DriverVersion ?? "não informada"} · {SelectedPackageReview.Candidate.SourceHashLabel} {SelectedPackageReview.Candidate.SourceSha256}";
    public string? InstallCaptureFeedback
    {
        get => _installCaptureFeedback;
        private set
        {
            if (SetProperty(ref _installCaptureFeedback, value))
                OnPropertyChanged(nameof(HasInstallCaptureFeedback));
        }
    }
    public bool HasInstallCaptureFeedback => !string.IsNullOrWhiteSpace(InstallCaptureFeedback);
    public bool DriverInstallConsequencesAccepted
    {
        get => _driverInstallConsequencesAccepted;
        set
        {
            if (SetProperty(ref _driverInstallConsequencesAccepted, value))
            {
                OnPropertyChanged(nameof(CanRecordPackageInstallConsentAndCapture));
                RecordPackageInstallConsentAndCaptureCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public bool ShowCandidateReview
    {
        get => _showCandidateReview;
        private set
        {
            if (SetProperty(ref _showCandidateReview, value))
            {
                OnPropertyChanged(nameof(ShowInventoryFilters));
                OnPropertyChanged(nameof(ShowInventoryGrid));
                OnPropertyChanged(nameof(ShowInventoryError));
                OnPropertyChanged(nameof(ShowInventoryNoMatches));
                OnPropertyChanged(nameof(ShowInventoryEmptyState));
                OnPropertyChanged(nameof(CandidateReviewButtonLabel));
            }
        }
    }
    public bool ShowInventoryFilters => !ShowCandidateReview;
    public bool ShowInventoryGrid => ShowDriverGrid && !ShowCandidateReview;
    public bool ShowInventoryError => HasError && !ShowCandidateReview;
    public bool ShowInventoryNoMatches => HasNoMatches && !ShowCandidateReview;
    public bool ShowInventoryEmptyState => IsEmptyState && !ShowCandidateReview;
    public string CandidateReviewButtonLabel => ShowCandidateReview ? "Voltar ao inventário" : "Candidatos oficiais";
    public string CandidateInventoryStatus => State switch
    {
        "Success" => "Correspondência calculada a partir da leitura PnP local concluída.",
        "Partial" => "Leitura PnP parcial: ausência de correspondência pode significar que os IDs não foram retornados.",
        "Error" => "Inventário local indisponível: a compatibilidade do dispositivo não pode ser confirmada nesta leitura.",
        "Empty" => "A consulta local não retornou dispositivos; nenhum candidato foi associado.",
        "Loading" => "Lendo IDs Plug and Play localmente…",
        _ => "Aguarde a leitura local antes de interpretar a correspondência."
    };
    public bool IsImportingPackage
    {
        get => _isImportingPackage;
        private set
        {
            if (SetProperty(ref _isImportingPackage, value))
            {
                OnPropertyChanged(nameof(CanRecordPackageInstallConsentAndCapture));
                ImportLocalPackageCommand.NotifyCanExecuteChanged();
            RecordPackageInstallConsentAndCaptureCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsNotLoading));
                OnPropertyChanged(nameof(IsEmptyState));
                OnPropertyChanged(nameof(ShowDriverGrid));
                OnPropertyChanged(nameof(ShowInventoryGrid));
                OnPropertyChanged(nameof(ShowInventoryEmptyState));
                ReloadCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotLoading => !IsLoading;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasDrivers => FilteredDrivers.Count > 0;
    public bool HasNoMatches => _allDrivers.Count > 0 && FilteredDrivers.Count == 0;
    public bool HasWarnings => State == "Partial";
    public bool IsEmptyState => !IsLoading && (State == "Empty" || (State == "Partial" && _allDrivers.Count == 0));
    public bool ShowDriverGrid => HasDrivers && !IsLoading && !HasError;
    public string DriverCountLabel => _allDrivers.Count == 1 ? "1 driver encontrado" : $"{_allDrivers.Count:N0} drivers encontrados";
    public string CollectedAtLabel => _collectedAtUtc is null
        ? "Ainda não analisado"
        : $"Analisado em {_collectedAtUtc.Value.ToLocalTime():dd/MM/yyyy 'às' HH:mm}";

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        State = "Loading";
        ErrorMessage = null;
        ExportFeedback = null;
        _allDrivers = Array.Empty<DriverInfo>();
        FilteredDrivers.Clear();
        CandidateReviews.Clear();
        OnPropertyChanged(nameof(HasDrivers));
        OnPropertyChanged(nameof(HasNoMatches));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(ShowDriverGrid));
        OnPropertyChanged(nameof(DriverCountLabel));
        ExportCommand.NotifyCanExecuteChanged();
        StatusMessage = "Lendo informações locais de drivers e dispositivos…";

        try
        {
            var result = await _inventoryService.CollectAsync();
            _allDrivers = result.Drivers;
            CandidateReviews.Clear();
            if (result.Availability != InventoryAvailability.Failed)
            {
                foreach (var review in DriverPackageCandidateCatalog.Assess(_allDrivers))
                    CandidateReviews.Add(review);
            }
            _collectedAtUtc = result.CollectedAtUtc;
            OnPropertyChanged(nameof(CollectedAtLabel));
            OnPropertyChanged(nameof(DriverCountLabel));

            if (result.Availability == InventoryAvailability.Failed)
            {
                State = "Error";
                ErrorMessage = result.ErrorMessage ?? "O inventário de drivers falhou.";
                StatusMessage = "A análise não foi concluída.";
            }
            else if (result.Availability == InventoryAvailability.Partial)
            {
                State = "Partial";
                StatusMessage = result.Warnings.FirstOrDefault() ?? "A análise foi concluída com campos indisponíveis.";
            }
            else if (_allDrivers.Count == 0)
            {
                State = "Empty";
                StatusMessage = "A consulta foi concluída, mas o Windows não retornou drivers.";
            }
            else
            {
                State = "Success";
                StatusMessage = "Inventário concluído. Nenhuma configuração foi alterada.";
            }

            ApplyFilter();
            OnPropertyChanged(nameof(IsEmptyState));
            OnPropertyChanged(nameof(ShowDriverGrid));
        }
        catch (OperationCanceledException)
        {
            State = "Error";
            ErrorMessage = "A análise foi cancelada.";
            StatusMessage = "A análise não foi concluída.";
        }
        catch (Exception exception)
        {
            State = "Error";
            ErrorMessage = $"Falha ao consultar o inventário ({exception.GetType().Name}).";
            StatusMessage = "A análise não foi concluída.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var filtered = DriverCatalog.Filter(_allDrivers, SearchText, SelectedFilter.Value);
        FilteredDrivers.Clear();
        foreach (var driver in filtered) FilteredDrivers.Add(driver);
        OnPropertyChanged(nameof(HasDrivers));
        OnPropertyChanged(nameof(HasNoMatches));
        OnPropertyChanged(nameof(ShowInventoryNoMatches));
        OnPropertyChanged(nameof(ShowDriverGrid));
        OnPropertyChanged(nameof(ShowInventoryGrid));
        ExportCommand.NotifyCanExecuteChanged();
    }

    private void ExportFilteredDrivers()
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".csv",
            FileName = $"drivers-{DateTime.Now:yyyy-MM-dd}.csv",
            Filter = "Arquivo CSV (*.csv)|*.csv",
            Title = "Exportar inventário de drivers"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, DriverCatalog.ToSanitizedCsv(FilteredDrivers), new System.Text.UTF8Encoding(true));
            ExportFeedback = "Arquivo exportado. Identificadores internos de dispositivos foram omitidos.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ExportFeedback = $"Não foi possível salvar o arquivo ({exception.GetType().Name}).";
        }
    }

    private void ToggleCandidateReview() => ShowCandidateReview = !ShowCandidateReview;

    private async Task ImportLocalPackageAsync()
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Pacote de driver (INF ou ZIP)|*.inf;*.zip|Arquivo INF (*.inf)|*.inf|Pacote ZIP (*.zip)|*.zip",
            Title = "Analisar pacote de driver local sem executar instaladores"
        };

        if (dialog.ShowDialog() != true) return;

        IsImportingPackage = true;
        PackageImportFeedback = "Analisando INF/ZIP em segundo plano. Instaladores e scripts não serão executados…";
        PackageAnalysisSummary = null;
        InstallCaptureFeedback = null;
        SelectedPackageReview = null;
        DriverInstallConsequencesAccepted = false;
        LocalPackageCandidates.Clear();
        _currentPackageAnalysis?.Dispose();
        _currentPackageAnalysis = null;
        try
        {
            var devices = LastResult?.Availability == InventoryAvailability.Available
                ? LastResult.Drivers
                : Array.Empty<DriverInfo>();
            var analysis = await _packageAnalyzer.AnalyzeAsync(
                dialog.FileName, devices, GetCurrentPackagePlatform());
            _currentPackageAnalysis = analysis;
            foreach (var candidate in analysis.Candidates) LocalPackageCandidates.Add(candidate);
            var identity = DriverPackageCandidateCatalog.IdentifyArchive(analysis.SourceFileName, analysis.SourceSha256);
            var recognized = identity.RecognizedCandidate is null
                ? "Hash não corresponde a um arquivo ASUS previamente revisado."
                : $"Hash reconhecido como os mesmos bytes do candidato {identity.RecognizedCandidate.PackageTitle}; isso não prova necessidade nem substitui a análise genérica.";
            var sourceHashLabel = analysis.SourceFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? "SHA-256 do ZIP" : "SHA-256 do INF selecionado";
            PackageAnalysisSummary = $"{analysis.SourceFileName} · {sourceHashLabel} {analysis.SourceSha256}. {analysis.IntegritySummary} {recognized}";
            PackageImportFeedback = analysis.Candidates.Count == 0
                ? "Nenhum INF foi encontrado para análise. Nenhum instalador foi executado."
                : $"Análise concluída: {analysis.Candidates.Count} INF(s). Leia o resultado e os motivos de cada um; somente candidato compatível exato pode gerar prévia.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidDataException or ArgumentException)
        {
            PackageImportFeedback = $"Análise recusada ou incompleta ({exception.GetType().Name}): {exception.Message} Nenhum instalador foi executado e nenhum driver/configuração do Windows foi alterado.";
        }
        finally
        {
            IsImportingPackage = false;
        }
    }

    private void PreparePackageInstallPreview(DriverPackageInfCandidate? candidate)
    {
        if (candidate is null) return;
        if (LastResult?.Availability != InventoryAvailability.Available)
        {
            InstallCaptureFeedback = "A leitura Plug and Play não está completa; atualize o inventário antes de preparar um alvo.";
            return;
        }
        if (!candidate.CanPrepareInstallation || candidate.DeviceMatches.Count != 1)
        {
            InstallCaptureFeedback = "Este resultado é incompatível, ambíguo ou incompleto; não pode receber prévia de instalação.";
            return;
        }

        try
        {
            SelectedPackageReview = _packageInstallationService.CreateReview(
                candidate, candidate.DeviceMatches[0].Device, GetCurrentAdjustmentPlatform());
            DriverInstallConsequencesAccepted = false;
            InstallCaptureFeedback = "Prévia local criada. Nenhuma captura ou instalação foi registrada; execução real permanece bloqueada nesta etapa.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException or ArgumentException)
        {
            SelectedPackageReview = null;
            InstallCaptureFeedback = $"Não foi possível preparar a prévia ({exception.GetType().Name}): {exception.Message}";
        }
    }

    private async Task RecordPackageInstallConsentAndCaptureAsync()
    {
        if (SelectedPackageReview is null || !DriverInstallConsequencesAccepted) return;
        IsImportingPackage = true;
        InstallCaptureFeedback = "Relendo o dispositivo e os arquivos; o consentimento e a captura serão gravados apenas se o estado ainda corresponder à prévia…";
        try
        {
            var entry = await _packageInstallationService.CaptureAsync(
                SelectedPackageReview, consequencesAccepted: true);
            InstallCaptureFeedback = entry.State switch
            {
                JournalOperationState.SnapshotsCaptured => $"Consentimento específico e captura somente leitura salvos no Histórico local · {entry.OperationId.ToString("N")[..8].ToUpperInvariant()}. Driver Store não alterado; driver não instalado; execução real bloqueada.",
                JournalOperationState.Conflict => "O estado do dispositivo divergiu; conflito registrado no Histórico. Nenhum driver foi instalado.",
                JournalOperationState.Failed or JournalOperationState.Interrupted => $"Captura não concluída ({entry.State}); consulte o Histórico. Nenhuma instalação foi tentada.",
                _ => $"Estado registrado: {entry.State}. Nenhuma instalação foi tentada."
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            InstallCaptureFeedback = $"Consentimento/captura não foram confirmados ({exception.GetType().Name}). Nenhuma instalação foi tentada.";
        }
        finally
        {
            IsImportingPackage = false;
        }
    }

    private static DriverPackageTargetPlatform GetCurrentPackagePlatform()
    {
        var version = OperatingSystem.IsWindows() ? Environment.OSVersion.Version : new Version(0, 0);
        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => string.Empty
        };
        return new(architecture, version.Major, version.Minor, version.Build);
    }

    private static PlatformDescriptor GetCurrentAdjustmentPlatform()
    {
        var packagePlatform = GetCurrentPackagePlatform();
        var architecture = packagePlatform.Architecture switch { "amd64" => "x64", "x86" => "x86", "arm64" => "arm64", _ => string.Empty };
        return new(OperatingSystem.IsWindows(), architecture, packagePlatform.WindowsBuild);
    }

    public void Dispose()
    {
        _currentPackageAnalysis?.Dispose();
        _currentPackageAnalysis = null;
    }

    private void OpenOfficialSource(DriverPackageAssessment? assessment)
    {
        if (assessment is null || !Uri.TryCreate(assessment.Candidate.OfficialSupportUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Host != "www.asus.com")
        {
            PackageImportFeedback = "O endereço oficial não pôde ser validado.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            PackageImportFeedback = $"Não foi possível abrir a página oficial ({exception.GetType().Name}).";
        }
    }
}
