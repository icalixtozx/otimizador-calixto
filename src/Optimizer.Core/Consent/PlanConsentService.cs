using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Optimizer.Core.Adjustments;

namespace Optimizer.Core.Consent;

public enum ConsentValidationStatus
{
    Accepted,
    Declined,
    StalePlan,
    InvalidReceipt
}

public sealed record ConsequenceConsentDecision(
    string AdjustmentId,
    int Version,
    string Consequence,
    bool Accepted);

public sealed record PlanConsentReceipt(
    Guid PlanId,
    string PlanFingerprint,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ConsequenceConsentDecision> Decisions,
    string ReceiptFingerprint)
{
    // A consent receipt is not an implementation or authorization to write to Windows.
    public bool CanAuthorizeRealApplication => false;
}

public sealed record ConsentValidationResult(
    ConsentValidationStatus Status,
    string Message)
{
    public bool AllConsequencesAccepted => Status == ConsentValidationStatus.Accepted;
}

public sealed class PlanConsentService
{
    public PlanConsentReceipt Record(
        AdjustmentPlan plan,
        IReadOnlyDictionary<string, bool> decisionsByAdjustmentId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(decisionsByAdjustmentId);

        if (!plan.CanAcceptConsent)
            throw new InvalidOperationException("Consent cannot be recorded for a blocked or incomplete plan.");

        var requiredIds = plan.Items.Select(item => item.AdjustmentId).ToHashSet(StringComparer.Ordinal);
        var suppliedIds = decisionsByAdjustmentId.Keys.ToHashSet(StringComparer.Ordinal);
        if (!requiredIds.SetEquals(suppliedIds))
            throw new ArgumentException("Provide one explicit decision for every plan consequence.", nameof(decisionsByAdjustmentId));

        var decisions = plan.Items
            .Select(item => new ConsequenceConsentDecision(
                item.AdjustmentId,
                item.Version!.Value,
                item.Consequence,
                decisionsByAdjustmentId[item.AdjustmentId]))
            .ToArray();

        var immutableDecisions = Array.AsReadOnly(decisions);
        var createdAt = DateTimeOffset.UtcNow;
        var fingerprint = ComputeReceiptFingerprint(plan.Id, plan.Fingerprint, createdAt, immutableDecisions);

        return new(
            plan.Id,
            plan.Fingerprint,
            createdAt,
            immutableDecisions,
            fingerprint);
    }

    public ConsentValidationResult Validate(AdjustmentPlan plan, PlanConsentReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(receipt);

        if (receipt.PlanId != plan.Id ||
            !string.Equals(receipt.PlanFingerprint, plan.Fingerprint, StringComparison.Ordinal))
        {
            return new(ConsentValidationStatus.StalePlan, "O consentimento pertence a outro plano ou a uma versão anterior.");
        }

        if (!plan.CanAcceptConsent || receipt.Decisions is null || receipt.Decisions.Count != plan.Items.Count || string.IsNullOrWhiteSpace(receipt.ReceiptFingerprint))
            return new(ConsentValidationStatus.InvalidReceipt, "O recibo não corresponde aos itens elegíveis do plano.");

        for (var index = 0; index < plan.Items.Count; index++)
        {
            var item = plan.Items[index];
            var decision = receipt.Decisions[index];
            if (decision.AdjustmentId != item.AdjustmentId ||
                decision.Version != item.Version ||
                decision.Consequence != item.Consequence)
            {
                return new(ConsentValidationStatus.InvalidReceipt, "As consequências registradas não correspondem ao plano.");
            }
        }

        var expectedFingerprint = ComputeReceiptFingerprint(
            receipt.PlanId,
            receipt.PlanFingerprint,
            receipt.CreatedAtUtc,
            receipt.Decisions);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(expectedFingerprint),
                    Convert.FromHexString(receipt.ReceiptFingerprint)))
            {
                return new(ConsentValidationStatus.InvalidReceipt, "A impressão digital do recibo é inválida.");
            }
        }
        catch (FormatException)
        {
            return new(ConsentValidationStatus.InvalidReceipt, "A impressão digital do recibo está malformada.");
        }

        return receipt.Decisions.All(decision => decision.Accepted)
            ? new(ConsentValidationStatus.Accepted, "Todas as consequências foram aceitas para este plano.")
            : new(ConsentValidationStatus.Declined, "Há consequências não aceitas; o plano não está consentido.");
    }

    private static string ComputeReceiptFingerprint(
        Guid planId,
        string planFingerprint,
        DateTimeOffset createdAt,
        IReadOnlyList<ConsequenceConsentDecision> decisions)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            PlanId = planId,
            PlanFingerprint = planFingerprint,
            CreatedAtUtc = createdAt,
            Decisions = decisions
        });

        return Convert.ToHexString(SHA256.HashData(payload));
    }
}
