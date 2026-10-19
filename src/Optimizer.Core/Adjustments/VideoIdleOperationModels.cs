using System.Security.Cryptography;
using System.Text;

namespace Optimizer.Core.Adjustments;

public static class VideoIdleSetting
{
    public const uint ProposedAcTimeoutSeconds = 900;
    public static readonly Guid DisplaySubgroupId = new("7516b95f-f776-4464-8c53-06167f40cc99");
    public static readonly Guid VideoIdleSettingId = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
}

public sealed record VideoIdlePowerState(Guid ActiveSchemeId, uint AcTimeoutSeconds);

public sealed record VideoIdlePreview(
    DateTimeOffset ReadAtUtc,
    Guid ActiveSchemeId,
    uint CurrentAcTimeoutSeconds,
    uint ProposedAcTimeoutSeconds)
{
    public bool AlreadyAtProposedValue => CurrentAcTimeoutSeconds == ProposedAcTimeoutSeconds;
}

public enum VideoIdleOperationState
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

public enum VideoIdleAction
{
    Apply,
    Revert
}

public sealed record VideoIdleOperationRecord(
    int SchemaVersion,
    Guid OperationId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    Guid ActiveSchemeId,
    uint PreviousAcTimeoutSeconds,
    uint ProposedAcTimeoutSeconds,
    string CaptureFingerprint,
    VideoIdleOperationState State,
    string Detail);

public sealed record VideoIdleApproval(
    Guid OperationId,
    VideoIdleAction Action,
    string CaptureFingerprint,
    bool ConsequenceAccepted,
    string ConfirmationPhrase)
{
    public static VideoIdleApproval Confirm(
        VideoIdleOperationRecord record,
        VideoIdleAction action,
        bool consequenceAccepted,
        string confirmationPhrase) => new(
            record.OperationId,
            action,
            record.CaptureFingerprint,
            consequenceAccepted,
            confirmationPhrase);

    public static string RequiredPhrase(Guid operationId, VideoIdleAction action)
    {
        var verb = action == VideoIdleAction.Apply ? "APPLY" : "REVERT";
        return $"{verb} VIDEOIDLE {operationId.ToString("N")[..8].ToUpperInvariant()}";
    }
}

public interface IVideoIdlePowerAdapter
{
    VideoIdlePowerState ReadActiveAcTimeout();
    void WriteAcTimeout(Guid schemeId, uint seconds);
    void ActivateScheme(Guid schemeId);
}

public interface IVideoIdleMutationPolicy
{
    bool IsAuthorized(VideoIdleApproval approval, VideoIdleOperationRecord record);
}

/// <summary>Production default: denies every real write until a separately approved policy is wired.</summary>
public sealed class BlockedVideoIdleMutationPolicy : IVideoIdleMutationPolicy
{
    public bool IsAuthorized(VideoIdleApproval approval, VideoIdleOperationRecord record) => false;
}

/// <summary>Explicit opt-in policy; approval is bound to one operation, action, fingerprint, consequence and phrase.</summary>
public sealed class ConfirmedVideoIdleMutationPolicy(bool enabled) : IVideoIdleMutationPolicy
{
    public bool IsAuthorized(VideoIdleApproval approval, VideoIdleOperationRecord record) =>
        enabled &&
        approval.OperationId == record.OperationId &&
        approval.Action is VideoIdleAction.Apply or VideoIdleAction.Revert &&
        string.Equals(approval.CaptureFingerprint, record.CaptureFingerprint, StringComparison.Ordinal) &&
        approval.ConsequenceAccepted &&
        string.Equals(
            approval.ConfirmationPhrase,
            VideoIdleApproval.RequiredPhrase(record.OperationId, approval.Action),
            StringComparison.Ordinal);
}

internal static class VideoIdleCaptureFingerprint
{
    public static string Compute(Guid operationId, Guid schemeId, uint previousSeconds, uint proposedSeconds)
    {
        var canonical = string.Join("\n",
            operationId.ToString("D"),
            schemeId.ToString("D"),
            VideoIdleSetting.DisplaySubgroupId.ToString("D"),
            VideoIdleSetting.VideoIdleSettingId.ToString("D"),
            previousSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            proposedSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
