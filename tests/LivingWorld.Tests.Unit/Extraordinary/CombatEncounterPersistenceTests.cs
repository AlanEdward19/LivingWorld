using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>REALISM-16: encontro persiste entre ticks sem resolver num único cálculo.</summary>
public sealed class CombatEncounterPersistenceTests
{
    [Fact]
    public void StartEncounter_creates_active_state_that_survives_a_tick_advance()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 99, ScenarioRunner.DefaultMap(99),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: ExtraordinaryScenarioData.Disabled);
        var attacker = Npc(new NpcId(1), 100);
        var defender = Npc(new NpcId(2), 80);
        world.AddNpc(attacker);
        world.AddNpc(defender);
        var sink = new RecordingSink();
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        var id = CombatEncounterSystem.StartEncounter(world, attacker.Id, defender.Id, 15, tick);
        Assert.Equal(CombatEncounterStatus.Active, world.FindCombatEncounter(id)!.Status);
        Assert.Equal(0, world.FindCombatEncounter(id)!.RoundsElapsed);

        // Avanço de relógio do mundo sem ProcessRound — estado permanece Active.
        world.CurrentDate = world.CurrentDate.AddHours(1);

        var still = world.FindCombatEncounter(id);
        Assert.NotNull(still);
        Assert.Equal(CombatEncounterStatus.Active, still.Status);
        Assert.Equal(0, still.RoundsElapsed);
        Assert.Equal(attacker.Id, still.Attacker);
        Assert.Equal(defender.Id, still.Defender);
        Assert.Equal(80, defender.Health);
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.CombatResolved);
    }

    [Fact]
    public void CombatEncounters_and_CombatRules_are_hasher_classified_canonical()
    {
        var encountersProp = typeof(WorldState).GetProperty(nameof(WorldState.CombatEncounters));
        var nextIdProp = typeof(WorldState).GetProperty(nameof(WorldState.NextCombatEncounterId));
        var rulesProp = typeof(WorldState).GetProperty(nameof(WorldState.CombatRules));
        Assert.NotNull(encountersProp);
        Assert.NotNull(nextIdProp);
        Assert.NotNull(rulesProp);
        Assert.NotEmpty(encountersProp!.GetCustomAttributes(typeof(CanonicalAttribute), false));
        Assert.NotEmpty(nextIdProp!.GetCustomAttributes(typeof(CanonicalAttribute), false));
        Assert.NotEmpty(rulesProp!.GetCustomAttributes(typeof(CanonicalAttribute), false));
        Assert.Empty(encountersProp.GetCustomAttributes(typeof(VolatileAttribute), false));
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
