using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>Independent Test (spec P2 Combate) + teto de rounds declarado no cenário.</summary>
public sealed class CombatEncounterIndependentTests
{
    [Fact]
    public void Independent_test_two_npcs_log_shows_distinct_rounds_before_resolution()
    {
        // Spec Independent Test: 2 NPCs — log mostra múltiplos rounds distintos antes da
        // resolução final, nunca um único CombatResolved no mesmo tick do início.
        var rules = new CombatRules(MaxRounds: 8, FleeHealthThreshold: 0, FleeProbability: 0);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 4242, ScenarioRunner.DefaultMap(4242),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: ExtraordinaryScenarioData.Disabled,
            combatRules: rules);
        var a = Npc(new NpcId(1), 100);
        var b = Npc(new NpcId(2), 100);
        world.AddNpc(a);
        world.AddNpc(b);
        var sink = new RecordingSink();
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        var id = CombatEncounterSystem.StartEncounter(world, a.Id, b.Id, magnitude: 8, tick);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatEncounterStarted);
        // No mesmo instante do início: ainda sem CombatResolved.
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.CombatResolved);

        CombatRoundOutcome outcome = CombatRoundOutcome.Continuing;
        for (int i = 0; i < rules.MaxRounds + 2 && outcome == CombatRoundOutcome.Continuing; i++)
        {
            world.CurrentDate = world.CurrentDate.AddHours(1);
            outcome = CombatEncounterSystem.ProcessRound(world, id, tick);
        }

        var rounds = sink.Events.Where(e => e.Kind == WorldEventKind.CombatRound).ToList();
        Assert.True(rounds.Count >= 2, $"esperava ≥2 rounds distintos; got {rounds.Count}");
        var roundNumbers = rounds
            .Select(e => e.Payload.Split('|')[1])
            .Distinct()
            .ToList();
        Assert.True(roundNumbers.Count >= 2, "payloads de round devem distinguir rounds diferentes");
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatResolved);
        Assert.NotEqual(CombatEncounterStatus.Active, world.FindCombatEncounter(id)!.Status);
    }

    [Fact]
    public void Scenario_declared_max_rounds_is_never_exceeded()
    {
        var rules = new CombatRules(MaxRounds: 4, FleeHealthThreshold: 0, FleeProbability: 0);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 11, ScenarioRunner.DefaultMap(11),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: ExtraordinaryScenarioData.Disabled,
            combatRules: rules);
        var a = Npc(new NpcId(1), 100);
        var b = Npc(new NpcId(2), 100);
        world.AddNpc(a);
        world.AddNpc(b);
        var sink = new RecordingSink();
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);
        var id = CombatEncounterSystem.StartEncounter(world, a.Id, b.Id, magnitude: 1, tick);

        for (int i = 0; i < 20; i++)
        {
            var outcome = CombatEncounterSystem.ProcessRound(world, id, tick);
            Assert.True(world.FindCombatEncounter(id)!.RoundsElapsed <= rules.MaxRounds);
            if (outcome != CombatRoundOutcome.Continuing) break;
        }

        Assert.True(world.FindCombatEncounter(id)!.RoundsElapsed <= rules.MaxRounds);
        Assert.NotEqual(CombatEncounterStatus.Active, world.FindCombatEncounter(id)!.Status);
        var roundEvents = sink.Events.Count(e => e.Kind == WorldEventKind.CombatRound);
        Assert.True(roundEvents <= rules.MaxRounds);
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
