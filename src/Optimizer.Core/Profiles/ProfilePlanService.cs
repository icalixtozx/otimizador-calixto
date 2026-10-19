using Optimizer.Core.Adjustments;

namespace Optimizer.Core.Profiles;

public sealed class ProfilePlanService(
    OptimizerProfileCatalog profileCatalog,
    AdjustmentCatalog adjustmentCatalog,
    AdjustmentCompatibilityEvaluator compatibilityEvaluator,
    AdjustmentPlanService planService)
{
    public ProfilePlanResult CreatePlan(
        string profileId,
        PlatformDescriptor platform,
        IEnumerable<string>? explicitlySelectedIds = null)
    {
        ArgumentNullException.ThrowIfNull(platform);
        if (!profileCatalog.TryGet(profileId, out var profile) || profile is null)
            throw new ArgumentException($"Unknown profile: {profileId}", nameof(profileId));

        var explicitIds = (explicitlySelectedIds ?? [])
            .Select(id => id?.Trim() ?? string.Empty)
            .ToArray();
        if (explicitIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Adjustment IDs cannot be empty.", nameof(explicitlySelectedIds));

        var selectedIds = new List<string>();
        var suggestedIds = new List<string>();
        var rejected = new List<ProfileSelectionIssue>();

        if (profile.SuggestCompatibleItems)
        {
            foreach (var adjustment in adjustmentCatalog.Definitions)
            {
                if (adjustment.Availability != AdjustmentAvailability.SimulationOnly ||
                    !IsRiskWithinProfile(adjustment.Risk, profile.MaximumSelectableRisk) ||
                    !compatibilityEvaluator.Evaluate(adjustment, platform).CanSimulate)
                {
                    continue;
                }

                selectedIds.Add(adjustment.Id);
                suggestedIds.Add(adjustment.Id);
            }
        }

        foreach (var id in explicitIds.Distinct(StringComparer.Ordinal))
        {
            if (adjustmentCatalog.TryGet(id, out var adjustment) && adjustment is not null &&
                !IsRiskWithinProfile(adjustment.Risk, profile.MaximumSelectableRisk))
            {
                rejected.Add(new(id, $"Risco {adjustment.Risk} excede o limite do perfil {profile.Name}."));
                continue;
            }

            if (!selectedIds.Contains(id, StringComparer.Ordinal))
                selectedIds.Add(id);
        }

        var plan = planService.CreatePlan(selectedIds, platform);
        return new(
            profile,
            plan,
            Array.AsReadOnly(suggestedIds.ToArray()),
            Array.AsReadOnly(rejected.ToArray()),
            RequiresManualReview: true);
    }

    private static bool IsRiskWithinProfile(
        AdjustmentRiskLevel risk,
        AdjustmentRiskLevel maximum) =>
        risk != AdjustmentRiskLevel.Unknown && risk <= maximum;
}
