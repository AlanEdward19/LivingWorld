using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Population.Body;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Body;

/// <summary>AD-139: hormônios como estado do corpo — puberdade, ciclo, janela fértil, amenorreia da fome, cólica na rotina e na
/// escolha, impulso da adolescência, sexo fora da união e a reação a ele, tristeza pós-parto e testosterona.</summary>
public class HormonesTests
{
    private static readonly WorldCalendar Calendar = ScenarioRunner.DefaultCalendar;
    private static readonly Personality Mid = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    /// <summary>Concepção certa em toda ovulação: isola o comportamento sob teste do sorteio de fertilidade.</summary>
    internal static readonly ReproductionRules Certain = ReproductionRules.Default with
    {
        FemaleMonthlyFecundability = AgeCurve.Of((0, 1.0)),
        FemaleSterileFraction = AgeCurve.Of((0, 0.0)),
        MaleFertility = AgeCurve.Of((0, 1.0)),
        MiscarriageRiskByAge = AgeCurve.Of((0, 0.0)),
        TwinRate = 0,
        PretermBands = [],
    };

    internal sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    internal static WorldState World(ReproductionRules? reproduction = null, UnionRules? unions = null, ulong seed = 7)
    {
        var family = ScenarioRunner.DefaultFamilyRules with
        {
            Unions = unions ?? UnionRules.Medieval,
            ConceptionRelationshipFloor = 0,
            ConceptionResourceFloor = new Dictionary<int, long>(),
            MaternalDeathRisk = 0,
            InfantDeathRisk = 0,
        };
        var population = ScenarioRunner.DefaultPopulationRules with { Reproduction = reproduction ?? Certain };
        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog, population,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: ScenarioRunner.DefaultEconomyRules, familyRules: family);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(80);
        world.AdvanceNpcIdTo(1); // id 0 não é pessoa para a vida interior
        return world;
    }

    internal static Npc Person(WorldState world, Sex sex, double ageYears, Personality? p = null, Npc? mother = null, Npc? father = null)
    {
        var id = world.NextNpcIdAndAdvance();
        var npc = new Npc(id, $"n{id.Value}", sex, world.CurrentDate.AddHours(-(long)(ageYears * Calendar.HoursPerYear)),
            new CultureId(1), new CellCoord(0, 0), mother?.Id, father?.Id, household: null, health: 100,
            personality: p ?? Mid, profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    internal static Household Home(WorldState world, params Npc[] members)
    {
        var home = new Household(world.NextHouseholdIdAndAdvance(), new CellCoord(0, 0), members[0].Id, members.Select(m => m.Id).ToList(),
            new Dictionary<ResourceType, long> { [new ResourceType(1)] = 120, [new ResourceType(2)] = 60 });
        world.AddHousehold(home);
        foreach (var m in members) m.JoinHousehold(home.Id);
        return home;
    }

    internal static void Wed(Npc a, Npc b)
    {
        a.Marry(b.Id);
        b.Marry(a.Id);
        a.SetUnion(new UnionState(Unions: 1));
        b.SetUnion(new UnionState(Unions: 1));
    }

    internal static TickContext Ctx(WorldState world, Sink? sink = null) => new(world, world.Rng, world.Scheduler, sink);

    private static void NextMonth(WorldState world) => world.CurrentDate = world.CurrentDate.AddHours(Calendar.HoursPerMonth);

    /// <summary>Meses de natalidade até a gravidez (ou o limite).</summary>
    private static int MonthsToConceive(WorldState world, Npc mother, int limit)
    {
        var natality = new NatalitySystem();
        for (int m = 0; m < limit; m++)
        {
            natality.Tick(world, Ctx(world));
            if (mother.PregnantUntil is not null) return m;
            NextMonth(world);
        }
        return int.MaxValue;
    }

    private static double PubertyAgeWith(double undernutrition)
    {
        var world = World();
        var girl = Person(world, Sex.Female, 9);
        girl.SetUndernutrition(undernutrition);
        for (int m = 0; m < 12 * 14 && girl.PubertyTick is null; m++)
        {
            NextMonth(world);
            Endocrine.MonthlyUpkeep(world, girl, world.CurrentDate.TotalHours);
        }
        return (girl.PubertyTick!.Value - girl.BirthDate.TotalHours) / (double)Calendar.HoursPerYear;
    }

    [Fact]
    public void Menarche_comes_at_the_individual_age_and_hunger_delays_it()
    {
        var r = EndocrineRules.Default;
        double fed = PubertyAgeWith(0), hungry = PubertyAgeWith(0.4); // mesma menina (mesma semente e id), com e sem fome

        Assert.InRange(fed, r.PubertyMinYears, r.PubertyMaxYears);
        Assert.InRange(hungry - fed, 0.4 * r.UndernutritionPubertyDelayYears - 0.15, 0.4 * r.UndernutritionPubertyDelayYears + 0.15);
    }

    [Fact]
    public void A_married_girl_does_not_conceive_before_menarche_and_conceives_on_her_ovulation_after_it()
    {
        int Months(double menarcheMean, out Couple w)
        {
            var reproduction = Certain with { Endocrine = EndocrineRules.Default with { FemalePubertyMeanYears = menarcheMean, PubertyStdDevYears = 0 } };
            var world = World(reproduction);
            var wife = Person(world, Sex.Female, 16.2);
            var husband = Person(world, Sex.Male, 22);
            Home(world, wife, husband);
            Wed(wife, husband);
            w = new Couple(world, wife);
            return MonthsToConceive(world, wife, 6);
        }

        int late = Months(18.5, out _), early = Months(13, out var conceived);
        var traits = Endocrine.TraitsOf(conceived.State, conceived.Wife);
        int dayOfConception = Endocrine.CycleDayAt(conceived.State, conceived.Wife, traits, conceived.Wife.Reproduction!.Pregnancy!.ConceivedTick);

        Assert.Equal((int.MaxValue, true), (late, early <= 1));
        Assert.Equal(traits.CycleDays - EndocrineRules.Default.LutealDays, dayOfConception); // a concepção cai na ovulação
    }

    private sealed record Couple(WorldState State, Npc Wife);

    [Fact]
    public void Hunger_stops_the_cycle_and_with_it_conception()
    {
        int Months(double undernutrition)
        {
            var world = World();
            var wife = Person(world, Sex.Female, 25);
            var husband = Person(world, Sex.Male, 27);
            Home(world, wife, husband);
            Wed(wife, husband);
            wife.SetUndernutrition(undernutrition);
            return MonthsToConceive(world, wife, 6);
        }

        Assert.Equal((int.MaxValue, true), (Months(0.6), Months(0) <= 1));
    }

    private static long FirstTickOnCycleDay(WorldState world, Npc woman, int day)
    {
        var traits = Endocrine.TraitsOf(world, woman);
        long tick = world.CurrentDate.TotalHours;
        while (Endocrine.CycleDayAt(world, woman, traits, tick) != day) tick += Calendar.HoursPerDay;
        return tick;
    }

    [Fact]
    public void Cramps_keep_a_woman_who_has_them_home_on_the_first_days_and_weigh_against_work_in_the_choice()
    {
        var reproduction = Certain with { Endocrine = EndocrineRules.Default with { DysmenorrheaShare = 1, CrampWorkWithdrawal = 1 } };
        var world = World(reproduction);
        var woman = Person(world, Sex.Female, 25);
        Home(world, woman);
        long cramp = FirstTickOnCycleDay(world, woman, 0), calm = FirstTickOnCycleDay(world, woman, 10);

        world.CurrentDate = WorldDate.Epoch(Calendar).AddHours(cramp);
        var routine = BehaviorDecisionSystem.MoodShapesRoutine(world, Ctx(world), woman, ActionType.Work, cramp);
        var withCramps = DecisionContextBuilder.Build(world, woman, cramp);
        double Work(DecisionContext c) => BehaviorDecisionSystem.ScoreOf(
            c, ActionType.Work, world.NeedsRules, world.EconomyRules, null, default, ForesightMechanic.EmptyPreviews);

        Assert.Equal(ActionType.Idle, routine);
        Assert.True(Work(withCramps) < Work(withCramps with { Inner = withCramps.Inner! with { Hormones = default } }));
        Assert.Equal((0.0, 0.0), (Endocrine.CrampWithdrawal(world, woman, calm), Endocrine.SnapshotOf(world, woman, calm).CrampWork));
    }

    [Fact]
    public void The_adolescent_drive_rises_after_puberty_peaks_and_matures_by_twenty_five()
    {
        var world = World();
        var boy = Person(world, Sex.Male, 13);
        boy.SetPuberty(boy.BirthDate.TotalHours + 14 * Calendar.HoursPerYear);
        double At(double age) => Endocrine.AdolescentDrive(world, boy, boy.BirthDate.TotalHours + (long)(age * Calendar.HoursPerYear));

        Assert.Equal((0.0, 0.5, 1.0, 0.0), (At(13.5), At(15), At(16), At(25)));
        Assert.InRange(At(20.5), 0.4, 0.6);
    }

    [Fact]
    public void Youth_raises_risk_taking_and_lowers_the_bar_for_a_partner()
    {
        var world = World();
        var teen = Person(world, Sex.Female, 17);
        var adult = Person(world, Sex.Female, 30);
        teen.SetPuberty(teen.BirthDate.TotalHours + 15 * Calendar.HoursPerYear);
        long now = world.CurrentDate.TotalHours;

        Assert.True(NatalitySystem.Temperament(world, teen, now) > 1.5 * NatalitySystem.Temperament(world, adult, now));
        Assert.True(CourtshipSystem.ThresholdOf(world, teen) < CourtshipSystem.ThresholdOf(world, adult));
    }

    private static (WorldState World, Npc Woman, Npc Man) Courting(UnionRules unions)
    {
        var world = World(unions: unions);
        var woman = Person(world, Sex.Female, 20);
        var man = Person(world, Sex.Male, 22);
        Home(world, woman);
        Home(world, man);
        woman.StartCourtship(man.Id);
        man.StartCourtship(woman.Id);
        return (world, woman, man);
    }

    [Fact]
    public void An_unmarried_courting_couple_can_conceive_only_when_sex_outside_union_exists()
    {
        var withIntimacy = UnionRules.Medieval with { Intimacy = IntimacyRules.Medieval with { CourtingPerMonth = 1 } };
        var (world, woman, man) = Courting(withIntimacy);
        int months = MonthsToConceive(world, woman, 3);
        var (chaste, chasteWoman, _) = Courting(UnionRules.Medieval with { Intimacy = IntimacyRules.Disabled });

        Assert.True(months <= 1);
        Assert.Equal(man.Id.Value, woman.Reproduction!.Pregnancy!.FatherId);
        Assert.Contains(world.Scheduler.Snapshot(), e => e.Payload!.StartsWith(NatalitySystem.ShowsPrefix, StringComparison.Ordinal));
        Assert.Equal(int.MaxValue, MonthsToConceive(chaste, chasteWoman, 3));
    }

    private static (WorldState World, Npc Mother, Npc Father, Npc Grandmother, Sink Sink) PregnantUnwed(Personality fatherTemper, bool fatherMarried)
    {
        var rules = UnionRules.Medieval with { Intimacy = IntimacyRules.Medieval with { ForcedMarriageShare = 1 } };
        var world = World(unions: rules);
        var grandmother = Person(world, Sex.Female, 45);
        var mother = Person(world, Sex.Female, 18, mother: grandmother);
        var father = Person(world, Sex.Male, 20, p: fatherTemper);
        Home(world, grandmother, mother);
        if (fatherMarried)
        {
            var wife = Person(world, Sex.Female, 20);
            Home(world, father, wife);
            Wed(father, wife);
        }
        else Home(world, father);
        long now = world.CurrentDate.TotalHours;
        mother.BecomePregnant(world.CurrentDate.AddDays(150));
        mother.SetReproduction(new ReproductiveState(Pregnancy: new PregnancyState(now - 110 * 24, now + 150 * 24, father.Id.Value, "F", 268, false)));
        var sink = new Sink();
        var ctx = Ctx(world, sink);
        var evt = ctx.ScheduleEvent(now, NatalitySystem.SystemName, $"{NatalitySystem.ShowsPrefix}{mother.Id.Value}|{father.Id.Value}");
        new NatalitySystem().HandleEvent(world, ctx, evt);
        return (world, mother, father, grandmother, sink);
    }

    [Fact]
    public void When_the_pregnancy_shows_a_single_loyal_father_is_made_to_marry_her()
    {
        var loyal = Mid with { Loyalty = 100 };
        var (_, mother, father, _, sink) = PregnantUnwed(loyal, fatherMarried: false);

        Assert.Equal(father.Id, mother.Spouse);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.UnwedPregnancy && e.Payload.EndsWith("|casamento"));
    }

    [Fact]
    public void A_married_father_leaves_her_and_shame_falls_on_her_and_her_parents()
    {
        var (world, mother, father, grandmother, sink) = PregnantUnwed(Mid, fatherMarried: true);
        long now = world.CurrentDate.TotalHours;

        Assert.Null(mother.Spouse);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.UnwedPregnancy && e.Payload.EndsWith("|abandono"));
        Assert.True(PsycheMechanic.EmotionOf(world, mother, Emotion.Shame, now) > 0.3);
        Assert.True(PsycheMechanic.Strongest(world, mother, LifeEventKind.Abandoned, now, father.Id.Value) > 0);
        Assert.True(PsycheMechanic.Strongest(world, grandmother, LifeEventKind.ChildUnwedPregnancy, now) > 0);
    }

    private static double GriefAfterBirth(double share)
    {
        var world = World(Certain with { Endocrine = EndocrineRules.Default with { PostpartumDepressionShare = share } });
        var wife = Person(world, Sex.Female, 25);
        var husband = Person(world, Sex.Male, 27);
        var home = Home(world, wife, husband);
        Wed(wife, husband);
        long now = world.CurrentDate.TotalHours;
        wife.BecomePregnant(world.CurrentDate);
        wife.SetReproduction(new ReproductiveState(Pregnancy: new PregnancyState(now - 268 * 24, now, husband.Id.Value, "F", 268, false)));
        var ctx = Ctx(world);
        var evt = ctx.ScheduleEvent(now, NatalitySystem.SystemName, $"{wife.Id.Value}|{husband.Id.Value}|{home.Id.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);
        return PsycheMechanic.EmotionOf(world, wife, Emotion.Grief, now + 30 * 24);
    }

    [Fact]
    public void Some_mothers_are_low_for_months_after_the_birth()
    {
        Assert.Equal((0.0, true), (GriefAfterBirth(0), GriefAfterBirth(1) > 0.2));
    }

    [Fact]
    public void Testosterone_makes_young_men_more_aggressive_than_women_and_boys()
    {
        var world = World();
        var man = Person(world, Sex.Male, 20);
        var woman = Person(world, Sex.Female, 20);
        var boy = Person(world, Sex.Male, 8);
        long now = world.CurrentDate.TotalHours;
        double Of(Npc n) => Endocrine.AggressionFactor(world, n, now);

        Assert.True(Of(man) > 1.5 * Of(woman) && Of(man) > 1.5 * Of(boy), $"{Of(man)} {Of(woman)} {Of(boy)}");
    }

    [Fact]
    public void Twins_and_very_young_mothers_are_born_preterm_more_often()
    {
        var rules = ReproductionRules.Default;
        double Preterm(double risk)
        {
            var rng = new WorldRng(11);
            int n = 0;
            for (int i = 0; i < 20_000; i++) if (rules.RollGestation(rng, risk).Band is not null) n++;
            return n / 20_000.0;
        }
        var world = World();
        var teen = Person(world, Sex.Female, 16);

        Assert.InRange(Preterm(NatalitySystem.PretermRisk(rules.Endocrine, teen, 16, twins: true)) / Preterm(1.0), 6.0, 12.0);
    }
}
