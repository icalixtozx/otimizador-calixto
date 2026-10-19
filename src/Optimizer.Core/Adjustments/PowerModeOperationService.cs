namespace Optimizer.Core.Adjustments;

/// <summary>
/// Owns persisted power-mode operations. Every operation is bound to a fresh AC/DC read and active
/// power-plan GUID. Real writes require an enabled policy plus operation-specific consent.
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
        if (!current.ApiAvailable || !current.CanModifyAc || !current.CanModifyDc || current.ActivePlanId is null)
            throw new PlatformNotSupportedException(
                "A persisted reversible capture requires readable AC, DC, and active-plan state. No setting was changed.");

        // Capture both sides even for a one-sided proposal. This provides a baseline to detect drift
        // and verify preservation of the unselected side before and after a future operation.
        var previousAc = ReadKnownMode(current.AcModeId, current.AcErrorCode, "AC");
        var previousDc = ReadKnownMode(current.DcModeId, current.DcErrorCode, "DC");
        var now = DateTimeOffset.UtcNow;
        var record = new PowerModeOperationRecord(
            2,
            Guid.NewGuid(),
            now,
            now,
            previousAc,
            previousDc,
            targetAcMode,
            targetDcMode,
            string.Empty,
            PowerModeOperationState.Captured,
            "Captura real persistida de AC, DC e plano ativo. Nenhuma configuração foi alterada; não é backup do Windows.",
            current.ActivePlanId);
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
        if (record.SchemaVersion < 2 || record.CapturedActivePlanId is null)
            throw new InvalidOperationException("This legacy capture lacks a full AC/DC/active-plan baseline. Create a new capture for review.");

        // This is a new read for this operation, never the UI's older preview. A difference means
        // the reviewed plan is stale and no setter is called.
        var initial = _power.ReadConfiguredModes();
        if (!MatchesCapturedSnapshot(initial, record))
            return await PersistStateAsync(record, PowerModeOperationState.Conflict,
                "AC, DC, or the active power plan changed since review; no write was attempted. Re-read and create a new capture for review.");
        if (AllSelectedModesEqualTargets(record))
            return await PersistStateAsync(record, PowerModeOperationState.NoChangeRequired,
                "The selected modes already match the requested values; no write was needed.");

        record = await PersistStateAsync(record, PowerModeOperationState.MutationMayHaveStarted,
            "Application intent was persisted before the first write. Result is not verified.", CancellationToken.None);
        var acWasWritten = false;
        var dcWasWritten = false;
        try
        {
            foreach (var side in SelectedSides(record))
            {
                var before = _power.ReadConfiguredModes();
                if (!MatchesExpectedForApply(before, record, acWasWritten, dcWasWritten))
                    return await PersistStateAsync(record,
                        acWasWritten || dcWasWritten ? PowerModeOperationState.MutationUnverified : PowerModeOperationState.Conflict,
                        "AC, DC, or the active plan changed before this write; no further write was attempted. Review the current state.", CancellationToken.None);

                var previous = GetPrevious(record, side);
                var target = GetTarget(record, side);
                if (previous == target) continue;

                if (side == PowerModeSide.Ac) _power.SetConfiguredAcMode(target);
                else _power.SetConfiguredDcMode(target);
                if (side == PowerModeSide.Ac) acWasWritten = true;
                else dcWasWritten = true;

                var after = _power.ReadConfiguredModes();
                if (!MatchesExpectedForApply(after, record, acWasWritten, dcWasWritten))
                    return await PersistStateAsync(record, PowerModeOperationState.MutationUnverified,
                        "Readback did not confirm the requested side or preservation of the other side and active plan; manual review is required.", CancellationToken.None);

                record = await PersistStateAsync(record, PowerModeOperationState.MutationMayHaveStarted,
                    $"{SideLabel(side)} preference verified; the other side and active plan also matched the captured state.", CancellationToken.None);
            }

            var verified = _power.ReadConfiguredModes();
            if (!MatchesExpectedForApply(verified, record, acWasWritten, dcWasWritten))
                return await PersistStateAsync(record, PowerModeOperationState.MutationUnverified,
                    "Final readback differs from the approved AC/DC targets or the preserved AC/DC/active-plan state.", CancellationToken.None);

            return await PersistStateAsync(record, PowerModeOperationState.Verified,
                "A later read confirmed selected preferences and confirmed the unselected side and active plan were preserved. This does not prove effective runtime mode or performance gain.", CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return await PersistStateAsync(record, PowerModeOperationState.MutationUnverified,
                $"The operation failed after it was armed ({exception.GetType().Name}); re-read current modes and plan, then review manually.",
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
        if (record.SchemaVersion < 2 || record.CapturedActivePlanId is null)
            throw new InvalidOperationException("This legacy capture lacks a full AC/DC/active-plan baseline and cannot be restored by this version.");
        if (record.State is not (PowerModeOperationState.Verified or PowerModeOperationState.MutationMayHaveStarted or
            PowerModeOperationState.MutationUnverified or PowerModeOperationState.ReversionUnverified))
            throw new InvalidOperationException("The power-mode operation state does not permit conditional restoration.");

        var current = _power.ReadConfiguredModes();
        if (!IsRestorableState(current, record))
            return await PersistStateAsync(record, PowerModeOperationState.Conflict,
                "AC, DC, or the active plan differs from the captured state and operation targets; restoration was refused without a write.");
        if (AllSelectedModesArePrevious(current, record))
            return await PersistStateAsync(record, PowerModeOperationState.Reverted,
                "Selected preferences already match captured values, and the other side and active plan are preserved; no restoration write was needed.");

        record = await PersistStateAsync(record, PowerModeOperationState.ReversionMayHaveStarted,
            "Restoration intent was persisted before the first write. Result is not verified.", CancellationToken.None);
        var expectedAc = ReadMode(current, PowerModeSide.Ac);
        var expectedDc = ReadMode(current, PowerModeSide.Dc);
        try
        {
            foreach (var side in SelectedSides(record))
            {
                var before = _power.ReadConfiguredModes();
                if (!MatchesExpectedState(before, record, expectedAc, expectedDc))
                    return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                        "AC, DC, or the active plan changed during restoration; no further write was attempted.", CancellationToken.None);

                var previous = GetPrevious(record, side);
                var target = GetTarget(record, side);
                var currentMode = ReadMode(before, side);
                if (currentMode == previous) continue;
                if (currentMode != target)
                    return await PersistStateAsync(record, PowerModeOperationState.Conflict,
                        $"{SideLabel(side)} no longer equals the requested preference; restoration was refused.", CancellationToken.None);

                if (side == PowerModeSide.Ac) _power.SetConfiguredAcMode(previous);
                else _power.SetConfiguredDcMode(previous);
                if (side == PowerModeSide.Ac) expectedAc = previous;
                else expectedDc = previous;

                var after = _power.ReadConfiguredModes();
                if (!MatchesExpectedState(after, record, expectedAc, expectedDc))
                    return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                        $"Readback after restoring {SideLabel(side)} did not confirm the captured value and preservation of the other side and active plan.", CancellationToken.None);

                record = await PersistStateAsync(record, PowerModeOperationState.ReversionMayHaveStarted,
                    $"{SideLabel(side)} captured preference restored and read back; the other side and active plan were preserved.", CancellationToken.None);
            }

            var verified = _power.ReadConfiguredModes();
            if (!MatchesExpectedState(verified, record, expectedAc, expectedDc))
                return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                    "Final readback differs from captured preferences or the preserved state; manual review is required.", CancellationToken.None);

            return await PersistStateAsync(record, PowerModeOperationState.Reverted,
                "A later read confirmed captured AC/DC preferences and preserved the unselected side and active plan.", CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return await PersistStateAsync(record, PowerModeOperationState.ReversionUnverified,
                $"Restoration failed after it was armed ({exception.GetType().Name}); re-read current modes and plan, then review manually.",
                CancellationToken.None);
        }
    }

    public static bool MatchesCapturedState(PowerModeReadResult state, PowerModeOperationRecord record) =>
        MatchesCapturedSnapshot(state, record);

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
        throw new PlatformNotSupportedException($"A recognized configured {side} mode is not readable{suffix}; AC, DC, and plan must all be readable for a reversible operation.");
    }

    private static bool MatchesCapturedSnapshot(PowerModeReadResult state, PowerModeOperationRecord record) =>
        record.SchemaVersion >= 2 && record.CapturedActivePlanId is Guid planId && state.ApiAvailable &&
        state.ActivePlanId == planId && ReadModeOrNull(state, PowerModeSide.Ac) == record.PreviousAcMode &&
        ReadModeOrNull(state, PowerModeSide.Dc) == record.PreviousDcMode;

    private static bool MatchesExpectedForApply(PowerModeReadResult state, PowerModeOperationRecord record, bool acWasWritten, bool dcWasWritten)
    {
        if (!HasCapturedPlan(state, record)) return false;
        var expectedAc = record.TargetAcMode is not null && acWasWritten ? record.TargetAcMode : record.PreviousAcMode;
        var expectedDc = record.TargetDcMode is not null && dcWasWritten ? record.TargetDcMode : record.PreviousDcMode;
        return ReadModeOrNull(state, PowerModeSide.Ac) == expectedAc && ReadModeOrNull(state, PowerModeSide.Dc) == expectedDc;
    }

    private static bool IsRestorableState(PowerModeReadResult state, PowerModeOperationRecord record)
    {
        if (!HasCapturedPlan(state, record)) return false;
        var ac = ReadModeOrNull(state, PowerModeSide.Ac);
        var dc = ReadModeOrNull(state, PowerModeSide.Dc);
        if (ac is null || dc is null) return false;
        return IsPreviousOrTarget(ac.Value, record.PreviousAcMode, record.TargetAcMode) &&
            IsPreviousOrTarget(dc.Value, record.PreviousDcMode, record.TargetDcMode);
    }

    private static bool MatchesExpectedState(PowerModeReadResult state, PowerModeOperationRecord record,
        UserConfiguredPowerMode expectedAc, UserConfiguredPowerMode expectedDc) =>
        HasCapturedPlan(state, record) && ReadModeOrNull(state, PowerModeSide.Ac) == expectedAc &&
        ReadModeOrNull(state, PowerModeSide.Dc) == expectedDc;

    private static bool AllSelectedModesArePrevious(PowerModeReadResult state, PowerModeOperationRecord record)
    {
        if (!HasCapturedPlan(state, record)) return false;
        return ReadModeOrNull(state, PowerModeSide.Ac) == record.PreviousAcMode &&
            ReadModeOrNull(state, PowerModeSide.Dc) == record.PreviousDcMode;
    }

    private static bool AllSelectedModesEqualTargets(PowerModeOperationRecord record) =>
        SelectedSides(record).All(side => GetPrevious(record, side) == GetTarget(record, side));

    private static bool HasCapturedPlan(PowerModeReadResult state, PowerModeOperationRecord record) =>
        state.ApiAvailable && record.CapturedActivePlanId is Guid expected && state.ActivePlanId == expected;

    private static bool IsPreviousOrTarget(UserConfiguredPowerMode current, UserConfiguredPowerMode? previous, UserConfiguredPowerMode? target) =>
        previous is UserConfiguredPowerMode prior &&
        (current == prior || target is UserConfiguredPowerMode requested && current == requested);

    private static IEnumerable<PowerModeSide> SelectedSides(PowerModeOperationRecord record)
    {
        if (record.TargetAcMode is not null) yield return PowerModeSide.Ac;
        if (record.TargetDcMode is not null) yield return PowerModeSide.Dc;
    }

    private static UserConfiguredPowerMode GetPrevious(PowerModeOperationRecord record, PowerModeSide side) =>
        (side == PowerModeSide.Ac ? record.PreviousAcMode : record.PreviousDcMode)
        ?? throw new InvalidDataException($"{SideLabel(side)} prior mode is missing.");

    private static UserConfiguredPowerMode GetTarget(PowerModeOperationRecord record, PowerModeSide side) =>
        GetTargetOptional(record, side) ?? throw new InvalidDataException($"{SideLabel(side)} target is missing.");

    private static UserConfiguredPowerMode? GetTargetOptional(PowerModeOperationRecord record, PowerModeSide side) =>
        side == PowerModeSide.Ac ? record.TargetAcMode : record.TargetDcMode;

    private static UserConfiguredPowerMode ReadMode(PowerModeReadResult state, PowerModeSide side) =>
        ReadModeOrNull(state, side) ?? throw new PlatformNotSupportedException($"A recognized {SideLabel(side)} preference is not readable.");

    private static UserConfiguredPowerMode? ReadModeOrNull(PowerModeReadResult state, PowerModeSide side)
    {
        var id = side == PowerModeSide.Ac ? state.AcModeId : state.DcModeId;
        return id is Guid modeId && UserConfiguredPowerModes.TryParse(modeId, out var mode) ? mode : null;
    }

    private static string SideLabel(PowerModeSide side) => side == PowerModeSide.Ac ? "AC" : "DC/bateria";

    private enum PowerModeSide { Ac, Dc }
}
