using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T17: <see cref="NatalitySystem"/> — concepção por cônjuge, pisos, parto
/// agendado, risco de parto e hereditariedade.</summary>
public class NatalitySystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly LifeTable Table = LifeTable.Create(90,
    [
        new LifeTableBracket(0, 89, 0.01),
    ]).Value!;

    private static readonly PopulationRules Rules = PopulationRules.Create(
        Table, fertilityMinAge: 16, fertilityMaxAge: 45, annualConceptionChance: 1.0, gestationDays: 10).Value!;

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static FamilyRules PermissiveFamilyRules() =>
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
            vitalityMortalitySelectionEnabled: true).Value!;

    private static Dictionary<(RelationshipEventType, RelationshipAxis), double> ZeroDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0;
        return deltas;
    }

    private (WorldState World, TickContext Ctx, Npc Mother, Npc Father, Household Household) BuildWorldWithMarriedCouple(
        PopulationCatalog catalog, ulong seed, FamilyRules? familyRules = null)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var family = familyRules ?? PermissiveFamilyRules();
        var world = new WorldState(
            Calendar, seed, map, catalog, Rules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, familyRules: family);
        var location = new CellCoord(1, 1);

        var mother = new Npc(
            new NpcId(1), "mother", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-20), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location,
            vitality: 60, upbringing: 40);
        var father = new Npc(
            new NpcId(2), "father", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-22), new CultureId(1), location,
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

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
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
    public void Baby_born_at_runtime_gets_a_real_personality_not_the_old_fixed_placeholder()
    {
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>()), seed: 42);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.NotNull(baby.Personality);
        Assert.NotEqual(SomePersonality, baby.Personality);
    }

    [Fact]
    public void Baby_born_at_runtime_gets_a_profession_valid_for_the_scenario_catalog()
    {
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int> { 5, 6, 7 }, new HashSet<int>());
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(catalog, seed: 7);

        var baby = TriggerBirth(world, ctx, mother.Id, father.Id, household.Id);

        Assert.True(catalog.IsValidProfession(baby.Profession));
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
    public void HandleEvent_maternal_death_risk_kills_mother_without_creating_baby()
    {
        var rules = PermissiveFamilyRules() with { MaternalDeathRisk = 1.0 };
        var (world, ctx, mother, father, household) = BuildWorldWithMarriedCouple(
            ScenarioRunner.DefaultPopulationCatalog, seed: 6, familyRules: rules);
        int npcsBefore = world.Npcs.Count;

        var evt = ctx.ScheduleEvent(world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{mother.Id.Value}|{father.Id.Value}|{household.Id.Value}|100");
        new NatalitySystem().HandleEvent(world, ctx, evt);

        Assert.Equal(npcsBefore, world.Npcs.Count);
        Assert.False(mother.IsAlive);
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
}
