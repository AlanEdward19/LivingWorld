using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

public class HouseholdTests
{
    [Fact]
    public void Head_must_be_a_member_at_construction()
    {
        Assert.Throws<ArgumentException>(() => new Household(
            new HouseholdId(1), new CellCoord(0, 0), head: new NpcId(9), members: [new NpcId(1)]));
    }

    [Fact]
    public void Removing_a_non_head_member_keeps_the_head()
    {
        var household = new Household(new HouseholdId(1), new CellCoord(0, 0), new NpcId(1), [new NpcId(1), new NpcId(2)]);
        household.RemoveMember(new NpcId(2));

        Assert.Equal(new NpcId(1), household.Head);
        Assert.DoesNotContain(new NpcId(2), household.Members);
    }

    [Fact]
    public void Removing_the_head_promotes_the_lowest_id_remaining_member()
    {
        var household = new Household(new HouseholdId(1), new CellCoord(0, 0), new NpcId(5), [new NpcId(5), new NpcId(2), new NpcId(9)]);
        household.RemoveMember(new NpcId(5));

        Assert.Equal(new NpcId(2), household.Head);
    }

    [Fact]
    public void Removing_the_last_member_leaves_it_empty()
    {
        var household = new Household(new HouseholdId(1), new CellCoord(0, 0), new NpcId(1), [new NpcId(1)]);
        household.RemoveMember(new NpcId(1));

        Assert.True(household.IsEmpty);
    }

    // --- Fase 8 (T4): CityId ---

    [Fact]
    public void Constructor_preserves_the_city_passed_in()
    {
        var city = new CityId(Guid.Parse("00000000-0000-0000-0000-0000000000cc"));
        var household = new Household(
            new HouseholdId(1), new CellCoord(0, 0), new NpcId(1), [new NpcId(1)], city: city);

        Assert.Equal(city, household.City);
    }

    [Fact]
    public void JoinCity_changes_the_household_city()
    {
        var household = new Household(new HouseholdId(1), new CellCoord(0, 0), new NpcId(1), [new NpcId(1)]);
        var city = new CityId(Guid.Parse("00000000-0000-0000-0000-0000000000dd"));

        household.JoinCity(city);

        Assert.Equal(city, household.City);
    }
}
