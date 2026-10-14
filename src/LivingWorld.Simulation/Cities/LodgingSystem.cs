using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities;

/// <summary>Hospedagem (lodging-and-rent), <c>Monthly</c>: quem mora num prédio cujo tipo declara <c>Lodging</c> paga o
/// aluguel da carteira do chefe ao tesouro da oficina do prédio (o locador); quem não paga acumula atraso e,
/// passada a carência do tipo, perde a vaga. Domicílios sem teto alugam a vaga mais barata da própria cidade.
/// Moeda só muda de dono (conservação) e não há sorteio: tudo em ordem de id. Cenário sem <c>Lodging</c> não faz nada.</summary>
[LivingCapability("LODGING", Events = [WorldEventKind.RentUnpaid, WorldEventKind.Evicted], Consumers = ["timeline.lodging"])]
[RunsAfter(typeof(WagePaymentSystem))]
public sealed class LodgingSystem : ISimulationSystem
{
    public const string SystemName = "cities-lodging";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        var recipes = world.CityCatalog.BuildingRecipes;
        var landlords = new Dictionary<BuildingId, Workplace>();
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
            if (workplace.Building is { } building)
                landlords.TryAdd(building, workplace);

        var lodgings = world.Buildings
            .Where(b => recipes.TryGetValue(b.BuildingTypeId, out var recipe) && recipe.Lodging is not null && landlords.ContainsKey(b.Id))
            .OrderBy(b => b.Id.Value)
            .ToList();
        if (lodgings.Count == 0) return;

        var tenants = world.Households.Where(h => h.Dwelling is not null)
            .OrderBy(h => h.Id.Value)
            .GroupBy(h => h.Dwelling!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var contexts = new Dictionary<CityId, ValuationContext>();
        long RentOf(Building building)
        {
            if (!contexts.TryGetValue(building.City, out var context))
            {
                var city = world.FindCity(building.City);
                if (city is null) return recipes[building.BuildingTypeId].Lodging!.RentPerMonth;
                contexts[building.City] = context = new ValuationContext(world, city);
            }
            return BuildingValuation.RentOf(context, building, recipes[building.BuildingTypeId]);
        }

        foreach (var building in lodgings)
            CollectRent(world, ctx, building, recipes[building.BuildingTypeId].Lodging!, RentOf(building), landlords[building.Id], tenants);

        MoveInHomeless(world, ctx, lodgings, tenants, RentOf);
    }

    private static void CollectRent(
        WorldState world, TickContext ctx, Building building, LodgingTerms terms, long monthlyRent, Workplace landlord,
        Dictionary<BuildingId, List<Household>> tenants)
    {
        if (!tenants.TryGetValue(building.Id, out var residents)) return;

        var rent = new Money(monthlyRent);
        foreach (var household in residents.ToList())
        {
            var head = world.FindNpc(household.Head);
            if (head is { IsAlive: true } && head.TryDebitWallet(rent).IsSuccess)
            {
                landlord.CreditTreasury(rent);
                household.PayRent();
                continue;
            }

            int arrears = household.RecordUnpaidRent();
            ctx.LogEvent(
                WorldEventKind.RentUnpaid, $"{household.Id.Value}|{building.Id.Value}|{rent.Amount}|{arrears}", sourceSystem: "LodgingSystem");
            if (arrears <= terms.GraceMonths) continue;

            household.Evict(ctx.CurrentTick);
            residents.Remove(household);
            ctx.LogEvent(WorldEventKind.Evicted, $"{household.Id.Value}|{building.Id.Value}", sourceSystem: "LodgingSystem");
        }
    }

    private static void MoveInHomeless(
        WorldState world, TickContext ctx, IReadOnlyList<Building> lodgings, Dictionary<BuildingId, List<Household>> tenants,
        Func<Building, long> rentOf)
    {
        foreach (var household in world.Households.Where(h => h.Dwelling is null && h.PendingRelocationCity is null).OrderBy(h => h.Id.Value).ToList())
        {
            var head = world.FindNpc(household.Head);
            if (head is not { IsAlive: true }) continue;

            Building? best = null;
            long bestRent = long.MaxValue;
            foreach (var building in lodgings)
            {
                if (building.City != household.City || building.Position is null) continue;
                long rent = rentOf(building);
                int occupied = tenants.TryGetValue(building.Id, out var residents) ? residents.Count : 0;
                if (occupied >= DwellingCapacity.Of(world, building) || rent > head.Wallet.Amount || rent >= bestRent) continue;
                best = building;
                bestRent = rent;
            }

            if (best is null) continue;

            household.MoveIn(best.Id, DwellingCapacity.InteriorCellOf(best, best.Position!.Value));
            if (!tenants.TryGetValue(best.Id, out var list))
                tenants[best.Id] = list = [];
            list.Add(household);
        }
    }
}
