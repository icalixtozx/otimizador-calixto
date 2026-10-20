using System.Globalization;
using System.Management;
using Optimizer.Core.Inventory;

namespace Optimizer.Windows;

public sealed class WmiDriverInventoryService : IDriverInventoryService
{
    public Task<DriverInventoryResult> CollectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Collect(cancellationToken), cancellationToken);

    private static DriverInventoryResult Collect(CancellationToken cancellationToken)
    {
        var drivers = new List<DriverInfo>();
        var warnings = new List<string>();

        var driversRead = TryRead(
            "SELECT DeviceName, DeviceID, DriverProviderName, DriverVersion, DriverDate, " +
            "InfName, IsSigned, Signer FROM Win32_PnPSignedDriver",
            item => drivers.Add(new DriverInfo(
                ReadString(item["DeviceName"]) ?? "Dispositivo sem nome informado",
                ReadString(item["DeviceID"]),
                ReadString(item["DriverProviderName"]),
                ReadString(item["DriverVersion"]),
                ReadString(item["DriverDate"]),
                ReadString(item["InfName"]),
                ReadNullableBoolean(item["IsSigned"]),
                ReadString(item["Signer"]),
                ProblemCode: null)),
            "Não foi possível consultar os metadados de driver reportados pelo Windows.",
            warnings,
            cancellationToken);

        if (!driversRead)
        {
            return new DriverInventoryResult(
                DateTimeOffset.UtcNow,
                InventoryAvailability.Failed,
                Array.Empty<DriverInfo>(),
                warnings,
                warnings[0]);
        }

        var driverIndexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < drivers.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(drivers[index].DeviceId))
                driverIndexById.TryAdd(drivers[index].DeviceId!, index);
        }

        var devicesRead = TryRead(
            "SELECT PNPDeviceID, Name, ConfigManagerErrorCode, HardwareID, CompatibleID FROM Win32_PnPEntity",
            item =>
            {
                var id = ReadString(item["PNPDeviceID"]);
                var name = ReadString(item["Name"])?.Trim();
                var code = ReadNullableInt32(item["ConfigManagerErrorCode"]);
                var hardwareIds = ReadStringList(item["HardwareID"]);
                var compatibleIds = ReadStringList(item["CompatibleID"]);
                if (string.IsNullOrWhiteSpace(id)) return;

                if (driverIndexById.TryGetValue(id, out var existingIndex))
                {
                    drivers[existingIndex] = drivers[existingIndex] with
                    {
                        ProblemCode = code,
                        HardwareIds = hardwareIds,
                        CompatibleIds = compatibleIds
                    };
                    return;
                }

                if (string.IsNullOrWhiteSpace(name)) name = "Dispositivo sem nome informado";
                driverIndexById[id] = drivers.Count;
                drivers.Add(new DriverInfo(
                    name,
                    id,
                    Provider: null,
                    Version: null,
                    DriverDateRaw: null,
                    InfName: null,
                    IsSigned: null,
                    Signer: null,
                    ProblemCode: code,
                    DriverMetadataAvailable: false,
                    HardwareIds: hardwareIds,
                    CompatibleIds: compatibleIds));
            },
            "Não foi possível consultar todos os dispositivos Plug and Play ou seus códigos informados pelo Windows.",
            warnings,
            cancellationToken);

        var availability = devicesRead ? InventoryAvailability.Available : InventoryAvailability.Partial;
        return new DriverInventoryResult(
            DateTimeOffset.UtcNow,
            availability,
            drivers,
            warnings,
            ErrorMessage: null);
    }

    private static bool TryRead(
        string query,
        Action<ManagementObject> readItem,
        string failureMessage,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    readItem(item);
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            warnings.Add($"{failureMessage} ({exception.GetType().Name})");
            return false;
        }
    }

    private static string? ReadString(object? value) =>
        value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> ReadStringList(object? value)
    {
        if (value is not Array array) return Array.Empty<string>();
        return array.Cast<object?>()
            .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static bool? ReadNullableBoolean(object? value)
    {
        try
        {
            return value is null ? null : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static int? ReadNullableInt32(object? value)
    {
        try
        {
            return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
