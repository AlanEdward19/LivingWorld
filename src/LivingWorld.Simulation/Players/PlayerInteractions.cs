using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>Um recurso de uma oferta: quanto há (estoque, mãos) e o preço da banca (0 fora de banca).</summary>
public sealed record InteractionResource(int ResourceId, long Quantity, long Price);

/// <summary>O que o corpo conduzido alcança agora e o que pode fazer ali (AD-197d). <c>Kind</c>: <c>door</c> (prédio: enter, exit),
/// <c>stock</c> (casa: take, deposit), <c>stall</c> (oficina com preço: buy, sell), <c>bed</c> (casa: sleep), <c>person</c> (pessoa:
/// talk, strike, abandon), <c>house</c> (a própria casa: sell). O cliente mostra só isto.</summary>
public sealed record InteractionOffer(
    string Kind, long Id, string Label, IReadOnlyList<string> Verbs, int X, int Y, IReadOnlyList<InteractionResource> Resources);

/// <summary>O que a interação fez: a quantidade e o valor que mudaram de mão (0 quando não é troca).</summary>
public sealed record InteractionResult(string Kind, long Id, string Verb, long Quantity, long Value);

/// <summary>Interagir com o que está perto (AD-197d, decisão do dono: "pode interagir com as coisas"), resolvido no motor pelos
/// mesmos sistemas de qualquer NPC: a porta é o passo pela soleira (<see cref="PlayerGround"/>); o estoque é a despensa da casa e as
/// mãos (<see cref="Npc.PickUp(ResourceType, long)"/>, <see cref="Npc.GiveCarriedTo"/>); a banca é a troca de mercado
/// (<see cref="MarketTransaction"/>, preço da banca, tesouro, livro de trocas e o laço com quem atende); a cama é o sono que o
/// jogador já pede; a pessoa é a conversa (a disponibilidade de sempre e o laço de conversa), o golpe e, com o filho dependente, o
/// abandono; a casa própria é a venda. As ordens contra o que a pessoa valoriza passam pela disputa de vontade
/// (<see cref="PlayerOrders"/>). Toda interação é validada contra a oferta do mesmo instante: fora dela, recusa sem mudar o mundo.</summary>
public static partial class PlayerInteractions
{
    public static IReadOnlyList<InteractionOffer> Offers(WorldState world, Npc npc)
    {
        var offers = new List<InteractionOffer>();
        var at = PlayerGround.PoseOf(world, npc);
        var inside = PlayerGround.At(world, at)?.Building.Id;
        var household = npc.Household is { } h ? world.FindHousehold(h) : null;

        int floor = PlayerGround.FloorOf(npc);
        if (floor > 0) inside = npc.Interior!.Building;
        foreach (var building in world.Buildings.OrderBy(b => b.Id.Value))
            if (floor == 0 && PlayerGround.DoorOf(building) is { } door && building.CompletedAtTick <= world.CurrentDate.TotalHours)
            {
                if (at == door.Doorstep) offers.Add(Offer("door", building.Id.Value, "", ["enter"], door.Door));
                else if (at == door.Door) offers.Add(Offer("door", building.Id.Value, "", ["exit"], door.Doorstep));
            }

        if (household is not null && Near(at, inside, household.Location, household.Dwelling))
        {
            var stock = household.Stock.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.Id)
                .Select(kv => new InteractionResource(kv.Key.Id, kv.Value, 0)).ToList();
            var verbs = new List<string>();
            if (!npc.IsCarrying && stock.Count > 0) verbs.Add("take");
            if (npc.IsCarrying || Laden(world, npc)) verbs.Add("deposit");
            if (verbs.Count > 0) offers.Add(Offer("stock", household.Id.Value, "", verbs, household.Location, stock));
            var bed = world.BedOf(household.Id)?.Location ?? household.Location;
            if (Near(at, inside, bed, household.Dwelling)) offers.Add(Offer("bed", household.Id.Value, "", ["sleep"], bed));
            if (household.Dwelling is { } dwelling && OwnedHome(world, dwelling) && world.FindBuilding(dwelling) is { Position: { } spot })
                offers.Add(Offer("house", dwelling.Value, "", ["sell"], spot));
        }

        if (world.EconomyRules.Enabled)
            foreach (var stall in world.Workplaces.OrderBy(w => w.Id.Value))
            {
                if (stall.Prices.Count == 0 || !Near(at, inside, stall.Location, stall.Building)) continue;
                var goods = stall.Prices.Where(p => p.Value > 0).OrderBy(p => p.Key.Id)
                    .Select(p => new InteractionResource(p.Key.Id, stall.Stock.GetValueOrDefault(p.Key), p.Value)).ToList();
                var verbs = new List<string>();
                if (goods.Any(g => g.Quantity > 0)) verbs.Add("buy");
                if (npc.IsCarrying && stall.Prices.GetValueOrDefault(new ResourceType(npc.CarriedResourceId)) > 0) verbs.Add("sell");
                if (CanSteal(world, npc) && Outsider(npc, stall) && goods.Any(g => g.Quantity > 0)) verbs.Add("steal");
                if (verbs.Count > 0) offers.Add(Offer("stall", stall.Id.Value, "", verbs, stall.Location, goods));
            }

        foreach (var other in world.AliveNpcIndex.Alive)
        {
            if (other.Id == npc.Id || other.City != npc.City) continue;
            if (other.CurrentAction != ActionType.Travel && other.Interior is null && Chebyshev(other.CurrentLocation, at) > 1) continue;
            var pose = PlayerGround.PoseOf(world, other);
            if (Chebyshev(pose, at) > 1 || !PlayerGround.SameLevel(npc, other)) continue;
            var verbs = new List<string> { "talk" };
            if (world.CrimeOn) verbs.Add("strike");
            if (PlayerOrders.DependentChild(world, npc, other)) verbs.Add("abandon");
            offers.Add(Offer("person", other.Id.Value, other.Name, verbs, pose));
        }
        WorldOffers(world, npc, at, floor, inside, household, offers);
        return offers;
    }

    /// <summary>Faz a interação, se ela está na oferta de agora. Erros começam como os de <see cref="PlayerCommands"/> (a borda mapeia
    /// 404, 409, 400); a recusa da vontade começa com <see cref="PlayerCommands.Refused"/>.</summary>
    public static Result<InteractionResult> Interact(WorldState world, TickContext ctx, Npc npc, string kind, long id, string verb,
        int? resourceId = null, long? quantity = null)
    {
        var offer = Offers(world, npc).FirstOrDefault(o => o.Kind == kind && o.Id == id && o.Verbs.Contains(verb));
        if (offer is null) return Fail($"{PlayerCommands.Conflict} interact: {kind} {id} {verb} fora do alcance ou indisponível agora");
        if (quantity is <= 0) return Fail("interact: quantidade deve ser > 0");
        return (kind, verb) switch
        {
            ("door", _) => Walk(world, ctx, npc, offer),
            ("stairs", _) => Climb(world, npc, offer, verb == "up" ? 1 : -1),
            (_, "steal") => Steal(world, ctx, npc, offer, resourceId),
            ("object", _) => Use(world, npc, offer, verb),
            ("item", _) => Carry(world, npc, offer, verb),
            ("workshop" or "field", "work") => PlayerCommands.Act(world, npc.Id, npc.ControllerId!, ActionType.Work) is { IsSuccess: false } busy
                ? Fail(busy.Error!) : Done(offer, verb, 0, 0),
            ("water", _) => Water(world, npc, offer, verb),
            ("leaving", _) => Collect(world, ctx, npc, offer),
            ("stock", "take") => Take(world, npc, world.FindHousehold(new HouseholdId(id))!, resourceId, quantity, offer),
            ("stock", _) => Deposit(world, npc, world.FindHousehold(new HouseholdId(id))!, offer),
            ("stall", "buy") => Buy(world, npc, world.FindWorkplace(new WorkplaceId(id))!, resourceId, quantity ?? 1, offer),
            ("stall", _) => Sell(world, npc, world.FindWorkplace(new WorkplaceId(id))!, offer),
            ("bed", _) => PlayerCommands.Act(world, npc.Id, npc.ControllerId!, ActionType.Sleep) is { IsSuccess: false } refused
                ? Fail(refused.Error!) : Done(offer, verb, 0, 0),
            ("person", "talk") => Talk(world, npc, world.FindNpc(new NpcId(id))!, offer),
            ("person", "strike") => PlayerCommands.Strike(world, ctx, npc.Id, npc.ControllerId!, new NpcId(id)) is { IsSuccess: false } hit
                ? Fail(hit.Error!) : Done(offer, verb, 0, 0),
            ("person", _) => PlayerOrders.AbandonChild(world, ctx, npc, world.FindNpc(new NpcId(id))!) is { IsSuccess: false } left
                ? Fail(left.Error!) : Done(offer, verb, 0, 0),
            _ => Sale(world, ctx, npc, new BuildingId(id), offer),
        };
    }

    private static Result<InteractionResult> Sale(WorldState world, TickContext ctx, Npc npc, BuildingId house, InteractionOffer offer)
    {
        var sold = PlayerOrders.SellHouse(world, ctx, npc, world.FindHousehold(npc.Household!.Value)!, world.FindBuilding(house)!);
        return sold.IsSuccess ? Done(offer, "sell", 1, sold.Value) : Fail(sold.Error!);
    }

    private static Result<InteractionResult> Walk(WorldState world, TickContext ctx, Npc npc, InteractionOffer offer)
    {
        var to = new CellCoord(offer.X, offer.Y);
        var from = PlayerGround.PoseOf(world, npc);
        var step = PlayerGround.Step(world, npc, from, to);
        if (!step.IsSuccess) return Fail(step.Error!);
        var walked = PlayerCommands.Walk(world, ctx, npc, from, to); // a porta é um passo: no tempo do corpo
        return walked.IsSuccess ? Done(offer, offer.Verbs[0], 0, 0) : Fail(walked.Error!);
    }

    private static Result<InteractionResult> Take(WorldState world, Npc npc, Household home, int? resourceId, long? quantity,
        InteractionOffer offer)
    {
        var pick = offer.Resources.FirstOrDefault(r => resourceId is null || r.ResourceId == resourceId);
        if (pick is null) return Fail("interact: a despensa não tem esse recurso");
        long capacity = Math.Max(1, npc.CarryCapacity > 0 ? npc.CarryCapacity : Npc.DefaultCarryCapacity);
        long n = Math.Min(quantity ?? Math.Min(pick.Quantity, capacity), pick.Quantity);
        if (n > capacity) return Fail("interact: excede o que as mãos levam");
        var resource = new ResourceType(pick.ResourceId);
        if (!home.Withdraw(resource, n).IsSuccess) return Fail("interact: a despensa não tem tanto");
        var held = npc.PickUp(resource, n, capacity);
        if (!held.IsSuccess)
        {
            home.Deposit(resource, n); // devolve: nada some
            return Fail(held.Error!);
        }
        return Done(offer, "take", n, 0);
    }

    /// <summary>Guardar: o que vai nas mãos e a carga trazida (a água da fonte, o espólio recolhido), como em toda volta para casa.</summary>
    private static Result<InteractionResult> Deposit(WorldState world, Npc npc, Household home, InteractionOffer offer)
    {
        long n = npc.CarriedQuantity;
        if (npc.IsCarrying && npc.GiveCarriedTo(home) is { IsSuccess: false } given) return Fail(given.Error!);
        if (Laden(world, npc)) LivingWorld.Simulation.Invention.PersonalCarry.Unpack(world, npc, home);
        return Done(offer, "deposit", n, 0);
    }

    /// <summary>Comprar na banca: o mesmo caminho da compra de qualquer NPC (preço da banca, carteira, estoque, tesouro, entrega na
    /// despensa da casa, livro de trocas); sem casa, nas mãos.</summary>
    private static Result<InteractionResult> Buy(WorldState world, Npc npc, Workplace stall, int? resourceId, long quantity, InteractionOffer offer)
    {
        var item = offer.Resources.FirstOrDefault(r => r.ResourceId == resourceId && r.Quantity > 0);
        if (item is null) return Fail("interact: a banca não vende esse recurso agora");
        var resource = new ResourceType(item.ResourceId);
        var home = npc.Household is { } h ? world.FindHousehold(h) : null;
        var tx = new TransactionContext(npc.Wallet, stall.Treasury, stall.Stock.GetValueOrDefault(resource),
            home?.Stock.GetValueOrDefault(resource) ?? 0, resource, item.Price, quantity);
        var check = MarketTransaction.Execute(tx);
        if (!check.IsSuccess) return Fail($"interact: compra recusada ({check.Error})");
        if (home is null && !npc.PickUp(resource, quantity).IsSuccess) return Fail("interact: sem casa e sem como levar");
        var price = new Money(item.Price * quantity);
        npc.TryDebitWallet(price);
        stall.Withdraw(resource, quantity);
        stall.CreditTreasury(price);
        home?.Deposit(resource, quantity);
        LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.NoteTrade(world, npc, stall, resource, quantity, price.Amount);
        return Done(offer, "buy", quantity, price.Amount);
    }

    /// <summary>Vender à banca o que se leva nas mãos, ao preço dela, pago do tesouro dela (a mesma troca, de mão trocada).</summary>
    private static Result<InteractionResult> Sell(WorldState world, Npc npc, Workplace stall, InteractionOffer offer)
    {
        var resource = new ResourceType(npc.CarriedResourceId);
        long unit = stall.Prices.GetValueOrDefault(resource), n = npc.CarriedQuantity;
        var tx = new TransactionContext(stall.Treasury, npc.Wallet, n, stall.Stock.GetValueOrDefault(resource), resource, unit, n);
        var check = MarketTransaction.Execute(tx);
        if (!check.IsSuccess) return Fail($"interact: a banca não paga agora ({check.Error})");
        var price = new Money(unit * n);
        stall.TryDebitTreasury(price);
        npc.CreditWallet(price);
        npc.TakeCarried();
        long lost = stall.Deposit(resource, n, world.EconomyRules);
        world.RecordTrade(stall.Id, resource, world.CurrentDate.TotalHours / world.CurrentDate.Calendar.HoursPerDay, n - lost, price.Amount);
        return Done(offer, "sell", n, price.Amount);
    }

    /// <summary>Puxar conversa: a pessoa aceita ou não pela disponibilidade de sempre (ocupada, hostil); aceitando, o encontro conta como
    /// conversa no laço dos dois e quem foi chamado reage já.</summary>
    private static Result<InteractionResult> Talk(WorldState world, Npc npc, Npc other, InteractionOffer offer)
    {
        long hours = world.CurrentDate.TotalHours;
        var toward = world.Relationships.TryGetValue(new RelationshipKey(other.Id, npc.Id), out var bond) ? bond : null;
        var decision = LivingWorld.Simulation.Llm.ConversationAvailabilityPolicy.Evaluate(other, world.NeedsRules,
            LivingWorld.Simulation.Llm.ConversationAvailabilityPolicy.PlayerRules, toward, hours);
        if (decision.Result != LivingWorld.Domain.Llm.ConversationStartDecision.Accepted)
            return Fail($"{PlayerCommands.Conflict} conversa recusada: {decision.Result}");
        foreach (var key in new[] { new RelationshipKey(npc.Id, other.Id), new RelationshipKey(other.Id, npc.Id) })
        {
            var relationship = world.GetOrCreateRelationship(key, hours);
            relationship.ApplyEvent(RelationshipEventType.Conversation, world.FamilyRules);
            relationship.MarkContact(hours);
        }
        PlayerCommands.WakeNow(world, other);
        return Done(offer, "talk", 0, 0);
    }

    /// <summary>Perto de um lugar: a uma célula dele, ou dentro do prédio dele.</summary>
    private static bool Near(CellCoord at, BuildingId? inside, CellCoord place, BuildingId? building) =>
        Chebyshev(at, place) <= 1 || (inside is { } i && building is { } b && i == b);

    private static int Chebyshev(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Casa própria: não é vaga de hospedagem (essas são da oficina que aluga).</summary>
    private static bool OwnedHome(WorldState world, BuildingId dwelling) =>
        world.FindBuilding(dwelling) is { } building
        && !(world.CityCatalog.BuildingRecipes.TryGetValue(building.BuildingTypeId, out var recipe) && recipe.Lodging is not null);

    private static InteractionOffer Offer(string kind, long id, string label, IReadOnlyList<string> verbs, CellCoord at,
        IReadOnlyList<InteractionResource>? resources = null) =>
        new(kind, id, label, verbs, at.X, at.Y, resources ?? []);

    private static Result<InteractionResult> Done(InteractionOffer offer, string verb, long quantity, long value) =>
        Result<InteractionResult>.Ok(new InteractionResult(offer.Kind, offer.Id, verb, quantity, value));

    private static Result<InteractionResult> Fail(string error) => Result<InteractionResult>.Fail(error);
}
