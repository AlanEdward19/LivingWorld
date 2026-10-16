using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Casamento monogâmico — helper estático reusado por
/// <see cref="CourtshipSystem"/>.</summary>
public static class MarriageSystem
{
    public static void Marry(WorldState world, TickContext ctx, Npc spouseA, Npc spouseB)
    {
        var previousA = spouseA.Household;
        var previousB = spouseB.Household;
        LeavePreviousHousehold(world, ctx, spouseA);
        LeavePreviousHousehold(world, ctx, spouseB);

        var head = spouseA.Id.Value <= spouseB.Id.Value ? spouseA : spouseB;
        var other = ReferenceEquals(head, spouseA) ? spouseB : spouseA;
        var stock = world.FamilyRules.MarriageInitialStock
            .ToDictionary(kv => new ResourceType(kv.Key), kv => kv.Value);

        var householdId = world.NextHouseholdIdAndAdvance();
        var household = new Household(
            householdId, head.CurrentLocation, head.Id, [head.Id, other.Id], stock, city: head.City);
        world.AddHousehold(household);

        foreach (var (resource, amount) in stock)
            world.RecordResourceProduced(resource, amount);

        head.JoinHousehold(householdId);
        other.JoinHousehold(householdId);
        BringDependents(world, ctx, previousA, spouseA, household);
        BringDependents(world, ctx, previousB, spouseB, household);
        spouseA.Marry(spouseB.Id);
        spouseB.Marry(spouseA.Id);

        ctx.LogEvent(WorldEventKind.Marriage, $"{spouseA.Id.Value}|{spouseB.Id.Value}", sourceSystem: "MarriageSystem");
    }

    /// <summary>Quem casa leva consigo os filhos pequenos que ficariam sem ninguém (ou que são seus): sem isso a criança do domicílio de um adulto
    /// único era deixada sozinha ao casamento e morria de sede (dívida da Fase 21).</summary>
    private static void BringDependents(WorldState world, TickContext ctx, HouseholdId? previousId, Npc leaving, Household destination)
    {
        if (previousId is not { } id || world.FindHousehold(id) is not { } previous) return;
        var rules = world.LifeStageRules;
        var now = world.CurrentDate;
        bool someoneRemains = HouseholdRedistribution.HasLivingAdultOrElder(world, previous, rules, now);

        var kids = previous.Members
            .Select(world.FindNpc)
            .Where(n => n is { IsAlive: true } && rules.LifeStageOf(n.AgeYears(now)) == LivingWorld.Domain.Population.Body.LifeStage.Child
                && (!someoneRemains || n.MotherId == leaving.Id || n.FatherId == leaving.Id))
            .Cast<Npc>()
            .OrderBy(n => n.Id.Value)
            .ToList();
        foreach (var kid in kids)
        {
            previous.RemoveMember(kid.Id);
            destination.AddMember(kid.Id);
            kid.JoinHousehold(destination.Id);
        }
        HouseholdCleanup.DissolveIfEmpty(world, ctx, previous);
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
