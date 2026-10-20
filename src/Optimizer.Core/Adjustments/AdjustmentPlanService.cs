using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Optimizer.Core.Adjustments;

public sealed class AdjustmentCompatibilityEvaluator
{
    public CompatibilityAssessment Evaluate(AdjustmentDefinition adjustment, PlatformDescriptor platform)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        ArgumentNullException.ThrowIfNull(platform);

        if (adjustment.Availability == AdjustmentAvailability.BlockedExperimental)
        {
            return new(
                CompatibilityStatus.Blocked,
                false,
                false,
                adjustment.AvailabilityReason ?? "Este item experimental está bloqueado.");
        }

        if (!platform.IsWindows)
            return new(CompatibilityStatus.Incompatible, false, false, "O item está limitado ao Windows.");

        if (string.IsNullOrWhiteSpace(platform.Architecture))
            return new(CompatibilityStatus.Unknown, false, false, "A arquitetura não foi identificada.");

        if (!adjustment.SupportedArchitectures.Contains(platform.Architecture, StringComparer.OrdinalIgnoreCase))
            return new(CompatibilityStatus.Incompatible, false, false, "A arquitetura não consta no suporte declarado.");

        if (adjustment.MinimumWindowsBuild is int minimumBuild)
        {
            if (platform.WindowsBuild is null)
                return new(CompatibilityStatus.Unknown, false, false, "O build do Windows não foi identificado.");
            if (platform.WindowsBuild.Value < minimumBuild)
                return new(CompatibilityStatus.Incompatible, false, false, $"Requer build {minimumBuild} ou posterior.");
        }

        if (adjustment.Availability == AdjustmentAvailability.RequiresSpecificApproval)
        {
            return new(
                CompatibilityStatus.ReviewSupported,
                false,
                true,
                "Compatível para revisão individual; execução real continua bloqueada por padrão e exige aprovação específica.");
        }

        return new(
            CompatibilityStatus.SimulationSupported,
            true,
            true,
            "Compatível somente com simulação. Nenhuma alteração do Windows está disponível.");
    }
}

public sealed class AdjustmentPlanService(
    AdjustmentCatalog catalog,
    AdjustmentCompatibilityEvaluator compatibilityEvaluator)
{
    public AdjustmentPlan CreatePlan(IEnumerable<string> selectedIds, PlatformDescriptor platform)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);
        ArgumentNullException.ThrowIfNull(platform);

        var normalizedIds = selectedIds.Select(id => id?.Trim() ?? string.Empty).ToArray();
        if (normalizedIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Adjustment IDs cannot be empty.", nameof(selectedIds));

        var uniqueIds = normalizedIds.Distinct(StringComparer.Ordinal).ToArray();
        var items = uniqueIds.Select(id => CreateItem(id, platform)).ToArray();
        var fingerprint = ComputeFingerprint(platform, items);

        return new AdjustmentPlan(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            platform with { },
            Array.AsReadOnly(items),
            fingerprint);
    }

    private AdjustmentPlanItem CreateItem(string id, PlatformDescriptor platform)
    {
        if (!catalog.TryGet(id, out var adjustment) || adjustment is null)
        {
            return new(
                id,
                null,
                id,
                null,
                null,
                AdjustmentRiskLevel.Unknown,
                null,
                "A alteração não está descrita; a execução fica bloqueada.",
                null,
                PlanItemStatus.UnknownAdjustment,
                "ID ausente do catálogo.");
        }

        var assessment = compatibilityEvaluator.Evaluate(adjustment, platform);
        var status = assessment.Status switch
        {
            CompatibilityStatus.SimulationSupported => PlanItemStatus.ReadyForSimulation,
            CompatibilityStatus.ReviewSupported => PlanItemStatus.ReadyForExplicitApproval,
            CompatibilityStatus.Blocked => PlanItemStatus.Blocked,
            CompatibilityStatus.Incompatible => PlanItemStatus.Incompatible,
            _ => PlanItemStatus.Blocked
        };

        return new(
            adjustment.Id,
            adjustment.Version,
            adjustment.Name,
            adjustment.Description,
            adjustment.Area,
            adjustment.Risk,
            adjustment.RequiresAdministrator,
            adjustment.Consequence,
            adjustment.Availability,
            status,
            assessment.Reason);
    }

    private static string ComputeFingerprint(
        PlatformDescriptor platform,
        IReadOnlyList<AdjustmentPlanItem> items)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            platform.IsWindows,
            platform.Architecture,
            platform.WindowsBuild,
            Items = items.Select(item => new
            {
                item.AdjustmentId,
                item.Version,
                item.Name,
                item.Description,
                item.Area,
                item.Risk,
                item.RequiresAdministrator,
                item.Consequence,
                item.Availability,
                item.Status,
                item.Reason
            })
        });

        return Convert.ToHexString(SHA256.HashData(payload));
    }
}
