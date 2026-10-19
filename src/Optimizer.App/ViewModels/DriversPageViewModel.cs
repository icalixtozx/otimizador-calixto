using System.Collections.ObjectModel;
using System.IO;
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
    private bool _isLoading;
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
    }

    public DriverInventoryResult? LastResult { get; private set; }
    public ObservableCollection<SignatureFilterOption> FilterOptions { get; }
    public ObservableCollection<DriverInfo> FilteredDrivers { get; } = [];
    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand ExportCommand { get; }

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
            }
        }
    }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public string? ExportFeedback { get => _exportFeedback; private set => SetProperty(ref _exportFeedback, value); }
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
        OnPropertyChanged(nameof(ShowDriverGrid));
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
}
