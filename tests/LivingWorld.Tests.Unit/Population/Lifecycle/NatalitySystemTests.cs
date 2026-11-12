using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Lifecycle;

/// <summary>Fase 7, T17: <see cref="NatalitySystem"/> — concepção por cônjuge, pisos, parto
/// agendado, risco de parto e hereditariedade.</summary>
public class NatalitySystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly LifeTable Table = LifeTable.Create(90,
    [
        new LifeTableBracket(0, 89, 0.01),
    ]).Value!;

    /// <summary>Concepção certa no primeiro ciclo (fecundabilidade 1, ninguém estéril, sem perda nem gêmeos nem prematuro): isola
    /// o comportamento sob teste do sorteio de fertilidade (AD-130).</summary>
    private static readonly ReproductionRules Certain = ReproductionRules.Default with
    {
        FemaleMonthlyFecundability = AgeCurve.Of((0, 1.0)),
        FemaleSterileFraction = AgeCurve.Of((0, 0.0)),
        MaleFertility = AgeCurve.Of((0, 1.0)),
        MiscarriageRiskByAge = AgeCurve.Of((0, 0.0)),
        TwinRate = 0,
        PretermBands = [],
    };

    private static readonly PopulationRules Rules = PopulationRules.Create(
        Table, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 1.0, gestationDays: 10).Value!
        with
    { Reproduction = Certain };

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static FamilyRules PermissiveFamilyRules(double personalityHeritability = 0.0) =>
        FamilyRules.Create(
            relationshipDeltas: ZeroDeltas(),
            decayPerDay: 0,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: new Dictionary<AttractionFactor, double>(),
            courtshipThreshold: 0.5,
            courtshipDurationDays: 10,
            marriageInitialStock: new Dictionary<int, long>(),
            conceptionHealthFloor: 0,
            conceptionRelationshipFloor: 0,
            conceptionResourceFloor: new Dictionary<int, long>(),
            maternalDeathRisk: 0,
            infantDeathRisk: 0,
            vitalityMotherWeight: 0.5,
            vitalityFatherWeight: 0.5,
            vitalityMutationStdDev: 0,
            vitalityMortalityWeight: 0,
            upbringingWealthWeight: 0.2,
            environmentalWealthChannelEnabled: false,
            neutralDriftEnabled: false,
            vitalityMortalitySelectionEnabled: true,
            personalityHeritability: personalityHeritability).Value!;

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> ZeroDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        return deltas;
    }

    private (WorldState World, TickContext Ctx, Npc Mother, Npc Father, Household Household) BuildWorldWithMarriedCouple(
        PopulationCatalog catalog, ulong seed, FamilyRules? familyRules = null, ReproductionRules? reproduction = null,
        int motherAgeYears = 20, int fatherAgeYears = 22, IWorldEventSink? sink = null, LivingWorld.Domain.Behavior.NeedsRules? needsRules = null)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var family = familyRules ?? PermissiveFamilyRules();
        var world = new WorldState(
            Calendar, seed, map, catalog, Rules with { Reproduction = reproduction ?? Certain },
            needsRules ?? ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, familyRules: family);
        var location = new CellCoord(1, 1);

        var mother = new Npc(
            new NpcId(1), "mother", Sex.Female, WorldDate.Epoch(Calendar).AddYears(20 - motherAgeYears), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            vitality: 60, upbringing: 40);
        var father = new Npc(
            new NpcId(2), "father", Sex.Male, WorldDate.Epoch(Calendar).AddYears(20 - fatherAgeYears), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            vitality: 70, upbringing: 45);

        mother.Marry(father.Id);
        father.Marry(mother.Id);

        var household = new Household(new HouseholdId(1), location, mother.Id, [mother.Id, father.Id]);
        mother.JoinHousehold(household.Id);
        father.JoinHousehold(household.Id);
        household.Deposit(new ResourceType(1), 100);

        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(3);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(20);

        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        return (world, ctx, mother, father, household);
    }

    private static Npc TriggerBirth(
        WorldState world, TickContext ctx, NpcId motherId, NpcId fatherId, HouseholdId householdId, long conceptionStock = 100)
    {
        var natality = new NatalitySystem();
        var evt = ctx.ScheduleEvent(world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{motherId.Value}|{fatherId.Value}|{householdId.Value}|{conceptionStock}");
        int npcsBefore = world.Npcs.Count;

        natality.HandleEvent(world, ctx, evt);

        Assert.Equal(npcsBefore + 1, world.Npcs.Count);
        return world.Npcs[^1];
    }

    [Fact]
    public void A_newborn_keeps_the_mothers_city_instead_of_becoming_an_unemployable_cityless_adult()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(ScenarioRunner.DefaultPopulationCatalog, 42);
        var city = new CityId(new Guid("11111111-1111-1111-1111-111111111111"));
        mother.JoinCity(city); father.JoinCity(city); household.JoinCity(city);
        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);
        Assert.Equal(city, baby.City);
        Assert.Equal(household.City, baby.City);
    }

    [Fact]
    public void Baby_born_at_runtime_gets_a_real_personality_not_the_old_fixed_placeholder()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>()), seed: 42);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.NotNull(baby.Personality);
        Assert.NotEqual(SomePersonality, baby.Personality);
    }

    [Fact]
    public void Baby_born_at_runtime_has_no_profession_even_when_the_scenario_has_crafts()
    {
        // AD-136: ninguém nasce com ofício; ele é escolhido na juventude (VocationMechanic)
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int> { 5, 6, 7 }, new HashSet<int>());
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(catalog, seed: 7);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.Equal(ProfessionType.None, baby.Profession);
    }

    [Fact]
    public void Baby_born_with_an_empty_profession_catalog_gets_the_sentinel_without_throwing()
    {
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(catalog, seed: 99);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.Equal(ProfessionType.None, baby.Profession);
    }

    [Fact]
    public void Baby_born_at_runtime_inherits_RateGene_from_the_real_parents_not_the_default_fallback()
    {
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());
        var map = ScenarioRunner.DefaultMap(55);
        var world = new WorldState(
            Calendar, 55, map, catalog, Rules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, familyRules: PermissiveFamilyRules());
        var location = new CellCoord(1, 1);

        var mother = new Npc(
            new NpcId(1), "mother", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-20), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            rateGene: new RateGene(5.0), vitality: 55, upbringing: 50);
        var father = new Npc(
            new NpcId(2), "father", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-22), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            rateGene: new RateGene(5.0), vitality: 55, upbringing: 50);
        mother.Marry(father.Id);
        father.Marry(mother.Id);

        var household = new Household(new HouseholdId(1), location, mother.Id, [mother.Id, father.Id]);
        mother.JoinHousehold(household.Id);
        father.JoinHousehold(household.Id);

        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(3);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(20);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.True(baby.RateGene.Value > 3.0, $"esperado RateGene herdado perto de 5.0 (pais=5.0), veio {baby.RateGene.Value}");
    }

    [Fact]
    public void Same_seed_produces_the_same_baby_personality_and_profession_twice()
    {
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int> { 1, 2, 3 }, new HashSet<int>());

        var (worldA, ctxA, motherA, fatherA, householdA) = BuildWorldWithMarriedCouple(catalog, seed: 123);
        var babyA = TriggerBirth(worldA, ctxA, motherA.Id, fatherA.Id, householdA.Id);

        var (worldB, ctxB, motherB, fatherB, householdB) = BuildWorldWithMarriedCouple(catalog, seed: 123);
        var babyB = TriggerBirth(worldB, ctxB, motherB.Id, fatherB.Id, householdB.Id);

        Assert.Equal(babyA.Personality, babyB.Personality);
        Assert.Equal(babyA.Profession, babyB.Profession);
        Assert.Equal((babyA.Height, babyA.Weight, babyA.MuscleMass),
            (babyB.Height, babyB.Weight, babyB.MuscleMass));
    }

    [Fact]
    public void Baby_born_at_runtime_has_the_size_of_a_newborn_not_of_an_adult()
    {
        // AD-134: antes o recém-nascido saía com corpo de adulto (1,70 m, 68 kg); a intenção (corpo plausível) continua
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(catalog, seed: 13);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.True(baby.Height is > 0.40 and < 0.60 && baby.Weight is > 2.0 and < 5.0 && baby.MuscleMass < 1.5,
            $"{baby.Height} m, {baby.Weight} kg, {baby.MuscleMass} kg");
    }

    [Fact]
    public void A_preterm_baby_is_born_smaller_than_a_term_baby()
    {
        var growth = Domain.Population.Body.GrowthRules.Default;
        double term = growth.HeightAt(Sex.Female, 0, 1.59, 268);
        double preterm = growth.HeightAt(Sex.Female, 0, 1.59, 200);
        double termWeight = growth.WeightAt(Sex.Female, 0, term, 1.0, 0, 268);
        double pretermWeight = growth.WeightAt(Sex.Female, 0, preterm, 1.0, 0, 200);

        Assert.True(preterm < term && pretermWeight < termWeight * 0.6, $"{preterm}/{term} m, {pretermWeight}/{termWeight} kg");
    }

    [Fact]
    public void Tick_does_not_conceive_when_health_floor_not_met()
    {
        var rules = PermissiveFamilyRules() with { ConceptionHealthFloor = 90 };
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 1, familyRules: rules);
        mother.SetHealth(40);

        new NatalitySystem().Tick(world, ctx);

        Assert.Null(mother.PregnantUntil);
        Assert.Empty(world.Scheduler.Snapshot());
    }

    [Fact]
    public void Tick_does_not_conceive_when_relationship_floor_not_met()
    {
        var rules = PermissiveFamilyRules() with { ConceptionRelationshipFloor = 80 };
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 2, familyRules: rules);

        new NatalitySystem().Tick(world, ctx);

        Assert.Null(mother.PregnantUntil);
    }

    [Fact]
    public void Tick_does_not_conceive_when_resource_floor_not_met()
    {
        var rules = PermissiveFamilyRules() with
        {
            ConceptionResourceFloor = new Dictionary<int, long> { [1] = 10_000 },
        };
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 3, familyRules: rules);

        new NatalitySystem().Tick(world, ctx);

        Assert.Null(mother.PregnantUntil);
    }

    [Fact]
    public void Tick_schedules_birth_without_creating_child_immediately()
    {
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 4);
        int npcsBefore = world.Npcs.Count;

        new NatalitySystem().Tick(world, ctx);

        Assert.Equal(npcsBefore, world.Npcs.Count);
        Assert.NotNull(mother.PregnantUntil);
        Assert.NotEmpty(world.Scheduler.Snapshot());
    }

    [Fact]
    public void HandleEvent_is_silent_when_mother_died_before_birth()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 5);
        mother.Die(world.CurrentDate);
        int npcsBefore = world.Npcs.Count;

        var evt = ctx.ScheduleEvent(world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);

        Assert.Equal(npcsBefore, world.Npcs.Count);
    }

    [Fact]
    public void HandleEvent_maternal_death_risk_kills_mother_without_creating_baby_when_physiology_is_static()
    {
        // o modelo de antes (sem fisiologia): a morte materna não deixa bebê
        var rules = PermissiveFamilyRules() with { MaternalDeathRisk = 1.0 };
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 6, familyRules: rules,
            needsRules: ScenarioRunner.DefaultNeedsRules with { Physiology = Domain.Health.PhysiologyRules.Static });
        int npcsBefore = world.Npcs.Count;

        var evt = ctx.ScheduleEvent(world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);

        Assert.Equal(npcsBefore, world.Npcs.Count);
        Assert.False(mother.IsAlive);
    }

    [Fact]
    public void A_mother_who_dies_in_childbirth_still_delivers_a_living_baby()
    {
        var rules = PermissiveFamilyRules() with { MaternalDeathRisk = 1.0 };
        var physiology = Domain.Health.PhysiologyRules.Default;
        var needs = ScenarioRunner.DefaultNeedsRules with
        {
            Physiology = physiology with { Lactation = physiology.Lactation with { StillbirthRiskWhenMotherDies = 0 } },
        };
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 6, familyRules: rules, needsRules: needs);
        int npcsBefore = world.Npcs.Count;

        var evt = ctx.ScheduleEvent(world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);

        Assert.True(world.Npcs.Count == npcsBefore + 1 && world.Npcs[^1].IsAlive && !mother.IsAlive);
    }

    [Fact]
    public void HandleEvent_infant_death_risk_creates_no_live_baby()
    {
        var rules = PermissiveFamilyRules() with { InfantDeathRisk = 1.0, MaternalDeathRisk = 0 };
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 7, familyRules: rules);
        int npcsBefore = world.Npcs.Count;

        var evt = ctx.ScheduleEvent(world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);

        Assert.Equal(npcsBefore, world.Npcs.Count);
        Assert.True(mother.IsAlive);
    }

    [Fact]
    public void Baby_uses_conception_stock_for_upbringing_not_current_household_wealth()
    {
        var rules = PermissiveFamilyRules() with { UpbringingWealthWeight = 0.5 };
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 8, familyRules: rules);

        const long capturedStock = 80;
        household.Deposit(new ResourceType(1), 10_000);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id, conceptionStock: capturedStock);

        double expected = HeredityService.DeriveUpbringingFromConceptionStock(capturedStock, rules);
        Assert.Equal(expected, baby.Upbringing);
    }

    [Fact]
    public void Baby_inherits_vitality_from_parents()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 9);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.InRange(baby.Vitality, 0, 100);
        Assert.NotEqual(50.0, baby.Vitality);
    }

    [Fact]
    public void A_baby_is_born_without_skills_even_when_both_parents_are_masters()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>()), seed: 11);
        var skillsRules = ScenarioRunner.DefaultSkillsRules;
        mother.GainSkill(new SkillType(0), skillsRules.Cap * 0.9, skillsRules.Cap);
        father.GainSkill(new SkillType(0), skillsRules.Cap * 0.9, skillsRules.Cap);
        Assert.True(mother.Skills.Get(new SkillType(0)) > 0);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.Equal(0, baby.Skills.Get(new SkillType(0))); // habilidade é aprendida, não herdada (SKILL-09)
    }

    [Fact]
    public void A_baby_of_two_average_parents_stays_within_the_segregation_range_when_the_scenario_declares_full_heritability()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>()), seed: 21,
            familyRules: PermissiveFamilyRules(personalityHeritability: 1.0));

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        // pais com todos os traços em 50, h = 1: o filho é a média mais segregação de ±35,7, ou seja 14 a 86 (um sorteio independente sairia do intervalo em ~28% dos traços)
        Assert.All(
            new[] { baby.Personality.Extroversion, baby.Personality.Agreeableness, baby.Personality.Conscientiousness, baby.Personality.EmotionalStability,
                baby.Personality.Openness, baby.Personality.Ambition, baby.Personality.Loyalty, baby.Personality.Altruism,
                baby.Personality.Impulsivity, baby.Personality.RiskAversion },
            trait => Assert.InRange(trait, 14, 86));
    }
    private sealed class ListSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static ScheduledEvent PendingBirth(WorldState world) =>
        world.Scheduler.Snapshot().Single(e => e.SystemName == NatalitySystem.SystemName);

    [Fact]
    public void Conception_falls_on_any_day_of_the_cycle_so_due_dates_spread_instead_of_one_fixed_offset()
    {
        var offsets = new HashSet<long>();
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var (world, ctx, _, _, _) = BuildWorldWithMarriedCouple(ScenarioRunner.DefaultPopulationCatalog, seed);
            new NatalitySystem().Tick(world, ctx);
            offsets.Add((PendingBirth(world).TargetTick - world.CurrentDate.TotalHours) / world.Calendar.HoursPerDay);
        }

        // antes (AD-130) todo parto saía em agora + GestationDays: um único deslocamento
        Assert.True(offsets.Count >= 15, $"só {offsets.Count} deslocamentos distintos em 30 concepções");
    }

    [Fact]
    public void Mother_does_not_conceive_again_during_postpartum_infecundity()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(ScenarioRunner.DefaultPopulationCatalog, seed: 31);
        TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        new NatalitySystem().Tick(world, ctx);

        Assert.Null(mother.PregnantUntil);
        Assert.True(mother.Reproduction!.InfecundUntilTick > world.CurrentDate.TotalHours);
    }

    [Fact]
    public void Baby_sex_is_decided_at_conception_and_kept_in_the_pregnancy()
    {
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 32, reproduction: Certain with { MaleBirthShare = 1.0 });
        new NatalitySystem().Tick(world, ctx);
        Assert.Equal("M", mother.Reproduction!.Pregnancy!.Sexes);

        var evt = PendingBirth(world);
        world.CurrentDate = world.CurrentDate.AddHours(evt.TargetTick - world.CurrentDate.TotalHours);
        new NatalitySystem().HandleEvent(world, ctx, evt);

        Assert.Equal(Sex.Male, world.Npcs[^1].Sex);
    }

    [Fact]
    public void Default_sex_ratio_at_birth_is_about_105_boys_per_100_girls()
    {
        double share = ReproductionRules.Default.MaleBirthShare;
        Assert.InRange(share / (1 - share), 1.03, 1.07);
    }

    [Fact]
    public void Twin_pregnancy_delivers_two_babies()
    {
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 33, reproduction: Certain with { TwinRate = 1.0 });
        new NatalitySystem().Tick(world, ctx);
        int before = world.Npcs.Count;

        new NatalitySystem().HandleEvent(world, ctx, PendingBirth(world));

        Assert.Equal(before + 2, world.Npcs.Count(n => n.MotherId == mother.Id) + before);
    }

    [Fact]
    public void Early_pregnancy_loss_ends_without_a_baby_and_logs_a_miscarriage()
    {
        var sink = new ListSink();
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 34, reproduction: Certain with { MiscarriageRiskByAge = AgeCurve.Of((0, 1.0)) },
            sink: sink);
        new NatalitySystem().Tick(world, ctx);
        var pregnancy = mother.Reproduction!.Pregnancy!;
        Assert.True(pregnancy.EndsInLoss);
        Assert.InRange(pregnancy.GestationDays, Certain.MiscarriageMinDays, Certain.MiscarriageMaxDays);
        int before = world.Npcs.Count;

        new NatalitySystem().HandleEvent(world, ctx, PendingBirth(world));

        Assert.Equal(before, world.Npcs.Count);
        Assert.Contains(sink.Events, e => e.Kind == LivingWorld.Domain.History.WorldEventKind.StillBirth
            && e.Payload.EndsWith(NatalitySystem.MiscarriageMarker, StringComparison.Ordinal));
    }

    [Fact]
    public void Woman_past_menopause_never_conceives_with_the_default_curves()
    {
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 35, reproduction: ReproductionRules.Default, motherAgeYears: 51);
        for (int month = 0; month < 24; month++)
        {
            new NatalitySystem().Tick(world, ctx);
            world.CurrentDate = world.CurrentDate.AddHours(world.Calendar.HoursPerMonth);
        }

        Assert.Null(mother.Reproduction);
    }

    [Fact]
    public void An_older_husband_still_fathers_a_child()
    {
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 36,
            reproduction: Certain with { MaleFertility = ReproductionRules.Default.MaleFertility }, fatherAgeYears: 62);
        for (int month = 0; month < 24 && mother.PregnantUntil is null; month++)
        {
            new NatalitySystem().Tick(world, ctx);
            world.CurrentDate = world.CurrentDate.AddHours(world.Calendar.HoursPerMonth);
        }

        Assert.NotNull(mother.PregnantUntil);
    }

    [Fact]
    public void Newborn_records_its_gestation_length()
    {
        var (world, ctx, mother, _, _) = BuildWorldWithMarriedCouple(ScenarioRunner.DefaultPopulationCatalog, seed: 37);
        new NatalitySystem().Tick(world, ctx);
        int days = mother.Reproduction!.Pregnancy!.GestationDays;

        new NatalitySystem().HandleEvent(world, ctx, PendingBirth(world));

        Assert.Equal(days, world.Npcs[^1].GestationAtBirthDays);
    }
}
