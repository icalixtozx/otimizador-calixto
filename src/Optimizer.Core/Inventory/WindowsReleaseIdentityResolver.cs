using System.Globalization;
using System.Text.RegularExpressions;

namespace Optimizer.Core.Inventory;

public sealed record WindowsIdentitySources(
    string? CimCaption,
    string? CimVersion,
    int? CimBuildNumber,
    int? CimProductType,
    int? CimOperatingSystemSku,
    string? RegistryProductName,
    string? RegistryEditionId,
    string? RegistryDisplayVersion,
    int? RegistryBuildNumber,
    int? RegistryUbr);

public sealed record ResolvedWindowsIdentity(
    string? Name,
    string? Edition,
    string? Version,
    string? Build,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Resolves the client release name from documented CIM and version/build evidence.
/// ProductName in the CurrentVersion registry key is comparison-only because it can
/// remain stale after a feature update; by itself it is never treated as confirmation.
/// </summary>
public static partial class WindowsReleaseIdentityResolver
{
    private const int Windows11MinimumBuild = 22000;
    private const int WorkstationProductType = 1;

    [GeneratedRegex(@"\bWindows\s+(?<release>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsReleasePattern();

    public static ResolvedWindowsIdentity Resolve(WindowsIdentitySources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var warnings = new List<string>();
        var cimBuild = sources.CimBuildNumber;
        var registryBuild = sources.RegistryBuildNumber;

        if (cimBuild is not null && registryBuild is not null && cimBuild != registryBuild)
        {
            warnings.Add($"A build CIM ({cimBuild.Value}) diverge de CurrentBuildNumber no Registro ({registryBuild.Value}); foi usado o valor CIM do Windows em execução.");
        }

        var build = cimBuild ?? registryBuild;
        var buildSuffixIsCurrent = registryBuild is not null && build == registryBuild;
        var buildText = build is null
            ? null
            : buildSuffixIsCurrent && sources.RegistryUbr is int ubr
                ? $"{build.Value.ToString(CultureInfo.InvariantCulture)}.{ubr.ToString(CultureInfo.InvariantCulture)}"
                : build.Value.ToString(CultureInfo.InvariantCulture);

        var version = FirstNonEmpty(sources.RegistryDisplayVersion, sources.CimVersion);
        var registryEdition = FirstNonEmpty(sources.RegistryEditionId);
        var cimSkuEdition = EditionIdForSku(sources.CimOperatingSystemSku);
        var captionEdition = FindCaptionEdition(sources.CimCaption);
        var isNt10 = Version.TryParse(sources.CimVersion, out var parsedVersion) &&
                     parsedVersion.Major == 10 && parsedVersion.Minor == 0;
        var captionRelease = FindWindowsRelease(sources.CimCaption);
        var registryRelease = FindWindowsRelease(sources.RegistryProductName);
        int? buildRelease = isNt10 && build is int knownBuild
            ? knownBuild >= Windows11MinimumBuild ? 11 : 10
            : null;

        string? name;
        var captionBuildConflict = captionRelease is not null && buildRelease is not null && captionRelease != buildRelease;
        var captionPlatformConflict = captionRelease is 10 or 11 &&
                                      ((parsedVersion is not null && !isNt10) ||
                                       (sources.CimProductType is int productType && productType != WorkstationProductType));
        var identityConflict = captionBuildConflict || captionPlatformConflict;
        var knownEditions = new[]
        {
            NormalizeEdition(registryEdition),
            NormalizeEdition(cimSkuEdition),
            NormalizeEdition(captionEdition)
        }.Where(edition => edition is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var editionConflict = knownEditions.Length > 1;
        if (editionConflict)
        {
            var captionEditionText = captionEdition is null ? "indisponível/não reconhecida" : $"'{captionEdition}'";
            var skuText = cimSkuEdition is null
                ? $"SKU CIM {sources.CimOperatingSystemSku?.ToString(CultureInfo.InvariantCulture) ?? "indisponível/não mapeado"}"
                : $"SKU CIM {sources.CimOperatingSystemSku} ('{cimSkuEdition}')";
            var registryEditionText = registryEdition is null ? "indisponível" : $"'{registryEdition}'";
            warnings.Add($"A edição diverge entre Caption CIM ({captionEditionText}), {skuText} e EditionID ({registryEditionText}); a edição foi deixada sem confirmação.");
        }
        else if (sources.CimOperatingSystemSku is int sku && cimSkuEdition is null)
        {
            warnings.Add($"OperatingSystemSKU CIM {sku.ToString(CultureInfo.InvariantCulture)} não está mapeado neste app; a edição não foi confirmada por SKU.");
        }
        if (identityConflict)
        {
            name = "Windows (identificação divergente)";
            warnings.Add(captionBuildConflict
                ? $"Caption CIM identifica Windows {captionRelease}, mas a versão CIM {sources.CimVersion} e a build {build} apontam para Windows {buildRelease} pela regra documentada de build 22000. A identificação foi marcada como divergente."
                : $"Caption CIM identifica Windows {captionRelease}, mas versão CIM ({sources.CimVersion ?? "indisponível"}) ou tipo de produto ({sources.CimProductType?.ToString(CultureInfo.InvariantCulture) ?? "indisponível"}) não correspondem a um cliente Windows 10/11. A identificação foi marcada como divergente.");
        }
        else if (buildRelease is int inferredRelease && sources.CimProductType == WorkstationProductType)
        {
            name = $"Windows {inferredRelease.ToString(CultureInfo.InvariantCulture)}";
            if (captionRelease is null)
            {
                warnings.Add("O nome do produto não foi reconhecido no Caption CIM; Windows 10/11 foi inferido pela versão 10.0, build e tipo de produto cliente.");
            }
        }
        else if (captionRelease is int captionedRelease)
        {
            name = $"Windows {captionedRelease.ToString(CultureInfo.InvariantCulture)}";
            if (build is null)
            {
                warnings.Add("O nome do Windows veio do Caption CIM, mas não havia build para confirmar a classificação.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(sources.CimCaption))
        {
            name = NormalizeCaption(sources.CimCaption);
        }
        else if (!string.IsNullOrWhiteSpace(sources.RegistryProductName))
        {
            name = "Windows (identificação não confirmada)";
            warnings.Add($"A única fonte de nome disponível é ProductName no Registro ('{sources.RegistryProductName.Trim()}'); ela pode estar desatualizada e não foi apresentada como identificação confirmada.");
        }
        else
        {
            name = null;
        }

        var resolvedRelease = FindWindowsRelease(name);
        if (!identityConflict && resolvedRelease is int currentRelease &&
            registryRelease is int registeredRelease && currentRelease != registeredRelease)
        {
            warnings.Add($"ProductName no Registro informa Windows {registeredRelease}, enquanto Caption CIM/build identificam Windows {currentRelease}; foi usado CIM com versão/build porque ProductName pode permanecer desatualizado após uma atualização.");
        }

        if (resolvedRelease == 11)
        {
            warnings.Add("O inventário não confirma a inscrição nem o canal do Windows Insider. Consulte Configurações > Windows Update > Programa Windows Insider para verificar a configuração do dispositivo.");
        }

        return new ResolvedWindowsIdentity(
            name,
            editionConflict ? null : cimSkuEdition ?? registryEdition ?? captionEdition,
            version,
            buildText,
            warnings);
    }

    private static int? FindWindowsRelease(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = WindowsReleasePattern().Match(value);
        return match.Success && int.TryParse(match.Groups["release"].Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var release)
            ? release
            : null;
    }

    private static string? EditionIdForSku(int? sku) => sku switch
    {
        4 => "Enterprise",
        7 => "ServerStandard",
        48 => "Professional",
        49 => "ProfessionalN",
        101 => "Core",
        _ => null
    };

    private static string? FindCaptionEdition(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption)) return null;
        var match = WindowsReleasePattern().Match(caption);
        if (!match.Success) return null;
        var edition = caption[(match.Index + match.Length)..].Trim();
        return string.IsNullOrWhiteSpace(edition) ? null : edition;
    }

    private static string? NormalizeEdition(string? edition) => edition?.Trim().ToUpperInvariant() switch
    {
        "PROFESSIONAL" or "PRO" => "professional",
        "PROFESSIONALN" or "PRO N" => "professional-n",
        "PROFESSIONALWORKSTATION" or "PRO FOR WORKSTATIONS" => "professional-workstation",
        "ENTERPRISE" => "enterprise",
        "ENTERPRISEN" or "ENTERPRISE N" => "enterprise-n",
        "EDUCATION" => "education",
        "CORE" or "HOME" => "home",
        "COREN" or "HOME N" => "home-n",
        "CORESINGLELANGUAGE" or "HOME SINGLE LANGUAGE" => "home-single-language",
        "SERVERSTANDARD" or "STANDARD SERVER" => "server-standard",
        _ => null
    };

    private static string NormalizeCaption(string value)
    {
        var caption = value.Trim();
        return caption.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase)
            ? caption["Microsoft ".Length..]
            : caption;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
