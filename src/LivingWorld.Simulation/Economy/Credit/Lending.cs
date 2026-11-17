using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Venture;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Credit;

/// <summary>Uma proposta de empréstimo ainda não executada: quem empresta (pessoa ou tesouro da cidade), quanto, por quanto tempo e
/// com que acréscimo. <see cref="Due"/> é o que se devolve; <see cref="Installment"/>, a parcela do mês.</summary>
internal sealed record LoanOffer(Npc? Lender, City? City, long Amount, double AnnualCharge, int TermMonths, long ChargeCap, string Kind = "cidade")
{
    public long Charge => (long)Math.Ceiling(Amount * AnnualCharge * TermMonths / 12.0);
    public long Due => Amount + Charge;
    public long Installment => (long)Math.Ceiling(Due / (double)Math.Max(1, TermMonths));
}

/// <summary>Quem empresta e como (AD-155). Ninguém tem lista: quem tem moeda acima da própria reserva decide pelo próprio estado. A
/// ordem é a da vida: primeiro os parentes crescidos (pais, filhos, irmãos; sem acréscimo), depois os conhecidos que confiam no
/// tomador, depois o rico da cidade e por fim o tesouro da cidade, estes dois só com a oficina em garantia. Cada um empresta até
/// <c>1 − Aversão/100</c> do que tem acima da reserva e só se a confiança (mais parentesco, garantia e o favor que deve ao tomador,
/// o eixo Dívida) passa do mínimo, se a parcela cabe no ganho que a conta promete e se o tomador não tem calote recente nem má fama.</summary>
/// <summary>AD-159, só para a sonda: o motivo do credor que chegou mais perto de emprestar (índice em
/// <see cref="Observation.VentureProbe.Refusals"/>).</summary>
internal sealed class LendRefusal
{
    public const int NoLender = 0, Infamy = 1, NoMoney = 2, Aversion = 3, Distrust = 4, Installment = 5, Ceiling = 6;
    public int Stage { get; private set; }
    public void Reach(int stage) { if (stage > Stage) Stage = stage; }
}

internal static class Lending
{
    public static LoanOffer? Find(WorldState world, Npc borrower, long amount, int termMonths, bool collateral, long monthlyCapacity,
        LendRefusal? why = null)
    {
        if (!world.CreditOn || amount <= 0 || termMonths <= 0 || monthlyCapacity <= 0) return null;
        var rules = world.EconomyRules.Credit!;
        var venture = world.EconomyRules.Venture!;
        long now = world.CurrentDate.TotalHours;
        long memory = now - (long)rules.DefaultMemoryYears * world.Calendar.HoursPerYear;
        var book = world.Credit; // só lê: sem livro não há calote
        bool badDebtor = book?.DefaultedSince(borrower.Id.Value, memory) == true;
        bool infamous = LivingWorld.Simulation.Crime.Justice.ReputationOf(world, borrower.Id.Value, now) > rules.MaxInfamy; // AD-178: a linhagem pesa
        double maxInstallment = rules.MaxInstallmentShare * monthlyCapacity;

        LoanOffer? From(Npc lender, bool kin, string kind)
        {
            if (badDebtor && !(kin && book?.DefaultedSince(borrower.Id.Value, memory, exceptAgainst: lender.Id.Value) == false)) { why?.Reach(LendRefusal.Infamy); return null; }
            if (infamous && !kin) { why?.Reach(LendRefusal.Infamy); return null; }
            long free = lender.Wallet.Amount - VentureCapital.ReserveOf(world, venture, lender);
            if (free < amount) { why?.Reach(LendRefusal.NoMoney); return null; }
            if (Math.Floor(free * (1 - lender.Personality.RiskAversion / 100.0)) < amount) { why?.Reach(LendRefusal.Aversion); return null; }
            double trust = (kin ? rules.KinTrust : 0) + (collateral ? rules.CollateralTrust : 0);
            if (world.Relationships.TryGetValue(new RelationshipKey(lender.Id, borrower.Id), out var bond)) trust += bond.Trust + bond.Debt;
            if (trust < rules.MinLenderTrust) { why?.Reach(LendRefusal.Distrust); return null; }
            var offer = Offer(rules, lender, null, amount, kin ? rules.KinAnnualCharge : rules.AnnualCharge, termMonths) with { Kind = kind };
            if (offer.Installment <= maxInstallment) return offer;
            why?.Reach(LendRefusal.Installment);
            return null;
        }

        var kin = Kin(world, borrower);
        foreach (var lender in kin)
            if (From(lender, kin: true, "parente") is { } offer) return offer;

        var targets = world.RelationshipTargetsOf(borrower.Id);
        var known = new List<Npc>();
        for (int i = 0; i < targets.Count; i++)
            if (world.FindNpc(targets[i]) is { IsAlive: true } npc && npc.City == borrower.City && Eligible(world, borrower, npc) && !kin.Contains(npc))
                known.Add(npc);
        known.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        foreach (var lender in known)
            if (From(lender, kin: false, "conhecido") is { } offer) return offer;

        if (!collateral || badDebtor || infamous) { why?.Reach(badDebtor || infamous ? LendRefusal.Infamy : LendRefusal.NoLender); return null; }
        var rich = new List<Npc>();
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.City == borrower.City && npc.Wallet.Amount > amount && Eligible(world, borrower, npc) && !kin.Contains(npc) && !known.Contains(npc))
                rich.Add(npc);
        rich.Sort((a, b) => b.Wallet.Amount != a.Wallet.Amount ? b.Wallet.Amount.CompareTo(a.Wallet.Amount) : a.Id.Value.CompareTo(b.Id.Value));
        foreach (var lender in rich)
            if (From(lender, kin: false, "rico") is { } offer) return offer;

        if (world.FindCity(borrower.City) is { } city && Math.Floor(city.Treasury.Amount * rules.CityLendShare) >= amount)
        {
            var offer = Offer(rules, null, city, amount, rules.AnnualCharge, termMonths);
            if (offer.Installment <= maxInstallment) return offer;
            why?.Reach(LendRefusal.Installment);
        }
        else why?.Reach(LendRefusal.NoMoney);

        return null;
    }

    private static LoanOffer Offer(CreditRules rules, Npc? lender, City? city, long amount, double charge, int term) =>
        new(lender, city, amount, charge, term,
            (long)Math.Ceiling(amount * Math.Max(charge, rules.MaxAnnualCharge) * Math.Max(1, term) / 12.0));

    /// <summary>Executa a proposta: a moeda sai do credor e entra na carteira do tomador (nada se cria); o contrato vai ao livro; o
    /// tomador fica grato e em dívida de favor com o credor (evento de ajuda: confiança, afeto e o eixo Dívida sobem).</summary>
    public static Loan Grant(WorldState world, TickContext ctx, LoanOffer offer, Npc borrower, LoanPurpose purpose, long workplace,
        bool log = true)
    {
        if (offer.Lender is { } lender)
        {
            if (!lender.TryDebitWallet(new Money(offer.Amount)).IsSuccess)
                throw new InvalidOperationException("Lending: carteira do credor mudou entre a proposta e a concessão");
        }
        else if (!offer.City!.TryDebitTreasury(new Money(offer.Amount)).IsSuccess)
            throw new InvalidOperationException("Lending: tesouro mudou entre a proposta e a concessão");
        else world.RecordTreasury(offer.City.Id, ctx.CurrentTick, revenue: 0, spending: offer.Amount);

        borrower.CreditWallet(new Money(offer.Amount));
        var loan = world.CreditBook.Add(new Loan
        {
            Lender = offer.Lender?.Id.Value ?? -1,
            LenderCity = offer.City?.Id.Value ?? Guid.Empty,
            Borrower = borrower.Id.Value,
            Workplace = workplace,
            Purpose = purpose,
            Principal = offer.Amount,
            Balance = offer.Due,
            Installment = offer.Installment,
            Charged = offer.Charge,
            ChargeCap = Math.Max(offer.Charge, offer.ChargeCap),
            StartTick = ctx.CurrentTick,
        });
        if (offer.Lender is { } l)
        {
            var gratitude = world.GetOrCreateRelationship(new RelationshipKey(borrower.Id, l.Id), ctx.CurrentTick);
            gratitude.ApplyEvent(RelationshipEventType.Help, world.FamilyRules);
            gratitude.MarkContact(ctx.CurrentTick);
        }

        if (log) LogGrant(ctx, loan, offer.TermMonths, offer.Kind);
        return loan;
    }

    public static void LogGrant(TickContext ctx, Loan loan, int termMonths, string kind) =>
        ctx.LogEvent(WorldEventKind.LoanGranted,
            $"{loan.Id}|{loan.LenderTag}|{kind}|{loan.Borrower}|{PurposeTag(loan.Purpose)}|{loan.Principal}|{loan.Balance}|{loan.Installment}|{termMonths}|{loan.Workplace}",
            sourceSystem: WorkingCapitalSystem.SystemName);

    public static string PurposeTag(LoanPurpose purpose) => purpose switch
    {
        LoanPurpose.Founding => "abertura",
        LoanPurpose.Working => "giro",
        LoanPurpose.Food => "comida",
        _ => "reabertura",
    };

    /// <summary>Parentes vivos e crescidos (pais, filhos, irmãos; o cônjuge é do casal), por id.</summary>
    internal static List<Npc> Kin(WorldState world, Npc npc)
    {
        var kin = new List<Npc>();
        foreach (var other in world.AliveNpcIndex.Alive)
        {
            if (!Eligible(world, npc, other)) continue;
            bool parent = npc.MotherId == other.Id || npc.FatherId == other.Id;
            bool child = other.MotherId == npc.Id || other.FatherId == npc.Id;
            bool sibling = (npc.MotherId is { } m && other.MotherId == m) || (npc.FatherId is { } f && other.FatherId == f);
            if (parent || child || sibling) kin.Add(other);
        }

        kin.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        return kin;
    }

    private static bool Eligible(WorldState world, Npc borrower, Npc other) =>
        other.IsAlive && other.Id != borrower.Id && other.Id != borrower.Spouse
        && world.LifeStageRules.LifeStageOf(other.AgeYears(world.CurrentDate)) != LifeStage.Child;
}
