using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Llm;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Constrói <see cref="DecisionContext"/> on-demand por wake (Fase 16.3 P1b, COH-11).
/// Não persiste; não referencia o <see cref="WorldState"/> no tipo resultante.</summary>
public static class DecisionContextBuilder
{
    /// <summary>Máximo de memórias recuperadas por wake (P1b; dirty-cache em P2a).</summary>
    public const int DefaultMemoryRecallCount = 5;

    public static DecisionContext Build(WorldState world, Npc npc, long tick, LlmRules? llmRules = null)
    {
        var needs = new NeedsSnapshot(
            npc.HungerAt(tick),
            npc.ThirstAt(tick),
            npc.SleepAt(tick),
            npc.SocialAt(tick));

        var body = BuildBodySlice(world, npc);
        var household = BuildHouseholdSlice(world, npc);
        var memories = BuildMemorySlice(world, npc, needs, llmRules);
        var beliefs = BuildBeliefSlice(world, npc);
        var relationships = BuildRelationshipSlice(world, npc);

        return new DecisionContext(
            npc.Id,
            tick,
            needs,
            body,
            household,
            RelevantMemories: memories,
            RelevantBeliefs: beliefs,
            KnownRelationships: relationships,
            PowerOpportunities: PowerOpportunityProvider.ApplicableTo(world, npc, tick),
            npc.Personality,
            npc.CurrentAction,
            ForesightPreviews: ForesightMechanic.PreviewsFor(world, npc.Id, tick));
    }

    internal static BodySnapshot BuildBodySlice(WorldState world, Npc npc) =>
        new(
            npc.Height,
            npc.Weight,
            npc.MuscleMass,
            BodyMechanic.WorkCapacityMultiplier(world, npc),
            BodyMechanic.MovementCostMultiplier(world, npc));

    internal static HouseholdSnapshot? BuildHouseholdSlice(WorldState world, Npc npc)
    {
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } h)
            return null;

        return new HouseholdSnapshot(
            h.Id,
            new Dictionary<ResourceType, long>(h.Stock),
            h.Members.ToList());
    }

    internal static IReadOnlyList<NpcMemory> BuildMemorySlice(
        WorldState world, Npc npc, NeedsSnapshot needs, LlmRules? llmRules = null)
    {
        var rules = llmRules ?? LlmRules.Default;
        string needQuery = DeriveActiveNeedQuery(needs);
        var memories = MemoryRecall.Recall(world, npc.Id, needQuery, DefaultMemoryRecallCount, rules);
        return memories.Count == 0 ? Array.Empty<NpcMemory>() : memories.ToArray();
    }

    internal static IReadOnlyList<string> BuildBeliefSlice(WorldState world, Npc npc)
    {
        var beliefs = NpcBeliefQuery.BeliefsOf(world, npc.Id);
        return beliefs.Count == 0 ? Array.Empty<string>() : beliefs.ToArray();
    }

    internal static IReadOnlyList<RelationshipFact> BuildRelationshipSlice(WorldState world, Npc npc) =>
        KnownRelationshipsOf(world, npc.Id);

    /// <summary>Relações A→* já existentes (lazy AD-061) — nunca cria entrada nova a partir
    /// da decisão (COH-14).</summary>
    private static IReadOnlyList<RelationshipFact> KnownRelationshipsOf(WorldState world, NpcId npcId)
    {
        if (world.Relationships.Count == 0)
            return Array.Empty<RelationshipFact>();

        var facts = new List<RelationshipFact>();
        foreach (var (key, rel) in world.Relationships.OrderBy(kv => kv.Key.To.Value))
        {
            if (key.From != npcId) continue;
            facts.Add(new RelationshipFact(
                key.To,
                (int)Math.Round(rel.Trust),
                (int)Math.Round(rel.Affection),
                (int)Math.Round(rel.Respect),
                (int)Math.Round(rel.Debt)));
        }

        return facts.Count == 0 ? Array.Empty<RelationshipFact>() : facts.ToArray();
    }

    /// <summary>Query bag-of-words derivada do need com maior déficit (COH-12) — alimenta
    /// <see cref="MemoryRecall.Recall"/>; lista vazia de memórias/crenças é OK (COH-16).</summary>
    internal static string DeriveActiveNeedQuery(NeedsSnapshot needs)
    {
        int hunger = 100 - needs.Hunger;
        int thirst = 100 - needs.Thirst;
        int sleep = 100 - needs.Sleep;
        int social = 100 - needs.Social;
        int max = Math.Max(Math.Max(hunger, thirst), Math.Max(sleep, social));
        if (max <= 0) return "";

        if (hunger == max) return "hunger food fome eat meal scarcity";
        if (thirst == max) return "thirst water sede drink";
        if (sleep == max) return "sleep rest tired cansaco";
        return "social friend betrayal trust relation traição";
    }
}
