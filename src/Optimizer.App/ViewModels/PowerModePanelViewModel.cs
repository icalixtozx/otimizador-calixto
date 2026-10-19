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

/// <summary>Read-only real preview and persisted capture UI; the production mutation policy remains denied.</summary>
public sealed class PowerModePanelViewModel : ObservableObject
{
    private readonly PowerModeOperationService _service;
    private PowerModeReadResult? _preview;
    private PowerModeOperationRecord? _capture;
    private PowerModeOption? _selectedAcTarget;
    private PowerModeOption? _selectedDcTarget;
    private bool _acceptAcConsequences;
    private bool _acceptDcConsequences;
    private bool _isBusy;
    private string _statusMessage = "Leitura do modo de energia não solicitada. Nenhuma configuração foi consultada.";
    private string _resultLabel = "SEM LEITURA REAL";

    public PowerModePanelViewModel(IPowerModeAdapter adapter, string historyDirectory)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _service = new PowerModeOperationService(
            adapter,
            new JsonPowerModeOperationStore(Path.Combine(historyDirectory, "RealPowerMode")));
        Options = Array.AsReadOnly(new[]
        {
            new PowerModeOption(UserConfiguredPowerMode.BestEfficiency, "Melhor eficiência energética"),
            new PowerModeOption(UserConfiguredPowerMode.Balanced, "Equilibrado"),
            new PowerModeOption(UserConfiguredPowerMode.BestPerformance, "Melhor desempenho")
        });
        History = [];
        ReadCommand = new AsyncRelayCommand(ReadAsync, () => !IsBusy);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => CanCapture);
    }

    public IReadOnlyList<PowerModeOption> Options { get; }
    public ObservableCollection<PowerModeHistoryItem> History { get; }
    public AsyncRelayCommand ReadCommand { get; }
    public AsyncRelayCommand CaptureCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ReadCommand.NotifyCanExecuteChanged();
                CaptureCommand.NotifyCanExecuteChanged();
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

    public bool AcceptAcConsequences
    {
        get => _acceptAcConsequences;
        set
        {
            if (SetProperty(ref _acceptAcConsequences, value)) OnPropertyChanged(nameof(CanConsentForReview));
        }
    }

    public bool AcceptDcConsequences
    {
        get => _acceptDcConsequences;
        set
        {
            if (SetProperty(ref _acceptDcConsequences, value)) OnPropertyChanged(nameof(CanConsentForReview));
        }
    }

    public bool CanSelectAc => _preview?.CanModifyAc == true && !IsBusy;
    public bool CanSelectDc => _preview?.CanModifyDc == true && !IsBusy;
    public bool CanCapture => !IsBusy && _preview?.ApiAvailable == true &&
        ((_selectedAcTarget is not null && _preview.CanModifyAc) ||
         (_selectedDcTarget is not null && _preview.CanModifyDc));
    public bool HasCapture => _capture is not null;
    public bool CanReviewAcConsent => _capture?.TargetAcMode is not null;
    public bool CanReviewDcConsent => _capture?.TargetDcMode is not null;
    public bool CanConsentForReview => _capture is not null &&
        (_capture.TargetAcMode is null || AcceptAcConsequences) &&
        (_capture.TargetDcMode is null || AcceptDcConsequences);
    public bool HasAcTarget => SelectedAcTarget is not null;
    public bool HasDcTarget => SelectedDcTarget is not null;
    public string CurrentAcText => FormatMode(_preview?.AcModeId, _preview?.AcErrorCode, _preview?.ApiAvailable == true, "AC/tomada");
    public string CurrentDcText => FormatMode(_preview?.DcModeId, _preview?.DcErrorCode, _preview?.ApiAvailable == true, "DC/bateria");
    public string CompatibilityText => _preview is null
        ? "Compatibilidade ainda não consultada. A leitura ocorre somente quando solicitada."
        : _preview.CompatibilityReason;
    public string AcConsequence => FormatConsequence(SelectedAcTarget?.Mode, isDc: false);
    public string DcConsequence => FormatConsequence(SelectedDcTarget?.Mode, isDc: true);
    public string CaptureSummary => _capture is null
        ? "Nenhuma captura persistida."
        : $"Operação {_capture.OperationId.ToString("N")[..8].ToUpperInvariant()} · AC: {FormatCaptured(_capture.PreviousAcMode, _capture.TargetAcMode)} · DC/bateria: {FormatCaptured(_capture.PreviousDcMode, _capture.TargetDcMode)}.";
    public string RequiredApplyPhrase => _capture is null
        ? "Uma frase de confirmação só aparece depois da captura."
        : $"Se houver aprovação futura da operação: {PowerModeApproval.RequiredPhrase(_capture.OperationId, PowerModeAction.Apply)}";

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
                    $"AC: {FormatCaptured(entry.PreviousAcMode, entry.TargetAcMode)} · DC/bateria: {FormatCaptured(entry.PreviousDcMode, entry.TargetDcMode)}. {entry.Detail}"));
            }
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
            OnPropertyChanged(nameof(CompatibilityText));
            ResultLabel = _preview.ApiAvailable ? "PRÉVIA REAL · SOMENTE LEITURA" : "API INDISPONÍVEL · SEM ESCRITA";
            StatusMessage = _preview.ApiAvailable
                ? "Leitura real das preferências configuradas concluída. Isto não identifica o modo efetivo do sistema nem promete ganho de desempenho."
                : _preview.CompatibilityReason;
            NotifyEligibility();
        }
        catch (Exception exception)
        {
            _preview = null;
            OnPropertyChanged(nameof(CurrentAcText));
            OnPropertyChanged(nameof(CurrentDcText));
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
            AcceptAcConsequences = false;
            AcceptDcConsequences = false;
            OnPropertyChanged(nameof(HasCapture));
            OnPropertyChanged(nameof(CaptureSummary));
            OnPropertyChanged(nameof(RequiredApplyPhrase));
            OnPropertyChanged(nameof(CanReviewAcConsent));
            OnPropertyChanged(nameof(CanReviewDcConsent));
            OnPropertyChanged(nameof(CanConsentForReview));
            ResultLabel = "CAPTURA REAL PERSISTIDA · NÃO APLICADA";
            StatusMessage = "Estado anterior e alvos AC/DC gravados no histórico local. Nenhuma configuração foi alterada; isto não é backup do Windows.";
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            _capture = null;
            OnPropertyChanged(nameof(HasCapture));
            OnPropertyChanged(nameof(CaptureSummary));
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
        AcceptAcConsequences = false;
        AcceptDcConsequences = false;
        ResultLabel = "PLANO ALTERADO · NOVA CAPTURA NECESSÁRIA";
        StatusMessage = message;
        OnPropertyChanged(nameof(HasCapture));
        OnPropertyChanged(nameof(CaptureSummary));
        OnPropertyChanged(nameof(RequiredApplyPhrase));
        OnPropertyChanged(nameof(CanReviewAcConsent));
        OnPropertyChanged(nameof(CanReviewDcConsent));
        OnPropertyChanged(nameof(CanConsentForReview));
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
