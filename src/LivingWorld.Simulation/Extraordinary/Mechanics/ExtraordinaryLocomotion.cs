using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Struct: resolvido por NPC a cada deslocamento (quase sempre sem modificador), não vale uma alocação.</summary>
public readonly record struct ExtraordinaryLocomotionProfile(
    bool HasModifier,
    bool CanFly,
    double SpeedMultiplier);

public sealed record ExtraordinaryLocomotionAdvance(
    bool Moved,
    bool Reached,
    int Steps);

/// <summary>Adapta efeitos genéricos de locomoção ao movimento autoritativo por célula.</summary>
public static class ExtraordinaryLocomotion
{
    public static ExtraordinaryLocomotionProfile Resolve(WorldState world, Npc npc) =>
        GravityMechanic.ResolveProfile(world, npc);

    public static ExtraordinaryLocomotionAdvance Advance(
        WorldState world,
        Npc npc,
        CellCoord destination,
        long tick,
        HashSet<CellCoord> occupancy,
        ExtraordinaryLocomotionProfile profile,
        TickContext? ctx = null)
    {
        if (!profile.HasModifier || npc.Interior is not null || npc.CurrentLocation == destination)
            return new(false, npc.CurrentLocation == destination, 0);

        IReadOnlyList<CellCoord> path;
        if (profile.CanFly)
        {
            if (!world.Map.TryGetCell(destination, out _)) return new(false, false, 0);
            path = StraightLine(npc.CurrentLocation, destination);
        }
        else
        {
            var routed = MapPathfinder.ShortestPath(world.Map, npc.CurrentLocation, destination);
            if (!routed.IsSuccess) return new(false, false, 0);
            path = routed.Value!;
        }

        int budget = profile.SpeedMultiplier >= 1
            ? Math.Max(1, (int)Math.Floor(profile.SpeedMultiplier))
            : (int)Math.Floor(profile.SpeedMultiplier);
        var next = npc.CurrentLocation;
        int steps = 0;
        foreach (var cell in path.Skip(1).Take(budget))
        {
            if (occupancy.Contains(cell) || world.IsExtraordinaryConstructCell(cell) || IsBuildingCell(world, cell)) break;
            next = cell;
            steps++;
        }
        if (steps == 0) return new(false, false, 0);
        if (!TryPayMovementCosts(world, npc, tick, ctx)) return new(false, false, 0);

        occupancy.Remove(npc.CurrentLocation);
        npc.MoveTo(next, tick);
        occupancy.Add(next);
        return new(true, next == destination, steps);
    }

    private static bool IsBuildingCell(WorldState world, CellCoord cell)
    {
        foreach (var building in world.Buildings)
        {
            if (world.FindCity(building.City) is not { } city) continue;
            var (bounds, _) = SpatialBoundsResolver.ResolveCity(
                city, CityPopulationQuery.Population(world, city.Id), world.Map.Width, world.Map.Height);
            var position = building.Position
                ?? BuildingPlacementResolver.Resolve(building, city, world, bounds)?.Position;
            if (position is null) continue;
            if (BuildingFootprintGenerator.Generate(building)
                .Any(part => new CellCoord(
                    position.Value.X + part.Cell.X,
                    position.Value.Y + part.Cell.Y) == cell))
                return true;
        }
        return false;
    }

    private static bool TryPayMovementCosts(
        WorldState world, Npc npc, long tick, TickContext? ctx)
    {
        var carrier = world.CarrierOf(npc.Id);
        if (carrier is not { IsManifested: true }) return true;
        var declarations = world.Extraordinary.Descriptors
            .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal)
                && descriptor.Effects.Any(effect => effect.StartsWith("movement.", StringComparison.Ordinal)))
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .SelectMany(descriptor => descriptor.Costs)
            .ToList();
        if (declarations.Count == 0) return true;

        var parsed = new List<MovementCost>(declarations.Count);
        foreach (var declaration in declarations)
        {
            if (!TryParseCost(world, npc, declaration, out var cost)) return false;
            parsed.Add(cost);
        }
        var household = npc.Household is { } householdId ? world.FindHousehold(householdId) : null;
        foreach (var group in parsed.GroupBy(item => item.Key, StringComparer.Ordinal))
        {
            long required = group.Sum(item => (long)item.Amount);
            var first = group.First();
            long available = first.Stat is { } stat
                ? stat.Read(npc, tick)
                : household is not null
                    ? household.Stock.GetValueOrDefault(first.Resource)
                    : -1;
            if (available < required) return false;
        }
        foreach (var cost in parsed)
        {
            if (cost.Stat is { } stat)
                stat.SubtractRaw(npc, cost.Amount, tick);
            else
                household!.Withdraw(cost.Resource, cost.Amount);
            ctx?.LogEvent(WorldEventKind.ExtraordinaryCostPaid, $"{npc.Id.Value}|movement|{cost.Token}", sourceSystem: "ExtraordinaryLocomotion");
        }
        return true;
    }

    /// <summary><see cref="Stat"/> nulo = custo em estoque da casa (<see cref="Resource"/>).</summary>
    private readonly record struct MovementCost(string Token, string Key, NpcStat? Stat, ResourceType Resource, int Amount);

    /// <summary>Lê o custo pelos parsers únicos (ADR-0040), depois da mesma redução da forma tipada que o motor faz.</summary>
    private static bool TryParseCost(WorldState world, Npc npc, string declaration, out MovementCost cost)
    {
        string token = ReduceTypedCost(world, npc, declaration);
        string key = token.LastIndexOf(':') is > 0 and var separator ? token[..separator] : token;
        if (CarrierCostMechanic.Cost.TryParse(token, out var carrierCost))
        {
            cost = new(token, key, carrierCost.Stat, default, carrierCost.Amount);
            return true;
        }
        if (HouseholdResourceCostMechanic.Cost.TryParse(token, out var householdCost))
        {
            cost = new(token, key, null, householdCost.Resource, householdCost.Amount);
            return true;
        }
        cost = default;
        return false;
    }

    /// <summary><c>chave:tipo:n</c> vira <c>chave:n×vulnerabilidade</c>, como em <c>ScaleTypedMagnitude</c>; o resto passa intacto.</summary>
    private static string ReduceTypedCost(WorldState world, Npc npc, string declaration)
    {
        if (!ExtraordinaryMechanicSupport.TrySplitTypedMagnitude(declaration, out string stripped, out string type)
            || !CarrierCostMechanic.Cost.Accepts(stripped) && !HouseholdResourceCostMechanic.Cost.Accepts(stripped))
            return declaration;

        var parsed = ExtraordinaryMechanicSupport.ParseAmount(stripped, "Costs", allowSigned: false).Value;
        int factor = ExtraordinaryInvocationEngine.ResolveVulnerabilityFactor(world, npc, type);
        return $"{parsed.Key}:{ExtraordinaryMechanicSupport.ApplyVulnerabilityFactor(parsed.Amount, factor)}";
    }

    private static IReadOnlyList<CellCoord> StraightLine(CellCoord start, CellCoord goal)
    {
        var result = new List<CellCoord> { start };
        int x = start.X;
        int y = start.Y;
        int dx = Math.Abs(goal.X - x);
        int sx = x < goal.X ? 1 : -1;
        int dy = -Math.Abs(goal.Y - y);
        int sy = y < goal.Y ? 1 : -1;
        int error = dx + dy;
        while (x != goal.X || y != goal.Y)
        {
            int twice = error * 2;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
            result.Add(new CellCoord(x, y));
        }
        return result;
    }
}
