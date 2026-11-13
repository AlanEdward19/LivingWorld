using LivingWorld.Domain.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cognition;

/// <summary>AD-142 no mundo: desligado não escreve nada novo; ligado é determinístico, muda o mundo (o ganho decide), os NPCs
/// passam a diferir no que valorizam e o snapshot com cérebro reabre e segue idêntico.</summary>
[Trait("Category", "Scenario")]
public class BrainWorldTests
{
    private const int Days = 120;

    internal sealed record Observed(string Hash, int WithBrain, int WithLearnedGain, double GainSpread);

    private static (WorldState World, WorldClock Clock) World(ulong seed, bool brain)
    {
        var (world, clock) = ScenarioRunner.Create(seed);
        if (brain) world.UseBrain(BrainRules.Default);
        return (world, clock);
    }

    private static Observed Run(ulong seed, bool brain)
    {
        var (world, clock) = World(seed, brain);
        clock.Run(world, Days * world.Calendar.HoursPerDay);
        var brains = world.Npcs.Where(n => n.IsAlive && n.Brain is not null).Select(n => n.Brain!).ToList();
        var gains = brains.SelectMany(b => b.Gains).Select(g => (double)g).ToList();
        double mean = gains.Count == 0 ? 0 : gains.Average();
        double spread = gains.Count == 0 ? 0 : Math.Sqrt(gains.Average(g => (g - mean) * (g - mean)));
        return new Observed(WorldSnapshot.CanonicalHash(world), brains.Count, brains.Count(b => b.Gains.Any(g => g != 0)), spread);
    }

    /// <summary>Corrida compartilhada (ADR-0044): só valores observados.</summary>
    private static readonly Lazy<(Observed On, Observed Off)> Shared = new(() => (Run(3, brain: true), Run(3, brain: false)));

    [Fact]
    public void Shared_run_matches_an_isolated_run()
    {
        Assert.Equal(Run(3, brain: true).Hash, Shared.Value.On.Hash);
    }

    [Fact]
    public void Brain_off_writes_no_brain_state_and_no_rules_in_the_snapshot()
    {
        var (world, clock) = World(3, brain: false);
        clock.Run(world, 10 * world.Calendar.HoursPerDay);
        string json = WorldSnapshot.Serialize(world);

        Assert.DoesNotContain("\"BrainRules\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Brain\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_seed_with_brain_gives_the_same_world()
    {
        Assert.Equal(Run(3, brain: true).Hash, Run(3, brain: true).Hash);
    }

    [Fact]
    public void Different_seeds_with_brain_give_different_worlds()
    {
        Assert.NotEqual(Run(4, brain: true).Hash, Shared.Value.On.Hash);
    }

    [Fact]
    public void Learned_gains_change_the_world_they_decide_in()
    {
        Assert.NotEqual(Shared.Value.Off.Hash, Shared.Value.On.Hash);
    }

    [Fact]
    public void Npcs_come_to_differ_in_what_they_value()
    {
        var on = Shared.Value.On;

        Assert.True(on.WithLearnedGain > on.WithBrain / 2, $"{on.WithLearnedGain} de {on.WithBrain}");
        Assert.True(on.GainSpread > 1, $"desvio dos ganhos {on.GainSpread}");
    }

    [Fact]
    public void A_world_with_brains_reopens_from_the_snapshot_and_goes_on_identically()
    {
        var (world, clock) = World(5, brain: true);
        clock.Run(world, 60 * world.Calendar.HoursPerDay);
        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.True(reopened.BrainOn);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));

        clock.Run(world, 30 * world.Calendar.HoursPerDay);
        new WorldClock(ScenarioRunner.DefaultSystems(), clock.MaxIterationsPerTick).Run(reopened, 30 * world.Calendar.HoursPerDay);

        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));
    }
}
