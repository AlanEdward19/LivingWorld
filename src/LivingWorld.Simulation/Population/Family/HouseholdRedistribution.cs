using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Redistribui filhos vivos quando o household não tem adulto/idoso vivo (Fase 7, T13,
/// FAM-17, AD-057/AD-058).</summary>
public static class HouseholdRedistribution
{
    public static void HandleOrphaned(
        WorldState world, TickContext ctx, Household household, LifeStageRules lifeStageRules, WorldDate now)
    {
        var survivors = household.Members
            .OrderBy(id => id.Value)
            .Select(id => world.FindNpc(id))
            .Where(n => n is { IsAlive: true })
            .Cast<Npc>()
            .ToList();

        foreach (var child in survivors)
        {
            household.RemoveMember(child.Id);
            if (TryFindRelativeHousehold(world, child, lifeStageRules, now) is { } targetId)
            {
                var target = world.FindHousehold(targetId)!;
                target.AddMember(child.Id);
                child.JoinHousehold(targetId);
            }
            else
                CreateUnitaryHousehold(world, child);
        }

        HouseholdCleanup.DissolveIfEmpty(world, ctx, household);
    }

    internal static bool HasLivingAdultOrElder(
        WorldState world, Household household, LifeStageRules lifeStageRules, WorldDate now) =>
        household.Members.Any(id =>
        {
            var npc = world.FindNpc(id);
            return npc is { IsAlive: true }
                && lifeStageRules.LifeStageOf(npc.AgeYears(now)) is LifeStage.Adult or LifeStage.Elder;
        });

    private static HouseholdId? TryFindRelativeHousehold(
        WorldState world, Npc child, LifeStageRules lifeStageRules, WorldDate now)
    {
        foreach (var relativeId in CandidateRelativeIds(child, world, lifeStageRules, now))
        {
            var relative = world.FindNpc(relativeId);
            if (relative is not { IsAlive: true, Household: { } householdId }) continue;
            if (world.FindHousehold(householdId) is null) continue;
            return householdId;
        }

        return null;
    }

    private static IEnumerable<NpcId> CandidateRelativeIds(
        Npc child, WorldState world, LifeStageRules lifeStageRules, WorldDate now) =>
        GrandparentIds(child, world)
            .Concat(AdultSiblingIds(child, world, lifeStageRules, now))
            .Distinct()
            .OrderBy(id => id.Value);

    private static IEnumerable<NpcId> GrandparentIds(Npc child, WorldState world)
    {
        foreach (var parentId in new[] { child.MotherId, child.FatherId })
        {
            if (parentId is not { } pid) continue;
            var parent = world.FindNpc(pid);
            if (parent is null) continue;
            foreach (var grandparentId in new[] { parent.MotherId, parent.FatherId })
            {
                if (grandparentId is { } gpid)
                    yield return gpid;
            }
        }
    }

    private static IEnumerable<NpcId> AdultSiblingIds(
        Npc child, WorldState world, LifeStageRules lifeStageRules, WorldDate now)
    {
        if (child.MotherId is null || child.FatherId is null) yield break;

        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            if (npc.Id == child.Id || !npc.IsAlive) continue;
            if (npc.MotherId != child.MotherId || npc.FatherId != child.FatherId) continue;
            if (lifeStageRules.LifeStageOf(npc.AgeYears(now)) is not (LifeStage.Adult or LifeStage.Elder))
                continue;
            yield return npc.Id;
        }
    }

    private static void CreateUnitaryHousehold(WorldState world, Npc child)
    {
        var householdId = world.NextHouseholdIdAndAdvance();
        var household = new Household(householdId, child.CurrentLocation, child.Id, [child.Id]);
        world.AddHousehold(household);
        child.JoinHousehold(householdId);
    }
}
