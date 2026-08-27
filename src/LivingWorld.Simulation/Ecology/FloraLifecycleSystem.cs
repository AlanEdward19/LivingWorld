using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Ciclo de vida base da flora — avanço de estágio por temperatura/estação (Fase 16.4).
/// Roda com <c>Extraordinary.Enabled == false</c>; <c>flora.growth-rate</c> multiplica a taxa
/// de base, nunca a substitui (REALISM-07/08/11).</summary>
public sealed class FloraLifecycleSystem : ISimulationSystem
{
    public const string SystemName = "flora-lifecycle";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        AdvanceStage(world, ctx);
    }

    /// <summary>REALISM-07/08/11: avança (ou reverte) estágio conforme temperatura efetiva;
    /// poder multiplica a taxa de base.</summary>
    public static void AdvanceStage(WorldState world, TickContext ctx)
    {
        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;

        foreach (var plant in world.Flora.OrderBy(p => p.Id.Value).ToList())
        {
            if (!rulesBySpecies.TryGetValue(plant.Species, out var rules))
                continue;

            double baseRate = BaseGrowthRate(world, plant, rules, tick);
            double multiplier = FloraMechanic.GrowthRateMultiplier(world, plant);
            int delta = (int)Math.Floor(baseRate * multiplier);
            if (delta == 0)
                continue;

            int nextStage = plant.GrowthStage + delta;
            if (nextStage < 0)
            {
                Kill(world, ctx, plant);
                continue;
            }

            world.ReplacePlant(plant with { GrowthStage = nextStage });
            if (plant.GrowthStage < rules.MaturityStage && nextStage >= rules.MaturityStage)
            {
                ctx.LogEvent(
                    WorldEventKind.PlantMatured,
                    plant.Id.Value.ToString(),
                    sourceSystem: SystemName);
            }
        }
    }

    /// <summary>Taxa de base: positiva dentro da faixa (escala com conforto térmico);
    /// negativa fora da faixa (reverte — nunca avança normalmente).</summary>
    public static double BaseGrowthRate(
        WorldState world, Plant plant, PlantSpeciesRules rules, long currentTick)
    {
        float temp = EnvironmentTemperatureMechanic.EffectiveTemperature(
            world, plant.Position, currentTick);

        if (temp < rules.MinToleratedTemp || temp > rules.MaxToleratedTemp)
            return -1;

        float mid = (rules.MinToleratedTemp + rules.MaxToleratedTemp) / 2f;
        float half = (rules.MaxToleratedTemp - rules.MinToleratedTemp) / 2f;
        if (half <= 0)
            return 1;

        double comfort = 1.0 - Math.Abs(temp - mid) / half;
        // 1.0 na borda da faixa, 2.0 no centro — estações distintas mudam a taxa inteira.
        return 1.0 + Math.Clamp(comfort, 0, 1);
    }

    internal static void Kill(WorldState world, TickContext ctx, Plant plant)
    {
        world.RemovePlant(plant.Id);
        ctx.LogEvent(WorldEventKind.Death, plant.Id.Value.ToString(), sourceSystem: SystemName);
    }

    internal static Dictionary<string, PlantSpeciesRules> IndexRules(WorldState world) =>
        world.PlantSpeciesRules
            .GroupBy(r => r.Species, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
}
