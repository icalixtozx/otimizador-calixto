using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Optimizer.Core.Adjustments;

public sealed class AdjustmentCatalog
{
    private static readonly Regex StableIdPattern = new(
        "^[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<string, AdjustmentDefinition> _definitionsById;

    public AdjustmentCatalog(IEnumerable<AdjustmentDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var definitionsById = new Dictionary<string, AdjustmentDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            Validate(definition);
            var safeDefinition = definition with
            {
                SupportedArchitectures = Array.AsReadOnly(definition.SupportedArchitectures.ToArray())
            };

            if (!definitionsById.TryAdd(safeDefinition.Id, safeDefinition))
            {
                throw new ArgumentException($"Duplicate adjustment ID: {safeDefinition.Id}", nameof(definitions));
            }
        }

        _definitionsById = new ReadOnlyDictionary<string, AdjustmentDefinition>(definitionsById);
        Definitions = Array.AsReadOnly(definitionsById.Values.OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<AdjustmentDefinition> Definitions { get; }

    public bool TryGet(string id, out AdjustmentDefinition? definition) =>
        _definitionsById.TryGetValue(id, out definition);

    private static void Validate(AdjustmentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!StableIdPattern.IsMatch(definition.Id))
            throw new ArgumentException("Adjustment IDs must use a stable lowercase identifier.", nameof(definition));
        if (definition.Version < 1)
            throw new ArgumentException("Adjustment versions must be positive.", nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Name) ||
            string.IsNullOrWhiteSpace(definition.Description) ||
            string.IsNullOrWhiteSpace(definition.Consequence))
        {
            throw new ArgumentException("Name, description, and consequence are required.", nameof(definition));
        }
        if (definition.SupportedArchitectures is null)
            throw new ArgumentException("Supported architectures must be specified.", nameof(definition));
        if (definition.Availability == AdjustmentAvailability.SimulationOnly &&
            definition.SupportedArchitectures.Count == 0)
        {
            throw new ArgumentException("Simulation-only entries must declare supported architectures.", nameof(definition));
        }
        if (definition.Availability == AdjustmentAvailability.BlockedExperimental &&
            string.IsNullOrWhiteSpace(definition.AvailabilityReason))
        {
            throw new ArgumentException("Blocked experimental entries must explain why they are blocked.", nameof(definition));
        }
        if (definition.MinimumWindowsBuild is < 0)
            throw new ArgumentException("Minimum Windows build cannot be negative.", nameof(definition));
    }
}
