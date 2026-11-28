using System.Runtime.CompilerServices;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
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
    private sealed record Plan(ObjectCatalog Catalog, int Version, IReadOnlyList<HomePiece> Pieces);

    private static readonly ConditionalWeakTable<Building, Plan> Cache = new();

    /// <summary>Mobília do prédio com <paramref name="lodgingBeds"/> camas de hospedagem (0 = só a do catálogo; esse caso fica em cache).</summary>
    public static IReadOnlyList<HomePiece> Of(Building building, ObjectCatalog catalog, int lodgingBeds = 0)
    {
        if (lodgingBeds != 0) return Compute(building, catalog, lodgingBeds);
        int version = building.Edit?.Version ?? -1;
        if (Cache.TryGetValue(building, out var cached) && ReferenceEquals(cached.Catalog, catalog) && cached.Version == version) return cached.Pieces;
        var pieces = Compute(building, catalog, 0);
        Cache.AddOrUpdate(building, new Plan(catalog, version, pieces));
        return pieces;
    }

    private static IReadOnlyList<HomePiece> Compute(Building building, ObjectCatalog catalog, int lodgingBeds)
    {
        // #264: o planejador trabalha na planta e nos cômodos de ORIGEM (a obra do morador não reembaralha a mobília do catálogo); a compra, a venda e a mudança entram por cima
        var layout = BuildingLayoutCache.BaseOf(building).Layout;
        var rooms = BuildingRooms.BaseOf(building, catalog);
        long seed = building.Id.Value * 31 + building.BuildingTypeId;
        var furnishing = catalog.FurnishingFor(building.BuildingTypeId);
        var result = new List<HomePiece>();

        // as camas de hospedagem tomam os primeiros lugares do térreo; a mobília do catálogo fica com o que sobra.
        // #226: cada peça só vai para os cômodos que atendem ao uso dela (o que o catálogo declara), senão para qualquer um
        var wanted = new List<IReadOnlySet<CellCoord>?>();
        for (int i = 0; i < lodgingBeds; i++) wanted.Add(RoomCells(rooms.Floors[0], catalog, "bed"));
        foreach (var id in furnishing) wanted.Add(RoomCells(rooms.Floors[0], catalog, id));
        var ground = FurniturePlanner.Plan(layout, rooms.Floors[0], 0, lodgingBeds + furnishing.Count, seed, wanted);
        for (int slot = 0; slot < furnishing.Count; slot++)
        {
            var id = furnishing[slot];
            result.Add(new HomePiece(id, ObjectVariation.Index(catalog.Find(id)?.VariationSeed ?? 0, slot), ground[lodgingBeds + slot]));
        }

        ApplyEdit(building, result);
        int remaining = lodgingBeds;
        for (int i = 0; i < lodgingBeds; i++)
            if (ground[i] is { } spot) { result.Add(new HomePiece("bed", 0, spot)); remaining--; }
        for (int floor = 1; floor < layout.Floors && remaining > 0; floor++)
            foreach (var spot in FurniturePlanner.Plan(layout, rooms.Floors[floor], floor, remaining, seed,
                         Enumerable.Repeat(RoomCells(rooms.Floors[floor], catalog, "bed"), remaining).ToArray()))
                if (spot is not null) { result.Add(new HomePiece("bed", 0, spot)); remaining--; }

        return result;
    }

    /// <summary>#264: por cima da mobília do catálogo, o que o morador fez: a peça vendida some (o índice fica), a movida muda de lugar e as compradas entram no fim, na ordem da compra.</summary>
    private static void ApplyEdit(Building building, List<HomePiece> pieces)
    {
        if (building.Edit is not { } edit) return;
        foreach (var move in edit.Moved)
            if (move.Piece >= 0 && move.Piece < pieces.Count)
                pieces[move.Piece] = pieces[move.Piece] with { Spot = new FurnitureSpot(move.Floor, new CellCoord(move.X, move.Y), move.Facing) };
        foreach (var gone in edit.Removed)
            if (gone >= 0 && gone < pieces.Count) pieces[gone] = pieces[gone] with { Spot = null };
        foreach (var bought in edit.Added)
            pieces.Add(new HomePiece(bought.Id, bought.Variation, bought.Gone ? null : new FurnitureSpot(bought.Floor, new CellCoord(bought.X, bought.Y), bought.Facing)));
    }

    /// <summary>Quantas peças do catálogo do tipo de prédio há (as compradas vêm depois delas na mobília).</summary>
    public static int BaseCount(Building building, ObjectCatalog catalog) => catalog.FurnishingFor(building.BuildingTypeId).Count;

    /// <summary>As células dos cômodos do andar que atendem a algum uso do objeto (pontos de uso com ação, no catálogo, contra os usos do cômodo); nulo = sem
    /// restrição (objeto sem uso declarado, andar sem divisão ou nenhum cômodo que o atenda).</summary>
    private static IReadOnlySet<CellCoord>? RoomCells(FloorRooms floor, ObjectCatalog catalog, string objectId)
    {
        if (floor.Rooms.Count < 2 || catalog.Rooms is not { } roomCatalog || catalog.Find(objectId) is not { } descriptor) return null;
        if (descriptor.Rooms is { Count: > 0 } named) // #255: o cenário diz em que cômodos a peça fica (o chuveiro no banheiro)
        {
            var byName = new HashSet<CellCoord>();
            foreach (var room in floor.Rooms)
                if (named.Contains(room.Kind))
                    foreach (var cell in room.Cells) byName.Add(cell);
            return byName.Count > 0 ? byName : null;
        }

        var uses = descriptor.UsePoints.Where(p => p.Action is not null).Select(p => p.Action!.Value).ToHashSet();
        if (uses.Count == 0) return null;
        var cells = new HashSet<CellCoord>();
        foreach (var room in floor.Rooms)
            if (roomCatalog.Kinds.FirstOrDefault(k => k.Id == room.Kind) is { } kind && kind.Uses.Any(uses.Contains))
                foreach (var cell in room.Cells) cells.Add(cell);
        return cells.Count > 0 ? cells : null;
    }

    /// <summary>O cômodo (tipo do catálogo) em que está o lugar, ou nulo sem divisão; só leitura, para o cliente e para os testes.</summary>
    public static string? RoomOf(Building building, ObjectCatalog? catalog, FurnitureSpot spot) =>
        BuildingRooms.Of(building, catalog).RoomAt(spot.Floor, spot.Cell)?.Kind;

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
