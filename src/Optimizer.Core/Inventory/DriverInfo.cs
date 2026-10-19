namespace Optimizer.Core.Inventory;

/// <summary>
/// Driver data reported by Windows. DeviceId is retained only in memory for
/// correlating Plug and Play data; it must never be displayed or exported.
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
    int? ProblemCode,
    bool DriverMetadataAvailable = true)
{
    public string DriverDetailsStatus => !DriverMetadataAvailable
        ? "Detalhes do driver não retornados"
        : IsSigned switch
        {
            true => "Driver assinado",
            false => "Driver sem assinatura reportada",
            _ => "Assinatura não informada"
        };

    public string ProblemCodeLabel => ProblemCode is int code
        ? $"PnP {code} reportado pelo Windows"
        : "Código PnP não informado";
}
