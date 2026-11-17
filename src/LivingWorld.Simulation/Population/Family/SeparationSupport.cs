using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Sustento depois da separação (AD-165), uma vez por mês pelo <see cref="UnionSystem"/>. A AD-164 mediu que quem sai
/// de uma união leva os filhos pequenos e uma parte da despensa e fica sem sustento (fome crítica 3× a da aldeia, 10× nos
/// menores de 5). Aqui o pai ou a mãe que não mora com o filho comum abaixo de <see cref="UnionRules.SupportChildUntilAge"/>
/// cobre o que falta ao mês da casa dele: primeiro comida da própria despensa, depois moeda da própria carteira, sem descer
/// abaixo da reserva da própria casa. Nada é criado: a comida e a moeda só mudam de dono, e cada remessa grava um
/// <see cref="WorldEventKind.SupportPaid"/>. Consumidor causal: a despensa e a carteira da casa do filho, que a decisão de
/// comer e de comprar já lê. No abandono quem fugiu não manda nada.</summary>
public static class SeparationSupport
{
    /// <summary>Quem <paramref name="payer"/> deve sustentar hoje, e manda; devolve se mandou algo.</summary>
    public static bool Pay(WorldState world, TickContext ctx, Npc payer)
    {
        var rules = world.FamilyRules.Unions;
        var economy = world.EconomyRules;
        if (rules.SupportChildUntilAge <= 0 || !economy.Enabled) return false;
        if (payer.Union is not { FormerSpouse: { } formerId, EndMode: { } mode } || mode == UnionEndMode.Abandonment) return false;
        if (world.FindNpc(new NpcId(formerId)) is not { IsAlive: true, Household: { } homeId } carer) return false;
        if (homeId == payer.Household || world.FindHousehold(homeId) is not { } home) return false;

        var now = world.CurrentDate;
        int children = 0, members = 0;
        foreach (var memberId in home.Members)
        {
            if (world.FindNpc(memberId) is not { IsAlive: true } member) continue;
            members++;
            bool common = (member.MotherId == payer.Id && member.FatherId == carer.Id)
                          || (member.FatherId == payer.Id && member.MotherId == carer.Id);
            if (common && member.AgeYears(now) < rules.SupportChildUntilAge) children++;
        }
        if (children == 0) return false;

        // o que falta à casa do filho para o mês, em moeda: comida e água de cada dependente pelo preço do mercado da cidade
        // (o mais barato; sem mercado, o piso), menos o que a casa tem na despensa e nas carteiras
        var food = new ResourceType(economy.FoodResourceId);
        var water = new ResourceType(economy.WaterResourceId);
        long foodPrice = PriceOf(world, carer, food, economy.PriceFloor.GetValueOrDefault(economy.FoodResourceId, 1));
        long waterPrice = PriceOf(world, carer, water, economy.PriceFloor.GetValueOrDefault(economy.WaterResourceId, 1));
        long days = now.Calendar.DaysPerMonth;
        long dependants = Math.Min(members, children + (rules.SupportIncludesCarer && carer.Sex == Sex.Female ? 1 : 0));
        long foodStock = home.Stock.GetValueOrDefault(food);
        long have = foodStock * foodPrice + home.Stock.GetValueOrDefault(water) * waterPrice;
        foreach (var memberId in home.Members)
            if (world.FindNpc(memberId) is { IsAlive: true } member) have += member.Wallet.Amount;
        long shortfall = dependants * days * (foodPrice + waterPrice) - have;
        if (shortfall <= 0) return false;

        // primeiro a comida que a casa do pagador tem além da reserva dela
        long sentFood = 0;
        var own = payer.Household is { } ownId ? world.FindHousehold(ownId) : null;
        long ownMembers = Math.Max(1, own?.Members.Count ?? 1);
        if (own is not null)
        {
            long spare = own.Stock.GetValueOrDefault(food) - ownMembers * rules.SupportReserveDays;
            long units = Math.Min(spare, Math.Min(dependants * days - foodStock, shortfall / foodPrice));
            if (units > 0 && own.Withdraw(food, units).IsSuccess)
            {
                home.Deposit(food, units);
                sentFood = units;
                shortfall -= units * foodPrice;
            }
        }

        // depois a moeda que passa da reserva da própria casa
        long sentCoin = 0;
        long keep = ownMembers * rules.SupportReserveDays * (foodPrice + waterPrice);
        long coin = Math.Min(Math.Max(0, shortfall), payer.Wallet.Amount - keep);
        var recipient = carer; // quem cria o filho faz as compras da casa dele
        if (coin > 0 && payer.TryDebitWallet(new Money(coin)).IsSuccess)
        {
            recipient.CreditWallet(new Money(coin));
            sentCoin = coin;
        }

        if (sentFood == 0 && sentCoin == 0) return false;
        ctx.LogEvent(WorldEventKind.SupportPaid, $"{payer.Id.Value}|{recipient.Id.Value}|{sentFood}|{sentCoin}|{children}",
            sourceSystem: UnionSystem.SystemName);
        return true;
    }

    /// <summary>Preço do recurso na barraca mais barata da cidade de quem cria o filho; sem barraca, o piso.</summary>
    private static long PriceOf(WorldState world, Npc carer, ResourceType resource, long floor)
    {
        long best = long.MaxValue;
        foreach (var market in world.Workplaces)
            if (market.City == carer.City && market.Prices.TryGetValue(resource, out long price) && price < best) best = price;
        return Math.Max(1, best == long.MaxValue ? floor : Math.Max(floor, best));
    }
}
