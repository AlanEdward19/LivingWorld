using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Invention;
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

        if (WaterFetch.On(world))
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new CellCoord(at.X + dx, at.Y + dy);
                    if (!world.Map.TryGetCell(cell, out var water) || !water.HasWater) continue;
                    offers.Add(Offer("water", WaterId(cell), "", household is null ? ["drink"] : ["drink", "fill"], cell));
                    dy = 2;
                    break;
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
        for (int i = 0; i < furnishing.Count; i++)
        {
            if (catalog.Find(furnishing[i]) is not { } descriptor) continue;
            var verbs = descriptor.UsePoints.Where(p => p.Action is int a && PlayerCommands.Choosable.Contains((ActionType)a)
                    && (ActionType)a != ActionType.UsePower && Objects.ObjectUse.Allows(descriptor, (ActionType)a, npc))
                .Select(p => ((ActionType)p.Action!.Value).ToString().ToLowerInvariant()).Distinct().ToList();
            if (verbs.Count > 0) offers.Add(Offer("object", here.Value * 1000 + i, descriptor.Id, verbs, at));
        }
    }

    /// <summary>AD-197k: usar o objeto é pedir a ação dele ali (o motor aplica o efeito do objeto na ação, como para qualquer NPC).</summary>
    private static Result<InteractionResult> Use(WorldState world, Npc npc, InteractionOffer offer, string verb)
    {
        if (!Enum.TryParse<ActionType>(verb, ignoreCase: true, out var action)) return Fail("interact: ação desconhecida");
        var asked = PlayerCommands.Act(world, npc.Id, npc.ControllerId!, action);
        return asked.IsSuccess ? Done(offer, verb, 0, 0) : Fail(asked.Error!);
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
