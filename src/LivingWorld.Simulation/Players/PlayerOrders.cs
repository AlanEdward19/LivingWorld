using System.Globalization;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>AD-197d: ordens que mudam a vida da pessoa possuída e podem ir contra o que ela valoriza (decisão do dono: "vender a casa,
/// abandonar um filho"). O peso sai do que a ordem faz (<see cref="OrderHarm"/>): a quem ela fere, quanto, e se tira a casa dela; a
/// recusa é a mesma disputa de vontade do golpe (<see cref="PossessionContest.RefusesOrder"/>): 409 com o motivo, o mundo intacto e
/// a recusa no registro. Na posse plena ela obedece presa e a objeção fica com ela (<see cref="Npc.RecordCaptiveObjection"/>).
/// Obedecida, a ordem passa pelos caminhos de sempre: a venda é troca de dinheiro e de teto (quem compra muda para a casa, quem
/// vende fica sem teto como no despejo); o abandono é o de <see cref="UnionDissolution"/> (quem sai vai sozinho e sem nada) e o
/// filho vive o abandono na psique e no laço.</summary>
public static class PlayerOrders
{
    /// <summary>Filho dependente de <paramref name="parent"/>: criança, viva, filho dele e morando na mesma casa.</summary>
    public static bool DependentChild(WorldState world, Npc parent, Npc child) =>
        child.IsAlive && parent.Household is { } home && child.Household == home
        && (child.MotherId == parent.Id || child.FatherId == parent.Id)
        && world.LifeStageRules.LifeStageOf(child.AgeYears(world.CurrentDate)) == LifeStage.Child;

    /// <summary>O que vender a casa faz: tira o teto de quem mora com ela (na medida de <see cref="PossessionRules.HomeLoss"/>) e o dela.</summary>
    public static OrderHarm SaleHarm(WorldState world, Npc seller, Household home) =>
        new([.. home.Members.Where(m => m != seller.Id).OrderBy(m => m.Value)
            .Select(m => world.FindNpc(m)).OfType<Npc>().Where(n => n.IsAlive)
            .Select(n => (n, world.Players.Possession.HomeLoss))], LosesHome: true);

    /// <summary>O que abandonar o filho faz: o dano inteiro nele.</summary>
    public static OrderHarm AbandonHarm(Npc child) => OrderHarm.Hurt(child);

    /// <summary>Vender a própria casa a uma casa da mesma cidade sem teto próprio (sem teto ou de aluguel) que possa pagar: o preço do
    /// imóvel no cenário (<c>Pricing</c>) ou, sem ele, a oferta de <see cref="PossessionRules.OfferShare"/> da carteira do chefe da
    /// compradora. Devolve o preço.</summary>
    public static Result<long> SellHouse(WorldState world, TickContext ctx, Npc seller, Household home, Building house)
    {
        if (home.Dwelling != house.Id) return Result<long>.Fail($"{PlayerCommands.Conflict} a casa não é dela");
        var harm = SaleHarm(world, seller, home);
        if (Refusal(world, ctx, seller, harm, "não vende a casa") is { } no) return Result<long>.Fail(no);

        var buyer = BuyerFor(world, home, house, out long price);
        if (buyer is null) return Result<long>.Fail($"{PlayerCommands.Conflict} ninguém na cidade compra a casa agora");
        if (!world.FindNpc(buyer.Head)!.TryDebitWallet(new Money(price)).IsSuccess)
            return Result<long>.Fail($"{PlayerCommands.Conflict} quem compra não tem o preço");
        seller.CreditWallet(new Money(price));
        buyer.MoveIn(house.Id, home.Location);
        home.Evict(ctx.CurrentTick); // sem teto, como no despejo: dorme onde estiver até achar vaga
        Objection(world, ctx, seller, harm);
        ctx.LogEvent(WorldEventKind.HouseSold, string.Create(CultureInfo.InvariantCulture,
            $"{seller.Id.Value}|{home.Id.Value}|{buyer.Id.Value}|{house.Id.Value}|{price}"), sourceSystem: PossessionSystem.SystemName);
        return Result<long>.Ok(price);
    }

    /// <summary>Abandonar o filho dependente: quem sai vai sozinho e sem nada (o abandono da união, se o cônjuge fica na casa); o filho
    /// vive o abandono e o laço com quem saiu sofre a traição.</summary>
    public static Result<Unit> AbandonChild(WorldState world, TickContext ctx, Npc parent, Npc child)
    {
        if (!DependentChild(world, parent, child)) return Result<Unit>.Fail($"{PlayerCommands.Conflict} não é filho dependente dela");
        var harm = AbandonHarm(child);
        if (Refusal(world, ctx, parent, harm, $"não abandona {child.Name}") is { } no) return Result<Unit>.Fail(no);

        var home = world.FindHousehold(parent.Household!.Value)!;
        if (parent.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true } spouse && spouse.Household == home.Id)
            UnionDissolution.Separate(world, ctx, parent, spouse, UnionEndMode.Abandonment);
        else
            UnionDissolution.Desert(world, ctx, parent);
        long hours = world.CurrentDate.TotalHours;
        if (world.NeedsRules.Psyche.Enabled) PsycheMechanic.Experience(world, child, LifeEventKind.Abandoned, parent.Id.Value, hours);
        world.GetOrCreateRelationship(new RelationshipKey(child.Id, parent.Id), hours).ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
        Objection(world, ctx, parent, harm);
        ctx.LogEvent(WorldEventKind.ChildAbandoned, string.Create(CultureInfo.InvariantCulture,
            $"{parent.Id.Value}|{child.Id.Value}|{parent.Household!.Value.Value}"), sourceSystem: PossessionSystem.SystemName);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>A recusa da vontade (posse comum), com o peso no motivo; nulo se ela obedece.</summary>
    internal static string? Refusal(WorldState world, TickContext ctx, Npc npc, OrderHarm harm, string what) =>
        PossessionContest.RefusesOrder(world, npc, harm, ctx.CurrentTick)
            ? string.Create(CultureInfo.InvariantCulture,
                $"{PlayerCommands.Refused} {npc.Name} {what} (peso {PossessionContest.OrderWeight(world, npc, harm):0})")
            : null;

    /// <summary>Obedecida: na posse plena a objeção fica com a pessoa presa; quem convive estranha na medida do peso.</summary>
    internal static void Objection(WorldState world, TickContext ctx, Npc npc, OrderHarm harm)
    {
        double weight = world.Players.Enabled ? PossessionContest.OrderWeight(world, npc, harm) : 0;
        if (npc.ControlMode == PossessionMode.Full)
        {
            var dearest = harm.Harmed.OrderByDescending(h => PossessionWill.CareFor(world, npc, h.Person) * h.Severity)
                .ThenBy(h => h.Person.Id.Value).Select(h => h.Person.Id.Value).DefaultIfEmpty(npc.Id.Value).First();
            npc.RecordCaptiveObjection(dearest, weight >= world.Players.RefuseHarmCare ? (int)Math.Round(weight) : 0, ctx.CurrentTick);
        }
        PossessionWitness.OnOrder(world, ctx, npc, weight);
    }

    /// <summary>A casa da mesma cidade, sem teto próprio, que pode pagar o preço; a de chefe mais rico, depois a de menor id.</summary>
    private static Household? BuyerFor(WorldState world, Household seller, Building house, out long price)
    {
        long listed = 0;
        if (world.FindCity(house.City) is { } city
            && world.CityCatalog.BuildingRecipes.TryGetValue(house.BuildingTypeId, out var recipe) && recipe.Pricing is not null)
            listed = BuildingValuation.ValueOf(new ValuationContext(world, city), house, recipe);
        Household? best = null;
        long bestWallet = 0;
        price = 0;
        foreach (var candidate in world.Households.OrderBy(h => h.Id.Value))
        {
            if (candidate.Id == seller.Id || candidate.City != seller.City || candidate.PendingRelocationCity is not null) continue;
            if (candidate.Dwelling is { } d && !Rented(world, d)) continue;
            if (world.FindNpc(candidate.Head) is not { IsAlive: true } head) continue;
            long offer = listed > 0 ? listed : (long)(head.Wallet.Amount * world.Players.Possession.OfferShare);
            if (offer <= 0 || head.Wallet.Amount < offer || head.Wallet.Amount <= bestWallet) continue;
            best = candidate;
            bestWallet = head.Wallet.Amount;
            price = offer;
        }
        return best;
    }

    private static bool Rented(WorldState world, BuildingId dwelling) =>
        world.FindBuilding(dwelling) is { } building
        && world.CityCatalog.BuildingRecipes.TryGetValue(building.BuildingTypeId, out var recipe) && recipe.Lodging is not null;
}
