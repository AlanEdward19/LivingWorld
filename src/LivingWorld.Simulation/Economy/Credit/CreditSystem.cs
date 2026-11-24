using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Credit;

/// <summary>O mês do crédito (AD-155), <c>Monthly</c>, depois da folha e antes da retirada do lucro (a dívida vem antes do lucro).
/// (1) O salário atrasado é pago com o caixa que sobra acima da próxima folha. (2) Cada parcela sai do caixa da oficina dada em
/// garantia e, faltando, da carteira do devedor; parcela incompleta é atraso, com multa até o teto da
/// usura; <see cref="CreditRules.DefaultAfterMissedMonths"/> meses seguidos é calote: com a oficina em garantia, ela vai à falência;
/// sem garantia, o credor penhora a carteira e perde o resto. Crédito de quem saiu do mundo passa aos herdeiros; dívida de quem saiu
/// segue a oficina (o herdeiro que ficou com ela) ou vira calote. (3) A oficina de dono cuja folha não fecha
/// <see cref="CreditRules.BankruptcyMonths"/> meses seguidos fecha de verdade (<see cref="Bankruptcy"/>). Consumidores: a moeda do
/// credor (que gasta, empresta de novo, deixa de herança), a fama do mau pagador (ninguém empresta a ele), a mágoa do credor
/// (briga) e o emprego dos que ficam livres.</summary>
[LivingCapability("CREDIT", Events = [WorldEventKind.LoanRepaid, WorldEventKind.LoanDefaulted, WorldEventKind.WorkplaceBankrupt],
    Consumers = ["timeline.wages", "inspector.employment"])]
[RunsAfter(typeof(WagePaymentSystem))]
public sealed class CreditSystem : ISimulationSystem
{
    public const string SystemName = "economy-credit";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CreditOn) return;
        var rules = world.EconomyRules.Credit!;
        if (world.Credit is { } book)
        {
            PayArrears(world, book);
            foreach (var loan in book.Loans.OrderBy(l => l.Id).ToList())
            {
                if (!book.Loans.Contains(loan)) continue; // saiu numa falência deste mês
                Debts.ReassignClaim(world, loan);
                if (world.FindNpc(new NpcId(loan.Borrower)) is { IsAlive: true } borrower)
                {
                    if (ctx.CurrentTick >= loan.FirstDueTick) Collect(world, ctx, rules, loan, borrower); // na carência não se cobra
                }
                else BorrowerGone(world, ctx, loan);
            }
        }

        foreach (var shop in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (shop.Owner is null || shop.Closed) continue;
            if (!shop.PayrollFailed) { world.Credit?.SetDistress(shop.Id.Value, 0); continue; }
            int months = (world.Credit?.DistressOf(shop.Id.Value) ?? 0) + 1;
            if (months >= rules.BankruptcyMonths) Bankruptcy.Liquidate(world, ctx, shop);
            else world.CreditBook.SetDistress(shop.Id.Value, months);
        }
    }

    private static void Collect(WorldState world, TickContext ctx, CreditRules rules, Loan loan, LivingWorld.Domain.Population.Npc borrower)
    {
        long due = Math.Min(loan.Installment, loan.Balance);
        long paid = 0;
        var shop = loan.Workplace > 0 ? world.FindWorkplace(new WorkplaceId(loan.Workplace)) : null;
        if (shop is { Closed: false } && shop.Owner == borrower.Id)
        {
            long fromShop = Math.Min(due, shop.Treasury.Amount);
            if (fromShop > 0 && shop.TryDebitTreasury(new Money(fromShop)).IsSuccess) paid += fromShop;
        }

        long fromWallet = Math.Min(due - paid, borrower.Wallet.Amount); // a parcela cabe no salário (ver Lending); a penhora é que deixa o mínimo
        if (fromWallet > 0 && borrower.TryDebitWallet(new Money(fromWallet)).IsSuccess) paid += fromWallet;
        Debts.PayLender(world, ctx, loan, paid);

        if (loan.Balance <= 0) { Debts.Repaid(world, ctx, loan); return; }
        if (paid >= due) { loan.Missed = 0; return; }

        loan.Missed++;
        long penalty = Math.Min((long)Math.Ceiling((due - paid) * rules.LatePenaltyShare), Math.Max(0, loan.ChargeCap - loan.Charged));
        loan.Balance += penalty;
        loan.Charged += penalty;
        if (loan.Missed >= rules.DefaultAfterMissedMonths) Default(world, ctx, rules, loan, borrower, shop);
    }

    /// <summary>Calote. Oficina em garantia que ainda trabalha: falência. Oficina já fechada que ainda é dele: o credor fica com o
    /// prédio. Sem garantia: penhora da carteira acima do que se deixa ao devedor; o resto se perde.</summary>
    private static void Default(WorldState world, TickContext ctx, CreditRules rules, Loan loan, LivingWorld.Domain.Population.Npc borrower,
        Workplace? shop)
    {
        if (shop is not null && shop.Owner == borrower.Id)
        {
            if (!shop.Closed) { Bankruptcy.Liquidate(world, ctx, shop); return; }
            long value = Debts.Seize(world, loan, shop);
            if (loan.Balance <= 0) { Debts.Repaid(world, ctx, loan); return; }
            loan.Workplace = 0;
            Debts.WriteOff(world, ctx, loan, value, "oficina");
            return;
        }

        long seize = Math.Min(loan.Balance, Math.Max(0, borrower.Wallet.Amount - Debts.Exempt(world, rules)));
        if (seize > 0 && borrower.TryDebitWallet(new Money(seize)).IsSuccess) Debts.PayLender(world, ctx, loan, seize);
        else seize = 0;
        if (loan.Balance <= 0) { Debts.Repaid(world, ctx, loan); return; }
        Debts.WriteOff(world, ctx, loan, seize, "penhora");
    }

    /// <summary>O devedor morreu (o espólio já pagou o que pôde da dívida pessoal) ou deixou o mundo. A dívida com a oficina em
    /// garantia passa a quem ficou com a oficina; sem isso, o credor fica com o prédio que era dele e perde o resto.</summary>
    private static void BorrowerGone(WorldState world, TickContext ctx, Loan loan)
    {
        var shop = loan.Workplace > 0 ? world.FindWorkplace(new WorkplaceId(loan.Workplace)) : null;
        if (shop?.Owner is { } owner && owner.Value != loan.Borrower && world.FindNpc(owner) is { IsAlive: true })
        {
            loan.Borrower = owner.Value;
            return;
        }

        long value = 0;
        if (shop is not null && shop.Owner?.Value == loan.Borrower)
        {
            if (!shop.Closed)
            {
                foreach (var id in shop.Employees.ToList())
                {
                    shop.Fire(id);
                    if (world.FindNpc(id) is { IsAlive: true } npc && npc.Employer == shop.Id) npc.Fire();
                }
            }

            value = Debts.Seize(world, loan, shop);
        }

        if (loan.Balance <= 0) { Debts.Repaid(world, ctx, loan); return; }
        Debts.WriteOff(world, ctx, loan, value, shop is null ? "partiu" : "oficina", markDebtor: false);
    }

    /// <summary>Salário atrasado das oficinas de dono, pago com o caixa acima da próxima folha, por oficina e id.</summary>
    private static void PayArrears(WorldState world, CreditLedger book)
    {
        if (book.Arrears.Count == 0) return;
        foreach (var group in book.Arrears.GroupBy(a => a.Workplace).OrderBy(g => g.Key).ToList())
        {
            var shop = world.FindWorkplace(new WorkplaceId(group.Key));
            if (shop is null || shop.Closed) continue;
            long spare = shop.Treasury.Amount - Payroll.Due(world, shop).Sum(d => d.Wage.Amount);
            foreach (var arrear in group.OrderBy(a => a.Npc))
            {
                if (world.FindNpc(new NpcId(arrear.Npc)) is not { IsAlive: true } worker) { book.Arrears.Remove(arrear); continue; }
                long pay = Math.Min(arrear.Owed, spare);
                if (pay <= 0 || !shop.TryDebitTreasury(new Money(pay)).IsSuccess) continue;
                worker.CreditWallet(new Money(pay));
                spare -= pay;
                arrear.Owed -= pay;
                if (arrear.Owed <= 0) book.Arrears.Remove(arrear);
            }
        }
    }
}
