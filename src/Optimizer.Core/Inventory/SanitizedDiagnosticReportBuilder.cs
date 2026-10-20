using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Optimizer.Core.Inventory;

public sealed record DiagnosticReportPrivacyContext(
    string? ComputerName,
    string? AccountName,
    string? AccountDomain = null,
    IReadOnlyList<string>? AdditionalSensitiveValues = null);

/// <summary>Builds a local, reviewable report. Hardware model names may still identify a device.</summary>
public static partial class SanitizedDiagnosticReportBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly Regex[] SensitivePatterns =
    [
        new(@"(?i)\b(?:PCI|USB|HID|ACPI|ROOT|SWD|BTH|DISPLAY|SCSI|I2C|SERENUM|STORAGE|WPD)\\[A-Z0-9_&\\.-]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        new(@"(?i)\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        new(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        new(@"(?i)(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        new(@"(?i)\b(?:serial(?:\s+(?:number|no\.?))?|s\s*/\s*n)\s*[:=#-]?\s*[A-Z0-9][A-Z0-9-]{2,}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        new(@"(?i)(?:[A-Z]:\\|\\\\[^\\\s]+\\[^\\\s]+\\)[^\r\n\t\""<>|]*", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
    ];

    private static readonly Regex PossibleAddressToken = new(
        @"(?i)(?<![A-Z0-9:.])[A-F0-9:.%_-]{2,}(?![A-Z0-9:.%_-])",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly string[] SystemSources =
    [
        "CIM/WMI Win32_OperatingSystem para Caption, versão, build, tipo de produto e SKU",
        "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion para ProductName, EditionID, DisplayVersion, build e UBR (comparação somente leitura)",
        "CIM/WMI Win32_Processor, Win32_VideoController, Win32_ComputerSystem, Win32_BaseBoard, Win32_DiskDrive e Win32_NetworkAdapter (PhysicalAdapter=True)"
    ];

    private const string DriverSource =
        "CIM/WMI Win32_PnPSignedDriver e Win32_PnPEntity; PNPDeviceID é usado apenas para correlação local e não é exportado";

    private const string PowerSource =
        "PowrProf.dll: PowerEnumerate, PowerGetActiveScheme, PowerReadFriendlyName, PowerReadACValueIndex e PowerReadDCValueIndex";

    public static string BuildJson(
        ComputerInventory? computer,
        DriverInventoryResult? drivers,
        PowerPlanInventoryResult? powerPlans,
        DateTimeOffset? generatedAtUtc = null,
        DiagnosticReportPrivacyContext? privacyContext = null,
        string? computerErrorMessage = null)
    {
        var privacy = privacyContext ?? new DiagnosticReportPrivacyContext(null, null);
        var report = new
        {
            schemaVersion = 2,
            generatedAtUtc = (generatedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            reviewNotice = "Relatório local para revisão. Nomes de conta/computador, IDs PnP, GUIDs, e-mails, endereços IPv4, números de série e caminhos locais conhecidos são omitidos ou redigidos. Nomes e modelos de hardware permanecem e podem identificar características do equipamento; revise o conteúdo inteiro antes de compartilhar. Resultados parciais, fontes, avisos/divergências e campos desconhecidos são preservados quando disponíveis. O app não envia este arquivo pela rede.",
            excludedTechnicalFields = new[] { "PNPDeviceID", "HardwareID", "CompatibleID", "GUID do plano", "INF publicado localmente", "assinante do catálogo" },
            system = new
            {
                availability = computer is null
                    ? computerErrorMessage is null ? "NotCollected" : "Failed"
                    : computer.Warnings.Count == 0 ? "Available" : "Partial",
                source = SystemSources,
                collectedAtUtc = computer?.CollectedAtUtc,
                error = Sanitize(computer is null ? computerErrorMessage : null, privacy),
                windowsProduct = Sanitize(computer?.WindowsName, privacy),
                windowsEdition = Sanitize(computer?.WindowsEdition, privacy),
                windowsVersion = Sanitize(computer?.WindowsVersion, privacy),
                windowsBuild = Sanitize(computer?.WindowsBuild, privacy),
                architecture = Sanitize(computer?.Architecture, privacy),
                processor = Sanitize(computer?.Processor, privacy),
                graphicsAdapters = Sanitize(computer?.GraphicsAdapters, privacy),
                memory = Sanitize(computer?.Memory, privacy),
                motherboard = Sanitize(computer?.Motherboard, privacy),
                storageDevices = Sanitize(computer?.StorageDevices, privacy),
                networkAdapters = Sanitize(computer?.NetworkAdapters, privacy),
                observations = Sanitize(computer?.Warnings, privacy),
                unknownFields = UnknownSystemFields(computer)
            },
            power = new
            {
                availability = powerPlans?.Availability.ToString() ?? "NotCollected",
                source = PowerSource,
                collectedAtUtc = powerPlans?.CollectedAtUtc,
                error = Sanitize(powerPlans?.ErrorMessage, privacy),
                observations = Sanitize(powerPlans?.Warnings, privacy),
                plans = powerPlans?.Plans.Select(plan => new
                {
                    name = Sanitize(plan.Name, privacy),
                    plan.IsActive,
                    plan.VideoIdleAcSeconds,
                    plan.VideoIdleDcSeconds,
                    unknownFields = UnknownPowerFields(plan)
                }).ToArray() ?? Array.Empty<object>(),
                unknownFields = powerPlans is null ? new[] { "availability", "collectedAtUtc", "plans" } : Array.Empty<string>()
            },
            drivers = new
            {
                availability = drivers?.Availability.ToString() ?? "NotCollected",
                source = DriverSource,
                collectedAtUtc = drivers?.CollectedAtUtc,
                error = Sanitize(drivers?.ErrorMessage, privacy),
                observations = Sanitize(drivers?.Warnings, privacy),
                items = drivers?.Drivers.Select(driver => new
                {
                    deviceName = Sanitize(driver.DeviceName, privacy),
                    provider = Sanitize(driver.Provider, privacy),
                    version = Sanitize(driver.Version, privacy),
                    driverDate = Sanitize(driver.DriverDateRaw, privacy),
                    driver.IsSigned,
                    problemCodeWindowsReported = driver.ProblemCode,
                    metadataStatus = driver.DriverMetadataAvailable ? "Reported" : "Unavailable",
                    unknownFields = UnknownDriverFields(driver)
                }).ToArray() ?? Array.Empty<object>(),
                unknownFields = drivers is null ? new[] { "availability", "collectedAtUtc", "items" } : Array.Empty<string>()
            }
        };

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public static string SanitizeText(string? value, DiagnosticReportPrivacyContext? privacyContext = null) =>
        Sanitize(value, privacyContext ?? new DiagnosticReportPrivacyContext(null, null)) ?? string.Empty;

    private static string? Sanitize(string? value, DiagnosticReportPrivacyContext privacy)
    {
        if (value is null) return null;
        var sanitized = value;
        foreach (var pattern in SensitivePatterns)
            sanitized = pattern.Replace(sanitized, "[removido]");
        sanitized = PossibleAddressToken.Replace(sanitized, match =>
            IPAddress.TryParse(match.Value, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6
                ? "[removido]"
                : match.Value);

        var knownIdentifiers = new[]
                { privacy.ComputerName, privacy.AccountName, privacy.AccountDomain }
            .Concat(privacy.AdditionalSensitiveValues ?? Array.Empty<string>())
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate) && candidate!.Trim().Length >= 3)
            .Select(candidate => candidate!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(candidate => candidate.Length);

        foreach (var identifier in knownIdentifiers)
        {
            var expression = $"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(identifier)}(?![\\p{{L}}\\p{{N}}])";
            sanitized = Regex.Replace(sanitized, expression, "[removido]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }

        return sanitized;
    }

    private static string[]? Sanitize(IReadOnlyList<string>? values, DiagnosticReportPrivacyContext privacy) =>
        values?.Select(value => Sanitize(value, privacy) ?? string.Empty).ToArray();

    private static string[] UnknownSystemFields(ComputerInventory? computer)
    {
        if (computer is null)
            return ["collectedAtUtc", "windowsProduct", "windowsEdition", "windowsVersion", "windowsBuild", "architecture", "processor", "graphicsAdapters", "memory", "motherboard", "storageDevices", "networkAdapters"];

        var unknown = new List<string>();
        AddIfUnknown(unknown, "windowsProduct", computer.WindowsName);
        AddIfUnknown(unknown, "windowsEdition", computer.WindowsEdition);
        AddIfUnknown(unknown, "windowsVersion", computer.WindowsVersion);
        AddIfUnknown(unknown, "windowsBuild", computer.WindowsBuild);
        AddIfUnknown(unknown, "architecture", computer.Architecture);
        AddIfUnknown(unknown, "processor", computer.Processor);
        AddIfUnknown(unknown, "graphicsAdapters", computer.GraphicsAdapters);
        AddIfUnknown(unknown, "memory", computer.Memory);
        AddIfUnknown(unknown, "motherboard", computer.Motherboard);
        AddIfUnknown(unknown, "storageDevices", computer.StorageDevices);
        AddIfUnknown(unknown, "networkAdapters", computer.NetworkAdapters);
        return unknown.ToArray();
    }

    private static string[] UnknownDriverFields(DriverInfo driver)
    {
        var unknown = new List<string>();
        AddIfUnknown(unknown, "deviceName", driver.DeviceName);
        AddIfUnknown(unknown, "provider", driver.Provider);
        AddIfUnknown(unknown, "version", driver.Version);
        AddIfUnknown(unknown, "driverDate", driver.DriverDateRaw);
        AddIfUnknown(unknown, "isSigned", driver.IsSigned);
        AddIfUnknown(unknown, "problemCodeWindowsReported", driver.ProblemCode);
        return unknown.ToArray();
    }

    private static string[] UnknownPowerFields(PowerPlanInfo plan)
    {
        var unknown = new List<string>();
        AddIfUnknown(unknown, "videoIdleAcSeconds", plan.VideoIdleAcSeconds);
        AddIfUnknown(unknown, "videoIdleDcSeconds", plan.VideoIdleDcSeconds);
        return unknown.ToArray();
    }

    private static void AddIfUnknown(ICollection<string> target, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) target.Add(field);
    }

    private static void AddIfUnknown<T>(ICollection<string> target, string field, IReadOnlyCollection<T>? value)
    {
        if (value is null || value.Count == 0) target.Add(field);
    }

    private static void AddIfUnknown<T>(ICollection<string> target, string field, T? value) where T : struct
    {
        if (value is null) target.Add(field);
    }
}
