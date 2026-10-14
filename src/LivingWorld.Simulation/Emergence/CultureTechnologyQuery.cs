using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Emergence;

public sealed record TechnologyView(string Category, int[] Vector, string Key, long DiscoveredAtTick, bool IsVariation, string? VariationOfKey, int AdoptedByCities);

/// <summary>Tecnologias de uma cultura, só pelo vetor (sem nome, ADR-0013), a partir de <c>fromTick</c>.</summary>
public static class CultureTechnologyQuery
{
    public const string UnknownCulture = "Culture: não existe";

    public static Result<IReadOnlyList<TechnologyView>> Of(WorldState world, CultureId culture, long fromTick)
    {
        bool known = world.Technologies.Any(t => t.Culture == culture) || world.Npcs.Any(n => n.Culture == culture);
        if (!known) return Result<IReadOnlyList<TechnologyView>>.Fail(UnknownCulture);

        IReadOnlyList<TechnologyView> views = world.Technologies
            .Where(t => t.Culture == culture && t.DiscoveredAtTick >= fromTick)
            .Select(t => new TechnologyView(
                t.Category, t.Vector.Axes.ToArray(), t.Vector.Key, t.DiscoveredAtTick, t.IsVariation, t.VariationOfKey, t.AdoptedBy.Count))
            .ToList();
        return Result<IReadOnlyList<TechnologyView>>.Ok(views);
    }
}
