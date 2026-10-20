using Optimizer.Core.Adjustments;
using Optimizer.Core.Consent;

namespace Optimizer.Core.Recovery;

public enum JournalOperationState
{
    // Keep the original numeric values stable: existing journal JSON stores these enums as numbers.
    Planned = 0,
    ConsentRecorded = 1,
    SnapshotsCaptured = 2,
    MutationMayHaveStarted = 3,
    Failed = 4,
    Interrupted = 5,
    SimulationCompleted = 6,
    MutationVerified = 7,
    RestorationMayHaveStarted = 8,
    RestoredVerified = 9,
    NoChangeRequired = 10,
    Conflict = 11
}

public enum RecoveryAssessmentStatus
{
    NoMutationRecorded,
    NoSystemChangeExpected,
    SnapshotAvailableForManualReview,
    SnapshotMissing
}

public enum JournalDataOrigin
{
    UserPlan,
    SyntheticDemonstration
}

public enum JournalResumeStatus
{
    CanContinueBeforeMutation,
    RequiresCurrentPlanReview,
    ManualReviewRequired,
    AlreadyFinished,
    PlanConflict
}

public sealed record JournalPlanItem(string AdjustmentId, int Version);

public sealed record AdjustmentSnapshot(
    string AdjustmentId,
    int Version,
    DateTimeOffset CapturedAtUtc,
    Dictionary<string, string> Values);

public sealed record JournalEvent(
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string Message);

public sealed record OperationJournalEntry(
    Guid OperationId,
    Guid PlanId,
    string PlanFingerprint,
    string ProfileId,
    DateTimeOffset CreatedAtUtc,
    JournalOperationState State,
    IReadOnlyList<JournalPlanItem> PlanItems,
    PlanConsentReceipt? Consent,
    IReadOnlyList<AdjustmentSnapshot> Snapshots,
    IReadOnlyList<JournalEvent> Events,
    JournalDataOrigin DataOrigin = JournalDataOrigin.UserPlan);

public sealed record JournalResumeAssessment(
    JournalResumeStatus Status,
    string Message,
    bool CanAutomaticallyContinue);

public sealed record RecoveryAssessment(
    RecoveryAssessmentStatus Status,
    string Message,
    bool CanAutomaticallyRecover);

public sealed class OperationJournalCoordinator(PlanConsentService consentService)
{
    public OperationJournalEntry CreatePlanned(
        AdjustmentPlan plan,
        string profileId,
        JournalDataOrigin dataOrigin = JournalDataOrigin.UserPlan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanAcceptConsent)
            throw new InvalidOperationException("Only fully described and reviewable plans can enter the journal.");
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile ID is required.", nameof(profileId));

        return new(
            Guid.NewGuid(),
            plan.Id,
            plan.Fingerprint,
            profileId,
            DateTimeOffset.UtcNow,
            JournalOperationState.Planned,
            Array.AsReadOnly(plan.Items.Select(item => new JournalPlanItem(
                item.AdjustmentId,
                item.Version!.Value)).ToArray()),
            null,
            Array.AsReadOnly(Array.Empty<AdjustmentSnapshot>()),
            Array.AsReadOnly([
                new JournalEvent("Planned", DateTimeOffset.UtcNow, "Plano registrado; nenhuma alteração foi iniciada.")
            ]),
            dataOrigin);
    }

    public OperationJournalEntry RecordConsent(
        OperationJournalEntry entry,
        AdjustmentPlan plan,
        PlanConsentReceipt receipt)
    {
        ValidatePlanIdentity(entry, plan);
        if (entry.State != JournalOperationState.Planned)
            throw new InvalidOperationException("Consent can only be recorded for a planned operation.");

        var validation = consentService.Validate(plan, receipt);
        if (!validation.AllConsequencesAccepted)
            throw new InvalidOperationException("Every plan consequence must be accepted before proceeding.");

        return AppendEvent(
            entry with
            {
                State = JournalOperationState.ConsentRecorded,
                Consent = receipt
            },
            "ConsentRecorded",
            "Consentimento individual registrado para as consequências deste plano.");
    }

    public OperationJournalEntry RecordSnapshot(
        OperationJournalEntry entry,
        AdjustmentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (entry.DataOrigin == JournalDataOrigin.SyntheticDemonstration)
            throw new InvalidOperationException("Synthetic demonstrations cannot create Windows backup snapshots.");
        if (entry.State is not (JournalOperationState.ConsentRecorded or JournalOperationState.SnapshotsCaptured))
            throw new InvalidOperationException("A snapshot requires recorded consent and must precede mutation.");
        if (snapshot.Version < 1 || string.IsNullOrWhiteSpace(snapshot.AdjustmentId))
            throw new ArgumentException("Snapshot adjustment ID and version are required.", nameof(snapshot));

        var planItem = entry.PlanItems.SingleOrDefault(item => item.AdjustmentId == snapshot.AdjustmentId);
        if (planItem is null || planItem.Version != snapshot.Version)
            throw new ArgumentException("Snapshot does not match an item in the operation plan.", nameof(snapshot));
        if (entry.Snapshots.Any(existing => existing.AdjustmentId == snapshot.AdjustmentId))
            throw new InvalidOperationException("A snapshot already exists for this plan item.");

        var copiedSnapshot = snapshot with
        {
            Values = new Dictionary<string, string>(snapshot.Values, StringComparer.Ordinal)
        };
        var snapshots = entry.Snapshots.Append(copiedSnapshot).ToArray();
        var allSnapshotsCaptured = snapshots.Length == entry.PlanItems.Count;
        return AppendEvent(
            entry with
            {
                State = allSnapshotsCaptured ? JournalOperationState.SnapshotsCaptured : JournalOperationState.ConsentRecorded,
                Snapshots = Array.AsReadOnly(snapshots)
            },
            "SnapshotCaptured",
            allSnapshotsCaptured
                ? "Snapshots de todos os itens persistidos antes de qualquer operação."
                : $"Snapshot persistido para {snapshot.AdjustmentId}; aguardando os demais itens.");
    }

    public OperationJournalEntry ArmMutation(OperationJournalEntry entry, AdjustmentPlan plan)
    {
        ValidatePlanIdentity(entry, plan);
        if (entry.State != JournalOperationState.SnapshotsCaptured ||
            entry.Consent is null ||
            !JournalSnapshotValidator.HasCompleteSnapshots(entry))
        {
            throw new InvalidOperationException("Consent and a persisted snapshot for every item are required before mutation.");
        }

        var validation = consentService.Validate(plan, entry.Consent);
        if (!validation.AllConsequencesAccepted)
            throw new InvalidOperationException("Consent is no longer valid for the current plan.");

        return AppendEvent(
            entry with { State = JournalOperationState.MutationMayHaveStarted },
            "MutationArmed",
            "Ponto de recuperação gravado antes da operação; esta biblioteca não executa a mutação.");
    }

    public OperationJournalEntry MarkInterrupted(OperationJournalEntry entry, string reason) =>
        MarkTerminal(entry, JournalOperationState.Interrupted, "Interrupted", reason);

    public OperationJournalEntry MarkFailed(OperationJournalEntry entry, string reason) =>
        MarkTerminal(entry, JournalOperationState.Failed, "Failed", reason);

    public OperationJournalEntry MarkMutationVerified(OperationJournalEntry entry, string message) =>
        MarkFromState(entry, JournalOperationState.MutationMayHaveStarted,
            JournalOperationState.MutationVerified, "MutationVerified", message);

    public OperationJournalEntry MarkRestorationMayHaveStarted(OperationJournalEntry entry, string message)
    {
        if (entry.State is not (JournalOperationState.MutationMayHaveStarted or JournalOperationState.MutationVerified or
            JournalOperationState.Failed or JournalOperationState.Interrupted or JournalOperationState.RestorationMayHaveStarted))
            throw new InvalidOperationException("The journal state does not permit a conditional restoration attempt.");

        return AppendEvent(entry with { State = JournalOperationState.RestorationMayHaveStarted },
            "RestorationArmed", message);
    }

    public OperationJournalEntry MarkRestoredVerified(OperationJournalEntry entry, string message) =>
        MarkFromState(entry, JournalOperationState.RestorationMayHaveStarted,
            JournalOperationState.RestoredVerified, "RestoredVerified", message);

    public OperationJournalEntry MarkNoChangeRequired(OperationJournalEntry entry, string message) =>
        MarkFromState(entry, JournalOperationState.SnapshotsCaptured,
            JournalOperationState.NoChangeRequired, "NoChangeRequired", message);

    /// <summary>Records a conflict only when the caller has evidence that no write was issued.</summary>
    public OperationJournalEntry MarkConflict(OperationJournalEntry entry, string message)
    {
        if (entry.State is not (JournalOperationState.Planned or JournalOperationState.ConsentRecorded or
            JournalOperationState.SnapshotsCaptured or JournalOperationState.MutationMayHaveStarted or
            JournalOperationState.RestorationMayHaveStarted or JournalOperationState.MutationVerified or
            JournalOperationState.Failed or JournalOperationState.Interrupted))
            throw new InvalidOperationException("The journal state does not permit a no-write conflict result.");

        var state = entry.State is JournalOperationState.Planned or JournalOperationState.ConsentRecorded or
            JournalOperationState.SnapshotsCaptured
            ? JournalOperationState.Conflict
            : entry.State;
        return AppendEvent(entry with { State = state }, "Conflict", message);
    }

    /// <summary>Records an access or read failure for which the adapter confirms no setter was called.</summary>
    public OperationJournalEntry RecordPreWriteFailure(OperationJournalEntry entry, string message)
    {
        if (entry.State is JournalOperationState.SimulationCompleted or JournalOperationState.RestoredVerified or
            JournalOperationState.NoChangeRequired or JournalOperationState.Conflict)
            throw new InvalidOperationException("A terminal journal operation cannot receive a new pre-write failure.");
        return AppendEvent(entry, "WriteNotAttempted", message);
    }

    public OperationJournalEntry MarkSimulationCompleted(OperationJournalEntry entry)
    {
        if (entry.DataOrigin != JournalDataOrigin.SyntheticDemonstration)
            throw new InvalidOperationException("Only an explicitly synthetic demonstration can be marked as simulated.");
        if (entry.Snapshots.Count > 0 || entry.State == JournalOperationState.SnapshotsCaptured)
            throw new InvalidOperationException("A captured real-operation plan cannot be converted into a simulation result.");
        if (entry.State == JournalOperationState.SimulationCompleted)
            throw new InvalidOperationException("A simulation result has already been recorded.");

        if (entry.State is JournalOperationState.MutationMayHaveStarted or JournalOperationState.MutationVerified or
            JournalOperationState.RestorationMayHaveStarted or JournalOperationState.RestoredVerified or
            JournalOperationState.NoChangeRequired or JournalOperationState.Conflict or
            JournalOperationState.Interrupted or JournalOperationState.Failed)
            throw new InvalidOperationException("A real-operation state cannot be replaced by a simulation result.");

        return AppendEvent(
            entry with { State = JournalOperationState.SimulationCompleted },
            "SimulationCompleted",
            entry.DataOrigin == JournalDataOrigin.SyntheticDemonstration
                ? "Demonstração com dados sintéticos concluída; nenhum ajuste do Windows foi aplicado e nenhum backup do Windows foi criado."
                : "Simulação concluída; nenhum ajuste do Windows foi aplicado.");
    }

    public JournalResumeAssessment AssessResume(
        OperationJournalEntry entry,
        AdjustmentPlan? currentPlan = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.State is JournalOperationState.SimulationCompleted or JournalOperationState.MutationVerified or
            JournalOperationState.RestoredVerified or JournalOperationState.NoChangeRequired or JournalOperationState.Conflict)
            return new(JournalResumeStatus.AlreadyFinished, "A operação registrada terminou; nenhuma ação será repetida automaticamente.", false);

        if (entry.State is JournalOperationState.MutationMayHaveStarted or JournalOperationState.RestorationMayHaveStarted or
            JournalOperationState.Interrupted or JournalOperationState.Failed)
            return new(JournalResumeStatus.ManualReviewRequired,
                "Pode ter havido uma operação real. Exige revisão manual; o app não reaplica nem reverte automaticamente.", false);

        if (currentPlan is null)
            return new(JournalResumeStatus.RequiresCurrentPlanReview,
                "Recarregue e confira o plano atual e seu consentimento antes de continuar.", false);

        if (entry.PlanId != currentPlan.Id ||
            !string.Equals(entry.PlanFingerprint, currentPlan.Fingerprint, StringComparison.Ordinal))
            return new(JournalResumeStatus.PlanConflict, "O plano atual difere do diário; continuação recusada.", false);

        if (entry.State == JournalOperationState.Planned)
            return new(JournalResumeStatus.CanContinueBeforeMutation,
                "O diário confirma que nenhuma mutação começou; a revisão pode continuar após novo consentimento.", false);

        if (entry.State == JournalOperationState.ConsentRecorded && entry.Consent is not null &&
            consentService.Validate(currentPlan, entry.Consent).AllConsequencesAccepted)
            return new(JournalResumeStatus.CanContinueBeforeMutation,
                "O plano e o consentimento ainda correspondem; a preparação pode continuar sem aplicar alterações.", false);

        if (entry.State == JournalOperationState.SnapshotsCaptured && entry.Consent is not null &&
            JournalSnapshotValidator.HasCompleteSnapshots(entry) &&
            consentService.Validate(currentPlan, entry.Consent).AllConsequencesAccepted)
            return new(JournalResumeStatus.CanContinueBeforeMutation,
                "A preparação pode ser retomada após nova revisão; nenhuma mutação é disparada por esta avaliação.", false);

        return new(JournalResumeStatus.PlanConflict,
            "O estado do diário não permite continuação segura; revisar manualmente.", false);
    }

    private static OperationJournalEntry MarkTerminal(
        OperationJournalEntry entry,
        JournalOperationState state,
        string eventType,
        string reason)
    {
        if (entry.State is not (JournalOperationState.MutationMayHaveStarted or JournalOperationState.RestorationMayHaveStarted))
            throw new InvalidOperationException("Only an armed operation can be marked failed or interrupted.");

        return AppendEvent(entry with { State = state }, eventType, reason);
    }

    private static void ValidatePlanIdentity(OperationJournalEntry entry, AdjustmentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(plan);
        if (entry.PlanId != plan.Id ||
            !string.Equals(entry.PlanFingerprint, plan.Fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Journal entry belongs to a different plan.");
        }
    }

    private static OperationJournalEntry AppendEvent(
        OperationJournalEntry entry,
        string eventType,
        string message)
    {
        var events = entry.Events.Append(new JournalEvent(eventType, DateTimeOffset.UtcNow, message)).ToArray();
        return entry with { Events = Array.AsReadOnly(events) };
    }

    private static OperationJournalEntry MarkFromState(
        OperationJournalEntry entry,
        JournalOperationState expected,
        JournalOperationState next,
        string eventType,
        string message)
    {
        if (entry.State != expected)
            throw new InvalidOperationException($"Journal state {entry.State} cannot transition to {next}.");
        return AppendEvent(entry with { State = next }, eventType, message);
    }

}

public static class OperationRecoveryEvaluator
{
    public static RecoveryAssessment Assess(OperationJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.State == JournalOperationState.SimulationCompleted)
        {
            return new(
                RecoveryAssessmentStatus.NoSystemChangeExpected,
                entry.DataOrigin == JournalDataOrigin.SyntheticDemonstration
                    ? "Demonstração com dados sintéticos; nenhuma alteração ou backup do Windows foi feito."
                    : "A operação foi somente uma simulação em memória.",
                CanAutomaticallyRecover: false);
        }

        if (entry.State is JournalOperationState.Planned or
            JournalOperationState.ConsentRecorded or
            JournalOperationState.SnapshotsCaptured or
            JournalOperationState.Conflict or
            JournalOperationState.NoChangeRequired)
        {
            return new(
                RecoveryAssessmentStatus.NoMutationRecorded,
                "O diário não registra que uma operação tenha começado.",
                CanAutomaticallyRecover: false);
        }

        if (!JournalSnapshotValidator.HasCompleteSnapshots(entry))
        {
            return new(
                RecoveryAssessmentStatus.SnapshotMissing,
                "Há uma operação incompleta sem snapshot suficiente; recuperação automática indisponível.",
                CanAutomaticallyRecover: false);
        }

        return new(
            RecoveryAssessmentStatus.SnapshotAvailableForManualReview,
            "Snapshots disponíveis para revisão manual. Nenhuma reversão automática foi executada.",
            CanAutomaticallyRecover: false);
    }
}

internal static class JournalSnapshotValidator
{
    public static bool HasCompleteSnapshots(OperationJournalEntry entry)
    {
        if (entry.PlanItems is null || entry.Snapshots is null || entry.PlanItems.Count == 0 ||
            entry.Snapshots.Count != entry.PlanItems.Count)
        {
            return false;
        }

        return entry.PlanItems.All(item =>
            entry.Snapshots.Count(snapshot =>
                snapshot.AdjustmentId == item.AdjustmentId && snapshot.Version == item.Version) == 1);
    }
}
