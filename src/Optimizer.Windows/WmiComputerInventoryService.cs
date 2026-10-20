using System.Globalization;
using System.Management;
using Microsoft.Win32;
using Optimizer.Core.Inventory;

namespace Optimizer.Windows;

public sealed class WmiComputerInventoryService : IComputerInventoryService
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public Task<ComputerInventoryResult> CollectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Collect(cancellationToken), cancellationToken);

    private static ComputerInventoryResult Collect(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var windows = ReadWindowsIdentity(warnings, cancellationToken);

        var processors = new List<string>();
        TryRead("SELECT Name FROM Win32_Processor", item =>
        {
            var name = ReadString(item["Name"]);
            if (!string.IsNullOrWhiteSpace(name)) processors.Add(name.Trim());
        }, "O processador não pôde ser identificado.", warnings, cancellationToken);

        var graphics = new List<string>();
        TryRead("SELECT Name FROM Win32_VideoController", item =>
        {
            var name = ReadString(item["Name"]);
            if (!string.IsNullOrWhiteSpace(name)) graphics.Add(name.Trim());
        }, "As placas de vídeo não puderam ser identificadas.", warnings, cancellationToken);

        string? memory = null;
        TryRead("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", item =>
        {
            var bytes = ReadNullableUInt64(item["TotalPhysicalMemory"]);
            if (bytes is not null) memory = $"{bytes.Value / 1024d / 1024d / 1024d:0.#} GiB";
        }, "A memória instalada não pôde ser identificada.", warnings, cancellationToken);

        var boards = new List<string>();
        TryRead("SELECT Manufacturer, Product FROM Win32_BaseBoard", item =>
        {
            var manufacturer = ReadString(item["Manufacturer"]);
            var product = ReadString(item["Product"]);
            var value = string.Join(" ", new[] { manufacturer, product }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            if (!string.IsNullOrWhiteSpace(value)) boards.Add(value.Trim());
        }, "A placa-mãe não pôde ser identificada.", warnings, cancellationToken);

        var storage = new List<string>();
        TryRead("SELECT Model, Size FROM Win32_DiskDrive", item =>
        {
            var model = ReadString(item["Model"]);
            var sizeBytes = ReadNullableUInt64(item["Size"]);
            if (!string.IsNullOrWhiteSpace(model))
            {
                var capacity = sizeBytes is null ? "" : $" · {sizeBytes.Value / 1024d / 1024d / 1024d:0.#} GiB";
                storage.Add($"{model.Trim()}{capacity}");
            }
        }, "Os dispositivos de armazenamento não puderam ser identificados.", warnings, cancellationToken);

        var network = new List<string>();
        TryRead("SELECT Name, Manufacturer FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True", item =>
        {
            var name = ReadString(item["Name"]);
            if (string.IsNullOrWhiteSpace(name)) return;
            var manufacturer = ReadString(item["Manufacturer"]);
            network.Add(string.IsNullOrWhiteSpace(manufacturer)
                ? name.Trim()
                : $"{name.Trim()} · {manufacturer.Trim()}");
        }, "Os adaptadores de rede não puderam ser identificados.", warnings, cancellationToken);

        if (windows.Name is null && processors.Count == 0 && graphics.Count == 0 &&
            memory is null && boards.Count == 0 && storage.Count == 0 && network.Count == 0)
        {
            return new ComputerInventoryResult(null, "O Windows não retornou dados de inventário.");
        }

        var inventory = new ComputerInventory(
            DateTimeOffset.UtcNow,
            windows.Name,
            windows.Edition,
            windows.Version,
            windows.Build,
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            JoinOrNull(processors),
            graphics,
            memory,
            JoinOrNull(boards),
            storage,
            network,
            warnings);

        return new ComputerInventoryResult(inventory, ErrorMessage: null);
    }

    private static ResolvedWindowsIdentity ReadWindowsIdentity(
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        string? caption = null;
        string? cimVersion = null;
        int? cimBuild = null;
        int? productType = null;
        int? operatingSystemSku = null;
        TryRead("SELECT Caption, Version, BuildNumber, ProductType, OperatingSystemSKU FROM Win32_OperatingSystem", item =>
        {
            caption = ReadString(item["Caption"]);
            cimVersion = ReadString(item["Version"]);
            cimBuild = ReadNullableInt32(item["BuildNumber"]);
            productType = ReadNullableInt32(item["ProductType"]);
            operatingSystemSku = ReadNullableInt32(item["OperatingSystemSKU"]);
        }, "A versão e a build do Windows não puderam ser lidas pelo CIM.", warnings, cancellationToken);

        string? productName = null;
        string? edition = null;
        string? displayVersion = null;
        int? registryBuild = null;
        int? ubr = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
            if (key is null)
            {
                warnings.Add("A versão do Windows não pôde ser lida no Registro.");
            }
            else
            {
                productName = ReadRegistryString(key, "ProductName");
                edition = ReadRegistryString(key, "EditionID");
                displayVersion = ReadRegistryString(key, "DisplayVersion");
                registryBuild = ReadNullableInt32(key.GetValue("CurrentBuildNumber") ?? key.GetValue("CurrentBuild"));
                ubr = ReadNullableInt32(key.GetValue("UBR"));
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($"Os metadados complementares do Windows não puderam ser lidos no Registro ({exception.GetType().Name}).");
        }

        var identity = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
            caption,
            cimVersion,
            cimBuild,
            productType,
            operatingSystemSku,
            productName,
            edition,
            displayVersion,
            registryBuild,
            ubr));
        foreach (var warning in identity.Warnings) warnings.Add(warning);
        return identity;
    }

    private static string? ReadRegistryString(RegistryKey key, string name) =>
        key.GetValue(name) is { } value ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

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

    private static ulong? ReadNullableUInt64(object? value)
    {
        try
        {
            return value is null ? null : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
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

    private static string? JoinOrNull(IEnumerable<string> values)
    {
        var items = values.Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        return items.Length == 0 ? null : string.Join(" / ", items);
    }
}
