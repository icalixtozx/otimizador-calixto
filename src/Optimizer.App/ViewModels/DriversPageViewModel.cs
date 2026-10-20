using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;
using Optimizer.Core.Inventory;

namespace Optimizer.App.ViewModels;

public sealed class SignatureFilterOption(string label, DriverSignatureFilter value)
{
    public string Label { get; } = label;
    public DriverSignatureFilter Value { get; } = value;
    public override string ToString() => Label;
}

public sealed class DriversPageViewModel : ObservableObject
{
    private readonly IDriverInventoryService _inventoryService;
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

    public DriversPageViewModel(IDriverInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
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
    }

    public DriverInventoryResult? LastResult { get; private set; }
    public ObservableCollection<SignatureFilterOption> FilterOptions { get; }
    public ObservableCollection<DriverInfo> FilteredDrivers { get; } = [];
    public ObservableCollection<DriverPackageAssessment> CandidateReviews { get; } = [];
    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ToggleCandidateReviewCommand { get; }
    public AsyncRelayCommand ImportLocalPackageCommand { get; }
    public RelayCommand<DriverPackageAssessment> OpenOfficialSourceCommand { get; }

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
                ImportLocalPackageCommand.NotifyCanExecuteChanged();
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
            Filter = "Pacote ZIP (*.zip)|*.zip|Todos os arquivos (*.*)|*.*",
            Title = "Conferir um pacote de driver local (somente leitura)"
        };

        if (dialog.ShowDialog() != true) return;

        IsImportingPackage = true;
        PackageImportFeedback = "Lendo o ZIP local e calculando SHA-256. Nenhum conteúdo será extraído ou executado…";
        try
        {
            await using var stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
            var digest = await SHA256.HashDataAsync(stream);
            var result = DriverPackageCandidateCatalog.IdentifyArchive(
                Path.GetFileName(dialog.FileName), Convert.ToHexString(digest));
            PackageImportFeedback = $"{result.FileName}: SHA-256 {result.Sha256}. {result.Status}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            PackageImportFeedback = $"Não foi possível ler o pacote local ({exception.GetType().Name}). Nenhum arquivo foi alterado.";
        }
        finally
        {
            IsImportingPackage = false;
        }
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
