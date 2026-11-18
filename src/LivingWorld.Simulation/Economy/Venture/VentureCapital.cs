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
        VenturePlan? plan = null, Credit.LoanPurpose purpose = Credit.LoanPurpose.Founding,
        LivingWorld.Domain.Population.Skills.SkillType? tradeSkill = null)
    {
        var spouse = founder.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true } s ? s : null;
        long free = Math.Max(0, founder.Wallet.Amount + (spouse?.Wallet.Amount ?? 0) - ReserveOf(world, rules, founder));
        long own = Math.Min(free, needed);
        var probe = world.VentureProbe;
        if (free >= needed) probe?.Mark("capital_no_casal");
        var credit = world.CreditOn ? world.EconomyRules.Credit : null;
        // AD-155 (E22): o fundador ambicioso e pouco medroso não imobiliza toda a poupança: guarda uma parte
        // (<c>(Ambição − Aversão)/100</c>, até a parte que se financia) e pede essa parte emprestada; sem credor, põe tudo
        long kept = 0;
        if (credit is not null && plan is not null && purpose == Credit.LoanPurpose.Founding)
        {
            double leverage = Math.Clamp((founder.Personality.Ambition - founder.Personality.RiskAversion) / 100.0, 0, credit.MaxLoanShare);
            kept = (long)Math.Floor(own * leverage);
            own -= kept;
        }

        var partners = new List<(Npc, long)>();
        long missing = needed - own;
        // quem tem moeda escolhe entre virar sócio (risco e parte do lucro) e emprestar (retorno fixo, a oficina em garantia);
        // primeiro entram os que preferem a sociedade (só na falta de verdade, não na parte que o fundador guardou), depois tenta o
        // empréstimo, e só então o fundador põe o que guardou e os outros viram sócios
        var financiers = missing > 0 && allowPartners ? PartnersOf(world, rules, founder).ToList() : [];
        if (financiers.Count > 0) probe?.Mark("procuraram_socio");
        var later = new List<Npc>();
        foreach (var f in financiers)
            if (credit is null || plan is null || PrefersPartnership(rules, founder, f, tradeSkill)) TakePartner(f, missing - kept); else later.Add(f);

        Credit.LoanOffer? loan = TryLoan();
        if (loan is null && missing > 0)
        {
            long back = Math.Min(kept, missing);
            own += back;
            missing -= back;
            foreach (var f in later) TakePartner(f, missing);
            loan = TryLoan();
        }

        var household = new List<(Npc, long)>();
        long fromFounder = Math.Min(own, founder.Wallet.Amount);
        if (fromFounder > 0) household.Add((founder, fromFounder));
        if (own - fromFounder > 0 && spouse is not null) household.Add((spouse, own - fromFounder));

        void TakePartner(Npc partner, long cap)
        {
            if (partners.Count >= rules.MaxPartners || missing <= 0 || cap <= 0) return;
            long offer = (long)Math.Floor(Math.Max(0, partner.Wallet.Amount - ReserveOf(world, rules, partner))
                * (1 - partner.Personality.RiskAversion / 100.0));
            long put = Math.Min(offer, Math.Min(missing, cap));
            if (put <= 0) return;
            partners.Add((partner, put));
            missing -= put;
        }

        // o que falta, se não passa da parte que se financia (menos para quem tem medo do risco), pede emprestado com a oficina em
        // garantia; a parcela tem de caber no ganho que a conta promete
        Credit.LoanOffer? TryLoan()
        {
            if (missing <= 0 || plan is null || credit is null) return null;
            long maxLoan = (long)Math.Floor(needed * credit.MaxLoanShare * Math.Clamp(1.5 - founder.Personality.RiskAversion / 100.0, 0, 1));
            int term = purpose == Credit.LoanPurpose.Founding ? credit.FoundingTermMonths : credit.WorkingTermMonths;
            probe?.Mark("pediram_credito");
            if (missing > maxLoan) { probe?.Refuse(Credit.LendRefusal.Ceiling); return null; }
            var why = probe is null ? null : new Credit.LendRefusal();
            // AD-159: o que a oficina rende à casa de quem abre é o lucro mais o salário dele mesmo na folha; e o prazo da abertura
            // cresce até o teto se a parcela do prazo curto não cabe
            long ownWage = (long)Math.Floor(plan.MonthlyPayroll / (double)Math.Max(1, rules.StartCrew) * credit.OwnWageInCapacityShare);
            long capacity = plan.MonthlySales - plan.MonthlyPayroll + ownWage;
            int maxTerm = purpose == Credit.LoanPurpose.Founding ? Math.Max(term, credit.MaxFoundingTermMonths) : term;
            Credit.LoanOffer? found = null;
            for (int t = term; found is null && t <= maxTerm; t += 12)
                found = Credit.Lending.Find(world, founder, missing, t, collateral: true, capacity, why);
            // o credor parente que também põe a parte dele (o cônjuge na parte do casal, o sócio) não empresta a mesma moeda duas
            // vezes: sem carteira para as duas, não há empréstimo (antes a conta passava e o débito da parte falhava na hora)
            if (found?.Lender is { } lender)
            {
                long committed = 0;
                foreach (var (p, put) in partners) if (p.Id == lender.Id) committed += put;
                if (spouse is not null && lender.Id == spouse.Id) committed += Math.Max(0, own + kept - founder.Wallet.Amount);
                if (lender.Wallet.Amount - committed < found.Amount) found = null;
            }
            if (found is not null) { missing = 0; probe?.Mark("credito_concedido"); }
            else if (why is not null) probe!.Refuse(why.Stage);
            return found;
        }

        if (partners.Count > 0) probe?.Mark("tiveram_socio");
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
            int grace = world.EconomyRules.Credit?.FoundingGraceMonths ?? 0; // AD-159: a oficina monta estoque antes da 1ª parcela
            var cal = world.CurrentDate.Calendar;
            Granted.FirstDueTick = ctx.CurrentTick + (long)Math.Max(0, grace) * cal.HoursPerDay * cal.DaysPerMonth;
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

    /// <summary>Sociedade ou empréstimo (AD-155, E22)? Quem financia quer ser sócio quanto menos teme o risco e se sabe o ofício (pode
    /// trabalhar e vigiar o negócio); o fundador ambicioso não quer dividir o lucro. Sociedade se
    /// <c>(1 − Aversão do financiador) + 0,25 se ele sabe o ofício − Ambição do fundador / 200</c> chega a
    /// <see cref="VentureRules.PartnershipAppeal"/>; senão ele prefere emprestar.</summary>
    internal static bool PrefersPartnership(VentureRules rules, Npc founder, Npc financier,
        LivingWorld.Domain.Population.Skills.SkillType? tradeSkill)
    {
        double appeal = 1 - financier.Personality.RiskAversion / 100.0 - founder.Personality.Ambition / 200.0;
        if (tradeSkill is { } skill && financier.Skills.Get(skill) >= rules.MinSkill) appeal += 0.25;
        return appeal >= rules.PartnershipAppeal;
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
