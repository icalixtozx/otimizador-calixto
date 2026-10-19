namespace Optimizer.Core.Inventory;

/// <summary>
/// A driver row reported by Windows. DeviceId is retained only for in-memory
/// correlation with Plug and Play data; user-facing exports omit it.
/// </summary>
public sealed record DriverInfo(
    string DeviceName,
    string? DeviceId,
    string? Provider,
    string? Version,
    string? DriverDateRaw,
    string? InfName,
    bool? IsSigned,
    string? Signer,
    int? ProblemCode);
