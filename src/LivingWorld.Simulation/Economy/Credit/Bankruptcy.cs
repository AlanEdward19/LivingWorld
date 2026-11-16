using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Credit;

/// <summary>A falência de uma oficina de dono (AD-155, sobra E19): fecha de verdade. A gente fica livre; o estoque é vendido a quem usa
/// ou vende o bem na cidade, ao preço de piso, até o caixa de cada comprador. AD-162: o credor com penhor leva o bem penhorado (o
/// primeiro empréstimo com a oficina em garantia, por id, fica com o prédio e o valor dele abate a dívida); o caixa é dividido em
/// rateio proporcional ao devido entre todos os credores que sobram, salário atrasado junto, sem preferência (estatuto inglês de 1543,
/// <i>pari passu</i>; a preferência do salário é do século XIX); a sobra vai ao dono e aos sócios pela cota; o que ainda se deve fica
/// com o dono e os sócios na proporção das cotas, como dívida pessoal. A oficina fica fechada (<see cref="Workplace.Closed"/>) até
/// quem ficou com ela reabrir ou vendê-la a quem abre.</summary>
internal static class Bankruptcy
{
    public static void Liquidate(WorldState world, TickContext ctx, Workplace shop)
    {
        var book = world.CreditBook;
        long ownerId = shop.Owner?.Value ?? -1;
        var partners = shop.Partners?.ToList() ?? [];

        int freed = 0;
        foreach (var id in shop.Employees.OrderBy(e => e.Value).ToList())
        {
            shop.Fire(id);
            if (world.FindNpc(id) is not { IsAlive: true } npc || npc.Employer != shop.Id) continue;
            npc.Fire();
            freed++;
            ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{shop.Id.Value}", sourceSystem: CreditSystem.SystemName);
        }

        SellStock(world, shop);
        long assets = shop.Treasury.Amount;
        long toCreditors = 0;

        var secured = book.Loans.Where(l => l.Workplace == shop.Id.Value).OrderBy(l => l.Id).ToList();
        string keeper = ownerId < 0 ? $"city-{shop.City.Value}" : $"npc-{ownerId}";
        if (secured.FirstOrDefault(l => l.Balance > 0) is { } first)
        {
            toCreditors += Debts.Seize(world, first, shop);
            keeper = first.LenderTag;
            if (ownerId >= 0) book.MarkDefault(ownerId, first.Lender, ctx.CurrentTick,
                ctx.CurrentTick - (long)world.EconomyRules.Credit!.DefaultMemoryYears * world.Calendar.HoursPerYear);
        }

        toCreditors += ShareProRata(world, ctx, shop, secured);
        ShareLeftover(world, ctx, shop, ownerId, partners);

        long remaining = 0;
        foreach (var loan in secured)
        {
            if (loan.Balance <= 0) { Debts.Repaid(world, ctx, loan); continue; }
            remaining += loan.Balance;
            loan.Workplace = 0;
            loan.Missed = 0;
            if (loan.Borrower != ownerId) continue; // tomado por quem não é o dono: segue com ele
            Debts.Split(world, loan, partners.Where(p => world.FindNpc(p.Npc) is { IsAlive: true }).Select(p => (p.Npc.Value, p.Basis)).ToList());
        }

        shop.SetClosed(true);
        book.SetDistress(shop.Id.Value, 0);
        ctx.LogEvent(WorldEventKind.WorkplaceBankrupt,
            $"{shop.Id.Value}|{ownerId}|{assets}|{toCreditors}|{remaining}|{freed}|{keeper}", sourceSystem: CreditSystem.SystemName);
    }

    /// <summary>O caixa em rateio proporcional ao devido (AD-162): salário atrasado desta oficina e saldo dos empréstimos com ela em
    /// garantia, depois do prédio, todos na mesma fila. Quem morreu não recebe salário (a parte fica no caixa e volta ao dono); o salário
    /// que não coube se perde, o saldo do empréstimo segue como dívida.</summary>
    private static long ShareProRata(WorldState world, TickContext ctx, Workplace shop, List<Loan> secured)
    {
        var book = world.CreditBook;
        var owed = book.Arrears.Where(a => a.Workplace == shop.Id.Value).ToList();
        var workers = owed.Select(a => world.FindNpc(new NpcId(a.Npc)) is { IsAlive: true } w ? w : null).ToList();
        long total = 0;
        for (int i = 0; i < owed.Count; i++) if (workers[i] is not null) total += owed[i].Owed;
        foreach (var loan in secured) total += Math.Max(0, loan.Balance);
        long cash = Math.Min(shop.Treasury.Amount, total);
        long paid = 0;
        for (int i = 0; i < owed.Count; i++)
        {
            long part = total == 0 || workers[i] is null ? 0 : owed[i].Owed * cash / total;
            if (part > 0 && shop.TryDebitTreasury(new Money(part)).IsSuccess)
            {
                workers[i]!.CreditWallet(new Money(part));
                paid += part;
            }

            book.Arrears.Remove(owed[i]);
        }

        foreach (var loan in secured)
        {
            long part = total == 0 ? 0 : Math.Max(0, loan.Balance) * cash / total;
            if (part <= 0 || !shop.TryDebitTreasury(new Money(part)).IsSuccess) continue;
            Debts.PayLender(world, ctx, loan, part);
            paid += part;
        }

        return paid;
    }

    /// <summary>O que sobrou no caixa depois dos credores: a cota de cada sócio vivo, o resto ao dono vivo, senão à cidade.</summary>
    private static void ShareLeftover(WorldState world, TickContext ctx, Workplace shop, long ownerId,
        List<WorkplaceStake> partners)
    {
        long cash = shop.Treasury.Amount;
        if (cash <= 0 || !shop.TryDebitTreasury(new Money(cash)).IsSuccess) return;
        long rest = cash;
        foreach (var stake in partners)
        {
            long part = cash * stake.Basis / WorkplaceStake.Whole;
            if (part <= 0 || world.FindNpc(stake.Npc) is not { IsAlive: true } partner) continue;
            partner.CreditWallet(new Money(part));
            rest -= part;
        }

        if (world.FindNpc(new NpcId(ownerId)) is { IsAlive: true } owner) owner.CreditWallet(new Money(rest));
        else if (world.FindCity(shop.City) is { } city)
        {
            city.CreditTreasury(new Money(rest));
            world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: rest, spending: 0);
        }
        else shop.CreditTreasury(new Money(rest));
    }

    /// <summary>O estoque vai a quem trabalha na cidade e usa ou vende o bem (insumo, produto ou mercado), ao preço de piso, até o
    /// caixa e o espaço de cada um, por id. O que ninguém compra fica no prédio, com quem ficar com ele.</summary>
    private static void SellStock(WorldState world, Workplace shop)
    {
        var economy = world.EconomyRules;
        var catalog = world.EconomyCatalog;
        foreach (var (resource, units0) in shop.Stock.OrderBy(s => s.Key.Id).ToList())
        {
            long units = units0;
            if (units <= 0) continue;
            long price = Math.Max(1, economy.PriceFloor.GetValueOrDefault(resource.Id, 1));
            foreach (var buyer in world.Workplaces.OrderBy(w => w.Id.Value))
            {
                if (units <= 0) break;
                if (buyer == shop || buyer.Closed || buyer.City != shop.City || buyer.Employees.Count == 0) continue;
                bool uses = catalog.MarketLocationTypeIds.Contains(buyer.LocationType.Id)
                    || (catalog.Recipes.TryGetValue(buyer.LocationType.Id, out var recipe)
                        && (recipe.Inputs.ContainsKey(resource.Id) || recipe.Outputs.ContainsKey(resource.Id)));
                if (!uses) continue;
                long capacity = economy.CapacityOf(resource, buyer.LocationType);
                long room = capacity == long.MaxValue ? long.MaxValue : Math.Max(0, capacity - buyer.Stock.GetValueOrDefault(resource));
                long take = Math.Min(units, Math.Min(room, buyer.Treasury.Amount / price));
                if (take <= 0 || !shop.Withdraw(resource, take).IsSuccess) continue;
                if (!buyer.TryDebitTreasury(new Money(take * price)).IsSuccess)
                {
                    shop.Deposit(resource, take, economy);
                    continue;
                }

                buyer.Deposit(resource, take, economy);
                shop.CreditTreasury(new Money(take * price));
                units -= take;
            }
        }
    }
}
