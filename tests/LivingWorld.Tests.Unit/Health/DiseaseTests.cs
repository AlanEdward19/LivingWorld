using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Health;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Health;

/// <summary>disease-epidemics: contágio determinístico por co-localização, cura com imunidade ou morte pela letalidade da doença,
/// surto por densidade, desligado no padrão do cenário.</summary>
public class DiseaseTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly CellCoord Here = new(1, 1);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static DiseaseRules Rules(
        double contagion = 1.0, double lethality = 0.0, int durationDays = 5, int immunityDays = 60,
        int threshold = int.MaxValue, double outbreakChance = 0.0) =>
        new(true, [new DiseaseProfile(1, contagion, lethality, durationDays, immunityDays)], threshold, outbreakChance);

    private static WorldState NewWorld(DiseaseRules? rules = null, ulong seed = 1) => new(
        Calendar, seed, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: ScenarioRunner.DefaultFamilyRules, diseaseRules: rules);

    private static Npc Add(WorldState world, CellCoord at)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25), new CultureId(1),
            at, null, null, household: null, health: 100, personality: Neutral, profession: new ProfessionType(1), currentLocation: at);
        world.AddNpc(npc);
        return npc;
    }

    private static RecordingSink Day(WorldState world)
    {
        world.CurrentDate = world.CurrentDate.AddHours(24);
        var sink = new RecordingSink();
        new OutbreakSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));
        return sink;
    }

    [Fact]
    public void The_default_scenario_has_no_disease_and_a_disabled_system_changes_nothing()
    {
        var world = NewWorld();
        var a = Add(world, Here);
        Add(world, Here);
        world.AddInfection(a.Id, 1, endTick: 1000);
        string before = WorldSnapshot.CanonicalHash(world);

        Day(world);

        Assert.False(world.DiseaseRules.Enabled);
        Assert.Single(world.Infections);
        Assert.Equal(1000, world.Infections[0].EndTick);
        Assert.NotNull(before);
    }

    [Fact]
    public void An_infected_npc_infects_a_susceptible_one_in_the_same_cell_but_not_in_another_cell_or_when_immune()
    {
        var world = NewWorld(Rules(contagion: 1.0, durationDays: 10));
        var sick = Add(world, Here);
        var neighbour = Add(world, Here);
        var distant = Add(world, new CellCoord(5, 5));
        var immune = Add(world, Here);
        world.AddInfection(sick.Id, 1, endTick: world.CurrentDate.TotalHours + 10 * 24);
        world.SetImmunity(immune.Id, 1, untilTick: world.CurrentDate.TotalHours + 1000);

        Day(world);

        Assert.True(world.IsInfected(neighbour.Id, 1));
        Assert.False(world.IsInfected(distant.Id, 1));
        Assert.False(world.IsInfected(immune.Id, 1));
    }

    [Fact]
    public void A_zero_contagion_never_spreads()
    {
        var world = NewWorld(Rules(contagion: 0.0, durationDays: 10));
        var sick = Add(world, Here);
        var neighbour = Add(world, Here);
        world.AddInfection(sick.Id, 1, endTick: world.CurrentDate.TotalHours + 10 * 24);

        for (int i = 0; i < 5; i++) Day(world);

        Assert.False(world.IsInfected(neighbour.Id, 1));
    }

    [Fact]
    public void The_same_world_gives_the_same_epidemic()
    {
        static List<(long, long)> Run()
        {
            var world = NewWorld(Rules(contagion: 0.3, lethality: 0.3, durationDays: 4), seed: 7);
            var first = Add(world, Here);
            for (int i = 0; i < 40; i++) Add(world, Here);
            world.AddInfection(first.Id, 1, endTick: world.CurrentDate.TotalHours + 4 * 24);
            for (int day = 0; day < 30; day++) Day(world);
            return world.Npcs.Select(n => (n.Id.Value, n.IsAlive ? 1L : 0L)).Concat(world.Infections.Select(i => (i.Npc.Value, i.EndTick))).ToList();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void An_infection_ends_in_immunity_that_blocks_reinfection_and_then_expires()
    {
        var world = NewWorld(Rules(contagion: 1.0, lethality: 0.0, durationDays: 2, immunityDays: 10));
        var sick = Add(world, Here);
        world.AddInfection(sick.Id, 1, endTick: world.CurrentDate.TotalHours + 2 * 24);

        Day(world); Day(world);
        Assert.False(world.IsInfected(sick.Id, 1)); // curado
        Assert.True(world.IsImmune(sick.Id, 1));

        var carrier = Add(world, Here);
        world.AddInfection(carrier.Id, 1, endTick: world.CurrentDate.TotalHours + 5 * 24);
        Day(world);
        Assert.False(world.IsInfected(sick.Id, 1)); // imune: não pega de novo

        for (int i = 0; i < 12; i++) Day(world);
        Assert.False(world.IsImmune(sick.Id, 1)); // a imunidade venceu
        Assert.DoesNotContain(world.Immunities, x => x.Npc == sick.Id);
    }

    [Fact]
    public void A_lethality_of_one_kills_at_the_end_with_a_disease_death_event()
    {
        var world = NewWorld(Rules(contagion: 0.0, lethality: 1.0, durationDays: 1));
        var sick = Add(world, Here);
        world.AddInfection(sick.Id, 1, endTick: world.CurrentDate.TotalHours + 24);

        var sink = Day(world);

        Assert.False(sick.IsAlive);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Disease && e.Payload == $"{sick.Id.Value}|1");
        Assert.Empty(world.Infections);
    }

    [Fact]
    public void An_outbreak_starts_only_in_a_city_over_the_threshold_and_logs_one_event()
    {
        var below = NewWorld(Rules(threshold: 5, outbreakChance: 1.0));
        for (int i = 0; i < 3; i++) Add(below, Here);
        Assert.DoesNotContain(Day(below).Events, e => e.Kind == WorldEventKind.OutbreakStarted);
        Assert.Empty(below.Infections);

        var over = NewWorld(Rules(threshold: 5, outbreakChance: 1.0));
        for (int i = 0; i < 8; i++) Add(over, Here);
        var sink = Day(over);

        Assert.Single(sink.Events, e => e.Kind == WorldEventKind.OutbreakStarted);
        Assert.Single(over.Infections);
    }

    [Fact]
    public void Mortality_of_a_finished_outbreak_is_within_half_to_one_and_a_half_times_the_declared_lethality()
    {
        var world = NewWorld(Rules(contagion: 1.0, lethality: 0.2, durationDays: 3, immunityDays: 360), seed: 3);
        var first = Add(world, Here);
        for (int i = 0; i < 399; i++) Add(world, Here);
        world.AddInfection(first.Id, 1, endTick: world.CurrentDate.TotalHours + 3 * 24);

        for (int day = 0; day < 12; day++) Day(world);

        int dead = world.Npcs.Count(n => !n.IsAlive);
        double mortality = dead / 400.0;
        Assert.InRange(mortality, 0.2 * 0.5, 0.2 * 1.5);
        Assert.Empty(world.Infections); // o surto terminou
    }

    [Fact]
    public void Infections_immunities_and_rules_survive_a_snapshot_round_trip_with_an_equal_hash()
    {
        var world = NewWorld(Rules(contagion: 0.4, lethality: 0.1));
        var a = Add(world, Here);
        var b = Add(world, Here);
        world.AddInfection(a.Id, 1, endTick: 500);
        world.SetImmunity(b.Id, 1, untilTick: 900);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.Infections, restored.Infections);
        Assert.Equal(world.Immunities, restored.Immunities);
        Assert.Equal(world.DiseaseRules.Diseases, restored.DiseaseRules.Diseases);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void The_village_with_a_moderate_disease_keeps_seventy_percent_of_the_control_population_over_five_years_in_eight_seeds()
    {
        var disease = new DiseaseRules(true, [new DiseaseProfile(1, 0.08, 0.15, 6, 180)], OutbreakPopulationThreshold: 20, OutbreakDailyChance: 0.01);
        long control = 0, sick = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var (plain, plainClock) = ScenarioRunner.Create(seed);
            plainClock.Run(plain, 5 * 12 * 30 * 24);
            control += plain.Npcs.Count(n => n.IsAlive);

            var (ill, illClock) = ScenarioRunner.Create(seed, diseaseRules: disease);
            illClock.Run(ill, 5 * 12 * 30 * 24);
            sick += ill.Npcs.Count(n => n.IsAlive);
        }

        Assert.True(sick >= 0.7 * control, $"população final com doença {sick} contra controle {control}");
    }
}
