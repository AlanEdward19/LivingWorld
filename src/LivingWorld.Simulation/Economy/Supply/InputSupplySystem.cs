using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Supply;

/// <summary>Cadeia de suprimento dentro de cada cidade, <c>Daily</c>: leva o insumo que falta a quem precisa dele,
/// comprando de outra oficina da mesma cidade ao preço dela. Dois compradores: a oficina cuja receita declara
/// <c>Inputs</c> (paga com o próprio tesouro, até <see cref="TargetDays"/> dias de produção) e a própria cidade, que
/// mantém em <see cref="City.Stock"/> o material de uma obra de cada receita do catálogo (paga com <see
/// cref="City.Treasury"/>). O dinheiro só muda de caixa e o recurso só muda de estoque: nada é cunhado nem destruído.
/// Sem vendedor com estoque, sem caixa ou sem espaço, a compra é 0 e o mundo fica idêntico.</summary>
[LivingCapability("SUPPLY", Consumers = ["inspector.production", "inspector.city.construction"])]
[RunsAfter(typeof(EmploymentSystem))]
public sealed class InputSupplySystem : ISimulationSystem
{
    public const string SystemName = "economy-input-supply";

    /// <summary>Dias de produção que a oficina tenta manter em estoque de cada insumo.</summary>
    public const int TargetDays = 3;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.EconomyRules;
        if (!rules.Enabled) return;

        var catalog = world.EconomyCatalog;
        bool workplacesNeedInputs = catalog.Recipes.Values.Any(recipe => recipe.Inputs.Count > 0);
        var cityTargets = world.CityRules.Enabled ? ConstructionTargets(world) : [];
        if (!workplacesNeedInputs && cityTargets.Count == 0) return;

        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
        {
            var workplaces = world.Workplaces.Where(w => w.City == city.Id).OrderBy(w => w.Id.Value).ToList();
            if (workplaces.Count == 0) continue;

            if (workplacesNeedInputs)
                foreach (var buyer in workplaces)
                    SupplyWorkplace(world, catalog, rules, workplaces, buyer);

            foreach (var (resource, target) in cityTargets.OrderBy(e => e.Key.Id))
            {
                long missing = target - city.Stock.GetValueOrDefault(resource);
                if (missing <= 0) continue;
                long bought = Buy(rules, workplaces, null, resource, missing,
                    city.Treasury, out var paid);
                if (bought <= 0) continue;
                city.TryDebitTreasury(paid);
                city.DepositStock(resource, bought);
                world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: 0, spending: paid.Amount);
            }
        }
    }

    /// <summary>Material de uma obra: o maior insumo que qualquer receita do catálogo pede de cada recurso.</summary>
    private static Dictionary<ResourceType, long> ConstructionTargets(WorldState world)
    {
        var targets = new Dictionary<ResourceType, long>();
        foreach (var recipe in world.CityCatalog.BuildingRecipes.Values)
            foreach (var (resource, amount) in recipe.Inputs)
                if (amount > 0 && amount > targets.GetValueOrDefault(resource))
                    targets[resource] = amount;
        return targets;
    }

    private static void SupplyWorkplace(
        WorldState world, EconomyCatalog catalog, EconomyRules rules, List<Workplace> workplaces, Workplace buyer)
    {
        if (!catalog.Recipes.TryGetValue(buyer.LocationType.Id, out var recipe) || recipe.Inputs.Count == 0) return;
        int employees = buyer.Employees.Count;
        if (employees == 0) return;

        foreach (var (resourceId, perWorker) in recipe.Inputs.OrderBy(e => e.Key))
        {
            if (perWorker <= 0) continue;
            var resource = new ResourceType(resourceId);
            long missing = perWorker * employees * TargetDays - buyer.Stock.GetValueOrDefault(resource);
            if (missing <= 0) continue;

            long room = rules.CapacityOf(resource, buyer.LocationType) - buyer.Stock.GetValueOrDefault(resource);
            long bought = Buy(rules, workplaces, buyer, resource, Math.Min(missing, room),
                buyer.Treasury, out var paid);
            if (bought <= 0) continue;
            buyer.TryDebitTreasury(paid);
            buyer.Deposit(resource, bought, rules);
        }
    }

    /// <summary>Compra até <paramref name="wanted"/> unidades dos vendedores mais baratos (desempate pelo menor id),
    /// limitado ao caixa do comprador. Retira o estoque e credita o caixa de cada vendedor; devolve a quantidade e o
    /// total pago, que o chamador debita do comprador.</summary>
    private static long Buy(
        EconomyRules rules, List<Workplace> workplaces, Workplace? buyer, ResourceType resource, long wanted,
        Money buyerTreasury, out Money paid)
    {
        paid = Money.Zero;
        if (wanted <= 0) return 0;

        long remainingFunds = buyerTreasury.Amount;
        long bought = 0;
        long total = 0;
        foreach (var seller in workplaces
                     .Where(w => w != buyer && w.Stock.GetValueOrDefault(resource) > 0)
                     .OrderBy(w => PriceOf(rules, w, resource)).ThenBy(w => w.Id.Value))
        {
            if (bought >= wanted) break;
            long price = PriceOf(rules, seller, resource);
            long affordable = remainingFunds / price;
            long quantity = Math.Min(Math.Min(wanted - bought, seller.Stock[resource]), affordable);
            if (quantity <= 0) continue;

            seller.Withdraw(resource, quantity);
            seller.CreditTreasury(new Money(quantity * price));
            bought += quantity;
            total += quantity * price;
            remainingFunds -= quantity * price;
        }

        paid = new Money(total);
        return bought;
    }

    private static long PriceOf(EconomyRules rules, Workplace seller, ResourceType resource) =>
        Math.Max(1, seller.Prices.TryGetValue(resource, out var price)
            ? price
            : rules.PriceFloor.GetValueOrDefault(resource.Id, 1));
}
