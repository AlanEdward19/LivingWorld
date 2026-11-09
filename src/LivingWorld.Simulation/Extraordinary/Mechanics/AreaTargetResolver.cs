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

    public static bool IsSelector(string token) => AreaSelectorMechanic.IsSelector(token);

    public static Result<IReadOnlyList<NpcId>> Resolve(
        WorldState world, Npc carrier, IReadOnlyList<string> effects)
    {
        var selectors = effects.Where(IsSelector).ToList();
        if (selectors.Count == 0)
            return Result<IReadOnlyList<NpcId>>.Ok([]);

        IEnumerable<Npc> candidates = world.Npcs.Where(npc => npc.IsAlive);
        foreach (var selector in selectors.OrderBy(item => item, StringComparer.Ordinal))
        {
            var parsed = AreaSelectorMechanic.Selector.Parse(selector);
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

    private static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
