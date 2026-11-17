using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Lifecycle;

/// <summary>Espólio (estate-inheritance): na morte, a carteira do morto passa aos herdeiros em ordem fixa (cônjuge vivo,
/// depois filhos, depois pais, depois irmãos; sem ninguém, ao tesouro da cidade), em partes iguais com o resto ao de menor
/// id. A despensa do domicílio que fica sem ninguém vai ao domicílio do primeiro herdeiro (em vez de se perder) e a oficina
/// de que ele era dono passa ao herdeiro de menor id presente nela (ou, se nenhum está lá, ao de menor id). Nada é criado
/// nem destruído: o saldo do mundo é o mesmo antes e depois da morte. Chamado por <see cref="NpcDeath"/>, uma vez por morte.
/// AD-150 (achado 30), com <c>CrimeRules</c> ligado: a norma do cenário reparte — o cônjuge fica com a sua parte (a terça da
/// viúva) e os filhos com o resto em partes iguais; a oficina vai ao filho homem mais velho; a desigualdade e o temperamento
/// fazem herdeiros brigarem (<see cref="WorldEventKind.InheritanceDisputed"/>, mágoa que pode virar agressão).</summary>
public static class EstateSystem
{
    public const string SystemName = "EstateSystem";

    public static void Settle(WorldState world, TickContext ctx, Npc dead)
    {
        LivingWorld.Simulation.Population.Family.UnionSystem.OnDeath(world, dead); // luto de filho afasta o casal (AD-136)
        LivingWorld.Simulation.Economy.Credit.CreditEstate.PayDebts(world, ctx, dead); // AD-155: credores antes dos herdeiros
        long wallet = dead.Wallet.Amount;
        var owned = OwnedWorkplaces(world, dead);
        var household = dead.Household is { } householdId ? world.FindHousehold(householdId) : null;
        bool lastInHousehold = household is { } h && h.Members.Count == 1 && h.Members[0] == dead.Id;
        long stockUnits = lastInHousehold ? household!.Stock.Values.Sum() : 0;
        if (wallet <= 0 && stockUnits <= 0 && owned.Count == 0) return;

        var norm = world.CrimeOn ? world.CrimeRules!.Inheritance : null;
        var heirs = norm is null ? HeirsOf(world, dead) : NormHeirs(world, dead);
        long toCity = 0;
        long[] shares = [];
        wallet -= NamesakeLegacy(world, dead, heirs, wallet);
        if (wallet > 0)
        {
            if (heirs.Count > 0) shares = PayHeirs(dead, heirs, wallet, norm, noteInheritance: world.EconomyRules.Venture is not null);
            else toCity = PayCity(world, dead, wallet);
        }

        long movedStock = stockUnits > 0 && heirs.Count > 0 ? MoveStock(world, household!, heirs) : 0;
        var son = norm is { WorkplaceToEldestSon: true } && owned.Count > 0 ? EldestSon(dead, heirs) : null;
        int transferred = TransferWorkplaces(world, owned, son is null ? heirs : [son]);
        foreach (var heir in heirs)
        {
            var relationship = world.GetOrCreateRelationship(new RelationshipKey(dead.Id, heir.Id), world.CurrentDate.TotalHours);
            relationship.ApplyEvent(RelationshipEventType.Inheritance, world.FamilyRules);
            relationship.MarkContact(world.CurrentDate.TotalHours);
        }

        ctx.LogEvent(
            WorldEventKind.EstateSettled,
            $"{dead.Id.Value}|{string.Join(',', heirs.Select(x => x.Id.Value))}|{(heirs.Count > 0 ? wallet : toCity)}|{movedStock}|{transferred}",
            sourceSystem: SystemName);
        if (norm is not null && heirs.Count >= 2) Disputes(world, ctx, dead, heirs, shares, son, wallet, norm);
    }

    /// <summary>Herdeiros pela norma (AD-150): cônjuge vivo e filhos vivos juntos (cônjuge primeiro, filhos por id); sem
    /// nenhum dos dois, pais e depois irmãos, como antes.</summary>
    internal static List<Npc> NormHeirs(WorldState world, Npc dead)
    {
        var heirs = new List<Npc>();
        if (dead.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true } spouse) heirs.Add(spouse);
        foreach (var child in world.AliveNpcIndex.Alive)
            if (child.MotherId == dead.Id || child.FatherId == dead.Id) heirs.Add(child);
        return heirs.Count > 0 ? heirs : HeirsOf(world, dead);
    }

    private static Npc? EldestSon(Npc dead, List<Npc> heirs)
    {
        Npc? eldest = null;
        foreach (var heir in heirs)
            if ((heir.MotherId == dead.Id || heir.FatherId == dead.Id) && heir.Sex == Sex.Male
                && (eldest is null || heir.BirthDate.TotalHours < eldest.BirthDate.TotalHours))
                eldest = heir;
        return eldest;
    }

    /// <summary>Disputa (AD-150): cada par de herdeiros adultos que não é o casal pode brigar, mais quanto mais desigual a
    /// partilha (a oficina conta como a parte maior) e quanto menos agradáveis os dois; quem ficou com menos guarda mais mágoa.</summary>
    private static void Disputes(WorldState world, TickContext ctx, Npc dead, List<Npc> heirs, long[] shares, Npc? son, long wallet,
        LivingWorld.Domain.Crime.InheritanceRules norm)
    {
        if (norm.DisputeChance <= 0 || (wallet < norm.DisputeMinValue && son is null)) return;
        var rng = ctx.OneShot($"estate-dispute-{dead.Id.Value}");
        long top = Math.Max(1, shares.Length == 0 ? 1 : shares.Max());
        for (int i = 0; i < heirs.Count; i++)
            for (int j = i + 1; j < heirs.Count; j++)
            {
                Npc a = heirs[i], b = heirs[j];
                if (a.Spouse == b.Id || IsChild(world, a) || IsChild(world, b)) continue;
                long sa = i < shares.Length ? shares[i] : 0, sb = j < shares.Length ? shares[j] : 0;
                double unequal = Math.Abs(sa - sb) / (double)top + (son is not null && (son == a || son == b) ? 1.0 : 0.0);
                double temper = (2.0 - (a.Personality.Agreeableness + b.Personality.Agreeableness) / 100.0) / 2.0;
                if (rng.NextDouble() >= norm.DisputeChance * temper * (0.25 + unequal)) continue;
                bool aLost = sa < sb || (son is not null && son == b);
                LivingWorld.Simulation.Crime.Violence.AddGrudge(world, a.Id.Value, b.Id.Value, "heranca",
                    aLost ? norm.DisputeGrudge : norm.DisputeGrudge / 2, ctx.CurrentTick);
                LivingWorld.Simulation.Crime.Violence.AddGrudge(world, b.Id.Value, a.Id.Value, "heranca",
                    aLost ? norm.DisputeGrudge / 2 : norm.DisputeGrudge, ctx.CurrentTick);
                ctx.LogEvent(WorldEventKind.InheritanceDisputed, $"{a.Id.Value}|{b.Id.Value}|{dead.Id.Value}|{wallet}", SystemName);
            }
    }

    private static bool IsChild(WorldState world, Npc npc) =>
        world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Child;

    /// <summary>Herdeiros vivos, no primeiro grau que tem alguém, por id crescente (determinístico).</summary>
    internal static List<Npc> HeirsOf(WorldState world, Npc dead)
    {
        if (dead.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true } spouse)
            return [spouse];

        var children = world.Npcs.Where(n => n.IsAlive && (n.MotherId == dead.Id || n.FatherId == dead.Id)).OrderBy(n => n.Id.Value).ToList();
        if (children.Count > 0) return children;

        var parents = new List<Npc>();
        foreach (var parentId in new[] { dead.MotherId, dead.FatherId })
            if (parentId is { } id && world.FindNpc(id) is { IsAlive: true } parent) parents.Add(parent);
        if (parents.Count > 0) return parents.OrderBy(n => n.Id.Value).ToList();

        return world.Npcs
            .Where(n => n.IsAlive && n.Id != dead.Id
                && ((dead.MotherId is { } m && n.MotherId == m) || (dead.FatherId is { } f && n.FatherId == f)))
            .OrderBy(n => n.Id.Value).ToList();
    }

    /// <summary>AD-178: o homenageado deixa ao xará vivo que não é herdeiro (o neto que leva o seu nome) uma fração da carteira
    /// antes da partilha; o de menor id, se houver mais de um. A moeda só muda de carteira. Devolve quanto saiu.</summary>
    private static long NamesakeLegacy(WorldState world, Npc dead, List<Npc> heirs, long wallet)
    {
        double share = world.FamilyRules.Names.NamesakeLegacyShare;
        long legacy = (long)(wallet * Math.Clamp(share, 0, 1));
        if (legacy <= 0) return 0;
        Npc? namesake = null;
        foreach (var other in world.AliveNpcIndex.Alive)
            if ((namesake is null || other.Id.Value < namesake.Id.Value) && !heirs.Contains(other)
                && LivingWorld.Simulation.Population.Naming.NameBonds.IsNamesake(world, other, dead))
                namesake = other;
        if (namesake is null || !dead.TryDebitWallet(new Money(legacy)).IsSuccess) return 0;
        namesake.CreditWallet(new Money(legacy));
        if (world.EconomyRules.Venture is not null) namesake.NoteInheritance(legacy);
        return legacy;
    }

    /// <summary>Partes iguais com o resto ao primeiro; pela norma (AD-150), o cônjuge (primeiro da lista) fica com a sua parte
    /// e os filhos repartem o resto. Devolve a parte de cada herdeiro, na ordem da lista.</summary>
    private static long[] PayHeirs(Npc dead, List<Npc> heirs, long wallet, LivingWorld.Domain.Crime.InheritanceRules? norm,
        bool noteInheritance)
    {
        _ = dead.TryDebitWallet(new Money(wallet));
        var shares = new long[heirs.Count];
        bool spouseFirst = norm is not null && heirs.Count > 1 && dead.Spouse == heirs[0].Id;
        int first = spouseFirst ? 1 : 0;
        if (spouseFirst) shares[0] = (long)(wallet * Math.Clamp(norm!.SpouseShare, 0, 1));
        long rest = wallet - shares[0] * first;
        int count = heirs.Count - first;
        long share = rest / count;
        long remainder = rest - share * count;
        for (int i = first; i < heirs.Count; i++)
            shares[i] = share + (i == first ? remainder : 0);
        for (int i = 0; i < heirs.Count; i++)
        {
            if (shares[i] <= 0) continue;
            heirs[i].CreditWallet(new Money(shares[i]));
            if (noteInheritance) heirs[i].NoteInheritance(shares[i]); // AD-152: origem do capital
        }

        return shares;
    }

    private static long PayCity(WorldState world, Npc dead, long wallet)
    {
        if (world.FindCity(dead.City) is not { } city) return 0; // sem cidade: o dinheiro fica com o morto, como antes

        _ = dead.TryDebitWallet(new Money(wallet));
        city.CreditTreasury(new Money(wallet));
        world.RecordTreasury(city.Id, world.CurrentDate.TotalHours, revenue: wallet, spending: 0);
        return wallet;
    }

    private static long MoveStock(WorldState world, Household from, List<Npc> heirs)
    {
        var target = heirs.Select(h => h.Household is { } id ? world.FindHousehold(id) : null).FirstOrDefault(x => x is not null && x.Id != from.Id);
        if (target is null) return 0;

        long moved = 0;
        foreach (var (resource, amount) in from.Stock.ToList())
        {
            if (amount <= 0 || !from.Withdraw(resource, amount).IsSuccess) continue;
            target.Deposit(resource, amount);
            moved += amount;
        }

        return moved;
    }

    private static List<Workplace> OwnedWorkplaces(WorldState world, Npc dead) =>
        world.Workplaces.Where(w => w.Owner == dead.Id).ToList();

    private static int TransferWorkplaces(WorldState world, List<Workplace> owned, List<Npc> heirs)
    {
        foreach (var workplace in owned)
        {
            var heir = heirs.FirstOrDefault(h => h.CurrentLocation == workplace.Location) ?? heirs.FirstOrDefault()
                ?? Journeyman(world, workplace);
            workplace.AssignOwner(heir?.Id);
        }

        return heirs.Count > 0 ? owned.Count : 0;
    }

    /// <summary>Sem herdeiro, a oficina passa ao oficial mais hábil que trabalha nela (AD-144), como o mestre que deixa a
    /// oficina ao melhor aprendiz; ninguém lá, fica sem dono (da cidade). Empate: o mais velho, depois o menor id.</summary>
    internal static Npc? Journeyman(WorldState world, Workplace workplace)
    {
        var skills = LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultSkillsRules;
        Npc? best = null;
        double bestSkill = -1;
        foreach (var id in workplace.Employees.OrderBy(e => e.Value))
        {
            if (world.FindNpc(id) is not { IsAlive: true } npc) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Child) continue;
            double skill = skills.SkillByProfession.TryGetValue(npc.Profession.Id, out var type) ? npc.Skills.Get(type) : 0;
            if (skill > bestSkill || (skill == bestSkill && best is not null && npc.BirthDate.TotalHours < best.BirthDate.TotalHours))
            {
                best = npc;
                bestSkill = skill;
            }
        }

        return best;
    }
}
