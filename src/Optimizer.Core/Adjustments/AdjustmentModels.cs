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
    SimulationOnly,
    BlockedExperimental
}

public enum CompatibilityStatus
{
    SimulationSupported,
    Blocked,
    Incompatible,
    Unknown
}

public enum PlanItemStatus
{
    ReadyForSimulation,
    Blocked,
    Incompatible,
    UnknownAdjustment
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
