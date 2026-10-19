namespace Optimizer.Core.Inventory;

public sealed record PowerPlanInfo(
    Guid SchemeId,
    string Name,
    bool IsActive,
    uint? VideoIdleAcSeconds,
    uint? VideoIdleDcSeconds);

public sealed record PowerPlanInventoryResult(
    DateTimeOffset CollectedAtUtc,
    InventoryAvailability Availability,
    IReadOnlyList<PowerPlanInfo> Plans,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage);

public interface IPowerPlanInventoryService
{
    PowerPlanInventoryResult Collect(CancellationToken cancellationToken = default);
}
