namespace Optimizer.Core.Adjustments;

/// <summary>
/// Owns the staged VIDEOIDLE workflow. Capture and preview are read-only; all writes require an explicit
/// enabled policy and an operation-specific approval. This type is not wired for execution in the app.
/// </summary>
public sealed class VideoIdleOperationService
{
    private readonly IVideoIdlePowerAdapter _power;
    private readonly JsonVideoIdleOperationStore _store;
    private readonly IVideoIdleMutationPolicy _mutationPolicy;

    public VideoIdleOperationService(
        IVideoIdlePowerAdapter power,
        JsonVideoIdleOperationStore store,
        IVideoIdleMutationPolicy? mutationPolicy = null)
    {
        _power = power ?? throw new ArgumentNullException(nameof(power));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mutationPolicy = mutationPolicy ?? new BlockedVideoIdleMutationPolicy();
    }

    public VideoIdlePreview ReadPreview()
    {
        var current = _power.ReadActiveAcTimeout();
        return new(
            DateTimeOffset.UtcNow,
            current.ActiveSchemeId,
            current.AcTimeoutSeconds,
            VideoIdleSetting.ProposedAcTimeoutSeconds);
    }

    public async Task<VideoIdleOperationRecord> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = _power.ReadActiveAcTimeout();
        var now = DateTimeOffset.UtcNow;
        var operationId = Guid.NewGuid();
        var fingerprint = VideoIdleCaptureFingerprint.Compute(
            operationId,
            current.ActiveSchemeId,
            current.AcTimeoutSeconds,
            VideoIdleSetting.ProposedAcTimeoutSeconds);
        var record = new VideoIdleOperationRecord(
            1,
            operationId,
            now,
            now,
            current.ActiveSchemeId,
            current.AcTimeoutSeconds,
            VideoIdleSetting.ProposedAcTimeoutSeconds,
            fingerprint,
            VideoIdleOperationState.Captured,
            "Captura real somente leitura persistida. Nenhuma configuração foi alterada; isto não é backup do Windows.");
        await _store.SaveAsync(record, cancellationToken);
        return record;
    }

    public async Task<IReadOnlyList<VideoIdleOperationRecord>> LoadHistoryAsync(
        CancellationToken cancellationToken = default) => await _store.LoadAllAsync(cancellationToken);

    public async Task<VideoIdleOperationRecord> ApplyAsync(
        Guid operationId,
        VideoIdleApproval approval,
        CancellationToken cancellationToken = default)
    {
        var record = await LoadRequiredAsync(operationId, cancellationToken);
        EnsureAuthorized(approval, record, VideoIdleAction.Apply);
        if (record.State != VideoIdleOperationState.Captured)
            throw new InvalidOperationException("Only a newly captured VIDEOIDLE operation can be applied.");

        var current = _power.ReadActiveAcTimeout();
        if (current.ActiveSchemeId != record.ActiveSchemeId ||
            current.AcTimeoutSeconds != record.PreviousAcTimeoutSeconds)
        {
            return await PersistStateAsync(record, VideoIdleOperationState.Conflict,
                "Plano ativo ou valor AC divergiu da captura; nenhuma escrita foi tentada.");
        }

        if (record.PreviousAcTimeoutSeconds == record.ProposedAcTimeoutSeconds)
        {
            return await PersistStateAsync(record, VideoIdleOperationState.NoChangeRequired,
                "O valor capturado já é o valor proposto; nenhuma escrita foi necessária.");
        }

        // Persist the recovery boundary before the first real write. Cancellation is no longer honored after this point.
        record = await PersistStateAsync(record, VideoIdleOperationState.MutationMayHaveStarted,
            "Intenção persistida antes da primeira escrita. Resultado ainda não verificado.", CancellationToken.None);
        try
        {
            var beforeWrite = _power.ReadActiveAcTimeout();
            if (beforeWrite.ActiveSchemeId != record.ActiveSchemeId ||
                beforeWrite.AcTimeoutSeconds != record.PreviousAcTimeoutSeconds)
            {
                return await PersistStateAsync(record, VideoIdleOperationState.Conflict,
                    "Plano ativo ou valor AC mudou depois da gravação da intenção; escrita recusada.", CancellationToken.None);
            }

            _power.WriteAcTimeout(record.ActiveSchemeId, record.ProposedAcTimeoutSeconds);
            var beforeActivation = _power.ReadActiveAcTimeout();
            if (beforeActivation.ActiveSchemeId != record.ActiveSchemeId ||
                beforeActivation.AcTimeoutSeconds != record.ProposedAcTimeoutSeconds)
            {
                return await PersistStateAsync(record, VideoIdleOperationState.MutationUnverified,
                    "O plano ativo ou valor AC mudou depois da escrita; o plano capturado não foi reativado.", CancellationToken.None);
            }

            _power.ActivateScheme(record.ActiveSchemeId);
            var verified = _power.ReadActiveAcTimeout();
            if (verified.ActiveSchemeId != record.ActiveSchemeId ||
                verified.AcTimeoutSeconds != record.ProposedAcTimeoutSeconds)
            {
                return await PersistStateAsync(record, VideoIdleOperationState.MutationUnverified,
                    "A leitura posterior divergiu do valor esperado; requer revisão manual.", CancellationToken.None);
            }

            return await PersistStateAsync(record, VideoIdleOperationState.Verified,
                "Leitura posterior confirmou o plano capturado e o valor AC proposto.", CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return await PersistStateAsync(record, VideoIdleOperationState.MutationUnverified,
                $"A operação falhou após armar a mutação ({exception.GetType().Name}); leia o estado atual e revise manualmente.",
                CancellationToken.None);
        }
    }

    public async Task<VideoIdleOperationRecord> RevertAsync(
        Guid operationId,
        VideoIdleApproval approval,
        CancellationToken cancellationToken = default)
    {
        var record = await LoadRequiredAsync(operationId, cancellationToken);
        EnsureAuthorized(approval, record, VideoIdleAction.Revert);
        if (record.State == VideoIdleOperationState.NoChangeRequired)
            return record;
        if (record.State is not (VideoIdleOperationState.Verified or
            VideoIdleOperationState.MutationMayHaveStarted or
            VideoIdleOperationState.MutationUnverified or
            VideoIdleOperationState.ReversionUnverified))
        {
            throw new InvalidOperationException("The VIDEOIDLE operation state does not permit conditional reversal.");
        }

        var current = _power.ReadActiveAcTimeout();
        if (current.ActiveSchemeId != record.ActiveSchemeId ||
            current.AcTimeoutSeconds != record.ProposedAcTimeoutSeconds)
        {
            return await PersistStateAsync(record, VideoIdleOperationState.Conflict,
                "Plano ativo ou valor AC mudou desde a operação; reversão recusada sem escrita.");
        }

        record = await PersistStateAsync(record, VideoIdleOperationState.ReversionMayHaveStarted,
            "Intenção de reversão persistida antes da primeira escrita. Resultado ainda não verificado.",
            CancellationToken.None);
        try
        {
            var beforeWrite = _power.ReadActiveAcTimeout();
            if (beforeWrite.ActiveSchemeId != record.ActiveSchemeId ||
                beforeWrite.AcTimeoutSeconds != record.ProposedAcTimeoutSeconds)
            {
                return await PersistStateAsync(record, VideoIdleOperationState.Conflict,
                    "Plano ativo ou valor AC mudou depois da gravação da intenção; reversão recusada.", CancellationToken.None);
            }

            _power.WriteAcTimeout(record.ActiveSchemeId, record.PreviousAcTimeoutSeconds);
            var beforeActivation = _power.ReadActiveAcTimeout();
            if (beforeActivation.ActiveSchemeId != record.ActiveSchemeId ||
                beforeActivation.AcTimeoutSeconds != record.PreviousAcTimeoutSeconds)
            {
                return await PersistStateAsync(record, VideoIdleOperationState.ReversionUnverified,
                    "O plano ativo ou valor AC mudou depois da escrita; o plano capturado não foi reativado.", CancellationToken.None);
            }

            _power.ActivateScheme(record.ActiveSchemeId);
            var verified = _power.ReadActiveAcTimeout();
            if (verified.ActiveSchemeId != record.ActiveSchemeId ||
                verified.AcTimeoutSeconds != record.PreviousAcTimeoutSeconds)
            {
                return await PersistStateAsync(record, VideoIdleOperationState.ReversionUnverified,
                    "A leitura posterior não confirmou o valor anterior; requer revisão manual.", CancellationToken.None);
            }

            return await PersistStateAsync(record, VideoIdleOperationState.Reverted,
                "Leitura posterior confirmou a restauração do índice AC capturado.", CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return await PersistStateAsync(record, VideoIdleOperationState.ReversionUnverified,
                $"A reversão falhou após armar a mutação ({exception.GetType().Name}); requer leitura e revisão manual.",
                CancellationToken.None);
        }
    }

    private async Task<VideoIdleOperationRecord> LoadRequiredAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var record = await _store.LoadAsync(operationId, cancellationToken);
        return record ?? throw new FileNotFoundException("Persisted VIDEOIDLE capture was not found.");
    }

    private void EnsureAuthorized(
        VideoIdleApproval approval,
        VideoIdleOperationRecord record,
        VideoIdleAction expectedAction)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.Action != expectedAction || !_mutationPolicy.IsAuthorized(approval, record))
            throw new UnauthorizedAccessException("Real VIDEOIDLE execution is blocked or lacks specific approval.");
    }

    private async Task<VideoIdleOperationRecord> PersistStateAsync(
        VideoIdleOperationRecord record,
        VideoIdleOperationState state,
        string detail,
        CancellationToken cancellationToken = default)
    {
        var updated = record with { State = state, UpdatedAtUtc = DateTimeOffset.UtcNow, Detail = detail };
        await _store.SaveAsync(updated, cancellationToken);
        return updated;
    }
}
