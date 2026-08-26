using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation;

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

        var rules = llmRules ?? LlmRules.Default;
        string needQuery = DeriveActiveNeedQuery(needs);
        var memories = MemoryRecall.Recall(world, npc.Id, needQuery, DefaultMemoryRecallCount, rules);
        var beliefs = NpcBeliefQuery.BeliefsOf(world, npc.Id);
        var relationships = KnownRelationshipsOf(world, npc.Id);

        return new DecisionContext(
            npc.Id,
            tick,
            needs,
            body,
            household,
            RelevantMemories: memories.Count == 0 ? Array.Empty<NpcMemory>() : memories.ToArray(),
            RelevantBeliefs: beliefs.Count == 0 ? Array.Empty<string>() : beliefs.ToArray(),
            KnownRelationships: relationships,
            PowerOpportunities: PowerOpportunityProvider.ApplicableTo(world, npc, tick),
            npc.Personality,
            npc.CurrentAction);
    }

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
