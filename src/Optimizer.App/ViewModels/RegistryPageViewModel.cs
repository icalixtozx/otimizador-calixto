using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Recovery;
using Optimizer.Core.Registry;

namespace Optimizer.App.ViewModels;

public sealed record RegistryVisibilityOption(FileExtensionVisibility Value, string Label);

public sealed record RegistryOperationHistoryRow(
    Guid OperationId,
    string State,
    string Date,
    string Event,
    string Recovery);

public sealed class RegistryPageViewModel : ObservableObject
{
    private readonly RegistryPreferenceOperationService _service;
    private FileExtensionVisibility _selectedVisibility = FileExtensionVisibility.Show;
    private RegistryPreferenceReview? _review;
    private bool _isBusy;
    private bool _consequenceAccepted;
    private string _statusMessage = "Nenhuma leitura do Registro foi solicitada.";
    private string _resultMessage = "Nenhuma captura desta sessão.";

    public RegistryPageViewModel(RegistryPreferenceOperationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        VisibilityOptions = Array.AsReadOnly(new[]
        {
            new RegistryVisibilityOption(FileExtensionVisibility.Show, "Exibir extensões"),
            new RegistryVisibilityOption(FileExtensionVisibility.Hide, "Ocultar extensões")
        });
        History = [];
        ReviewCommand = new AsyncRelayCommand(ReviewAsync, () => !IsBusy);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, CanCapture);
        RefreshHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync, () => !IsBusy);
    }

    public IReadOnlyList<RegistryVisibilityOption> VisibilityOptions { get; }
    public ObservableCollection<RegistryOperationHistoryRow> History { get; }
    public AsyncRelayCommand ReviewCommand { get; }
    public AsyncRelayCommand CaptureCommand { get; }
    public AsyncRelayCommand RefreshHistoryCommand { get; }

    public FileExtensionVisibility SelectedVisibility
    {
        get => _selectedVisibility;
        set
        {
            if (IsBusy) return;
            if (!SetProperty(ref _selectedVisibility, value)) return;
            Review = null;
            ConsequenceAccepted = false;
            ResultMessage = "O alvo mudou. Leia o estado atual e revise a nova consequência.";
        }
    }

    public RegistryPreferenceReview? Review
    {
        get => _review;
        private set
        {
            if (!SetProperty(ref _review, value)) return;
            OnPropertyChanged(nameof(HasReview));
            OnPropertyChanged(nameof(CurrentStateText));
            OnPropertyChanged(nameof(TargetText));
            OnPropertyChanged(nameof(CompatibilityText));
            OnPropertyChanged(nameof(ConsequenceText));
            OnPropertyChanged(nameof(ManagementText));
            OnPropertyChanged(nameof(ReviewedAtText));
            CaptureCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasReview => Review is not null;
    public bool CanChangeTarget => !IsBusy;
    public string AddressText => @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\HideFileExt · Registry64 · REG_DWORD";
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanChangeTarget));
            ReviewCommand.NotifyCanExecuteChanged();
            CaptureCommand.NotifyCanExecuteChanged();
            RefreshHistoryCommand.NotifyCanExecuteChanged();
        }
    }

    public bool ConsequenceAccepted
    {
        get => _consequenceAccepted;
        set
        {
            if (!SetProperty(ref _consequenceAccepted, value)) return;
            OnPropertyChanged(nameof(CaptureNotice));
            CaptureCommand.NotifyCanExecuteChanged();
        }
    }

    public string CurrentStateText => Review is null
        ? "Ainda não lido."
        : DescribeState(Review.CurrentState);

    public string TargetText => Review is null
        ? "Aguarda leitura real."
        : Review.TargetValue == RegistryPreferenceCatalog.ShowFileExtensionsValue
            ? "REG_DWORD 0 · exibir extensões"
            : "REG_DWORD 1 · ocultar extensões";

    public string CompatibilityText
    {
        get
        {
            if (Review is null) return "A compatibilidade será avaliada quando você solicitar a leitura.";
            var item = Review.Plan.Items.Single();
            var status = item.Status switch
            {
                Optimizer.Core.Adjustments.PlanItemStatus.ReadyForExplicitApproval => "Revisão compatível; execução real continua bloqueada",
                Optimizer.Core.Adjustments.PlanItemStatus.Blocked => "Indisponível",
                Optimizer.Core.Adjustments.PlanItemStatus.Incompatible => "Incompatível",
                Optimizer.Core.Adjustments.PlanItemStatus.UnknownAdjustment => "Compatibilidade desconhecida",
                _ => "Não disponível para esta ação"
            };
            return $"{status}. {item.Reason}";
        }
    }

    public string ConsequenceText => Review?.Plan.Items.Single().Consequence ??
        "Exibir ajuda a distinguir extensões. Ocultar pode deixar nomes ambíguos. Não há benefício de desempenho alegado.";

    public string ManagementText => Review is null
        ? "Estado de gerenciamento ainda não consultado."
        : $"Gerenciamento: {Review.Management.Status}. {Review.Management.Detail} Aplicação e restauração permanecem bloqueadas.";

    public string ReviewedAtText => Review is null
        ? string.Empty
        : $"Prévia baseada no estado real · {Review.ReviewedAtUtc.ToLocalTime():g}";

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

    public string CaptureNotice => ConsequenceAccepted
        ? "Consentimento revisado. Ele autoriza apenas registrar a consequência e a captura no histórico; não autoriza escrita no Registro."
        : "A captura só será salva depois de revisar e aceitar a consequência. Nenhuma alteração será aplicada.";

    public async Task LoadHistoryAsync()
    {
        try
        {
            var entries = await _service.LoadHistoryAsync();
            var registryEntries = entries
                .Where(entry => entry.ProfileId == RegistryPreferenceCatalog.ProfileId)
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .ToArray();
            History.Clear();
            foreach (var entry in registryEntries)
            {
                History.Add(new RegistryOperationHistoryRow(
                    entry.OperationId,
                    StateLabel(entry.State),
                    entry.CreatedAtUtc.ToLocalTime().ToString("g"),
                    entry.Events.LastOrDefault()?.Message ?? "Sem evento no diário.",
                    OperationRecoveryEvaluator.Assess(entry).Message));
            }
        }
        catch (Exception exception)
        {
            ResultMessage = $"O histórico compartilhado não pôde ser lido ({exception.GetType().Name}). Nenhuma ação do Windows foi executada.";
        }
    }

    private async Task ReviewAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Review = null;
        ConsequenceAccepted = false;
        var targetVisibility = SelectedVisibility;
        try
        {
            var review = await Task.Run(() => _service.CreateReview(CurrentPlatform(), targetVisibility));
            if (targetVisibility != SelectedVisibility)
            {
                StatusMessage = "O alvo mudou durante a leitura; descartei a prévia. Leia novamente para revisar o alvo atual.";
                return;
            }
            Review = review;
            StatusMessage = "Leitura real concluída. A página não aplica a preferência e não chama setters.";
            ResultMessage = "Confira estado, compatibilidade, gerenciamento e consequência antes de registrar a captura.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"A prévia não pôde ser criada ({exception.GetType().Name}). Nenhuma escrita foi tentada.";
            ResultMessage = "Estado atual indisponível; atualize ou confira a configuração nas opções nativas do Explorador.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CaptureAsync()
    {
        if (!CanCapture() || Review is null) return;
        IsBusy = true;
        try
        {
            var entry = await _service.CaptureAsync(Review, consequenceAccepted: true);
            ResultMessage = entry.State switch
            {
                JournalOperationState.SnapshotsCaptured => $"Consentimento e captura persistidos · operação {entry.OperationId.ToString("N")[..8].ToUpperInvariant()}. Nenhum valor do Registro foi alterado.",
                JournalOperationState.Conflict => "O valor mudou desde a prévia; conflito registrado e captura recusada. Nenhuma escrita foi tentada.",
                JournalOperationState.Failed or JournalOperationState.Interrupted => $"Captura não concluída ({entry.State}); consulte o histórico. Nenhuma escrita foi tentada.",
                _ => $"Estado registrado: {entry.State}. Nenhuma escrita foi tentada."
            };
            ConsequenceAccepted = false;
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            ResultMessage = $"A captura não foi confirmada ({exception.GetType().Name}). Nenhuma escrita foi tentada.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCapture() => !IsBusy && Review is not null && ConsequenceAccepted;

    private static PlatformDescriptor CurrentPlatform() => new(
        OperatingSystem.IsWindows(),
        RuntimeInformation.OSArchitecture.ToString(),
        OperatingSystem.IsWindows() ? Environment.OSVersion.Version.Build : null);

    private static string DescribeState(RegistryValueState state)
    {
        if (!state.KeyExists) return "A chave não existe. O protótipo não a cria.";
        if (!state.ValueExists) return "A chave existe; HideFileExt está ausente.";
        var value = state.Value!;
        if (value == RegistryPreferenceCatalog.ShowFileExtensionsValue) return "Presente · REG_DWORD 0 · extensões configuradas para aparecer.";
        if (value == RegistryPreferenceCatalog.HideFileExtensionsValue) return "Presente · REG_DWORD 1 · extensões configuradas para ficar ocultas.";
        return $"Presente · tipo {value.Kind}; o conteúdo não corresponde ao mapeamento documentado.";
    }

    private static string StateLabel(JournalOperationState state) => state switch
    {
        JournalOperationState.Planned => "Plano registrado",
        JournalOperationState.ConsentRecorded => "Consentimento revisado",
        JournalOperationState.SnapshotsCaptured => "Captura real registrada",
        JournalOperationState.Conflict => "Conflito; sem escrita",
        JournalOperationState.Failed => "Falha; revisão manual",
        JournalOperationState.Interrupted => "Interrompido; revisão manual",
        JournalOperationState.MutationMayHaveStarted => "Aplicação possivelmente iniciada",
        JournalOperationState.MutationVerified => "Aplicação verificada no diário",
        JournalOperationState.RestorationMayHaveStarted => "Restauração possivelmente iniciada",
        JournalOperationState.RestoredVerified => "Restauração verificada no diário",
        JournalOperationState.NoChangeRequired => "Nenhuma escrita necessária",
        _ => "Estado desconhecido"
    };
}
