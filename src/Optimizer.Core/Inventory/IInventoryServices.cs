namespace Optimizer.Core.Inventory;

public interface IDriverInventoryService
{
    Task<DriverInventoryResult> CollectAsync(CancellationToken cancellationToken = default);
}

public interface IComputerInventoryService
{
    Task<ComputerInventoryResult> CollectAsync(CancellationToken cancellationToken = default);
}
