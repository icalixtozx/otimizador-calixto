namespace Optimizer.Core.Inventory;

public enum DriverPackageMatchKind
{
    NotFound,
    ExactHardwareId,
    CompatibleIdOnly,
    Ambiguous
}

/// <summary>
/// A fixed, reviewed ASUS package record. It describes one archive examined
/// on the review host; it does not mean the package is installed or approved.
/// </summary>
public sealed record DriverPackageCandidate(
    string Id,
    string DeviceLabel,
    string Chipset,
    IReadOnlyList<string> MatchHardwareIds,
    IReadOnlyList<string> MatchCompatibleIds,
    string PackageTitle,
    string Version,
    string PublishedDate,
    string TargetOperatingSystems,
    string InfArchitectureSection,
    string InfName,
    string CatalogName,
    string InfMatchId,
    string OfficialSupportUrl,
    string DownloadUrl,
    string? PublishedArchiveSha256,
    string ReviewedArchiveSha256,
    string ArchiveIntegritySummary,
    string CatalogSignatureSummary,
    string InfMembershipSummary,
    string OsCompatibilitySummary,
    string ReviewEnvironment,
    string PendingItems);

public sealed record DriverPackageAssessment(
    DriverPackageCandidate Candidate,
    DriverInfo? InstalledDriver,
    DriverPackageMatchKind MatchKind,
    string MatchStatus)
{
    public string InstalledPackageSummary => InstalledDriver is null
        ? "Dispositivo não encontrado no inventário local."
        : $"{InstalledDriver.Provider ?? "Fornecedor não informado"} · versão {InstalledDriver.Version ?? "não informada"} · INF {InstalledDriver.InfName ?? "não informado"} · {InstalledDriver.DriverDetailsStatus}";

    public string CandidateStatus => MatchKind switch
    {
        DriverPackageMatchKind.ExactHardwareId => "Candidato encontrado para um Hardware ID exato.",
        DriverPackageMatchKind.CompatibleIdOnly => "Correspondência somente por Compatible ID; instalação bloqueada para revisão.",
        DriverPackageMatchKind.Ambiguous => "Correspondência ambígua; nenhuma instalação pode ser considerada.",
        _ => "Sem correspondência para este dispositivo no inventário local."
    };

    public bool IsCandidateFound => MatchKind is DriverPackageMatchKind.ExactHardwareId or DriverPackageMatchKind.CompatibleIdOnly;
}

public sealed record DriverPackageImportResult(
    string FileName,
    string Sha256,
    DriverPackageCandidate? RecognizedCandidate)
{
    public string Status => RecognizedCandidate is null
        ? "ZIP local lido e hash calculado. Não corresponde a um dos arquivos ASUS previamente revisados; origem, assinatura, INF e compatibilidade continuam sem validação."
        : $"O hash corresponde ao arquivo revisado: {RecognizedCandidate.PackageTitle}. Isso reconhece os mesmos bytes; não instala nem aprova o driver.";
}

public static class DriverPackageCandidateCatalog
{
    private const string AsusSupport = "https://www.asus.com/us/motherboards-components/motherboards/prime/prime-b550m-a-ac/helpdesk_download?model2Name=PRIME-B550M-A-AC";

    private static readonly DriverPackageCandidate[] Candidates =
    [
        new(
            "asus-rtl8111h-lan-1168.27.50.919",
            "Ethernet Realtek PCIe GbE",
            "RTL8111H / RTL8168H",
            ["PCI\\VEN_10EC&DEV_8168&SUBSYS_86771043&REV_15"],
            ["PCI\\VEN_10EC&DEV_8168&REV_15"],
            "Realtek RTL8111H LAN driver",
            "1168.27.50.919",
            "31/12/2025",
            "Windows 11 64-bit",
            "NTamd64.10.0...22000",
            "rt68cx21x64.inf",
            "rt68cx21x64.cat",
            "PCI\\VEN_10EC&DEV_8168&SUBSYS_86771043&REV_15",
            AsusSupport,
            "https://dlcdnets.asus.com/pub/ASUS/mb/04LAN/DRV_LAN_Realtek_8111_SZ-TSD_W11_64_V11682750919_20251230R.zip",
            "3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D",
            "3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D",
            "722 entradas descompactadas; CRC32 e comprimentos conferem com o diretório ZIP.",
            "Catálogo válido no Windows; signatário Microsoft Windows Hardware Compatibility Publisher.",
            "rt68cx21x64.inf validado como membro de rt68cx21x64.cat por WinVerifyTrust/DRIVER_ACTION_VERIFY.",
            "INF x64 com decoração mínima Windows 10.0 build 22000; o host de revisão executa Windows 11 x64 build 26300.9711.",
            "Revisado em 20/10/2026 neste PC: Windows 11 x64 build 26300.9711.",
            "A aplicação continua indisponível. A correspondência exata e as assinaturas do pacote não provam que trocar o driver seja necessário ou melhore desempenho. Não houve instalação nem teste funcional do pacote."),
        new(
            "asus-rtl8821ce-wifi-2024.10.138.3",
            "Wi-Fi Realtek 8821CE",
            "RTL8821CE",
            ["PCI\\VEN_10EC&DEV_C821&SUBSYS_304A1A3B"],
            ["PCI\\VEN_10EC&DEV_C821"],
            "Realtek RTL8821CE WiFi driver",
            "2024.10.138.3",
            "04/10/2022",
            "Windows 10/11 64-bit",
            "NTamd64.10.0",
            "netrtwlane.inf",
            "netrtwlane.cat",
            "PCI\\VEN_10EC&DEV_C821&SUBSYS_304A1A3B",
            AsusSupport,
            "https://dlcdnets.asus.com/pub/ASUS/mb/08WIRELESS/DRV_WiFi_RTK_8821CE_SZ-TSD_W11_64_V2024101383_20220929R.zip",
            null,
            "E061EBD3E77F54DA07ECED9876DB54DCAA46E871DB4015FEE6EC1F2987535647",
            "37 entradas descompactadas; CRC32 e comprimentos conferem com o diretório ZIP.",
            "Catálogo válido no Windows; signatário Microsoft Windows Hardware Compatibility Publisher.",
            "netrtwlane.inf validado como membro de netrtwlane.cat por WinVerifyTrust/DRIVER_ACTION_VERIFY.",
            "INF x64 com decoração NTamd64.10.0; ASUS lista Windows 10/11 64-bit. O host de revisão é Windows 11 x64 build 26300.9711.",
            "Revisado em 20/10/2026 neste PC: Windows 11 x64 build 26300.9711.",
            "A ASUS não publicou SHA-256 para este arquivo; o digest local serve para reconhecer uma cópia idêntica, não para provar origem. A aplicação continua indisponível e não houve instalação nem teste funcional."),
    ];

    public static IReadOnlyList<DriverPackageCandidate> All => Candidates;

    public static IReadOnlyList<DriverPackageAssessment> Assess(IEnumerable<DriverInfo> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        var inventory = drivers.ToArray();
        return Candidates.Select(candidate => Assess(candidate, inventory)).ToArray();
    }

    public static DriverPackageImportResult IdentifyArchive(string fileName, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        var normalized = sha256.Trim().ToUpperInvariant();
        if (normalized.Length != 64 || !normalized.All(Uri.IsHexDigit))
            throw new ArgumentException("O SHA-256 deve conter exatamente 64 dígitos hexadecimais.", nameof(sha256));
        var candidate = Candidates.SingleOrDefault(item => string.Equals(item.ReviewedArchiveSha256, normalized, StringComparison.OrdinalIgnoreCase));
        return new DriverPackageImportResult(Path.GetFileName(fileName), normalized, candidate);
    }

    private static DriverPackageAssessment Assess(DriverPackageCandidate candidate, IReadOnlyList<DriverInfo> inventory)
    {
        var exactMatches = inventory.Where(driver => Intersects(driver.HardwareIds, candidate.MatchHardwareIds)).ToArray();
        if (exactMatches.Length == 1)
            return new DriverPackageAssessment(candidate, exactMatches[0], DriverPackageMatchKind.ExactHardwareId, "Hardware ID exato presente no INF revisado.");
        if (exactMatches.Length > 1)
            return new DriverPackageAssessment(candidate, null, DriverPackageMatchKind.Ambiguous, "Mais de um dispositivo contém o Hardware ID selecionado.");

        var compatibleMatches = inventory.Where(driver => Intersects(driver.CompatibleIds, candidate.MatchCompatibleIds)).ToArray();
        if (compatibleMatches.Length == 1)
            return new DriverPackageAssessment(candidate, compatibleMatches[0], DriverPackageMatchKind.CompatibleIdOnly, "Somente um Compatible ID genérico coincide; requer revisão adicional.");
        if (compatibleMatches.Length > 1)
            return new DriverPackageAssessment(candidate, null, DriverPackageMatchKind.Ambiguous, "Mais de um dispositivo contém os Compatible IDs selecionados.");

        return new DriverPackageAssessment(candidate, null, DriverPackageMatchKind.NotFound, "Nenhum ID do inventário coincide com os IDs revisados para este INF.");
    }

    private static bool Intersects(IReadOnlyList<string>? reported, IReadOnlyList<string> expected) =>
        reported is not null && reported.Any(actual => expected.Contains(actual, StringComparer.OrdinalIgnoreCase));
}
