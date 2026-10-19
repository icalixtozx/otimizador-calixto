namespace Optimizer.Core.Inventory;

public enum InventoryAvailability
{
    Available,
    Partial,
    Failed
}

public sealed record DriverInventoryResult(
    DateTimeOffset CollectedAtUtc,
    InventoryAvailability Availability,
    IReadOnlyList<DriverInfo> Drivers,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage)
{
    public static DriverInventoryResult Failed(string message) => new(
        DateTimeOffset.UtcNow,
        InventoryAvailability.Failed,
        Array.Empty<DriverInfo>(),
        Array.Empty<string>(),
        message);
}

public enum DriverSignatureFilter
{
    All,
    Signed,
    Unsigned,
    Unknown
}
