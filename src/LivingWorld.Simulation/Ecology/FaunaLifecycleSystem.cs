using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Ecology;

/// <summary>Ciclo de vida base da fauna — fome, reprodução e predação (Fase 16.4). Roda com
/// <c>Extraordinary.Enabled == false</c>; poderes modulam, não substituem (REALISM-06).</summary>
public sealed class FaunaLifecycleSystem : ISimulationSystem
{
    public const string SystemName = "fauna-lifecycle";
    public const double PredationEnergyGain = 40;
    /// <summary>Teto leve (REALISM-19): evita explosão O(n²) de reprodução em horizontes longos.</summary>
    public const int MaxAliveFauna = 400;

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

    /// <summary>REALISM-03: par elegível (mesma espécie, raio, energia) gera filhote próximo.</summary>
    public static void TryReproduce(WorldState world, TickContext ctx)
    {
        if (world.Fauna.Count(a => a.IsAlive) >= MaxAliveFauna)
            return;
        if (!world.AnimalSpeciesRules.Any(r => r.ReproduceProbability > 0))
            return;

        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;
        var alive = world.Fauna.Where(a => a.IsAlive).OrderBy(a => a.Id.Value).ToList();
        var used = new HashSet<long>();

        for (int i = 0; i < alive.Count; i++)
        {
            var a = alive[i];
            if (used.Contains(a.Id.Value)) continue;
            if (!rulesBySpecies.TryGetValue(a.Species, out var rules)) continue;
            if (rules.ReproduceProbability <= 0) continue;
            if (a.Energy.ValueAt(tick) < rules.ReproduceEnergyThreshold) continue;

            for (int j = i + 1; j < alive.Count; j++)
            {
                var b = alive[j];
                if (used.Contains(b.Id.Value)) continue;
                if (!string.Equals(a.Species, b.Species, StringComparison.Ordinal)) continue;
                if (b.Energy.ValueAt(tick) < rules.ReproduceEnergyThreshold) continue;
                if (Chebyshev(a.Position, b.Position) > rules.ReproduceRadius) continue;

                double roll = ctx.StreamFor("fauna-reproduce", PairStreamId(a.Id.Value, b.Id.Value, tick))
                    .NextDouble();
                used.Add(a.Id.Value);
                used.Add(b.Id.Value);
                if (roll >= rules.ReproduceProbability) break;

                var childPos = BirthPosition(world, ctx, a.Position, b.Position, a.Id.Value, b.Id.Value, tick);
                var childId = world.NextAnimalIdAndAdvance();
                var child = new Animal(
                    childId,
                    a.Species,
                    childPos,
                    true,
                    null,
                    LazyNeed.Initial(100, tick, rules.HungerDecayPerTick));
                world.AddAnimal(child);
                ctx.LogEvent(
                    WorldEventKind.Birth,
                    $"{a.Id.Value}|{b.Id.Value}|{childId.Value}",
                    sourceSystem: SystemName);
                break;
            }
        }
    }

    private static CellCoord BirthPosition(
        WorldState world, TickContext ctx, CellCoord a, CellCoord b, long idA, long idB, long tick)
    {
        int midX = (a.X + b.X) / 2;
        int midY = (a.Y + b.Y) / 2;
        var candidates = new List<CellCoord>(9);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var cell = new CellCoord(midX + dx, midY + dy);
                if (world.Map.TryGetCell(cell, out _))
                    candidates.Add(cell);
            }

        if (candidates.Count == 0)
            return a;

        candidates.Sort((left, right) =>
        {
            int cmp = left.X.CompareTo(right.X);
            return cmp != 0 ? cmp : left.Y.CompareTo(right.Y);
        });
        int index = (int)(ctx.StreamFor("fauna-birth-pos", PairStreamId(idA, idB, tick)).NextDouble() * candidates.Count);
        if (index >= candidates.Count) index = candidates.Count - 1;
        return candidates[index];
    }

    /// <summary>REALISM-04: predador consome presa no raio; sem PredatorOf é no-op.</summary>
    public static void TryPredate(WorldState world, TickContext ctx)
    {
        if (!world.AnimalSpeciesRules.Any(r =>
                !string.IsNullOrEmpty(r.PredatorOf) && r.PredationProbability > 0))
            return;

        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;
        var alive = world.Fauna.Where(a => a.IsAlive).OrderBy(a => a.Id.Value).ToList();
        var eaten = new HashSet<long>();

        foreach (var predator in alive)
        {
            if (eaten.Contains(predator.Id.Value)) continue;
            if (!rulesBySpecies.TryGetValue(predator.Species, out var rules)) continue;
            if (string.IsNullOrEmpty(rules.PredatorOf) || rules.PredationProbability <= 0) continue;

            foreach (var prey in alive)
            {
                if (prey.Id.Value == predator.Id.Value) continue;
                if (eaten.Contains(prey.Id.Value)) continue;
                if (!string.Equals(prey.Species, rules.PredatorOf, StringComparison.Ordinal)) continue;
                if (Chebyshev(predator.Position, prey.Position) > rules.ReproduceRadius) continue;

                double roll = ctx.StreamFor("fauna-predate", PairStreamId(predator.Id.Value, prey.Id.Value, tick))
                    .NextDouble();
                if (roll >= rules.PredationProbability) continue;

                var currentPredator = world.FindAnimal(predator.Id) ?? predator;
                double gained = currentPredator.Energy.ValueAt(tick) + PredationEnergyGain;
                world.ReplaceAnimal(currentPredator with
                {
                    Energy = currentPredator.Energy.WithValue(gained, tick),
                });
                Kill(world, ctx, prey, WorldEventKind.Death);
                eaten.Add(prey.Id.Value);
                break;
            }
        }
    }

    internal static void Kill(WorldState world, TickContext ctx, Animal animal, WorldEventKind kind)
    {
        if (!animal.IsAlive) return;
        world.ReplaceAnimal(animal with { IsAlive = false, DeathTick = ctx.CurrentTick });
        ctx.LogEvent(kind, animal.Id.Value.ToString(), sourceSystem: SystemName);
    }

    internal static Dictionary<string, AnimalSpeciesRules> IndexRules(WorldState world) =>
        world.AnimalSpeciesRules
            .GroupBy(r => r.Species, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    public static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static long PairStreamId(long idA, long idB, long tick) =>
        unchecked(idA * 1_000_000_000L + idB * 1_000_000L + tick);
}
