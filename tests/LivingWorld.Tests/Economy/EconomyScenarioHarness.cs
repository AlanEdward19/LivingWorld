using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T25 (ECON-25/28): decorator de teste que escala a produção de um recurso a
/// partir de um tick declarado — nunca um segundo cenário C# hardcoded (mesmo
/// <see cref="ScenarioRunner.DefaultSystems"/>/<c>Create</c>, só um sistema extra depois de
/// <see cref="ProductionSystem"/> na lista). Usa o delta de <see cref="WorldState.ResourceProduced"/>
/// entre ticks (T24) pra saber quanto foi produzido *neste* tick e retira a fração cortada do
/// estoque do(s) <see cref="Workplace"/> que a recebeu — resultado equivalente a produção menor,
/// sem duplicar a lógica de <see cref="ProductionSystem"/>.</summary>
public sealed class ProductionMultiplierDecorator(ResourceType resource, double multiplier, long fromTick) : ISimulationSystem
{
    private long _lastProduced;

    public string Name => "test-production-multiplier";
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        long currentTotal = world.ResourceProduced.GetValueOrDefault(resource);
        long producedThisTick = currentTotal - _lastProduced;
        _lastProduced = currentTotal;

        if (ctx.CurrentTick < fromTick || producedThisTick <= 0) return;

        long reduceBy = (long)(producedThisTick * (1 - multiplier));
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (reduceBy <= 0) break;
            long take = Math.Min(workplace.Stock.GetValueOrDefault(resource), reduceBy);
            if (take <= 0) continue;
            workplace.Withdraw(resource, take);
            reduceBy -= take;
        }
    }
}

public static class EconomyScenarioHarness
{
    /// <summary>Base (multiplier 1.0) ou tratamento (multiplier &lt; 1.0) sobre a mesma seed —
    /// insere o decorator logo depois de <see cref="ProductionSystem"/> na lista padrão.
    /// Capacidade do recurso reduzida pra uma ordem de grandeza comparável à demanda real da
    /// população — a capacidade default (T20, calibrada pra nunca faltar comida) satura os dois
    /// braços no mesmo teto e esconde qualquer diferença de preço (achado rodando o teste na
    /// prática); só o teto muda, nenhuma outra regra do cenário default é duplicada
    /// (ECON-28).</summary>
    public static (WorldState World, WorldClock Clock) Create(
        ulong seed, ResourceType resource, double productionMultiplier, long fromTick, int initialPopulation = 20)
    {
        var scarceRules = ScenarioRunner.DefaultEconomyRules with
        {
            CapacityByResourceLocation = ScenarioRunner.DefaultEconomyRules.CapacityByResourceLocation
                .ToDictionary(kv => kv.Key, kv => kv.Key.ResourceId == resource.Id ? initialPopulation : kv.Value),
        };
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: initialPopulation, economyRules: scarceRules);

        var systems = new List<ISimulationSystem>();
        foreach (var system in ScenarioRunner.DefaultSystems())
        {
            systems.Add(system);
            if (system.Name == ProductionSystem.SystemName)
                systems.Add(new ProductionMultiplierDecorator(resource, productionMultiplier, fromTick));
        }

        return (world, new WorldClock(systems));
    }
}
