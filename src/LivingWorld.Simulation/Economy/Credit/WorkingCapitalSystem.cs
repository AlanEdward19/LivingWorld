using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Venture;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Credit;

/// <summary>Capital de giro (AD-155), <c>Monthly</c>, antes da folha. A oficina de dono cujo caixa não cobre a folha do mês: (1) o dono
/// põe do bolso o que o casal tem acima da reserva da casa; (2) se o negócio tem freguesia (a venda que a conta promete passa da folha),
/// ele pede emprestado alguns meses de folha, com a oficina em garantia (<see cref="Lending"/>: parente, conhecido, o rico da vila, o
/// tesouro da cidade); (3) o que ainda falta ele corta: demite os últimos que entraram, nunca ele mesmo, até a folha caber no caixa.
/// (4) A casa sem comida e sem dinheiro para o mês pede emprestado a parente ou conhecido (<see cref="FoodCredit"/>).
/// A oficina sem dono é da cidade, que a cobre (<see cref="OwnerDrawSystem"/>). Consumidores: a folha paga (<see cref="WagePaymentSystem"/>),
/// a parcela que o <see cref="CreditSystem"/> cobra e o desempregado que procura trabalho (<see cref="EmploymentSystem"/>).</summary>
[LivingCapability("CREDIT", Events = [WorldEventKind.LoanGranted], Consumers = ["timeline.wages", "inspector.employment"])]
[RunsAfter(typeof(MarketPricingSystem))]
public sealed class WorkingCapitalSystem(LivingWorld.Domain.Population.Skills.SkillsRules skillsRules) : ISimulationSystem
{
    public const string SystemName = "economy-working-capital";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CreditOn) return;
        var rules = world.EconomyRules.Credit!;
        var venture = world.EconomyRules.Venture!;
        foreach (var shop in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (shop.Closed || shop.Owner is not { } ownerId || world.FindNpc(ownerId) is not { IsAlive: true } owner) continue;
            long due = Payroll.Due(world, shop).Sum(d => d.Wage.Amount);
            long gap = due - shop.Treasury.Amount;
            if (due <= 0 || gap <= 0) continue;

            gap -= Inject(world, venture, owner, shop, gap);
            if (gap > 0) gap -= Borrow(world, ctx, rules, venture, owner, shop, due);
            if (gap > 0) LayOff(world, ctx, shop, owner);
        }

        FoodCredit.Run(world, ctx); // a casa sem comida e sem dinheiro pede a parente ou conhecido
    }

    /// <summary>O dono põe do bolso (dele e do cônjuge) o que passa da reserva da casa, até a falta.</summary>
    private static long Inject(WorldState world, VentureRules venture, Npc owner, Workplace shop, long gap)
    {
        var spouse = owner.Spouse is { } s && world.FindNpc(s) is { IsAlive: true } sp ? sp : null;
        long free = owner.Wallet.Amount + (spouse?.Wallet.Amount ?? 0) - VentureCapital.ReserveOf(world, venture, owner);
        long put = Math.Min(gap, Math.Max(0, free));
        if (put <= 0) return 0;
        long fromOwner = Math.Min(put, owner.Wallet.Amount);
        if (fromOwner > 0 && !owner.TryDebitWallet(new Money(fromOwner)).IsSuccess) return 0;
        long fromSpouse = put - fromOwner;
        if (fromSpouse > 0 && !spouse!.TryDebitWallet(new Money(fromSpouse)).IsSuccess) { put = fromOwner; }
        shop.CreditTreasury(new Money(put));
        return put;
    }

    /// <summary>Empréstimo de giro: só quem tem freguesia (venda prometida maior que a folha), um por vez por oficina; o que entra vai
    /// ao caixa. Devolve quanto da falta ele cobriu.</summary>
    private long Borrow(WorldState world, TickContext ctx, CreditRules rules, VentureRules venture, Npc owner, Workplace shop, long due)
    {
        if (world.Credit?.Loans.Any(l => l.Workplace == shop.Id.Value && l.Purpose == LoanPurpose.Working) == true) return 0;
        if (VentureTrade.Of(world, skillsRules, shop.LocationType.Id) is not { } trade) return 0;
        var plan = VenturePlan.For(world, venture, trade, shop.City, self: shop);
        long capacity = plan.MonthlySales - due;
        long amount = Math.Max(due - shop.Treasury.Amount, (long)Math.Ceiling(rules.WorkingLoanPayrollMonths * due) - shop.Treasury.Amount);
        if (capacity <= 0 || Lending.Find(world, owner, amount, rules.WorkingTermMonths, collateral: true, capacity) is not { } offer) return 0;
        Lending.Grant(world, ctx, offer, owner, LoanPurpose.Working, shop.Id.Value);
        if (!owner.TryDebitWallet(new Money(offer.Amount)).IsSuccess) return 0;
        shop.CreditTreasury(new Money(offer.Amount));
        return offer.Amount;
    }

    /// <summary>Demite os que entraram por último (o fim da lista), nunca o dono, até a folha caber no caixa.</summary>
    private static void LayOff(WorldState world, TickContext ctx, Workplace shop, Npc owner)
    {
        while (true)
        {
            var due = Payroll.Due(world, shop);
            if (due.Sum(d => d.Wage.Amount) <= shop.Treasury.Amount) return;
            NpcId? last = null;
            for (int i = shop.Employees.Count - 1; i >= 0; i--)
                if (shop.Employees[i] != owner.Id && due.Any(d => d.Npc.Id == shop.Employees[i])) { last = shop.Employees[i]; break; }
            if (last is not { } id) return;
            shop.Fire(id);
            if (world.FindNpc(id) is { } npc && npc.Employer == shop.Id) npc.Fire();
            ctx.LogEvent(WorldEventKind.Fired, $"{id.Value}|{shop.Id.Value}", sourceSystem: SystemName);
        }
    }
}
