using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Ciclo de vida base da fauna — fome, reprodução e predação (Fase 16.4). Roda com
/// <c>Extraordinary.Enabled == false</c>; poderes modulam, não substituem (REALISM-06).</summary>
public sealed class FaunaLifecycleSystem : ISimulationSystem
{
    public const string SystemName = "fauna-lifecycle";
    public const double PredationEnergyGain = 40;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        ApplyHunger(world, ctx);
        TryReproduce(world, ctx);
        TryPredate(world, ctx);
    }

    /// <summary>REALISM-01/02: decai energia por espécie; energia 0 → morte + evento causal.</summary>
    public static void ApplyHunger(WorldState world, TickContext ctx)
    {
        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;

        foreach (var animal in world.Fauna.Where(a => a.IsAlive).OrderBy(a => a.Id.Value).ToList())
        {
            if (!rulesBySpecies.TryGetValue(animal.Species, out var rules))
                continue;

            var energy = animal.Energy;
            if (Math.Abs(energy.DecayRatePerTick - rules.HungerDecayPerTick) > double.Epsilon)
                energy = energy.WithDecayRate(rules.HungerDecayPerTick, tick);

            if (energy.ValueAt(tick) > 0)
            {
                if (energy != animal.Energy)
                    world.ReplaceAnimal(animal with { Energy = energy });
                continue;
            }

            Kill(world, ctx, animal with { Energy = energy }, WorldEventKind.Starvation);
        }
    }

    /// <summary>REALISM-03 — implementado em T5.</summary>
    public static void TryReproduce(WorldState world, TickContext ctx)
    {
        _ = (world, ctx);
    }

    /// <summary>REALISM-04 — implementado em T6.</summary>
    public static void TryPredate(WorldState world, TickContext ctx)
    {
        _ = (world, ctx);
    }

    internal static void Kill(WorldState world, TickContext ctx, Animal animal, WorldEventKind kind)
    {
        if (!animal.IsAlive) return;
        world.ReplaceAnimal(animal with { IsAlive = false });
        ctx.LogEvent(kind, animal.Id.Value.ToString(), sourceSystem: SystemName);
    }

    internal static Dictionary<string, AnimalSpeciesRules> IndexRules(WorldState world) =>
        world.AnimalSpeciesRules
            .GroupBy(r => r.Species, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    internal static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
