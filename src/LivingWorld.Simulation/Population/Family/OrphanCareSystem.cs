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
/// acolhedor passa a alimentá-la e a ser o cuidador de <see cref="ExposureAccumulator"/> (idioma e desenvolvimento).</summary>
[LivingCapability("FAMILY", Events = [WorldEventKind.OrphanTakenIn], Consumers = ["inspector.npc.household", "timeline.family"])]
[RunsAfter(typeof(NatalitySystem))]
public sealed class OrphanCareSystem : ISimulationSystem
{
    public const string SystemName = "population-orphan-care";

    /// <summary>Afeto mínimo (0–100) para um adulto contar como alguém que a criança já conhece e em quem confia.</summary>
    private const int MinimumAffection = 50;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.OntogenyRules.OrphanCare) return;
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

        foreach (var child in orphans)
            TryTakeIn(world, ctx, child, adults);
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
        if (FindCaregiver(world, child, adults, rules, now) is not var (caregiver, bond)) return false;
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
