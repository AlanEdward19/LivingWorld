using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Crime;

/// <summary>Sequestro (AD-154). O motivo sai do estado: <b>vingança</b> (mágoa forte contra alguém: leva o filho pequeno dele
/// ou ele mesmo), <b>resgate</b> (o pobre de pouca moral leva a criança de uma família que tem moeda) e <b>posse</b> (quer alguém
/// que não o quer). A vítima fica presa na casa do sequestrador (célula real do mapa), fora da rotina, comendo e bebendo do
/// que a casa dele tem (sem nada, morre de fome ou sede, causa explícita). A família busca todo dia; desfechos: resgate pago
/// (moeda da família para o sequestrador, conservada), soltura, fuga, achada pela família (flagrante), morte. A justiça é a
/// comum: a vítima que volta sabe quem foi.</summary>
internal static class Kidnapping
{
    private static double Callousness(Personality p) => (1.5 - p.Agreeableness / 100.0) * (1.5 - p.Conscientiousness / 100.0);

    public static double CeilingPerYear(KidnappingRules rules, double maxBodyPow) => rules.PerYear * maxBodyPow * 1.5 * 1.5 * 2.25;

    /// <summary>Chance por ano de planejar um sequestro, o motivo e a vítima (nulo: sem motivo no estado, chance zero).</summary>
    public static double ChancePerYear(WorldState world, KidnappingRules rules, ViolenceRules violence, Npc npc, long now, double deterrence,
        out string motive, out Npc? victim, out long ransom)
    {
        motive = "";
        ransom = 0;
        victim = Vengeance(world, rules, npc, now, out double weight);
        if (victim is not null) motive = "vinganca";
        else if (CrimeSystem.IsPoor(world, npc) && (victim = RansomTarget(world, rules, npc, out ransom)) is not null) (motive, weight) = ("resgate", 1.0);
        else if ((victim = Desired(world, rules, npc)) is not null) (motive, weight) = ("posse", 1.0);
        if (victim is null) return 0;
        return rules.PerYear * weight * Violence.Propensity(world, violence, npc, now) * Callousness(npc.Personality) * Math.Max(0, 1 - deterrence);
    }

    /// <summary>A mágoa mais forte (acima do mínimo) contra alguém vivo da cidade: leva o filho pequeno da casa dele, ou ele.</summary>
    private static Npc? Vengeance(WorldState world, KidnappingRules rules, Npc npc, long now, out double weight)
    {
        weight = 0;
        var violence = world.CrimeRules!.Violence;
        int hpd = world.Calendar.HoursPerDay;
        Npc? target = null;
        foreach (var grudge in world.Crime!.Grudges)
        {
            if (grudge.Holder != npc.Id.Value) continue;
            int intensity = Violence.Intensity(violence, grudge, now, hpd);
            if (intensity < rules.VengeanceGrudgeMin || intensity / 100.0 <= weight) continue;
            if (world.FindNpc(new NpcId(grudge.Target)) is not { IsAlive: true } other || other.City != npc.City || Violence.Domestic(npc, other)) continue;
            (target, weight) = (other, intensity / 100.0);
        }
        if (target is null) return null;
        return YoungestChildAtHome(world, target) ?? target;
    }

    private static Npc? YoungestChildAtHome(WorldState world, Npc parent)
    {
        if (parent.Household is not { } id || world.FindHousehold(id) is not { } home) return null;
        Npc? youngest = null;
        foreach (var memberId in home.Members.OrderBy(m => m.Value))
            if (world.FindNpc(memberId) is { IsAlive: true } kid && (kid.MotherId == parent.Id || kid.FatherId == parent.Id)
                && world.LifeStageRules.LifeStageOf(kid.AgeYears(world.CurrentDate)) == LifeStage.Child
                && (youngest is null || kid.BirthDate.TotalHours > youngest.BirthDate.TotalHours))
                youngest = kid;
        return youngest;
    }

    /// <summary>Resgate: entre os conhecidos, a criança cuja família (pais, em moeda) é a mais rica, acima do mínimo.</summary>
    private static Npc? RansomTarget(WorldState world, KidnappingRules rules, Npc npc, out long ransom)
    {
        ransom = 0;
        Npc? best = null;
        long richest = rules.RansomMinFamilyCoin - 1;
        foreach (var id in world.RelationshipTargetsOf(npc.Id))
        {
            if (world.FindNpc(id) is not { IsAlive: true } contact || contact.City != npc.City || Violence.Domestic(npc, contact)) continue;
            var kid = world.LifeStageRules.LifeStageOf(contact.AgeYears(world.CurrentDate)) == LifeStage.Child ? contact : YoungestChildAtHome(world, contact);
            if (kid is null || kid.Id == npc.Id || Violence.Domestic(npc, kid)) continue;
            long coin = FamilyCoin(world, kid, npc);
            if (coin > richest) (best, richest) = (kid, coin);
        }
        if (best is not null) ransom = Math.Clamp((long)(richest * rules.RansomShare), 1, Math.Max(1, rules.RansomMaxCoin));
        return best;
    }

    /// <summary>Posse: a pessoa solteira do outro sexo, adulta, de quem ele gosta muito e que não gosta dele.</summary>
    private static Npc? Desired(WorldState world, KidnappingRules rules, Npc npc)
    {
        if (npc.Spouse is not null) return null;
        var targets = world.RelationshipTargetsOf(npc.Id);
        var bonds = world.RelationshipsFrom(npc.Id);
        if (bonds is null) return null;
        Npc? best = null;
        double strongest = rules.PossessionAffectionMin;
        for (int i = 0; i < targets.Count && i < bonds.Count; i++)
        {
            if (bonds[i].Affection < strongest) continue;
            if (world.FindNpc(targets[i]) is not { IsAlive: true } other || other.Sex == npc.Sex || other.Spouse is not null || other.City != npc.City) continue;
            if (world.LifeStageRules.LifeStageOf(other.AgeYears(world.CurrentDate)) == LifeStage.Child || Violence.Domestic(npc, other)) continue;
            double back = world.Relationships.TryGetValue(new RelationshipKey(other.Id, npc.Id), out var rel) ? rel.Affection : 0;
            if (back >= rules.PossessionRejectedBelow) continue;
            (best, strongest) = (other, bonds[i].Affection);
        }
        return best;
    }

    /// <summary>Quem paga pela vítima: pais e cônjuge vivos (fora o sequestrador), em ordem de id.</summary>
    private static List<Npc> Payers(WorldState world, Npc victim, Npc? offender)
    {
        var list = new List<Npc>();
        foreach (var id in new[] { victim.MotherId, victim.FatherId, victim.Spouse })
            if (id is { } v && world.FindNpc(v) is { IsAlive: true } who && who.Id != offender?.Id && !list.Contains(who)) list.Add(who);
        list.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        return list;
    }

    private static long FamilyCoin(WorldState world, Npc victim, Npc? offender) => Payers(world, victim, offender).Sum(p => p.Wallet.Amount);

    /// <summary>O sequestro na hora sorteada: quem estiver no lugar pode ver (caso aberto já); a vítima vai para a casa do
    /// sequestrador e fica presa.</summary>
    public static void Abduct(WorldState world, TickContext ctx, Npc offender, Npc victim, string motive, long ransom, WorldRng rng)
    {
        var ledger = world.Crime!;
        if (!offender.IsAlive || !victim.IsAlive || offender.City != victim.City || offender.Id == victim.Id) return;
        if (ledger.CaptivityOf(victim.Id.Value) is not null || ledger.CaptivityOf(offender.Id.Value) is not null) return;
        if (offender.Household is not { } h || world.FindHousehold(h) is not { } home || victim.Household == h) return;
        long now = ctx.CurrentTick;
        string category = world.LifeStageRules.LifeStageOf(victim.AgeYears(world.CurrentDate)) == LifeStage.Child ? "crianca" : "adulto";
        var outcome = Justice.OnCrime(world, ctx, CrimeKind.Kidnapping, offender, victim, victim.Id.Value, victim.CurrentLocation,
            ransom, category, motive, caught: false, rng);
        ledger.AddCaptivity(new Captivity
        {
            Victim = victim.Id.Value,
            Offender = offender.Id.Value,
            Motive = motive,
            StartTick = now,
            HoldX = home.Location.X,
            HoldY = home.Location.Y,
            Ransom = ransom,
            CaseId = outcome.CaseId,
        });
        victim.MoveTo(home.Location, now);
        victim.SetCurrentAction(ActionType.Sleep, now);
        ctx.LogEvent(WorldEventKind.Kidnapped,
            $"{offender.Id.Value}|{victim.Id.Value}|{motive}|{category}|{ransom}|{outcome.Seers}|{outcome.CaseId}", Theft.SourceName);
    }

    /// <summary>A hora de quem está preso (chamada pela decisão no lugar da rotina): fica na célula do cativeiro, deitado; com
    /// fome, sede ou sono urgentes, come e bebe do que a casa do sequestrador tem e dorme no chão. Falso para quem não está preso.</summary>
    public static bool Hold(WorldState world, Npc npc, long now)
    {
        if (world.Crime?.CaptivityOf(npc.Id.Value) is not { } held) return false;
        var cell = new CellCoord(held.HoldX, held.HoldY);
        if (npc.CurrentLocation != cell) npc.MoveTo(cell, now);
        if (npc.CurrentAction != ActionType.Sleep) npc.SetCurrentAction(ActionType.Sleep, now);
        int urgent = 100 - world.NeedsRules.UrgencyThreshold;
        if (npc.SleepAt(now) <= urgent) npc.SetSleep((int)(100 * world.RestPlaceCatalog.GroundEfficiency), now);
        bool hungry = npc.HungerAt(now) <= urgent, thirsty = npc.ThirstAt(now) <= urgent;
        if (!hungry && !thirsty) return true;
        if (world.FindNpc(new NpcId(held.Offender)) is not { Household: { } h } || world.FindHousehold(h) is not { } jail) return true;
        var economy = world.EconomyRules;
        var food = new ResourceType(economy.FoodResourceId);
        var water = new ResourceType(economy.WaterResourceId);
        if (hungry && jail.Withdraw(food, 1).IsSuccess)
        {
            npc.SetHunger(Math.Max(npc.HungerAt(now), ObjectUse.ConsumeLevel(world, food, ObjectEffectTargets.Hunger)), now);
            world.RecordResourceConsumed(food, 1);
        }
        if (thirsty && jail.Withdraw(water, 1).IsSuccess)
        {
            npc.SetThirst(Math.Max(npc.ThirstAt(now), ObjectUse.ConsumeLevel(world, water, ObjectEffectTargets.Thirst)), now);
            world.RecordResourceConsumed(water, 1);
        }
        return true;
    }

    /// <summary>Um dia de cada cativeiro, em ordem de vítima: morte (de fome ou sede, ou do sequestrador), sequestrador fora do
    /// mundo, fuga, busca da família, resgate pago e o prazo (soltura ou morte).</summary>
    public static void Daily(WorldState world, TickContext ctx, long day)
    {
        if (world.Crime!.Captives is not { } captives) return;
        var rules = world.CrimeRules!.Kidnapping!;
        long now = ctx.CurrentTick;
        int hpd = world.Calendar.HoursPerDay;
        foreach (var held in captives.ToList())
        {
            var rng = ctx.OneShot($"kidnap-day-{held.Victim}-{day}");
            var victim = world.FindNpc(new NpcId(held.Victim));
            var offender = world.FindNpc(new NpcId(held.Offender));
            int days = (int)((now - held.StartTick) / hpd);
            if (victim is not { IsAlive: true }) { End(world, ctx, held, victim, offender, "morte", 0, null, days, rng); continue; }
            if (offender is not { IsAlive: true }) { End(world, ctx, held, victim, offender, "autor_saiu", 0, null, days, rng); continue; }
            bool child = world.LifeStageRules.LifeStageOf(victim.AgeYears(world.CurrentDate)) == LifeStage.Child;
            double escape = rules.EscapePerDay * (1.5 - victim.Personality.RiskAversion / 100.0) * (child ? rules.ChildEscapeFactor : 1.0);
            if (rng.NextDouble() < escape) { End(world, ctx, held, victim, offender, "fuga", 0, null, days, rng); continue; }
            if (Search(world, rules, victim, offender, now, rng) is { } finder) { End(world, ctx, held, victim, offender, "achada", 0, finder, days, rng); continue; }
            if (held.Motive == "resgate" && days >= rules.RansomDemandAfterDays && rng.NextDouble() < rules.RansomPayPerDay
                && Pay(world, victim, offender, held.Ransom) is long paid and > 0)
            {
                End(world, ctx, held, victim, offender, "resgate", paid, null, days, rng);
                continue;
            }
            if (days < rules.MaxHoldDays) continue;
            double kill = rules.KillAtEndChance * Violence.Propensity(world, world.CrimeRules.Violence, offender, now)
                * (held.Motive == "vinganca" ? rules.VengeanceKillFactor : 1.0);
            End(world, ctx, held, victim, offender, rng.NextDouble() < kill ? "morte" : "soltura", 0, null, days, rng);
        }
    }

    /// <summary>Cada parente na cidade procura; quem desconfia do sequestrador (mágoa contra ele ou fama ruim) acha mais rápido.</summary>
    private static Npc? Search(WorldState world, KidnappingRules rules, Npc victim, Npc offender, long now, WorldRng rng)
    {
        bool suspect = Justice.InfamyOf(world, offender.Id.Value, now).All >= 30;
        foreach (var kin in Violence.KinOf(world, victim))
        {
            if (kin.Id == offender.Id || kin.City != victim.City) continue;
            bool knows = suspect || world.Crime!.GrudgeOf(offender.Id.Value, kin.Id.Value) is not null
                || world.Crime.GrudgeOf(kin.Id.Value, offender.Id.Value) is not null;
            if (rng.NextDouble() < rules.SearchFindPerDay * (knows ? rules.SuspectSearchFactor : 1.0)) return kin;
        }
        return null;
    }

    /// <summary>A família paga o que pode até o resgate pedido (só se juntos têm tudo); a moeda passa ao sequestrador.</summary>
    private static long Pay(WorldState world, Npc victim, Npc offender, long ransom)
    {
        var payers = Payers(world, victim, offender);
        if (ransom <= 0 || payers.Sum(p => p.Wallet.Amount) < ransom) return 0;
        long left = ransom;
        foreach (var payer in payers)
        {
            long part = Math.Min(left, payer.Wallet.Amount);
            if (part <= 0 || !payer.TryDebitWallet(new Money(part)).IsSuccess) continue;
            offender.CreditWallet(new Money(part));
            left -= part;
            if (left == 0) break;
        }
        return ransom - left;
    }

    /// <summary>Fim do cativeiro: a vítima volta para casa (ou morre), lembra; a família sente e quer vingança quando sabe quem
    /// foi; o caso abre (ou ganha a vítima e os parentes como quem sabe) para a justiça comum.</summary>
    private static void End(WorldState world, TickContext ctx, Captivity held, Npc? victim, Npc? offender, string outcome, long paid,
        Npc? finder, int days, WorldRng rng)
    {
        var ledger = world.Crime!;
        ledger.RemoveCaptivity(held);
        long now = ctx.CurrentTick;
        var rules = world.CrimeRules!;
        long caseId = held.CaseId;
        if (outcome == "morte" && victim is { IsAlive: true }) NpcDeath.Apply(world, ctx, victim, WorldEventKind.Homicide);
        bool alive = victim is { IsAlive: true };

        if (alive && offender is not null)
        {
            string category = world.LifeStageRules.LifeStageOf(victim!.AgeYears(world.CurrentDate)) == LifeStage.Child ? "crianca" : "adulto";
            if (caseId < 0 && offender.IsAlive)
                caseId = Justice.OnCrime(world, ctx, CrimeKind.Kidnapping, offender, victim, victim.Id.Value, victim.CurrentLocation,
                    paid, category, held.Motive, caught: finder is not null, rng, victimKnows: true).CaseId;
            Violence.AddGrudge(world, victim.Id.Value, offender.Id.Value, "sequestro", rules.Violence.AssaultGrudge, now);
            if (world.NeedsRules.Psyche.Enabled) PsycheMechanic.Experience(world, victim, LifeEventKind.Kidnapped, offender.Id.Value, now);
            if (victim.Household is { } h && world.FindHousehold(h) is { } home) victim.MoveTo(home.Location, now);
            victim.SetCurrentAction(ActionType.Idle, now);
        }

        // a família sabe quem foi quando a vítima volta ou quando alguém viu o sequestro
        var known = caseId >= 0 ? ledger.Cases.Find(c => c.Id == caseId) : null;
        if (known is not null && finder is not null) known.Caught = true;
        if (victim is not null && offender is not null && (alive || known is not null))
            foreach (var kin in Violence.KinOf(world, victim))
            {
                if (kin.Id == offender.Id) continue;
                Violence.AddGrudge(world, kin.Id.Value, offender.Id.Value, "vinganca",
                    outcome == "morte" ? rules.Violence.KinKilledGrudge : rules.Kidnapping!.KinGrudge, now);
                if (world.NeedsRules.Psyche.Enabled)
                    PsycheMechanic.Experience(world, kin, outcome == "morte" ? LifeEventKind.KinMurdered : LifeEventKind.KinAssaulted, offender.Id.Value, now);
                if (known is not null && !known.Knowers.Exists(k => k.Npc == kin.Id.Value))
                    known.Knowers.Add(new CaseKnower { Npc = kin.Id.Value, Accused = offender.Id.Value, Value = paid });
            }
        if (alive && known is not null && !known.Knowers.Exists(k => k.Npc == held.Victim))
            known.Knowers.Add(new CaseKnower { Npc = held.Victim, Accused = held.Offender, Value = paid });

        ctx.LogEvent(WorldEventKind.KidnappingEnded, $"{held.Victim}|{held.Offender}|{outcome}|{days}|{paid}|{caseId}", Theft.SourceName);
    }
}
