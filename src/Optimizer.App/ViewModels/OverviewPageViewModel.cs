using Optimizer.Core.Inventory;

namespace Optimizer.App.ViewModels;

public sealed class OverviewPageViewModel : ObservableObject
{
    private ComputerInventory? _inventory;
    private bool _isLoading;
    private string _statusText = "A análise ainda não foi executada.";
    private string? _errorMessage;
    private DriverInventoryResult? _driverInventoryResult;
    private PowerPlanInventoryResult? _powerPlanInventoryResult;

    public ComputerInventory? Inventory
    {
        get => _inventory;
        set
        {
            if (SetProperty(ref _inventory, value))
            {
                OnPropertyChanged(nameof(WindowsLabel));
                OnPropertyChanged(nameof(SanitizedDiagnosticJson));
                OnPropertyChanged(nameof(HasDiagnosticData));
                OnPropertyChanged(nameof(ProcessorLabel));
                OnPropertyChanged(nameof(GraphicsLabel));
                OnPropertyChanged(nameof(MemoryLabel));
                OnPropertyChanged(nameof(MotherboardLabel));
                OnPropertyChanged(nameof(StorageLabel));
                OnPropertyChanged(nameof(NetworkLabel));
                OnPropertyChanged(nameof(HasWarnings));
            }
        }
    }

    public DriverInventoryResult? DriverInventoryResult
    {
        get => _driverInventoryResult;
        set
        {
            if (SetProperty(ref _driverInventoryResult, value))
            {
                OnPropertyChanged(nameof(SanitizedDiagnosticJson));
                OnPropertyChanged(nameof(HasDiagnosticData));
                OnPropertyChanged(nameof(HasDiagnosticData));
            }
        }
    }

    public PowerPlanInventoryResult? PowerPlanInventoryResult
    {
        get => _powerPlanInventoryResult;
        set
        {
            if (SetProperty(ref _powerPlanInventoryResult, value))
            {
                OnPropertyChanged(nameof(PowerPlans));
                OnPropertyChanged(nameof(PowerPlanStatusText));
                OnPropertyChanged(nameof(HasPowerPlans));
                OnPropertyChanged(nameof(HasPowerPlanWarnings));
                OnPropertyChanged(nameof(SanitizedDiagnosticJson));
                OnPropertyChanged(nameof(HasDiagnosticData));
            }
        }
    }

    public IReadOnlyList<PowerPlanInfo> PowerPlans => PowerPlanInventoryResult?.Plans ?? Array.Empty<PowerPlanInfo>();
    public bool HasPowerPlans => PowerPlans.Count > 0;
    public bool HasPowerPlanWarnings => PowerPlanInventoryResult?.Warnings.Count > 0;
    public string PowerPlanStatusText => PowerPlanInventoryResult?.Availability switch
    {
        InventoryAvailability.Available => $"{PowerPlans.Count} planos enumerados por APIs nativas do Windows. Nenhuma configuração foi alterada.",
        InventoryAvailability.Partial => PowerPlanInventoryResult.Warnings.FirstOrDefault() ?? "Lista parcial de planos de energia.",
        InventoryAvailability.Failed => PowerPlanInventoryResult.ErrorMessage ?? "Não foi possível consultar os planos de energia.",
        _ => "Planos de energia ainda não consultados."
    };

    public bool HasDiagnosticData => Inventory is not null || DriverInventoryResult is not null || PowerPlanInventoryResult is not null;
    public string SanitizedDiagnosticJson => SanitizedDiagnosticReportBuilder.BuildJson(
        Inventory,
        DriverInventoryResult,
        PowerPlanInventoryResult,
        privacyContext: new DiagnosticReportPrivacyContext(
            Environment.MachineName,
            Environment.UserName,
            Environment.UserDomainName),
        computerErrorMessage: ErrorMessage);

    public string CollectedAtLabel => Inventory is null
        ? "Aguardando inventário do computador."
        : $"Inventário local em {Inventory.CollectedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string? ErrorMessage { get => _errorMessage; set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasWarnings => Inventory?.Warnings.Count > 0;
    public string WindowsLabel
    {
        get
        {
            var inventory = Inventory;
            return Join(
                inventory?.WindowsName,
                Prefix("Edição", inventory?.WindowsEdition),
                Prefix("Versão", inventory?.WindowsVersion),
                Prefix("Build", inventory?.WindowsBuild),
                Prefix("Arquitetura", inventory?.Architecture));
        }
    }
    public string ProcessorLabel => Inventory?.Processor ?? "Desconhecido";
    public string GraphicsLabel => Inventory?.GraphicsAdapters.Count > 0 ? string.Join("\n", Inventory.GraphicsAdapters) : "Desconhecida";
    public string MemoryLabel => Inventory?.Memory ?? "Desconhecida";
    public string MotherboardLabel => Inventory?.Motherboard ?? "Desconhecida";
    public string StorageLabel => Inventory?.StorageDevices.Count > 0 ? string.Join("\n", Inventory.StorageDevices) : "Nenhum dispositivo identificado";
    public string NetworkLabel => Inventory?.NetworkAdapters.Count > 0 ? string.Join("\n", Inventory.NetworkAdapters) : "Nenhum adaptador identificado";

    private static string Join(params string?[] values)
    {
        var parts = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return parts.Length == 0 ? "Desconhecido" : string.Join(" · ", parts);
    }

    private static string? Prefix(string prefix, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{prefix} {value}";
}
