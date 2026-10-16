using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>REALISM-33/34 — hospedeiro pode resistir possessão (P3 Possessão).</summary>
public sealed class PossessionResistanceTests
{
    [Fact]
    public void High_vitality_host_resists_possession_more_often_than_low_vitality_same_scenario()
    {
        const int seedCount = 80;
        const int maxTicks = 400;

        int highResists = CountSeedsWithResistWithin(seedCount, maxTicks, vitality: 95);
        int lowResists = CountSeedsWithResistWithin(seedCount, maxTicks, vitality: 5);

        Assert.True(highResists > lowResists,
            $"expected high vitality to resist more often (high={highResists}, low={lowResists})");
    }

    [Fact]
    public void Possession_resistance_fact_identifies_possessor()
    {
        var (world, carrier, target) = WorldWithPossession(seed: 7, hostVitality: 99);
        InvokePossess(world, carrier, target);

        var sink = new RecordingSink();
        var stateSystem = new ExtraordinaryStateSystem();
        for (int i = 0; i < 500 && ControlMechanic.IsPossessed(world, target); i++)
            stateSystem.Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        var fact = Assert.Single(world.Facts, f => f.Kind == WorldEventKind.PossessionResisted);
        Assert.Equal([target.Id, carrier.Id], fact.Participants);
        Assert.Equal($"{target.Id.Value}|{carrier.Id.Value}|possession-resisted", fact.Payload);
        Assert.Contains(sink.Events, evt =>
            evt.Kind == WorldEventKind.PossessionResisted
            && evt.Payload == fact.Payload
            && evt.SourceSystem == "ControlMechanic");
    }

    [Fact]
    public void Possession_resistance_is_deterministic_for_same_seed_and_vitality()
    {
        bool first = ResistsWithinTicks(seed: 19, hostVitality: 90, maxTicks: 300);
        bool second = ResistsWithinTicks(seed: 19, hostVitality: 90, maxTicks: 300);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Possession_resistance_clears_possessed_by_without_ceasing_possessor_manifestation()
    {
        var (world, carrier, target) = WorldWithPossession(seed: 11, hostVitality: 99);
        InvokePossess(world, carrier, target);
        Assert.True(ControlMechanic.IsPossessed(world, target));

        var stateSystem = new ExtraordinaryStateSystem();
        for (int i = 0; i < 500 && ControlMechanic.IsPossessed(world, target); i++)
            stateSystem.Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Null(TargetState(world, target).PossessedBy);
        Assert.True(CarrierState(world, carrier).IsManifested);
    }

    private static int CountSeedsWithResistWithin(int seedCount, int maxTicks, double vitality)
    {
        int count = 0;
        for (int seed = 0; seed < seedCount; seed++)
        {
            if (ResistsWithinTicks(seed, vitality, maxTicks))
                count++;
        }

        return count;
    }

    private static bool ResistsWithinTicks(int seed, double hostVitality, int maxTicks)
    {
        var (world, carrier, target) = WorldWithPossession(seed, hostVitality);
        InvokePossess(world, carrier, target);
        if (!ControlMechanic.IsPossessed(world, target)) return false;

        var stateSystem = new ExtraordinaryStateSystem();
        for (int i = 0; i < maxTicks; i++)
        {
            stateSystem.Tick(world, new TickContext(world, world.Rng, world.Scheduler));
            if (!ControlMechanic.IsPossessed(world, target))
                return world.Facts.Any(f => f.Kind == WorldEventKind.PossessionResisted);
        }

        return false;
    }

    private static void InvokePossess(WorldState world, Npc carrier, Npc target)
    {
        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(501, carrier.Id, "test-power", target.Id));
        Assert.True(result.IsSuccess, result.Error);
    }

    private static ExtraordinaryCarrierState CarrierState(WorldState world, Npc npc) =>
        world.ExtraordinaryCarriers.First(item => item.CarrierId == npc.Id);

    private static ExtraordinaryCarrierState TargetState(WorldState world, Npc npc) =>
        world.ExtraordinaryCarriers.First(item => item.CarrierId == npc.Id);

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPossession(
        int seed, double hostVitality)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", ["control.possess:Sleep"], "Active", [], "Guaranteed",
            [], [], [], []);
        var carrierState = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, (ulong)seed, ScenarioRunner.DefaultMap((ulong)seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [carrierState]);
        var carrier = Npc(new NpcId(1), vitality: 50);
        var target = Npc(new NpcId(2), vitality: hostVitality);
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static Npc Npc(NpcId id, double vitality) => new(
        id, "n", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: 100,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0),
        vitality: vitality);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
