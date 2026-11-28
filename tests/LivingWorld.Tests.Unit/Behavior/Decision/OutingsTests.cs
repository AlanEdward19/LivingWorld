using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior.Decision;

/// <summary>#242: quem está de folga (o turno acabou, sem emprego) decide pelo estado dela aonde ir: casa, largo, conhecido, passeio curto.
/// A escolha é a mesma no bloco todo, não acontece à noite nem com criança, e desligada (<c>LW_AD192=nooutings</c>) fica onde está.</summary>
public class OutingsTests
{
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static WorldState World(int hour, bool outings = true)
    {
        var layout = outings ? null : CityLayoutRules.Default with { Streets = CityLayoutRules.Default.Streets with { AfterWorkOutings = false } };
        var world = ScenarioRunner.Create(1, initialPopulation: 0, cityLayout: layout).World;
        world.CurrentDate = world.CurrentDate.AddHours((hour - world.CurrentDate.Hour + 24) % 24);
        return world;
    }

    private static Npc Person(WorldState world, long id, CellCoord home, CellCoord at, int age = 30)
    {
        var city = world.ActiveCities().First();
        var npc = new Npc(new NpcId(id), $"p{id} Galmor", Sex.Female, world.CurrentDate.AddYears(-age), ScenarioRunner.DefaultCulture, home,
            motherId: null, fatherId: null, household: null, health: 100, personality: Neutral, profession: ProfessionType.None, currentLocation: at, city: city.Id);
        world.AddNpc(npc);
        var house = new LivingWorld.Domain.Population.Family.Household(new HouseholdId(id), home, npc.Id, [npc.Id], city: city.Id);
        world.AddHousehold(house);
        npc.JoinHousehold(house.Id);
        return npc;
    }

    [Fact]
    public void After_the_shift_she_goes_home_to_the_square_or_for_a_short_walk_with_a_real_destination()
    {
        var world = World(hour: 15);
        var square = world.ActiveCities().First().Location;
        var market = MarketIndex.BuildForTick(world);
        var home = new CellCoord(square.X + 2, square.Y + 1);
        var post = new CellCoord(square.X - 4, square.Y - 3); // o posto, longe de casa
        var kinds = new HashSet<string>();
        int stayed = 0;
        for (long id = 1; id <= 60; id++)
        {
            var npc = Person(world, id, home, post);
            var dest = BehaviorDecisionSystem.OutingDestinationOf(world, npc, ActionType.Idle, market);
            if (dest is not { } cell || cell == post) { stayed++; continue; }
            kinds.Add(cell == home ? "home" : cell == square ? "square"
                : Math.Max(Math.Abs(cell.X - home.X), Math.Abs(cell.Y - home.Y)) is >= 3 and <= 8 ? "stroll" : "other");
        }

        Assert.True(stayed < 6, $"quase todos saem do posto ({stayed} ficaram)");
        Assert.True(kinds.Count >= 3, $"kinds={string.Join(",", kinds)} stayed={stayed}");
        Assert.Contains("home", kinds);
        Assert.Contains("square", kinds);
        Assert.Contains("stroll", kinds);
        Assert.DoesNotContain("other", kinds);
    }

    [Fact]
    public void The_choice_is_the_same_world_after_world_and_differs_between_people()
    {
        var a = World(hour: 15);
        var b = World(hour: 15);
        var square = a.ActiveCities().First().Location;
        var home = new CellCoord(square.X + 2, square.Y + 1);
        var post = new CellCoord(square.X - 4, square.Y - 3);
        var seen = new HashSet<CellCoord>();
        for (long id = 1; id <= 30; id++)
        {
            var chosen = BehaviorDecisionSystem.OutingDestinationOf(a, Person(a, id, home, post), ActionType.Idle, MarketIndex.BuildForTick(a));
            Assert.Equal(chosen, BehaviorDecisionSystem.OutingDestinationOf(b, Person(b, id, home, post), ActionType.Idle, MarketIndex.BuildForTick(b)));
            if (chosen is { } cell) seen.Add(cell);
        }

        Assert.True(seen.Count >= 4, $"seen={seen.Count} " + "cada um escolhe pelo seu estado e pela sua sorte, não todos o mesmo lugar");
    }

    [Fact]
    public void At_night_the_child_the_rule_off_and_who_is_on_duty_stay_where_they_are()
    {
        var night = World(hour: 23);
        var square = night.ActiveCities().First().Location;
        var home = new CellCoord(square.X + 2, square.Y + 1);
        var post = new CellCoord(square.X - 4, square.Y - 3);
        Assert.Null(BehaviorDecisionSystem.OutingDestinationOf(night, Person(night, 1, home, post), ActionType.Idle, MarketIndex.BuildForTick(night)));

        var day = World(hour: 15);
        var kid = Person(day, 2, home, post, age: 6);
        Assert.Null(BehaviorDecisionSystem.OutingDestinationOf(day, kid, ActionType.Idle, MarketIndex.BuildForTick(day)));

        var off = World(hour: 15, outings: false);
        for (long id = 1; id <= 20; id++)
            Assert.Null(BehaviorDecisionSystem.OutingDestinationOf(off, Person(off, id, home, post), ActionType.Idle, MarketIndex.BuildForTick(off)));
    }
}
