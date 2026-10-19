using System.Collections.ObjectModel;
using Optimizer.Core.Adjustments;

namespace Optimizer.Core.Profiles;

public enum OptimizerProfileKind
{
    Light,
    Balanced,
    Extreme,
    Custom
}

public sealed record OptimizerProfile(
    string Id,
    string Name,
    string Description,
    OptimizerProfileKind Kind,
    AdjustmentRiskLevel MaximumSelectableRisk,
    bool SuggestCompatibleItems,
    bool RequiresExplicitSelection);

public sealed class OptimizerProfileCatalog
{
    private readonly IReadOnlyDictionary<string, OptimizerProfile> _profilesById;

    public OptimizerProfileCatalog(IEnumerable<OptimizerProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var byId = new Dictionary<string, OptimizerProfile>(StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) ||
                string.IsNullOrWhiteSpace(profile.Name) ||
                string.IsNullOrWhiteSpace(profile.Description))
            {
                throw new ArgumentException("Profile ID, name, and description are required.", nameof(profiles));
            }
            if (profile.RequiresExplicitSelection && profile.SuggestCompatibleItems)
                throw new ArgumentException("Profiles requiring explicit selection cannot auto-suggest items.", nameof(profiles));
            if (!byId.TryAdd(profile.Id, profile))
                throw new ArgumentException($"Duplicate profile ID: {profile.Id}", nameof(profiles));
        }

        _profilesById = new ReadOnlyDictionary<string, OptimizerProfile>(byId);
        Profiles = Array.AsReadOnly(byId.Values.ToArray());
    }

    public IReadOnlyList<OptimizerProfile> Profiles { get; }

    public bool TryGet(string id, out OptimizerProfile? profile) =>
        _profilesById.TryGetValue(id, out profile);

    public static OptimizerProfileCatalog CreateDefault() =>
        new(
        [
            new(
                "light",
                "Leve",
                "Sugere somente itens simuláveis classificados como baixo risco.",
                OptimizerProfileKind.Light,
                AdjustmentRiskLevel.Low,
                SuggestCompatibleItems: true,
                RequiresExplicitSelection: false),
            new(
                "balanced",
                "Equilibrado",
                "Sugere itens simuláveis de baixo ou moderado risco para revisão.",
                OptimizerProfileKind.Balanced,
                AdjustmentRiskLevel.Moderate,
                SuggestCompatibleItems: true,
                RequiresExplicitSelection: false),
            new(
                "extreme",
                "Extremo",
                "Não seleciona itens automaticamente; cada item exigirá escolha e revisão explícitas.",
                OptimizerProfileKind.Extreme,
                AdjustmentRiskLevel.High,
                SuggestCompatibleItems: false,
                RequiresExplicitSelection: true),
            new(
                "custom",
                "Personalizado",
                "Usa somente os itens escolhidos explicitamente.",
                OptimizerProfileKind.Custom,
                AdjustmentRiskLevel.High,
                SuggestCompatibleItems: false,
                RequiresExplicitSelection: true)
        ]);
}

public sealed record ProfileSelectionIssue(string AdjustmentId, string Reason);

public sealed record ProfilePlanResult(
    OptimizerProfile Profile,
    AdjustmentPlan Plan,
    IReadOnlyList<string> SuggestedAdjustmentIds,
    IReadOnlyList<ProfileSelectionIssue> RejectedSelections,
    bool RequiresManualReview);
