using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using Optimizer.Core.Network;

namespace Optimizer.App.ViewModels;

public sealed record NetworkAdapterRow(
    string Name,
    string Description,
    string State,
    string LinkSpeed,
    string Addresses,
    string Gateways,
    string DnsServers);

public sealed class NetworkPageViewModel : ObservableObject
{
    private readonly NetworkDiagnosticsService _service;
    private CancellationTokenSource? _testCancellation;
    private bool _isLoadingAdapters;
    private bool _isTesting;
    private string _destination = "example.com";
    private string _statusMessage = "A leitura local de adaptadores ainda não foi solicitada.";
    private string? _warningMessage;
    private string _progressMessage = string.Empty;
    private NetworkConnectivityResult? _lastResult;

    public NetworkPageViewModel(NetworkDiagnosticsService service)
    {
        _service = service;
        LoadAdaptersCommand = new AsyncRelayCommand(LoadAdaptersAsync, () => !IsLoadingAdapters && !IsTesting);
        TestConnectivityCommand = new AsyncRelayCommand(TestConnectivityAsync, () => CanTest);
        CancelTestCommand = new RelayCommand(CancelTest, () => IsTesting);
        Adapters = [];
    }

    public ObservableCollection<NetworkAdapterRow> Adapters { get; }
    public AsyncRelayCommand LoadAdaptersCommand { get; }
    public AsyncRelayCommand TestConnectivityCommand { get; }
    public RelayCommand CancelTestCommand { get; }

    public string Destination
    {
        get => _destination;
        set
        {
            if (!SetProperty(ref _destination, value)) return;
            OnPropertyChanged(nameof(DestinationPreview));
            NotifyCommands();
        }
    }

    public string DestinationPreview => string.IsNullOrWhiteSpace(Destination) ? "Destino: informe um host ou IP" : $"Destino: {Destination.Trim()}";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? WarningMessage { get => _warningMessage; private set { if (SetProperty(ref _warningMessage, value)) OnPropertyChanged(nameof(HasWarning)); } }
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public string ProgressMessage { get => _progressMessage; private set => SetProperty(ref _progressMessage, value); }
    public bool IsLoadingAdapters { get => _isLoadingAdapters; private set { if (SetProperty(ref _isLoadingAdapters, value)) NotifyCommands(); } }
    public bool IsTesting { get => _isTesting; private set { if (SetProperty(ref _isTesting, value)) NotifyCommands(); } }
    public bool IsIdle => !IsLoadingAdapters && !IsTesting;
    public bool HasAdapters => Adapters.Count > 0;
    public bool HasNoAdapters => !HasAdapters;
    public bool HasResult => LastResult is not null;
    public NetworkConnectivityResult? LastResult
    {
        get => _lastResult;
        private set
        {
            if (!SetProperty(ref _lastResult, value)) return;
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(OutcomeLabel));
            OnPropertyChanged(nameof(ResultMetadata));
            OnPropertyChanged(nameof(RoundtripSummary));
            OnPropertyChanged(nameof(ResultSummary));
            OnPropertyChanged(nameof(ResultDetails));
        }
    }

    public bool CanTest => IsIdle && !string.IsNullOrWhiteSpace(Destination);
    public string OutcomeLabel => LastResult?.Outcome switch
    {
        NetworkConnectivityOutcome.Success => "RESPOSTAS ICMP RECEBIDAS",
        NetworkConnectivityOutcome.PartialResponses => "RESPOSTAS PARCIAIS",
        NetworkConnectivityOutcome.NoIcmpResponses => "SEM RESPOSTA ICMP",
        NetworkConnectivityOutcome.DnsResolutionFailed => "FALHA DE RESOLUÇÃO DNS",
        NetworkConnectivityOutcome.NoActiveConnection => "SEM ENDEREÇO LOCAL ATIVO",
        NetworkConnectivityOutcome.AdapterReadFailed => "FALHA NA LEITURA LOCAL",
        NetworkConnectivityOutcome.ProbeFailed => "SONDAGEM SEM RESULTADO CLASSIFICÁVEL",
        NetworkConnectivityOutcome.Cancelled => "TESTE CANCELADO",
        _ => ""
    };
    public string ResultMetadata => LastResult is null ? "" :
        $"IP sondado: {LastResult.ResolvedAddress?.ToString() ?? "não resolvido"} · Amostras: {LastResult.CompletedSamples}/{LastResult.RequestedSamples} · Respostas: {LastResult.RepliesReceived} · Sem resposta: {LastResult.LostResponses} ({FormatLoss(LastResult.LossPercentage)}) · Timeout: {LastResult.TimeoutMilliseconds} ms · Duração total: {LastResult.DurationMilliseconds} ms";
    public string RoundtripSummary => LastResult is null || LastResult.AverageRoundtripMilliseconds is null
        ? "RTT: sem respostas medidas"
        : $"RTT das respostas: mínimo {LastResult.MinimumRoundtripMilliseconds} ms · média {LastResult.AverageRoundtripMilliseconds.Value.ToString("F1", CultureInfo.InvariantCulture)} ms · máximo {LastResult.MaximumRoundtripMilliseconds} ms";
    public string ResultSummary => LastResult?.Summary ?? "";
    public string ResultDetails => LastResult is null || LastResult.ProbeDetails.Count == 0
        ? ""
        : $"Status reportados: {string.Join(", ", LastResult.ProbeDetails.Distinct())}";

    public async Task LoadAdaptersAsync()
    {
        if (!IsIdle) return;
        IsLoadingAdapters = true;
        StatusMessage = "Lendo adaptadores e configurações locais; esta etapa não envia sondagens.";
        WarningMessage = null;
        try
        {
            var inventory = await Task.Run(_service.ReadAdapters);
            Adapters.Clear();
            foreach (var adapter in inventory.Adapters)
            {
                Adapters.Add(new NetworkAdapterRow(
                    adapter.Name,
                    $"{adapter.InterfaceType} · {adapter.Description}",
                    adapter.LinkState,
                    FormatLinkSpeed(adapter.LinkSpeedBitsPerSecond),
                    FormatValues(adapter.Addresses),
                    FormatValues(adapter.Gateways),
                    FormatValues(adapter.DnsServers)));
            }

            OnPropertyChanged(nameof(HasAdapters));
            OnPropertyChanged(nameof(HasNoAdapters));
            WarningMessage = inventory.Warnings.Count == 0 ? null : string.Join(" ", inventory.Warnings);
            var activeAddressCount = inventory.Adapters.Count(item => item.IsOperational && item.Addresses.Any(address =>
                IPAddress.TryParse(address.Split('%')[0], out var parsed) && !IPAddress.IsLoopback(parsed)));
            StatusMessage = inventory.Adapters.Count == 0
                ? "Nenhum adaptador foi retornado pelo Windows. Isso descreve a leitura local."
                : $"Leitura local concluída: {inventory.Adapters.Count} interfaces, incluindo virtuais/túnel; {activeAddressCount} operacional(is) com endereço não local. Nenhuma configuração foi alterada.";
        }
        catch (Exception exception)
        {
            Adapters.Clear();
            OnPropertyChanged(nameof(HasAdapters));
            OnPropertyChanged(nameof(HasNoAdapters));
            WarningMessage = $"A leitura local falhou ({exception.GetType().Name}).";
            StatusMessage = "Não foi possível ler os adaptadores.";
        }
        finally
        {
            IsLoadingAdapters = false;
        }
    }

    private async Task TestConnectivityAsync()
    {
        if (!CanTest) return;
        LastResult = null;
        WarningMessage = null;
        IsTesting = true;
        _testCancellation = new CancellationTokenSource();
        ProgressMessage = $"Preparando até {NetworkDiagnosticsService.DefaultSampleCount} sondagens para {Destination.Trim()}…";
        StatusMessage = "Teste ativo iniciado por ação explícita. Resolução de nome e sondagens podem enviar tráfego.";

        var progress = new Progress<NetworkProbeProgress>(value =>
            ProgressMessage = $"Sondagens concluídas: {value.CompletedSamples}/{value.RequestedSamples} · respostas recebidas: {value.RepliesReceived}");
        try
        {
            LastResult = await _service.TestConnectivityAsync(
                Destination,
                NetworkDiagnosticsService.DefaultSampleCount,
                NetworkDiagnosticsService.DefaultTimeoutMilliseconds,
                progress,
                _testCancellation.Token);
            StatusMessage = LastResult.Summary;
        }
        catch (Exception exception)
        {
            WarningMessage = $"O teste não pôde ser concluído ({exception.GetType().Name}).";
            StatusMessage = "Nenhuma conclusão sobre acesso geral à internet foi feita.";
        }
        finally
        {
            _testCancellation.Dispose();
            _testCancellation = null;
            IsTesting = false;
            ProgressMessage = string.Empty;
        }
    }

    private void CancelTest()
    {
        if (IsTesting) _testCancellation?.Cancel();
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CanTest));
        LoadAdaptersCommand.NotifyCanExecuteChanged();
        TestConnectivityCommand.NotifyCanExecuteChanged();
        CancelTestCommand.NotifyCanExecuteChanged();
    }

    private static string FormatValues(IReadOnlyList<string> values) => values.Count == 0 ? "Não informado" : string.Join(" · ", values);

    private static string FormatLinkSpeed(long? bitsPerSecond)
    {
        if (bitsPerSecond is null) return "Não informado";
        var megabits = bitsPerSecond.Value / 1_000_000d;
        return $"{megabits.ToString("F0", CultureInfo.InvariantCulture)} Mb/s no enlace";
    }

    private static string FormatLoss(double? percentage) => percentage is null ? "n/a" : $"{percentage.Value.ToString("F0", CultureInfo.InvariantCulture)}%";
}
