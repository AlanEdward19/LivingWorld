using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Credit;

/// <summary>Dívida no espólio (AD-155): os credores recebem antes dos herdeiros. A dívida pessoal do morto sai da carteira dele antes da
/// partilha; o que a carteira não cobre se perde (o herdeiro não responde além do espólio). A dívida com a oficina em garantia segue a
/// oficina: quem a herda passa a dever (<see cref="CreditSystem"/>, no mês). O crédito do morto passa aos herdeiros dele no mês
/// (<see cref="Debts.ReassignClaim"/>), pela mesma norma do espólio.</summary>
internal static class CreditEstate
{
    public static void PayDebts(WorldState world, TickContext ctx, Npc dead)
    {
        if (!world.CreditOn || world.Credit is not { } book) return;
        foreach (var loan in book.Loans.Where(l => l.Borrower == dead.Id.Value).OrderBy(l => l.Id).ToList())
        {
            if (loan.Workplace > 0 && world.FindWorkplace(new WorkplaceId(loan.Workplace)) is { Closed: false } shop && shop.Owner == dead.Id)
                continue; // segue a oficina
            long pay = Math.Min(Math.Max(0, loan.Balance), dead.Wallet.Amount);
            if (pay > 0 && dead.TryDebitWallet(new Money(pay)).IsSuccess) Debts.PayLender(world, ctx, loan, pay);
            else pay = 0;
            if (loan.Balance <= 0) Debts.Repaid(world, ctx, loan);
            else Debts.WriteOff(world, ctx, loan, pay, "espolio", markDebtor: false);
        }
    }
}
