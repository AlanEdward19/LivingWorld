using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>AD-186: quem quer conversar e está só em casa sai para achar alguém (o conhecido de quem mais gosta, senão o largo);
/// com alguém perto, ou com a regra desligada, conversa onde está.</summary>
public class SeekCompanyTests
{
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static WorldState World(bool seek = true)
    {
        var needs = ScenarioRunner.DefaultNeedsRules with { Psyche = ScenarioRunner.DefaultNeedsRules.Psyche with { SeekCompany = seek } };
        return ScenarioRunner.Create(1, initialPopulation: 0, needsRules: needs).World;
    }

    private static Npc Person(WorldState world, long id, CellCoord at)
    {
        var city = world.ActiveCities().First();
        var npc = new Npc(new NpcId(id), $"p{id} Galmor", Sex.Female, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture, at,
            motherId: null, fatherId: null, household: null, health: 100, personality: Neutral, profession: ProfessionType.None, currentLocation: at, city: city.Id);
        world.AddNpc(npc);
        var home = new Household(new HouseholdId(id), at, npc.Id, [npc.Id], city: city.Id);
        world.AddHousehold(home);
        npc.JoinHousehold(home.Id);
        return npc;
    }

    private static void Likes(WorldState world, Npc who, Npc whom, double affection) =>
        world.GetOrCreateRelationship(new RelationshipKey(who.Id, whom.Id), 1).CopyAxesFrom(Relationship.FromAxes(50, affection, 50, 0, 1));

    [Fact]
    public void Alone_at_home_she_goes_to_the_friend_she_likes_most_and_with_company_she_stays()
    {
        var world = World();
        var city = world.ActiveCities().First().Location;
        var lonely = Person(world, 1, new CellCoord(city.X + 6, city.Y));
        var friend = Person(world, 2, new CellCoord(city.X - 6, city.Y));
        var acquaintance = Person(world, 3, new CellCoord(city.X, city.Y + 6));
        Likes(world, lonely, friend, 80);
        Likes(world, lonely, acquaintance, 20);

        Assert.Equal(friend.CurrentLocation, BehaviorDecisionSystem.CompanyDestinationOf(world, lonely));

        acquaintance.MoveTo(new CellCoord(lonely.CurrentLocation.X + 1, lonely.CurrentLocation.Y), 1);
        Assert.Null(BehaviorDecisionSystem.CompanyDestinationOf(world, lonely));
    }

    [Fact]
    public void Knowing_no_one_she_goes_to_the_square_and_without_the_rule_she_talks_alone()
    {
        var world = World();
        var city = world.ActiveCities().First().Location;
        var lonely = Person(world, 1, new CellCoord(city.X + 6, city.Y));
        Assert.Equal(city, BehaviorDecisionSystem.CompanyDestinationOf(world, lonely));

        var off = World(seek: false);
        var offCity = off.ActiveCities().First().Location;
        var alone = Person(off, 1, new CellCoord(offCity.X + 6, offCity.Y));
        Assert.Null(BehaviorDecisionSystem.CompanyDestinationOf(off, alone));
    }
}
