using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Socorro em grão da cidade na carestia (AD-182, 08c item 148). Medido (seed 26, quebra do ano 4): o celeiro tinha 16 mil
/// unidades e soltava por dia o que dura até a colheita (acabou um mês antes dela: o racionamento estava certo), mas a venda ia a quem
/// pagava 20 a unidade; as 20–25 casas carentes só tinham esmola em moeda, que não compra na banca vazia, e morriam (104 → 73 vivos,
/// 300 furtos por mês). A cidade decide: a ajuda mútua registra as casas carentes cuja moeda não compra comida
/// (<see cref="WorldState.GrainReliefRequests"/>), e a lavoura, ao soltar a ração do dia, entrega primeiro a elas a comida de
/// <see cref="PopulationPressureRules.GrainReliefDays"/> dias, comprada pelo tesouro ao preço da venda; a venda fica com o resto. A
/// ração do dia não cresce: soltar além dela adiantava o consumo e matava no fim da carestia (tentado: 4,6× as mortes de fome).
/// A semente não sai. Comida e moeda só mudam de lugar.</summary>
internal static class GrainRelief
{
    public const string Source = "grain-relief";

    /// <summary>Serve, do celeiro de <paramref name="field"/> e dentro de <paramref name="ration"/>, os pedidos da cidade da fazenda;
    /// devolve as unidades entregues (que saem da ração da venda).</summary>
    internal static long Serve(WorldState world, TickContext ctx, Workplace farm, CropBatch field, ResourceType food, long ration)
    {
        var requests = world.GrainReliefRequests;
        if (requests.Count == 0 || ration <= 0 || field.Barn <= 0 || world.FindCity(farm.City) is not { } city || city.Treasury.Amount <= 0)
            return 0;
        long asked = 0;
        foreach (var r in requests) if (r.House.City == farm.City) asked += r.Units;
        long budget = Math.Min(Math.Min(asked, ration), field.Barn);
        if (budget <= 0) return 0;
        long price = Math.Max(1, farm.Prices.TryGetValue(food, out var p) && p > 0 ? p
            : world.EconomyRules.PriceFloor.GetValueOrDefault(food.Id, 1));
        long served = 0;
        for (int i = 0; i < requests.Count; i++)
        {
            var (house, recipient, units) = requests[i];
            if (house.City != farm.City || units <= 0) continue;
            long share = Math.Min(Math.Min(units, (long)Math.Ceiling(units * (double)budget / asked)), budget - served);
            share = Math.Min(share, city.Treasury.Amount / price);
            if (share <= 0 || !city.TryDebitTreasury(new Money(share * price)).IsSuccess) continue;
            field.TakeBarn(share);
            farm.CreditTreasury(new Money(share * price));
            world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: 0, spending: share * price);
            house.Deposit(food, share);
            served += share;
            requests[i] = (house, recipient, units - share);
            world.PurchaseTally.GrainReliefUnits += share;
            ctx.LogEvent(WorldEventKind.AidGiven, $"city-{city.Id.Value}|{recipient.Id.Value}|{share}|food", sourceSystem: Source);
        }
        return served;
    }
}
