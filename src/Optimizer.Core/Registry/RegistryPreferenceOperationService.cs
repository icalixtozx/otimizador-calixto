using System.Security.Cryptography;
using System.Text.Json;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Consent;
using Optimizer.Core.Recovery;

namespace Optimizer.Core.Registry;

/// <summary>
/// Reviews, captures, and conditionally operates on the single documented File Explorer preference.
/// It reuses the application's plan, consequence-consent, and journal services. The default mutation
/// policy denies writes; even an enabled policy requires the management inspector to say Unmanaged.
/// </summary>
public sealed class RegistryPreferenceOperationService
{
    private const string StateKey = "RegistryStateJson";
    private const string PlanKey = "PlanJson";
    private const string TargetKey = "TargetJson";
    private const string FingerprintKey = "CaptureFingerprint";
    private readonly IRegistryPreferenceAdapter _adapter;
    private readonly IRegistryManagementInspector _management;
    private readonly JsonOperationJournalStore _journalStore;
    private readonly PlanConsentService _consentService;
    private readonly OperationJournalCoordinator _journal;
    private readonly IRegistryMutationPolicy _mutationPolicy;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public RegistryPreferenceOperationService(
        IRegistryPreferenceAdapter adapter,
        IRegistryManagementInspector management,
        JsonOperationJournalStore journalStore,
        PlanConsentService consentService,
        OperationJournalCoordinator journal,
        IRegistryMutationPolicy? mutationPolicy = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        _consentService = consentService ?? throw new ArgumentNullException(nameof(consentService));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _mutationPolicy = mutationPolicy ?? new BlockedRegistryMutationPolicy();
    }

    public RegistryPreferenceReview CreateReview(
        PlatformDescriptor platform,
        FileExtensionVisibility targetVisibility = FileExtensionVisibility.Show)
    {
        ArgumentNullException.ThrowIfNull(platform);
        var targetValue = RegistryPreferenceCatalog.TargetValue(targetVisibility);
        var catalog = RegistryPreferenceCatalog.CreateAdjustmentCatalog(targetVisibility);
        var compatibility = new AdjustmentCompatibilityEvaluator();
        var planService = new AdjustmentPlanService(catalog, compatibility);
        var plan = planService.CreatePlan([RegistryPreferenceCatalog.AdjustmentId], platform);
        if (plan.Items.Count != 1 || plan.Items[0].Status != PlanItemStatus.ReadyForExplicitApproval)
            throw new PlatformNotSupportedException(plan.Items.FirstOrDefault()?.Reason ??
                "A preferência de Registro não está disponível para revisão nesta plataforma.");

        var state = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
        EnsureValidState(state);
        var management = _management.Assess(RegistryPreferenceCatalog.FileExtensionsAddress);
        return new(
            plan,
            targetVisibility,
            targetValue,
            state,
            management,
            DateTimeOffset.UtcNow,
            targetVisibility == FileExtensionVisibility.Show
                ? "Prévia baseada no estado real do Registro. A proposta exibe extensões; preferência de interface, sem ganho de desempenho alegado."
                : "Prévia baseada no estado real do Registro. A proposta oculta extensões e pode tornar nomes ambíguos; preferência de interface, sem ganho de desempenho alegado.");
    }

    /// <summary>Records explicit consequence consent and an exact, typed snapshot. This never writes the Registry.</summary>
    public async Task<OperationJournalEntry> CaptureAsync(
        RegistryPreferenceReview review,
        bool consequenceAccepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        EnsureReview(review);
        if (!consequenceAccepted)
            throw new InvalidOperationException("A consequência foi recusada; consentimento e captura não foram registrados.");

        var entry = _journal.CreatePlanned(review.Plan, RegistryPreferenceCatalog.ProfileId);
        await _journalStore.SaveAsync(entry, cancellationToken);
        var receipt = _consentService.Record(review.Plan,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                [RegistryPreferenceCatalog.AdjustmentId] = true
            });
        entry = _journal.RecordConsent(entry, review.Plan, receipt);
        await _journalStore.SaveAsync(entry, cancellationToken);

        RegistryValueState current;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
            EnsureValidState(current);
        }
        catch (Exception exception) when (IsOperationalFailure(exception))
        {
            entry = _journal.RecordPreWriteFailure(entry,
                $"A leitura/captura falhou ({exception.GetType().Name}); nenhuma escrita foi chamada.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            return entry;
        }

        if (!StatesEqual(current, review.CurrentState))
        {
            entry = _journal.MarkConflict(entry,
                "Chave, presença, tipo ou dado diferiu da prévia que foi revisada; a captura foi recusada. Atualize a prévia e revise novamente.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            return entry;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [StateKey] = JsonSerializer.Serialize(current),
            [PlanKey] = JsonSerializer.Serialize(review.Plan),
            [TargetKey] = JsonSerializer.Serialize(review.TargetValue),
            [FingerprintKey] = ComputeCaptureFingerprint(entry.OperationId, review.Plan.Fingerprint, current, review.TargetValue)
        };
        entry = _journal.RecordSnapshot(entry, new AdjustmentSnapshot(
            RegistryPreferenceCatalog.AdjustmentId,
            RegistryPreferenceCatalog.AdjustmentVersion,
            DateTimeOffset.UtcNow,
            values));
        await _journalStore.SaveAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<OperationJournalEntry> ApplyAsync(
        Guid operationId,
        RegistryPreferenceApproval approval,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var entry = await LoadRequiredAsync(operationId, cancellationToken);
            var (plan, previous, target, fingerprint) = ValidateCapturedEntry(entry);
            EnsureAuthorized(entry, approval, fingerprint, RegistryPreferenceAction.Apply);
            if (entry.State != JournalOperationState.SnapshotsCaptured)
                throw new InvalidOperationException("Somente uma captura consentida pode iniciar uma operação de Registro.");

            var management = _management.Assess(RegistryPreferenceCatalog.FileExtensionsAddress);
            if (management.Status != RegistryManagementStatus.Unmanaged)
                return await PersistConflictAsync(entry,
                    "O gerenciamento organizacional é administrado ou desconhecido; a escrita foi recusada.", cancellationToken);

            RegistryValueState current;
            try
            {
                current = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
                EnsureValidState(current);
            }
            catch (Exception exception) when (IsOperationalFailure(exception))
            {
                return await PersistPreWriteFailureAsync(entry,
                    $"A leitura imediatamente anterior falhou ({exception.GetType().Name}); nenhum setter foi chamado.", cancellationToken);
            }

            if (!StatesEqual(current, previous))
                return await PersistConflictAsync(entry,
                    "A chave, presença, tipo ou dado do valor mudou desde a captura; nenhuma escrita foi chamada.", cancellationToken);
            if (!current.KeyExists)
                return await PersistPreWriteFailureAsync(entry,
                    "A chave pai não existe. O protótipo não cria chaves do Windows e não chamou setter.", cancellationToken);
            if (IsAlreadyAtTarget(current, target))
            {
                entry = _journal.MarkNoChangeRequired(entry,
                    "A preferência já corresponde ao alvo revisado; nenhum valor foi escrito.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            // Persist the shared journal's recovery state before the only permitted registry write.
            entry = _journal.ArmMutation(entry, plan);
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            var setterCalled = false;
            try
            {
                var finalPreWrite = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
                EnsureValidState(finalPreWrite);
                if (!StatesEqual(finalPreWrite, previous))
                    return await PersistConflictAsync(entry,
                        "O valor mudou enquanto a intenção de operação era persistida; nenhum setter foi chamado.", cancellationToken);

                setterCalled = true;
                _adapter.WriteValue(RegistryPreferenceCatalog.FileExtensionsAddress,
                    target);
                var after = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
                EnsureValidState(after);
                if (!MatchesAppliedState(after, target))
                    return await PersistFailedAsync(entry,
                        $"A leitura posterior não confirmou {DescribeTarget(target)}; operação exige revisão manual.", interrupted: false);

                entry = _journal.MarkMutationVerified(entry,
                    $"Leitura posterior confirmou {DescribeTarget(target)} no mesmo valor nomeado. A exibição visual no Explorador não foi verificada.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }
            catch (OperationCanceledException)
            {
                if (!setterCalled)
                    return await PersistPreWriteFailureAsync(entry,
                        "A operação foi interrompida antes do setter; nenhuma escrita foi chamada.", cancellationToken);
                return await PersistFailedAsync(entry,
                    "A operação foi interrompida após a intenção ser registrada; releia o estado antes de qualquer recuperação.", interrupted: true);
            }
            catch (Exception exception) when (IsOperationalFailure(exception))
            {
                if (!setterCalled)
                    return await PersistPreWriteFailureAsync(entry,
                        $"A releitura imediatamente antes da escrita falhou ({exception.GetType().Name}); nenhum setter foi chamado.", cancellationToken);
                return await PersistFailedAsync(entry,
                    $"A escrita ou verificação falhou após armar o diário ({exception.GetType().Name}); releia o Registro antes de recuperar.", interrupted: false);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<OperationJournalEntry> RestoreAsync(
        Guid operationId,
        RegistryPreferenceApproval approval,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var entry = await LoadRequiredAsync(operationId, cancellationToken);
            var (_, previous, target, fingerprint) = ValidateCapturedEntry(entry);
            EnsureAuthorized(entry, approval, fingerprint, RegistryPreferenceAction.Restore);
            if (entry.State == JournalOperationState.NoChangeRequired)
                return entry;
            if (entry.State is not (JournalOperationState.MutationVerified or JournalOperationState.MutationMayHaveStarted or
                JournalOperationState.Failed or JournalOperationState.Interrupted or JournalOperationState.RestorationMayHaveStarted))
                throw new InvalidOperationException("O estado do diário não permite restauração condicional.");

            var management = _management.Assess(RegistryPreferenceCatalog.FileExtensionsAddress);
            if (management.Status != RegistryManagementStatus.Unmanaged)
                return await PersistConflictAsync(entry,
                    "O gerenciamento organizacional é administrado ou desconhecido; a restauração foi recusada.", cancellationToken);

            RegistryValueState current;
            try
            {
                current = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
                EnsureValidState(current);
            }
            catch (Exception exception) when (IsOperationalFailure(exception))
            {
                return await PersistPreWriteFailureAsync(entry,
                    $"A leitura antes da restauração falhou ({exception.GetType().Name}); nenhum setter foi chamado.", cancellationToken);
            }

            if (StatesEqual(current, previous))
            {
                entry = _journal.MarkRestorationMayHaveStarted(entry,
                    "A captura original já está presente; nenhuma escrita ou exclusão foi necessária.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                RegistryValueState finalRead;
                try
                {
                    finalRead = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
                    EnsureValidState(finalRead);
                }
                catch (Exception exception) when (IsOperationalFailure(exception))
                {
                    return await PersistPreWriteFailureAsync(entry,
                        $"A releitura da captura original falhou ({exception.GetType().Name}); nenhum setter foi chamado.", cancellationToken);
                }
                if (!StatesEqual(finalRead, previous))
                    return await PersistConflictAsync(entry,
                        "O estado mudou enquanto a confirmação era persistida; nenhum setter foi chamado.", cancellationToken);

                entry = _journal.MarkRestoredVerified(entry,
                    "A leitura confirmou a captura original sem alterar a chave nem seus valores.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }

            if (!MatchesAppliedState(current, target))
                return await PersistConflictAsync(entry,
                    "O valor atual não é a captura nem o alvo aplicado; mudança externa detectada e nenhuma restauração foi feita.", cancellationToken);
            if (!previous.KeyExists)
                return await PersistConflictAsync(entry,
                    "A chave não existia na captura. A escrita teria sido recusada, portanto não é seguro excluir qualquer chave.", cancellationToken);

            entry = _journal.MarkRestorationMayHaveStarted(entry,
                "Intenção de restauração persistida antes de restaurar somente o valor capturado ou remover o valor criado.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            var setterCalled = false;
            try
            {
                var finalPreRestore = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
                EnsureValidState(finalPreRestore);
                if (StatesEqual(finalPreRestore, previous))
                {
                    entry = _journal.MarkRestoredVerified(entry,
                        "A captura original reapareceu enquanto a intenção era persistida; nenhum setter foi chamado.");
                    await _journalStore.SaveAsync(entry, CancellationToken.None);
                    return entry;
                }
                if (!MatchesAppliedState(finalPreRestore, target))
                    return await PersistConflictAsync(entry,
                        "O valor mudou enquanto a intenção de restauração era persistida; nenhum setter foi chamado.", cancellationToken);

                setterCalled = true;
                if (previous.ValueExists)
                    _adapter.WriteValue(RegistryPreferenceCatalog.FileExtensionsAddress, previous.Value!);
                else
                    _adapter.DeleteValue(RegistryPreferenceCatalog.FileExtensionsAddress);

                var after = _adapter.Read(RegistryPreferenceCatalog.FileExtensionsAddress);
                EnsureValidState(after);
                if (!StatesEqual(after, previous))
                    return await PersistFailedAsync(entry,
                        "A leitura posterior não confirmou a presença, tipo, dado e estado da chave capturados.", interrupted: false);

                entry = _journal.MarkRestoredVerified(entry,
                    previous.ValueExists
                        ? "O tipo e dado anteriores foram restaurados e lidos novamente."
                        : "Somente o valor criado pela operação foi removido; a chave pai e os demais valores foram preservados.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return entry;
            }
            catch (OperationCanceledException)
            {
                if (!setterCalled)
                    return await PersistPreWriteFailureAsync(entry,
                        "A restauração foi interrompida antes do setter; nenhuma escrita ou exclusão foi chamada.", cancellationToken);
                return await PersistFailedAsync(entry,
                    "A restauração foi interrompida após a intenção ser registrada; releia o estado antes de nova ação.", interrupted: true);
            }
            catch (Exception exception) when (IsOperationalFailure(exception))
            {
                if (!setterCalled)
                    return await PersistPreWriteFailureAsync(entry,
                        $"A releitura imediatamente anterior à restauração falhou ({exception.GetType().Name}); nenhum setter foi chamado.", cancellationToken);
                return await PersistFailedAsync(entry,
                    $"A restauração ou verificação falhou após armar o diário ({exception.GetType().Name}); revisão manual necessária.", interrupted: false);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task<IReadOnlyList<OperationJournalEntry>> LoadHistoryAsync(CancellationToken cancellationToken = default) =>
        _journalStore.LoadAllAsync(cancellationToken);

    public static string RequiredConfirmationPhrase(Guid operationId, RegistryPreferenceAction action) =>
        RegistryPreferenceApproval.RequiredPhrase(operationId, action);

    private async Task<OperationJournalEntry> LoadRequiredAsync(Guid operationId, CancellationToken cancellationToken) =>
        await _journalStore.LoadAsync(operationId, cancellationToken) ??
        throw new KeyNotFoundException("A operação de Registro não foi encontrada no diário compartilhado.");

    private (AdjustmentPlan Plan, RegistryValueState Previous, RegistryTypedValue Target, string Fingerprint) ValidateCapturedEntry(OperationJournalEntry entry)
    {
        if (entry.DataOrigin != JournalDataOrigin.UserPlan || entry.ProfileId != RegistryPreferenceCatalog.ProfileId ||
            !JournalSnapshotValidator.HasCompleteSnapshots(entry))
            throw new InvalidDataException("A operação não contém uma captura real completa do ajuste esperado.");

        var snapshot = entry.Snapshots.Single(item => item.AdjustmentId == RegistryPreferenceCatalog.AdjustmentId);
        if (snapshot.Version != RegistryPreferenceCatalog.AdjustmentVersion ||
            !snapshot.Values.TryGetValue(StateKey, out var stateJson) ||
            !snapshot.Values.TryGetValue(PlanKey, out var planJson) ||
            !snapshot.Values.TryGetValue(TargetKey, out var targetJson) ||
            !snapshot.Values.TryGetValue(FingerprintKey, out var fingerprint))
            throw new InvalidDataException("A captura não contém os campos requeridos.");

        var previous = JsonSerializer.Deserialize<RegistryValueState>(stateJson) ??
            throw new InvalidDataException("O estado capturado está vazio.");
        EnsureValidState(previous);
        var target = JsonSerializer.Deserialize<RegistryTypedValue>(targetJson) ??
            throw new InvalidDataException("O alvo persistido está vazio.");
        var plan = JsonSerializer.Deserialize<AdjustmentPlan>(planJson) ??
            throw new InvalidDataException("O plano persistido está vazio.");
        if (plan.Id != entry.PlanId || !string.Equals(plan.Fingerprint, entry.PlanFingerprint, StringComparison.Ordinal) ||
            plan.Items.Count != 1 || plan.Items[0].AdjustmentId != RegistryPreferenceCatalog.AdjustmentId ||
            !IsAllowedTarget(target) ||
            !string.Equals(fingerprint, ComputeCaptureFingerprint(entry.OperationId, plan.Fingerprint, previous, target), StringComparison.Ordinal))
            throw new InvalidDataException("A captura, o plano ou a impressão digital divergem do diário.");

        if (entry.Consent is null || !_consentService.Validate(plan, entry.Consent).AllConsequencesAccepted)
            throw new InvalidDataException("O consentimento persistido não corresponde às consequências atuais.");
        return (plan, previous, target, fingerprint);
    }

    private void EnsureAuthorized(
        OperationJournalEntry entry,
        RegistryPreferenceApproval approval,
        string fingerprint,
        RegistryPreferenceAction expectedAction)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.OperationId != entry.OperationId || approval.Action != expectedAction ||
            !approval.ConsequenceAccepted || !approval.OrganizationManagementReviewed ||
            !string.Equals(approval.CaptureFingerprint, fingerprint, StringComparison.Ordinal) ||
            !string.Equals(approval.ConfirmationPhrase, RequiredConfirmationPhrase(entry.OperationId, expectedAction), StringComparison.Ordinal) ||
            !_mutationPolicy.IsAuthorized(approval, fingerprint))
        {
            throw new UnauthorizedAccessException("A política padrão bloqueia escrita. A operação exige aprovação específica separada do consentimento.");
        }
    }

    private void EnsureReview(RegistryPreferenceReview review)
    {
        if (review.Plan.Items.Count != 1 || review.Plan.Items[0].AdjustmentId != RegistryPreferenceCatalog.AdjustmentId ||
            review.Plan.Items[0].Version != RegistryPreferenceCatalog.AdjustmentVersion ||
            review.Plan.Items[0].Status != PlanItemStatus.ReadyForExplicitApproval ||
            !review.CurrentState.IsValid || !IsAllowedTarget(review.TargetValue) ||
            review.TargetValue != RegistryPreferenceCatalog.TargetValue(review.TargetVisibility))
            throw new InvalidOperationException("A revisão não corresponde ao ajuste de Registro suportado.");
    }

    private async Task<OperationJournalEntry> PersistConflictAsync(
        OperationJournalEntry entry,
        string message,
        CancellationToken cancellationToken)
    {
        entry = _journal.MarkConflict(entry, message);
        await _journalStore.SaveAsync(entry, CancellationToken.None);
        return entry;
    }

    private async Task<OperationJournalEntry> PersistPreWriteFailureAsync(
        OperationJournalEntry entry,
        string message,
        CancellationToken cancellationToken)
    {
        entry = _journal.RecordPreWriteFailure(entry, message);
        await _journalStore.SaveAsync(entry, CancellationToken.None);
        return entry;
    }

    private async Task<OperationJournalEntry> PersistFailedAsync(
        OperationJournalEntry entry,
        string message,
        bool interrupted)
    {
        entry = interrupted ? _journal.MarkInterrupted(entry, message) : _journal.MarkFailed(entry, message);
        await _journalStore.SaveAsync(entry, CancellationToken.None);
        return entry;
    }

    private static bool IsAlreadyAtTarget(RegistryValueState state, RegistryTypedValue target) =>
        state.KeyExists && ((!state.ValueExists && target == RegistryPreferenceCatalog.ShowFileExtensionsValue) ||
            (state.ValueExists && state.Value == target));

    private static bool MatchesAppliedState(RegistryValueState state, RegistryTypedValue target) =>
        state.KeyExists && state.ValueExists && state.Value == target;

    private static bool IsAllowedTarget(RegistryTypedValue value) =>
        value == RegistryPreferenceCatalog.ShowFileExtensionsValue || value == RegistryPreferenceCatalog.HideFileExtensionsValue;

    private static string DescribeTarget(RegistryTypedValue target) =>
        target == RegistryPreferenceCatalog.ShowFileExtensionsValue
            ? "REG_DWORD 0 (exibir extensões)"
            : "REG_DWORD 1 (ocultar extensões)";

    private static bool StatesEqual(RegistryValueState left, RegistryValueState right) =>
        left.KeyExists == right.KeyExists && left.ValueExists == right.ValueExists && left.Value == right.Value;

    private static void EnsureValidState(RegistryValueState state)
    {
        if (state is null || !state.IsValid)
            throw new InvalidDataException("O adaptador retornou um estado do Registro inconsistente.");
    }

    private static string ComputeCaptureFingerprint(
        Guid operationId,
        string planFingerprint,
        RegistryValueState state,
        RegistryTypedValue target)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            OperationId = operationId,
            PlanFingerprint = planFingerprint,
            State = state,
            Target = target
        });
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    private static bool IsOperationalFailure(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not InvalidOperationException and not ArgumentException;
}
