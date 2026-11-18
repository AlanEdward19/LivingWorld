using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ontogeny;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Acolhimento de órfãos, <c>Monthly</c> (dívida da Fase 21): criança sem adulto nem idoso vivo no domicílio é levada por um adulto
/// — parente primeiro (avós, irmão adulto, tios), depois quem tem laço de afeto com ela, depois a comunidade da própria cidade. Antes só havia
/// parente próximo no momento da morte; sem ele a criança ficava num domicílio unitário e morria de sede. Consumidor causal: o domicílio
/// acolhedor passa a alimentá-la e a ser o cuidador de <see cref="ExposureAccumulator"/> (idioma e desenvolvimento). AD-186: diário,
/// irmãos juntos e quem acolhe escolhido por laço, meios e generosidade (<see cref="Choose"/>).</summary>
[LivingCapability("FAMILY", Events = [WorldEventKind.OrphanTakenIn], Consumers = ["inspector.npc.household", "timeline.family"])]
[RunsAfter(typeof(NatalitySystem))]
public sealed class OrphanCareSystem : ISimulationSystem
{
    public const string SystemName = "population-orphan-care";

    /// <summary>Afeto mínimo (0–100) para um adulto contar como alguém que a criança já conhece e em quem confia.</summary>
    private const int MinimumAffection = 50;

    public string Name => SystemName;
    /// <summary>AD-186: diário (a casa que fica só com crianças é vista no mesmo dia; antes, no fim do mês, e a de 2 anos morria de
    /// fome com moeda em casa). No modo de antes (<see cref="Domain.Ontogeny.OntogenyRules.OrphanCareLegacy"/>) só o 1º dia do mês.</summary>
    public TickFrequency Frequency => TickFrequency.Daily;

    /// <summary>Peso do laço de quem acolhe (AD-186): parente, quem gosta da criança (× afeto), quem gostava do pai ou da mãe
    /// (× afeto), a cidade. Calibração sem fonte medida; o costume das fontes (parentes primeiro, depois padrinhos e vizinhos,
    /// a paróquia por último) está em docs/domain/names.md.</summary>
    private const double KinBond = 1.0, ChildBond = 0.8, ParentBond = 0.6, CityBond = 0.15;

    /// <summary>Meses de salário mínimo por boca com que os meios da casa contam a metade (AD-186).</summary>
    private const double MeansHalfAtWageMonths = 6;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.OntogenyRules.OrphanCare) return;
        bool legacy = world.OntogenyRules.OrphanCareLegacy;
        var calendar = world.Calendar;
        if (legacy && ctx.CurrentTick / calendar.HoursPerDay % calendar.DaysPerMonth != 0) return;
        var rules = world.LifeStageRules;
        var now = world.CurrentDate;
        List<Npc>? orphans = null;
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive || rules.LifeStageOf(npc.AgeYears(now)) != LifeStage.Child) continue;
            bool covered = npc.Household is { } id && world.FindHousehold(id) is { } home
                && HouseholdRedistribution.HasLivingAdultOrElder(world, home, rules, now);
            if (!covered) (orphans ??= []).Add(npc);
        }
        if (orphans is null) return;
        orphans.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value)); // ordena só quem precisa, não o mundo todo

        var adults = world.Npcs
            .Where(n => n.IsAlive && n.Household is not null && rules.LifeStageOf(n.AgeYears(now)) is LifeStage.Adult or LifeStage.Elder)
            .OrderBy(n => n.Id.Value)
            .ToList();

        if (legacy)
        {
            foreach (var child in orphans)
                TryTakeIn(world, ctx, child, adults);
            return;
        }

        // AD-186: os irmãos da mesma casa vão juntos para quem acolhe (o primeiro de cada casa decide pelo grupo)
        var done = new HashSet<long>();
        foreach (var child in orphans)
        {
            if (!done.Add(child.Id.Value) || !child.IsAlive) continue;
            var group = new List<Npc> { child };
            if (child.Household is { } hid && world.FindHousehold(hid) is { } old)
                foreach (var other in orphans)
                    if (other.Id != child.Id && other.Household == hid && done.Add(other.Id.Value)) group.Add(other);
            if (Choose(world, group, adults, rules, now) is not var (caregiver, bond)) continue;
            foreach (var member in group) Move(world, ctx, member, caregiver, bond, detach: true);
        }
    }

    private static void Move(WorldState world, TickContext ctx, Npc child, Npc caregiver, string bond, bool detach)
    {
        var now = world.CurrentDate;
        var target = world.FindHousehold(caregiver.Household!.Value)!;
        if (detach && child.Household is { } oldId && world.FindHousehold(oldId) is { } old)
        {
            old.RemoveMember(child.Id);
            child.LeaveHousehold(now);
            HouseholdCleanup.DissolveIfEmpty(world, ctx, old);
        }
        target.AddMember(child.Id);
        child.JoinHousehold(target.Id);
        ctx.LogEvent(
            WorldEventKind.OrphanTakenIn, $"{child.Id.Value}|{caregiver.Id.Value}|{target.Id.Value}|{bond}", sourceSystem: SystemName);
    }

    /// <summary>AD-186: quem acolhe <paramref name="group"/> (irmãos da mesma casa), por decisão de quem acolhe: laço × meios × generosidade
    /// ÷ casa cheia. Laço: parente (avós, irmão adulto, tios) 1; quem gosta da criança (afeto ≥ 50) 0,8 × afeto; quem gostava do pai ou
    /// da mãe (afeto ≥ 50, vivos ou não) 0,6 × afeto; qualquer adulto da cidade 0,15. Meios: moeda por boca da casa contando as
    /// crianças e o que elas trazem, pela metade em <see cref="MeansHalfAtWageMonths"/> salários mínimos. Empate pelo menor id.</summary>
    private static (Npc Caregiver, string Bond)? Choose(WorldState world, List<Npc> group, List<Npc> adults, LifeStageRules rules, WorldDate now)
    {
        var child = group[0];
        var bonds = new Dictionary<long, (double Bond, string Name)>();
        void Offer(long id, double bond, string name)
        {
            if (!bonds.TryGetValue(id, out var have) || bond > have.Bond) bonds[id] = (bond, name);
        }
        foreach (var relativeId in KinIds(world, child)) Offer(relativeId.Value, KinBond, "parente");
        foreach (var contactId in world.RelationshipTargetsOf(child.Id))
            if (world.Relationships.TryGetValue(new RelationshipKey(child.Id, contactId), out var relation) && relation.Affection >= MinimumAffection)
                Offer(contactId.Value, ChildBond * relation.Affection / 100.0, "afeto");
        foreach (var parentId in new[] { child.MotherId, child.FatherId })
        {
            if (parentId is not { } pid) continue;
            foreach (var contactId in world.RelationshipTargetsOf(pid))
                if (world.Relationships.TryGetValue(new RelationshipKey(pid, contactId), out var relation) && relation.Affection >= MinimumAffection)
                    Offer(contactId.Value, ParentBond * relation.Affection / 100.0, "laco-dos-pais");
        }

        long minWage = world.EconomyRules.WageByProfession.Count == 0 ? 0 : world.EconomyRules.WageByProfession.Values.Min();
        double half = Math.Max(1, MeansHalfAtWageMonths * minWage);
        long brought = 0;
        foreach (var c in group) brought += c.Wallet.Amount;
        var groupIds = new HashSet<long>(group.Select(g => g.Id.Value));

        Npc? best = null;
        string bestBond = "";
        double bestScore = 0;
        foreach (var adult in adults)
        {
            if (groupIds.Contains(adult.Id.Value) || !adult.IsAlive || adult.Household is not { } hid) continue;
            if (world.FindHousehold(hid) is not { IsHomeless: false } home || group.Any(g => g.Household == hid)) continue;
            double bond;
            string name;
            if (bonds.TryGetValue(adult.Id.Value, out var b)) (bond, name) = b;
            else if (adult.City == child.City) (bond, name) = (CityBond, "comunidade");
            else continue;
            long wealth = brought;
            foreach (var m in home.Members)
                if (world.FindNpc(m) is { IsAlive: true } member) wealth += member.Wallet.Amount;
            int mouths = home.Members.Count + group.Count;
            double perMouth = wealth / (double)Math.Max(1, mouths);
            double means = perMouth / (perMouth + half);
            double score = bond * (0.3 + 0.7 * means) * (0.5 + adult.Personality.Altruism / 100.0) / (1 + 0.15 * home.Members.Count);
            if (score > bestScore) (best, bestBond, bestScore) = (adult, name, score);
        }
        return best is null ? null : (best, bestBond);
    }

    /// <summary>Leva a criança para o domicílio de um adulto (parente, afeto ou comunidade). <paramref name="adults"/> evita recalcular a lista
    /// quando vários órfãos são tratados juntos; <paramref name="detach"/> é falso quando quem chama (a morte dos pais) já a tirou do domicílio antigo.
    /// Sem ninguém disponível devolve falso e a criança fica como está.</summary>
    internal static bool TryTakeIn(WorldState world, TickContext ctx, Npc child, List<Npc>? adults = null, bool detach = true)
    {
        if (!world.OntogenyRules.OrphanCare) return false;
        var rules = world.LifeStageRules;
        var now = world.CurrentDate;
        adults ??= world.Npcs
            .Where(n => n.IsAlive && n.Household is not null && rules.LifeStageOf(n.AgeYears(now)) is LifeStage.Adult or LifeStage.Elder)
            .OrderBy(n => n.Id.Value)
            .ToList();
        var found = world.OntogenyRules.OrphanCareLegacy ? FindCaregiver(world, child, adults, rules, now) : Choose(world, [child], adults, rules, now);
        if (found is not var (caregiver, bond)) return false;
        Move(world, ctx, child, caregiver, bond, detach);
        return true;
    }

    private static (Npc Caregiver, string Bond)? FindCaregiver(
        WorldState world, Npc child, List<Npc> adults, LifeStageRules rules, WorldDate now)
    {
        // 1. Parentes: avós e irmãos adultos (como na morte dos pais) e, agora, tios.
        foreach (var relativeId in KinIds(world, child))
            if (world.FindNpc(relativeId) is { IsAlive: true, Household: not null } relative
                && rules.LifeStageOf(relative.AgeYears(now)) is LifeStage.Adult or LifeStage.Elder
                && world.FindHousehold(relative.Household!.Value) is not null)
                return (relative, "parente");

        // 2. Quem a criança já conhece e de quem gosta.
        foreach (var contactId in world.RelationshipTargetsOf(child.Id).OrderBy(c => c.Value))
            if (world.Relationships.TryGetValue(new RelationshipKey(child.Id, contactId), out var relation)
                && relation.Affection >= MinimumAffection
                && adults.FirstOrDefault(a => a.Id == contactId) is { } friend)
                return (friend, "afeto");

        // 3. A comunidade: o adulto mais generoso da cidade, com a casa menos cheia (desempate por id).
        var community = adults
            .Where(a => a.City == child.City && a.Id != child.Id)
            .OrderBy(a => world.FindHousehold(a.Household!.Value)?.Members.Count ?? int.MaxValue)
            .ThenByDescending(a => a.Personality.Altruism)
            .FirstOrDefault();
        return community is null ? null : (community, "comunidade");
    }

    private static IEnumerable<NpcId> KinIds(WorldState world, Npc child)
    {
        var seen = new HashSet<NpcId>();
        foreach (var id in HouseholdRedistribution.GrandparentIds(child, world))
            if (seen.Add(id)) yield return id;
        foreach (var sibling in world.Npcs.Where(n => n.Id != child.Id && n.IsAlive
                     && child.MotherId is not null && n.MotherId == child.MotherId && n.FatherId == child.FatherId).OrderBy(n => n.Id.Value))
            if (seen.Add(sibling.Id)) yield return sibling.Id;
        foreach (var parentId in new[] { child.MotherId, child.FatherId })
        {
            if (parentId is null || world.FindNpc(parentId.Value) is not { } parent) continue;
            foreach (var uncle in world.Npcs.Where(n => n.Id != parent.Id && n.IsAlive
                         && ((parent.MotherId is not null && n.MotherId == parent.MotherId) || (parent.FatherId is not null && n.FatherId == parent.FatherId)))
                         .OrderBy(n => n.Id.Value))
                if (seen.Add(uncle.Id)) yield return uncle.Id;
        }
    }
}
