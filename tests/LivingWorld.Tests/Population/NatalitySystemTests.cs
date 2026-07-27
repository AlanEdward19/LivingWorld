using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Task 7: NPC nascido em runtime (NatalitySystem.HandleEvent) sorteia Personality e
/// Profession de verdade, nunca o placeholder fixo que a Fase 2 desta task removeu.</summary>
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

    private (WorldState World, TickContext Ctx, NpcId Mother, NpcId Father, HouseholdId Household) BuildWorldWithCouple(
        PopulationCatalog catalog, ulong seed)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, catalog, Rules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var location = new CellCoord(1, 1);

        var mother = new Npc(
            new NpcId(1), "mother", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-20), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location);
        var father = new Npc(
            new NpcId(2), "father", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-22), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: ProfessionType.None, currentLocation: location);

        var household = new Household(new HouseholdId(1), location, mother.Id, [mother.Id, father.Id]);
        mother.JoinHousehold(household.Id);
        father.JoinHousehold(household.Id);

        world.AddNpc(mother);
        world.AddNpc(father);
        world.AddHousehold(household);
        world.AdvanceNpcIdTo(3);
        world.AdvanceHouseholdIdTo(2);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(20); // idade da mãe/pai bate com FertilityMinAge

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        return (world, ctx, mother.Id, father.Id, household.Id);
    }

    private static Npc TriggerBirth(WorldState world, TickContext ctx, NpcId motherId, NpcId fatherId, HouseholdId householdId)
    {
        var natality = new NatalitySystem();
        var evt = ctx.ScheduleEvent(world.CurrentDate.TotalHours, NatalitySystem.SystemName,
            $"{motherId.Value}|{fatherId.Value}|{householdId.Value}");
        int npcsBefore = world.Npcs.Count;

        natality.HandleEvent(world, ctx, evt);

        Assert.Equal(npcsBefore + 1, world.Npcs.Count);
        return world.Npcs[^1];
    }

    [Fact]
    public void Baby_born_at_runtime_gets_a_real_personality_not_the_old_fixed_placeholder()
    {
        var (world, ctx, motherId, fatherId, householdId) = BuildWorldWithCouple(
            new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>()), seed: 42);

        var baby = TriggerBirth(world, ctx, motherId, fatherId, householdId);

        Assert.NotNull(baby.Personality);
        // Placeholder removido pela T7 era Personality(50,50,...,50) fixo para todo NPC — com
        // RNG real, bater exatamente nos 10 traços é praticamente impossível.
        Assert.NotEqual(SomePersonality, baby.Personality);
    }

    [Fact]
    public void Baby_born_at_runtime_gets_a_profession_valid_for_the_scenario_catalog()
    {
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int> { 5, 6, 7 }, new HashSet<int>());
        var (world, ctx, motherId, fatherId, householdId) = BuildWorldWithCouple(catalog, seed: 7);

        var baby = TriggerBirth(world, ctx, motherId, fatherId, householdId);

        Assert.True(catalog.IsValidProfession(baby.Profession));
    }

    [Fact]
    public void Baby_born_with_an_empty_profession_catalog_gets_the_sentinel_without_throwing()
    {
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());
        var (world, ctx, motherId, fatherId, householdId) = BuildWorldWithCouple(catalog, seed: 99);

        var baby = TriggerBirth(world, ctx, motherId, fatherId, householdId);

        Assert.Equal(ProfessionType.None, baby.Profession);
    }

    [Fact]
    public void Same_seed_produces_the_same_baby_personality_and_profession_twice()
    {
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int> { 1, 2, 3 }, new HashSet<int>());

        var (worldA, ctxA, motherA, fatherA, householdA) = BuildWorldWithCouple(catalog, seed: 123);
        var babyA = TriggerBirth(worldA, ctxA, motherA, fatherA, householdA);

        var (worldB, ctxB, motherB, fatherB, householdB) = BuildWorldWithCouple(catalog, seed: 123);
        var babyB = TriggerBirth(worldB, ctxB, motherB, fatherB, householdB);

        Assert.Equal(babyA.Personality, babyB.Personality);
        Assert.Equal(babyA.Profession, babyB.Profession);
    }
}
