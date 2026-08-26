using LivingWorld.Domain;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Simulation;

/// <summary>Constrói <see cref="DecisionContext"/> on-demand por wake (Fase 16.3 P1b, COH-11).
/// Não persiste; não referencia o <see cref="WorldState"/> no tipo resultante.</summary>
public static class DecisionContextBuilder
{
    /// <summary>Máximo de memórias recuperadas por wake (P1b; dirty-cache em P2a).</summary>
    public const int DefaultMemoryRecallCount = 5;

    public static DecisionContext Build(WorldState world, Npc npc, long tick)
    {
        var needs = new NeedsSnapshot(
            npc.HungerAt(tick),
            npc.ThirstAt(tick),
            npc.SleepAt(tick),
            npc.SocialAt(tick));

        var body = new BodySnapshot(
            npc.Height,
            npc.Weight,
            npc.MuscleMass,
            BodyMechanic.WorkCapacityMultiplier(world, npc),
            BodyMechanic.MovementCostMultiplier(world, npc));

        HouseholdSnapshot? household = null;
        if (npc.Household is { } householdId && world.FindHousehold(householdId) is { } h)
        {
            household = new HouseholdSnapshot(
                h.Id,
                new Dictionary<ResourceType, long>(h.Stock),
                h.Members.ToList());
        }

        return new DecisionContext(
            npc.Id,
            tick,
            needs,
            body,
            household,
            RelevantMemories: Array.Empty<NpcMemory>(),
            RelevantBeliefs: Array.Empty<string>(),
            KnownRelationships: Array.Empty<RelationshipFact>(),
            PowerOpportunities: Array.Empty<PowerOpportunity>(),
            npc.Personality,
            npc.CurrentAction);
    }
}
