namespace Optimizer.Core.Adjustments;

public enum AdjustmentArea
{
    Power,
    Registry,
    Policies,
    Processes,
    Network,
    Graphics,
    Other
}

public enum AdjustmentRiskLevel
{
    Unknown,
    Low,
    Moderate,
    High
}

public enum AdjustmentAvailability
{
    SimulationOnly = 0,
    BlockedExperimental = 1,
    RequiresSpecificApproval = 2
}

public enum CompatibilityStatus
{
    SimulationSupported = 0,
    Blocked = 1,
    Incompatible = 2,
    Unknown = 3,
    ReviewSupported = 4
}

public enum PlanItemStatus
{
    ReadyForSimulation = 0,
    Blocked = 1,
    Incompatible = 2,
    UnknownAdjustment = 3,
    ReadyForExplicitApproval = 4
}

public enum AdjustmentSimulationOutcome
{
    Completed,
    Refused,
    Failed,
    Interrupted
}

public enum AdjustmentSimulationStepStatus
{
    Simulated,
    Failed,
    Interrupted,
    NotRun
}

public sealed record AdjustmentDefinition(
    string Id,
    int Version,
    string Name,
    string Description,
    AdjustmentArea Area,
    AdjustmentRiskLevel Risk,
    string Consequence,
    bool RequiresAdministrator,
    IReadOnlyList<string> SupportedArchitectures,
    int? MinimumWindowsBuild,
    AdjustmentAvailability Availability,
    string? AvailabilityReason);

public sealed record PlatformDescriptor(
    bool IsWindows,
    string Architecture,
    int? WindowsBuild);

public sealed record CompatibilityAssessment(
    CompatibilityStatus Status,
    bool CanSimulate,
    bool CanReview,
    string Reason);

public sealed record AdjustmentPlanItem(
    string AdjustmentId,
    int? Version,
    string Name,
    string? Description,
    AdjustmentArea? Area,
    AdjustmentRiskLevel Risk,
    bool? RequiresAdministrator,
    string Consequence,
    AdjustmentAvailability? Availability,
    PlanItemStatus Status,
    string Reason);

public sealed record AdjustmentPlan(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    PlatformDescriptor Platform,
    IReadOnlyList<AdjustmentPlanItem> Items,
    string Fingerprint)
{
    public bool CanSimulate =>
        Items.Count > 0 &&
        Items.All(item => item.Status == PlanItemStatus.ReadyForSimulation);

    public bool CanAcceptConsent => Items.Count > 0 &&
        Items.All(item => item.Status is PlanItemStatus.ReadyForSimulation or PlanItemStatus.ReadyForExplicitApproval);

    // Real application is deliberately unavailable until a separately validated implementation exists.
    public bool CanApply => false;
}

public sealed record AdjustmentSimulationStep(
    string AdjustmentId,
    AdjustmentSimulationStepStatus Status,
    string Detail);

public sealed record AdjustmentSimulationResult(
    Guid PlanId,
    AdjustmentSimulationOutcome Outcome,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    IReadOnlyList<AdjustmentSimulationStep> Steps,
    string Message,
    bool WindowsChanged);
