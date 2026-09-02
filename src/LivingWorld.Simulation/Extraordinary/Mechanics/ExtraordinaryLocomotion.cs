using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed record ExtraordinaryLocomotionProfile(
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
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (carrier is not { IsManifested: true }) return true;
        var declarations = world.Extraordinary.Descriptors
            .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal)
                && descriptor.Effects.Any(effect => effect.StartsWith("movement.", StringComparison.Ordinal)))
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .SelectMany(descriptor => descriptor.Costs)
            .ToList();
        if (declarations.Count == 0) return true;

        var parsed = new List<(string Token, string Key, int Amount)>();
        foreach (var declaration in declarations)
        {
            int separator = declaration.LastIndexOf(':');
            if (separator <= 0 || !int.TryParse(declaration[(separator + 1)..], out int amount) || amount <= 0)
                return false;
            parsed.Add((declaration, declaration[..separator], amount));
        }
        var household = npc.Household is { } householdId ? world.FindHousehold(householdId) : null;
        foreach (var group in parsed.GroupBy(item => item.Key, StringComparer.Ordinal))
        {
            long required = group.Sum(item => (long)item.Amount);
            long available = group.Key switch
            {
                "carrier.health" => npc.Health,
                "carrier.hunger" => npc.HungerAt(tick),
                "carrier.thirst" => npc.ThirstAt(tick),
                "carrier.sleep" => npc.SleepAt(tick),
                "carrier.social" => npc.SocialAt(tick),
                _ when TryHouseholdResource(group.Key, out var resource) && household is not null =>
                    household.Stock.GetValueOrDefault(resource),
                _ => -1,
            };
            if (available < required) return false;
        }
        foreach (var (token, key, amount) in parsed)
        {
            switch (key)
            {
                case "carrier.health": npc.SetHealth(npc.Health - amount); break;
                case "carrier.hunger": npc.SetHunger(npc.HungerAt(tick) - amount, tick); break;
                case "carrier.thirst": npc.SetThirst(npc.ThirstAt(tick) - amount, tick); break;
                case "carrier.sleep": npc.SetSleep(npc.SleepAt(tick) - amount, tick); break;
                case "carrier.social": npc.SetSocial(npc.SocialAt(tick) - amount, tick); break;
                default:
                    _ = TryHouseholdResource(key, out var resource);
                    household!.Withdraw(resource, amount);
                    break;
            }
            ctx?.LogEvent(WorldEventKind.ExtraordinaryCostPaid, $"{npc.Id.Value}|movement|{token}", sourceSystem: "ExtraordinaryLocomotion");
        }
        return true;
    }

    private static bool TryHouseholdResource(string key, out ResourceType resource)
    {
        const string prefix = "household.resource.";
        bool valid = key.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(key[prefix.Length..], out int id) && id >= 0;
        resource = new ResourceType(valid ? int.Parse(key[prefix.Length..]) : 0);
        return valid;
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
