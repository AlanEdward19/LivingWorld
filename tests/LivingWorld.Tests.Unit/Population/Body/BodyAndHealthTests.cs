using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Health;
using LivingWorld.Simulation.Population.Body;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population.Body;

/// <summary>AD-134: corpo que cresce, genética quantitativa com segregação, saúde dinâmica e amamentação.</summary>
public class BodyAndHealthTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly GrowthRules Growth = GrowthRules.Default;
    private static readonly Personality SomePersonality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static WorldState BuildWorld(NeedsRules? needs = null)
    {
        var world = new WorldState(
            Calendar, seed: 3, ScenarioRunner.DefaultMap(3), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, needs ?? ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(100);
        return world;
    }

    private static Npc Person(WorldState world, long id, Sex sex, double ageYears, HouseholdId? household = null,
        NpcId? mother = null, int health = 100, int? gestation = null)
    {
        var npc = new Npc(
            new NpcId(id), $"p{id}", sex, world.CurrentDate.AddHours(-(long)(ageYears * Calendar.HoursPerYear)), new CultureId(1),
            new CellCoord(1, 1), mother, null, household, health, SomePersonality, ProfessionType.None, new CellCoord(1, 1));
        if (gestation is { } days) npc.SetGestationAtBirth(days);
        npc.ConfigureNeedDecay(world.NeedsRules, world.CurrentDate.TotalHours);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void A_newborn_at_term_measures_about_half_a_metre_and_three_kilos()
    {
        double height = Growth.HeightAt(Sex.Male, 0, Growth.MaleAdultHeightMean, 268);
        double weight = Growth.WeightAt(Sex.Male, 0, height, 1.0, 0, 268);

        Assert.True(height is > 0.47 and < 0.53 && weight is > 2.9 and < 3.7, $"{height} m, {weight} kg");
    }

    [Fact]
    public void Children_grow_by_age_and_a_five_year_old_is_below_one_metre_fifteen()
    {
        double five = Growth.HeightAt(Sex.Male, 5, Growth.MaleAdultHeightMean, null);
        double one = Growth.HeightAt(Sex.Male, 1, Growth.MaleAdultHeightMean, null);

        Assert.True(one < five && five < 1.15, $"{one} → {five}");
    }

    [Fact]
    public void Adult_men_are_taller_and_more_muscular_than_adult_women()
    {
        double man = Growth.HeightAt(Sex.Male, 30, Growth.MaleAdultHeightMean, null);
        double woman = Growth.HeightAt(Sex.Female, 30, Growth.FemaleAdultHeightMean, null);
        double manMuscle = Growth.MuscleTarget(Sex.Male, 30, Growth.WeightAt(Sex.Male, 30, man, 1, 0, null), 0);
        double womanMuscle = Growth.MuscleTarget(Sex.Female, 30, Growth.WeightAt(Sex.Female, 30, woman, 1, 0, null), 0);

        Assert.True(man - woman is > 0.08 and < 0.15 && manMuscle > womanMuscle * 1.3, $"{man - woman} m, {manMuscle}/{womanMuscle} kg");
    }

    [Fact]
    public void The_growth_spurt_peaks_earlier_in_girls_than_in_boys()
    {
        static double PeakAge(AgeCurve curve)
        {
            double best = 0, bestAge = 0;
            for (double age = 8; age < 18; age += 0.25)
                if (curve.At(age + 0.25) - curve.At(age) is var gain && gain > best) (best, bestAge) = (gain, age);
            return bestAge;
        }

        Assert.True(PeakAge(Growth.FemaleHeightFraction) + 1 < PeakAge(Growth.MaleHeightFraction));
    }

    [Fact]
    public void Old_people_shrink_and_lose_muscle()
    {
        double at40 = Growth.HeightAt(Sex.Male, 40, 1.71, null), at80 = Growth.HeightAt(Sex.Male, 80, 1.71, null);
        double m30 = Growth.MuscleTarget(Sex.Male, 30, 64, 0), m70 = Growth.MuscleTarget(Sex.Male, 70, 64, 0);

        Assert.True(at80 < at40 - 0.02 && m70 < m30 * 0.85, $"{at40}→{at80} m, {m30}→{m70} kg");
    }

    [Fact]
    public void Undernutrition_while_growing_costs_adult_stature_and_a_fed_child_loses_none()
    {
        Assert.Equal(0.0, Growth.StuntingLoss(Sex.Female, 6, 1.0 / 12, 1.59, 0.0));
        Assert.True(Growth.StuntingLoss(Sex.Female, 6, 1.0 / 12, 1.59, 0.8) > 0);
    }

    [Fact]
    public void Monthly_growth_makes_a_hungry_child_shorter_as_an_adult_than_a_fed_twin()
    {
        var world = BuildWorld();
        var fed = Person(world, 1, Sex.Male, 4);
        var hungry = Person(world, 2, Sex.Male, 4);
        fed.SetStature(0, 1.71);
        hungry.SetStature(0, 1.71);
        hungry.SetUndernutrition(0.8);

        for (int month = 0; month < 36; month++)
        {
            long tick = world.CurrentDate.TotalHours + month * Calendar.HoursPerMonth;
            BodyGrowth.MonthlyUpdate(world, fed, tick);
            BodyGrowth.MonthlyUpdate(world, hungry, tick);
        }

        Assert.True(hungry.AdultHeight < fed.AdultHeight - 0.01, $"{hungry.AdultHeight} vs {fed.AdultHeight}");
    }

    [Fact]
    public void Physical_work_builds_muscle_and_disuse_loses_it()
    {
        var world = BuildWorld();
        var npc = Person(world, 1, Sex.Male, 30);
        npc.SetStature(0, 1.71);
        npc.SetBody(1.71, 64, 25);
        long tick = world.CurrentDate.TotalHours;
        for (int month = 0; month < 24; month++, tick += Calendar.HoursPerMonth)
        {
            for (int day = 0; day < Calendar.DaysPerMonth; day++) npc.AddPhysicalWorkDay();
            BodyGrowth.MonthlyUpdate(world, npc, tick);
        }

        double trained = npc.MuscleMass;
        for (int month = 0; month < 24; month++, tick += Calendar.HoursPerMonth)
            BodyGrowth.MonthlyUpdate(world, npc, tick);

        // o alvo depende da compleição do NPC: o treino leva acima do sedentário e o desuso tira de volta mais que isso
        double sedentary = Growth.MuscleTarget(Sex.Male, 34, npc.Weight, 0);
        Assert.True(trained > sedentary + 1.0 && npc.MuscleMass < trained - 1.5, $"25 → {trained} → {npc.MuscleMass} (sedentário {sedentary})");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mendelian_inheritance_keeps_the_population_variance_across_generations_and_blending_loses_it(bool mendelian)
    {
        var rng = new WorldRng(42);
        var genetics = mendelian ? QuantitativeGeneticsRules.Default : QuantitativeGeneticsRules.Blending;
        var family = ScenarioRunner.DefaultFamilyRules;
        var population = Enumerable.Range(0, 2000).Select(_ => HeredityService.RollInitialVitality(rng)).ToList();
        double initial = Variance(population);
        for (int generation = 0; generation < 6; generation++)
            population = Enumerable.Range(0, population.Count).Select(i => HeredityService.InheritVitality(
                population[(int)(rng.NextDouble() * population.Count)], population[(int)(rng.NextDouble() * population.Count)],
                family, genetics, rng)).ToList();

        double ratio = Variance(population) / initial;
        Assert.Equal(mendelian, ratio is > 0.8 and < 1.25);
    }

    [Fact]
    public void Rate_gene_with_segregation_keeps_its_variance_instead_of_doubling()
    {
        var rng = new WorldRng(7);
        var genes = Enumerable.Range(0, 2000).Select(_ => RateGene.RollInitial(rng)).ToList();
        double initial = Variance(genes.Select(g => g.Value).ToList());
        for (int generation = 0; generation < 6; generation++)
            genes = Enumerable.Range(0, genes.Count).Select(_ => RateGene.Inherit(
                genes[(int)(rng.NextDouble() * genes.Count)], genes[(int)(rng.NextDouble() * genes.Count)], rng,
                QuantitativeGeneticsRules.Default)).ToList();

        Assert.InRange(Variance(genes.Select(g => g.Value).ToList()) / initial, 0.8, 1.25);
    }

    [Fact]
    public void Starving_lowers_health_every_day_and_food_and_sleep_bring_it_back()
    {
        var world = BuildWorld();
        var rules = world.NeedsRules.Physiology;
        var npc = Person(world, 1, Sex.Female, 25);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(0, now);
        npc.MarkHungerZeroSince(now);
        for (int day = 0; day < 10; day++)
        {
            npc.SetThirst(100, now + day * 24); // só fome: com água
            PhysiologySystem.DailyUpdate(world, rules, npc, now + day * 24);
        }
        int starved = npc.Health;

        npc.ClearHungerZeroSince();
        for (int day = 10; day < 20; day++)
        {
            long tick = now + day * 24;
            npc.SetHunger(100, tick);
            npc.SetThirst(100, tick);
            npc.SetSleep(100, tick);
            PhysiologySystem.DailyUpdate(world, rules, npc, tick);
        }

        Assert.True(starved <= 100 - 10 * rules.StarvationHealthLossPerDay + 1 && npc.Health > starved, $"{starved} → {npc.Health}");
    }

    [Fact]
    public void The_old_recover_more_slowly_than_the_young()
    {
        var world = BuildWorld();
        var rules = world.NeedsRules.Physiology;
        var young = Person(world, 1, Sex.Male, 25, health: 40);
        var old = Person(world, 2, Sex.Male, 65, health: 40);
        long now = world.CurrentDate.TotalHours;
        for (int day = 0; day < 10; day++)
            foreach (var npc in new[] { young, old })
            {
                npc.SetHunger(100, now + day * 24);
                npc.SetThirst(100, now + day * 24);
                npc.SetSleep(100, now + day * 24);
                PhysiologySystem.DailyUpdate(world, rules, npc, now + day * 24);
            }

        Assert.True(young.Health - 40 > old.Health - 40, $"{young.Health} vs {old.Health}");
    }

    [Fact]
    public void Undernutrition_multiplies_the_monthly_death_risk_more_in_small_children()
    {
        var world = BuildWorld();
        var child = Person(world, 1, Sex.Male, 2);
        var adult = Person(world, 2, Sex.Male, 30);
        double childFed = MortalitySystem.StateMultiplier(world, child, false);
        double adultFed = MortalitySystem.StateMultiplier(world, adult, false);
        child.SetUndernutrition(1);
        adult.SetUndernutrition(1);

        double childRatio = MortalitySystem.StateMultiplier(world, child, false) / childFed;
        double adultRatio = MortalitySystem.StateMultiplier(world, adult, false) / adultFed;
        Assert.True(adultRatio > 1.5 && childRatio > adultRatio, $"{childRatio} vs {adultRatio}");
    }

    [Fact]
    public void A_weak_person_works_less_and_walks_more_slowly()
    {
        var world = BuildWorld();
        var strong = Person(world, 1, Sex.Male, 30, health: 100);
        var weak = Person(world, 2, Sex.Male, 30, health: 15);
        strong.SetBody(1.71, 64, 25);
        weak.SetBody(1.71, 64, 25);

        Assert.True(BodyMechanic.WorkCapacityMultiplier(world, weak) < BodyMechanic.WorkCapacityMultiplier(world, strong) * 0.7
            && BodyMechanic.MovementCostMultiplier(world, weak) > BodyMechanic.MovementCostMultiplier(world, strong) * 1.3);
    }

    [Fact]
    public void A_child_starves_sooner_than_an_adult()
    {
        var world = BuildWorld();
        var child = Person(world, 1, Sex.Male, 2);
        var adult = Person(world, 2, Sex.Male, 30);
        long now = world.CurrentDate.TotalHours;

        Assert.True(NeedsDecaySystem.StarvationTicksFor(world, child, world.NeedsRules, now)
            < NeedsDecaySystem.StarvationTicksFor(world, adult, world.NeedsRules, now) / 2);
    }

    private (WorldState World, Npc Mother, Npc Baby, Household Home) NursingFamily(double babyAgeDays = 30)
    {
        var world = BuildWorld();
        var home = new Household(new HouseholdId(1), new CellCoord(1, 1), new NpcId(1), [new NpcId(1), new NpcId(2)]);
        world.AddHousehold(home);
        var mother = Person(world, 1, Sex.Female, 25, home.Id);
        var baby = Person(world, 2, Sex.Female, babyAgeDays / Calendar.DaysPerMonth / Calendar.MonthsPerYear, home.Id, mother.Id,
            gestation: 268);
        long birth = baby.BirthDate.TotalHours;
        mother.SetReproduction(new ReproductiveState(Parity: 1, LastBirthTick: birth, InfecundUntilTick: birth + 365 * 24));
        mother.MarkLactation(world.CurrentDate.TotalHours - 24);
        return (world, mother, baby, home);
    }

    [Fact]
    public void A_nursling_with_a_living_mother_at_home_is_breastfed()
    {
        var (world, mother, baby, _) = NursingFamily();

        Assert.True(Lactation.TryNurse(world, baby, world.CurrentDate.TotalHours) && baby.LastNursedTick == world.CurrentDate.TotalHours);
    }

    [Fact]
    public void Nursing_makes_the_mothers_hunger_fall_faster()
    {
        var (world, mother, _, _) = NursingFamily();
        double before = mother.HungerNeed.DecayRatePerTick;

        Lactation.RefreshHungerCost(world, mother, world.CurrentDate.TotalHours);

        Assert.True(mother.HungerNeed.DecayRatePerTick > before * 1.2);
    }

    [Fact]
    public void A_starving_mother_has_no_milk_and_the_baby_is_not_nursed()
    {
        var (world, mother, baby, _) = NursingFamily();
        mother.MarkHungerZeroSince(world.CurrentDate.TotalHours - 48);

        Assert.False(Lactation.TryNurse(world, baby, world.CurrentDate.TotalHours));
    }

    [Fact]
    public void An_unnursed_newborn_has_a_much_higher_death_risk_than_a_nursed_one()
    {
        var (world, _, baby, _) = NursingFamily();
        double unnursed = MortalitySystem.StateMultiplier(world, baby, true);
        baby.MarkNursed(world.CurrentDate.TotalHours);

        Assert.True(unnursed > 3 * MortalitySystem.StateMultiplier(world, baby, true));
    }

    [Fact]
    public void An_unnursed_newborn_is_not_condemned_within_the_month()
    {
        // o fator sobre o risco anual da tábua não pode saturar em 1: sem leite a época registra ~50% de sobrevivência no 1º ano
        var (world, _, baby, _) = NursingFamily(babyAgeDays: 1);
        double month = MortalitySystem.WindowRisk(world, baby, world.CurrentDate.TotalHours, Calendar.HoursPerMonth);

        Assert.InRange(month, 0.05, 0.3);
    }

    [Fact]
    public void Weaning_is_gradual_between_six_months_and_two_years()
    {
        var lactation = LactationRules.Default;

        Assert.True(lactation.MilkShare(100) == 1 && lactation.MilkShare(400) is > 0.2 and < 0.8 && lactation.MilkShare(800) == 0);
    }

    [Fact]
    public void The_death_of_a_nursling_ends_the_mothers_lactational_amenorrhea()
    {
        var (world, mother, baby, _) = NursingFamily(babyAgeDays: 90);
        long before = mother.Reproduction!.InfecundUntilTick;
        var ctx = new LivingWorld.Simulation.Scheduling.TickContext(world, world.Rng, world.Scheduler);

        NpcDeath.Apply(world, ctx, baby, Domain.History.WorldEventKind.Death);

        Assert.True(mother.Reproduction!.InfecundUntilTick < before - 100 * 24, $"{before} → {mother.Reproduction.InfecundUntilTick}");
    }

    [Fact]
    public void Another_lactating_woman_of_the_household_nurses_a_baby_whose_mother_died()
    {
        var (world, mother, baby, home) = NursingFamily();
        var nurse = Person(world, 3, Sex.Female, 28, home.Id);
        home.AddMember(nurse.Id);
        nurse.SetReproduction(new ReproductiveState(Parity: 2, LastBirthTick: world.CurrentDate.TotalHours - 60 * 24));
        nurse.MarkLactation(world.CurrentDate.TotalHours - 24);
        mother.Die(world.CurrentDate);

        Assert.Same(nurse, Lactation.FindCaregiver(world, baby, world.CurrentDate.TotalHours));
    }

    private static double Variance(IReadOnlyList<double> values)
    {
        double mean = values.Average();
        return values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
    }
}
