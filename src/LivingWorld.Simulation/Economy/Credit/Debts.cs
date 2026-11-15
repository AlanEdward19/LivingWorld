using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Credit;

/// <summary>Movimentos de um contrato (AD-155): pagar ao credor, passar o crédito do credor que saiu do mundo aos herdeiros, repartir
/// a dívida entre dono e sócios, executar a garantia e dar baixa no calote. A moeda só muda de dono; o saldo é direito, não moeda.</summary>
internal static class Debts
{
    public static long MinWage(WorldState world) =>
        world.EconomyRules.WageByProfession.Count == 0 ? 0 : world.EconomyRules.WageByProfession.Values.Min();

    /// <summary>O que a penhora deixa na carteira do devedor.</summary>
    public static long Exempt(WorldState world, CreditRules rules) =>
        (long)Math.Ceiling(rules.DistraintExemptWageMonths * MinWage(world));

    /// <summary>Paga <paramref name="amount"/> (já tirado do devedor) ao credor: carteira dele ou tesouro da cidade.</summary>
    public static void PayLender(WorldState world, TickContext ctx, Loan loan, long amount)
    {
        if (amount <= 0) return;
        loan.Balance -= amount;
        loan.Paid += amount;
        if (!loan.FromCity && world.FindNpc(new NpcId(loan.Lender)) is { IsAlive: true } lender)
        {
            lender.CreditWallet(new Money(amount));
            return;
        }

        var city = world.FindCity(new CityId(loan.LenderCity))
            ?? (world.FindNpc(new NpcId(loan.Borrower)) is { } b ? world.FindCity(b.City) : null);
        if (city is null) throw new InvalidOperationException("Debts: credor sem destino para a moeda");
        city.CreditTreasury(new Money(amount));
        world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: amount, spending: 0);
    }

    /// <summary>O credor morreu ou deixou o mundo: o crédito passa aos herdeiros dele (pela norma do espólio, em partes iguais, a sobra
    /// ao primeiro); sem herdeiro, ao tesouro da cidade dele.</summary>
    public static void ReassignClaim(WorldState world, Loan loan)
    {
        if (loan.FromCity || world.FindNpc(new NpcId(loan.Lender)) is not { IsAlive: false } gone) return;
        var heirs = (world.CrimeOn ? EstateSystem.NormHeirs(world, gone) : EstateSystem.HeirsOf(world, gone))
            .Where(h => h.IsAlive && h.Id.Value != loan.Borrower).ToList();
        if (heirs.Count == 0)
        {
            loan.Lender = -1;
            loan.LenderCity = gone.City.Value;
            return;
        }

        var parts = Split(world, loan, heirs.Skip(1).Select(h => (h.Id.Value, WorkplaceStake.Whole / heirs.Count)).ToList());
        loan.Lender = heirs[0].Id.Value;
        for (int i = 0; i < parts.Count; i++)
        {
            parts[i].Lender = parts[i].Borrower; // a parte é do herdeiro como credor; o devedor segue o mesmo, com a mesma garantia
            parts[i].Borrower = loan.Borrower;
            parts[i].Workplace = loan.Workplace;
        }
    }

    /// <summary>Tira de <paramref name="loan"/> uma parte por cota (décimos de milésimo) em contratos novos, com saldo, parcela,
    /// principal e acréscimo na proporção; o original fica com o resto. <paramref name="borrowers"/> diz quem vira devedor de cada
    /// parte (ou, para o credor, o chamador ajusta).</summary>
    public static List<Loan> Split(WorldState world, Loan loan, List<(long Npc, int Basis)> borrowers)
    {
        var parts = new List<Loan>();
        var (principal, balance, installment, charged, cap) = (loan.Principal, loan.Balance, loan.Installment, loan.Charged, loan.ChargeCap);
        foreach (var (npc, basis) in borrowers)
        {
            long Part(long v) => v * basis / WorkplaceStake.Whole; // sobre o contrato inteiro, não sobre o que sobrou
            var part = new Loan
            {
                Lender = loan.Lender,
                LenderCity = loan.LenderCity,
                Borrower = npc,
                Workplace = 0,
                Purpose = loan.Purpose,
                Principal = Part(principal),
                Balance = Part(balance),
                Installment = Math.Max(1, Part(installment)),
                Charged = Part(charged),
                ChargeCap = Part(cap),
                StartTick = loan.StartTick,
                Missed = loan.Missed,
            };
            if (part.Balance <= 0) continue;
            loan.Principal -= part.Principal;
            loan.Balance -= part.Balance;
            loan.Installment = Math.Max(1, loan.Installment - part.Installment);
            loan.Charged -= part.Charged;
            loan.ChargeCap -= part.ChargeCap;
            parts.Add(world.CreditBook.Add(part));
        }

        return parts;
    }

    /// <summary>Calote: o credor perde o saldo; o devedor ganha a fama de mau pagador; o credor deixa de confiar e de gostar dele
    /// (traição, confiança a zero) e, com a justiça ligada, guarda mágoa (a dívida vira motivo de briga).</summary>
    public static void WriteOff(WorldState world, TickContext ctx, Loan loan, long seized, string how, bool markDebtor = true)
    {
        var rules = world.EconomyRules.Credit!;
        var book = world.CreditBook;
        long lost = Math.Max(0, loan.Balance);
        book.Loans.Remove(loan);
        long now = ctx.CurrentTick;
        if (markDebtor)
            book.MarkDefault(loan.Borrower, loan.Lender, now, now - (long)rules.DefaultMemoryYears * world.Calendar.HoursPerYear);
        if (!loan.FromCity && world.FindNpc(new NpcId(loan.Lender)) is { IsAlive: true } lender
            && world.FindNpc(new NpcId(loan.Borrower)) is { IsAlive: true } debtor)
        {
            var bond = world.GetOrCreateRelationship(new RelationshipKey(lender.Id, debtor.Id), now);
            bond.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
            bond.CoolTo(affectionCeiling: Math.Min(bond.Affection, 10), trustCeiling: 0);
            bond.MarkContact(now);
            LivingWorld.Simulation.Crime.Violence.AddGrudge(world, lender.Id.Value, debtor.Id.Value, "divida", rules.DefaultGrudge, now);
        }

        ctx.LogEvent(WorldEventKind.LoanDefaulted, $"{loan.Id}|{loan.Borrower}|{loan.LenderTag}|{lost}|{seized}|{how}",
            sourceSystem: CreditSystem.SystemName);
    }

    public static void Repaid(WorldState world, TickContext ctx, Loan loan)
    {
        world.CreditBook.Loans.Remove(loan);
        if (!loan.FromCity && world.FindNpc(new NpcId(loan.Lender)) is { IsAlive: true } lender
            && world.FindNpc(new NpcId(loan.Borrower)) is { IsAlive: true } debtor)
        {
            var bond = world.GetOrCreateRelationship(new RelationshipKey(lender.Id, debtor.Id), ctx.CurrentTick);
            bond.ApplyEvent(RelationshipEventType.Trade, world.FamilyRules); // pagou o que devia: palavra cumprida
            bond.MarkContact(ctx.CurrentTick);
        }

        ctx.LogEvent(WorldEventKind.LoanRepaid, $"{loan.Id}|{loan.Borrower}|{loan.LenderTag}|{loan.Paid}", sourceSystem: CreditSystem.SystemName);
    }

    /// <summary>O que a oficina vale para quem a toma em pagamento: o foro do lote, a obra e o equipamento, como na abertura.</summary>
    public static long ShopValue(WorldState world, Workplace shop)
    {
        var venture = world.EconomyRules.Venture!;
        long wage = 0;
        foreach (var (profession, type) in world.EconomyCatalog.LocationTypeByProfession.OrderBy(p => p.Key))
            if (type == shop.LocationType.Id) { wage = world.EconomyRules.WageByProfession.GetValueOrDefault(profession); break; }
        long daily = Math.Max(1, MinWage(world) / world.CurrentDate.Calendar.DaysPerMonth);
        long equipment = venture.EquipmentResourceId == 0 ? 0
            : venture.EquipmentUnits * Math.Max(1, world.EconomyRules.PriceFloor.GetValueOrDefault(venture.EquipmentResourceId, 1));
        return (long)Math.Ceiling(venture.PlotFeeWageMonths * wage) + venture.BuildPersonDays * daily + equipment;
    }

    /// <summary>A garantia executada: o prédio passa ao credor (ou à cidade), fechado, e o valor dele abate o saldo.</summary>
    public static long Seize(WorldState world, Loan loan, Workplace shop)
    {
        long value = Math.Min(Math.Max(0, loan.Balance), ShopValue(world, shop));
        loan.Balance -= value;
        loan.Paid += value;
        shop.AssignOwner(loan.FromCity ? null : new NpcId(loan.Lender));
        shop.SetPartners([]);
        shop.SetClosed(true);
        return value;
    }
}
