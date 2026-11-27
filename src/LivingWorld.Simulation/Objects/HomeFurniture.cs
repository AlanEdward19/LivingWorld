using System.Runtime.CompilerServices;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Objects;
using LivingWorld.Simulation.Cities;

namespace LivingWorld.Simulation.Objects;

/// <summary>Peça de mobília com o lugar que o motor lhe deu (<see cref="FurniturePlanner"/>); <c>Spot</c> nulo = não coube sem fechar o caminho.</summary>
public sealed record HomePiece(string Id, int Variation, FurnitureSpot? Spot);

/// <summary>#237: a mobília de um prédio e onde ela fica, decidida pelo motor (antes o cliente sorteava a célula por conta própria e a cama
/// podia cair na porta). Primeiro a mobília do catálogo para o tipo do prédio, na ordem dele (índice = posição na lista, base da variação),
/// depois as camas de hospedagem que couberam. Função pura da planta e do catálogo.</summary>
public static class HomeFurniture
{
    private sealed record Plan(ObjectCatalog Catalog, IReadOnlyList<HomePiece> Pieces);

    private static readonly ConditionalWeakTable<Building, Plan> Cache = new();

    /// <summary>Mobília do prédio com <paramref name="lodgingBeds"/> camas de hospedagem (0 = só a do catálogo; esse caso fica em cache).</summary>
    public static IReadOnlyList<HomePiece> Of(Building building, ObjectCatalog catalog, int lodgingBeds = 0)
    {
        if (lodgingBeds != 0) return Compute(building, catalog, lodgingBeds);
        if (Cache.TryGetValue(building, out var cached) && ReferenceEquals(cached.Catalog, catalog)) return cached.Pieces;
        var pieces = Compute(building, catalog, 0);
        Cache.AddOrUpdate(building, new Plan(catalog, pieces));
        return pieces;
    }

    private static IReadOnlyList<HomePiece> Compute(Building building, ObjectCatalog catalog, int lodgingBeds)
    {
        var layout = BuildingLayoutCache.Of(building).Layout;
        long seed = building.Id.Value * 31 + building.BuildingTypeId;
        var furnishing = catalog.FurnishingFor(building.BuildingTypeId);
        var result = new List<HomePiece>();

        // as camas de hospedagem tomam os primeiros lugares do térreo; a mobília do catálogo fica com o que sobra
        var ground = FurniturePlanner.Plan(layout, 0, lodgingBeds + furnishing.Count, seed);
        for (int slot = 0; slot < furnishing.Count; slot++)
        {
            var id = furnishing[slot];
            result.Add(new HomePiece(id, ObjectVariation.Index(catalog.Find(id)?.VariationSeed ?? 0, slot), ground[lodgingBeds + slot]));
        }

        int remaining = lodgingBeds;
        for (int i = 0; i < lodgingBeds; i++)
            if (ground[i] is { } spot) { result.Add(new HomePiece("bed", 0, spot)); remaining--; }
        for (int floor = 1; floor < layout.Floors && remaining > 0; floor++)
            foreach (var spot in FurniturePlanner.Plan(layout, floor, remaining, seed))
                if (spot is not null) { result.Add(new HomePiece("bed", 0, spot)); remaining--; }

        return result;
    }

    /// <summary>Lugares das camas do catálogo (peças que atendem a dormir), por andar e na ordem da mobília; vazio sem cama.</summary>
    public static IReadOnlyList<FurnitureSpot> BedSpots(Building building, ObjectCatalog catalog)
    {
        var beds = new List<FurnitureSpot>();
        foreach (var piece in Of(building, catalog))
            if (piece.Spot is { } spot && catalog.Find(piece.Id) is { } descriptor
                && descriptor.UsePoints.Any(p => p.Action == (int)ActionType.Sleep))
                beds.Add(spot);
        return beds;
    }
}
