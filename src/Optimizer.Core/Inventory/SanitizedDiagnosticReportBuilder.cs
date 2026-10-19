using System.Text.Json;
using System.Text.Json.Serialization;

namespace Optimizer.Core.Inventory;

/// <summary>
/// Produces a reviewable diagnostic document that excludes machine/account names,
/// PnP IDs, scheme GUIDs, serial numbers, network addresses, and local file paths.
/// </summary>
public static class SanitizedDiagnosticReportBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string BuildJson(
        ComputerInventory? computer,
        DriverInventoryResult? drivers,
        PowerPlanInventoryResult? powerPlans,
        DateTimeOffset? generatedAtUtc = null)
    {
        var report = new
        {
            schemaVersion = 1,
            generatedAtUtc = (generatedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            reviewNotice = "Relatório local para revisão manual. Não contém nome da conta, nome do computador, identificadores PnP, GUIDs dos planos, números de série, endereços de rede ou caminhos locais. Revise os nomes de hardware antes de compartilhar.",
            system = computer is null ? null : new
            {
                collectedAtUtc = computer.CollectedAtUtc,
                windowsProduct = computer.WindowsName,
                windowsEdition = computer.WindowsEdition,
                windowsVersion = computer.WindowsVersion,
                windowsBuild = computer.WindowsBuild,
                architecture = computer.Architecture,
                processor = computer.Processor,
                graphicsAdapters = computer.GraphicsAdapters,
                memory = computer.Memory,
                motherboard = computer.Motherboard,
                storageDevices = computer.StorageDevices,
                networkAdapters = computer.NetworkAdapters,
                warnings = computer.Warnings
            },
            power = powerPlans is null ? null : new
            {
                collectedAtUtc = powerPlans.CollectedAtUtc,
                availability = powerPlans.Availability.ToString(),
                error = powerPlans.ErrorMessage,
                warnings = powerPlans.Warnings,
                plans = powerPlans.Plans.Select(plan => new
                {
                    plan.Name,
                    plan.IsActive,
                    plan.VideoIdleAcSeconds,
                    plan.VideoIdleDcSeconds
                }).ToArray()
            },
            drivers = drivers is null ? null : new
            {
                collectedAtUtc = drivers.CollectedAtUtc,
                availability = drivers.Availability.ToString(),
                error = drivers.ErrorMessage,
                warnings = drivers.Warnings,
                items = drivers.Drivers.Select(driver => new
                {
                    driver.DeviceName,
                    driver.Provider,
                    driver.Version,
                    driver.DriverDateRaw,
                    driver.InfName,
                    driver.IsSigned,
                    driver.Signer,
                    problemCodeWindowsReported = driver.ProblemCode
                }).ToArray()
            }
        };

        return JsonSerializer.Serialize(report, JsonOptions);
    }
}
