using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Credit;

/// <summary>Empréstimo para comer (AD-155): o crédito de aldeia mais comum era o de consumo, no ano ruim ou no fim do salário. A casa
/// cuja despensa tem menos de <see cref="LivingWorld.Domain.Economy.CreditRules.FoodLoanPantryDays"/> dias de comida por pessoa e
/// cujo dinheiro não compra <see cref="LivingWorld.Domain.Economy.CreditRules.FoodLoanDays"/> dias dela pede a falta ao preço de quem vende comida na cidade. Quem pede é o adulto da casa
/// com o maior salário (sem salário não há de onde sair a parcela); empresta só parente ou conhecido que confia (sem garantia).
/// A moeda vai à carteira dele, e a casa compra pelo caminho de sempre.</summary>
internal static class FoodCredit
{
    public static void Run(WorldState world, TickContext ctx)
    {
        var rules = world.EconomyRules.Credit!;
        var economy = world.EconomyRules;
        var food = new ResourceType(economy.FoodResourceId);
        int days = rules.FoodLoanDays;
        foreach (var house in world.Households.OrderBy(h => h.Id.Value))
        {
            if (house.PendingRelocationCity is not null) continue;
            long cash = 0;
            int members = 0;
            Npc? earner = null;
            long wage = 0;
            foreach (var id in house.Members)
            {
                if (world.FindNpc(id) is not { IsAlive: true } m) continue;
                members++;
                cash += m.Wallet.Amount;
                if (m.Employer is not { } job || world.FindWorkplace(job) is not { PayrollFailed: false }) continue;
                long w = economy.WageByProfession.GetValueOrDefault(m.Profession.Id);
                if (w > wage || (w == wage && earner is not null && m.Id.Value < earner.Id.Value)) { wage = w; earner = m; }
            }

            if (earner is null || members == 0 || house.Stock.GetValueOrDefault(food) >= (long)members * rules.FoodLoanPantryDays) continue;
            long month = (long)members * days * PriceOf(world, house.City, food);
            long need = month - cash;
            if (need <= 0) continue;
            if (world.Credit?.Loans.Any(l => l.Borrower == earner.Id.Value && l.Purpose == LoanPurpose.Food) == true) continue;
            if (Lending.Find(world, earner, need, rules.FoodTermMonths, collateral: false, wage) is not { } offer) continue;
            Lending.Grant(world, ctx, offer, earner, LoanPurpose.Food, workplace: 0);
        }
    }

    /// <summary>O preço da comida em quem a vende na cidade (o mais barato com preço), senão o piso.</summary>
    private static long PriceOf(WorldState world, LivingWorld.Domain.Shared.CityId city, ResourceType food)
    {
        long best = long.MaxValue;
        foreach (var w in world.Workplaces)
            if (w.City == city && !w.Closed && w.Prices.TryGetValue(food, out var p) && p > 0 && p < best) best = p;
        return best != long.MaxValue ? best : Math.Max(1, world.EconomyRules.PriceFloor.GetValueOrDefault(food.Id, 1));
    }
}
