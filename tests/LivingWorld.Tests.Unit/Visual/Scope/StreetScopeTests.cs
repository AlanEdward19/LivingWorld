using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Visual.Scope;

/// <summary>AD-176: a rua de verdade chega ao cliente no escopo Cidade e no delta (só o que mudou), e quem viaja leva as células
/// da rota real, para o boneco andar por elas e não cortar caminho na tela.</summary>
public class StreetScopeTests
{
    private static StreetCellView Road(int x, StreetSurface surface, int band = 0, StreetWorkView? work = null) =>
        new(new CityId(Guid.Empty), new CellCoord(x, 0), surface, 5, false, false) { UseBand = band, Work = work };

    [Fact]
    public void Only_changed_road_cells_go_in_the_delta_and_the_reducer_rebuilds_the_same_roads()
    {
        var before = LivingScopeState.Empty with { Roads = [Road(1, StreetSurface.Trail), Road(2, StreetSurface.Dirt), Road(3, StreetSurface.Dirt)] };
        var after = LivingScopeState.Empty with
        {
            Roads = [Road(1, StreetSurface.Trail), Road(2, StreetSurface.Dirt, work: new StreetWorkView(StreetSurface.Gravel, 40, false, "treasury"))],
        };

        var delta = ScopeDeltaBuilder.Diff(1, before, after);

        Assert.Equal([new CellCoord(2, 0)], delta.RoadUpserts.Select(r => r.Cell));
        Assert.Equal([new CellCoord(3, 0)], delta.RoadRemoved);
        Assert.Equal(after.Roads, LivingDeltaReducer.Apply(before, delta).Roads);
    }

    [Fact]
    public void City_scope_carries_the_trodden_streets_of_the_city()
    {
        var (world, clock) = ScenarioRunner.Create(1);
        clock.Run(world, world.Calendar.HoursPerMonth * 4);
        var city = world.ActiveCities().First();

        var state = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.Value.ToString()));

        Assert.NotEmpty(state.Roads);
        Assert.All(state.Roads, r => Assert.Equal(city.Id, r.City));
    }

    [Fact]
    public void A_travelling_npc_carries_its_real_route_from_where_it_is_to_where_it_goes()
    {
        var (world, clock) = ScenarioRunner.Create(1);
        var city = world.ActiveCities().First();
        var scope = new VisualScope(VisualScopeKind.City, city.Id.Value.ToString());
        for (int hour = 0; hour < 48; hour++)
        {
            clock.Tick(world);
            var state = LivingScopeProjector.Build(world, scope);
            var walker = state.Npcs.FirstOrDefault(n => n.CurrentAction == ActionType.Travel && n.Route is { Count: > 2 });
            if (walker is null) continue;
            Assert.Equal(walker.Location, walker.Route![0]);
            for (int i = 1; i < walker.Route.Count; i++)
                Assert.True(Math.Max(Math.Abs(walker.Route[i].X - walker.Route[i - 1].X), Math.Abs(walker.Route[i].Y - walker.Route[i - 1].Y)) <= 1,
                    "a rota anda célula a célula");
            Assert.NotNull(walker.RouteArrivalTick);
            return;
        }
        Assert.Fail("ninguém viajou por mais de duas células em dois dias");
    }

    [Fact]
    public void Stone_paving_buys_the_stone_from_a_market_that_sells_it_and_hauls_it_by_road()
    {
        var (world, _) = ScenarioRunner.Create(5);
        var city = world.ActiveCities().First();
        var ground = world.Ground!;
        var stone = new ResourceType(world.StreetRules.StoneResourceId);
        var quarry = world.Workplaces.First();
        quarry.Deposit(stone, 500, world.EconomyRules);
        quarry.SetPrice(stone, 3);
        world.AddTechnology(new Technology(world.Npcs.First().Culture, PavingTechnique.Category, new TechnologyVector([3, 6, 3]), 0, city.Id, null, [city.Id]));
        int index = ground.IndexOf(city.Location);
        ground.SetSurface(index, GroundSurface.Dirt);
        ground.AddPasses(index, 50_000);
        city.CreditTreasury(new Money(1_000_000));
        long stockBefore = quarry.Stock[stone], cashBefore = quarry.Treasury.Amount;

        PavingDecision.Monthly(world, new TickContext(world, world.Rng, world.Scheduler));

        var work = Assert.Single(ground.Works);
        Assert.Equal(GroundSurface.Stone, work.Surface);
        Assert.True(quarry.Stock[stone] < stockBefore);
        Assert.True(quarry.Treasury.Amount > cashBefore);
    }
}
