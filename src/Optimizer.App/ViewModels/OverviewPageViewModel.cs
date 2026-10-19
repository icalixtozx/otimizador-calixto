using Optimizer.Core.Inventory;

namespace Optimizer.App.ViewModels;

public sealed class OverviewPageViewModel : ObservableObject
{
    private ComputerInventory? _inventory;
    private bool _isLoading;
    private string _statusText = "A análise ainda não foi executada.";
    private string? _errorMessage;

    public ComputerInventory? Inventory
    {
        get => _inventory;
        set
        {
            if (SetProperty(ref _inventory, value))
            {
                OnPropertyChanged(nameof(WindowsLabel));
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
