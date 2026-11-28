using System.Globalization;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Objects;

/// <summary>O que um comando de casa custa ou devolve, e uma frase para o aviso.</summary>
public sealed record HomeOutcome(long Price, long Refund, string Note);

/// <summary>#264: modo comprar e construir. O motor valida e aplica; o cliente só pede (<c>interact {kind:"home", id: prédio, verb}</c>) e pergunta antes (<see cref="Check"/>) para o
/// fantasma verde ou vermelho. Verbos: <c>buy:objeto</c> (#265: paga e reserva na loja; trazer e posicionar são de <see cref="HomeFreight"/>: <c>fetch|fetchhelp|load|unload|drop|deliver|drive|place|stash|cancel|sellp</c>), <c>move:peça:andar:x:y:giro</c>, <c>sell:peça</c>, <c>discard:peça</c>, <c>wall|open|door|shut:andar:ax:ay:bx:by</c>
/// (levantar, derrubar, abrir porta, fechar porta), <c>extend:x:y</c> e <c>paint:floor|wall:andar:x:y:tinta</c> (o cômodo inteiro). A moeda só muda de mão (carteira → caixa da loja de móveis,
/// que também recebe a obra; a revenda sai do caixa dela) e o móvel sai do estoque da loja ou é feito por encomenda com material do estoque dela e prazo; nada nasce por regra e nada aparece em casa sem ser trazido.
/// A obra nunca prende ninguém: depois de cada mudança toda célula livre segue ligada à porta.</summary>
public static class HomeCommands
{
    private const long HourMs = 3_600_000;

    /// <summary>Executa o comando de casa: 409 com o motivo se não vale (o mundo não muda), senão aplica e devolve o que custou.</summary>
    public static Result<InteractionResult> Apply(WorldState world, TickContext ctx, Npc npc, long buildingId, string verb)
    {
        var done = Run(world, ctx, npc, buildingId, verb, commit: true);
        if (!done.IsSuccess) return Result<InteractionResult>.Fail(done.Error!);
        var o = done.Value!;
        return Result<InteractionResult>.Ok(new InteractionResult("home", buildingId, verb, 1, o.Price > 0 ? o.Price : o.Refund, "done", o.Note));
    }

    /// <summary>A mesma validação sem aplicar nada: para o fantasma verde ou vermelho e o motivo do vermelho.</summary>
    public static Result<HomeOutcome> Check(WorldState world, Npc npc, long buildingId, string verb) => Run(world, null, npc, buildingId, verb, commit: false);

    /// <summary>Quem mora na casa (dono do domicílio) e a casa com o catálogo de compra; nulo no que falha, com o motivo.</summary>
    public static (Building? Building, string? Error) HomeOf(WorldState world, Npc npc, long buildingId)
    {
        if (world.ObjectCatalog is not { Home: not null }) return (null, "este mundo não tem o modo comprar");
        if (world.FindBuilding(new BuildingId(buildingId)) is not { Position: not null } building) return (null, "casa não encontrada");
        var household = npc.Household is { } h ? world.FindHousehold(h) : null;
        if (household?.Dwelling != building.Id) return (null, "esta casa não é sua");
        return (building, null);
    }

    private static Result<HomeOutcome> Run(WorldState world, TickContext? ctx, Npc npc, long buildingId, string verb, bool commit)
    {
        var (building, error) = HomeOf(world, npc, buildingId);
        if (building is null) return Refuse(error!);
        var catalog = world.ObjectCatalog!;
        var home = catalog.Home!;
        var parts = verb.Split(':');
        try
        {
            return parts[0] switch
            {
                "buy" when parts.Length == 2 => Buy(world, npc, building, catalog, home, parts[1], commit),
                "move" when parts.Length == 6 => Move(world, building, catalog, Int(parts[1]), Int(parts[2]), Int(parts[3]), Int(parts[4]), Int(parts[5]), commit),
                "sell" when parts.Length == 2 => Retire(world, npc, building, catalog, home, Int(parts[1]), sell: true, commit),
                "discard" when parts.Length == 2 => Retire(world, npc, building, catalog, home, Int(parts[1]), sell: false, commit),
                "wall" or "open" or "door" or "shut" when parts.Length == 6 => Edge(world, npc, building, catalog, home, parts[0], Int(parts[1]), Int(parts[2]), Int(parts[3]), Int(parts[4]), Int(parts[5]), commit),
                "extend" when parts.Length == 3 => Extend(world, npc, building, catalog, home, Int(parts[1]), Int(parts[2]), commit),
                "paint" when parts.Length == 6 => Paint(world, npc, building, catalog, home, parts[1], Int(parts[2]), Int(parts[3]), Int(parts[4]), parts[5], commit),
                _ => HomeFreight.Run(world, ctx, npc, building, catalog, home, parts, commit) ?? Refuse($"comando de casa desconhecido: {verb}"),
            };
        }
        catch (FormatException)
        {
            return Refuse("números do comando inválidos");
        }
    }

    private static int Int(string text) => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);

    internal static Result<HomeOutcome> Refuse(string reason) => Result<HomeOutcome>.Fail($"{PlayerCommands.Conflict} {reason}");

    internal static Result<HomeOutcome> Ok(long price, long refund, string note) => Result<HomeOutcome>.Ok(new HomeOutcome(price, refund, note));

    /// <summary>A loja de móveis do bairro: o local do tipo que o cenário põe como empreiteiro (<c>Home.build.contractor</c>), aberto; vende, recompra e recebe a obra.</summary>
    public static Workplace? Shop(WorldState world, HomeCatalog home) =>
        world.Workplaces.Where(w => w.LocationType.Id == home.Build.Contractor && !w.Closed).OrderBy(w => w.Id.Value).FirstOrDefault();

    // ---------------- comprar ----------------

    /// <summary>#265: pagar não põe o móvel em casa. A compra tira 1 do estoque da loja (ou faz sob encomenda com o material dela, pelo preço com acréscimo e no prazo) e leva a moeda da
    /// carteira ao caixa; o móvel fica reservado na loja em nome da casa (<see cref="Parcel"/>) até alguém o trazer (<see cref="HomeFreight"/>).</summary>
    private static Result<HomeOutcome> Buy(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, string objectId, bool commit)
    {
        if (home.Item(objectId) is not { } item || catalog.Find(objectId) is not { } descriptor) return Refuse("esse móvel não está à venda");
        if (Shop(world, home) is not { } shop) return Refuse("não há loja de móveis no bairro");
        var edit = building.Edit;
        if ((edit?.Added.Count ?? 0) + (edit?.Parcels.Count ?? 0) >= home.Rules.MaxPieces) return Refuse("a casa não comporta mais móveis comprados");

        var resource = new ResourceType(item.Resource);
        long price = item.Price;
        bool inStock = shop.Stock.GetValueOrDefault(resource) >= 1;
        long readyAt = 0;
        if (!inStock)
        {
            if (item.Craft is not { } craft) return Refuse($"{descriptor.Name ?? descriptor.Id} está esgotado na loja");
            foreach (var (key, need) in craft.Materials)
                if (shop.Stock.GetValueOrDefault(new ResourceType(int.Parse(key, CultureInfo.InvariantCulture))) < need)
                    return Refuse($"{descriptor.Name ?? descriptor.Id} está esgotado e a loja está sem material para fazer");
            price = price * (100 + home.Rules.OrderMarkupPercent) / 100;
            readyAt = world.CurrentDate.TotalMilliseconds + craft.Hours * HourMs;
        }

        if (npc.Wallet.Amount < price) return Refuse($"falta dinheiro: custa {price}, você tem {npc.Wallet.Amount}");
        string name = descriptor.Name ?? descriptor.Id;
        if (!commit) return Ok(price, 0, inStock ? name : $"{name} sob encomenda");

        npc.TryDebitWallet(new Money(price));
        shop.CreditTreasury(new Money(price));
        if (inStock) shop.Withdraw(resource, 1);
        else
            foreach (var (key, need) in item.Craft!.Materials) shop.Withdraw(new ResourceType(int.Parse(key, CultureInfo.InvariantCulture)), need);
        world.RecordTrade(shop.Id, resource, world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay, 1, price);

        edit = Editable(world, building);
        int variation = ObjectVariation.Index(descriptor.VariationSeed, HomeFurniture.BaseCount(building, catalog) + edit.Added.Count + edit.NextParcel);
        HomeFreight.Reserve(world, building, objectId, variation, price, readyAt);
        Touched(world, building);
        string where = home.Haul is null ? "no depósito da casa" : "na loja, em seu nome";
        return Ok(price, 0, inStock ? $"Comprou {name}: {where}" : $"{name} encomendado: fica pronto em {Math.Max(1, (readyAt - world.CurrentDate.TotalMilliseconds) / 60_000)} min, {where}");
    }

    // ---------------- mover, vender, descartar ----------------

    private static Result<HomeOutcome> Move(WorldState world, Building building, ObjectCatalog catalog, int piece, int floor, int x, int y, int facing, bool commit)
    {
        var home = catalog.Home!;
        var pieces = HomeFurniture.Of(building, catalog);
        if (piece < 0 || piece >= pieces.Count || pieces[piece].Spot is null) return Refuse("essa peça não existe mais");
        if (Busy(world, building, piece)) return Refuse("alguém está usando essa peça");
        var item = home.Item(pieces[piece].Id) ?? new HomeItem(pieces[piece].Id, "", 0, 0, -1);
        if (Placement(building, catalog, home, pieces, piece, item, floor, new CellCoord(x, y), facing) is { } why) return Refuse(why);
        if (!commit) return Ok(0, 0, "Mover");

        var edit = Editable(world, building);
        int baseCount = HomeFurniture.BaseCount(building, catalog);
        if (piece >= baseCount) edit.Added[piece - baseCount] = edit.Added[piece - baseCount] with { Floor = floor, X = x, Y = y, Facing = facing };
        else
        {
            edit.Moved.RemoveAll(m => m.Piece == piece);
            edit.Moved.Add(new PieceMove(piece, floor, x, y, facing));
        }

        Touched(world, building);
        return Ok(0, 0, "Móvel movido");
    }

    private static Result<HomeOutcome> Retire(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, int piece, bool sell, bool commit)
    {
        var pieces = HomeFurniture.Of(building, catalog);
        if (piece < 0 || piece >= pieces.Count || pieces[piece].Spot is null) return Refuse("essa peça não existe mais");
        if (Busy(world, building, piece)) return Refuse("alguém está usando essa peça");
        var name = catalog.Find(pieces[piece].Id)?.Name ?? pieces[piece].Id;
        long refund = 0;
        Workplace? shop = null;
        if (sell)
        {
            if (home.Item(pieces[piece].Id) is not { } item) return Refuse("a loja não revende essa peça: descarte");
            if ((shop = Shop(world, home)) is null) return Refuse("não há loja de móveis no bairro");
            refund = item.Price * home.Rules.ResalePercent / 100;
            if (shop.Treasury.Amount < refund) return Refuse("a loja não tem caixa para comprar essa peça agora");
        }

        if (!commit) return Ok(0, refund, sell ? $"Vender {name}" : $"Descartar {name}");
        if (sell)
        {
            shop!.TryDebitTreasury(new Money(refund));
            npc.CreditWallet(new Money(refund));
            shop.Deposit(new ResourceType(home.Item(pieces[piece].Id)!.Resource), 1, world.EconomyRules);
            world.RecordTrade(shop.Id, new ResourceType(home.Item(pieces[piece].Id)!.Resource), world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay, 1, refund);
        }

        var edit = Editable(world, building);
        int baseCount = HomeFurniture.BaseCount(building, catalog);
        if (piece >= baseCount) edit.Added[piece - baseCount] = edit.Added[piece - baseCount] with { Gone = true };
        else if (!edit.Removed.Contains(piece)) edit.Removed.Add(piece);
        world.SetLight(building.Id.Value, piece, false);
        Touched(world, building);
        return Ok(0, refund, sell ? $"Vendeu {name} por {refund}" : $"Descartou {name}");
    }

    /// <summary>Alguém está usando (ou a caminho de usar) a peça.</summary>
    internal static bool Busy(WorldState world, Building building, int piece)
    {
        foreach (var other in world.AliveNpcIndex.Alive)
            if (other.BodyTask is { Action: ObjectUsage.Action } task && task.Building == building.Id.Value && task.Piece == piece) return true;
        return false;
    }

    // ---------------- lugar ----------------

    /// <summary>Por que a peça não pode ficar ali (ou nulo): piso livre do cômodo, sem bloquear porta, encostada na parede quando o móvel pede, no cômodo certo e com caminho até a porta.</summary>
    public static string? Placement(Building building, ObjectCatalog catalog, HomeCatalog home, IReadOnlyList<HomePiece> pieces, int ignore, HomeItem item, int floor, CellCoord cell, int facing)
    {
        var layout = BuildingLayoutCache.Of(building).Layout;
        var rooms = BuildingRooms.Of(building, catalog);
        if (floor < 0 || floor >= layout.FloorLayouts.Count) return "esse andar não existe";
        if (facing is not (0 or 90 or 180 or 270)) return "giro inválido (0, 90, 180 ou 270)";
        var floorLayout = layout.FloorLayouts[floor];
        var material = floorLayout.Cells.FirstOrDefault(c => c.Cell == cell);
        if (material is null) return "aqui não é piso da casa";
        if (material.Material != BuildingMaterial.Floor) return "porta e escada ficam livres";
        var blocked = new HashSet<CellCoord>();
        for (int i = 0; i < pieces.Count; i++)
            if (i != ignore && pieces[i].Spot is { } spot && spot.Floor == floor) blocked.Add(spot.Cell);
        if (blocked.Contains(cell)) return "já tem um móvel aqui";

        var materials = floorLayout.Cells.ToDictionary(c => c.Cell, c => c.Material);
        CellCoord? entry = FurniturePlanner.StartOf(materials, floor) is { } start ? FurniturePlanner.EntryOf(materials, start, floor) : null;
        if (HomeEditor.Doorways(rooms.Floors[floor], entry).Contains(cell)) return "bloquearia a passagem da porta";
        if (item.Rooms is { Count: > 0 } allowed && rooms.RoomAt(floor, cell) is { } room && room.Kind is { } kind && !allowed.Contains(kind))
            return $"esse móvel só vai em: {string.Join(", ", allowed)}";
        if (item.Wall)
        {
            var back = facing switch { 0 => new CellCoord(0, -1), 90 => new CellCoord(-1, 0), 270 => new CellCoord(1, 0), _ => new CellCoord(0, 1) };
            if (!rooms.Blocked[floor].Contains(WallEdge.Between(cell, new CellCoord(cell.X + back.X, cell.Y + back.Y)))) return "precisa ficar encostado na parede (gire para o fundo ficar na parede)";
        }

        blocked.Add(cell);
        if (!HomeEditor.AllConnected(floorLayout, rooms.Floors[floor], blocked)) return "fecharia o caminho até a porta";
        return null;
    }

    // ---------------- obra ----------------

    private static Result<HomeOutcome> Edge(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, string op,
        int floor, int ax, int ay, int bx, int by, bool commit)
    {
        var layout = BuildingLayoutCache.Of(building).Layout;
        var rooms = BuildingRooms.Of(building, catalog);
        if (floor < 0 || floor >= layout.FloorLayouts.Count) return Refuse("esse andar não existe");
        if (Math.Abs(ax - bx) + Math.Abs(ay - by) != 1) return Refuse("a parede fica entre duas células vizinhas");
        var edge = WallEdge.Between(new CellCoord(ax, ay), new CellCoord(bx, by));
        string state = HomeEditor.EdgeState(layout.FloorLayouts[floor], rooms.Floors[floor], edge);
        if (state is "none" or "outer") return Refuse(state == "none" ? "fora da planta" : "a parede de fora não muda");
        (string need, string result, long price, string wrong) = op switch
        {
            "wall" => ("open", "wall", home.Build.Wall, state == "door" ? "tem uma porta aí: feche-a antes" : "já é parede"),
            "open" => ("wall", "open", home.Build.Demolish, state == "door" ? "é uma porta" : "não há parede aí"),
            "door" => ("wall", "door", home.Build.Door, state == "door" ? "já é porta" : "só se abre porta em parede"),
            _ => ("door", "wall", home.Build.Wall, "não há porta aí"),
        };
        if (state != need) return Refuse(wrong);

        var simulated = Clone(building.Edit);
        simulated.Edges.RemoveAll(e => e.Floor == floor && e.Edge == edge);
        simulated.Edges.Add(new EdgeEdit(floor, edge.A.X, edge.A.Y, edge.B.X, edge.B.Y, result));
        var wall = BuildingFootprintGenerator.WallMaterialOf(building.BuildingTypeId);
        var after = HomeEditor.ApplyRooms(BuildingRooms.BaseOf(building, catalog).Floors[floor], layout.FloorLayouts[floor], simulated, floor, wall);
        var pieces = HomeFurniture.Of(building, catalog);
        if (!HomeEditor.AllConnected(layout.FloorLayouts[floor], after, Occupied(building, pieces, floor))) return Refuse("deixaria parte da casa sem caminho até a porta");
        if (Pay(world, npc, home, price, commit, "a obra") is { } refusal) return Refuse(refusal);
        if (!commit) return Ok(price, 0, op switch { "wall" => "Levantar parede", "open" => "Derrubar parede", "door" => "Abrir porta", _ => "Fechar porta" });

        var edit = Editable(world, building);
        edit.Edges.RemoveAll(e => e.Floor == floor && e.Edge == edge);
        edit.Edges.Add(new EdgeEdit(floor, edge.A.X, edge.A.Y, edge.B.X, edge.B.Y, result));
        Touched(world, building);
        return Ok(price, 0, op switch { "wall" => "Parede levantada", "open" => "Parede derrubada", "door" => "Porta aberta", _ => "Porta fechada" });
    }

    private static Result<HomeOutcome> Extend(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, int x, int y, bool commit)
    {
        var layout = BuildingLayoutCache.Of(building).Layout;
        var cells = layout.FloorLayouts[0].Cells.Select(c => c.Cell).ToHashSet();
        var cell = new CellCoord(x, y);
        if (cells.Contains(cell)) return Refuse("aí já é casa");
        if ((building.Edit?.Cells.Count ?? 0) >= home.Build.MaxExtraCells) return Refuse("o lote não comporta mais ampliação");
        if (!HomeEditor.Around.Any(d => cells.Contains(new CellCoord(x + d.X, y + d.Y)))) return Refuse("a ampliação começa encostada na casa");
        var doorOutside = (layout.FloorLayouts[0].Doors ?? []).SelectMany(d => new[] { d.A, d.B }).FirstOrDefault(c => !cells.Contains(c));
        if (layout.FloorLayouts[0].Doors is { Count: > 0 } && cell == doorOutside) return Refuse("a frente da porta fica livre");
        var origin = building.Position!.Value;
        var shape = new[] { cells.Select(c => c.X).Concat([x]), cells.Select(c => c.Y).Concat([y]) };
        if (shape[0].Max() - shape[0].Min() + 1 > BuildingShape.MaxSide || shape[1].Max() - shape[1].Min() + 1 > BuildingShape.MaxSide) return Refuse("a casa não pode passar do tamanho máximo");
        var worldCell = new CellCoord(origin.X + x, origin.Y + y);
        if (!PlayerGround.Walkable(world, worldCell) || PlayerGround.At(world, worldCell) is not null) return Refuse("o lote não vai até aí (rua, outro prédio ou fora do mapa)");
        if (world.Ground is { } ground && ground.IndexOf(worldCell) is var at and >= 0 && ground.PavingAt(at) != GroundSurface.None) return Refuse("a rua está aí");
        if (Pay(world, npc, home, home.Build.Cell, commit, "a ampliação") is { } refusal) return Refuse(refusal);
        if (!commit) return Ok(home.Build.Cell, 0, "Ampliar a casa");

        Editable(world, building).Cells.Add(cell);
        Touched(world, building);
        Building.TouchLayoutVersion();
        return Ok(home.Build.Cell, 0, "Casa ampliada");
    }

    private static Result<HomeOutcome> Paint(WorldState world, Npc npc, Building building, ObjectCatalog catalog, HomeCatalog home, string kind, int floor, int x, int y,
        string paintId, bool commit)
    {
        if (home.Paint(paintId) is not { } paint) return Refuse("essa tinta não existe");
        if (paint.Kind != kind) return Refuse(kind == "floor" ? "essa tinta é de parede" : "essa tinta é de piso");
        var layout = BuildingLayoutCache.Of(building).Layout;
        var rooms = BuildingRooms.Of(building, catalog);
        if (floor < 0 || floor >= layout.FloorLayouts.Count) return Refuse("esse andar não existe");
        if (rooms.RoomAt(floor, new CellCoord(x, y)) is not { } room) return Refuse("aí não há cômodo");
        var inRoom = room.Cells.ToHashSet();
        var marks = new List<PaintMark>();
        if (kind == "floor")
            foreach (var cell in room.Cells) marks.Add(new PaintMark("floor", floor, cell.X, cell.Y, 0, 0, paintId));
        else
        {
            var closed = rooms.Blocked[floor];
            var seen = new HashSet<WallEdge>();
            foreach (var cell in room.Cells)
                foreach (var d in HomeEditor.Around)
                {
                    var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                    var edge = WallEdge.Between(cell, next);
                    if (!inRoom.Contains(next) && closed.Contains(edge) && seen.Add(edge)) marks.Add(new PaintMark("wall", floor, edge.A.X, edge.A.Y, edge.B.X, edge.B.Y, paintId));
                }
        }

        if (marks.Count == 0) return Refuse("esse cômodo não tem o que pintar");
        bool same = marks.All(m => building.Edit?.Paints.Any(p => Same(p, m) && p.Paint == paintId) == true);
        if (same) return Refuse("já está pintado assim");
        long price = paint.Price * marks.Count;
        if (Pay(world, npc, home, price, commit, "a pintura") is { } refusal) return Refuse(refusal);
        if (!commit) return Ok(price, 0, $"Pintar de {paint.Name}");

        var edit = Editable(world, building);
        foreach (var mark in marks)
        {
            edit.Paints.RemoveAll(p => Same(p, mark));
            edit.Paints.Add(mark);
        }

        Touched(world, building);
        return Ok(price, 0, $"Pintou de {paint.Name}");
    }

    private static bool Same(PaintMark a, PaintMark b) => a.Kind == b.Kind && a.Floor == b.Floor && a.X == b.X && a.Y == b.Y && a.BX == b.BX && a.BY == b.BY;

    /// <summary>Quanto se paga à obra: da carteira para o caixa do empreiteiro (a loja de móveis). Nulo se pode; o motivo se não.</summary>
    private static string? Pay(WorldState world, Npc npc, HomeCatalog home, long price, bool commit, string what)
    {
        if (Shop(world, home) is not { } shop) return "não há loja de móveis no bairro para fazer a obra";
        if (npc.Wallet.Amount < price) return $"falta dinheiro: {what} custa {price}, você tem {npc.Wallet.Amount}";
        if (!commit || price <= 0) return null;
        npc.TryDebitWallet(new Money(price));
        shop.CreditTreasury(new Money(price));
        return null;
    }

    private static HashSet<CellCoord> Occupied(Building building, IReadOnlyList<HomePiece> pieces, int floor)
    {
        var cells = new HashSet<CellCoord>();
        foreach (var piece in pieces)
            if (piece.Spot is { } spot && spot.Floor == floor) cells.Add(spot.Cell);
        return cells;
    }

    private static HomeEdit Clone(HomeEdit? edit) => edit is null ? new HomeEdit() : new HomeEdit
    {
        Added = [.. edit.Added],
        Moved = [.. edit.Moved],
        Removed = [.. edit.Removed],
        Edges = [.. edit.Edges],
        Cells = [.. edit.Cells],
        Paints = [.. edit.Paints],
        Parcels = [.. edit.Parcels],
        NextParcel = edit.NextParcel,
        Car = edit.Car,
    };

    private static HomeEdit Editable(WorldState world, Building building)
    {
        building.Edit ??= new HomeEdit();
        return building.Edit;
    }

    private static void Touched(WorldState world, Building building)
    {
        building.Edit!.Touch();
        world.TouchBuildings();
    }
}
