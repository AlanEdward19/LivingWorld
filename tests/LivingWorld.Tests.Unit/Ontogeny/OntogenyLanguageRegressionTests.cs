using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ontogeny;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ontogeny;

/// <summary>Fase 21c (ontogeny-language-regression): o idioma vem de quem cria, a doença regride o que ainda não se consolidou (com causa
/// no mesmo tick) e o pool agregado só guarda adultos. Mundos pequenos com só o <see cref="MilestoneProgressSystem"/> no relógio.</summary>
public class OntogenyLanguageRegressionTests
{
    private const int Seeds = 20;
    private const long Month = 30L * 24;
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly CellCoord Home = default;
    private const int CultureA = 1, CultureB = 2;

    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent e) => Events.Add(e);
    }

    private sealed class Scene(WorldState world, WorldClock clock, Npc child, Sink sink)
    {
        public WorldState World { get; } = world;
        public Npc Child { get; } = child;
        public Sink Sink { get; } = sink;
        public void RunMonths(int months) => clock.Run(World, months * Month);
    }

    /// <summary>Criança de cultura A com cuidadores das culturas dadas (um adulto por entrada).</summary>
    private static Scene Build(int seed, params int[] caregiverCultures)
    {
        var (world, _) = ScenarioRunner.Create((ulong)seed, initialPopulation: 0, ontogenyRules: OntogenyDefaults.Human);
        var sink = new Sink();
        var clock = new WorldClock([new MilestoneProgressSystem()], sink: sink);

        var child = new Npc(
            world.NextNpcIdAndAdvance(), "crianca", Sex.Male, world.CurrentDate, new CultureId(CultureA), Home, null, null, null, 100,
            Neutral, new ProfessionType(1), Home, rateGene: RateGene.RollInitial(new WorldRng((ulong)seed)));
        world.AddNpc(child);
        child.SetDevelopment(DevelopmentState.Newborn);

        var members = new List<NpcId>();
        foreach (int culture in caregiverCultures)
        {
            var adult = new Npc(
                world.NextNpcIdAndAdvance(), "cuidador", Sex.Female, world.CurrentDate.AddYears(-30), new CultureId(culture), Home, null, null, null,
                100, Neutral, new ProfessionType(1), Home);
            world.AddNpc(adult);
            members.Add(adult.Id);
        }
        members.Add(child.Id);
        var household = new Household(world.NextHouseholdIdAndAdvance(), Home, members[0], members);
        world.AddHousehold(household);
        foreach (var id in members) world.FindNpc(id)!.JoinHousehold(household.Id);
        return new Scene(world, clock, child, sink);
    }

    // --- Critério 7: o idioma vem de quem cria ---

    [Fact]
    public void Nobody_is_born_speaking_any_language()
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var scene = Build(seed, CultureB);
            Assert.True(scene.Child.Development!.Fluency is null || scene.Child.Development.Fluency.Entries.Count == 0);
            Assert.Equal(0, scene.Child.Development.Language);
        }
    }

    [Fact]
    public void An_orphan_raised_by_another_culture_ends_more_fluent_in_the_caregivers_language_than_in_the_birth_culture()
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var scene = Build(seed, CultureB);
            scene.RunMonths(6 * 12);

            var fluency = scene.Child.Development!.Fluency!;
            Assert.True(fluency.LevelOf(CultureB) > fluency.LevelOf(CultureA), $"seed {seed}");
            Assert.Equal(CultureB, OntogenyMechanic.SpokenCulture(scene.Child).Id);
            Assert.NotEqual(scene.Child.Culture, OntogenyMechanic.SpokenCulture(scene.Child));
        }
    }

    [Fact]
    public void Several_caregivers_split_the_fluency_by_their_languages()
    {
        var scene = Build(3, CultureB, CultureB, CultureA);
        scene.RunMonths(6 * 12);

        var fluency = scene.Child.Development!.Fluency!;
        Assert.True(fluency.LevelOf(CultureB) > fluency.LevelOf(CultureA));
        Assert.True(fluency.LevelOf(CultureA) > 0);
        Assert.Equal(scene.Child.Development.Language, fluency.Entries.Sum(e => e.Level), 6);
    }

    [Fact]
    public void The_language_is_kept_after_development_completes_and_courtship_reads_it_instead_of_the_birth_culture()
    {
        var scene = Build(4, CultureB);
        scene.RunMonths(22 * 12);

        Assert.Null(scene.Child.Development);
        Assert.Equal(CultureB, scene.Child.NativeLanguage!.Value.Id);

        var sameLanguage = new Npc(
            new NpcId(9001), "b", Sex.Female, scene.World.CurrentDate.AddYears(-20), new CultureId(CultureB), Home, null, null, null, 100,
            Neutral, new ProfessionType(1), Home);
        var sameBirthCulture = new Npc(
            new NpcId(9002), "a", Sex.Female, scene.World.CurrentDate.AddYears(-20), new CultureId(CultureA), Home, null, null, null, 100,
            Neutral, new ProfessionType(1), Home);
        Assert.Equal(1.0, CourtshipSystem.CulturalAffinityOf(scene.Child, sameLanguage));
        Assert.Equal(0.0, CourtshipSystem.CulturalAffinityOf(scene.Child, sameBirthCulture));
    }

    [Fact]
    public void Mutation_ignoring_the_caregivers_language_leaves_no_fluency_and_the_criterion_fails()
    {
        var rules = OntogenyDefaults.Human;
        var withLanguages = rules.Advance(DevelopmentState.Newborn, 0, 1.0, exposed: true, caregiverCultures: [CultureB]);
        var without = rules.Advance(DevelopmentState.Newborn, 0, 1.0, exposed: true, caregiverCultures: null);

        Assert.True(withLanguages.Fluency!.LevelOf(CultureB) > 0);
        Assert.Null(without.Fluency);
    }

    // --- Critério 8: regressão datável e com causa ---

    private static List<double[]> RunWithIllness(Scene scene, int fromMonth, int toMonth, int disease, int months)
    {
        var history = new List<double[]>();
        for (int month = 0; month < months; month++)
        {
            if (month == fromMonth) scene.World.AddInfection(scene.Child.Id, disease, long.MaxValue);
            if (month == toMonth) scene.World.RemoveInfection(scene.Child.Id, disease);
            scene.RunMonths(1);
            history.Add(scene.Child.Development is { } d ? Enum.GetValues<DevelopmentAxis>().Select(d.Get).ToArray() : Enumerable.Repeat(1.0, 6).ToArray());
        }
        return history;
    }

    [Fact]
    public void Every_drop_in_a_milestone_has_a_named_disease_event_in_the_same_month_and_none_happens_without_illness()
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var scene = Build(seed, CultureA);
            var history = RunWithIllness(scene, fromMonth: 30, toMonth: 36, disease: 7, months: 10 * 12);

            int drops = 0;
            for (int month = 1; month < history.Count; month++)
            {
                bool dropped = Enumerable.Range(0, 6).Any(a => history[month][a] < history[month - 1][a] - 1e-12);
                var events = scene.Sink.Events.Where(e => e.Kind == WorldEventKind.MilestoneRegressed && e.Tick / Month == month + 1).ToList(); // o sistema mensal dispara na virada do mês
                bool inIllness = month is >= 30 and < 36;

                Assert.Equal(dropped, events.Count > 0);
                if (dropped)
                {
                    drops++;
                    Assert.True(inIllness, $"seed {seed}: queda fora da doença no mês {month}");
                    Assert.All(events, e => Assert.EndsWith("|7", e.Payload));
                }
            }
            Assert.True(drops > 0, $"seed {seed}: a doença devia ter regredido algum marco");
        }
    }

    [Fact]
    public void A_consolidated_milestone_is_a_floor_and_a_recent_one_is_lost()
    {
        var rules = OntogenyDefaults.Human;
        // Aos 3 anos o marco 'walk' (limite 1,5) está consolidado; o progresso de Language ainda está dentro de janela aberta.
        var state = rules.StateAt(3.0);
        var sick = rules.Regress(state, 3.0);

        double walk = rules.Milestones.Single(m => m.Id == "walk").Threshold;
        Assert.True(sick.GrossMotor >= walk);
        Assert.True(sick.GrossMotor < state.GrossMotor);
        Assert.True(sick.Language < state.Language);

        var onlyFloor = rules.Regress(rules.StateAt(0.4), 0.4);
        Assert.All(Enum.GetValues<DevelopmentAxis>(), a => Assert.True(onlyFloor.Get(a) <= rules.StateAt(0.4).Get(a)));
    }

    [Fact]
    public void Regression_scales_the_fluency_with_the_language_progress()
    {
        var rules = OntogenyDefaults.Human;
        var state = rules.Advance(DevelopmentState.Newborn, 0, 2.0, true, [CultureB]);
        var sick = rules.Regress(state, 2.0);

        Assert.Equal(sick.Language, sick.Fluency!.Entries.Sum(e => e.Level), 6);
    }

    // --- Critério 9 (adaptado): o pool agregado só tem adultos ---

    [Fact]
    public void Materialized_people_are_adults_so_the_aggregate_pool_never_needs_a_development_state()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 40);
        clock.Run(world, 10L * 360 * 24);

        foreach (var npc in world.Npcs.Where(n => n.MaterializedAtTick is not null))
        {
            Assert.True(npc.AgeYears(world.CurrentDate) >= 18);
            Assert.Null(npc.Development);
        }
    }

    [Fact]
    public void Invalid_regression_loss_is_rejected_by_name()
    {
        Assert.Contains("RegressionLoss", OntogenyRules.Create(true, OntogenyDefaults.HumanMilestones, OntogenyDefaults.HumanRequirements, regressionLoss: 1).Error);
        Assert.Contains("RegressionLoss", OntogenyScenarioLoader.Load("""{"Ontogeny":{"Enabled":true,"RegressionLoss":"x"}}""").Error);
    }
}
