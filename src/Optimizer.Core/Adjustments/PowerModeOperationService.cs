namespace Optimizer.Core.Adjustments;

/// <summary>
/// Owns the persisted AC/DC user-configured power-mode workflow. Real changes require both
/// operation-specific consequences and confirmation, plus a separately enabled mutation policy.
/// </summary>
public sealed class PowerModeOperationService
{
    private readonly IPowerModeAdapter _power;
    private readonly JsonPowerModeOperationStore _store;
    private readonly IPowerModeMutationPolicy _mutationPolicy;

    public PowerModeOperationService(
        IPowerModeAdapter power,
        JsonPowerModeOperationStore store,
        IPowerModeMutationPolicy? mutationPolicy = null)
    {
        _power = power ?? throw new ArgumentNullException(nameof(power));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mutationPolicy = mutationPolicy ?? new BlockedPowerModeMutationPolicy();
    }

    public PowerModeReadResult ReadPreview() => _power.ReadConfiguredModes();

    public async Task<PowerModeOperationRecord> CaptureAsync(
        UserConfiguredPowerMode? targetAcMode,
        UserConfiguredPowerMode? targetDcMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (targetAcMode is null && targetDcMode is null)
            throw new ArgumentException("Choose a target mode for AC, DC, or both.");

        var current = _power.ReadConfiguredModes();
        if (!current.ApiAvailable)
            throw new PlatformNotSupportedException(current.CompatibilityReason);

        UserConfiguredPowerMode? previousAc = targetAcMode is null ? null : ReadKnownMode(current.AcModeId, current.AcErrorCode, "AC");
        UserConfiguredPowerMode? previousDc = targetDcMode is null ? null : ReadKnownMode(current.DcModeId, current.DcErrorCode, "DC");
        var now = DateTimeOffset.UtcNow;
        var record = new PowerModeOperationRecord(
            1,
            Guid.NewGuid(),
            now,
            now,
            previousAc,
            previousDc,
            targetAcMode,
            targetDcMode,
            string.Empty,
            PowerModeOperationState.Captured,
            "Captura real persistida dos modos configurados selecionados. Nenhuma configuração foi alterada; não é backup do Windows.");
        record = record with { CaptureFingerprint = PowerModeCaptureFingerprint.Compute(record) };
        await _store.SaveAsync(record, cancellationToken);
        return record;
    }

    public async Task<IReadOnlyList<PowerModeOperationRecord>> LoadHistoryAsync(CancellationToken cancellationToken = default) =>
        await _store.LoadAllAsync(cancellationToken);

    public async Task<PowerModeOperationRecord> ApplyAsync(
        Guid operationId,
        PowerModeApproval approval,
        CancellationToken cancellationToken = default)
    {
        var record = await LoadRequiredAsync(operationId, cancellationToken);
        EnsureAuthorized(approval, record, PowerModeAction.Apply);
        if (record.State != PowerModeOperationState.Captured)
            throw new InvalidOperationException("Only a captured power-mode operation may be applied.");

        var initial = _power.ReadConfiguredModes();
        if (!MatchesTargets(initial, record, useTargets: false))
            return await PersistStateAsync(record, PowerModeOperationState.Conflict,
                "A configured AC/DC mode changed since capture; no write was attempted.");
        if (AllSelectedModesEqualTargets(record))
            return await PersistStateAsync(record, PowerModeOperationState.NoChangeRequired,
                "The selected modes already match the requested values; no write was needed.");

        record = await PersistStateAsync(record, PowerModeOperationState.MutationMayHaveStarted,
            "Application intent was persisted before the first write. Result is not verified.", CancellationToken.None);
        var anyWrite = false;
        var acWasWritten = false;
        var dcWasWritten = false;
        try
        {
            foreach (var side in SelectedSides(record))
            {
                var before = _power.ReadConfiguredModes();
                if (!MatchesExpectedForApply(before, record, acWasWritten, dcWasWritten))
                    return await PersistStateAsync(record,
                        anyWrite ? PowerModeOperationState.MutationUnverified : PowerModeOperationState.Conflict,
                        "The configured AC/DC state changed before this write; no further write was attempted.", CancellationToken.None);

                var previous = GetPrevious(record, side);
                var target = GetTarget(record, side);
                if (previous == target) continue;

                if (side == PowerModeSide.Ac) _power.SetConfiguredAcMode(target);
                else _power.SetConfiguredDcMode(target);
                anyWrite = true;
                if (side == PowerModeSide.Ac) acWasWritten = true;
                else dcWasWritten = true;

                var after = _power.ReadConfiguredModes();
                if (!MatchesExpectedForApply(after, record, acWasWritten, dcWasWritten))
                    return await PersistStateAsync(record, PowerModeOperationState.MutationUnverified,
                        "A read after setting the configured mode did not match the expected AC/DC state; manual review is required.", CancellationToken.None);

                record = await PersistStateAsync(record, PowerModeOperationState.MutationMayHaveStarted,
                    $"{SideLabel(side)} configured preference verified; remaining selected side(s) are pending.", CancellationToken.None);
            }

            var verified = _power.ReadConfiguredModes();
            if (!MatchesTargets(verified, record, useTargets: true))
                return await PersistStateAsync(record, PowerModeOperationState.MutationUnverified,
                    "The final configured-mode readback differs from the requested values; manual review is required.", CancellationToken.None);

            return await PersistStateAsync(record, PowerModeOperationState.Verified,
                "A later read confirmed the requested user-configured AC/DC preference(s). This does not prove the effective runtime mode or a performance gain.", CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return await PersistStateAsync(record, PowerModeOperationState.MutationUnverified,
                $"The operation failed after it was armed ({exception.GetType().Name}); re-read current modes and review manually.",
                CancellationToken.None);
        }
    }

    public async Task<PowerModeOperationRecord> RevertAsync(
        Guid operationId,
        PowerModeApproval approval,
        CancellationToken cancellationToken = default)
    {
        var record = await LoadRequiredAsync(operationId, cancellationToken);
        EnsureAuthorized(approval, record, PowerModeAction.Revert);
        if (record.State == PowerModeOperationState.NoChangeRequired) return record;
        if (record.State is not (PowerModeOperationState.Verified or PowerModeOperationState.MutationMayHaveStarted or
            PowerModeOperationState.MutationUnverified or PowerModeOperationState.ReversionUnverified))
            throw new InvalidOperationException("The power-mode operation state does not permit conditional restoration.");

        var current = _power.ReadConfiguredModes();
        if (!IsRestorableState(current, record))
            return await PersistStateAsync(record, PowerModeOperationState.Conflict,
                "A selected AC/DC preference no longer equals either the captured value or the requested value; restoration was refused without a write.");
        if (AllSelectedModesArePrevious(current, record))
            return await PersistStateAsync(record, PowerModeOperationState.Reverted,
                "The selected preferences already match their captured values; no restoration write was needed.");

        record = await PersistStateAsync(record, PowerModeOperationState.ReversionMayHaveStarted,
            "Restoration intent was persisted before the first write. Result is not verified.", CancellationToken.None);
        try
        {
            foreach (var side in SelectedSides(record))
            {
                var before = _power.ReadConfiguredModes();
                if (!IsRestorableState(before, record))
                    return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                        "The configured AC/DC state changed during restoration; no further write was attempted.", CancellationToken.None);

                var previous = GetPrevious(record, side);
                var target = GetTarget(record, side);
                var currentMode = ReadKnownMode(GetModeId(before, side), GetErrorCode(before, side), SideLabel(side));
                if (currentMode == previous) continue;
                if (currentMode != target)
                    return await PersistStateAsync(record, PowerModeOperationState.Conflict,
                        $"{SideLabel(side)} no longer equals the requested preference; restoration was refused.", CancellationToken.None);

                if (side == PowerModeSide.Ac) _power.SetConfiguredAcMode(previous);
                else _power.SetConfiguredDcMode(previous);

                var after = _power.ReadConfiguredModes();
                if (!IsRestorableState(after, record) ||
                    ReadKnownMode(GetModeId(after, side), GetErrorCode(after, side), SideLabel(side)) != previous)
                    return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                        $"A read after restoring {SideLabel(side)} did not confirm the captured value; manual review is required.", CancellationToken.None);

                record = await PersistStateAsync(record, PowerModeOperationState.ReversionMayHaveStarted,
                    $"{SideLabel(side)} captured preference restored and read back; remaining selected side(s) are pending.", CancellationToken.None);
            }

            var verified = _power.ReadConfiguredModes();
            if (!AllSelectedModesArePrevious(verified, record))
                return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                    "The final readback differs from the captured preference(s); manual review is required.", CancellationToken.None);

            return await PersistStateAsync(record, PowerModeOperationState.Reverted,
                "A later read confirmed the selected preferences were restored to their captured values.", CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                $"Restoration failed after it was armed ({exception.GetType().Name}); re-read current modes and review manually.",
                CancellationToken.None);
        }
    }

    private async Task<PowerModeOperationRecord> LoadRequiredAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var record = await _store.LoadAsync(operationId, cancellationToken);
        return record ?? throw new FileNotFoundException("Persisted power-mode capture was not found.");
    }

    private void EnsureAuthorized(PowerModeApproval approval, PowerModeOperationRecord record, PowerModeAction action)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.Action != action || !_mutationPolicy.IsAuthorized(approval, record))
            throw new UnauthorizedAccessException("Real power-mode changes are blocked or lack operation-specific consequence consent and confirmation.");
    }

    private async Task<PowerModeOperationRecord> PersistStateAsync(
        PowerModeOperationRecord record,
        PowerModeOperationState state,
        string detail,
        CancellationToken cancellationToken = default)
    {
        var updated = record with { State = state, UpdatedAtUtc = DateTimeOffset.UtcNow, Detail = detail };
        await _store.SaveAsync(updated, cancellationToken);
        return updated;
    }

    private static UserConfiguredPowerMode ReadKnownMode(Guid? id, uint? errorCode, string side)
    {
        if (id is Guid modeId && UserConfiguredPowerModes.TryParse(modeId, out var mode)) return mode;
        var suffix = errorCode is uint code ? $" (Windows error {code})" : string.Empty;
        throw new PlatformNotSupportedException($"A recognized configured {side} mode is not readable{suffix}; that side cannot be changed reversibly.");
    }

    private static bool MatchesTargets(PowerModeReadResult state, PowerModeOperationRecord record, bool useTargets)
    {
        if (!state.ApiAvailable) return false;
        foreach (var side in SelectedSides(record))
        {
            var expected = useTargets ? GetTarget(record, side) : GetPrevious(record, side);
            var id = GetModeId(state, side);
            if (id is not Guid modeId || !UserConfiguredPowerModes.TryParse(modeId, out var actual) || actual != expected)
                return false;
        }
        return true;
    }

    private static bool MatchesExpectedForApply(
        PowerModeReadResult state,
        PowerModeOperationRecord record,
        bool acWasWritten,
        bool dcWasWritten)
    {
        if (!state.ApiAvailable) return false;
        foreach (var side in SelectedSides(record))
        {
            var wasWritten = side == PowerModeSide.Ac ? acWasWritten : dcWasWritten;
            var expected = wasWritten && GetTarget(record, side) != GetPrevious(record, side)
                ? GetTarget(record, side)
                : GetPrevious(record, side);
            var id = GetModeId(state, side);
            if (id is not Guid modeId || !UserConfiguredPowerModes.TryParse(modeId, out var actual) || actual != expected)
                return false;
        }
        return true;
    }

    private static bool IsRestorableState(PowerModeReadResult state, PowerModeOperationRecord record)
    {
        if (!state.ApiAvailable) return false;
        foreach (var side in SelectedSides(record))
        {
            var id = GetModeId(state, side);
            if (id is not Guid modeId || !UserConfiguredPowerModes.TryParse(modeId, out var actual)) return false;
            if (actual != GetPrevious(record, side) && actual != GetTarget(record, side)) return false;
        }
        return true;
    }

    private static bool AllSelectedModesArePrevious(PowerModeReadResult state, PowerModeOperationRecord record) =>
        MatchesTargets(state, record, useTargets: false);

    private static bool AllSelectedModesEqualTargets(PowerModeOperationRecord record) =>
        SelectedSides(record).All(side => GetPrevious(record, side) == GetTarget(record, side));

    private static IEnumerable<PowerModeSide> SelectedSides(PowerModeOperationRecord record)
    {
        if (record.TargetAcMode is not null) yield return PowerModeSide.Ac;
        if (record.TargetDcMode is not null) yield return PowerModeSide.Dc;
    }

    private static UserConfiguredPowerMode GetPrevious(PowerModeOperationRecord record, PowerModeSide side) =>
        (side == PowerModeSide.Ac ? record.PreviousAcMode : record.PreviousDcMode)
        ?? throw new InvalidDataException($"{SideLabel(side)} prior mode is missing.");

    private static UserConfiguredPowerMode GetTarget(PowerModeOperationRecord record, PowerModeSide side) =>
        (side == PowerModeSide.Ac ? record.TargetAcMode : record.TargetDcMode)
        ?? throw new InvalidDataException($"{SideLabel(side)} target mode is missing.");

    private static Guid? GetModeId(PowerModeReadResult state, PowerModeSide side) =>
        side == PowerModeSide.Ac ? state.AcModeId : state.DcModeId;

    private static uint? GetErrorCode(PowerModeReadResult state, PowerModeSide side) =>
        side == PowerModeSide.Ac ? state.AcErrorCode : state.DcErrorCode;

    private static string SideLabel(PowerModeSide side) => side == PowerModeSide.Ac ? "AC" : "DC/bateria";

    private enum PowerModeSide { Ac, Dc }
}
