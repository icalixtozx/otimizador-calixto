using System.Security.Cryptography;
using System.Text;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Recovery;

namespace Optimizer.Core.Processes;

public enum ProcessPriorityLevel
{
    Unknown,
    Idle,
    BelowNormal,
    Normal,
    AboveNormal,
    High,
    Realtime
}

public enum ProcessInspectionStatus
{
    Available,
    Partial,
    AccessDenied,
    Exited,
    NotFound,
    Failed
}

public enum ProcessIdentityComparison
{
    Match,
    DifferentProcess,
    Unverifiable
}

public enum ProcessPriorityWriteStatus
{
    Applied,
    AccessDenied,
    ProcessExited,
    IdentityMismatch,
    IdentityUnverifiable,
    PriorityConflict,
    Failed
}

public enum ProcessPriorityAction
{
    Apply,
    Restore
}

public sealed record ProcessIdentity(
    int ProcessId,
    DateTimeOffset CreatedAtUtc,
    string? ExecutablePath)
{
    public ProcessIdentityComparison CompareTo(ProcessIdentity? current)
    {
        if (current is null) return ProcessIdentityComparison.Unverifiable;
        if (ProcessId != current.ProcessId || CreatedAtUtc.UtcTicks != current.CreatedAtUtc.UtcTicks)
            return ProcessIdentityComparison.DifferentProcess;

        if (!string.IsNullOrWhiteSpace(ExecutablePath))
        {
            if (string.IsNullOrWhiteSpace(current.ExecutablePath))
                return ProcessIdentityComparison.Unverifiable;
            if (!string.Equals(ExecutablePath, current.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                return ProcessIdentityComparison.DifferentProcess;
        }

        return ProcessIdentityComparison.Match;
    }
}

public sealed record ProcessObservation(
    int ProcessId,
    string? Name,
    ProcessIdentity? Identity,
    ProcessPriorityLevel? CurrentPriority,
    ProcessInspectionStatus Status,
    string Detail)
{
    public bool CanReviewPriority =>
        Identity is not null && CurrentPriority is not null &&
        Status is ProcessInspectionStatus.Available or ProcessInspectionStatus.Partial;
}

public sealed record ProcessInventoryResult(
    DateTimeOffset ReadAtUtc,
    IReadOnlyList<ProcessObservation> Processes,
    IReadOnlyList<string> Warnings);

public sealed record ProcessPriorityWriteResult(
    ProcessPriorityWriteStatus Status,
    bool WriteAttempted,
    string Detail);

public sealed record ProcessPriorityReview(
    ProcessIdentity Identity,
    string ProcessName,
    ProcessPriorityLevel CurrentPriority,
    ProcessPriorityLevel TargetPriority,
    AdjustmentPlan Plan,
    DateTimeOffset ReviewedAtUtc,
    string CompatibilityMessage)
{
    public bool IsNoChange => CurrentPriority == TargetPriority;
}

public sealed record ProcessPriorityApproval(
    Guid OperationId,
    ProcessPriorityAction Action,
    string CaptureFingerprint,
    bool ConsequencesAccepted,
    bool RecoveryConsequencesAccepted,
    string ConfirmationPhrase);

public interface IProcessPriorityAdapter
{
    ProcessInventoryResult ListProcesses();
    ProcessObservation Inspect(int processId);
    ProcessPriorityWriteResult SetPriority(
        ProcessIdentity expectedIdentity,
        ProcessPriorityLevel expectedCurrentPriority,
        ProcessPriorityLevel targetPriority);
}

public interface IProcessPriorityMutationPolicy
{
    bool IsAuthorized(ProcessPriorityApproval approval, OperationJournalEntry entry, ProcessPrioritySnapshot snapshot);
}

/// <summary>Production default: no process priority write can be performed.</summary>
public sealed class BlockedProcessPriorityMutationPolicy : IProcessPriorityMutationPolicy
{
    public bool IsAuthorized(ProcessPriorityApproval approval, OperationJournalEntry entry, ProcessPrioritySnapshot snapshot) => false;
}

/// <summary>Explicit opt-in policy for isolated tests. The application must keep the blocked policy by default.</summary>
public sealed class ConfirmedProcessPriorityMutationPolicy(bool enabled) : IProcessPriorityMutationPolicy
{
    public bool IsAuthorized(ProcessPriorityApproval approval, OperationJournalEntry entry, ProcessPrioritySnapshot snapshot)
    {
        if (!enabled || approval.OperationId != entry.OperationId ||
            !string.Equals(approval.CaptureFingerprint, snapshot.CaptureFingerprint, StringComparison.Ordinal) ||
            !approval.ConsequencesAccepted)
        {
            return false;
        }

        var expectedPhrase = ProcessPriorityOperationService.RequiredConfirmationPhrase(entry.OperationId, approval.Action);
        return string.Equals(approval.ConfirmationPhrase, expectedPhrase, StringComparison.Ordinal) &&
            (approval.Action != ProcessPriorityAction.Restore || approval.RecoveryConsequencesAccepted);
    }
}

public sealed record ProcessPrioritySnapshot(
    ProcessIdentity Identity,
    ProcessPriorityLevel PreviousPriority,
    ProcessPriorityLevel TargetPriority,
    string CaptureFingerprint);

public static class ProcessPriorityTargets
{
    /// <summary>Windows' Realtime class is intentionally excluded from user selectable targets.</summary>
    public static IReadOnlyList<ProcessPriorityLevel> Allowed { get; } = Array.AsReadOnly(
        [ProcessPriorityLevel.Idle, ProcessPriorityLevel.BelowNormal, ProcessPriorityLevel.Normal,
            ProcessPriorityLevel.AboveNormal, ProcessPriorityLevel.High]);

    public static string DisplayName(ProcessPriorityLevel priority) => priority switch
    {
        ProcessPriorityLevel.Idle => "Ocioso",
        ProcessPriorityLevel.BelowNormal => "Abaixo do normal",
        ProcessPriorityLevel.Normal => "Normal",
        ProcessPriorityLevel.AboveNormal => "Acima do normal",
        ProcessPriorityLevel.High => "Alta",
        ProcessPriorityLevel.Realtime => "Tempo real (somente leitura)",
        _ => "Desconhecida"
    };
}

internal static class ProcessPriorityCaptureFingerprint
{
    public static string Compute(Guid operationId, string planFingerprint, ProcessPrioritySnapshot snapshot)
    {
        var canonical = string.Join('\n',
            operationId.ToString("D"),
            planFingerprint,
            snapshot.Identity.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            snapshot.Identity.CreatedAtUtc.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            snapshot.Identity.ExecutablePath ?? "<path-unavailable>",
            snapshot.PreviousPriority.ToString(),
            snapshot.TargetPriority.ToString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
