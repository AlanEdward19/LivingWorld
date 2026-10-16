using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Seletor de multi-alvo declarado no descritor (<c>area:radius:n</c> / <c>area:region:id</c>),
/// nunca um poder nomeado. Recalcula a cada invocação a partir da posição atual do portador.
/// </summary>
public static class AreaTargetResolver
{
    public static bool HasSelector(IReadOnlyList<string> effects) =>
        effects.Any(IsSelector);

    public static bool IsSelector(string token) =>
        token.StartsWith("area:radius:", StringComparison.Ordinal)
        || token.StartsWith("area:region:", StringComparison.Ordinal);

    public static Result<IReadOnlyList<NpcId>> Resolve(
        WorldState world, Npc carrier, IReadOnlyList<string> effects)
    {
        var selectors = effects.Where(IsSelector).ToList();
        if (selectors.Count == 0)
            return Result<IReadOnlyList<NpcId>>.Ok([]);

        IEnumerable<Npc> candidates = world.Npcs.Where(npc => npc.IsAlive);
        foreach (var selector in selectors.OrderBy(item => item, StringComparer.Ordinal))
        {
            var parsed = ParseSelector(selector);
            if (!parsed.IsSuccess)
                return Result<IReadOnlyList<NpcId>>.Fail(parsed.Error!);
            candidates = Filter(candidates, carrier, world, parsed.Value);
        }

        return Result<IReadOnlyList<NpcId>>.Ok(
            candidates.Select(npc => npc.Id).OrderBy(id => id.Value).ToList());
    }

    private static IEnumerable<Npc> Filter(
        IEnumerable<Npc> npcs, Npc carrier, WorldState world, AreaSelector selector) =>
        selector.Kind switch
        {
            "radius" => npcs.Where(npc => Chebyshev(npc.CurrentLocation, carrier.CurrentLocation) <= selector.Value),
            "region" => npcs.Where(npc => world.Map.TryGetCell(npc.CurrentLocation, out _)
                && world.Map.RegionOf(npc.CurrentLocation).Value == selector.Value),
            _ => npcs,
        };

    private static Result<AreaSelector> ParseSelector(string token)
    {
        var parts = token.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts[0] != "area"
            || parts[1] is not ("radius" or "region")
            || !int.TryParse(parts[2], out int value) || value < 0)
            return Result<AreaSelector>.Fail($"Effects: seletor de área inválido '{token}'");
        return Result<AreaSelector>.Ok(new AreaSelector(parts[1], value));
    }

    private static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private readonly record struct AreaSelector(string Kind, int Value);
}
