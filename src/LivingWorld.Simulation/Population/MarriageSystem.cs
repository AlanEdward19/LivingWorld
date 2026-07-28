using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Casamento monogâmico (Fase 7, T15, FAM-12, AD-060) — helper estático reusado por
/// <see cref="CourtshipSystem"/>.</summary>
public static class MarriageSystem
{
    public static void Marry(WorldState world, TickContext ctx, Npc spouseA, Npc spouseB)
    {
        LeavePreviousHousehold(world, ctx, spouseA);
        LeavePreviousHousehold(world, ctx, spouseB);

        var head = spouseA.Id.Value <= spouseB.Id.Value ? spouseA : spouseB;
        var other = ReferenceEquals(head, spouseA) ? spouseB : spouseA;
        var stock = world.FamilyRules.MarriageInitialStock
            .ToDictionary(kv => new ResourceType(kv.Key), kv => kv.Value);

        var householdId = world.NextHouseholdIdAndAdvance();
        var household = new Household(
            householdId, head.CurrentLocation, head.Id, [head.Id, other.Id], stock);
        world.AddHousehold(household);

        foreach (var (resource, amount) in stock)
            world.RecordResourceProduced(resource, amount);

        head.JoinHousehold(householdId);
        other.JoinHousehold(householdId);
        spouseA.Marry(spouseB.Id);
        spouseB.Marry(spouseA.Id);

        ctx.LogEvent(WorldEventKind.Marriage, $"{spouseA.Id.Value}|{spouseB.Id.Value}");
    }

    private static void LeavePreviousHousehold(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Household is not { } householdId) return;

        var household = world.FindHousehold(householdId);
        household?.RemoveMember(npc.Id);
        npc.LeaveHousehold(world.CurrentDate);
        if (household is not null)
            HouseholdCleanup.DissolveIfEmpty(world, ctx, household);
    }
}
