using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Posição no grid para recomputação cosmética lazy (Fase 28, LOD-10) — mesmo espaço que
/// <see cref="CellCoord"/>, tipo distinto para a camada aproximada.</summary>
public readonly record struct Position(int X, int Y)
{
    public static implicit operator Position(CellCoord cell) => new(cell.X, cell.Y);
    public static implicit operator CellCoord(Position position) => new(position.X, position.Y);
}

/// <summary>Modo de percorrer uma rota cosmética — espelha os dois padrões do motor: um passo por
/// tick (<see cref="ExtraordinaryLocomotion"/>) ou espera e chega no fim
/// (<see cref="TravelResolution"/>).</summary>
public enum MovementRouteKind
{
    StepPerTick,
    ArriveAtEnd,
}

/// <summary>Rota fechada para <see cref="LazyPosition.ValueAt"/> — waypoints + parâmetros de tempo;
/// vive fora do <see cref="LazyPosition"/> e é resolvida por id via
/// <see cref="ILazyPositionWorld"/>.</summary>
public readonly record struct MovementRoute(
    CellCoord[] Waypoints,
    long StartedAtTick,
    MovementRouteKind Kind,
    int TicksPerCell = 1,
    long ArrivalTick = 0)
{
    public static MovementRoute StepPath(IReadOnlyList<CellCoord> waypoints, long startedAtTick, int ticksPerCell = 1) =>
        new(waypoints.ToArray(), startedAtTick, MovementRouteKind.StepPerTick, Math.Max(1, ticksPerCell), 0);

    public static MovementRoute ArriveAt(CellCoord origin, CellCoord destination, long startedAtTick, long arrivalTick) =>
        new([origin, destination], startedAtTick, MovementRouteKind.ArriveAtEnd, 1, arrivalTick);
}

/// <summary>Consulta de rotas cosméticas sem acoplar Domain a Simulation — implementado por
/// <c>WorldState</c> (T5) ou por stubs de teste.</summary>
public interface ILazyPositionWorld
{
    bool TryGetRoute(RouteId routeId, out MovementRoute route);
}

/// <summary>Posição cosmética lazy (Fase 28, LOD-10) — valor materializado só em
/// <see cref="ValueAt"/>, nunca escrito por tick quando o NPC está fora de escopo observado.</summary>
public readonly record struct LazyPosition(Position LastKnown, long TickOfLastEvent, RouteId? PendingRoute)
{
    public Position ValueAt(long tick, ILazyPositionWorld world)
    {
        if (PendingRoute is not { } routeId || !world.TryGetRoute(routeId, out var route))
            return LastKnown;

        if (tick <= TickOfLastEvent)
            return LastKnown;

        return RoutePositionAt(route, tick);
    }

    public LazyPosition WithPosition(Position position, long tick) =>
        new(position, tick, PendingRoute);

    public LazyPosition WithRoute(RouteId? route, long tick) =>
        new(LastKnown, tick, route);

    public static LazyPosition Initial(Position position, long tick) =>
        new(position, tick, null);

    internal static Position RoutePositionAt(MovementRoute route, long tick)
    {
        if (route.Waypoints.Length == 0)
            return default;

        if (tick <= route.StartedAtTick)
            return route.Waypoints[0];

        return route.Kind switch
        {
            MovementRouteKind.ArriveAtEnd => tick < route.ArrivalTick
                ? route.Waypoints[0]
                : route.Waypoints[^1],
            MovementRouteKind.StepPerTick => StepPositionAt(route, tick),
            _ => route.Waypoints[^1],
        };
    }

    private static Position StepPositionAt(MovementRoute route, long tick)
    {
        long elapsed = tick - route.StartedAtTick;
        int lastIndex = route.Waypoints.Length - 1;
        long index = elapsed / route.TicksPerCell;
        if (index > lastIndex)
            return route.Waypoints[lastIndex];

        return route.Waypoints[(int)index];
    }
}
