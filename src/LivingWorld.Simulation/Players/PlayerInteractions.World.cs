using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

// AD-197j: o que mais o corpo alcança, pelos sistemas de qualquer NPC: a escada (outro andar), a fonte (beber e encher, WaterFetch),
// a lavoura (trabalhar nela, furtar o que ela guarda), o espólio no chão (recolher, Leavings) e o furto de despensa, banca e lavoura
// alheias (Theft: sucesso pela guarda, testemunha, justiça, mágoa). Furtar é ordem contra os valores: a vítima é ferida (0,3).
public static partial class PlayerInteractions
{
    /// <summary>O quanto um furto fere a vítima, na conta do peso da ordem (a perda de bens, menor que ferir o corpo).</summary>
    public const double TheftHarm = 0.3;

    private static void WorldOffers(WorldState world, Npc npc, CellCoord at, int floor, BuildingId? inside, Household? household,
        List<InteractionOffer> offers)
    {
        if (PlayerGround.At(world, at, floor) is { Material: BuildingMaterial.Stair } stair)
        {
            int floors = Cities.BuildingLayoutCache.Of(stair.Building).Layout.Floors;
            var verbs = new List<string>();
            if (floor + 1 < floors) verbs.Add("up");
            if (floor > 0) verbs.Add("down");
            if (verbs.Count > 0) offers.Add(Offer("stairs", stair.Building.Id.Value, "", verbs, at));
        }
        ObjectOffers(world, npc, at, inside, household, offers);
        if (floor > 0) return; // fonte, lavoura, espólio e casas alheias são do térreo

        bool steal = CanSteal(world, npc);
        if (steal)
            foreach (var other in world.Households.OrderBy(h => h.Id.Value))
            {
                if (other.Id == npc.Household || other.City != npc.City || other.IsEmpty || !Near(at, inside, other.Location, other.Dwelling)) continue;
                var stock = other.Stock.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.Id).Select(kv => new InteractionResource(kv.Key.Id, kv.Value, 0)).ToList();
                if (stock.Count > 0) offers.Add(Offer("stock", other.Id.Value, "", ["steal"], other.Location, stock));
            }

        foreach (var farm in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (farm.LocationType.Id != Economy.Production.CropSystem.DefaultFarmLocationTypeId
                || !Near(at, inside, farm.Location, farm.Building)) continue;
            var stock = farm.Stock.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.Id).Select(kv => new InteractionResource(kv.Key.Id, kv.Value, 0)).ToList();
            var verbs = new List<string>();
            if (npc.Employer == farm.Id) verbs.Add("work");
            if (steal && Outsider(npc, farm) && stock.Count > 0 && farm.Prices.Count == 0) verbs.Add("steal"); // com preço, a banca oferece
            if (verbs.Count > 0) offers.Add(Offer("field", farm.Id.Value, "", verbs, farm.Location, stock));
        }

        // AD-197k: oficina sem preço (ferraria, olaria): trabalhar nela, ou furtar o que ela guarda (o furto de sempre)
        foreach (var shop in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (shop.Prices.Count > 0 || shop.LocationType.Id == Economy.Production.CropSystem.DefaultFarmLocationTypeId
                || !Near(at, inside, shop.Location, shop.Building)) continue;
            var stock = shop.Stock.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.Id).Select(kv => new InteractionResource(kv.Key.Id, kv.Value, 0)).ToList();
            var verbs = new List<string>();
            if (npc.Employer == shop.Id) verbs.Add("work");
            if (steal && Outsider(npc, shop) && stock.Count > 0) verbs.Add("steal");
            if (verbs.Count > 0) offers.Add(Offer("workshop", shop.Id.Value, "", verbs, shop.Location, stock));
        }

        if (WaterFetch.On(world) && inside is null && WaterWithinReach(world, npc, at) is { } spring)
            offers.Add(Offer("water", WaterId(spring), "", household is null ? ["drink"] : ["drink", "fill"], spring, reachCells: ReachCells));
        if (inside is null) GatherOffers(world, npc, at, offers);

        // AD-197n: recipientes e veículos inventados pela casa (os designs da invenção que ela fez): levar um é o que o NPC faz ao sair
        // com ele (a carga, a água e a compra passam a caber nele; o veículo enche o barril na fonte); devolver, ao chegar
        if (world.InventionOn && world.Invention is { } ledger && household is not null && Near(at, inside, household.Location, household.Dwelling))
            foreach (var (item, function, holder) in ledger.UsableOf(household.Id.Value))
            {
                var verbs = holder == npc.Id.Value ? new List<string> { "return" } : holder < 0 ? ["take"] : [];
                if (verbs.Count > 0)
                    offers.Add(Offer("item", item.Id, function == 2 ? "veículo" : "recipiente", verbs, household.Location));
            }

        if (world.InventionOn && world.InventionRules!.Leavings.Enabled && world.Invention?.Leavings is { Count: > 0 } leavings)
            foreach (var leaving in leavings.OrderBy(l => l.Id))
            {
                var cell = new CellCoord(leaving.X, leaving.Y);
                if (leaving.CarrierId >= 0 || Chebyshev(cell, at) > 1) continue;
                if (Leavings.ClaimKind(world, leaving, npc) is not null) offers.Add(Offer("leaving", leaving.Id, "", ["collect"], cell));
                else if (steal) offers.Add(Offer("leaving", leaving.Id, "", ["steal"], cell));
            }
    }

    /// <summary>AD-197k: a mobília do prédio onde o corpo está (a casa dele ou a oficina em que trabalha), pelo catálogo de objetos
    /// (<see cref="Objects.ObjectUse"/>): cada objeto com ponto de uso para uma ação que o jogador pode pedir e que o corpo pode usar.
    /// Usar é pedir essa ação ali; o motor resolve com o efeito do objeto, como para qualquer NPC. Id = prédio × 1000 + posição na mobília.</summary>
    private static void ObjectOffers(WorldState world, Npc npc, CellCoord at, BuildingId? inside, Household? household, List<InteractionOffer> offers)
    {
        if (world.ObjectCatalog is not { } catalog || inside is not { } here || world.FindBuilding(here) is not { } building) return;
        bool mine = household?.Dwelling == here || (npc.Employer is { } e && world.FindWorkplace(e)?.Building == here);
        if (!mine) return;
        var furnishing = catalog.FurnishingFor(building.BuildingTypeId);
        // #252: cada móvel oferecido no lugar onde o motor o pôs (a mira e a roda escolhem pelo que está à frente e mais perto), do mais perto
        // do corpo ao mais longe; sem lugar (a peça não coube), na célula do corpo como antes
        var pieces = building.Position is not null ? Objects.HomeFurniture.Of(building, catalog) : [];
        var found = new List<(InteractionOffer Offer, long Distance, int Index)>();
        for (int i = 0; i < furnishing.Count; i++)
        {
            if (catalog.Find(furnishing[i]) is not { } descriptor) continue;
            if (descriptor.Uses is { Count: > 0 } allUses)
            {
                var catalogUses = allUses.Where(u => !u.Hidden && !NoToggle(world, here, i, u)).ToList(); // #258: ligar o que já está ligado não é oferta; #259: os usos de convite (atividade conjunta) não entram na roda do objeto
                if (catalogUses.Count == 0) continue;
                // #255: os usos vêm do catálogo; cada um com o nome, os minutos e o que mais sobe. Só peças com lugar na planta (o corpo anda até elas).
                if (i >= pieces.Count || pieces[i].Id != furnishing[i] || pieces[i].Spot is not { } placed || building.Position is not { } where) continue;
                var spotCell = new CellCoord(where.X + placed.Cell.X, where.Y + placed.Cell.Y);
                long ddx = spotCell.X - at.X, ddy = spotCell.Y - at.Y;
                var offerUses = catalogUses.Select(u => new OfferUse(u.Id, u.Name, u.DurationMin, u.Effects.Where(e => e.PerHour > 0).OrderByDescending(e => e.PerHour).Select(e => e.Need).FirstOrDefault(),
                    u.Occupies && ObjectUsage.OccupiedBy(world, here.Value, i, npc) is not null)).ToList();
                found.Add((Offer("object", here.Value * 1000 + i, descriptor.Name ?? descriptor.Id, catalogUses.Select(u => "use:" + u.Id).ToList(), spotCell) with { Uses = offerUses },
                    ddx * ddx + ddy * ddy, i));
                continue;
            }

            var verbs = descriptor.UsePoints.Where(p => p.Action is int a && PlayerCommands.Choosable.Contains((ActionType)a)
                    && (ActionType)a != ActionType.UsePower && Objects.ObjectUse.Allows(descriptor, (ActionType)a, npc))
                .Select(p => ((ActionType)p.Action!.Value).ToString().ToLowerInvariant()).Distinct().ToList();
            if (verbs.Count == 0) continue;
            var cell = at;
            if (i < pieces.Count && pieces[i].Id == furnishing[i] && pieces[i].Spot is { } spot && building.Position is { } origin)
                cell = new CellCoord(origin.X + spot.Cell.X, origin.Y + spot.Cell.Y);
            long dx = cell.X - at.X, dy = cell.Y - at.Y;
            found.Add((Offer("object", here.Value * 1000 + i, descriptor.Name ?? descriptor.Id, verbs, cell), dx * dx + dy * dy, i)); // #252: o nome do cenário, senão o id
        }
        foreach (var entry in found.OrderBy(f => f.Distance).ThenBy(f => f.Index)) offers.Add(entry.Offer);
    }

    /// <summary>#258: o uso que liga ou desliga uma luz que já está assim não muda nada: não se oferece.</summary>
    private static bool NoToggle(WorldState world, BuildingId building, int piece, LivingWorld.Domain.Objects.ObjectUseDef use) =>
        use.Toggle is { } toggle && world.LightIsOn(building.Value, piece) == (toggle == LivingWorld.Domain.Objects.ObjectToggles.LightOn);

    /// <summary>AD-197k: usar o objeto é pedir a ação dele ali (o motor aplica o efeito do objeto na ação, como para qualquer NPC).</summary>
    private static Result<InteractionResult> Use(WorldState world, TickContext ctx, Npc npc, InteractionOffer offer, string verb, string requested)
    {
        if (verb.StartsWith("use:", StringComparison.Ordinal))
        {
            // #255: o uso do catálogo: o corpo anda até a peça e a ação começa lá (ou entra na fila, se o corpo está ocupado)
            string useId = verb[4..];
            long building = offer.Id / 1000;
            int piece = (int)(offer.Id % 1000);
            var begun = requested.StartsWith("queue:", StringComparison.Ordinal) ? ObjectUsage.Enqueue(world, ctx, npc, building, piece, useId)
                : ObjectUsage.Begin(world, ctx, npc, building, piece, useId);
            return begun.IsSuccess ? Done(offer, requested, 0, 0) : Fail(begun.Error!);
        }

        if (!Enum.TryParse<ActionType>(verb, ignoreCase: true, out var action)) return Fail("interact: ação desconhecida");
        var asked = PlayerCommands.Act(world, npc.Id, npc.ControllerId!, action);
        return asked.IsSuccess ? Done(offer, verb, 0, 0) : Fail(asked.Error!);
    }

    /// <summary>AD-197n: levar a peça inventada (sai com ela, como quem arruma a ida) ou devolvê-la à casa.</summary>
    private static Result<InteractionResult> Carry(WorldState world, Npc npc, InteractionOffer offer, string verb)
    {
        world.Invention!.Hold(npc.Id.Value, verb == "take" ? offer.Id : 0);
        return Done(offer, verb, 1, 0);
    }

    /// <summary>#239: acima disto o corpo está numa tarefa longa (cortar na mata), não no passo de um segundo: não anda até acabar.</summary>
    public const long LongTaskMs = 5 * 60_000;

    /// <summary>#237: o alcance da água, em milésimos de célula, do corpo (posição fina) à borda mais perto da célula d'água: uma célula
    /// (1,5 m, o braço e uma inclinada). Antes valia a vizinhança 3×3 da célula do corpo, até ~3 m da água e através da parede.</summary>
    public const long WaterReachThousandths = FinePose.PerCell;

    /// <summary>O mesmo alcance em células (o que a oferta diz ao cliente).</summary>
    public const double ReachCells = WaterReachThousandths / (double)FinePose.PerCell;

    /// <summary>A célula d'água ao alcance do corpo agora (a mais perto; empate: menor linha, depois coluna), ou nula.</summary>
    private static CellCoord? WaterWithinReach(WorldState world, Npc npc, CellCoord at) => CellWithinReach(world, npc, at, c => c.HasWater);

    /// <summary>A célula que cumpre <paramref name="wanted"/> e está ao alcance do corpo (a mais perto, pela borda; empate: menor linha, depois
    /// coluna), ou nula. O corpo solto conta pela posição fina; sem ela, pelo centro da célula.</summary>
    private static CellCoord? CellWithinReach(WorldState world, Npc npc, CellCoord at, Func<MapCell, bool> wanted)
    {
        // #237: a posição fina AGORA (a gravada avançada pelo rumo desde o último comando), não a de uma latência atrás
        var fine = npc.Fine is { } f && f.CellX == at.X && f.CellY == at.Y ? PlayerWalk.Projected(world, npc, f)
            : new FinePose(at.X * FinePose.PerCell + 500, at.Y * FinePose.PerCell + 500, 0, 0, 0);
        at = new CellCoord(fine.CellX, fine.CellY);
        CellCoord? best = null;
        long bestD2 = long.MaxValue;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var cell = new CellCoord(at.X + dx, at.Y + dy);
                if (!world.Map.TryGetCell(cell, out var found) || !wanted(found)) continue;
                long gx = Math.Max(Math.Max(cell.X * FinePose.PerCell - fine.X, fine.X - (cell.X + 1) * FinePose.PerCell), 0);
                long gy = Math.Max(Math.Max(cell.Y * FinePose.PerCell - fine.Y, fine.Y - (cell.Y + 1) * FinePose.PerCell), 0);
                long d2 = gx * gx + gy * gy;
                if (d2 > WaterReachThousandths * WaterReachThousandths || d2 >= bestD2) continue;
                (best, bestD2) = (cell, d2);
            }
        return best;
    }

    /// <summary>#239: a mata ao alcance, com o que o motor diz que a célula tem e o que os NPCs da obra buscam na natureza
    /// (<c>StreetRules.HouseMaterialResourceId</c>, AD-194/195). Fora de prédio e de célula construída, com as mãos e o corpo livres.</summary>
    private static void GatherOffers(WorldState world, Npc npc, CellCoord at, List<InteractionOffer> offers)
    {
        var rules = world.StreetRules;
        if (!world.StreetsOn || rules.HouseGatherHours <= 0 || rules.HouseMaterialUnits <= 0
            || world.CurrentDate.TotalMilliseconds < npc.StepReadyAtMs) return;
        var resource = new ResourceType(rules.HouseMaterialResourceId);
        if (PlayerTasks.Room(world, npc, resource) <= 0) return; // mãos cheias, ou com outra coisa
        if (PlayerNature.NearestWithinReach(world, at, npc.Fine is { } f && f.CellX == at.X && f.CellY == at.Y ? PlayerWalk.Projected(world, npc, f) : npc.Fine) is not { } tree) return;
        var home = npc.Household is { } h ? world.FindHousehold(h)?.Location : null;
        long left = PlayerNature.Left(world, tree.Cell, world.CurrentDate.TotalMilliseconds);
        var (units, _) = PlayerTasks.CutSession(world, npc, resource, home ?? tree.Cell, tree.Cell, null, left);
        var offer = Offer("nature", WaterId(tree.Cell), "Árvore", ["gather"], tree.Cell, [new InteractionResource(resource.Id, units, 0)]);
        offers.Add(offer with { Fx = tree.FineX / (double)FinePose.PerCell, Fy = tree.FineY / (double)FinePose.PerCell });
    }

    /// <summary>#239/#241/#243: cortar lenha na árvore ao alcance. O corte leva o tempo dos NPCs da obra por unidade
    /// (<c>WorkHaul.CutHours</c>, <see cref="PlayerTasks.CutSession"/>: no máximo uma jornada, a carga que o corpo aguenta e a madeira que a
    /// árvore ainda tem); só o corte, a viagem o jogador anda. O corpo fica ocupado esse tempo (<see cref="BodyTask"/>: nem anda nem corta de
    /// novo antes) e a madeira sai unidade a unidade, da árvore (estado do mundo, finita) para as mãos, a cada unidade de tempo
    /// (<see cref="PlayerTasks.Settle"/>); <see cref="PlayerTasks.Interrupt"/> para a qualquer hora e deixa o que já saiu. A carga vai nas
    /// mãos, de onde segue para a despensa como a de qualquer NPC.</summary>
    private static Result<InteractionResult> Gather(WorldState world, TickContext ctx, Npc npc, InteractionOffer offer, long? quantity)
    {
        var resource = new ResourceType(offer.Resources[0].ResourceId);
        if (quantity is <= 0) return Fail("interact: quantidade deve ser > 0");
        long now = world.CurrentDate.TotalMilliseconds;
        if (now < npc.StepReadyAtMs) return Fail($"{PlayerCommands.Conflict} gather: o corpo ainda está ocupado");
        var home = npc.Household is { } h ? world.FindHousehold(h)?.Location : null;
        var wood = new CellCoord(offer.X, offer.Y);
        long left = PlayerNature.Left(world, wood, now);
        if (left <= 0) return Fail($"{PlayerCommands.Conflict} gather: a árvore já não tem madeira");
        if (PlayerTasks.Room(world, npc, resource) <= 0) return Fail($"{PlayerCommands.Conflict} gather: as mãos estão cheias");
        var (units, perUnitMs) = PlayerTasks.CutSession(world, npc, resource, home ?? wood, wood, quantity, left);
        PlayerCommands.TakeTheWheel(world, npc);
        PlayerTasks.BeginGather(world, ctx, npc, resource, wood, units, perUnitMs);
        return Done(offer, "gather", units, 0);
    }

    /// <summary>Uma célula como id de oferta (x na parte alta, y na baixa).</summary>
    internal static long WaterId(CellCoord cell) => ((long)cell.X << 32) | (uint)cell.Y;

    /// <summary>Leva carga pessoal (a água da fonte, o espólio, a comida da viagem) além do que vai nas mãos.</summary>
    private static bool Laden(WorldState world, Npc npc) =>
        world.InventionOn && world.Invention is { } ledger && ledger.CarriedGrams(npc.Id.Value, world.InventionRules!) > 0;

    /// <summary>O furto vale neste mundo e para este corpo (a idade mínima do cenário).</summary>
    private static bool CanSteal(WorldState world, Npc npc) =>
        world.TheftRules.Enabled && npc.AgeYears(world.CurrentDate) >= world.TheftRules.MinAgeYears;

    /// <summary>Nem dono nem empregado da oficina (de quem é da casa não se furta).</summary>
    private static bool Outsider(Npc npc, Workplace shop) => shop.Owner != npc.Id && !shop.Employees.Contains(npc.Id);

    /// <summary>Subir ou descer pela escada: o mesmo lugar no andar de cima ou de baixo, se ninguém está ali.</summary>
    private static Result<InteractionResult> Climb(WorldState world, Npc npc, InteractionOffer offer, int step)
    {
        var building = world.FindBuilding(new BuildingId(offer.Id))!;
        var origin = building.Position!.Value;
        int to = PlayerGround.FloorOf(npc) + step;
        var at = new CellCoord(offer.X, offer.Y);
        if (PlayerGround.PersonAt(world, npc, at, to, building.Id) is { } someone) return Fail($"move: {someone.Name} está ali");
        PlayerCommands.TakeTheWheel(world, npc);
        var local = new CellCoord(at.X - origin.X, at.Y - origin.Y);
        if (to == 0) npc.ExitBuilding();
        else npc.EnterBuilding(building.Id, new FloorLevel(to), local);
        return Done(offer, step > 0 ? "up" : "down", 0, 0);
    }

    /// <summary>Furtar pelo furto de sempre (<see cref="Theft.Commit"/>), depois da vontade da pessoa possuída.</summary>
    private static Result<InteractionResult> Steal(WorldState world, TickContext ctx, Npc npc, InteractionOffer offer, int? resourceId)
    {
        Household? house = null;
        Workplace? shop = null;
        TheftTarget target;
        long now = world.CurrentDate.TotalMilliseconds;
        if (offer.Kind == "leaving")
        {
            var leaving = world.Invention!.Leavings!.First(l => l.Id == offer.Id);
            long owner = Leavings.ClaimantHousehold(world, leaving);
            house = owner >= 0 ? world.FindHousehold(new HouseholdId(owner)) : null;
            int units = 0, resource = world.EconomyRules.FoodResourceId;
            foreach (var l in world.Invention.LeftLoads ?? [])
                if (l.LeavingId == leaving.Id) { units += l.Quantity; resource = l.ResourceId; }
            target = new TheftTarget(TheftTargetKind.Ground, house is null ? null : world.FindNpc(house.Head), house, null, resource, units, 0,
                units * Theft.PriceOf(world, npc.City, resource), new CellCoord(leaving.X, leaving.Y), leaving.Id);
        }
        else
        {
            var pick = offer.Resources.FirstOrDefault(r => (resourceId is null || r.ResourceId == resourceId) && r.Quantity > 0);
            if (pick is null) return Fail("interact: não há esse recurso para levar");
            if (offer.Kind == "stock") house = world.FindHousehold(new HouseholdId(offer.Id));
            else shop = world.FindWorkplace(new WorkplaceId(offer.Id));
            target = Theft.Chosen(world, npc, house, shop, pick.ResourceId, new CellCoord(offer.X, offer.Y));
        }
        var victim = target.VictimId >= 0 ? world.FindNpc(new NpcId(target.VictimId)) : null;
        var harm = victim is null ? new OrderHarm([]) : new OrderHarm([(victim, TheftHarm)]);
        if (PlayerOrders.Refusal(world, ctx, npc, harm, "não furta") is { } no) return Fail(no);
        var rng = ctx.OneShot($"player-theft-{npc.Id.Value}-{offer.Kind}-{offer.Id}-{now}");
        Theft.Commit(world, ctx, npc, TheftMotive.Opportunity, target, rng);
        PlayerOrders.Objection(world, ctx, npc, harm);
        return Done(offer, "steal", target.Units, target.Value);
    }

    /// <summary>Na fonte: beber até matar a sede e, com casa, encher o que couber (a mesma conta da ida de água).</summary>
    private static Result<InteractionResult> Water(WorldState world, Npc npc, InteractionOffer offer, string verb)
    {
        long hours = world.CurrentDate.TotalHours;
        if (verb == "drink")
        {
            WaterFetch.DrinkHere(world, npc, hours);
            return Done(offer, verb, 0, 0);
        }
        WaterFetch.FillAt(world, npc, world.FindHousehold(npc.Household!.Value)!, hours);
        return Done(offer, verb, world.Invention!.CarriedQuantity(npc.Id.Value, world.EconomyRules.WaterResourceId), 0);
    }

    /// <summary>Recolher o espólio de quem é da casa (ou o achado sem dono), como o NPC que vai buscá-lo.</summary>
    private static Result<InteractionResult> Collect(WorldState world, TickContext ctx, Npc npc, InteractionOffer offer)
    {
        var leaving = world.Invention!.Leavings!.First(l => l.Id == offer.Id);
        Leavings.Collect(world, ctx, npc, leaving.Id, Leavings.ClaimKind(world, leaving, npc)!);
        return Done(offer, "collect", 0, 0);
    }
}
