namespace Optimizer.Core.Programs;

public sealed record ProgramInstallConsent(
    string PackageId,
    string SourceName,
    bool ConsequencesReviewed,
    bool SourceAgreementAccepted,
    bool PackageAgreementAccepted)
{
    public bool IsCompleteFor(ProgramPackageDefinition package) =>
        string.Equals(PackageId, package.PackageId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(SourceName, package.SourceName, StringComparison.Ordinal)
        && ConsequencesReviewed
        && SourceAgreementAccepted
        && PackageAgreementAccepted;
}

public sealed record ProgramInstallRequest(string PackageId, ProgramInstallConsent? Consent);

public enum ProgramInstallOutcome
{
    ExecutionBlocked,
    UnknownPackageBlocked,
    ConsentRequired,
    ManagerUnavailable,
    NetworkUnavailable,
    PackageNotFound,
    ReportedSuccessUnverified,
    Failed,
    CancelledOutcomeUnknown,
    CancelledBeforeStart,
    RebootRequired,
    RebootInitiated,
    NotRun,
    NotRunRestartPending
}

public sealed record ProgramInstallResult(
    string PackageId,
    string PackageName,
    ProgramInstallOutcome Outcome,
    string Summary,
    int? ExitCode = null);

public enum ProgramRunnerOutcome
{
    Completed,
    ManagerUnavailable,
    Cancelled,
    Failed
}

public sealed record ProgramRunnerResult(ProgramRunnerOutcome Outcome, int? ExitCode = null);

public interface IProgramInstallRunner
{
    Task<ProgramRunnerResult> InstallAsync(
        ProgramPackageDefinition package,
        ProgramInstallConsent consent,
        CancellationToken cancellationToken);
}

public static class ProgramInstallCommandFactory
{
    public static IReadOnlyList<string> BuildArguments(ProgramCatalog catalog, ProgramInstallConsent consent)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(consent);
        if (!catalog.TryGet(consent.PackageId, out var package) || !consent.IsCompleteFor(package))
        {
            throw new InvalidOperationException("A package-specific review and both agreement acceptances are required.");
        }

        // Keep the command constrained to an exact catalog ID and source. Do not specify scope,
        // installer switches, force/downgrade, hash bypass, silent mode or automatic reboot.
        return Array.AsReadOnly(new[]
        {
            "install", "--id", package.PackageId, "--exact", "--source", package.SourceName,
            "--interactive", "--accept-source-agreements", "--accept-package-agreements"
        });
    }
}

public interface IProgramInstallExecutionPolicy
{
    bool IsAllowed { get; }
    string BlockReason { get; }
}

public sealed class BlockedProgramInstallExecutionPolicy(string blockReason) : IProgramInstallExecutionPolicy
{
    public bool IsAllowed => false;
    public string BlockReason { get; } = string.IsNullOrWhiteSpace(blockReason)
        ? "Instalação real de programas permanece bloqueada nesta versão."
        : blockReason;
}

public sealed class ProgramInstallService(
    ProgramCatalog catalog,
    IProgramInstallRunner runner,
    IProgramInstallExecutionPolicy executionPolicy)
{
    private const int NoPackageFound = unchecked((int)0x8A150014);
    private const int MissingPackage = unchecked((int)0x8A15004D);
    private const int CancelSignalReceived = unchecked((int)0x8A150005);
    private const int InstallerCancelled = unchecked((int)0x8A15010C);
    private const int NoNetwork = unchecked((int)0x8A150107);
    private const int RebootToFinish = unchecked((int)0x8A150109);
    private const int RebootForInstall = unchecked((int)0x8A15010A);
    private const int RebootInitiated = unchecked((int)0x8A15010B);

    public async Task<IReadOnlyList<ProgramInstallResult>> ExecuteAsync(
        IReadOnlyList<ProgramInstallRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var results = new List<ProgramInstallResult>(requests.Count);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pauseForRestart = false;
        var managerUnavailable = false;

        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            if (!catalog.TryGet(request.PackageId, out var package))
            {
                results.Add(new(request.PackageId, request.PackageId, ProgramInstallOutcome.UnknownPackageBlocked,
                    "O ID não pertence ao catálogo local revisado; nenhum comando foi iniciado."));
                continue;
            }

            if (!seenIds.Add(package.PackageId))
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.NotRun,
                    "Este pacote já aparece na seleção; a segunda ocorrência foi ignorada."));
                continue;
            }

            if (pauseForRestart)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.NotRunRestartPending,
                    "Não iniciado porque um resultado anterior requer reinício."));
                continue;
            }

            if (managerUnavailable)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.ManagerUnavailable,
                    "WinGet não pôde ser iniciado; este pacote não foi tentado."));
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.CancelledBeforeStart,
                    "Cancelado antes de iniciar este pacote."));
                MarkRemainingNotRun(requests, index + 1, results, "Cancelado antes de iniciar.");
                break;
            }

            if (!executionPolicy.IsAllowed)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.ExecutionBlocked,
                    executionPolicy.BlockReason));
                continue;
            }

            if (!request.Consent?.IsCompleteFor(package) ?? true)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.ConsentRequired,
                    "A revisão das consequências, do acordo da origem e do acordo do pacote precisa ser específica para este ID."));
                continue;
            }

            ProgramRunnerResult runnerResult;
            try
            {
                runnerResult = await runner.InstallAsync(package, request.Consent!, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.CancelledOutcomeUnknown,
                    "O processo WinGet foi interrompido. O estado do instalador pode ser parcial; verifique localmente antes de repetir."));
                MarkRemainingNotRun(requests, index + 1, results, "Cancelado após iniciar um instalador.");
                break;
            }

            if (runnerResult.Outcome == ProgramRunnerOutcome.ManagerUnavailable)
            {
                managerUnavailable = true;
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.ManagerUnavailable,
                    "O comando winget.exe não foi encontrado/iniciado; nenhum instalador foi tentado.", runnerResult.ExitCode));
                continue;
            }

            if (runnerResult.Outcome == ProgramRunnerOutcome.Cancelled)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.CancelledOutcomeUnknown,
                    "WinGet reportou cancelamento após o processo ser iniciado; o estado do instalador pode ser parcial.", runnerResult.ExitCode));
                MarkRemainingNotRun(requests, index + 1, results, "Cancelado após iniciar um instalador.");
                break;
            }

            if (runnerResult.Outcome == ProgramRunnerOutcome.Failed)
            {
                results.Add(new(package.PackageId, package.DisplayName, ProgramInstallOutcome.Failed,
                    "O processo WinGet falhou antes de retornar um código interpretável.", runnerResult.ExitCode));
                continue;
            }

            var mapped = MapExitCode(runnerResult.ExitCode);
            results.Add(new(package.PackageId, package.DisplayName, mapped.Outcome, mapped.Summary, runnerResult.ExitCode));
            if (mapped.Outcome is ProgramInstallOutcome.RebootRequired or ProgramInstallOutcome.RebootInitiated)
            {
                pauseForRestart = true;
            }
        }

        return results.AsReadOnly();
    }

    private static (ProgramInstallOutcome Outcome, string Summary) MapExitCode(int? exitCode) => exitCode switch
    {
        0 => (ProgramInstallOutcome.ReportedSuccessUnverified,
            "WinGet reportou sucesso para o pacote. A presença e o funcionamento não foram verificados após a operação."),
        NoPackageFound or MissingPackage => (ProgramInstallOutcome.PackageNotFound,
            "A origem/consulta não encontrou o ID exato. Nenhuma alternativa por nome foi selecionada."),
        NoNetwork => (ProgramInstallOutcome.NetworkUnavailable,
            "WinGet reportou que a operação exige conectividade. Não foi inferido que a rede inteira ou a internet estejam indisponíveis."),
        CancelSignalReceived or InstallerCancelled => (ProgramInstallOutcome.CancelledOutcomeUnknown,
            "WinGet reportou cancelamento depois do início; o estado do instalador pode ser parcial."),
        RebootToFinish or RebootForInstall => (ProgramInstallOutcome.RebootRequired,
            "WinGet reportou que um reinício é necessário. O app não reinicia o computador e pausa os demais pacotes."),
        RebootInitiated => (ProgramInstallOutcome.RebootInitiated,
            "WinGet reportou que um reinício foi iniciado pelo instalador. Este código não é tratado como autorização para reiniciar."),
        null => (ProgramInstallOutcome.Failed, "WinGet terminou sem código de saída disponível."),
        _ => (ProgramInstallOutcome.Failed, $"WinGet retornou código não classificado: 0x{unchecked((uint)exitCode.Value):X8}.")
    };

    private static void MarkRemainingNotRun(
        IReadOnlyList<ProgramInstallRequest> requests,
        int startIndex,
        ICollection<ProgramInstallResult> results,
        string reason)
    {
        for (var remaining = startIndex; remaining < requests.Count; remaining++)
        {
            var packageId = requests[remaining].PackageId;
            results.Add(new(packageId, packageId, ProgramInstallOutcome.NotRun, reason));
        }
    }
}
