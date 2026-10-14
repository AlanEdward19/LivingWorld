using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ontogeny;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ontogeny;

/// <summary>Fase 21b (ontogeny-acquisition): exposição ao cuidador, janela crítica com perda parcial permanente e gene como taxa.
/// Mundos pequenos com só o <see cref="MilestoneProgressSystem"/> no relógio: cada braço difere em uma única coisa.</summary>
public class OntogenyAcquisitionTests
{
    private const int Seeds = 20;
    private const long Month = 30L * 24;
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly CellCoord Home = default;

    private static double RateOf(int seed) => RateGene.RollInitial(new WorldRng((ulong)seed)).Value;

    private sealed class Scene(WorldState world, WorldClock clock, Npc child, Household household, Npc adult)
    {
        public WorldState World { get; } = world;
        public Npc Child { get; } = child;
        public Npc Adult { get; } = adult;

        public void RunMonths(int months) => clock.Run(World, months * Month);

        public void AdultJoins()
        {
            household.AddMember(Adult.Id);
            Adult.JoinHousehold(household.Id);
        }
    }

    /// <summary>Criança recém-nascida num domicílio; o adulto entra no domicílio já (<paramref name="caregiverPresent"/>) ou só depois (<see cref="Scene.AdultJoins"/>).</summary>
    private static Scene Build(int seed, double rate, bool caregiverPresent, OntogenyRules? rules = null)
    {
        var (world, _) = ScenarioRunner.Create((ulong)seed, initialPopulation: 0, ontogenyRules: rules ?? OntogenyDefaults.Human);
        var clock = new WorldClock([new MilestoneProgressSystem()]);

        var adult = new Npc(
            world.NextNpcIdAndAdvance(), "cuidador", Sex.Female, world.CurrentDate.AddYears(-30), new CultureId(1), Home, null, null, null, 100,
            Neutral, new ProfessionType(1), Home);
        var child = new Npc(
            world.NextNpcIdAndAdvance(), "crianca", Sex.Male, world.CurrentDate, new CultureId(1), Home, null, null, null, 100,
            Neutral, new ProfessionType(1), Home, rateGene: new RateGene(rate));
        world.AddNpc(adult);
        world.AddNpc(child);
        child.SetDevelopment(DevelopmentState.Newborn);

        var members = caregiverPresent ? new List<NpcId> { adult.Id, child.Id } : [child.Id];
        var household = new Household(world.NextHouseholdIdAndAdvance(), Home, members[0], members);
        world.AddHousehold(household);
        child.JoinHousehold(household.Id);
        if (caregiverPresent) adult.JoinHousehold(household.Id);
        return new Scene(world, clock, child, household, adult);
    }

    private static double Score(Npc npc) =>
        npc.Development is not { } d ? 6.0 : Enum.GetValues<DevelopmentAxis>().Sum(d.Get);

    private static int Attained(WorldState world, Npc npc) =>
        world.OntogenyRules.Milestones.Count(m => npc.Development is null || npc.Development.Get(m.Axis) >= m.Threshold);

    // --- Critério 4: convívio é causal ---

    [Fact]
    public void A_child_without_a_caregiver_reaches_fewer_milestones_than_one_with_in_every_seed()
    {
        int fewer = 0;
        for (int seed = 1; seed <= Seeds; seed++)
        {
            double rate = RateOf(seed);
            var cared = Build(seed, rate, caregiverPresent: true);
            var neglected = Build(seed, rate, caregiverPresent: false);
            cared.RunMonths(8 * 12);
            neglected.RunMonths(8 * 12);
            if (Attained(neglected.World, neglected.Child) < Attained(cared.World, cared.Child)) fewer++;
        }
        Assert.True(fewer >= 18, $"{fewer}/{Seeds}");
    }

    // --- Critério 5: janela crítica falseável nos dois lados ---

    private static (double OnTime, double Late, double None) ThreeArms(int seed, OntogenyRules? rules = null)
    {
        double rate = RateOf(seed);
        var onTime = Build(seed, rate, true, rules);
        var late = Build(seed, rate, false, rules);
        var none = Build(seed, rate, false, rules);
        late.RunMonths(36);
        late.AdultJoins();
        foreach (var arm in new[] { onTime, none }) arm.RunMonths(36);
        foreach (var arm in new[] { onTime, late, none }) arm.RunMonths(15 * 12 - 36);
        return (Score(onTime.Child), Score(late.Child), Score(none.Child));
    }

    [Fact]
    public void Window_order_is_strictly_on_time_over_late_over_none_and_late_never_reaches_on_time()
    {
        int ordered = 0;
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var (onTime, late, none) = ThreeArms(seed);
            if (onTime > late && late > none) ordered++;
            Assert.True(late < onTime, $"seed {seed}: tardia {late} alcançou no prazo {onTime}");
        }
        Assert.True(ordered >= 18, $"{ordered}/{Seeds}");
    }

    [Fact]
    public void Mutation_without_window_closure_late_exposure_recovers_everything_and_the_criterion_fails()
    {
        // Mesmos marcos, janelas empurradas para depois do horizonte: nenhuma fecha, o teto nunca cai.
        var open = OntogenyRules.Create(
            true, OntogenyDefaults.HumanMilestones.Select(m => m with { WindowLimitYears = 99 }).ToList(),
            OntogenyDefaults.HumanRequirements).Value!;

        for (int seed = 1; seed <= Seeds; seed++)
        {
            var (onTime, late, _) = ThreeArms(seed, open);
            Assert.True(late >= onTime - 0.05, $"seed {seed}: sem fechamento a tardia ({late}) devia alcançar o prazo ({onTime})");
        }
    }

    [Fact]
    public void The_ceiling_only_ever_goes_down_and_progress_never_exceeds_it()
    {
        var scene = Build(5, 1.0, caregiverPresent: false);
        var ceilings = new List<double[]>();
        for (int month = 0; month < 12 * 12; month++)
        {
            if (month == 36) scene.AdultJoins();
            scene.RunMonths(1);
            if (scene.Child.Development is not { } d) break;
            ceilings.Add(Enum.GetValues<DevelopmentAxis>().Select(d.CeilingOf).ToArray());
            foreach (var axis in Enum.GetValues<DevelopmentAxis>())
                Assert.True(d.Get(axis) <= d.CeilingOf(axis) + 1e-9);
        }

        for (int i = 1; i < ceilings.Count; i++)
            for (int a = 0; a < 6; a++)
                Assert.True(ceilings[i][a] <= ceilings[i - 1][a] + 1e-12, "o teto subiu");
        Assert.True(ceilings[^1].Any(c => c < 1.0), "a janela perdida devia ter reduzido algum teto");
    }

    // --- Critério 6: gene muda a taxa, exposição idêntica ---

    [Fact]
    public void Identical_genes_and_exposure_end_byte_identical_and_different_genes_acquire_at_different_ages()
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            double rate = RateOf(seed);
            var a = Build(seed, rate, true);
            var b = Build(seed, rate, true);
            a.RunMonths(5 * 12);
            b.RunMonths(5 * 12);
            Assert.Equal(a.Child.Development, b.Child.Development);

            var fast = Build(seed, rate * 1.25, true);
            var slow = Build(seed, rate, true);
            Assert.NotEqual(MonthsToFirstWords(fast), MonthsToFirstWords(slow));
        }
    }

    private static int MonthsToFirstWords(Scene scene)
    {
        for (int month = 1; month <= 12 * 8; month++)
        {
            scene.RunMonths(1);
            if (scene.Child.Development is not { } d || d.Language >= 0.4) return month;
        }
        return -1;
    }

    // --- Dinâmica pura ---

    [Fact]
    public void Without_exposure_nothing_is_gained_and_with_it_the_age_curve_is_followed()
    {
        var rules = OntogenyDefaults.Human;
        var none = rules.Advance(DevelopmentState.Newborn, 0.0, 0.5, exposed: false);
        var fed = rules.Advance(DevelopmentState.Newborn, 0.0, 0.5, exposed: true);

        Assert.Equal(0, none.Language);
        Assert.Equal(rules.ProgressAt(DevelopmentAxis.Language, 0.5), fed.Language, 9);
    }

    [Fact]
    public void Invalid_recovery_and_keep_are_rejected_with_their_field_names()
    {
        var ms = OntogenyDefaults.HumanMilestones;
        var req = OntogenyDefaults.HumanRequirements;
        Assert.Contains("LateRecoveryFactor", OntogenyRules.Create(true, ms, req, lateRecoveryFactor: 0).Error);
        Assert.Contains("MissedWindowKeep", OntogenyRules.Create(true, ms, req, missedWindowKeep: 1).Error);
    }
}
