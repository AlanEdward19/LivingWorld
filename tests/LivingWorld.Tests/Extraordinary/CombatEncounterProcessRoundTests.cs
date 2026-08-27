using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

/// <summary>REALISM-17/18/24/25 — ProcessRound: dano acumulado, morte, fuga, base sem poder.</summary>
public sealed class CombatEncounterProcessRoundTests
{
    [Fact]
    public void ProcessRound_accumulates_damage_across_rounds_with_dodge_or_block_via_resolver()
    {
        var (world, attacker, defender, id, sink) = WorldWithEncounter(
            magnitude: 10, attackerHp: 100, defenderHp: 100,
            combatRules: new CombatRules(MaxRounds: 20, FleeHealthThreshold: 0, FleeProbability: 0));
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        var first = CombatEncounterSystem.ProcessRound(world, id, tick);
        Assert.Equal(CombatRoundOutcome.Continuing, first);
        int hpAfter1 = defender.Health + attacker.Health;
        Assert.True(hpAfter1 < 200, "pelo menos um lado deve ter chance de dano/esquiva via Resolver");
        Assert.Equal(1, world.FindCombatEncounter(id)!.RoundsElapsed);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatRound && e.Payload.Contains("|1|"));

        var second = CombatEncounterSystem.ProcessRound(world, id, tick);
        Assert.True(second is CombatRoundOutcome.Continuing or CombatRoundOutcome.Resolved);
        Assert.True(world.FindCombatEncounter(id)!.RoundsElapsed >= 1);
        // Dano acumula sobre a vida já reduzida (não reseta).
        Assert.True(defender.Health + attacker.Health <= hpAfter1);
    }

    [Fact]
    public void ProcessRound_resolves_death_immediately_when_health_reaches_zero()
    {
        // Magnitude alta + HP baixo → morte no primeiro round bem-sucedido.
        var (world, attacker, defender, id, sink) = WorldWithEncounter(
            magnitude: 80, attackerHp: 100, defenderHp: 5,
            combatRules: new CombatRules(MaxRounds: 10, FleeHealthThreshold: 0, FleeProbability: 0));
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        CombatRoundOutcome outcome = CombatRoundOutcome.Continuing;
        for (int i = 0; i < 10 && outcome == CombatRoundOutcome.Continuing; i++)
            outcome = CombatEncounterSystem.ProcessRound(world, id, tick);

        Assert.Equal(CombatRoundOutcome.Resolved, outcome);
        Assert.Equal(CombatEncounterStatus.Resolved, world.FindCombatEncounter(id)!.Status);
        Assert.True(!attacker.IsAlive || !defender.IsAlive);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Death);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatResolved);
    }

    [Fact]
    public void ProcessRound_flee_below_threshold_ends_without_death()
    {
        var (world, attacker, defender, id, sink) = WorldWithEncounter(
            magnitude: 1, attackerHp: 100, defenderHp: 10,
            combatRules: new CombatRules(MaxRounds: 20, FleeHealthThreshold: 50, FleeProbability: 1.0));
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        var outcome = CombatEncounterSystem.ProcessRound(world, id, tick);

        Assert.Equal(CombatRoundOutcome.Fled, outcome);
        Assert.Equal(CombatEncounterStatus.Fled, world.FindCombatEncounter(id)!.Status);
        Assert.True(attacker.IsAlive);
        Assert.True(defender.IsAlive);
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.Death);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatResolved && e.Payload.Contains("Fled"));
    }

    [Fact]
    public void ProcessRound_works_with_extraordinary_disabled()
    {
        var (world, _, _, id, sink) = WorldWithEncounter(
            magnitude: 10, attackerHp: 100, defenderHp: 100,
            combatRules: new CombatRules(MaxRounds: 8, FleeHealthThreshold: 0, FleeProbability: 0),
            extraordinaryEnabled: false);
        Assert.False(world.Extraordinary.Enabled);
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        var outcome = CombatEncounterSystem.ProcessRound(world, id, tick);

        Assert.Equal(CombatRoundOutcome.Continuing, outcome);
        Assert.Equal(1, world.FindCombatEncounter(id)!.RoundsElapsed);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CombatRound);
    }

    [Fact]
    public void ProcessRound_round_cap_forces_resolution_never_infinite()
    {
        var rules = new CombatRules(MaxRounds: 3, FleeHealthThreshold: 0, FleeProbability: 0);
        var (world, attacker, defender, id, sink) = WorldWithEncounter(
            magnitude: 1, attackerHp: 100, defenderHp: 100, combatRules: rules);
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        CombatRoundOutcome last = CombatRoundOutcome.Continuing;
        for (int i = 0; i < 10; i++)
        {
            last = CombatEncounterSystem.ProcessRound(world, id, tick);
            if (last != CombatRoundOutcome.Continuing) break;
        }

        Assert.Equal(CombatRoundOutcome.Resolved, last);
        Assert.True(world.FindCombatEncounter(id)!.RoundsElapsed <= rules.MaxRounds);
        Assert.Equal(CombatEncounterStatus.Resolved, world.FindCombatEncounter(id)!.Status);
        Assert.True(attacker.IsAlive && defender.IsAlive); // exaustão, não morte
        Assert.Contains(sink.Events, e => e.Payload.Contains("Exhaustion"));
    }

    [Fact]
    public void ProcessRound_on_non_active_encounter_is_noop()
    {
        var (world, attacker, defender, id, sink) = WorldWithEncounter(
            magnitude: 80, attackerHp: 100, defenderHp: 5,
            combatRules: new CombatRules(MaxRounds: 10, FleeHealthThreshold: 0, FleeProbability: 0));
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);

        CombatRoundOutcome outcome = CombatRoundOutcome.Continuing;
        for (int i = 0; i < 10 && outcome == CombatRoundOutcome.Continuing; i++)
            outcome = CombatEncounterSystem.ProcessRound(world, id, tick);

        Assert.Equal(CombatEncounterStatus.Resolved, world.FindCombatEncounter(id)!.Status);
        int rounds = world.FindCombatEncounter(id)!.RoundsElapsed;
        int atkHp = attacker.Health;
        int defHp = defender.Health;
        int eventCount = sink.Events.Count;

        var again = CombatEncounterSystem.ProcessRound(world, id, tick);

        Assert.Equal(CombatRoundOutcome.Resolved, again);
        Assert.Equal(CombatEncounterStatus.Resolved, world.FindCombatEncounter(id)!.Status);
        Assert.Equal(rounds, world.FindCombatEncounter(id)!.RoundsElapsed);
        Assert.Equal(atkHp, attacker.Health);
        Assert.Equal(defHp, defender.Health);
        Assert.Equal(eventCount, sink.Events.Count);
    }

    private static (
        WorldState World, Npc Attacker, Npc Defender, CombatEncounterId Id, RecordingSink Sink)
        WorldWithEncounter(
            int magnitude, int attackerHp, int defenderHp, CombatRules combatRules,
            bool extraordinaryEnabled = true)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 77, ScenarioRunner.DefaultMap(77),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: extraordinaryEnabled
                ? new ExtraordinaryScenarioData(true, [])
                : ExtraordinaryScenarioData.Disabled,
            combatRules: combatRules);
        var attacker = Npc(new NpcId(1), attackerHp);
        var defender = Npc(new NpcId(2), defenderHp);
        world.AddNpc(attacker);
        world.AddNpc(defender);
        var sink = new RecordingSink();
        var tick = new TickContext(world, world.Rng, world.Scheduler, sink);
        var id = CombatEncounterSystem.StartEncounter(world, attacker.Id, defender.Id, magnitude, tick);
        return (world, attacker, defender, id, sink);
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
