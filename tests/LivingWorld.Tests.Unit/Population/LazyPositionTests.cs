using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Population;

public class LazyPositionTests
{
    private sealed class RouteWorld(Dictionary<RouteId, MovementRoute> routes) : ILazyPositionWorld
    {
        public bool TryGetRoute(RouteId routeId, out MovementRoute route) =>
            routes.TryGetValue(routeId, out route!);
    }

    [Fact]
    public void ValueAt_without_route_returns_last_known_at_future_ticks()
    {
        var lazy = LazyPosition.Initial(new Position(3, 4), tick: 10);

        Assert.Equal(new Position(3, 4), lazy.ValueAt(10, new RouteWorld([])));
        Assert.Equal(new Position(3, 4), lazy.ValueAt(99, new RouteWorld([])));
    }

    [Fact]
    public void ValueAt_with_missing_route_returns_last_known()
    {
        var lazy = new LazyPosition(new Position(1, 1), 5, new RouteId(42));

        Assert.Equal(new Position(1, 1), lazy.ValueAt(20, new RouteWorld([])));
    }

    [Fact]
    public void ValueAt_before_tick_of_last_event_returns_last_known()
    {
        var route = MovementRoute.StepPath(
            [new CellCoord(0, 0), new CellCoord(4, 0)], startedAtTick: 10);
        var world = new RouteWorld(new Dictionary<RouteId, MovementRoute> { [new RouteId(1)] = route });
        var lazy = new LazyPosition(new Position(0, 0), 10, new RouteId(1));

        Assert.Equal(new Position(0, 0), lazy.ValueAt(9, world));
        Assert.Equal(new Position(0, 0), lazy.ValueAt(10, world));
    }

    [Fact]
    public void ValueAt_step_route_advances_one_cell_per_tick_exactly()
    {
        var route = MovementRoute.StepPath(
            [new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0), new CellCoord(3, 0)],
            startedAtTick: 100,
            ticksPerCell: 1);
        var world = new RouteWorld(new Dictionary<RouteId, MovementRoute> { [new RouteId(7)] = route });
        var lazy = new LazyPosition(new Position(0, 0), 100, new RouteId(7));

        Assert.Equal(new Position(1, 0), lazy.ValueAt(101, world));
        Assert.Equal(new Position(2, 0), lazy.ValueAt(102, world));
        Assert.Equal(new Position(3, 0), lazy.ValueAt(103, world));
        Assert.Equal(new Position(3, 0), lazy.ValueAt(104, world));
        Assert.Equal(new Position(3, 0), lazy.ValueAt(200, world));
    }

    [Fact]
    public void ValueAt_step_route_honors_ticks_per_cell()
    {
        var route = MovementRoute.StepPath(
            [new CellCoord(0, 0), new CellCoord(2, 0), new CellCoord(4, 0)],
            startedAtTick: 0,
            ticksPerCell: 2);
        var world = new RouteWorld(new Dictionary<RouteId, MovementRoute> { [new RouteId(2)] = route });
        var lazy = new LazyPosition(new Position(0, 0), 0, new RouteId(2));

        Assert.Equal(new Position(0, 0), lazy.ValueAt(1, world));
        Assert.Equal(new Position(2, 0), lazy.ValueAt(2, world));
        Assert.Equal(new Position(2, 0), lazy.ValueAt(3, world));
        Assert.Equal(new Position(4, 0), lazy.ValueAt(4, world));
    }

    [Fact]
    public void ValueAt_arrive_at_end_stays_at_origin_until_arrival_tick()
    {
        var route = MovementRoute.ArriveAt(
            new CellCoord(1, 1), new CellCoord(9, 9), startedAtTick: 50, arrivalTick: 55);
        var world = new RouteWorld(new Dictionary<RouteId, MovementRoute> { [new RouteId(3)] = route });
        var lazy = new LazyPosition(new Position(1, 1), 50, new RouteId(3));

        Assert.Equal(new Position(1, 1), lazy.ValueAt(51, world));
        Assert.Equal(new Position(1, 1), lazy.ValueAt(54, world));
        Assert.Equal(new Position(9, 9), lazy.ValueAt(55, world));
        Assert.Equal(new Position(9, 9), lazy.ValueAt(80, world));
    }

    [Fact]
    public void WithPosition_and_WithRoute_update_snapshot_fields()
    {
        var lazy = LazyPosition.Initial(new Position(0, 0), 0);
        lazy = lazy.WithPosition(new Position(5, 5), 7);
        lazy = lazy.WithRoute(new RouteId(9), 7);

        Assert.Equal(new Position(5, 5), lazy.LastKnown);
        Assert.Equal(7, lazy.TickOfLastEvent);
        Assert.Equal(new RouteId(9), lazy.PendingRoute);
    }
}
