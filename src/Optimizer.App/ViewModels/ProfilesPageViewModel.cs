using System.Collections.ObjectModel;
using System.IO;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Consent;
using Optimizer.Core.Profiles;
using Optimizer.Core.Recovery;

namespace Optimizer.App.ViewModels;

public sealed record PlanReviewItem(
    string Name,
    string Description,
    string Risk,
    string Status,
    string Reason);

public sealed class ConsentDecisionViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _accepted;

    public ConsentDecisionViewModel(AdjustmentPlanItem item, Action changed)
    {
        Item = item;
        _changed = changed;
    }

    public AdjustmentPlanItem Item { get; }
    public string Name => Item.Name;
    public string Consequence => Item.Consequence;
    public string Risk => Item.Risk.ToString();
    public bool Accepted
    {
        get => _accepted;
        set
        {
            if (SetProperty(ref _accepted, value)) _changed();
        }
    }
}

public sealed record VideoIdleHistoryItem(
    VideoIdleOperationRecord Entry,
    string StateLabel,
    string Date,
    string Summary);

public sealed record OperationHistoryItem(
    OperationJournalEntry Entry,
    string Profile,
    string State,
    string Date,
    string DataLabel,
    string Recovery,
    string SnapshotNotice,
    string LatestEvent);

public sealed class ProfilesPageViewModel : ObservableObject
{
    private static readonly AdjustmentDefinition SyntheticMarker = new(
        "synthetic.demo-marker",
        1,
        "Marcador de demonstração",
        "Exemplo sintético para exercitar revisão, consentimento e histórico. Não representa um ajuste do Windows.",
        AdjustmentArea.Other,
        AdjustmentRiskLevel.Low,
        "Grava apenas uma entrada de demonstração no histórico local do aplicativo; não altera configurações e não cria backup do Windows.",
        RequiresAdministrator: false,
        SupportedArchitectures: ["x64"],
        MinimumWindowsBuild: null,
        Availability: AdjustmentAvailability.SimulationOnly,
        AvailabilityReason: null);

    private readonly AdjustmentCatalog _productCatalog;
    private readonly AdjustmentCatalog _syntheticCatalog = new([SyntheticMarker]);
    private readonly AdjustmentCompatibilityEvaluator _compatibility = new();
    private readonly PlanConsentService _consentService = new();
    private readonly OperationJournalCoordinator _journalCoordinator;
    private readonly JsonOperationJournalStore _journalStore;
    private readonly AdjustmentSimulationService _simulationService;
    private readonly VideoIdleOperationService _videoIdleOperationService;
    private readonly ProfilePlanService _productProfilePlanner;
    private readonly ProfilePlanService _syntheticProfilePlanner;
    private OptimizerProfile _selectedProfile;
    private bool _includeSyntheticDemo;
    private bool _isRunning;
    private bool _isVideoIdleBusy;
    private int _selectedSectionIndex;
    private string _statusMessage = "Nenhum ajuste real validado está disponível. A demonstração é opcional e sintética.";
    private string _historyStatusMessage = "A retomada é apenas avaliada; o app não reaplica nem reverte operações automaticamente.";
    private string? _simulationResult;
    private VideoIdlePreview? _videoIdlePreview;
    private string _videoIdleStatusMessage = "Prévia real não consultada. Nenhuma configuração será lida até você solicitar.";
    private string _videoIdleResultLabel = "SEM RESULTADO REAL VERIFICADO";
    private IReadOnlyList<PlanReviewItem> _reviewItems = Array.Empty<PlanReviewItem>();
    private ProfilePlanResult? _currentPlan;

    public ProfilesPageViewModel(
        OptimizerProfileCatalog profiles,
        AdjustmentCatalog adjustments,
        IVideoIdlePowerAdapter videoIdleAdapter,
        IPowerModeAdapter powerModeAdapter)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(adjustments);
        ArgumentNullException.ThrowIfNull(videoIdleAdapter);
        ArgumentNullException.ThrowIfNull(powerModeAdapter);
        _productCatalog = adjustments;
        ProfileOptions = Array.AsReadOnly(profiles.Profiles.ToArray());
        _selectedProfile = ProfileOptions.First(profile => profile.Id == "balanced");

        var productPlanService = new AdjustmentPlanService(_productCatalog, _compatibility);
        var demoPlanService = new AdjustmentPlanService(_syntheticCatalog, _compatibility);
        _productProfilePlanner = new ProfilePlanService(profiles, _productCatalog, _compatibility, productPlanService);
        _syntheticProfilePlanner = new ProfilePlanService(profiles, _syntheticCatalog, _compatibility, demoPlanService);
        _simulationService = new AdjustmentSimulationService(
            _syntheticCatalog,
            _compatibility,
            new InMemorySimulatedAdjustmentAdapter());
        _journalCoordinator = new OperationJournalCoordinator(_consentService);

        var historyDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CalixtoOptimizer",
            "History");
        _journalStore = new JsonOperationJournalStore(historyDirectory);
        _videoIdleOperationService = new VideoIdleOperationService(
            videoIdleAdapter,
            new JsonVideoIdleOperationStore(Path.Combine(historyDirectory, "RealVideoIdle")));
        PowerMode = new PowerModePanelViewModel(powerModeAdapter, historyDirectory);

        ConsentDecisions = [];
        History = [];
        VideoIdleHistory = [];
        BuildPlanCommand = new RelayCommand(RebuildPlan);
        SimulateCommand = new AsyncRelayCommand(SimulateAsync, () => CanSimulate && !IsRunning);
        ReviewResumeCommand = new RelayCommand<OperationHistoryItem>(ReviewResume);
        ReadVideoIdleCommand = new AsyncRelayCommand(ReadVideoIdlePreviewAsync, () => !IsVideoIdleBusy);
        CaptureVideoIdleCommand = new AsyncRelayCommand(CaptureVideoIdleAsync, () => CanCaptureVideoIdle);
        RebuildPlan();
    }

    public IReadOnlyList<OptimizerProfile> ProfileOptions { get; }
    public ObservableCollection<ConsentDecisionViewModel> ConsentDecisions { get; }
    public ObservableCollection<OperationHistoryItem> History { get; }
    public ObservableCollection<VideoIdleHistoryItem> VideoIdleHistory { get; }
    public PowerModePanelViewModel PowerMode { get; }
    public RelayCommand BuildPlanCommand { get; }
    public AsyncRelayCommand SimulateCommand { get; }
    public RelayCommand<OperationHistoryItem> ReviewResumeCommand { get; }
    public AsyncRelayCommand ReadVideoIdleCommand { get; }
    public AsyncRelayCommand CaptureVideoIdleCommand { get; }
    public int ProductCatalogCount => _productCatalog.Definitions.Count;
    public string CatalogStatus => ProductCatalogCount == 0
        ? "Catálogo de produto vazio: não há ajustes reais validados para planejar ou aplicar."
        : $"{ProductCatalogCount} item(ns) descrito(s); disponibilidade precisa ser revisada individualmente.";
    public bool HasSyntheticDemo => _includeSyntheticDemo;
    public bool HasVideoIdlePreview => VideoIdlePreview is not null;
    public bool CanCaptureVideoIdle => HasVideoIdlePreview && !IsVideoIdleBusy;
    public bool IsVideoIdleBusy
    {
        get => _isVideoIdleBusy;
        private set
        {
            if (SetProperty(ref _isVideoIdleBusy, value))
            {
                OnPropertyChanged(nameof(CanCaptureVideoIdle));
                ReadVideoIdleCommand.NotifyCanExecuteChanged();
                CaptureVideoIdleCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public VideoIdlePreview? VideoIdlePreview
    {
        get => _videoIdlePreview;
        private set
        {
            if (SetProperty(ref _videoIdlePreview, value))
            {
                OnPropertyChanged(nameof(HasVideoIdlePreview));
                OnPropertyChanged(nameof(CanCaptureVideoIdle));
                OnPropertyChanged(nameof(VideoIdlePreviewText));
                CaptureVideoIdleCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public string VideoIdlePreviewText => VideoIdlePreview is null
        ? "Ainda sem leitura do valor real."
        : $"Plano ativo lido · AC atual {VideoIdlePreview.CurrentAcTimeoutSeconds} s · proposta {VideoIdlePreview.ProposedAcTimeoutSeconds} s · leitura {VideoIdlePreview.ReadAtUtc.ToLocalTime():g}.";
    public string VideoIdleStatusMessage
    {
        get => _videoIdleStatusMessage;
        private set => SetProperty(ref _videoIdleStatusMessage, value);
    }
    public string VideoIdleResultLabel
    {
        get => _videoIdleResultLabel;
        private set => SetProperty(ref _videoIdleResultLabel, value);
    }
    public string PlanSourceLabel => HasSyntheticDemo
        ? "PLANO DE DEMONSTRAÇÃO · DADOS SINTÉTICOS"
        : "PLANO DO CATÁLOGO DE PRODUTO";
    public string PlanSummary => _currentPlan is null || _currentPlan.Plan.Items.Count == 0
        ? "O plano não contém itens. Nenhuma alteração pode ser aplicada."
        : $"{_currentPlan.Plan.Items.Count} item(ns) para revisão · Impressão digital {_currentPlan.Plan.Fingerprint[..12]}…";
    public IReadOnlyList<PlanReviewItem> ReviewItems
    {
        get => _reviewItems;
        private set => SetProperty(ref _reviewItems, value);
    }
    public bool HasReviewItems => ReviewItems.Count > 0;
    public bool HasConsentDecisions => ConsentDecisions.Count > 0;
    public bool CanSimulate => _currentPlan?.Plan.CanSimulate == true &&
        HasSyntheticDemo && ConsentDecisions.Count > 0 && ConsentDecisions.All(decision => decision.Accepted);
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanSimulate));
                SimulateCommand.NotifyCanExecuteChanged();
            }
        }
    }
    public int SelectedSectionIndex
    {
        get => _selectedSectionIndex;
        set => SetProperty(ref _selectedSectionIndex, value);
    }
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }
    public string HistoryStatusMessage
    {
        get => _historyStatusMessage;
        private set => SetProperty(ref _historyStatusMessage, value);
    }
    public string? SimulationResult
    {
        get => _simulationResult;
        private set => SetProperty(ref _simulationResult, value);
    }
    public OptimizerProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value)) RebuildPlan();
        }
    }
    public bool IncludeSyntheticDemo
    {
        get => _includeSyntheticDemo;
        set
        {
            if (SetProperty(ref _includeSyntheticDemo, value))
            {
                OnPropertyChanged(nameof(HasSyntheticDemo));
                OnPropertyChanged(nameof(PlanSourceLabel));
                RebuildPlan();
            }
        }
    }

    public async Task LoadHistoryAsync()
    {
        try
        {
            var entries = await _journalStore.LoadAllAsync();
            var videoIdleEntries = await _videoIdleOperationService.LoadHistoryAsync();
            await PowerMode.LoadHistoryAsync();
            VideoIdleHistory.Clear();
            foreach (var entry in videoIdleEntries)
            {
                VideoIdleHistory.Add(new VideoIdleHistoryItem(
                    entry,
                    GetVideoIdleStateLabel(entry.State),
                    entry.CapturedAtUtc.ToLocalTime().ToString("g"),
                    $"Plano ativo capturado · AC {entry.PreviousAcTimeoutSeconds} s · proposta {entry.ProposedAcTimeoutSeconds} s. {entry.Detail}"));
            }
            if (videoIdleEntries.Count > 0)
            {
                var latest = videoIdleEntries[0];
                VideoIdleResultLabel = latest.State is VideoIdleOperationState.Verified or VideoIdleOperationState.Reverted
                    ? "HISTÓRICO: REGISTRO DIZ QUE A LEITURA POSTERIOR CONFIRMOU; ESTADO ATUAL NÃO RECHECADO"
                    : "SEM RESULTADO REAL VERIFICADO";
            }
            History.Clear();
            foreach (var entry in entries)
            {
                var assessment = OperationRecoveryEvaluator.Assess(entry);
                History.Add(new OperationHistoryItem(
                    entry,
                    ProfileOptions.FirstOrDefault(profile => profile.Id == entry.ProfileId)?.Name ?? entry.ProfileId,
                    GetStateLabel(entry.State),
                    entry.CreatedAtUtc.ToLocalTime().ToString("g"),
                    entry.DataOrigin == JournalDataOrigin.SyntheticDemonstration
                        ? "DADOS SINTÉTICOS"
                        : "PLANO DO USUÁRIO",
                    assessment.Message,
                    entry.DataOrigin == JournalDataOrigin.SyntheticDemonstration || entry.Snapshots.Count == 0
                        ? "Sem snapshots. Isto não é backup do Windows."
                        : $"{entry.Snapshots.Count} snapshot(s) registrados; não são backup restaurável do Windows.",
                    entry.Events.LastOrDefault()?.Message ?? "Sem evento no diário."));
            }
        }
        catch (Exception exception)
        {
            var message = $"Não foi possível ler o histórico ({exception.GetType().Name}). Nenhuma ação do Windows foi executada.";
            StatusMessage = message;
            HistoryStatusMessage = message;
        }
    }

    private async Task ReadVideoIdlePreviewAsync()
    {
        if (IsVideoIdleBusy) return;
        IsVideoIdleBusy = true;
        try
        {
            var preview = await Task.Run(_videoIdleOperationService.ReadPreview);
            VideoIdlePreview = preview;
            VideoIdleStatusMessage = preview.AlreadyAtProposedValue
                ? "Leitura real concluída: o valor AC já é 900 s. Nenhuma configuração foi alterada."
                : "Leitura real concluída. A prévia não altera o Windows; a captura persistida consultará o estado novamente.";
            VideoIdleResultLabel = "PRÉVIA REAL · SOMENTE LEITURA";
        }
        catch (Exception exception)
        {
            VideoIdleStatusMessage = $"Não foi possível ler VIDEOIDLE ({exception.GetType().Name}). Nenhuma escrita foi tentada.";
            VideoIdleResultLabel = "LEITURA REAL INDISPONÍVEL";
        }
        finally
        {
            IsVideoIdleBusy = false;
        }
    }

    private async Task CaptureVideoIdleAsync()
    {
        if (!CanCaptureVideoIdle) return;
        IsVideoIdleBusy = true;
        try
        {
            var record = await _videoIdleOperationService.CaptureAsync();
            VideoIdlePreview = new VideoIdlePreview(
                record.CapturedAtUtc,
                record.ActiveSchemeId,
                record.PreviousAcTimeoutSeconds,
                record.ProposedAcTimeoutSeconds);
            VideoIdleStatusMessage = $"Captura real persistida · operação {record.OperationId:D}. Nenhuma configuração foi alterada; isto não é backup do Windows.";
            VideoIdleResultLabel = "CAPTURA REAL PERSISTIDA · NÃO APLICADA";
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            VideoIdleStatusMessage = $"A captura não foi persistida ({exception.GetType().Name}); nenhuma alteração do Windows foi tentada.";
            VideoIdleResultLabel = "CAPTURA NÃO CONFIRMADA";
        }
        finally
        {
            IsVideoIdleBusy = false;
        }
    }

    private static string GetVideoIdleStateLabel(VideoIdleOperationState state) => state switch
    {
        VideoIdleOperationState.Captured => "CAPTURA REAL PERSISTIDA · NÃO APLICADA",
        VideoIdleOperationState.MutationMayHaveStarted => "MUTAÇÃO PODE TER COMEÇADO · REVISÃO MANUAL",
        VideoIdleOperationState.Verified => "HISTÓRICO: REGISTRO DIZ QUE A LEITURA POSTERIOR CONFIRMOU; ESTADO ATUAL NÃO RECHECADO",
        VideoIdleOperationState.NoChangeRequired => "VALOR JÁ CONFIGURADO · SEM ESCRITA",
        VideoIdleOperationState.Conflict => "CONFLITO · ESCRITA RECUSADA",
        VideoIdleOperationState.MutationUnverified => "RESULTADO NÃO VERIFICADO · REVISÃO MANUAL",
        VideoIdleOperationState.ReversionMayHaveStarted => "REVERSÃO PODE TER COMEÇADO · REVISÃO MANUAL",
        VideoIdleOperationState.Reverted => "REVERSÃO VERIFICADA POR LEITURA POSTERIOR",
        VideoIdleOperationState.ReversionUnverified => "REVERSÃO NÃO VERIFICADA · REVISÃO MANUAL",
        _ => "ESTADO DESCONHECIDO · REVISÃO MANUAL"
    };
    private void RebuildPlan()
    {
        var planner = IncludeSyntheticDemo ? _syntheticProfilePlanner : _productProfilePlanner;
        var selectedIds = IncludeSyntheticDemo ? [SyntheticMarker.Id] : Array.Empty<string>();
        _currentPlan = planner.CreatePlan(
            SelectedProfile.Id,
            new PlatformDescriptor(IsWindows: true, Architecture: "x64", WindowsBuild: null),
            selectedIds);

        ReviewItems = Array.AsReadOnly(_currentPlan.Plan.Items.Select(item => new PlanReviewItem(
            item.Name,
            item.Description ?? item.Consequence,
            item.Risk.ToString(),
            item.Status.ToString(),
            item.Reason)).ToArray());
        ConsentDecisions.Clear();
        foreach (var item in _currentPlan.Plan.Items)
            ConsentDecisions.Add(new ConsentDecisionViewModel(item, OnConsentChanged));

        SimulationResult = null;
        StatusMessage = IncludeSyntheticDemo
            ? "Prévia sintética pronta. Revise a consequência e marque o consentimento individual para habilitar a simulação em memória."
            : CatalogStatus;
        OnPropertyChanged(nameof(PlanSummary));
        OnPropertyChanged(nameof(HasReviewItems));
        OnPropertyChanged(nameof(HasConsentDecisions));
        OnPropertyChanged(nameof(CanSimulate));
        SimulateCommand.NotifyCanExecuteChanged();
    }

    private void OnConsentChanged()
    {
        OnPropertyChanged(nameof(CanSimulate));
        SimulateCommand.NotifyCanExecuteChanged();
    }

    private async Task SimulateAsync()
    {
        if (!CanSimulate || _currentPlan is null) return;
        IsRunning = true;
        SimulationResult = null;

        try
        {
            var plan = _currentPlan.Plan;
            var decisions = ConsentDecisions.ToDictionary(
                decision => decision.Item.AdjustmentId,
                decision => decision.Accepted,
                StringComparer.Ordinal);
            var receipt = _consentService.Record(plan, decisions);
            var entry = _journalCoordinator.CreatePlanned(
                plan,
                SelectedProfile.Id,
                JournalDataOrigin.SyntheticDemonstration);
            entry = _journalCoordinator.RecordConsent(entry, plan, receipt);
            await _journalStore.SaveAsync(entry);

            var result = await _simulationService.SimulateAsync(plan);
            if (result.Outcome == AdjustmentSimulationOutcome.Completed)
                entry = _journalCoordinator.MarkSimulationCompleted(entry);
            await _journalStore.SaveAsync(entry);

            SimulationResult = result.Message;
            StatusMessage = "Demonstração concluída em memória. Nenhuma alteração do Windows foi feita e nenhum backup do Windows foi criado.";
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"A demonstração não foi concluída ({exception.GetType().Name}). Nenhuma alteração do Windows foi feita.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ReviewResume(OperationHistoryItem? item)
    {
        if (item is null) return;
        var assessment = _journalCoordinator.AssessResume(item.Entry);
        HistoryStatusMessage = assessment.Message;
    }

    private static string GetStateLabel(JournalOperationState state) => state switch
    {
        JournalOperationState.Planned => "Planejado",
        JournalOperationState.ConsentRecorded => "Consentimento registrado",
        JournalOperationState.SnapshotsCaptured => "Snapshots registrados",
        JournalOperationState.MutationMayHaveStarted => "Operação possivelmente iniciada",
        JournalOperationState.MutationVerified => "Operação verificada",
        JournalOperationState.RestorationMayHaveStarted => "Restauração possivelmente iniciada",
        JournalOperationState.RestoredVerified => "Restauração verificada",
        JournalOperationState.NoChangeRequired => "Nenhuma escrita necessária",
        JournalOperationState.Conflict => "Conflito sem escrita",
        JournalOperationState.Failed => "Falhou",
        JournalOperationState.Interrupted => "Interrompido",
        JournalOperationState.SimulationCompleted => "Simulação concluída",
        _ => "Desconhecido"
    };
}
