using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Simulation.Objects;

/// <summary>Uma ação possível sobre um móvel pago agora: o verbo (<c>fetch:3</c>), o texto, se vale e, se não, por quê (a mesma resposta do fantasma: <see cref="HomeCommands.Check"/>).</summary>
public sealed record HomeActionView(string Verb, string Label, bool Ok, string Reason, long Price);

/// <summary>Um móvel pago e ainda não posicionado: onde está de verdade (<c>shop</c>, <c>hands</c>, <c>car</c>, <c>courier</c>, <c>depot</c>), peso e volume (dado), quem o leva e o que dá para fazer.</summary>
public sealed record HomeParcelView(
    int Seq, string Id, string Name, string Where, double Kg, double Liters, long Paid, long ReadyInMs, long DueInMs, string? Carrier, string? Helper, string? Courier, string Carry,
    IReadOnlyList<HomeActionView> Actions, string? DueText = null, string? ReadyText = null);

/// <summary>O entregador a caminho: a rota da loja à porta (células do mundo, centro de cada uma) e os tempos (ms de mundo): quanto falta para sair e para chegar. O cliente só interpola.</summary>
public sealed record HomeCourierView(int Seq, string Id, string Item, string Name, long Npc, long StartsInMs, long ArrivesInMs, long TotalMs, IReadOnlyList<double[]> Path);

/// <summary>O carro da casa: onde está agora (célula fina; segue o corpo enquanto dirige), o porta-malas e a carga.</summary>
public sealed record HomeCarView(string Id, string Name, string Color, double X, double Y, bool Driving, long Driver, double TrunkKg, double TrunkLiters, double LoadKg, double LoadLiters);

public sealed record HomeSpotView(int X, int Y);

/// <summary>Peso, volume e jeito mais simples de trazer cada móvel à venda (<c>hands</c>, <c>helper</c> ou <c>vehicle</c>), tudo do dado.</summary>
public sealed record HomeHeftView(double Kg, double Liters, string Carry);

/// <summary>#265: a logística do modo comprar: o que está pago e onde, o carro, os entregadores a caminho, os pontos da loja e da casa, e as regras de mãos e de entrega.</summary>
public sealed record HomeHaulView(
    double ReachM, double HandsKg, double HandsLiters, long DeliveryFee, int DeliveryOpen, int DeliveryClose, HomeCarView? Car, HomeSpotView? ShopFront, HomeSpotView? HomeFront,
    IReadOnlyList<HomeParcelView> Parcels, IReadOnlyList<HomeCourierView> Couriers, IReadOnlyDictionary<string, HomeHeftView> Items);

/// <summary>#265, na ficha do corpo (aditivo): uma entrega que chegou (o cliente avisa "Seu sofá chegou" uma vez por número).</summary>
public sealed record HomeFreightNotice(int Seq, string Id, string Item, string By);

/// <summary>O móvel que o corpo leva nas mãos agora, o peso e com quem (se dois levam).</summary>
public sealed record HomeFreightCarry(int Seq, string Id, string Item, double Kg, string? With);

public sealed record HomeFreightSheet(HomeFreightCarry? Carry, IReadOnlyList<HomeFreightNotice> Arrived, int Coming);

public static class HomeFreightViews
{
    public static string CarryOf(HomeHaul haul, HomeHeft heft) =>
        heft.Kg <= haul.Hands.MaxKg && heft.Liters <= haul.Hands.MaxLiters ? "hands"
        : heft.Kg <= haul.Hands.MaxKg * haul.Hands.HelperFactor && heft.Liters <= haul.Hands.MaxLiters * haul.Hands.HelperFactor ? "helper" : "vehicle";

    private static readonly string[] AtShop = ["fetch", "fetchhelp", "load", "deliver", "cancel"];

    internal static HomeHaulView? Of(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home)
    {
        if (home.Haul is not { } haul) return null;
        long now = world.CurrentDate.TotalMilliseconds;
        var shop = HomeCommands.Shop(world, home);
        var shopFront = shop is null ? ((CellCoord Front, CellCoord Park)?)null : HomeFreight.ShopFront(world, shop);
        var homeFront = HomeFreight.FrontOf(world, building);
        string NameOf(long id) => world.FindNpc(new NpcId(id))?.Name ?? "";

        var parcels = new List<HomeParcelView>();
        var couriers = new List<HomeCourierView>();
        foreach (var p in building.Edit?.Parcels ?? [])
        {
            var heft = haul.HeftOf(p.Id) ?? new HomeHeft(0, 0);
            var actions = new List<HomeActionView>();
            string[] verbs = p.Where switch { "shop" => AtShop, "hands" => ["drop"], "car" => ["unload"], "depot" => ["sellp"], _ => [] };
            foreach (var verb in verbs)
            {
                var check = HomeCommands.Check(world, npc, building.Id.Value, $"{verb}:{p.Seq}");
                actions.Add(new HomeActionView($"{verb}:{p.Seq}", check.IsSuccess ? check.Value!.Note : "", check.IsSuccess, check.IsSuccess ? "" : check.Error!.Replace(PlayerCommands.Conflict, "").Trim(), check.Value?.Price ?? 0));
            }

            parcels.Add(new HomeParcelView(p.Seq, p.Id, HomeFreight.Name(catalog, p.Id), p.Where, heft.Kg, heft.Liters, p.Paid, Math.Max(0, p.ReadyAtMs - now), p.Where == "courier" ? Math.Max(0, p.DueMs - now) : 0,
                p.Carrier > 0 ? NameOf(p.Carrier) : null, p.Helper > 0 ? NameOf(p.Helper) : null, p.Courier > 0 ? NameOf(p.Courier) : null, CarryOf(haul, heft), actions,
                p.Where == "courier" ? LivingWorld.Simulation.Careers.CareerWork.When(world, p.DueMs) : null, p.Where == "shop" && p.ReadyAtMs > now ? LivingWorld.Simulation.Careers.CareerWork.When(world, p.ReadyAtMs) : null)); // #268: a hora de chegada, calculada pelo motor
            if (p.Where == "courier" && shopFront is { } from && homeFront is { } to && world.FindNpc(new NpcId(p.Courier)) is { } courier && HomeFreight.Path(world, courier, from.Front, to.Front) is { } path)
            {
                var cells = new List<double[]> { new[] { from.Front.X + 0.5, from.Front.Y + 0.5 } };
                cells.AddRange(path.Select(c => new[] { c.X + 0.5, c.Y + 0.5 }));
                couriers.Add(new HomeCourierView(p.Seq, p.Id, HomeFreight.Name(catalog, p.Id), courier.Name, courier.Id.Value, p.DepartMs - now, p.DueMs - now, p.DueMs - p.DepartMs, cells));
            }
        }

        HomeCarView? car = null;
        if (HomeFreight.VehicleOf(haul) is { } v)
        {
            var driver = HomeFreight.DriverOf(world, building);
            var spot = HomeFreight.CarCell(world, building);
            double x = spot.X + 0.5, y = spot.Y + 0.5;
            if (driver is not null)
            {
                var cell = PlayerGround.PoseOf(world, driver);
                (x, y) = driver.Fine is { } fine ? (fine.X / (double)FinePose.PerCell, fine.Y / (double)FinePose.PerCell) : (cell.X + 0.5, cell.Y + 0.5);
            }

            var (kg, liters) = HomeFreight.CarLoadOf(haul, building);
            car = new HomeCarView(v.Id, v.Name, v.Color, x, y, driver is not null, driver?.Id.Value ?? 0, v.TrunkKg, v.TrunkLiters, kg, liters);
        }

        var items = new Dictionary<string, HomeHeftView>();
        foreach (var item in home.Items)
            if (haul.HeftOf(item.Object) is { } h) items[item.Object] = new HomeHeftView(h.Kg, h.Liters, CarryOf(haul, h));
        return new HomeHaulView(haul.ReachM, haul.Hands.MaxKg, haul.Hands.MaxLiters, haul.Delivery.Fee, haul.Delivery.OpenHour, haul.Delivery.CloseHour, car,
            shopFront is { } s ? new HomeSpotView(s.Front.X, s.Front.Y) : null, homeFront is { } f ? new HomeSpotView(f.Front.X, f.Front.Y) : null, parcels, couriers, items);
    }

    /// <summary>O que a ficha do corpo diz do transporte: o que leva nas mãos, o que já chegou pelo entregador e quantas entregas vêm a caminho. Nulo sem nada disso.</summary>
    public static HomeFreightSheet? SheetOf(WorldState world, Npc npc)
    {
        if (world.ObjectCatalog is not { Home: { Haul: { } haul } } catalog || npc.Household is not { } h || world.FindHousehold(h)?.Dwelling is not { } dwelling
            || world.FindBuilding(dwelling)?.Edit is not { Parcels.Count: > 0 } edit) return null;
        HomeFreightCarry? carry = null;
        var arrived = new List<HomeFreightNotice>();
        int coming = 0;
        foreach (var p in edit.Parcels)
        {
            if (p.Where == "hands" && (p.Carrier == npc.Id.Value || p.Helper == npc.Id.Value))
            {
                long other = p.Carrier == npc.Id.Value ? p.Helper : p.Carrier;
                carry = new HomeFreightCarry(p.Seq, p.Id, HomeFreight.Name(catalog, p.Id), haul.HeftOf(p.Id)?.Kg ?? 0, other > 0 ? world.FindNpc(new NpcId(other))?.Name : null);
            }
            else if (p.Where == "courier") coming++;
            else if (p.Where == "depot" && p.Courier > 0) arrived.Add(new HomeFreightNotice(p.Seq, p.Id, HomeFreight.Name(catalog, p.Id), world.FindNpc(new NpcId(p.Courier))?.Name ?? ""));
        }

        return carry is null && arrived.Count == 0 && coming == 0 ? null : new HomeFreightSheet(carry, arrived, coming);
    }
}
