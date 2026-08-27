using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>Trava o contrato AD-010: strike imediato vs engage inicia encontro.</summary>
public sealed class CombatStrikeEngageContractTests
{
    [Fact]
    public void Strike_remains_immediate_single_shot_and_does_not_create_encounter()
    {
        var (world, carrier, target) = WorldWithPower(["combat.strike:20"]);
        var sink = new RecordingSink();
        int healthBefore = target.Health;

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(401, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotEqual(healthBefore, target.Health);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatResolved);
        Assert.Empty(world.CombatEncounters);
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.CombatEncounterStarted);
    }

    [Fact]
    public void Engage_starts_persistent_encounter_without_immediate_strike_damage()
    {
        var (world, carrier, target) = WorldWithPower(["combat.engage:20"]);
        var sink = new RecordingSink();
        int healthBefore = target.Health;

        var result = ExtraordinaryInvocationEngine.Invoke(
            world, new TickContext(world, world.Rng, world.Scheduler, sink),
            new ExtraordinaryInvocation(402, carrier.Id, "test-power", target.Id));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(healthBefore, target.Health);
        var encounter = Assert.Single(world.CombatEncounters);
        Assert.Equal(CombatEncounterStatus.Active, encounter.Status);
        Assert.Equal(carrier.Id, encounter.Attacker);
        Assert.Equal(target.Id, encounter.Defender);
        Assert.Equal(0, encounter.RoundsElapsed);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatEncounterStarted);
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.CombatResolved);
    }

    [Fact]
    public void Engage_prefix_is_distinct_from_strike_per_ad010()
    {
        Assert.Equal("combat.strike:", CombatMechanic.StrikePrefix);
        Assert.Equal("combat.engage:", CombatMechanic.EngagePrefix);
        Assert.NotEqual(CombatMechanic.StrikePrefix, CombatMechanic.EngagePrefix);
        Assert.IsType<CombatMechanic>(ExtraordinaryMechanicRegistry.Default.Resolve("combat.engage:12"));
    }

    private static (WorldState World, Npc Carrier, Npc Target) WorldWithPower(
        IReadOnlyList<string> effects)
    {
        var descriptor = new PowerDescriptor(
            "test-power", "test-source", effects, "Active", [], "Guaranteed",
            [], [], [], []);
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]), extraordinaryCarriers: [state]);
        var carrier = Npc(new NpcId(1), 100);
        var target = Npc(new NpcId(2), 80);
        world.AddNpc(carrier);
        world.AddNpc(target);
        return (world, carrier, target);
    }

    private static Npc Npc(NpcId id, int health) => new(
        id, "n", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: health,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
