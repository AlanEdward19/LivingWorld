using System.Globalization;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Objects;

/// <summary>#265: o móvel pago não aparece em casa: fica reservado no estoque da loja em nome da casa e vem de verdade (<see cref="Parcel"/>). Três jeitos, todos em dado (<c>Home.haul</c>):
/// <b>nas mãos</b> (<c>fetch</c>: o corpo anda da loja até a casa com a carga, mais devagar; só o que cabe nas mãos, ou nas de duas pessoas, <c>fetchhelp</c>), <b>de carro</b> (<c>load</c> no
/// porta-malas, <c>drive:shop|home</c> pelo corpo que dirige, <c>unload</c> em casa) ou <b>entrega</b> (<c>deliver</c>: um empregado da loja leva até a porta num trajeto real, a partir do
/// próximo horário de entrega). Em casa o móvel vai para o depósito (<c>drop</c>, <c>unload</c> ou a chegada do entregador) e de lá o jogador o põe no lugar (<c>place</c>) ou guarda de volta
/// (<c>stash</c>). Moeda só muda de mão; nada nasce nem se teletransporta.</summary>
public static class HomeFreight
{
    /// <summary>O <c>Action</c> do <see cref="BodyTask"/> de quem dirige o carro da casa.</summary>
    public const string DriveAction = "haul-drive";

    private const long MinuteMs = 60_000;
    private const long GiveUpMs = 90_000;

    public static HomeHaul? HaulOf(WorldState world) => world.ObjectCatalog?.Home?.Haul;

    // ---------------- o que é pago e onde está ----------------

    public static Parcel? Find(Building building, int seq) => building.Edit?.Parcels.FirstOrDefault(p => p.Seq == seq);

    public static string Name(ObjectCatalog catalog, string id) => catalog.Find(id)?.Name ?? id;

    internal static void Update(WorldState world, Building building, Parcel old, Parcel updated)
    {
        var list = building.Edit!.Parcels;
        list[list.IndexOf(old)] = updated;
        Touched(world, building);
    }

    private static void Touched(WorldState world, Building building)
    {
        building.Edit!.Touch();
        world.TouchBuildings();
    }

    /// <summary>Compra: o móvel pago entra reservado na loja (ou direto no depósito num cenário sem <c>haul</c>).</summary>
    internal static void Reserve(WorldState world, Building building, string objectId, int variation, long paid, long readyAtMs)
    {
        var edit = building.Edit!;
        edit.Parcels.Add(new Parcel(edit.NextParcel++, objectId, variation, paid, HaulOf(world) is null ? "depot" : "shop", readyAtMs));
    }

    // ---------------- lugares ----------------

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public static int ReachCells(WorldState world, HomeHaul haul) => Math.Max(1, (int)Math.Ceiling(haul.ReachM / Math.Max(0.1, world.Players.CellM)));

    private static bool Free(WorldState world, CellCoord cell) => PlayerGround.Walkable(world, cell) && PlayerGround.At(world, cell) is null;

    /// <summary>A soleira do prédio (fora, à frente da porta) e o ponto de parada do carro: uma célula adiante se está livre.</summary>
    public static (CellCoord Front, CellCoord Park)? FrontOf(WorldState world, Building building)
    {
        if (PlayerGround.DoorOf(building) is not { } door) return null;
        var out1 = new CellCoord(door.Doorstep.X + (door.Doorstep.X - door.Door.X), door.Doorstep.Y + (door.Doorstep.Y - door.Door.Y));
        return (door.Doorstep, Free(world, out1) ? out1 : door.Doorstep);
    }

    private static Building? ShopBuilding(WorldState world, Workplace shop) => shop.Building is { } id ? world.FindBuilding(id) : null;

    public static (CellCoord Front, CellCoord Park) ShopFront(WorldState world, Workplace shop) =>
        ShopBuilding(world, shop) is { } b && FrontOf(world, b) is { } front ? front : (shop.Location, shop.Location);

    /// <summary>Onde o carro da casa está: parado onde alguém o deixou, ou no lugar de partida, em frente à porta.</summary>
    public static CellCoord CarCell(WorldState world, Building building) =>
        building.Edit?.Car is { } car ? new CellCoord(car.X, car.Y) : FrontOf(world, building)?.Park ?? building.Position ?? default;

    public static HomeVehicle? VehicleOf(HomeHaul haul) => haul.Fleet.Count > 0 ? haul.Vehicle(haul.Fleet[0]) : null;

    private static bool BodyNear(WorldState world, Npc npc, CellCoord front, long? building, int reach) =>
        Cheb(PlayerGround.PoseOf(world, npc), front) <= reach || (building is { } b && PlayerGround.InsideBuilding(world, npc) == b);

    private static bool AtHome(WorldState world, Npc npc, Building home, HomeHaul haul) => FrontOf(world, home) is { } f && BodyNear(world, npc, f.Front, home.Id.Value, ReachCells(world, haul));

    private static bool AtShop(WorldState world, Npc npc, Workplace shop, HomeHaul haul) => BodyNear(world, npc, ShopFront(world, shop).Front, shop.Building?.Value, ReachCells(world, haul));

    private static (double Kg, double Liters) Heft(HomeHaul haul, Parcel p) => haul.HeftOf(p.Id) is { } h ? (h.Kg, h.Liters) : (0, 0);

    // ---------------- verbos ----------------

    /// <summary>Os verbos de logística do <c>interact {kind:"home"}</c>; nulo se o verbo não é daqui.</summary>
    internal static Result<HomeOutcome>? Run(WorldState world, TickContext? ctx, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, string[] parts, bool commit)
    {
        int Int(int i) => int.Parse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var haul = home.Haul;
        switch (parts[0])
        {
            case "place" when parts.Length == 6: return Place(world, building, catalog, home, Int(1), Int(2), Int(3), Int(4), Int(5), commit);
            case "stash" when parts.Length == 2: return Stash(world, building, catalog, home, Int(1), commit);
            case "sellp" or "cancel" when parts.Length == 2: return Return(world, npc, building, catalog, home, Int(1), parts[0] == "sellp", commit);
        }

        if (haul is null) return parts[0] is "fetch" or "fetchhelp" or "load" or "unload" or "drop" or "deliver" or "drive" ? HomeCommands.Refuse("este mundo não tem transporte de móveis") : null;
        return parts[0] switch
        {
            "fetch" when parts.Length == 2 => Fetch(world, npc, building, catalog, home, haul, Int(1), helped: false, commit),
            "fetchhelp" when parts.Length == 2 => Fetch(world, npc, building, catalog, home, haul, Int(1), helped: true, commit),
            "drop" when parts.Length == 2 => Drop(world, npc, building, catalog, haul, Int(1), commit),
            "load" when parts.Length == 2 => Load(world, npc, building, catalog, home, haul, Int(1), commit),
            "unload" when parts.Length == 2 => Unload(world, npc, building, catalog, haul, Int(1), commit),
            "deliver" when parts.Length == 2 => Deliver(world, ctx, npc, building, catalog, home, haul, Int(1), commit),
            "drive" when parts.Length == 2 => Drive(world, ctx, npc, building, home, haul, parts[1], commit),
            _ => null,
        };
    }

    private static Result<HomeOutcome> WithParcel(Building building, int seq, Func<Parcel, Result<HomeOutcome>> then) =>
        Find(building, seq) is { } p ? then(p) : HomeCommands.Refuse("esse móvel não está mais à espera");

    private static Result<HomeOutcome> Fetch(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, HomeHaul haul, int seq, bool helped, bool commit) =>
        WithParcel(building, seq, p =>
        {
            if (p.Where != "shop" || p.ReadyAtMs > world.CurrentDate.TotalMilliseconds) return HomeCommands.Refuse(p.Where == "shop" ? "a loja ainda está fazendo esse móvel" : "esse móvel não está na loja");
            if (HomeCommands.Shop(world, home) is not { } shop || !AtShop(world, npc, shop, haul)) return HomeCommands.Refuse("chegue até a loja para levar o móvel");
            var name = Name(catalog, p.Id);
            var hands = building.Edit!.Parcels.Any(o => o.Where == "hands" && (o.Carrier == npc.Id.Value || o.Helper == npc.Id.Value));
            if (hands) return HomeCommands.Refuse("as mãos já estão ocupadas com outro móvel");
            var (kg, liters) = Heft(haul, p);
            Npc? helper = null;
            if (helped)
            {
                helper = world.AliveNpcIndex.Alive.Where(o => o.Id != npc.Id && o.Household == npc.Household && o.AgeYears(world.CurrentDate) >= 16 && AtShop(world, o, shop, haul)
                    && !building.Edit.Parcels.Any(x => x.Where == "hands" && (x.Carrier == o.Id.Value || x.Helper == o.Id.Value))).OrderBy(o => o.Id.Value).FirstOrDefault();
                if (helper is null) return HomeCommands.Refuse("ninguém da casa está na loja para ajudar a carregar");
            }

            double factor = helped ? haul.Hands.HelperFactor : 1;
            if (kg > haul.Hands.MaxKg * factor || liters > haul.Hands.MaxLiters * factor)
                return HomeCommands.Refuse(string.Create(CultureInfo.InvariantCulture, $"{name} pesa {kg:0.#} kg e ocupa {liters:0} L: nas mãos só vão {haul.Hands.MaxKg * factor:0.#} kg e {haul.Hands.MaxLiters * factor:0} L; leve de carro{(helped ? "" : ", com ajuda")} ou peça a entrega"));
            if (!commit) return HomeCommands.Ok(0, 0, helped ? $"Levar {name} com {helper!.Name}" : $"Levar {name} nas mãos");
            Update(world, building, p, p with { Where = "hands", Carrier = npc.Id.Value, Helper = helper?.Id.Value ?? 0 });
            return HomeCommands.Ok(0, 0, helped ? $"{name} nas mãos de vocês dois: levem até em casa" : $"{name} nas mãos: leve até em casa");
        });

    private static Result<HomeOutcome> Drop(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeHaul haul, int seq, bool commit) =>
        WithParcel(building, seq, p =>
        {
            if (p.Where != "hands" || (p.Carrier != npc.Id.Value && p.Helper != npc.Id.Value)) return HomeCommands.Refuse("você não está levando esse móvel");
            if (!AtHome(world, npc, building, haul)) return HomeCommands.Refuse("deixe o móvel em casa: ainda não chegou");
            if (p.Helper > 0 && world.FindNpc(new NpcId(p.Helper == npc.Id.Value ? p.Carrier : p.Helper)) is { } other && !AtHome(world, other, building, haul)) return HomeCommands.Refuse($"{other.Name} ainda não chegou em casa com o móvel");
            if (!commit) return HomeCommands.Ok(0, 0, $"Deixar {Name(catalog, p.Id)} no depósito");
            Update(world, building, p, p with { Where = "depot", Carrier = 0, Helper = 0 });
            return HomeCommands.Ok(0, 0, $"{Name(catalog, p.Id)} no depósito da casa");
        });

    private static (double Kg, double Liters) CarLoad(HomeHaul haul, Building building)
    {
        double kg = 0, liters = 0;
        foreach (var o in building.Edit?.Parcels ?? [])
            if (o.Where == "car") { var h = Heft(haul, o); kg += h.Kg; liters += h.Liters; }
        return (kg, liters);
    }

    public static (double Kg, double Liters) CarLoadOf(HomeHaul haul, Building building) => CarLoad(haul, building);

    private static Result<HomeOutcome> Load(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, HomeHaul haul, int seq, bool commit) =>
        WithParcel(building, seq, p =>
        {
            if (p.Where != "shop" || p.ReadyAtMs > world.CurrentDate.TotalMilliseconds) return HomeCommands.Refuse(p.Where == "shop" ? "a loja ainda está fazendo esse móvel" : "esse móvel não está na loja");
            if (VehicleOf(haul) is not { } car) return HomeCommands.Refuse("a casa não tem carro");
            if (HomeCommands.Shop(world, home) is not { } shop || !AtShop(world, npc, shop, haul)) return HomeCommands.Refuse("chegue até a loja com o carro para carregar");
            var carCell = CarCell(world, building);
            int reach = ReachCells(world, haul);
            if (Cheb(carCell, ShopFront(world, shop).Front) > reach) return HomeCommands.Refuse("o carro não está na loja: dirija até lá");
            if (Cheb(PlayerGround.PoseOf(world, npc), carCell) > reach) return HomeCommands.Refuse("o carro está longe de você");
            var (kg, liters) = Heft(haul, p);
            var (usedKg, usedL) = CarLoad(haul, building);
            if (usedKg + kg > car.TrunkKg || usedL + liters > car.TrunkLiters)
                return HomeCommands.Refuse(string.Create(CultureInfo.InvariantCulture, $"não cabe no porta-malas: {usedKg + kg:0.#} de {car.TrunkKg:0.#} kg e {usedL + liters:0} de {car.TrunkLiters:0} L"));
            if (!commit) return HomeCommands.Ok(0, 0, $"Carregar {Name(catalog, p.Id)} no {car.Name.ToLowerInvariant()}");
            Update(world, building, p, p with { Where = "car" });
            return HomeCommands.Ok(0, 0, $"{Name(catalog, p.Id)} no porta-malas");
        });

    private static Result<HomeOutcome> Unload(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeHaul haul, int seq, bool commit) =>
        WithParcel(building, seq, p =>
        {
            if (p.Where != "car") return HomeCommands.Refuse("esse móvel não está no carro");
            if (PlayerTasks.Of(world, npc) is not null) return HomeCommands.Refuse("o corpo está ocupado");
            if (FrontOf(world, building) is not { } front || Cheb(CarCell(world, building), front.Front) > ReachCells(world, haul)) return HomeCommands.Refuse("o carro não está em casa: dirija até aqui");
            if (Cheb(PlayerGround.PoseOf(world, npc), CarCell(world, building)) > ReachCells(world, haul)) return HomeCommands.Refuse("o carro está longe de você");
            if (!commit) return HomeCommands.Ok(0, 0, $"Descarregar {Name(catalog, p.Id)} no depósito");
            Update(world, building, p, p with { Where = "depot" });
            return HomeCommands.Ok(0, 0, $"{Name(catalog, p.Id)} no depósito da casa");
        });

    // ---------------- carro ----------------

    private static Result<HomeOutcome> Drive(WorldState world, TickContext? ctx, Npc npc, Building building, HomeCatalog home, HomeHaul haul, string to, bool commit)
    {
        if (VehicleOf(haul) is not { } car) return HomeCommands.Refuse("a casa não tem carro");
        if (PlayerTasks.Of(world, npc) is not null || npc.BodyTask is { Action: DriveAction }) return HomeCommands.Refuse("o corpo está ocupado");
        if (HomeCommands.Shop(world, home) is not { } shop) return HomeCommands.Refuse("não há loja de móveis no bairro");
        var carCell = CarCell(world, building);
        if (Cheb(PlayerGround.PoseOf(world, npc), carCell) > ReachCells(world, haul)) return HomeCommands.Refuse("o carro está longe de você");
        if (building.Edit?.Parcels.Any(o => o.Where == "hands" && (o.Carrier == npc.Id.Value || o.Helper == npc.Id.Value)) == true) return HomeCommands.Refuse("guarde o móvel no porta-malas antes de dirigir");
        CellCoord target;
        if (to == "shop") target = ShopFront(world, shop).Park;
        else if (to == "home" && FrontOf(world, building) is { } front) target = front.Park;
        else return HomeCommands.Refuse("dirija para a loja (shop) ou para casa (home)");
        if (target == carCell) return HomeCommands.Refuse(to == "shop" ? "o carro já está na loja" : "o carro já está em casa");
        if (!commit) return HomeCommands.Ok(0, 0, to == "shop" ? $"Dirigir o {car.Name.ToLowerInvariant()} até a loja" : $"Dirigir o {car.Name.ToLowerInvariant()} para casa");

        long now = world.CurrentDate.TotalMilliseconds;
        PlayerCommands.TakeTheWheel(world, npc);
        npc.SetFine(new FinePose(carCell.X * FinePose.PerCell + 500, carCell.Y * FinePose.PerCell + 500, 0, 0, now)); // entra no carro: o corpo vai para onde ele está
        if (PlayerGround.PoseOf(world, npc) != carCell) npc.MoveTo(carCell, world.CurrentDate.TotalHours);
        long estimate = (long)(Cheb(carCell, target) * 1.6 * world.Players.CellM / car.Mps * 1000) + 8000;
        npc.SetBodyTask(new BodyTask(DriveAction, now, now + estimate) { Building = building.Id.Value, CellX = target.X, CellY = target.Y, LastMs = now });
        ctx?.ScheduleEventAtMs(now + 1, BodyTaskSystem.SystemName, $"{npc.Id.Value}|{now}|f");
        return HomeCommands.Ok(0, 0, to == "shop" ? "A caminho da loja" : "A caminho de casa");
    }

    /// <summary>O evento da direção (<see cref="BodyTaskSystem"/>): o corpo, com o carro, anda pela rota do chão até o ponto de parada à velocidade do veículo.</summary>
    public static void DriveStep(WorldState world, TickContext ctx, Npc npc, long startedAtMs)
    {
        if (npc.BodyTask is not { Action: DriveAction } task || task.StartedAtMs != startedAtMs || world.FindBuilding(new BuildingId(task.Building)) is not { } building) return;
        long now = world.CurrentDate.TotalMilliseconds;
        var target = new CellCoord(task.CellX, task.CellY);
        var fineTarget = (X: target.X * FinePose.PerCell + 500L, Y: target.Y * FinePose.PerCell + 500L);
        if (!world.FineTime)
        {
            npc.SetFine(new FinePose(fineTarget.X, fineTarget.Y, 0, 0, now));
            npc.MoveTo(target, world.CurrentDate.TotalHours);
            Park(world, npc, building, target);
            return;
        }

        var cell = PlayerGround.PoseOf(world, npc);
        var pose = npc.Fine is { } f && f.CellX == cell.X && f.CellY == cell.Y ? f : new FinePose(cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500, 0, 0, now);
        long elapsed = Math.Clamp(now - Math.Max(task.LastMs, pose.AtMs), 0, 4L * Math.Max(1, world.TickDeltaMs));
        var moved = PlayerWalk.Approach(world, ctx, npc, pose, fineTarget.X, fineTarget.Y, elapsed);
        if (moved.Arrived)
        {
            npc.SetFine(moved.Pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
            Park(world, npc, building, target);
            return;
        }

        npc.SetFine(moved.Pose with { AtMs = now });
        if (moved.Blocked && now - task.StartedAtMs > GiveUpMs)
        {
            npc.SetFine(moved.Pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
            Park(world, npc, building, PlayerGround.PoseOf(world, npc));
            return;
        }

        npc.SetBodyTask(task with { LastMs = now, UntilMs = Math.Max(task.UntilMs, now + 4000) });
        ctx.ScheduleEventAtMs(now + Math.Max(1, world.TickDeltaMs), BodyTaskSystem.SystemName, $"{npc.Id.Value}|{task.StartedAtMs}|f");
    }

    private static void Park(WorldState world, Npc npc, Building building, CellCoord at)
    {
        npc.SetBodyTask(null);
        var edit = building.Edit ??= new HomeEdit();
        edit.Car = FrontOf(world, building)?.Park == at ? null : new CarSpot(at.X, at.Y);
        Touched(world, building);
    }

    /// <summary>Quem dirige agora (o corpo com o <see cref="BodyTask"/> de direção) para esta casa, ou nulo.</summary>
    public static Npc? DriverOf(WorldState world, Building building)
    {
        foreach (var n in world.AliveNpcIndex.Alive)
            if (n.BodyTask is { Action: DriveAction } t && t.Building == building.Id.Value) return n;
        return null;
    }

    /// <summary>A velocidade do corpo que dirige (m/s do veículo, em dado); nula se não dirige.</summary>
    public static double? DrivingMps(WorldState world, Npc npc) =>
        npc.BodyTask is { Action: DriveAction } && HaulOf(world) is { } haul && VehicleOf(haul) is { } car ? car.Mps : null;

    /// <summary>Quanto a carga nas mãos alonga o passo (1 = sem carga): proporcional ao peso, dividido entre os dois que levam.</summary>
    public static double LoadFactor(WorldState world, Npc npc)
    {
        if (world.ObjectCatalog?.Home?.Haul is not { } haul || npc.Household is not { } h || world.FindHousehold(h)?.Dwelling is not { } dwelling
            || world.FindBuilding(dwelling)?.Edit is not { Parcels.Count: > 0 } edit) return 1.0;
        foreach (var p in edit.Parcels)
            if (p.Where == "hands" && (p.Carrier == npc.Id.Value || p.Helper == npc.Id.Value))
            {
                double share = Heft(haul, p).Kg / (p.Helper > 0 ? 2 : 1);
                return 1.0 + haul.Hands.SlowPercent / 100.0 * Math.Min(1.0, share / haul.Hands.MaxKg);
            }

        return 1.0;
    }

    // ---------------- entrega ----------------

    private static Result<HomeOutcome> Deliver(WorldState world, TickContext? ctx, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, HomeHaul haul, int seq, bool commit) =>
        WithParcel(building, seq, p =>
        {
            long now = world.CurrentDate.TotalMilliseconds;
            if (p.Where != "shop") return HomeCommands.Refuse("só se entrega o que está na loja");
            if (HomeCommands.Shop(world, home) is not { } shop) return HomeCommands.Refuse("não há loja de móveis no bairro");
            if (Courier(world, shop) is not { } courier) return HomeCommands.Refuse("a loja não tem quem entregue");
            if (FrontOf(world, building) is not { } here || Path(world, courier, ShopFront(world, shop).Front, here.Front) is not { } path) return HomeCommands.Refuse("não há caminho da loja até a sua porta");
            var d = haul.Delivery;
            long fee = d.Fee + d.PerKg * (long)Math.Ceiling(Heft(haul, p).Kg);
            if (npc.Wallet.Amount < fee) return HomeCommands.Refuse($"falta dinheiro: a entrega custa {fee}, você tem {npc.Wallet.Amount}");
            long travel = TravelMs(world, haul, path.Count);
            long depart = NextDeparture(world, haul, Math.Max(Math.Max(now + d.PrepMin * MinuteMs, p.ReadyAtMs), CourierFree(world, courier)));
            long due = depart + travel;
            string when = $"{courier.Name} entrega {Name(catalog, p.Id)} por {fee}: chega {LivingWorld.Simulation.Careers.CareerWork.When(world, due)}";
            if (!commit) return HomeCommands.Ok(fee, 0, when);
            npc.TryDebitWallet(new Money(fee));
            shop.CreditTreasury(new Money(fee));
            Update(world, building, p, p with { Where = "courier", Courier = courier.Id.Value, DepartMs = depart, DueMs = due, Fee = fee });
            ctx?.ScheduleEventAtMs(due, BodyTaskSystem.SystemName, $"{building.Id.Value}|{p.Seq}|d");
            return HomeCommands.Ok(fee, 0, when);
        });

    /// <summary>O evento de chegada do entregador (<see cref="BodyTaskSystem"/>): o móvel vai para o depósito da casa. O aviso é o próprio móvel entregue (<see cref="Parcel.Courier"/> segue gravado).</summary>
    public static void Arrive(WorldState world, long buildingId, int seq)
    {
        if (world.FindBuilding(new BuildingId(buildingId)) is not { } building || Find(building, seq) is not { Where: "courier" } p) return;
        Update(world, building, p, p with { Where = "depot" });
    }

    /// <summary>Quem entrega: o primeiro empregado vivo da loja que ninguém conduz (id menor).</summary>
    public static Npc? Courier(WorldState world, Workplace shop)
    {
        foreach (var id in shop.Employees.OrderBy(e => e.Value))
            if (world.FindNpc(id) is { IsAlive: true, ControllerId: null } n) return n;
        return null;
    }

    private static long CourierFree(WorldState world, Npc courier)
    {
        long free = 0;
        foreach (var b in world.Buildings)
            foreach (var o in b.Edit?.Parcels ?? [])
                if (o.Where == "courier" && o.Courier == courier.Id.Value) free = Math.Max(free, o.DueMs + (o.DueMs - o.DepartMs)); // volta andando
        return free;
    }

    public static long TravelMs(WorldState world, HomeHaul haul, int cells) => Math.Max(1000, (long)(cells * world.Players.CellM / haul.Delivery.Mps * 1000));

    /// <summary>A rota do entregador de uma soleira a outra pela regra do chão do passo do jogador (a mesma busca do <c>Approach</c>); nula sem caminho.</summary>
    public static List<CellCoord>? Path(WorldState world, Npc walker, CellCoord from, CellCoord to)
    {
        if (from == to) return [];
        var cache = new Dictionary<CellCoord, PlayerGround.Spot?>();
        PlayerGround.Spot? At(CellCoord c)
        {
            if (!cache.TryGetValue(c, out var spot)) cache[c] = spot = PlayerGround.At(world, c, 0);
            return spot;
        }

        return PlayerWalk.Route(world, walker, 0, from, to, At);
    }

    /// <summary>O primeiro instante a partir de <paramref name="at"/> dentro do horário de entrega (hora do dia, em dado).</summary>
    public static long NextDeparture(WorldState world, HomeHaul haul, long at)
    {
        long day = (long)world.Calendar.HoursPerDay * WorldMillis.PerHour;
        long today = Math.DivRem(at, day, out long inDay) * day;
        long hour = inDay / WorldMillis.PerHour;
        if (hour < haul.Delivery.OpenHour) return today + haul.Delivery.OpenHour * (long)WorldMillis.PerHour;
        if (hour >= haul.Delivery.CloseHour) return today + day + haul.Delivery.OpenHour * (long)WorldMillis.PerHour;
        return at;
    }

    // ---------------- depósito ----------------

    private static Result<HomeOutcome> Place(WorldState world, Building building, ObjectCatalog catalog, HomeCatalog home, int seq, int floor, int x, int y, int facing, bool commit) =>
        WithParcel(building, seq, p =>
        {
            if (p.Where != "depot") return HomeCommands.Refuse("esse móvel ainda não está no depósito da casa");
            var item = home.Item(p.Id) ?? new HomeItem(p.Id, "", 0, 0, -1);
            if (HomeCommands.Placement(building, catalog, home, HomeFurniture.Of(building, catalog), -1, item, floor, new CellCoord(x, y), facing) is { } why) return HomeCommands.Refuse(why);
            if (!commit) return HomeCommands.Ok(0, 0, $"Pôr {Name(catalog, p.Id)} aqui");
            var edit = building.Edit!;
            edit.Parcels.Remove(p);
            edit.Added.Add(new EditedPiece(p.Id, p.Variation, floor, x, y, facing, p.Paid));
            Touched(world, building);
            return HomeCommands.Ok(0, 0, $"{Name(catalog, p.Id)} posicionado");
        });

    private static Result<HomeOutcome> Stash(WorldState world, Building building, ObjectCatalog catalog, HomeCatalog home, int piece, bool commit)
    {
        var pieces = HomeFurniture.Of(building, catalog);
        if (piece < 0 || piece >= pieces.Count || pieces[piece].Spot is null) return HomeCommands.Refuse("essa peça não existe mais");
        if (HomeCommands.Busy(world, building, piece)) return HomeCommands.Refuse("alguém está usando essa peça");
        if (home.Item(pieces[piece].Id) is null) return HomeCommands.Refuse("essa peça não vai para o depósito");
        if (!commit) return HomeCommands.Ok(0, 0, $"Guardar {Name(catalog, pieces[piece].Id)} no depósito");
        var edit = building.Edit ??= new HomeEdit();
        int baseCount = HomeFurniture.BaseCount(building, catalog);
        if (piece >= baseCount) edit.Added[piece - baseCount] = edit.Added[piece - baseCount] with { Gone = true };
        else if (!edit.Removed.Contains(piece)) edit.Removed.Add(piece);
        world.SetLight(building.Id.Value, piece, false);
        edit.Parcels.Add(new Parcel(edit.NextParcel++, pieces[piece].Id, pieces[piece].Variation, 0, "depot"));
        Touched(world, building);
        return HomeCommands.Ok(0, 0, $"{Name(catalog, pieces[piece].Id)} guardado no depósito");
    }

    /// <summary><c>cancel</c>: desfaz a compra do que ainda está pronto na loja (devolve tudo e o móvel volta ao estoque); <c>sellp</c>: a loja recompra do depósito pelo preço de revenda.</summary>
    private static Result<HomeOutcome> Return(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, int seq, bool resale, bool commit) =>
        WithParcel(building, seq, p =>
        {
            if (HomeCommands.Shop(world, home) is not { } shop) return HomeCommands.Refuse("não há loja de móveis no bairro");
            if (home.Item(p.Id) is not { } item) return HomeCommands.Refuse("a loja não recompra essa peça");
            if (resale ? p.Where != "depot" : p.Where != "shop" || p.ReadyAtMs > world.CurrentDate.TotalMilliseconds) return HomeCommands.Refuse(resale ? "só se vende do depósito da casa" : "só se cancela o que está pronto na loja");
            long refund = resale ? item.Price * home.Rules.ResalePercent / 100 : p.Paid;
            if (shop.Treasury.Amount < refund) return HomeCommands.Refuse("a loja não tem caixa para devolver agora");
            if (!commit) return HomeCommands.Ok(0, refund, resale ? $"Vender {Name(catalog, p.Id)} do depósito" : $"Cancelar a compra de {Name(catalog, p.Id)}");
            shop.TryDebitTreasury(new Money(refund));
            npc.CreditWallet(new Money(refund));
            shop.Deposit(new ResourceType(item.Resource), 1, world.EconomyRules);
            world.RecordTrade(shop.Id, new ResourceType(item.Resource), world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay, 1, refund);
            building.Edit!.Parcels.Remove(p);
            Touched(world, building);
            return HomeCommands.Ok(0, refund, resale ? $"Vendeu {Name(catalog, p.Id)} por {refund}" : $"Compra de {Name(catalog, p.Id)} cancelada");
        });
}
