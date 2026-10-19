namespace Optimizer.Core.Inventory;

public sealed record ComputerInventory(
    DateTimeOffset CollectedAtUtc,
    string? WindowsName,
    string? WindowsEdition,
    string? WindowsVersion,
    string? WindowsBuild,
    string? Architecture,
    string? Processor,
    IReadOnlyList<string> GraphicsAdapters,
    string? Memory,
    string? Motherboard,
    IReadOnlyList<string> StorageDevices,
    IReadOnlyList<string> NetworkAdapters,
    IReadOnlyList<string> Warnings);

public sealed record ComputerInventoryResult(
    ComputerInventory? Inventory,
    string? ErrorMessage)
{
    public bool IsAvailable => Inventory is not null;
}
