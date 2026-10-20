using System.Security.Cryptography;
using System.Text;
using Optimizer.Core.Adjustments;
using Optimizer.Core.Consent;
using Optimizer.Core.Recovery;

namespace Optimizer.Core.Inventory;

/// <summary>
/// Prepares a one-device driver operation using shared plan consent and local history.
/// Its production mutation policy is blocked by default. Staging and device association are separate steps.
/// </summary>
public sealed class DriverPackageInstallationService
{
    private const string StagedInfPathPrefix = "StagedInfPath=";
    public const string AdjustmentId = "driver.assisted.install";
    public const string ProfileId = "driver-assisted-install";

    private readonly IDriverInventoryService _inventoryService;
    private readonly IDriverPackageInstaller _installer;
    private readonly IDriverPackageMutationPolicy _mutationPolicy;
    private readonly IDriverPackageSignatureVerifier _signatureVerifier;
    private readonly JsonOperationJournalStore _journalStore;
    private readonly PlanConsentService _consentService;
    private readonly OperationJournalCoordinator _journal;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public DriverPackageInstallationService(
        IDriverInventoryService inventoryService,
        IDriverPackageInstaller installer,
        IDriverPackageSignatureVerifier signatureVerifier,
        JsonOperationJournalStore journalStore,
        PlanConsentService consentService,
        OperationJournalCoordinator journal,
        IDriverPackageMutationPolicy? mutationPolicy = null)
    {
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        _consentService = consentService ?? throw new ArgumentNullException(nameof(consentService));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _mutationPolicy = mutationPolicy ?? new BlockedDriverPackageMutationPolicy(
            "Instalação de drivers está bloqueada por padrão nesta compilação experimental.");
    }

    public DriverPackageInstallReview CreateReview(
        DriverPackageInfCandidate candidate,
        DriverInfo targetDevice,
        PlatformDescriptor platform)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(targetDevice);
        ArgumentNullException.ThrowIfNull(platform);
        if (!candidate.CanPrepareInstallation)
            throw new InvalidOperationException("Somente um INF compatível segundo as verificações, assinado, com Hardware ID exato e alvo único pode receber prévia de instalação.");
        if (string.IsNullOrWhiteSpace(candidate.SourceSha256) || candidate.SourceSha256.Length != 64 ||
            !candidate.SourceSha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(candidate.InfSha256))
            throw new InvalidOperationException("A impressão digital do pacote/INF não está completa.");
        if (string.IsNullOrWhiteSpace(targetDevice.DeviceId) ||
            !string.Equals(candidate.DeviceMatches[0].Device.DeviceId, targetDevice.DeviceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O dispositivo escolhido não é o alvo exato encontrado na análise do pacote.");

        var packageName = candidate.SourceFileName ?? Path.GetFileName(candidate.InfPath);
        var identityHash = HashText(targetDevice.DeviceId);
        var consequence = $"Operação administrativa para o dispositivo {targetDevice.DeviceName}: origem {packageName} ({candidate.SourceHashLabel} {candidate.SourceSha256[..12]}…), INF {candidate.InfName}, versão declarada {candidate.DriverVersion ?? "não informada"}. A associação será tentada só se o INF staged aparecer como único candidato compatível de melhor rank; empate ou rank inferior será recusado, sem forçar downgrade. O adaptador pode desconectar e pode ser necessário reiniciar. O staging é no computador e o pacote pode ficar disponível para outros dispositivos correspondentes; diretivas do INF (como CopyINF, serviços ou filtros) podem ter efeitos adicionais. Reversão não é garantida e exigirá revisão manual.";
        var definition = new AdjustmentDefinition(
            AdjustmentId,
            1,
            "Instalação assistida de pacote de driver",
            $"{candidate.SourceHashLabel} {candidate.SourceSha256}; INF {candidate.InfName}; dispositivo local identificado internamente por SHA-256 {identityHash}.",
            AdjustmentArea.Drivers,
            AdjustmentRiskLevel.High,
            consequence,
            RequiresAdministrator: true,
            SupportedArchitectures: [platform.Architecture],
            MinimumWindowsBuild: null,
            Availability: AdjustmentAvailability.RequiresSpecificApproval,
            AvailabilityReason: "A instalação real continua bloqueada por padrão; esta prévia registra alvo e consequências.");
        var catalog = new AdjustmentCatalog([definition]);
        var compatibility = new AdjustmentCompatibilityEvaluator();
        var plan = new AdjustmentPlanService(catalog, compatibility).CreatePlan([AdjustmentId], platform);
        if (plan.Items.Count != 1 || plan.Items[0].Status != PlanItemStatus.ReadyForExplicitApproval)
            throw new PlatformNotSupportedException(plan.Items.FirstOrDefault()?.Reason ?? "A plataforma não pode preparar este plano.");

        return new(
            candidate,
            targetDevice.DeviceId,
            targetDevice,
            plan,
            DateTimeOffset.UtcNow,
            "Administrador do Windows. Esta versão não usa worker elevado separado; a operação futura só poderá chamar o backend se o processo já estiver elevado.",
            "O adaptador de rede pode cair durante a troca; conectividade remota pode ser perdida. O Windows pode solicitar reinicialização. Nenhum ganho de desempenho é previsto.",
            "A captura registra somente o driver ativo antes da operação. Não garante recuperação do fabricante, não remove pacotes do Driver Store e não substitui um ponto de restauração. A recuperação de dispositivo que deixe de iniciar requer procedimento manual/OEM.",
            "Alvo enumerado na análise: " + candidate.TargetSummary + ". DiInstallDevice será chamado com o SP_DEVINFO_DATA e o SP_DRVINFO_DATA deste devnode; a identidade, Hardware ID e INF ativo são relidos imediatamente antes da chamada. " +
            "O staging do pacote altera o Driver Store da máquina e pode tornar o pacote disponível para futuras instâncias correspondentes. Diretivas do INF, como CopyINF, AddService, filtros ou componentes, podem ampliar efeitos além do alvo. CopyINF pode stagingar pacotes adicionais, e o Windows documenta que, em certas condições, pacotes copiados podem ser instalados em dispositivos aplicáveis. O app não usa a flag de instalação de CopyINF e não promete isolamento total dos efeitos declarados pelo INF. Operações reais continuam bloqueadas por política.");
    }

    /// <summary>Records accepted consequences and a fresh read-only device snapshot; never invokes a setter.</summary>
    public async Task<OperationJournalEntry> CaptureAsync(
        DriverPackageInstallReview review,
        bool consequencesAccepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        ValidateReview(review);
        if (!consequencesAccepted)
            throw new InvalidOperationException("As consequências não foram aceitas; consentimento e captura não foram registrados.");

        var entry = _journal.CreatePlanned(review.Plan, ProfileId);
        await _journalStore.SaveAsync(entry, cancellationToken);
        var receipt = _consentService.Record(review.Plan,
            new Dictionary<string, bool>(StringComparer.Ordinal) { [AdjustmentId] = true });
        entry = _journal.RecordConsent(entry, review.Plan, receipt);
        await _journalStore.SaveAsync(entry, cancellationToken);

        var conflict = await CompareReviewedStateAsync(review, cancellationToken);
        if (conflict is not null)
        {
            entry = conflict.StartsWith("Falha:", StringComparison.Ordinal)
                ? _journal.RecordPreWriteFailure(entry, conflict + " Captura recusada; nenhuma instalação foi tentada.")
                : _journal.MarkConflict(entry, conflict + " Captura recusada; nenhuma instalação foi tentada.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            return entry;
        }

        var snapshotValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CaptureKind"] = "read-only-driver-before-stage",
            ["DeviceInstanceSha256"] = HashText(review.DeviceInstanceId),
            ["CurrentProvider"] = review.CurrentDriver.Provider ?? "",
            ["CurrentVersion"] = review.CurrentDriver.Version ?? "",
            ["CurrentInfName"] = review.CurrentDriver.InfName ?? "",
            ["CurrentSignatureReported"] = review.CurrentDriver.IsSigned?.ToString() ?? "unknown",
            ["TargetSourceFileSha256"] = review.Candidate.SourceSha256!,
            ["TargetInfName"] = review.Candidate.InfName,
            ["TargetInfSha256"] = review.Candidate.InfSha256!,
            ["TargetVersionDeclared"] = review.Candidate.DriverVersion ?? "",
            ["CatalogSigner"] = review.Candidate.CatalogSigner ?? "",
            ["DriverStoreStaged"] = "false",
            ["DeviceAssociationVerified"] = "false"
        };
        var snapshot = new AdjustmentSnapshot(AdjustmentId, 1, DateTimeOffset.UtcNow, snapshotValues);
        entry = _journal.RecordSnapshot(entry, snapshot);
        await _journalStore.SaveAsync(entry, cancellationToken);
        return entry;
    }

    public Task<DriverPackageInstallStepResult> StageToDriverStoreAsync(
        DriverPackageInstallReview review,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationStepAsync(review, operationId, install: false, cancellationToken);

    public Task<DriverPackageInstallStepResult> AssociateWithTargetDeviceAsync(
        DriverPackageInstallReview review,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationStepAsync(review, operationId, install: true, cancellationToken);

    private async Task<DriverPackageInstallStepResult> ExecuteMutationStepAsync(
        DriverPackageInstallReview review,
        Guid operationId,
        bool install,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);
        ValidateReview(review);
        if (!_mutationPolicy.IsEnabled)
            return new(DriverPackageInstallStepStatus.Blocked, null, false,
                string.IsNullOrWhiteSpace(_mutationPolicy.BlockReason)
                    ? "Operação real bloqueada por política." : _mutationPolicy.BlockReason);

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var entry = await _journalStore.LoadAsync(operationId, cancellationToken);
            if (entry is null || entry.PlanId != review.Plan.Id ||
                !string.Equals(entry.PlanFingerprint, review.Plan.Fingerprint, StringComparison.Ordinal) ||
                entry.State is not (JournalOperationState.SnapshotsCaptured or JournalOperationState.MutationMayHaveStarted) ||
                entry.Consent is null || !entry.HasCompleteSnapshots())
                return new(DriverPackageInstallStepStatus.RefusedByStateConflict, null, false,
                    "Não há consentimento e captura persistidos para este plano; atualize a leitura e revise novamente.");
            if (!_consentService.Validate(review.Plan, entry.Consent).AllConsequencesAccepted)
                return new(DriverPackageInstallStepStatus.RefusedByStateConflict, null, false,
                    "O consentimento persistido não corresponde às consequências atuais.");

            if (install && !entry.Events.Any(item => item.EventType == "DriverPackageStagedToDriverStore"))
                return new(DriverPackageInstallStepStatus.RefusedByStateConflict, null, false,
                    "O pacote ainda não foi registrado como preparado no Driver Store; a associação ao dispositivo não será tentada.");
            if (!install && entry.State != JournalOperationState.SnapshotsCaptured)
                return new(DriverPackageInstallStepStatus.RefusedByStateConflict, null, false,
                    "O plano já saiu do estado de captura; não será tentado outro staging.");

            var conflict = await CompareReviewedStateAsync(review, cancellationToken);
            if (conflict is not null)
            {
                if (entry.State == JournalOperationState.SnapshotsCaptured)
                {
                    entry = _journal.MarkConflict(entry, conflict + " Nenhuma chamada de instalação foi feita.");
                    await _journalStore.SaveAsync(entry, CancellationToken.None);
                }
                return new(DriverPackageInstallStepStatus.RefusedByStateConflict, null, false, conflict);
            }

            if (!FilesStillMatchReview(review.Candidate, out var filesReason))
                return new(DriverPackageInstallStepStatus.RefusedByStateConflict, null, false,
                    $"Os arquivos do pacote mudaram desde a análise: {filesReason} Nenhuma chamada de instalação foi feita.");

            if (!install)
            {
                entry = _journal.ArmMutation(entry, review.Plan);
                await _journalStore.SaveAsync(entry, cancellationToken);
            }

            string? stagedInfPath = null;
            DriverPackageCommandResult command;
            if (install)
            {
                var stageEvent = entry.Events.LastOrDefault(item => item.EventType == "DriverPackageStagedToDriverStore");
                stagedInfPath = ExtractStagedInfPath(stageEvent?.Message);
                if (string.IsNullOrWhiteSpace(stagedInfPath))
                    return new(DriverPackageInstallStepStatus.RefusedByStateConflict, null, false,
                        "O diário não contém o caminho do INF publicado pelo estágio; uma associação por alvo não pode ser identificada com segurança.");
                command = await _installer.AssociateWithDeviceAsync(new(
                    review.DeviceInstanceId,
                    review.Candidate.DeviceMatches[0].MatchedId,
                    review.CurrentDriver.InfName ?? "",
                    stagedInfPath), cancellationToken);
            }
            else
            {
                command = await _installer.StageToDriverStoreAsync(review.Candidate.InfPath, cancellationToken);
            }
            if (!command.Succeeded)
            {
                if (command.Refusal is not null)
                {
                    entry = _journal.MarkConflict(entry, command.Detail + " Nenhuma associação foi concluída.");
                    await _journalStore.SaveAsync(entry, CancellationToken.None);
                    return new(DriverPackageInstallStepStatus.RefusedByStateConflict,
                        command.ExitCode, command.RebootMayBeRequired, command.Detail);
                }
                entry = _journal.MarkFailed(entry, command.Detail);
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return new(command.ExitCode is 5 or 740 ? DriverPackageInstallStepStatus.AccessDenied : DriverPackageInstallStepStatus.Failed,
                    command.ExitCode, command.RebootMayBeRequired, command.Detail);
            }

            if (!install)
            {
                if (string.IsNullOrWhiteSpace(command.StagedInfPath))
                {
                    entry = _journal.MarkFailed(entry, "O adaptador reportou staging, mas não informou o INF publicado necessário para associação direcionada.");
                    await _journalStore.SaveAsync(entry, CancellationToken.None);
                    return new(DriverPackageInstallStepStatus.Failed, command.ExitCode, command.RebootMayBeRequired, entry.Events.Last().Message);
                }
                stagedInfPath = command.StagedInfPath;
                entry = _journal.RecordProgress(entry, "DriverPackageStagedToDriverStore",
                    $"{StagedInfPathPrefix}{stagedInfPath}\n{command.Detail} O pacote está no Driver Store; nenhum dispositivo foi associado.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return new(DriverPackageInstallStepStatus.DriverStoreStagedNotAssociated,
                    command.ExitCode, command.RebootMayBeRequired,
                    $"SetupCopyOEMInf informou staging do pacote como {Path.GetFileName(stagedInfPath)} no Driver Store. A associação ao dispositivo ainda não foi executada.");
            }

            if (command.RebootMayBeRequired == true)
            {
                entry = _journal.RecordProgress(entry, "DriverDeviceAssociationRestartPending",
                    $"A API direcionada retornou que o reinício é necessário para concluir a associação do INF {Path.GetFileName(stagedInfPath)} no dispositivo alvo. Nenhum reinício foi executado; o driver ativo não foi declarado verificado.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return new(DriverPackageInstallStepStatus.DeviceAssociationPendingRestart,
                    command.ExitCode, true,
                    "A associação direcionada solicitou reinício. O app não reinicia o PC; confira o INF ativo após reiniciar manualmente.");
            }

            var after = await _inventoryService.CollectAsync(cancellationToken);
            var target = after.Drivers.SingleOrDefault(driver =>
                string.Equals(driver.DeviceId, review.DeviceInstanceId, StringComparison.OrdinalIgnoreCase));
            if (after.Availability == InventoryAvailability.Available && target is not null &&
                string.Equals(target.InfName, Path.GetFileName(stagedInfPath), StringComparison.OrdinalIgnoreCase))
            {
                entry = _journal.RecordProgress(entry, "DriverDeviceAssociationVerified",
                    $"A leitura Plug and Play posterior reportou o INF publicado {Path.GetFileName(stagedInfPath)} no mesmo identificador de dispositivo; confirme o estado funcional do dispositivo separadamente.");
                entry = _journal.MarkMutationVerified(entry,
                    "Associação direcionada confirmada pela releitura WMI do INF ativo no mesmo dispositivo; funcionamento e conectividade continuam exigindo validação manual.");
                await _journalStore.SaveAsync(entry, CancellationToken.None);
                return new(DriverPackageInstallStepStatus.DeviceAssociationVerified,
                    command.ExitCode, command.RebootMayBeRequired,
                    "O INF ativo foi confirmado por nova leitura no mesmo dispositivo. Isso não comprova que a função do dispositivo opere corretamente.");
            }

            entry = _journal.RecordProgress(entry, "DriverDeviceAssociationNotVerified",
                $"DiInstallDevice retornou êxito, mas a releitura do INF publicado {Path.GetFileName(stagedInfPath)} não confirmou associação ao dispositivo-alvo. Revisão manual necessária.");
            await _journalStore.SaveAsync(entry, CancellationToken.None);
            return new(DriverPackageInstallStepStatus.DeviceAssociationNotVerified,
                command.ExitCode, command.RebootMayBeRequired,
                "O pacote pode estar no Driver Store, porém a associação ao dispositivo não foi confirmada pela releitura.");
        }
        catch (OperationCanceledException)
        {
            return new(DriverPackageInstallStepStatus.Failed, null, false,
                "Etapa cancelada. Se uma chamada ao Windows já começou, consulte o histórico e faça revisão manual.");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<string?> CompareReviewedStateAsync(DriverPackageInstallReview review, CancellationToken cancellationToken)
    {
        DriverInventoryResult current;
        try { current = await _inventoryService.CollectAsync(cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Falha: não foi possível reler o inventário de dispositivos ({exception.GetType().Name}).";
        }
        if (current.Availability != InventoryAvailability.Available)
            return "Falha: o inventário não está completo; o alvo não pode ser revalidado.";
        var matches = current.Drivers.Where(driver =>
            string.Equals(driver.DeviceId, review.DeviceInstanceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) return "O dispositivo-alvo desapareceu ou não pôde ser identificado de forma única.";
        var actual = matches[0];
        if (!(actual.HardwareIds ?? Array.Empty<string>()).Contains(
            review.Candidate.DeviceMatches[0].MatchedId, StringComparer.OrdinalIgnoreCase))
            return "Os Hardware IDs do dispositivo mudaram desde a revisão.";
        if (!string.Equals(actual.InfName, review.CurrentDriver.InfName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actual.Provider, review.CurrentDriver.Provider, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actual.Version, review.CurrentDriver.Version, StringComparison.OrdinalIgnoreCase))
            return "O driver ativo mudou desde a revisão; é necessária nova análise e captura.";
        return null;
    }

    private bool FilesStillMatchReview(DriverPackageInfCandidate candidate, out string reason)
    {
        reason = string.Empty;
        if (candidate.PayloadFilesSha256 is null || candidate.PayloadFilesSha256.Count == 0)
        {
            reason = "não há hashes dos arquivos necessários no registro da análise.";
            return false;
        }
        var root = candidate.PackageRootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            reason = "a raiz do pacote não está registrada.";
            return false;
        }
        foreach (var (relativePath, expected) in candidate.PayloadFilesSha256)
        {
            try
            {
                var full = Path.GetFullPath(Path.Combine(root, relativePath));
                var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                {
                    reason = $"{Path.GetFileName(relativePath)} foi removido ou saiu da raiz do pacote.";
                    return false;
                }
                using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
                var actual = Convert.ToHexString(SHA256.HashData(stream));
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"{Path.GetFileName(relativePath)} foi alterado após a análise.";
                    return false;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                reason = $"{Path.GetFileName(relativePath)} não pode ser conferido ({exception.GetType().Name}).";
                return false;
            }
        }
        var catalog = candidate.CatalogName is null ? null : candidate.PayloadFilesSha256.Keys.SingleOrDefault(key =>
            string.Equals(Path.GetFileName(key), candidate.CatalogName, StringComparison.OrdinalIgnoreCase));
        if (catalog is null)
        {
            reason = "o catálogo não está entre os arquivos com hash registrado.";
            return false;
        }
        var catalogPath = Path.GetFullPath(Path.Combine(root, catalog));
        var infSignature = _signatureVerifier.VerifyInf(candidate.InfPath, catalogPath);
        if (infSignature.Status != DriverPackageVerificationStatus.Valid)
        {
            reason = "a verificação de assinatura/membership do INF deixou de ser válida.";
            return false;
        }
        foreach (var relativePath in candidate.PayloadFilesSha256.Keys.Where(key =>
            !string.Equals(Path.GetFileName(key), candidate.CatalogName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Path.GetFullPath(Path.Combine(root, key)), Path.GetFullPath(candidate.InfPath), StringComparison.OrdinalIgnoreCase)))
        {
            var member = _signatureVerifier.VerifyCatalogMember(Path.Combine(root, relativePath), catalogPath);
            if (member.Status != DriverPackageVerificationStatus.Valid)
            {
                reason = $"a assinatura de catálogo de {Path.GetFileName(relativePath)} não foi confirmada.";
                return false;
            }
        }
        return true;
    }

    private static void ValidateReview(DriverPackageInstallReview review)
    {
        if (review.Plan.Items.Count != 1 || review.Plan.Items[0].AdjustmentId != AdjustmentId ||
            review.Plan.Items[0].Availability != AdjustmentAvailability.RequiresSpecificApproval ||
            review.Candidate.Outcome != DriverPackageAnalysisOutcome.CompatibleAccordingToChecks ||
            !review.Candidate.CanPrepareInstallation)
            throw new InvalidOperationException("A revisão de driver está incompleta ou não corresponde ao plano individual aprovado.");
    }

    private static string? ExtractStagedInfPath(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return null;
        var firstLine = detail.Split(["\r\n", "\n"], StringSplitOptions.None)[0];
        return firstLine.StartsWith(StagedInfPathPrefix, StringComparison.Ordinal)
            ? firstLine[StagedInfPathPrefix.Length..].Trim()
            : null;
    }

    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal static class DriverPackageJournalSnapshotExtensions
{
    public static bool HasCompleteSnapshots(this OperationJournalEntry entry) =>
        entry.PlanItems.Count > 0 && entry.PlanItems.All(planItem => entry.Snapshots.Count(snapshot =>
            snapshot.AdjustmentId == planItem.AdjustmentId && snapshot.Version == planItem.Version) == 1);
}
