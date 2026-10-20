using System.Globalization;
using System.Text.Json;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Consent;
using Optimizer.Core.Profiles;
using Optimizer.Core.Recovery;

namespace Optimizer.Core.Processes;

/// <summary>
/// Plans and journals an individual process-priority change. The production policy blocks every write
/// unless an explicit operation policy is injected; the app uses the blocked default.
/// </summary>
public sealed class ProcessPriorityOperationService
{
    public const string AdjustmentId = "process.priority";
    public const string ProfileId = "process-priority";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IProcessPriorityAdapter _adapter;
    private readonly JsonOperationJournalStore _journalStore;
    private readonly PlanConsentService _consentService;
    private readonly OperationJournalCoordinator _journal;
    private readonly IProcessPriorityMutationPolicy _mutationPolicy;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public ProcessPriorityOperationService(
        IProcessPriorityAdapter adapter,
        JsonOperationJournalStore journalStore,
        PlanConsentService consentService,
        OperationJournalCoordinator journal,
        IProcessPriorityMutationPolicy? mutationPolicy = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        _consentService = consentService ?? throw new ArgumentNullException(nameof(consentService));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _mutationPolicy = mutationPolicy ?? new BlockedProcessPriorityMutationPolicy();
    }

    public ProcessInventoryResult ListProcesses() => _adapter.ListProcesses();

    public ProcessPriorityReview CreateReview(
        ProcessIdentity selectedIdentity,
        ProcessPriorityLevel targetPriority,
        PlatformDescriptor platform)
    {
        ArgumentNullException.ThrowIfNull(selectedIdentity);
        ArgumentNullException.ThrowIfNull(platform);
        if (!ProcessPriorityTargets.Allowed.Contains(targetPriority))
            throw new ArgumentOutOfRangeException(nameof(targetPriority), "The requested target is not an allowed process priority.");

        var observed = _adapter.Inspect(selectedIdentity.ProcessId);
        EnsureReviewable(observed);
        if (selectedIdentity.CompareTo(observed.Identity) != ProcessIdentityComparison.Match)
            throw new InvalidOperationException("O processo selecionado terminou, mudou de identidade ou não pode ser identificado com segurança. Atualize a lista.");

        var currentPriority = observed.CurrentPriority!.Value;
        if (!ProcessPriorityTargets.Allowed.Contains(currentPriority))
            throw new InvalidOperationException(currentPriority == ProcessPriorityLevel.Realtime
                ? "Processos já em Tempo real não podem ser preparados para alteração ou restauração por este módulo."
                : "A classe de prioridade atual é desconhecida e não pode ser capturada com segurança.");

        var consequence = BuildConsequence(selectedIdentity.ProcessId, currentPriority, targetPriority);
        var risk = targetPriority == ProcessPriorityLevel.High
            ? AdjustmentRiskLevel.High
            : AdjustmentRiskLevel.Moderate;
        var definition = new AdjustmentDefinition(
            AdjustmentId,
            1,
            "Prioridade de um processo",
            "Define a classe de prioridade de agendamento para um único processo identificado por PID, criação e caminho quando acessível.",
            AdjustmentArea.Processes,
            risk,
            consequence,
            RequiresAdministrator: false,
            SupportedArchitectures: [platform.Architecture],
            MinimumWindowsBuild: null,
            Availability: AdjustmentAvailability.RequiresSpecificApproval,
            AvailabilityReason: "A execução é bloqueada por padrão e exige aprovação específica por operação.");
        var catalog = new AdjustmentCatalog([definition]);
        var compatibility = new AdjustmentCompatibilityEvaluator();
        var planService = new AdjustmentPlanService(catalog, compatibility);
        var profilePlanner = new ProfilePlanService(
            OptimizerProfileCatalog.CreateDefault(), catalog, compatibility, planService);
        var planResult = profilePlanner.CreatePlan("custom", platform, [AdjustmentId]);
        if (planResult.RejectedSelections.Count != 0 || !planResult.Plan.CanAcceptConsent || planResult.Plan.Items.Count != 1)
            throw new PlatformNotSupportedException(planResult.RejectedSelections.FirstOrDefault()?.Reason ??
                "Este processo ou sistema não é compatível com a revisão do ajuste.");

        return new(
            observed.Identity!,
            observed.Name ?? $"PID {observed.ProcessId}",
            currentPriority,
            targetPriority,
            planResult.Plan,
            DateTimeOffset.UtcNow,
            "Prévia baseada em leitura real. Prioridade maior não garante FPS e pode reduzir a capacidade de resposta de outros aplicativos.");
    }

    /// <summary>Stores plan consent and a fresh read-only snapshot. This method never invokes a setter.</summary>
    public async Task<OperationJournalEntry> CaptureAsync(
        ProcessPriorityReview review,
        bool consequencesAccepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        if (!review.Plan.CanAcceptConsent || review.Plan.Items.Count != 1 ||
            review.Plan.Items[0].AdjustmentId != AdjustmentId || review.Plan.Items[0].Availability != AdjustmentAvailability.RequiresSpecificApproval)
            throw new InvalidOperationException("A valid single-process review plan is required.");

        var entry = _journal.CreatePlanned(review.Plan, ProfileId);
        await _journalStore.SaveAsync(entry, cancellationToken);
        var receipt = _consentService.Record(review.Plan,
            new Dictionary<string, bool>(StringComparer.Ordinal) { [AdjustmentId] = consequencesAccepted });
        entry = _journal.RecordConsent(entry, review.Plan, receipt);
        await _journalStore.SaveAsync(entry, cancellationToken);
        if (!consequencesAccepted)
            throw new InvalidOperationException("A consequência foi recusada; nenhuma captura ou alteração foi iniciada.");

        var fresh = _adapter.Inspect(review.Identity.ProcessId);
        var issue = CompareCurrent(review.Identity, review.CurrentPriority, fresh);
        if (issue is not null)
        {
            entry = fresh.Status is ProcessInspectionStatus.AccessDenied or ProcessInspectionStatus.Failed
                ? _journal.RecordPreWriteFailure(entry, issue + " A captura foi recusada; nenhuma prioridade foi alterada.")
                : _journal.MarkConflict(entry, issue + " A captura foi recusada; nenhuma prioridade foi alterada.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            return entry;
        }

        var snapshot = new ProcessPrioritySnapshot(review.Identity, review.CurrentPriority,
            review.TargetPriority, string.Empty);
        snapshot = snapshot with
        {
            CaptureFingerprint = ProcessPriorityCaptureFingerprint.Compute(
                entry.OperationId, review.Plan.Fingerprint, snapshot)
        };
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProcessId"] = snapshot.Identity.ProcessId.ToString(CultureInfo.InvariantCulture),
            ["CreatedAtUtc"] = snapshot.Identity.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["ExecutablePath"] = snapshot.Identity.ExecutablePath ?? string.Empty,
            ["PreviousPriority"] = snapshot.PreviousPriority.ToString(),
            ["TargetPriority"] = snapshot.TargetPriority.ToString(),
            ["PlanJson"] = JsonSerializer.Serialize(review.Plan, JsonOptions),
            ["CaptureFingerprint"] = snapshot.CaptureFingerprint
        };
        entry = _journal.RecordSnapshot(entry, new AdjustmentSnapshot(
            AdjustmentId, 1, DateTimeOffset.UtcNow, values));
        await _journalStore.SaveAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<OperationJournalEntry> ApplyAsync(
        Guid operationId,
        ProcessPriorityApproval approval,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var entry = await LoadRequiredAsync(operationId, cancellationToken);
            var (plan, snapshot) = ValidateCapturedEntry(entry);
            EnsureAuthorized(approval, entry, snapshot, ProcessPriorityAction.Apply);
            if (entry.State != JournalOperationState.SnapshotsCaptured)
                throw new InvalidOperationException("Somente uma captura revisada pode iniciar a aplicação.");

            var before = _adapter.Inspect(snapshot.Identity.ProcessId);
            var issue = CompareCurrent(snapshot.Identity, snapshot.PreviousPriority, before);
            if (issue is not null)
            {
                if (before.Status is ProcessInspectionStatus.AccessDenied or ProcessInspectionStatus.Failed)
                    return await PersistPreWriteFailureAsync(entry, issue + " Nenhuma escrita foi iniciada.");
                return await PersistConflictAsync(entry, issue + " Nenhuma chamada de escrita foi feita.");
            }
            if (snapshot.PreviousPriority == snapshot.TargetPriority)
            {
                entry = _journal.MarkNoChangeRequired(entry,
                    "A prioridade já corresponde ao alvo; nenhuma chamada de escrita foi feita.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            // Persist a recovery point before the adapter is allowed to call the Windows setter.
            entry = _journal.ArmMutation(entry, plan);
            await _journalStore.SaveAsync(entry, CancellationToken.None);

            var write = _adapter.SetPriority(snapshot.Identity, snapshot.PreviousPriority, snapshot.TargetPriority);
            if (!write.WriteAttempted)
            {
                if (write.Status is ProcessPriorityWriteStatus.ProcessExited or ProcessPriorityWriteStatus.IdentityMismatch or
                    ProcessPriorityWriteStatus.IdentityUnverifiable or ProcessPriorityWriteStatus.PriorityConflict)
                    return await PersistConflictAsync(entry, write.Detail + " Nenhuma escrita foi iniciada.");

                return await PersistPreWriteFailureAsync(entry, write.Detail + " Nenhuma escrita foi iniciada.");
            }

            if (write.Status != ProcessPriorityWriteStatus.Applied)
            {
                entry = _journal.MarkFailed(entry,
                    write.Detail + " A chamada de escrita foi iniciada, mas o resultado não está verificado; não haverá restauração automática.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            var after = _adapter.Inspect(snapshot.Identity.ProcessId);
            issue = CompareCurrent(snapshot.Identity, snapshot.TargetPriority, after);
            if (issue is not null)
            {
                entry = _journal.MarkFailed(entry,
                    issue + " A escrita foi iniciada e não pôde ser verificada; não haverá restauração automática.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            entry = _journal.MarkMutationVerified(entry,
                "Leitura posterior confirmou a prioridade solicitada para o mesmo PID, instante de criação e caminho capturados.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            return entry;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<OperationJournalEntry> RestoreAsync(
        Guid operationId,
        ProcessPriorityApproval approval,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var entry = await LoadRequiredAsync(operationId, cancellationToken);
            var (_, snapshot) = ValidateCapturedEntry(entry);
            EnsureAuthorized(approval, entry, snapshot, ProcessPriorityAction.Restore);
            if (entry.State is not (JournalOperationState.MutationVerified or JournalOperationState.MutationMayHaveStarted or
                JournalOperationState.RestorationMayHaveStarted or JournalOperationState.Failed or JournalOperationState.Interrupted))
                throw new InvalidOperationException("O estado do diário não permite restauração condicional.");

            var current = _adapter.Inspect(snapshot.Identity.ProcessId);
            if (current.Status is ProcessInspectionStatus.AccessDenied or ProcessInspectionStatus.Failed)
                return await PersistPreWriteFailureAsync(entry,
                    InspectionProblem(current) + " A tentativa de restauração não chamou o setter.");
            var identityIssue = CompareIdentity(snapshot.Identity, current);
            if (identityIssue is not null)
                return await PersistConflictAsync(entry, identityIssue + " A restauração foi recusada; o PID não será reutilizado como alvo.");
            if (!current.CanReviewPriority)
                return await PersistConflictAsync(entry, InspectionProblem(current) + " A restauração foi recusada sem escrita.");

            var currentPriority = current.CurrentPriority!.Value;
            if (currentPriority == snapshot.PreviousPriority)
            {
                entry = _journal.MarkRestorationMayHaveStarted(entry,
                    "A leitura confirmou que o processo já está no valor capturado; nenhuma chamada de escrita será feita.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                entry = _journal.MarkRestoredVerified(entry,
                    "Restauração condicional verificada: mesmo processo, estado anterior já presente e nenhum setter chamado.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            if (currentPriority != snapshot.TargetPriority)
                return await PersistConflictAsync(entry,
                    $"A prioridade atual ({ProcessPriorityTargets.DisplayName(currentPriority)}) difere do valor aplicado e do valor original; mudança externa detectada, nenhuma escrita foi feita.");

            entry = _journal.MarkRestorationMayHaveStarted(entry,
                "Intenção de restauração condicional persistida antes de escrever o valor anterior.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);

            var write = _adapter.SetPriority(snapshot.Identity, snapshot.TargetPriority, snapshot.PreviousPriority);
            if (!write.WriteAttempted)
            {
                if (write.Status is ProcessPriorityWriteStatus.ProcessExited or ProcessPriorityWriteStatus.IdentityMismatch or
                    ProcessPriorityWriteStatus.IdentityUnverifiable or ProcessPriorityWriteStatus.PriorityConflict)
                    return await PersistConflictAsync(entry, write.Detail + " Nenhuma restauração foi iniciada.");

                return await PersistPreWriteFailureAsync(entry, write.Detail + " A restauração não foi iniciada.");
            }

            if (write.Status != ProcessPriorityWriteStatus.Applied)
            {
                entry = _journal.MarkFailed(entry,
                    write.Detail + " A restauração foi iniciada, mas o resultado não está verificado.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            var after = _adapter.Inspect(snapshot.Identity.ProcessId);
            var restoreIssue = CompareCurrent(snapshot.Identity, snapshot.PreviousPriority, after);
            if (restoreIssue is not null)
            {
                entry = _journal.MarkFailed(entry,
                    restoreIssue + " A restauração foi iniciada e exige revisão manual.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            entry = _journal.MarkRestoredVerified(entry,
                "Leitura posterior confirmou o valor anterior para o mesmo processo identificado na captura.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            return entry;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<OperationJournalEntry>> LoadHistoryAsync(CancellationToken cancellationToken = default) =>
        await _journalStore.LoadAllAsync(cancellationToken);

    public static string RequiredConfirmationPhrase(Guid operationId, ProcessPriorityAction action)
    {
        var verb = action == ProcessPriorityAction.Apply ? "APPLY" : "RESTORE";
        return $"{verb} PROCESS PRIORITY {operationId.ToString("N")[..8].ToUpperInvariant()}";
    }

    private async Task<OperationJournalEntry> LoadRequiredAsync(Guid operationId, CancellationToken cancellationToken) =>
        await _journalStore.LoadAsync(operationId, cancellationToken)
        ?? throw new InvalidOperationException("A operação não existe no histórico local.");

    private (AdjustmentPlan Plan, ProcessPrioritySnapshot Snapshot) ValidateCapturedEntry(OperationJournalEntry entry)
    {
        if (entry.ProfileId != ProfileId || entry.DataOrigin != JournalDataOrigin.UserPlan ||
            entry.PlanItems.Count != 1 || entry.PlanItems[0].AdjustmentId != AdjustmentId ||
            entry.Snapshots.Count != 1 || entry.Snapshots[0].AdjustmentId != AdjustmentId ||
            entry.Snapshots[0].Version != 1 || entry.Consent is null)
            throw new InvalidDataException("O diário não contém uma captura individual de prioridade válida.");

        var values = entry.Snapshots[0].Values;
        var planJson = RequiredValue(values, "PlanJson");
        var plan = JsonSerializer.Deserialize<AdjustmentPlan>(planJson, JsonOptions)
            ?? throw new InvalidDataException("O plano salvo não pôde ser lido.");
        if (plan.Id != entry.PlanId || !string.Equals(plan.Fingerprint, entry.PlanFingerprint, StringComparison.Ordinal) ||
            !plan.CanAcceptConsent || plan.Items.Count != 1 || plan.Items[0].AdjustmentId != AdjustmentId)
            throw new InvalidDataException("O plano salvo não corresponde à identidade do diário.");
        if (!_consentService.Validate(plan, entry.Consent).AllConsequencesAccepted)
            throw new InvalidDataException("O consentimento salvo é recusado, inválido ou pertence a outro plano.");

        if (!int.TryParse(RequiredValue(values, "ProcessId"), NumberStyles.None, CultureInfo.InvariantCulture, out var processId) ||
            !DateTimeOffset.TryParse(RequiredValue(values, "CreatedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAtUtc) ||
            !Enum.TryParse(RequiredValue(values, "PreviousPriority"), out ProcessPriorityLevel previous) ||
            !Enum.TryParse(RequiredValue(values, "TargetPriority"), out ProcessPriorityLevel target) ||
            !ProcessPriorityTargets.Allowed.Contains(previous) || !ProcessPriorityTargets.Allowed.Contains(target))
            throw new InvalidDataException("A captura de processo contém identidade ou prioridade inválida.");

        var pathValue = RequiredValue(values, "ExecutablePath");
        var identity = new ProcessIdentity(processId, createdAtUtc.ToUniversalTime(),
            string.IsNullOrEmpty(pathValue) ? null : pathValue);
        var fingerprint = RequiredValue(values, "CaptureFingerprint");
        var snapshot = new ProcessPrioritySnapshot(identity, previous, target, fingerprint);
        var expectedFingerprint = ProcessPriorityCaptureFingerprint.Compute(entry.OperationId, plan.Fingerprint, snapshot with { CaptureFingerprint = string.Empty });
        if (!string.Equals(expectedFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("A impressão digital da captura não é válida.");

        return (plan, snapshot);
    }

    private void EnsureAuthorized(
        ProcessPriorityApproval approval,
        OperationJournalEntry entry,
        ProcessPrioritySnapshot snapshot,
        ProcessPriorityAction expectedAction)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.Action != expectedAction || !_mutationPolicy.IsAuthorized(approval, entry, snapshot))
            throw new UnauthorizedAccessException("A execução real está bloqueada por padrão; consentimento revisado não equivale a autorização de escrita.");
    }

    private async Task<OperationJournalEntry> PersistConflictAsync(OperationJournalEntry entry, string message)
    {
        entry = _journal.MarkConflict(entry, message);
        await _journalStore.SaveAsync(entry, CancellationToken.None);
        return entry;
    }

    private async Task<OperationJournalEntry> PersistPreWriteFailureAsync(OperationJournalEntry entry, string message)
    {
        entry = _journal.RecordPreWriteFailure(entry, message);
        await _journalStore.SaveAsync(entry, CancellationToken.None);
        return entry;
    }

    private static void EnsureReviewable(ProcessObservation observation)
    {
        if (!observation.CanReviewPriority)
            throw new InvalidOperationException(InspectionProblem(observation));
    }

    private static string? CompareCurrent(
        ProcessIdentity expectedIdentity,
        ProcessPriorityLevel expectedPriority,
        ProcessObservation current)
    {
        var identityIssue = CompareIdentity(expectedIdentity, current);
        if (identityIssue is not null) return identityIssue;
        if (!current.CanReviewPriority) return InspectionProblem(current);
        if (current.CurrentPriority != expectedPriority)
            return $"Conflito: prioridade mudou de {ProcessPriorityTargets.DisplayName(expectedPriority)} para {ProcessPriorityTargets.DisplayName(current.CurrentPriority!.Value)}.";
        return null;
    }

    private static string? CompareIdentity(ProcessIdentity expectedIdentity, ProcessObservation current)
    {
        if (current.Status is ProcessInspectionStatus.Exited or ProcessInspectionStatus.NotFound)
            return $"O processo PID {expectedIdentity.ProcessId} terminou ou não está mais disponível.";
        return expectedIdentity.CompareTo(current.Identity) switch
        {
            ProcessIdentityComparison.Match => null,
            ProcessIdentityComparison.DifferentProcess => $"Conflito: o PID {expectedIdentity.ProcessId} agora identifica outra instância; nenhuma escrita deve ser feita.",
            _ => $"A identidade do PID {expectedIdentity.ProcessId} não pôde ser confirmada; nenhuma escrita deve ser feita."
        };
    }

    private static string InspectionProblem(ProcessObservation observation) => observation.Status switch
    {
        ProcessInspectionStatus.AccessDenied => $"Acesso negado ao consultar PID {observation.ProcessId}; a prioridade não foi alterada.",
        ProcessInspectionStatus.Exited => $"O PID {observation.ProcessId} terminou durante a consulta; nenhuma escrita será tentada.",
        ProcessInspectionStatus.NotFound => $"O PID {observation.ProcessId} não existe mais; nenhuma escrita será tentada.",
        _ => $"Não foi possível confirmar a identidade e a prioridade do PID {observation.ProcessId} ({observation.Status})."
    };

    private static string BuildConsequence(int processId, ProcessPriorityLevel current, ProcessPriorityLevel target)
    {
        var targetWarning = target switch
        {
            ProcessPriorityLevel.Idle => " A classe Ociosa só recebe CPU quando processos de prioridade maior deixam tempo disponível; o aplicativo pode responder bem mais devagar sob carga.",
            ProcessPriorityLevel.BelowNormal => " Abaixo do normal pode reduzir a parcela de CPU recebida enquanto há disputa com processos normais.",
            ProcessPriorityLevel.High => " A classe Alta pode consumir quase todo o tempo disponível da CPU e atrasar processos normais.",
            _ => string.Empty
        };
        return $"Afeta somente o processo identificado como PID {processId}: {ProcessPriorityTargets.DisplayName(current)} → {ProcessPriorityTargets.DisplayName(target)}. " +
            "A prioridade altera a preferência de agendamento da CPU; não muda frequência, tensão ou GPU. Prioridade maior não garante FPS e pode atrasar ou reduzir a resposta de outros aplicativos, incluindo processos do sistema." +
            targetWarning + " A mudança só vale enquanto este processo existir.";
    }

    private static string RequiredValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value)
            ? value
            : throw new InvalidDataException($"A captura está sem o campo {key}.");
}
