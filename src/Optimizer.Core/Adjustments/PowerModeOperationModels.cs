using System.Security.Cryptography;
using System.Text;

namespace Optimizer.Core.Adjustments;

public enum UserConfiguredPowerMode
{
    BestEfficiency,
    Balanced,
    BestPerformance
}

public static class UserConfiguredPowerModes
{
    public static readonly Guid BestEfficiencyId = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    public static readonly Guid BalancedId = Guid.Empty;
    public static readonly Guid BestPerformanceId = new("ded574b5-45a0-4f42-8737-46345c09c238");

    public static bool TryParse(Guid id, out UserConfiguredPowerMode mode)
    {
        if (id == BestEfficiencyId) { mode = UserConfiguredPowerMode.BestEfficiency; return true; }
        if (id == BalancedId) { mode = UserConfiguredPowerMode.Balanced; return true; }
        if (id == BestPerformanceId) { mode = UserConfiguredPowerMode.BestPerformance; return true; }
        mode = default;
        return false;
    }

    public static Guid ToId(UserConfiguredPowerMode mode) => mode switch
    {
        UserConfiguredPowerMode.BestEfficiency => BestEfficiencyId,
        UserConfiguredPowerMode.Balanced => BalancedId,
        UserConfiguredPowerMode.BestPerformance => BestPerformanceId,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static string DisplayName(UserConfiguredPowerMode mode) => mode switch
    {
        UserConfiguredPowerMode.BestEfficiency => "Melhor eficiência energética",
        UserConfiguredPowerMode.Balanced => "Equilibrado",
        UserConfiguredPowerMode.BestPerformance => "Melhor desempenho",
        _ => "Desconhecido"
    };
}

/// <summary>Read-only values returned by Windows' user-configured mode API, not the effective runtime mode.</summary>
public sealed record PowerModeReadResult(
    DateTimeOffset ReadAtUtc,
    bool ApiAvailable,
    Guid? AcModeId,
    Guid? DcModeId,
    uint? AcErrorCode,
    uint? DcErrorCode,
    string CompatibilityReason,
    Guid? ActivePlanId = null,
    uint? ActivePlanErrorCode = null)
{
    public bool CanModifyAc => ApiAvailable && ActivePlanId is not null &&
        AcModeId is Guid id && UserConfiguredPowerModes.TryParse(id, out _);
    public bool CanModifyDc => ApiAvailable && ActivePlanId is not null &&
        DcModeId is Guid id && UserConfiguredPowerModes.TryParse(id, out _);
}

public enum PowerModeOperationState
{
    Captured,
    MutationMayHaveStarted,
    Verified,
    NoChangeRequired,
    Conflict,
    MutationUnverified,
    ReversionMayHaveStarted,
    Reverted,
    ReversionUnverified
}

public enum PowerModeAction
{
    Apply,
    Revert
}

/// <summary>A persisted snapshot of only the selected AC/DC user-configured preferences.</summary>
public sealed record PowerModeOperationRecord(
    int SchemaVersion,
    Guid OperationId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    UserConfiguredPowerMode? PreviousAcMode,
    UserConfiguredPowerMode? PreviousDcMode,
    UserConfiguredPowerMode? TargetAcMode,
    UserConfiguredPowerMode? TargetDcMode,
    string CaptureFingerprint,
    PowerModeOperationState State,
    string Detail,
    Guid? CapturedActivePlanId = null);

public sealed record PowerModeApproval(
    Guid OperationId,
    PowerModeAction Action,
    string CaptureFingerprint,
    bool AcConsequencesAccepted,
    bool DcConsequencesAccepted,
    bool RecoveryConsequencesAccepted,
    string ConfirmationPhrase)
{
    public static PowerModeApproval Confirm(
        PowerModeOperationRecord record,
        PowerModeAction action,
        bool acConsequencesAccepted,
        bool dcConsequencesAccepted,
        bool recoveryConsequencesAccepted,
        string confirmationPhrase) => new(
            record.OperationId,
            action,
            record.CaptureFingerprint,
            acConsequencesAccepted,
            dcConsequencesAccepted,
            recoveryConsequencesAccepted,
            confirmationPhrase);

    public static string RequiredPhrase(Guid operationId, PowerModeAction action)
    {
        var verb = action == PowerModeAction.Apply ? "APPLY" : "REVERT";
        return $"{verb} POWER MODE {operationId.ToString("N")[..8].ToUpperInvariant()}";
    }
}

public interface IPowerModeAdapter
{
    PowerModeReadResult ReadConfiguredModes();
    void SetConfiguredAcMode(UserConfiguredPowerMode mode);
    void SetConfiguredDcMode(UserConfiguredPowerMode mode);
}

public interface IPowerModeMutationPolicy
{
    bool IsAuthorized(PowerModeApproval approval, PowerModeOperationRecord record);
}

/// <summary>Production default: denies every actual change.</summary>
public sealed class BlockedPowerModeMutationPolicy : IPowerModeMutationPolicy
{
    public bool IsAuthorized(PowerModeApproval approval, PowerModeOperationRecord record) => false;
}

/// <summary>Explicit opt-in policy used only by isolated tests until separate execution approval is granted.</summary>
public sealed class ConfirmedPowerModeMutationPolicy(bool enabled) : IPowerModeMutationPolicy
{
    public bool IsAuthorized(PowerModeApproval approval, PowerModeOperationRecord record)
    {
        if (!enabled || approval.OperationId != record.OperationId ||
            !string.Equals(approval.CaptureFingerprint, record.CaptureFingerprint, StringComparison.Ordinal))
            return false;

        if (approval.Action == PowerModeAction.Apply)
        {
            return (record.TargetAcMode is null || approval.AcConsequencesAccepted) &&
                (record.TargetDcMode is null || approval.DcConsequencesAccepted) &&
                string.Equals(approval.ConfirmationPhrase,
                    PowerModeApproval.RequiredPhrase(record.OperationId, PowerModeAction.Apply), StringComparison.Ordinal);
        }

        return approval.Action == PowerModeAction.Revert && approval.RecoveryConsequencesAccepted &&
            string.Equals(approval.ConfirmationPhrase,
                PowerModeApproval.RequiredPhrase(record.OperationId, PowerModeAction.Revert), StringComparison.Ordinal);
    }
}

internal static class PowerModeCaptureFingerprint
{
    public static string Compute(PowerModeOperationRecord record)
    {
        var parts = new List<string>
        {
            record.OperationId.ToString("D"),
            record.PreviousAcMode?.ToString() ?? "<none>",
            record.PreviousDcMode?.ToString() ?? "<none>",
            record.TargetAcMode?.ToString() ?? "<none>",
            record.TargetDcMode?.ToString() ?? "<none>"
        };
        if (record.SchemaVersion >= 2)
            parts.Add(record.CapturedActivePlanId?.ToString("D") ?? "<no-active-plan>");
        var canonical = string.Join("\n", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
