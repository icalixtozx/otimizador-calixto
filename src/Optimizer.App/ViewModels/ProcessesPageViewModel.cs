using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Processes;
using Optimizer.Core.Recovery;

namespace Optimizer.App.ViewModels;

public sealed record ProcessRowViewModel(ProcessObservation Observation)
{
    public int ProcessId => Observation.ProcessId;
    public string Name => Observation.Name ?? "Nome indisponível";
    public string Status => Observation.Status switch
    {
        ProcessInspectionStatus.Available => "Disponível",
        ProcessInspectionStatus.Partial => "Parcial",
        ProcessInspectionStatus.AccessDenied => "Acesso negado",
        ProcessInspectionStatus.Exited => "Encerrado durante a leitura",
        ProcessInspectionStatus.NotFound => "Não encontrado",
        _ => "Falha de leitura"
    };
    public string Priority => Observation.CurrentPriority is ProcessPriorityLevel priority
        ? ProcessPriorityTargets.DisplayName(priority)
        : "Indisponível";
    public string Created => Observation.Identity?.CreatedAtUtc.ToLocalTime().ToString("g") ?? "Indisponível";
    public string Path => Observation.Identity?.ExecutablePath ?? "Indisponível";
    public string Detail => Observation.Detail;
    public bool CanReview => Observation.CanReviewPriority &&
        Observation.CurrentPriority is ProcessPriorityLevel current && ProcessPriorityTargets.Allowed.Contains(current);
}

public sealed record ProcessHistoryRow(
    Guid OperationId,
    string State,
    string Date,
    string Event,
    string Recovery);

public sealed record ProcessPriorityOption(ProcessPriorityLevel Value, string Label);

public sealed class ProcessesPageViewModel : ObservableObject
{
    private readonly ProcessPriorityOperationService _service;
    private ProcessRowViewModel? _selectedProcess;
    private ProcessPriorityLevel _selectedTargetPriority = ProcessPriorityLevel.Normal;
    private ProcessPriorityReview? _review;
    private OperationJournalEntry? _lastOperation;
    private string _statusMessage = "Lista ainda não lida. Consultas são somente leitura.";
    private string _resultMessage = "Nenhuma captura registrada nesta sessão.";
    private bool _isBusy;
    private bool _consequencesAccepted;

    public ProcessesPageViewModel(ProcessPriorityOperationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        Processes = [];
        History = [];
        TargetPriorities = Array.AsReadOnly(ProcessPriorityTargets.Allowed
            .Select(priority => new ProcessPriorityOption(priority, ProcessPriorityTargets.DisplayName(priority))).ToArray());
        RefreshCommand = new AsyncRelayCommand(LoadProcessesAsync, () => !IsBusy);
        PrepareReviewCommand = new RelayCommand(PrepareReview, CanPrepareReview);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, CanCapture);
        RefreshHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync, () => !IsBusy);
    }

    public ObservableCollection<ProcessRowViewModel> Processes { get; }
    public ObservableCollection<ProcessHistoryRow> History { get; }
    public IReadOnlyList<ProcessPriorityOption> TargetPriorities { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand PrepareReviewCommand { get; }
    public AsyncRelayCommand CaptureCommand { get; }
    public AsyncRelayCommand RefreshHistoryCommand { get; }

    public ProcessRowViewModel? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (SetProperty(ref _selectedProcess, value))
            {
                ResetReview();
                NotifyCommands();
            }
        }
    }

    public ProcessPriorityLevel SelectedTargetPriority
    {
        get => _selectedTargetPriority;
        set
        {
            if (SetProperty(ref _selectedTargetPriority, value))
            {
                ResetReview();
                NotifyCommands();
            }
        }
    }

    public ProcessPriorityReview? Review
    {
        get => _review;
        private set
        {
            if (SetProperty(ref _review, value))
            {
                OnPropertyChanged(nameof(HasReview));
                OnPropertyChanged(nameof(ReviewSummary));
                OnPropertyChanged(nameof(ReviewConsequence));
                OnPropertyChanged(nameof(ReviewCompatibility));
                NotifyCommands();
            }
        }
    }

    public OperationJournalEntry? LastOperation
    {
        get => _lastOperation;
        private set
        {
            if (SetProperty(ref _lastOperation, value))
            {
                OnPropertyChanged(nameof(HasLastOperation));
                OnPropertyChanged(nameof(LastOperationSummary));
            }
        }
    }

    public bool HasReview => Review is not null;
    public bool HasLastOperation => LastOperation is not null;
    public string ReviewSummary => Review is null
        ? "Selecione um processo e solicite a revisão atualizada."
        : $"PID {Review.Identity.ProcessId} · criado {Review.Identity.CreatedAtUtc.ToLocalTime():g} · {ProcessPriorityTargets.DisplayName(Review.CurrentPriority)} → {ProcessPriorityTargets.DisplayName(Review.TargetPriority)}";
    public string ReviewConsequence => Review?.Plan.Items.SingleOrDefault()?.Consequence ?? string.Empty;
    public string ReviewCompatibility => Review?.CompatibilityMessage ?? string.Empty;
    public string LastOperationSummary => LastOperation is null
        ? string.Empty
        : $"{LastOperation.State} · operação {LastOperation.OperationId.ToString("N")[..8].ToUpperInvariant()} · {LastOperation.Events.LastOrDefault()?.Message}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ResultMessage
    {
        get => _resultMessage;
        private set => SetProperty(ref _resultMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                RefreshHistoryCommand.NotifyCanExecuteChanged();
                NotifyCommands();
            }
        }
    }

    public bool ConsequencesAccepted
    {
        get => _consequencesAccepted;
        set
        {
            if (SetProperty(ref _consequencesAccepted, value))
            {
                OnPropertyChanged(nameof(CaptureNotice));
                NotifyCommands();
            }
        }
    }

    public string CaptureNotice => ConsequencesAccepted
        ? "Consentimento de revisão marcado. Isto registra consequência e captura; não autoriza escrita real."
        : "Leia a consequência e marque o consentimento para salvar a captura. Nenhuma prioridade será alterada.";

    public async Task LoadProcessesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        SelectedProcess = null;
        ResetReview();
        try
        {
            StatusMessage = "Consultando processos localmente…";
            var result = await Task.Run(_service.ListProcesses);
            Processes.Clear();
            foreach (var process in result.Processes)
                Processes.Add(new ProcessRowViewModel(process));
            var denied = result.Processes.Count(item => item.Status == ProcessInspectionStatus.AccessDenied);
            var exited = result.Processes.Count(item => item.Status == ProcessInspectionStatus.Exited);
            StatusMessage = $"Leitura real concluída · {result.Processes.Count} processos listados · {denied} com acesso negado · {exited} encerrados durante a leitura. Nenhuma prioridade foi alterada.";
            if (result.Warnings.Count > 0)
                StatusMessage += $" {result.Warnings.Count} aviso(s) de leitura.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"A lista não pôde ser lida ({exception.GetType().Name}). Nenhuma prioridade foi alterada.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PrepareReview()
    {
        if (!CanPrepareReview()) return;
        try
        {
            Review = _service.CreateReview(
                SelectedProcess!.Observation.Identity!,
                SelectedTargetPriority,
                CurrentPlatform());
            ResultMessage = "Prévia baseada em estado real recém-consultado. Ainda não há consentimento registrado nem alteração.";
        }
        catch (Exception exception)
        {
            Review = null;
            ResultMessage = $"A revisão foi recusada ({exception.GetType().Name}). Atualize a lista e confirme a identidade do processo.";
        }
    }

    private async Task CaptureAsync()
    {
        if (!CanCapture() || Review is null) return;
        IsBusy = true;
        try
        {
            LastOperation = await _service.CaptureAsync(Review, ConsequencesAccepted);
            ResultMessage = LastOperation.Events.LastOrDefault()?.EventType == "WriteNotAttempted"
                ? "Acesso ou leitura indisponível; falha registrada no histórico, sem snapshot e sem alteração de prioridade."
                : LastOperation.State switch
                {
                    JournalOperationState.SnapshotsCaptured => "Consentimento e captura real persistidos no histórico. Nenhuma prioridade foi alterada.",
                    JournalOperationState.Conflict => "A identidade ou prioridade mudou; conflito registrado. Nenhuma prioridade foi alterada.",
                    _ => $"Estado registrado: {LastOperation.State}. Nenhuma prioridade foi alterada."
                };
            ConsequencesAccepted = false;
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            ResultMessage = $"A captura não foi confirmada ({exception.GetType().Name}). Nenhuma prioridade foi alterada.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadHistoryAsync()
    {
        try
        {
            var entries = await _service.LoadHistoryAsync();
            var processEntries = entries.Where(entry => entry.ProfileId == ProcessPriorityOperationService.ProfileId).ToArray();
            History.Clear();
            foreach (var entry in processEntries)
            {
                History.Add(new ProcessHistoryRow(
                    entry.OperationId,
                    StateLabel(entry.State),
                    entry.CreatedAtUtc.ToLocalTime().ToString("g"),
                    entry.Events.LastOrDefault()?.Message ?? "Sem evento no diário.",
                    OperationRecoveryEvaluator.Assess(entry).Message));
            }
        }
        catch (Exception exception)
        {
            ResultMessage = $"O histórico de processos não pôde ser lido ({exception.GetType().Name}).";
        }
    }

    private bool CanPrepareReview() => !IsBusy && SelectedProcess?.CanReview == true &&
        SelectedProcess.Observation.Identity is not null && ProcessPriorityTargets.Allowed.Contains(SelectedTargetPriority);

    private bool CanCapture() => !IsBusy && Review is not null && ConsequencesAccepted;

    private void ResetReview()
    {
        Review = null;
        ConsequencesAccepted = false;
    }

    private void NotifyCommands()
    {
        PrepareReviewCommand.NotifyCanExecuteChanged();
        CaptureCommand.NotifyCanExecuteChanged();
    }

    private static PlatformDescriptor CurrentPlatform() => new(
        OperatingSystem.IsWindows(),
        RuntimeInformation.OSArchitecture.ToString(),
        OperatingSystem.IsWindows() ? Environment.OSVersion.Version.Build : null);

    private static string StateLabel(JournalOperationState state) => state switch
    {
        JournalOperationState.Planned => "Planejado",
        JournalOperationState.ConsentRecorded => "Consentimento revisado",
        JournalOperationState.SnapshotsCaptured => "Captura real registrada",
        JournalOperationState.MutationMayHaveStarted => "Aplicação possivelmente iniciada",
        JournalOperationState.MutationVerified => "Aplicação verificada",
        JournalOperationState.RestorationMayHaveStarted => "Restauração possivelmente iniciada",
        JournalOperationState.RestoredVerified => "Restauração verificada",
        JournalOperationState.NoChangeRequired => "Nenhuma escrita necessária",
        JournalOperationState.Conflict => "Conflito sem escrita",
        JournalOperationState.Failed => "Falha; revisão manual",
        JournalOperationState.Interrupted => "Interrompido; revisão manual",
        JournalOperationState.SimulationCompleted => "Simulação concluída",
        _ => "Desconhecido"
    };
}
