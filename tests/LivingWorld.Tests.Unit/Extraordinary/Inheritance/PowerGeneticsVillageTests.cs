using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Extraordinary.Inheritance;

/// <summary>AD-156 na vila padrão com os poderes do cenário de referência ligados (15 anos, uma seed, simulada duas vezes):
/// mesmo mundo com a mesma seed, nascidos herdam alelos, e todo poder que alguém tem vem do próprio genótipo (sem acúmulo).</summary>
[Trait("Category", "Scenario")]
public sealed class PowerGeneticsVillageTests
{
    private const long Years = 15;

    private sealed class Counter : IWorldEventSink
    {
        public int Inherited { get; private set; }
        public void Record(WorldEvent evt) { if (evt.Kind == WorldEventKind.PowerInherited) Inherited++; }
    }

    private static readonly Lazy<(string Hash, string Again, bool AllBacked, int Inherited, int Carriers)> Run = new(() =>
    {
        var counter = new Counter();
        var (world, clock) = ScenarioRunner.Create(3, extraordinary: PowerGeneticsReport.ReferenceScenario());
        var observed = new WorldClock(clock.Systems, clock.MaxIterationsPerTick, counter);
        observed.Run(world, Years * world.Calendar.HoursPerYear);
        var (again, againClock) = ScenarioRunner.Create(3, extraordinary: PowerGeneticsReport.ReferenceScenario());
        againClock.Run(again, Years * again.Calendar.HoursPerYear);
        bool backed = world.ExtraordinaryCarriers.All(c => world.FindNpc(c.CarrierId) is not { } npc
            || c.PowerIds.All(id => PowerGenome.Find(world, id) is { } locus && PowerGeneticsMechanic.HasAffectedGenotype(world, npc, locus)));
        return (WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(again), backed, counter.Inherited,
            world.ExtraordinaryCarriers.Count);
    });

    [Fact]
    public void Same_seed_with_powers_on_produces_the_same_world()
    {
        Assert.Equal(Run.Value.Hash, Run.Value.Again);
    }

    [Fact]
    public void Every_power_a_villager_has_comes_from_their_own_alleles()
    {
        Assert.True(Run.Value.AllBacked && Run.Value.Carriers > 0, $"portadores: {Run.Value.Carriers}");
    }

    [Fact]
    public void Newborns_inherit_power_alleles_in_the_village()
    {
        Assert.True(Run.Value.Inherited > 0);
    }
}
