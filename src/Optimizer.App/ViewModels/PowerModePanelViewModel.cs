using System.Collections.ObjectModel;
using System.IO;
using Optimizer.Core.Adjustments;

namespace Optimizer.App.ViewModels;

public sealed record PowerModeOption(UserConfiguredPowerMode Mode, string Name);

public sealed record PowerModeHistoryItem(
    PowerModeOperationRecord Entry,
    string StateLabel,
    string Date,
    string Summary);

/// <summary>Real preview and capture UI. Writes are absent until a distinct, operation-specific authorization is entered.</summary>
public sealed class PowerModePanelViewModel : ObservableObject
{
    private readonly IPowerModeAdapter _adapter;
    private readonly JsonPowerModeOperationStore _store;
    private readonly PowerModeOperationService _service;
    private PowerModeReadResult? _preview;
    private PowerModeOperationRecord? _capture;
    private PowerModeOption? _selectedAcTarget;
    private PowerModeOption? _selectedDcTarget;
    private bool _reviewedAcConsequences;
    private bool _reviewedDcConsequences;
    private bool _authorizeAcConsequences;
    private bool _authorizeDcConsequences;
    private bool _authorizeApply;
    private bool _authorizeRevert;
    private bool _acceptRecoveryConsequences;
    private string _applyPhrase = string.Empty;
    private string _revertPhrase = string.Empty;
    private bool _isBusy;
    private string _statusMessage = "Leitura do modo de energia não solicitada. Nenhuma configuração foi consultada.";
    private string _resultLabel = "SEM LEITURA REAL";

    public PowerModePanelViewModel(IPowerModeAdapter adapter, string historyDirectory)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapter = adapter;
        _store = new JsonPowerModeOperationStore(Path.Combine(historyDirectory, "RealPowerMode"));
        _service = new PowerModeOperationService(_adapter, _store);
        Options = Array.AsReadOnly(new[]
        {
            new PowerModeOption(UserConfiguredPowerMode.BestEfficiency, "Melhor eficiência energética"),
            new PowerModeOption(UserConfiguredPowerMode.Balanced, "Equilibrado"),
            new PowerModeOption(UserConfiguredPowerMode.BestPerformance, "Melhor desempenho")
        });
        History = [];
        ReadCommand = new AsyncRelayCommand(ReadAsync, () => !IsBusy);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => CanCapture);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => CanApply);
        RevertCommand = new AsyncRelayCommand(RevertAsync, () => CanRevert);
    }

    public IReadOnlyList<PowerModeOption> Options { get; }
    public ObservableCollection<PowerModeHistoryItem> History { get; }
    public AsyncRelayCommand ReadCommand { get; }
    public AsyncRelayCommand CaptureCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand RevertCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ReadCommand.NotifyCanExecuteChanged();
                CaptureCommand.NotifyCanExecuteChanged();
                ApplyCommand.NotifyCanExecuteChanged();
                RevertCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public PowerModeOption? SelectedAcTarget
    {
        get => _selectedAcTarget;
        set
        {
            if (!SetProperty(ref _selectedAcTarget, value)) return;
            InvalidateCapture("O alvo AC mudou; capture novamente antes de revisar.");
            NotifyEligibility();
        }
    }

    public PowerModeOption? SelectedDcTarget
    {
        get => _selectedDcTarget;
        set
        {
            if (!SetProperty(ref _selectedDcTarget, value)) return;
            InvalidateCapture("O alvo DC/bateria mudou; capture novamente antes de revisar.");
            NotifyEligibility();
        }
    }

    public bool ReviewedAcConsequences
    {
        get => _reviewedAcConsequences;
        set => SetReview(ref _reviewedAcConsequences, value);
    }

    public bool ReviewedDcConsequences
    {
        get => _reviewedDcConsequences;
        set => SetReview(ref _reviewedDcConsequences, value);
    }

    public bool AuthorizeAcConsequences { get => _authorizeAcConsequences; set => SetAuthorization(ref _authorizeAcConsequences, value); }
    public bool AuthorizeDcConsequences { get => _authorizeDcConsequences; set => SetAuthorization(ref _authorizeDcConsequences, value); }
    public bool AuthorizeApply { get => _authorizeApply; set => SetAuthorization(ref _authorizeApply, value); }
    public bool AuthorizeRevert { get => _authorizeRevert; set => SetAuthorization(ref _authorizeRevert, value); }
    public bool AcceptRecoveryConsequences { get => _acceptRecoveryConsequences; set => SetAuthorization(ref _acceptRecoveryConsequences, value); }
    public string ApplyPhrase { get => _applyPhrase; set { if (SetProperty(ref _applyPhrase, value)) NotifyAuthorizationEligibility(); } }
    public string RevertPhrase { get => _revertPhrase; set { if (SetProperty(ref _revertPhrase, value)) NotifyAuthorizationEligibility(); } }

    public bool CanSelectAc => _preview?.CanModifyAc == true && !IsBusy;
    public bool CanSelectDc => _preview?.CanModifyDc == true && !IsBusy;
    public bool CanCapture => !IsBusy && _preview?.CanModifyAc == true && _preview.CanModifyDc &&
        ((_selectedAcTarget is not null && _preview.CanModifyAc) ||
         (_selectedDcTarget is not null && _preview.CanModifyDc));
    public bool HasCapture => _capture is not null;
    public bool CanReviewAcConsent => _capture?.TargetAcMode is not null;
    public bool CanReviewDcConsent => _capture?.TargetDcMode is not null;
    public bool CanConsentForReview => _capture is not null &&
        (_capture.TargetAcMode is null || ReviewedAcConsequences) &&
        (_capture.TargetDcMode is null || ReviewedDcConsequences);
    public bool CanApply => !IsBusy && _capture?.State == PowerModeOperationState.Captured &&
        _capture.SchemaVersion >= 2 && _capture.CapturedActivePlanId is not null &&
        (_capture.TargetAcMode is null || AuthorizeAcConsequences) &&
        (_capture.TargetDcMode is null || AuthorizeDcConsequences) && AuthorizeApply &&
        string.Equals(ApplyPhrase, RequiredApplyPhrase, StringComparison.Ordinal);
    public bool CanRevert => !IsBusy && _capture is { SchemaVersion: >= 2, CapturedActivePlanId: not null } && IsRevertable(_capture.State) &&
        AcceptRecoveryConsequences && AuthorizeRevert &&
        string.Equals(RevertPhrase, RequiredRevertPhrase, StringComparison.Ordinal);
    public bool HasAcTarget => SelectedAcTarget is not null;
    public bool HasDcTarget => SelectedDcTarget is not null;
    public string CurrentAcText => FormatMode(_preview?.AcModeId, _preview?.AcErrorCode, _preview?.ApiAvailable == true, "AC/tomada");
    public string CurrentDcText => FormatMode(_preview?.DcModeId, _preview?.DcErrorCode, _preview?.ApiAvailable == true, "DC/bateria");
    public string ActivePlanText => _preview?.ActivePlanId is Guid planId
        ? $"Plano ativo lido nesta sessão: {planId:D} · comparação separada do modo AC/DC"
        : _preview is null ? "Plano ativo: ainda não consultado." :
            $"Plano ativo: indisponível{(_preview.ActivePlanErrorCode is uint error ? $" (Windows {error})" : string.Empty)}; aplicação bloqueada.";
    public string CompatibilityText => _preview is null
        ? "Compatibilidade ainda não consultada. A leitura ocorre somente quando solicitada."
        : _preview.CompatibilityReason;
    public string AcConsequence => FormatConsequence(SelectedAcTarget?.Mode, isDc: false);
    public string DcConsequence => FormatConsequence(SelectedDcTarget?.Mode, isDc: true);
    public string CaptureSummary => _capture is null
        ? "Nenhuma captura persistida."
        : $"Operação {_capture.OperationId.ToString("N")[..8].ToUpperInvariant()} · AC: {FormatCaptured(_capture.PreviousAcMode, _capture.TargetAcMode)} · DC/bateria: {FormatCaptured(_capture.PreviousDcMode, _capture.TargetDcMode)} · plano ativo capturado: {_capture.CapturedActivePlanId?.ToString("D") ?? "não registrado"}.";
    public string RequiredApplyPhrase => _capture is null
        ? "Uma frase de confirmação só aparece depois da captura."
        : PowerModeApproval.RequiredPhrase(_capture.OperationId, PowerModeAction.Apply);
    public string RequiredRevertPhrase => _capture is null
        ? "A restauração exige uma captura persistida e confirmação própria."
        : PowerModeApproval.RequiredPhrase(_capture.OperationId, PowerModeAction.Revert);
    public string AuthorizationStatus => "Execução bloqueada por padrão. Revisão e consentimento demonstrativo não autorizam escrita; a autorização abaixo vale apenas para esta captura, ação e frase exata.";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ResultLabel
    {
        get => _resultLabel;
        private set => SetProperty(ref _resultLabel, value);
    }

    public async Task LoadHistoryAsync()
    {
        try
        {
            var entries = await _service.LoadHistoryAsync();
            History.Clear();
            foreach (var entry in entries)
            {
                History.Add(new PowerModeHistoryItem(
                    entry,
                    GetStateLabel(entry.State),
                    entry.CapturedAtUtc.ToLocalTime().ToString("g"),
                    $"AC: {FormatCaptured(entry.PreviousAcMode, entry.TargetAcMode)} · DC/bateria: {FormatCaptured(entry.PreviousDcMode, entry.TargetDcMode)} · plano ativo: {entry.CapturedActivePlanId?.ToString("D") ?? "não registrado"}. {entry.Detail}"));
            }
            var selectedId = _capture?.OperationId;
            _capture = (selectedId is Guid currentId ? entries.FirstOrDefault(item => item.OperationId == currentId) : null)
                ?? entries.FirstOrDefault(item => item.SchemaVersion >= 2 && item.CapturedActivePlanId is not null &&
                    (item.State == PowerModeOperationState.Captured || IsRevertable(item.State)));
            NotifyCaptureProperties();
            NotifyAuthorizationEligibility();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Não foi possível ler o histórico de modos de energia ({exception.GetType().Name}). Nenhuma configuração foi alterada.";
        }
    }

    private async Task ReadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _preview = await Task.Run(_service.ReadPreview);
            OnPropertyChanged(nameof(CurrentAcText));
            OnPropertyChanged(nameof(CurrentDcText));
            OnPropertyChanged(nameof(ActivePlanText));
            OnPropertyChanged(nameof(CompatibilityText));
            var captureIsStale = _capture is not null && !PowerModeOperationService.MatchesCapturedState(_preview, _capture);
            if (captureIsStale)
            {
                ResetAuthorization();
                ResultLabel = "CAPTURA DESATUALIZADA · NOVA REVISÃO NECESSÁRIA";
                StatusMessage = "AC, DC ou o plano ativo mudou desde a captura. O estado anterior não será reutilizado; faça nova captura e revisão.";
            }
            else
            {
                ResultLabel = _preview.ApiAvailable ? "PRÉVIA REAL · SOMENTE LEITURA" : "API INDISPONÍVEL · SEM ESCRITA";
                StatusMessage = _preview.ApiAvailable
                    ? "Leitura real das preferências configuradas concluída. Isto não identifica o modo efetivo do sistema nem promete ganho de desempenho."
                    : _preview.CompatibilityReason;
            }
            NotifyEligibility();
        }
        catch (Exception exception)
        {
            _preview = null;
            OnPropertyChanged(nameof(CurrentAcText));
            OnPropertyChanged(nameof(CurrentDcText));
            OnPropertyChanged(nameof(ActivePlanText));
            OnPropertyChanged(nameof(CompatibilityText));
            ResultLabel = "LEITURA REAL INDISPONÍVEL";
            StatusMessage = $"A leitura falhou ({exception.GetType().Name}); nenhuma escrita foi tentada.";
            NotifyEligibility();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CaptureAsync()
    {
        if (!CanCapture) return;
        IsBusy = true;
        try
        {
            _capture = await Task.Run(async () => await _service.CaptureAsync(
                SelectedAcTarget?.Mode,
                SelectedDcTarget?.Mode));
            ResetAuthorization();
            ReviewedAcConsequences = false;
            ReviewedDcConsequences = false;
            NotifyCaptureProperties();
            ResultLabel = "CAPTURA REAL PERSISTIDA · NÃO APLICADA";
            StatusMessage = "Estado anterior e alvos AC/DC gravados no histórico local. Nenhuma configuração foi alterada; isto não é backup do Windows.";
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            _capture = null;
            NotifyCaptureProperties();
            ResultLabel = "CAPTURA NÃO CONFIRMADA";
            StatusMessage = $"A captura não foi persistida ({exception.GetType().Name}); nenhuma escrita foi tentada.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InvalidateCapture(string message)
    {
        if (_capture is null) return;
        _capture = null;
        ReviewedAcConsequences = false;
        ReviewedDcConsequences = false;
        ResetAuthorization();
        ResultLabel = "PLANO ALTERADO · NOVA CAPTURA NECESSÁRIA";
        StatusMessage = message;
        NotifyCaptureProperties();
    }

    private void NotifyEligibility()
    {
        OnPropertyChanged(nameof(CanSelectAc));
        OnPropertyChanged(nameof(CanSelectDc));
        OnPropertyChanged(nameof(CanCapture));
        OnPropertyChanged(nameof(HasAcTarget));
        OnPropertyChanged(nameof(HasDcTarget));
        OnPropertyChanged(nameof(AcConsequence));
        OnPropertyChanged(nameof(DcConsequence));
        CaptureCommand.NotifyCanExecuteChanged();
    }

    private async Task ApplyAsync()
    {
        if (!CanApply || _capture is null) return;
        IsBusy = true;
        try
        {
            // Fresh read for the approved operation. A mismatch requires a new review, not a setter.
            var fresh = await Task.Run(_adapter.ReadConfiguredModes);
            _preview = fresh;
            OnPropertyChanged(nameof(CurrentAcText));
            OnPropertyChanged(nameof(CurrentDcText));
            OnPropertyChanged(nameof(ActivePlanText));
            OnPropertyChanged(nameof(CompatibilityText));
            if (!PowerModeOperationService.MatchesCapturedState(fresh, _capture))
            {
                ResetAuthorization();
                ResultLabel = "ESTADO MUDOU · NOVA REVISÃO NECESSÁRIA";
                StatusMessage = "AC, DC ou o plano ativo mudou desde a captura aprovada. Nenhum setter foi chamado; faça uma nova leitura, capture novamente e revise as consequências.";
                return;
            }

            var approval = PowerModeApproval.Confirm(
                _capture,
                PowerModeAction.Apply,
                _capture.TargetAcMode is null || AuthorizeAcConsequences,
                _capture.TargetDcMode is null || AuthorizeDcConsequences,
                recoveryConsequencesAccepted: false,
                confirmationPhrase: ApplyPhrase);
            // The default service remains denied. A separate enabled policy is created only inside
            // this explicit, phrase-checked apply command; it is never used for reads/captures.
            var explicitlyApprovedService = new PowerModeOperationService(
                _adapter, _store, new ConfirmedPowerModeMutationPolicy(enabled: true));
            _capture = await explicitlyApprovedService.ApplyAsync(_capture.OperationId, approval);
            ResetAuthorization();
            ResultLabel = GetStateLabel(_capture.State);
            StatusMessage = _capture.Detail;
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"A aplicação foi recusada ou falhou ({exception.GetType().Name}); consulte novamente AC, DC e plano ativo. Não declare sucesso sem leitura posterior.";
        }
        finally { IsBusy = false; NotifyAuthorizationEligibility(); }
    }

    private async Task RevertAsync()
    {
        if (!CanRevert || _capture is null) return;
        IsBusy = true;
        try
        {
            // RevertAsync makes its own fresh state read and refuses conflicts before any setter.
            _ = await Task.Run(_adapter.ReadConfiguredModes);
            var approval = PowerModeApproval.Confirm(
                _capture,
                PowerModeAction.Revert,
                acConsequencesAccepted: false,
                dcConsequencesAccepted: false,
                recoveryConsequencesAccepted: AcceptRecoveryConsequences,
                confirmationPhrase: RevertPhrase);
            var explicitlyApprovedService = new PowerModeOperationService(
                _adapter, _store, new ConfirmedPowerModeMutationPolicy(enabled: true));
            _capture = await explicitlyApprovedService.RevertAsync(_capture.OperationId, approval);
            ResetAuthorization();
            ResultLabel = GetStateLabel(_capture.State);
            StatusMessage = _capture.Detail;
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"A restauração foi recusada ou falhou ({exception.GetType().Name}); releia o estado e revise o conflito manualmente.";
        }
        finally { IsBusy = false; NotifyAuthorizationEligibility(); }
    }

    private void SetReview(ref bool field, bool value)
    {
        if (!SetProperty(ref field, value)) return;
        OnPropertyChanged(nameof(CanConsentForReview));
    }

    private void SetAuthorization(ref bool field, bool value)
    {
        if (SetProperty(ref field, value)) NotifyAuthorizationEligibility();
    }

    private void ResetAuthorization()
    {
        _authorizeAcConsequences = false;
        _authorizeDcConsequences = false;
        _authorizeApply = false;
        _authorizeRevert = false;
        _acceptRecoveryConsequences = false;
        _applyPhrase = string.Empty;
        _revertPhrase = string.Empty;
        foreach (var property in new[] { nameof(AuthorizeAcConsequences), nameof(AuthorizeDcConsequences), nameof(AuthorizeApply),
                     nameof(AuthorizeRevert), nameof(AcceptRecoveryConsequences), nameof(ApplyPhrase), nameof(RevertPhrase) })
            OnPropertyChanged(property);
        NotifyAuthorizationEligibility();
    }

    private void NotifyCaptureProperties()
    {
        foreach (var property in new[] { nameof(HasCapture), nameof(CaptureSummary), nameof(RequiredApplyPhrase), nameof(RequiredRevertPhrase),
                     nameof(CanReviewAcConsent), nameof(CanReviewDcConsent), nameof(CanConsentForReview), nameof(CanApply), nameof(CanRevert) })
            OnPropertyChanged(property);
        NotifyAuthorizationEligibility();
    }

    private void NotifyAuthorizationEligibility()
    {
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanRevert));
        ApplyCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
    }

    private static bool IsRevertable(PowerModeOperationState state) => state is
        PowerModeOperationState.Verified or PowerModeOperationState.MutationMayHaveStarted or
        PowerModeOperationState.MutationUnverified or PowerModeOperationState.ReversionUnverified;

    private static string FormatMode(Guid? id, uint? errorCode, bool apiAvailable, string side)
    {
        if (!apiAvailable) return $"{side}: indisponível";
        if (id is not Guid modeId)
            return errorCode is uint error ? $"{side}: leitura indisponível (Windows {error})" : $"{side}: leitura indisponível";
        return UserConfiguredPowerModes.TryParse(modeId, out var mode)
            ? $"{side}: {UserConfiguredPowerModes.DisplayName(mode)} · preferência configurada"
            : $"{side}: valor não reconhecido; alteração reversível bloqueada";
    }

    private static string FormatCaptured(UserConfiguredPowerMode? previous, UserConfiguredPowerMode? target) =>
        target is null ? "preservado/não selecionado" :
            $"{UserConfiguredPowerModes.DisplayName(previous!.Value)} → {UserConfiguredPowerModes.DisplayName(target.Value)}";

    private static string FormatConsequence(UserConfiguredPowerMode? target, bool isDc)
    {
        if (target is null) return "Nenhuma preferência selecionada; este lado será preservado.";
        var side = isDc ? "na bateria" : "ligado à tomada";
        var impact = target.Value switch
        {
            UserConfiguredPowerMode.BestPerformance => $"A preferência favorece desempenho {side}; pode aumentar consumo, temperatura e ruído da ventoinha.",
            UserConfiguredPowerMode.BestEfficiency => $"A preferência favorece eficiência {side}; pode reduzir resposta ou desempenho em algumas cargas.",
            _ => $"A preferência Equilibrado busca equilíbrio {side}; o resultado depende do Windows e do equipamento."
        };
        return $"{impact} O Windows pode sobrepor a preferência configurada; não há promessa de FPS.";
    }

    private static string GetStateLabel(PowerModeOperationState state) => state switch
    {
        PowerModeOperationState.Captured => "CAPTURA REAL PERSISTIDA · NÃO APLICADA",
        PowerModeOperationState.MutationMayHaveStarted => "OPERAÇÃO POSSIVELMENTE INICIADA · REVISÃO MANUAL",
        PowerModeOperationState.Verified => "PREFERÊNCIA CONFIGURADA CONFIRMADA POR LEITURA POSTERIOR",
        PowerModeOperationState.NoChangeRequired => "ALVOS JÁ CONFIGURADOS · SEM ESCRITA",
        PowerModeOperationState.Conflict => "CONFLITO · ESCRITA RECUSADA",
        PowerModeOperationState.MutationUnverified => "RESULTADO PARCIAL/NÃO VERIFICADO · REVISÃO MANUAL",
        PowerModeOperationState.ReversionMayHaveStarted => "RESTAURAÇÃO POSSIVELMENTE INICIADA · REVISÃO MANUAL",
        PowerModeOperationState.Reverted => "PREFERÊNCIAS CAPTURADAS RESTAURADAS POR LEITURA",
        PowerModeOperationState.ReversionUnverified => "RESTAURAÇÃO NÃO VERIFICADA · REVISÃO MANUAL",
        _ => "ESTADO DESCONHECIDO · REVISÃO MANUAL"
    };
}
