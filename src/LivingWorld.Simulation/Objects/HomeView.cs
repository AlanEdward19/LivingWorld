using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Objects;

public sealed record HomeEffectView(string Use, string Need, double PerHour, string NeedName);

/// <summary>Um móvel à venda: preço de hoje (encomenda custa mais), beleza, efeitos que o catálogo diz e o que a loja tem em estoque agora.</summary>
public sealed record HomeItemView(
    string Object, string Name, string Category, long Price, long OrderPrice, double Beauty, long Stock, bool Orderable, int CraftHours, bool Wall,
    IReadOnlyList<string> Rooms, IReadOnlyList<HomeEffectView> Effects, bool Light);

public sealed record HomePieceView(int Piece, string Id, string Name, int Floor, int X, int Y, int Facing, double Beauty, long Resale, bool Sellable, bool Busy);

public sealed record HomePaintView(string Id, string Name, string Kind, string Color, long Price, double Beauty);

public sealed record HomeOrderView(string Id, string Name, int Floor, int X, int Y, int Facing, long ReadyInMs);

public sealed record HomeRoomView(int Floor, int Index, string? Kind, int Cells, double Beauty, double Items, double Paint);

public sealed record HomeBuildView(long Wall, long Door, long Demolish, long Cell, int ExtraCells, int MaxExtraCells);

/// <summary>#264: o modo comprar lido do motor: o catálogo à venda com o estoque da loja, o que a casa tem e onde, a beleza de cada cômodo, as tintas, o preço da obra e as encomendas.
/// Só leitura, sob pedido (nunca por tick).</summary>
public sealed record HomeView(
    long Building, long Wallet, string? Shop, string? ShopNote, IReadOnlyList<HomeCategory> Categories, IReadOnlyList<HomeItemView> Items, IReadOnlyList<HomePieceView> Pieces,
    IReadOnlyList<HomeRoomView> Rooms, IReadOnlyList<HomePaintView> Paints, HomeBuildView Build, IReadOnlyList<HomeOrderView> Orders, int Floors, int ResalePercent)
{
    public static HomeView? Of(WorldState world, Npc npc)
    {
        if (world.ObjectCatalog is not { Home: { } home } catalog) return null;
        var household = npc.Household is { } h ? world.FindHousehold(h) : null;
        if (household?.Dwelling is not { } dwelling || world.FindBuilding(dwelling) is not { Position: not null } building) return null;
        var shop = HomeCommands.Shop(world, home);
        long now = world.CurrentDate.TotalMilliseconds;

        var items = new List<HomeItemView>();
        foreach (var item in home.Items)
        {
            if (catalog.Find(item.Object) is not { } descriptor) continue;
            long stock = shop?.Stock.GetValueOrDefault(new ResourceType(item.Resource)) ?? 0;
            bool orderable = stock < 1 && item.Craft is { } craft && shop is not null
                && craft.Materials.All(m => shop.Stock.GetValueOrDefault(new ResourceType(int.Parse(m.Key))) >= m.Value);
            var effects = new List<HomeEffectView>();
            foreach (var use in descriptor.Uses ?? [])
                foreach (var effect in use.Effects)
                    if (effect.PerHour != 0 && !effects.Any(e => e.Need == effect.Need)) effects.Add(new HomeEffectView(use.Name, effect.Need, effect.PerHour, catalog.Needs?.Find(effect.Need)?.Name ?? ""));
            items.Add(new HomeItemView(item.Object, descriptor.Name ?? descriptor.Id, item.Category, item.Price, item.Price * (100 + home.Rules.OrderMarkupPercent) / 100, item.Beauty,
                stock, orderable, item.Craft?.Hours ?? 0, item.Wall, item.Rooms ?? [], effects, descriptor.Light is not null));
        }

        var pieces = new List<HomePieceView>();
        var all = HomeFurniture.Of(building, catalog);
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Spot is not { } spot || catalog.Find(all[i].Id) is not { } descriptor) continue;
            var item = home.Item(all[i].Id);
            pieces.Add(new HomePieceView(i, all[i].Id, descriptor.Name ?? descriptor.Id, spot.Floor, spot.Cell.X, spot.Cell.Y, spot.Facing, item?.Beauty ?? 0,
                item is null ? 0 : item.Price * home.Rules.ResalePercent / 100, item is not null, Busy(world, building, i)));
        }

        var orders = (building.Edit?.Orders ?? []).Select(o => new HomeOrderView(o.Id, catalog.Find(o.Id)?.Name ?? o.Id, o.Floor, o.X, o.Y, o.Facing, Math.Max(0, o.ReadyAtMs - now))).ToList();
        var rooms = RoomBeauty.Rooms(building, catalog).Select(r => new HomeRoomView(r.Floor, r.Index, r.Kind, r.Cells, r.Beauty, r.Items, r.Paint)).ToList();
        var paints = home.Paints.Select(p => new HomePaintView(p.Id, p.Name, p.Kind, p.Color, p.Price, p.Beauty)).ToList();
        var floors = BuildingLayoutCache(building);
        return new HomeView(building.Id.Value, npc.Wallet.Amount, shop is null ? null : $"Loja de móveis {shop.Id.Value}", shop is null ? "Sem loja de móveis no bairro" : null,
            home.Categories, items, pieces, rooms, paints,
            new HomeBuildView(home.Build.Wall, home.Build.Door, home.Build.Demolish, home.Build.Cell, building.Edit?.Cells.Count ?? 0, home.Build.MaxExtraCells), orders, floors, home.Rules.ResalePercent);
    }

    private static int BuildingLayoutCache(Building building) => Cities.BuildingLayoutCache.Of(building).Layout.Floors;

    private static bool Busy(WorldState world, Building building, int piece)
    {
        foreach (var other in world.AliveNpcIndex.Alive)
            if (other.BodyTask is { Action: ObjectUsage.Action } task && task.Building == building.Id.Value && task.Piece == piece) return true;
        return false;
    }
}
