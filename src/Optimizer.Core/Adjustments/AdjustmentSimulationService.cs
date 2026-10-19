namespace Optimizer.Core.Adjustments;

public interface ISimulatedAdjustmentAdapter
{
    Task SimulateAsync(AdjustmentDefinition adjustment, CancellationToken cancellationToken);
}

public sealed class InMemorySimulatedAdjustmentAdapter : ISimulatedAdjustmentAdapter
{
    public Task SimulateAsync(AdjustmentDefinition adjustment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        cancellationToken.ThrowIfCancellationRequested();

        // This adapter is intentionally a no-op. It does not access Windows APIs or persistent storage.
        return Task.CompletedTask;
    }
}

public sealed class AdjustmentSimulationService(
    AdjustmentCatalog catalog,
    AdjustmentCompatibilityEvaluator compatibilityEvaluator,
    ISimulatedAdjustmentAdapter adapter)
{
    public async Task<AdjustmentSimulationResult> SimulateAsync(
        AdjustmentPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var startedAt = DateTimeOffset.UtcNow;

        if (!IsPlanStillSimulationSafe(plan))
        {
            return Result(
                plan,
                AdjustmentSimulationOutcome.Refused,
                startedAt,
                [],
                "Plano recusado: há itens desconhecidos, incompatíveis ou bloqueados.");
        }

        var steps = new List<AdjustmentSimulationStep>();
        for (var index = 0; index < plan.Items.Count; index++)
        {
            var item = plan.Items[index];
            if (!catalog.TryGet(item.AdjustmentId, out var definition) || definition is null)
            {
                AddRemaining(plan, steps, index, "Item indisponível no catálogo.");
                return Result(
                    plan,
                    AdjustmentSimulationOutcome.Refused,
                    startedAt,
                    steps,
                    "Plano recusado porque o catálogo mudou.");
            }

            try
            {
                await adapter.SimulateAsync(definition, cancellationToken);
                steps.Add(new(
                    item.AdjustmentId,
                    AdjustmentSimulationStepStatus.Simulated,
                    "Marcador de simulação em memória; nenhuma alteração do Windows foi aplicada."));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                steps.Add(new(
                    item.AdjustmentId,
                    AdjustmentSimulationStepStatus.Interrupted,
                    "Simulação interrompida; estado do Windows não foi alterado."));
                AddRemaining(plan, steps, index + 1, "Não executado após interrupção.");
                return Result(
                    plan,
                    AdjustmentSimulationOutcome.Interrupted,
                    startedAt,
                    steps,
                    "Simulação interrompida. Nenhuma alteração do Windows foi aplicada.");
            }
            catch (Exception exception)
            {
                steps.Add(new(
                    item.AdjustmentId,
                    AdjustmentSimulationStepStatus.Failed,
                    $"Falha simulada ({exception.GetType().Name}); nenhuma alteração do Windows foi aplicada."));
                AddRemaining(plan, steps, index + 1, "Não executado após falha.");
                return Result(
                    plan,
                    AdjustmentSimulationOutcome.Failed,
                    startedAt,
                    steps,
                    "A simulação falhou e foi encerrada sem alterações no Windows.");
            }
        }

        return Result(
            plan,
            AdjustmentSimulationOutcome.Completed,
            startedAt,
            steps,
            "Simulação concluída em memória. Nenhuma alteração do Windows foi aplicada.");
    }

    private bool IsPlanStillSimulationSafe(AdjustmentPlan plan)
    {
        if (plan.Items.Count == 0)
            return false;

        foreach (var item in plan.Items)
        {
            if (item.Status != PlanItemStatus.ReadyForSimulation ||
                !catalog.TryGet(item.AdjustmentId, out var definition) ||
                definition is null ||
                !compatibilityEvaluator.Evaluate(definition, plan.Platform).CanSimulate)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddRemaining(
        AdjustmentPlan plan,
        List<AdjustmentSimulationStep> steps,
        int firstIndex,
        string detail)
    {
        for (var index = firstIndex; index < plan.Items.Count; index++)
        {
            steps.Add(new(
                plan.Items[index].AdjustmentId,
                AdjustmentSimulationStepStatus.NotRun,
                detail));
        }
    }

    private static AdjustmentSimulationResult Result(
        AdjustmentPlan plan,
        AdjustmentSimulationOutcome outcome,
        DateTimeOffset startedAt,
        IReadOnlyList<AdjustmentSimulationStep> steps,
        string message) =>
        new(
            plan.Id,
            outcome,
            startedAt,
            DateTimeOffset.UtcNow,
            Array.AsReadOnly(steps.ToArray()),
            message,
            WindowsChanged: false);
}
