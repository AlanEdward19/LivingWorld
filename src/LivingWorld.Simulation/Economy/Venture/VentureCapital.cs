using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Venture;

/// <summary>De onde vem o dinheiro de quem abre (AD-152, fatia 5). Primeiro o do casal: a carteira do fundador e a do cônjuge acima
/// da reserva da casa (anos de salário guardado ou herança recebida, separada por <see cref="Npc.Inheritance"/>). O que falta pede
/// a sócios: parentes (pais, filhos, irmãos) e amigos que confiam nele (<see cref="VentureRules.MinPartnerTrust"/>, nos dois
/// sentidos), cada um com até <c>1 − Aversão/100</c> do que tem acima da própria reserva, até <see cref="VentureRules.MaxPartners"/>.
/// Não fecha a conta: não abre (empréstimo é a fatia 6 e entra aqui, depois dos sócios). Só calcula; quem debita é
/// <see cref="Take"/>.</summary>
internal sealed record VentureCapital(
    long Needed, Npc Founder, List<(Npc Npc, long Amount)> Household, List<(Npc Npc, long Amount)> Partners, Credit.LoanOffer? Loan = null)
{
    public long Own => Household.Sum(c => c.Amount);
    public long FromPartners => Partners.Sum(c => c.Amount);
    public long Borrowed => Loan?.Amount ?? 0;

    /// <summary>O contrato do empréstimo, depois de <see cref="Take"/> (a oficina em garantia entra quando nasce).</summary>
    public Credit.Loan? Granted { get; private set; }

    public static long ReserveOf(WorldState world, VentureRules rules, Npc npc)
    {
        long minWage = world.EconomyRules.WageByProfession.Count == 0 ? 0 : world.EconomyRules.WageByProfession.Values.Min();
        return (long)Math.Ceiling(rules.ReserveWageMonths * minWage * (0.5 + npc.Personality.RiskAversion / 100.0));
    }

    public static VentureCapital? Raise(WorldState world, VentureRules rules, Npc founder, long needed, bool allowPartners = true,
        VenturePlan? plan = null, Credit.LoanPurpose purpose = Credit.LoanPurpose.Founding)
    {
        var household = new List<(Npc, long)>();
        var spouse = founder.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true } s ? s : null;
        long free = Math.Max(0, founder.Wallet.Amount + (spouse?.Wallet.Amount ?? 0) - ReserveOf(world, rules, founder));
        long own = Math.Min(free, needed);
        long fromFounder = Math.Min(own, founder.Wallet.Amount);
        if (fromFounder > 0) household.Add((founder, fromFounder));
        if (own - fromFounder > 0 && spouse is not null) household.Add((spouse, own - fromFounder));

        var partners = new List<(Npc, long)>();
        long missing = needed - own;
        if (missing > 0 && allowPartners)
            foreach (var partner in PartnersOf(world, rules, founder))
            {
                if (partners.Count >= rules.MaxPartners || missing <= 0) break;
                long offer = (long)Math.Floor(Math.Max(0, partner.Wallet.Amount - ReserveOf(world, rules, partner))
                    * (1 - partner.Personality.RiskAversion / 100.0));
                long put = Math.Min(offer, missing);
                if (put <= 0) continue;
                partners.Add((partner, put));
                missing -= put;
            }

        // AD-155: o que falta, se não passa da parte que se financia (menos para quem tem medo do risco), pede emprestado com a
        // oficina em garantia; a parcela tem de caber no ganho que a conta promete
        Credit.LoanOffer? loan = null;
        if (missing > 0 && plan is not null && world.CreditOn)
        {
            var credit = world.EconomyRules.Credit!;
            long maxLoan = (long)Math.Floor(needed * credit.MaxLoanShare * Math.Clamp(1.5 - founder.Personality.RiskAversion / 100.0, 0, 1));
            int term = purpose == Credit.LoanPurpose.Founding ? credit.FoundingTermMonths : credit.WorkingTermMonths;
            if (missing <= maxLoan)
                loan = Credit.Lending.Find(world, founder, missing, term, collateral: true, plan.MonthlySales - plan.MonthlyPayroll);
            if (loan is not null) missing = 0;
        }

        return missing > 0 ? null : new VentureCapital(needed, founder, household, partners, loan);
    }

    /// <summary>Debita cada parte da carteira de quem a pôs (o empréstimo passa pela carteira do fundador); devolve quanto da parte do
    /// casal era herança (e a baixa no contador).</summary>
    public long Take(WorldState world, TickContext ctx, Credit.LoanPurpose purpose = Credit.LoanPurpose.Founding)
    {
        long inherited = 0;
        if (Loan is { } offer)
        {
            Granted = Credit.Lending.Grant(world, ctx, offer, Founder, purpose, workplace: 0, log: false);
            if (!Founder.TryDebitWallet(new Money(offer.Amount)).IsSuccess)
                throw new InvalidOperationException("VentureCapital: empréstimo não chegou à carteira");
        }

        foreach (var (npc, amount) in Household.Concat(Partners))
        {
            if (!npc.TryDebitWallet(new Money(amount)).IsSuccess)
                throw new InvalidOperationException("VentureCapital: carteira mudou entre a conta e o débito");
        }

        foreach (var (npc, amount) in Household)
        {
            long part = Math.Min(amount, npc.Inheritance);
            inherited += part;
            npc.SpendInheritance(part);
        }

        return inherited;
    }

    /// <summary>Parentes vivos e crescidos (pais, filhos, irmãos; o cônjuge já conta no casal) por id, depois amigos por id:
    /// confiança dele no amigo e do amigo nele acima do mínimo.</summary>
    private static IEnumerable<Npc> PartnersOf(WorldState world, VentureRules rules, Npc founder)
    {
        var kin = new List<Npc>();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive || npc.Id == founder.Id || npc.Id == founder.Spouse || !Grown(world, npc)) continue;
            bool parent = founder.MotherId == npc.Id || founder.FatherId == npc.Id;
            bool child = npc.MotherId == founder.Id || npc.FatherId == founder.Id;
            bool sibling = (founder.MotherId is { } m && npc.MotherId == m) || (founder.FatherId is { } f && npc.FatherId == f);
            if (parent || child || sibling) kin.Add(npc);
        }

        kin.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        foreach (var npc in kin) yield return npc;

        var targets = world.RelationshipTargetsOf(founder.Id);
        var relations = world.RelationshipsFrom(founder.Id);
        if (relations is null) yield break;
        for (int i = 0; i < targets.Count; i++)
        {
            if (relations[i].Trust < rules.MinPartnerTrust) continue;
            if (world.FindNpc(targets[i]) is not { IsAlive: true } friend || kin.Contains(friend) || friend.Id == founder.Spouse) continue;
            if (!Grown(world, friend)) continue;
            if (!world.Relationships.TryGetValue(new RelationshipKey(friend.Id, founder.Id), out var back) || back.Trust < rules.MinPartnerTrust) continue;
            yield return friend;
        }
    }

    private static bool Grown(WorldState world, Npc npc) =>
        world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Child;
}
