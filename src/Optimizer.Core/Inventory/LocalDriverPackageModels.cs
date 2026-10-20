using Optimizer.Core.Adjustments;

namespace Optimizer.Core.Inventory;

public enum DriverPackageAnalysisOutcome
{
    CompatibleAccordingToChecks,
    Incompatible,
    Ambiguous,
    Incomplete
}

public enum DriverPackageMatchStrength
{
    ExactHardwareId,
    CompatibleIdOnly
}

public enum DriverPackageVerificationStatus
{
    Valid,
    Invalid,
    Unavailable
}

public sealed record DriverPackageTargetPlatform(
    string Architecture,
    int? OsMajorVersion,
    int? OsMinorVersion,
    int? WindowsBuild,
    int? ProductType = null);

public sealed record DriverPackageSignatureResult(
    DriverPackageVerificationStatus Status,
    string? Signer,
    string Detail);

public interface IDriverPackageSignatureVerifier
{
    DriverPackageSignatureResult VerifyInf(string infPath, string catalogPath);
    DriverPackageSignatureResult VerifyCatalogMember(string filePath, string catalogPath);
}

public enum DriverPackageDependencyStatus
{
    Available,
    Missing,
    Ambiguous,
    Unavailable,
    CycleDetected,
    LimitExceeded
}

public sealed record DriverPackageSystemDependencyResult(
    DriverPackageDependencyStatus Status,
    string Detail);

public sealed record DriverPackageInfDependencyRequirement(
    IReadOnlyList<string> IncludedInfNames,
    IReadOnlyList<string> RequiredSections);

/// <summary>Read-only validation of INF files and sections supplied by the current Windows installation.</summary>
public interface IDriverPackageSystemInfDependencyResolver
{
    DriverPackageSystemDependencyResult Verify(
        IReadOnlyList<DriverPackageInfDependencyRequirement> requirements);
}

public sealed class UnavailableDriverPackageSystemInfDependencyResolver : IDriverPackageSystemInfDependencyResolver
{
    public DriverPackageSystemDependencyResult Verify(
        IReadOnlyList<DriverPackageInfDependencyRequirement> requirements) => new(
            DriverPackageDependencyStatus.Unavailable,
            "O analisador não tem um resolvedor para validar os INF e seções do Windows referenciados pelo pacote.");
}

public sealed record DriverPackageDeviceMatch(
    DriverInfo Device,
    DriverPackageMatchStrength Strength,
    string MatchedId,
    IReadOnlyList<string>? ApplicableInstallSections = null);

public sealed record DriverPackageInfCandidate(
    string InfName,
    string InfPath,
    string? Provider,
    string? DriverVersion,
    string? CatalogName,
    string? CatalogSigner,
    DriverPackageVerificationStatus SignatureStatus,
    DriverPackageAnalysisOutcome Outcome,
    IReadOnlyList<DriverPackageDeviceMatch> DeviceMatches,
    IReadOnlyList<string> RequiredPackageFiles,
    IReadOnlyList<string> Reasons,
    string? SourceSha256 = null,
    string? SourceFileName = null,
    string? InfSha256 = null,
    IReadOnlyDictionary<string, string>? PayloadFilesSha256 = null,
    string? PackageRootPath = null,
    string? DependencyResolutionSummary = null)
{
    public string OutcomeLabel => Outcome switch
    {
        DriverPackageAnalysisOutcome.CompatibleAccordingToChecks => "Compatível segundo as verificações realizadas",
        DriverPackageAnalysisOutcome.Incompatible => "Incompatível com os dispositivos/Windows verificados",
        DriverPackageAnalysisOutcome.Ambiguous => "Correspondência ambígua; instalação bloqueada",
        _ => "Análise incompleta; instalação bloqueada"
    };

    public string SignatureStatusLabel => SignatureStatus switch
    {
        DriverPackageVerificationStatus.Valid => "válida nas APIs do Windows",
        DriverPackageVerificationStatus.Invalid => "inválida/rejeitada",
        _ => "não verificada"
    };

    public string SourceHashLabel => (SourceFileName ?? InfName).EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        ? "SHA-256 do ZIP de origem"
        : "SHA-256 do INF selecionado";

    public bool CanPrepareInstallation => Outcome == DriverPackageAnalysisOutcome.CompatibleAccordingToChecks &&
        SignatureStatus == DriverPackageVerificationStatus.Valid &&
        DeviceMatches.Count == 1 && DeviceMatches[0].Strength == DriverPackageMatchStrength.ExactHardwareId &&
        DeviceMatches[0].ApplicableInstallSections?.Count == 1;

    public string TargetSummary => DeviceMatches.Count == 0
        ? "Nenhum dispositivo do inventário local corresponde."
        : "Correspondências no inventário lido:" + Environment.NewLine + string.Join(Environment.NewLine,
            DeviceMatches.Select(match => $"• {match.Device.DeviceName} · {match.Strength} · {match.Device.DeviceId ?? "ID não informado"} · ID do pacote: {match.MatchedId}")) +
          (DeviceMatches.Count > 1 ? Environment.NewLine + "Há vários alvos; a prévia permanece bloqueada." : string.Empty);

    public string ApplicableInstallSectionsSummary => DeviceMatches.Count == 0
        ? "Nenhuma seção de instalação aplicável identificada."
        : string.Join("; ", DeviceMatches.SelectMany(match => match.ApplicableInstallSections ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    public string ReasonsSummary => Reasons.Count == 0
        ? "As verificações documentadas para este resultado foram concluídas."
        : string.Join(" ", Reasons);
}

public sealed class LocalDriverPackageAnalysisResult : IDisposable
{
    private readonly string? _ownedExtractionDirectory;
    private bool _disposed;

    public LocalDriverPackageAnalysisResult(
        string sourceFileName,
        string sourceSha256,
        long expandedBytes,
        int archiveEntryCount,
        IReadOnlyList<DriverPackageInfCandidate> candidates,
        string integritySummary,
        string? ownedExtractionDirectory)
    {
        SourceFileName = sourceFileName;
        SourceSha256 = sourceSha256;
        ExpandedBytes = expandedBytes;
        ArchiveEntryCount = archiveEntryCount;
        Candidates = candidates;
        IntegritySummary = integritySummary;
        _ownedExtractionDirectory = ownedExtractionDirectory;
    }

    public string SourceFileName { get; }
    public string SourceSha256 { get; }
    public long ExpandedBytes { get; }
    public int ArchiveEntryCount { get; }
    public IReadOnlyList<DriverPackageInfCandidate> Candidates { get; }
    public string IntegritySummary { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownedExtractionDirectory is null) return;

        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CalixtoOptimizer", "DriverAnalysis")) + Path.DirectorySeparatorChar;
        var owned = Path.GetFullPath(_ownedExtractionDirectory);
        if (owned.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
        {
            try { Directory.Delete(owned, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

public sealed record DriverPackageInstallReview(
    DriverPackageInfCandidate Candidate,
    string DeviceInstanceId,
    DriverInfo CurrentDriver,
    AdjustmentPlan Plan,
    DateTimeOffset ReviewedAtUtc,
    string RequiredPermissions,
    string PossibleImpact,
    string RecoveryLimits,
    string InstallationReach);

public enum DriverPackageInstallStepStatus
{
    Blocked,
    RefusedByStateConflict,
    AccessDenied,
    Failed,
    DriverStoreStagedNotAssociated,
    DeviceAssociationPendingRestart,
    DeviceAssociationNotVerified,
    DeviceAssociationVerified
}

public sealed record DriverPackageInstallStepResult(
    DriverPackageInstallStepStatus Status,
    int? ExitCode,
    bool? RebootMayBeRequired,
    string Detail);

public sealed record DriverPackageCommandResult(
    bool Succeeded,
    int? ExitCode,
    bool? RebootMayBeRequired,
    string Detail,
    string? StagedInfPath = null,
    DriverPackageCommandRefusal? Refusal = null);

public enum DriverPackageCommandRefusal
{
    DeviceIdentityChanged,
    DeviceNotPresent,
    CandidateNotBestRank,
    CandidateRankAmbiguous,
    CandidateNotFoundInDriverStore
}

public sealed record DriverPackageAssociationRequest(
    string DeviceInstanceId,
    string ExpectedHardwareId,
    string ExpectedCurrentInfName,
    string StagedInfPath);

public sealed record DriverStoreDriverCandidate(string InfPath, uint Rank);

public enum DriverStoreCandidateSelectionStatus
{
    Selected,
    CandidateNotFound,
    CandidateNotBestRank,
    BestRankAmbiguous
}

public sealed record DriverStoreCandidateSelectionResult(
    DriverStoreCandidateSelectionStatus Status,
    DriverStoreDriverCandidate? Candidate,
    uint? BestRank,
    string Detail);

public static class DriverStoreCandidateSelector
{
    public static DriverStoreCandidateSelectionResult SelectUniqueBest(
        IReadOnlyList<DriverStoreDriverCandidate> compatibleDrivers,
        string requestedInfPath)
    {
        ArgumentNullException.ThrowIfNull(compatibleDrivers);
        if (string.IsNullOrWhiteSpace(requestedInfPath))
            return new(DriverStoreCandidateSelectionStatus.CandidateNotFound, null, null, "O INF staged não foi identificado.");

        var matches = compatibleDrivers.Where(candidate => string.Equals(
            Path.GetFullPath(candidate.InfPath), Path.GetFullPath(requestedInfPath), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            return new(DriverStoreCandidateSelectionStatus.CandidateNotFound, null, null,
                matches.Length == 0 ? "O INF staged não aparece na lista de drivers compatíveis do devnode." : "O INF staged aparece mais de uma vez na lista compatível.");

        if (compatibleDrivers.Count == 0)
            return new(DriverStoreCandidateSelectionStatus.CandidateNotFound, null, null, "Nenhum driver compatível foi enumerado.");
        var bestRank = compatibleDrivers.Min(candidate => candidate.Rank);
        var best = compatibleDrivers.Where(candidate => candidate.Rank == bestRank).ToArray();
        if (matches[0].Rank != bestRank)
            return new(DriverStoreCandidateSelectionStatus.CandidateNotBestRank, null, bestRank,
                $"O candidato solicitado tem rank {matches[0].Rank}, inferior ao melhor rank {bestRank}; a associação não será forçada.");
        if (best.Length != 1)
            return new(DriverStoreCandidateSelectionStatus.BestRankAmbiguous, null, bestRank,
                $"Há {best.Length} candidatos empatados no melhor rank {bestRank}; a escolha foi recusada.");
        return new(DriverStoreCandidateSelectionStatus.Selected, matches[0], bestRank,
            $"O candidato solicitado é o único driver com melhor rank {bestRank} na lista compatível enumerada.");
    }
}

public interface IDriverPackageInstaller
{
    Task<DriverPackageCommandResult> StageToDriverStoreAsync(string infPath, CancellationToken cancellationToken = default);
    Task<DriverPackageCommandResult> AssociateWithDeviceAsync(
        DriverPackageAssociationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDriverPackageMutationPolicy
{
    bool IsEnabled { get; }
    string BlockReason { get; }
}

public sealed class BlockedDriverPackageMutationPolicy(string blockReason) : IDriverPackageMutationPolicy
{
    public bool IsEnabled => false;
    public string BlockReason { get; } = string.IsNullOrWhiteSpace(blockReason)
        ? "A instalação real de drivers está bloqueada nesta compilação."
        : blockReason;
}
