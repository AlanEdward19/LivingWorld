using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Shared.Economy;

/// <summary>Decorator causal que escala somente o incremento de estoque produzido pelo sistema
/// envolvido. Medir o contador global confundia colheita com comida criada por casamento/processos
/// e, por rodar antes de CropSystem, aplicava o corte com um dia de atraso.</summary>
public sealed class ProductionMultiplierDecorator(
    ResourceType resource, double multiplier, long fromTick, ISimulationSystem inner) : ISimulationSystem
{
    public string Name => "test-production-multiplier";
    public TickFrequency Frequency => inner.Frequency;

    public void Tick(WorldState world, TickContext ctx)
    {
        var stockBefore = world.Workplaces.ToDictionary(
            workplace => workplace.Id,
            workplace => workplace.Stock.GetValueOrDefault(resource));

        inner.Tick(world, ctx);

        if (ctx.CurrentTick < fromTick || multiplier >= 1) return;

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            long producedHere = workplace.Stock.GetValueOrDefault(resource)
                                - stockBefore.GetValueOrDefault(workplace.Id);
            long reduceBy = (long)(Math.Max(0, producedHere) * (1 - multiplier));
            if (reduceBy > 0)
                workplace.Withdraw(resource, reduceBy);
        }
    }
}

public static class EconomyScenarioHarness
{
    public static (WorldState World, WorldClock Clock) CreateControlledFamineScenario(
        ulong seed, ResourceType resource, double productionMultiplier, long fromTick, int initialPopulation)
    {
        var scenario = Create(seed, resource, productionMultiplier, fromTick, initialPopulation);
        var world = scenario.World;
        var farm = world.Workplaces.Single(workplace => workplace.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);

        foreach (var household in world.Households)
            household.JoinCity(household.City, farm.Location);
        foreach (var npc in world.Npcs.Where(npc => npc.IsAlive))
            npc.MoveTo(farm.Location, world.CurrentDate.TotalHours);

        var farmers = world.Npcs
            .Where(npc => npc.IsAlive && npc.Profession.Id == 1)
            .OrderBy(npc => npc.Id.Value)
            .Take(40)
            .ToArray();
        foreach (var farmer in farmers)
        {
            Assert.True(farm.Hire(farmer.Id).IsSuccess);
            farmer.Hire(farm.Id);
        }
        Assert.Equal(40, farmers.Length);
        return scenario;
    }

    public static (WorldState World, WorldClock Clock) CreateControlledPriceScenario(
        ulong seed, ResourceType resource, double productionMultiplier, long fromTick, int initialPopulation)
    {
        var (world, _) = Create(seed, resource, productionMultiplier, fromTick, initialPopulation);
        var farm = world.Workplaces.Single(workplace => workplace.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
        foreach (var npc in world.Npcs.Where(npc => npc.IsAlive))
            npc.MoveTo(farm.Location, world.CurrentDate.TotalHours);

        var workers = world.Npcs
            .Where(npc => npc.IsAlive && npc.Profession.Id == 1)
            .OrderBy(npc => npc.Id.Value)
            .Take(10)
            .ToArray();

        foreach (var worker in workers)
        {
            Assert.True(farm.Hire(worker.Id).IsSuccess);
            worker.Hire(farm.Id);
            worker.MoveTo(farm.Location, world.CurrentDate.TotalHours);
        }

        Assert.Equal(10, workers.Length);
        ISimulationSystem crop = new ProductionMultiplierDecorator(
            resource, productionMultiplier, fromTick, new CropSystem());
        return (world, new WorldClock([crop, new MarketPricingSystem()]));
    }

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
                .ToDictionary(kv => kv.Key, kv => kv.Key.ResourceId == resource.Id
                    ? Math.Max(1, initialPopulation / 2)
                    : kv.Value),
        };
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: initialPopulation, economyRules: scarceRules);

        // O buffer default de 50 por pessoa cobre aproximadamente um mês e mascara uma quebra
        // desde t0. Dez unidades POR PESSOA evitam que o próprio controle nasça em fome, mas
        // ainda se esgotam no braço sem colheita dentro da janela causal.
        foreach (var household in world.Households.OrderBy(household => household.Id.Value))
        {
            long food = household.Stock.GetValueOrDefault(resource);
            long causalBuffer = 10L * household.Members.Count;
            if (food > causalBuffer)
                household.Withdraw(resource, food - causalBuffer);
        }

        var systems = new List<ISimulationSystem>();
        foreach (var system in ScenarioRunner.DefaultSystems())
        {
            systems.Add(system.Name == CropSystem.SystemName
                ? new ProductionMultiplierDecorator(resource, productionMultiplier, fromTick, system)
                : system);
        }

        return (world, new WorldClock(systems));
    }
}
