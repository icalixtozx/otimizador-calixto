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
            "Não foi possível consultar os drivers assinados do Windows.",
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

        var problemCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var devicesRead = TryRead(
            "SELECT PNPDeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity",
            item =>
            {
                var id = ReadString(item["PNPDeviceID"]);
                var code = ReadNullableInt32(item["ConfigManagerErrorCode"]);
                if (!string.IsNullOrWhiteSpace(id) && code is not null)
                {
                    problemCodes[id] = code.Value;
                }
            },
            "Não foi possível verificar os códigos de problema dos dispositivos Plug and Play.",
            warnings,
            cancellationToken);

        if (problemCodes.Count > 0)
        {
            for (var index = 0; index < drivers.Count; index++)
            {
                var driver = drivers[index];
                if (driver.DeviceId is not null && problemCodes.TryGetValue(driver.DeviceId, out var code))
                {
                    drivers[index] = driver with { ProblemCode = code };
                }
            }
        }

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
