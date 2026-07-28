using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T13/T14 (FAM-17): redistribuição de filhos quando o household fica sem
/// adulto/idoso vivo.</summary>
public class HouseholdRedistributionTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly WorldDate Now = WorldDate.Epoch(Calendar).AddYears(100);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly LifeStageRules LifeStages = ScenarioRunner.DefaultLifeStageRules;

    private static WorldState BuildWorld()
    {
        var world = new WorldState(
            Calendar, seed: 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            LifeStages, familyRules: FamilyRules.Disabled);
        world.CurrentDate = Now;
        return world;
    }

    private static Npc MakeNpc(
        WorldState world, Sex sex, int ageYears, NpcId? motherId = null, NpcId? fatherId = null,
        HouseholdId? household = null, NpcId? id = null)
    {
        var npcId = id ?? world.NextNpcIdAndAdvance();
        var birth = Now.AddYears(-ageYears);
        var npc = new Npc(
            npcId, $"npc-{npcId.Value}", sex, birth, new CultureId(1), new CellCoord(0, 0),
            motherId, fatherId, household, health: 100, personality: SomePersonality,
            profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static Household AddHousehold(WorldState world, params Npc[] members)
    {
        var head = members.OrderBy(m => m.Id.Value).First();
        var household = new Household(
            world.NextHouseholdIdAndAdvance(), head.CurrentLocation, head.Id,
            members.Select(m => m.Id).ToList());
        world.AddHousehold(household);
        foreach (var member in members)
            member.JoinHousehold(household.Id);
        return household;
    }

    private static TickContext Ctx(WorldState world) =>
        new(world, world.Rng, world.Scheduler);

    [Fact]
    public void Orphaned_children_join_living_grandparent_household()
    {
        var world = BuildWorld();
        var grandmother = MakeNpc(world, Sex.Female, ageYears: 55);
        var grandpaHousehold = AddHousehold(world, grandmother);

        var mother = MakeNpc(world, Sex.Female, ageYears: 30, motherId: grandmother.Id);
        var father = MakeNpc(world, Sex.Male, ageYears: 32);
        var child = MakeNpc(world, Sex.Male, ageYears: 8, motherId: mother.Id, fatherId: father.Id);
        var familyHome = AddHousehold(world, mother, father, child);

        NpcDeath.Apply(world, Ctx(world), mother, WorldEventKind.Death);
        NpcDeath.Apply(world, Ctx(world), father, WorldEventKind.Death);

        Assert.DoesNotContain(world.Households, h => h.Id == familyHome.Id);
        Assert.Equal(grandpaHousehold.Id, child.Household);
        Assert.Contains(child.Id, world.FindHousehold(grandpaHousehold.Id)!.Members);
    }

    [Fact]
    public void Orphaned_children_without_relative_each_become_head_of_unitary_household()
    {
        var world = BuildWorld();
        var mother = MakeNpc(world, Sex.Female, ageYears: 30);
        var father = MakeNpc(world, Sex.Male, ageYears: 32);
        var childA = MakeNpc(world, Sex.Female, ageYears: 6, motherId: mother.Id, fatherId: father.Id);
        var childB = MakeNpc(world, Sex.Male, ageYears: 4, motherId: mother.Id, fatherId: father.Id);
        var familyHome = AddHousehold(world, mother, father, childA, childB);

        NpcDeath.Apply(world, Ctx(world), mother, WorldEventKind.Death);
        NpcDeath.Apply(world, Ctx(world), father, WorldEventKind.Death);

        Assert.DoesNotContain(world.Households, h => h.Id == familyHome.Id);
        Assert.NotEqual(childA.Household, childB.Household);
        Assert.NotNull(childA.Household);
        Assert.NotNull(childB.Household);
        Assert.Equal(childA.Id, world.FindHousehold(childA.Household!.Value)!.Head);
        Assert.Equal(childB.Id, world.FindHousehold(childB.Household!.Value)!.Head);
    }

    [Fact]
    public void Orphaned_child_joins_adult_sibling_household_when_no_grandparent()
    {
        var world = BuildWorld();
        var mother = MakeNpc(world, Sex.Female, ageYears: 50);
        var father = MakeNpc(world, Sex.Male, ageYears: 52);
        var adultSibling = MakeNpc(world, Sex.Female, ageYears: 25, motherId: mother.Id, fatherId: father.Id);
        var siblingHome = AddHousehold(world, adultSibling);
        var child = MakeNpc(world, Sex.Male, ageYears: 10, motherId: mother.Id, fatherId: father.Id);
        var familyHome = AddHousehold(world, mother, father, child);

        NpcDeath.Apply(world, Ctx(world), mother, WorldEventKind.Death);
        NpcDeath.Apply(world, Ctx(world), father, WorldEventKind.Death);

        Assert.Equal(siblingHome.Id, child.Household);
        Assert.Contains(child.Id, world.FindHousehold(siblingHome.Id)!.Members);
        Assert.DoesNotContain(world.Households, h => h.Id == familyHome.Id);
    }

    [Fact]
    public void Death_leaving_empty_household_dissolves_without_orphan_handling()
    {
        var world = BuildWorld();
        var adult = MakeNpc(world, Sex.Male, ageYears: 40);
        var home = AddHousehold(world, adult);

        NpcDeath.Apply(world, Ctx(world), adult, WorldEventKind.Death);

        Assert.DoesNotContain(world.Households, h => h.Id == home.Id);
    }

    [Fact]
    public void Single_parent_death_leaves_surviving_adult_in_household()
    {
        var world = BuildWorld();
        var mother = MakeNpc(world, Sex.Female, ageYears: 35);
        var father = MakeNpc(world, Sex.Male, ageYears: 37);
        var child = MakeNpc(world, Sex.Female, ageYears: 5, motherId: mother.Id, fatherId: father.Id);
        var home = AddHousehold(world, mother, father, child);

        NpcDeath.Apply(world, Ctx(world), father, WorldEventKind.Death);

        Assert.Contains(world.Households, h => h.Id == home.Id);
        Assert.Equal(home.Id, mother.Household);
        Assert.Equal(home.Id, child.Household);
    }
}
