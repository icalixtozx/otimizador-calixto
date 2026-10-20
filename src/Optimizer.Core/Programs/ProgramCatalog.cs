namespace Optimizer.Core.Programs;

public sealed record ProgramPackageDefinition(
    string PackageId,
    string DisplayName,
    string Publisher,
    string SourceName,
    string VerifiedManifestVersion,
    DateOnly ManifestReviewedOn,
    string ManifestUrl,
    string LicenseName,
    string LicenseUrl,
    string DeclaredInstallScope,
    string ElevationNote,
    string MinimumWindowsVersion,
    string RestartNote,
    IReadOnlyList<string> ExactDisplayNames);

public sealed class ProgramCatalog
{
    private static readonly string[] AllowedPackageIds = ["7zip.7zip", "VideoLAN.VLC"];

    public ProgramCatalog(IEnumerable<ProgramPackageDefinition> packages)
    {
        var materialized = packages.ToArray();
        if (materialized.Length == 0 || materialized.Select(package => package.PackageId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != materialized.Length)
        {
            throw new ArgumentException("The program catalog must contain unique package IDs.", nameof(packages));
        }

        if (materialized.Any(package => !AllowedPackageIds.Contains(package.PackageId, StringComparer.OrdinalIgnoreCase)
            || !string.Equals(package.SourceName, "winget", StringComparison.Ordinal)
            || package.ExactDisplayNames.Count == 0))
        {
            throw new ArgumentException("Only the reviewed exact WinGet package catalog can be used.", nameof(packages));
        }

        Packages = Array.AsReadOnly(materialized);
    }

    public IReadOnlyList<ProgramPackageDefinition> Packages { get; }

    public bool TryGet(string packageId, out ProgramPackageDefinition package)
    {
        package = Packages.FirstOrDefault(candidate => string.Equals(candidate.PackageId, packageId, StringComparison.OrdinalIgnoreCase))!;
        return package is not null;
    }

    public static ProgramCatalog CreateDefault() => new(
    [
        new ProgramPackageDefinition(
            "7zip.7zip", "7-Zip", "Igor Pavlov", "winget", "26.04", new DateOnly(2026, 10, 20),
            "https://github.com/microsoft/winget-pkgs/tree/master/manifests/7/7zip/7zip/26.04",
            "LGPL-2.1", "https://7-zip.org/license.txt", "machine",
            "O manifesto revisado declara que o instalador solicita elevação própria; a política/UAC pode pedir credenciais.",
            "Windows Desktop conforme o manifesto aplicável; a compatibilidade precisa ser confirmada novamente pelo WinGet no momento da operação.",
            "O instalador/WinGet pode informar que é necessário reiniciar; nenhum reinício automático é autorizado.",
            ["7-Zip", "7-Zip 26.04", "7-Zip 26.04 (x64)"]),
        new ProgramPackageDefinition(
            "VideoLAN.VLC", "VLC media player", "VideoLAN", "winget", "3.0.24", new DateOnly(2026, 10, 20),
            "https://github.com/microsoft/winget-pkgs/tree/master/manifests/v/VideoLAN/VLC/3.0.24",
            "GPL-2.0", "https://code.videolan.org/videolan/vlc/-/blob/HEAD/COPYING", "machine",
            "Os instaladores x64 do manifesto revisado declaram elevação própria; o caminho concreto pode pedir UAC.",
            "Windows Desktop; o manifesto revisado declara MinimumOSVersion 6.1.7600.0. O WinGet ainda precisa escolher um instalador aplicável ao sistema.",
            "O instalador/WinGet pode informar que é necessário reiniciar; nenhum reinício automático é autorizado.",
            ["VLC media player", "VLC media player (VideoLAN)"])
    ]);
}

public enum InstalledProgramStatus
{
    IdentifiedInstalled,
    NotIdentified,
    Ambiguous,
    CheckIncomplete
}

public sealed record InstalledProgramEntry(string DisplayName, string Publisher, string? Version, string Scope);

public sealed record InstalledProgramInventory(IReadOnlyList<InstalledProgramEntry> Entries, IReadOnlyList<string> Warnings);

public interface IInstalledProgramInventoryProvider
{
    InstalledProgramInventory ReadInstalledPrograms();
}

public sealed record InstalledProgramMatch(
    InstalledProgramStatus Status,
    string Label,
    string? Version,
    string? Scope,
    IReadOnlyList<string> Reasons);

public static class InstalledProgramMatcher
{
    public static InstalledProgramMatch Match(ProgramPackageDefinition package, InstalledProgramInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(inventory);

        var matches = inventory.Entries
            .Where(entry => package.ExactDisplayNames.Contains(entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                && string.Equals(NormalizePublisher(package.Publisher), NormalizePublisher(entry.Publisher), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length > 1)
        {
            return new(InstalledProgramStatus.Ambiguous, "Possível instalação duplicada/ambígua", null, null,
                ["Mais de uma entrada exata de nome e publicador foi encontrada; não foi escolhida uma versão automaticamente."]);
        }

        if (matches.Length == 1 && inventory.Warnings.Count > 0)
        {
            return new(InstalledProgramStatus.CheckIncomplete, "Verificação incompleta", matches[0].Version, matches[0].Scope,
                ["Uma entrada exata foi encontrada, mas falhas de leitura podem ocultar uma segunda instalação; a identidade não foi considerada definitiva."]);
        }

        if (matches.Length == 1)
        {
            var entry = matches[0];
            return new(InstalledProgramStatus.IdentifiedInstalled, "Instalação identificada", entry.Version, entry.Scope,
                ["Correspondência exata de DisplayName e Publisher na lista local de desinstalação; não comprova origem WinGet nem integridade do programa."]);
        }

        return inventory.Warnings.Count > 0
            ? new(InstalledProgramStatus.CheckIncomplete, "Verificação incompleta", null, null,
                ["Nenhuma correspondência exata foi encontrada e houve falha parcial na leitura local; ausência não equivale a não instalado."])
            : new(InstalledProgramStatus.NotIdentified, "Não identificado localmente", null, null,
                ["Nenhuma correspondência exata foi encontrada nas entradas locais consultadas. O programa ainda pode estar instalado sem uma entrada identificável."]);
    }

    private static string NormalizePublisher(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
