using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public static class ExtraordinaryMechanicSupport
{
    public static Result<(string Key, int Amount)> ParseAmount(
        string declaration, string field, bool allowSigned)
    {
        int separator = declaration.LastIndexOf(':');
        if (separator <= 0 || separator == declaration.Length - 1
            || !int.TryParse(declaration[(separator + 1)..], out int amount)
            || amount == 0 || (!allowSigned && amount < 0))
            return Result<(string, int)>.Fail(
                $"{field}: use 'alvo:magnitude' com magnitude {(allowSigned ? "não zero" : "positiva")}");
        return Result<(string, int)>.Ok((declaration[..separator], amount));
    }

    public static int ClampNeed(long value) => (int)Math.Clamp(value, 0, 100);

    public static int HalfAwayFromZero(int value) =>
        value > 0 ? (value + 1) / 2 : (value - 1) / 2;

    public static int ScaledAmount(int amount, ResolutionResult resolution) =>
        resolution == ResolutionResult.PartialSuccess ? HalfAwayFromZero(amount) : amount;

    /// <summary>
    /// Optional type sits immediately before magnitude: <c>key:type:n</c>
    /// (e.g. <c>npc.health:sunlight:-10</c>, <c>combat.strike:sunlight:8</c>).
    /// Untyped <c>key:n</c> is unchanged. IntrinsicVulnerabilities parse as
    /// <c>type</c> (factor 2) or <c>type:factor</c> (integer factor). A suffix that
    /// is not an integer keeps the whole string as a narrative type token (factor 2).
    /// </summary>
    public static bool TrySplitTypedMagnitude(string declaration, out string stripped, out string type)
    {
        stripped = declaration;
        type = "";
        var parsed = ParseAmount(declaration, "Effects", allowSigned: true);
        if (!parsed.IsSuccess)
            return false;
        string key = parsed.Value.Key;
        int separator = key.LastIndexOf(':');
        if (separator <= 0 || separator == key.Length - 1)
            return false;
        type = key[(separator + 1)..];
        stripped = $"{key[..separator]}:{parsed.Value.Amount}";
        // Numeric middle segments are magnitudes (luck.curse:10:100), not vulnerability types.
        if (type.Length == 0 || type.All(static ch => char.IsDigit(ch) || ch == '-'))
            return false;
        return true;
    }

    public static int VulnerabilityFactor(IEnumerable<string> vulnerabilities, string type)
    {
        foreach (var entry in vulnerabilities)
        {
            if (!TryParseVulnerability(entry, out string declaredType, out int factor))
                continue;
            if (string.Equals(declaredType, type, StringComparison.Ordinal))
                return factor < 1 ? 1 : factor;
        }
        return 1;
    }

    public static bool TryParseVulnerability(string declaration, out string type, out int factor)
    {
        type = declaration;
        factor = 2;
        if (string.IsNullOrEmpty(declaration))
            return false;
        int separator = declaration.LastIndexOf(':');
        if (separator <= 0 || separator == declaration.Length - 1)
            return true;
        if (!int.TryParse(declaration[(separator + 1)..], out int parsed) || parsed == 0)
            return true;
        type = declaration[..separator];
        factor = parsed < 1 ? 1 : parsed;
        return type.Length > 0;
    }

    public static int ApplyVulnerabilityFactor(int amount, int factor) =>
        amount * (factor < 1 ? 1 : factor);

    public static bool TryResource(string key, out ResourceType resource)
    {
        const string prefix = "household.resource.";
        bool valid = key.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(key[prefix.Length..], out int id) && id >= 0;
        resource = new ResourceType(valid ? int.Parse(key[prefix.Length..]) : 0);
        return valid;
    }

    public static ResourceType ResourceOf(string key)
    {
        _ = TryResource(key, out var resource);
        return resource;
    }

    public static bool IsBuildingCell(WorldState world, CellCoord cell)
    {
        foreach (var building in world.Buildings)
        {
            var position = building.Position;
            if (position is null)
            {
                if (world.FindCity(building.City) is not { } city) continue;
                var (bounds, _) = SpatialBoundsResolver.ResolveCity(
                    city, CityPopulationQuery.Population(world, city.Id), world.Map.Width, world.Map.Height);
                position = BuildingPlacementResolver.Resolve(building, city, world, bounds)?.Position;
            }
            if (position is null) continue;
            if (BuildingFootprintGenerator.Generate(building).Any(part =>
                    new CellCoord(position.Value.X + part.Cell.X, position.Value.Y + part.Cell.Y) == cell))
                return true;
        }
        return false;
    }
}
