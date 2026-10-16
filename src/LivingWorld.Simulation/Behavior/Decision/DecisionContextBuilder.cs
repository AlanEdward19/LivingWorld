using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Llm;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Constrói <see cref="DecisionContext"/> on-demand por wake.
/// Não persiste; não referencia o <see cref="WorldState"/> no tipo resultante.</summary>
public static class DecisionContextBuilder
{
    /// <summary>Máximo de memórias recuperadas por wake (P1b; dirty-cache em P2a).</summary>
    public const int DefaultMemoryRecallCount = 5;

    public static DecisionContext Build(
        WorldState world, Npc npc, long tick, LlmRules? llmRules = null, double provisionTargetDays = 2.0)
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
        var relationships = new LazyRelationshipFacts(world, npc.Id);

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
            ForesightPreviews: ForesightMechanic.PreviewsFor(world, npc.Id, tick),
            WalletAmount: npc.Wallet.Amount,
            ProvisionTargetDays: provisionTargetDays);
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

        // Visões somente-leitura do próprio domicílio, sem copiar estoque e membros a cada decisão: o
        // contexto vive só durante a decisão (nada muta o domicílio entre montá-lo e usá-lo).
        return new HouseholdSnapshot(h.Id, h.Stock, h.Members);
    }

    internal static IReadOnlyList<NpcMemory> BuildMemorySlice(
        WorldState world, Npc npc, NeedsSnapshot needs, LlmRules? llmRules = null)
    {
        var rules = llmRules ?? LlmRules.Default;
        string needQuery = DeriveActiveNeedQuery(needs);
        var memories = MemoryRecall.Recall(world, npc.Id, needQuery, DefaultMemoryRecallCount, rules);
        return memories.Count == 0 ? Array.Empty<NpcMemory>() : memories.ToArray();
    }

    internal static IReadOnlyList<string> BuildBeliefSlice(WorldState world, Npc npc) =>
        NpcBeliefQuery.BeliefsOf(world, npc.Id);

    internal static IReadOnlyList<RelationshipFact> BuildRelationshipSlice(WorldState world, Npc npc) =>
        KnownRelationshipsOf(world, npc.Id);

    /// <summary>Relações A→* já existentes (lazy) — nunca cria entrada nova a partir
    /// da decisão.</summary>
    private static IReadOnlyList<RelationshipFact> KnownRelationshipsOf(WorldState world, NpcId npcId) =>
        MaterializeKnownRelationships(world, npcId);

    internal static RelationshipFact[] MaterializeKnownRelationships(WorldState world, NpcId npcId)
    {
        if (world.Relationships.Count == 0)
            return [];

        // Índice por NPC (já ordenado por To): O(grau) por decisão, não O(relações do mundo).
        var targets = world.RelationshipTargetsOf(npcId);
        if (targets.Count == 0)
            return [];

        var facts = new RelationshipFact[targets.Count];
        for (int i = 0; i < facts.Length; i++)
        {
            var to = targets[i];
            var rel = world.Relationships[new RelationshipKey(npcId, to)];
            facts[i] = new RelationshipFact(
                to,
                (int)Math.Round(rel.Trust),
                (int)Math.Round(rel.Affection),
                (int)Math.Round(rel.Respect),
                (int)Math.Round(rel.Debt));
        }

        return facts;
    }

    /// <summary>Query bag-of-words derivada do need com maior déficit — alimenta
    /// <see cref="MemoryRecall.Recall"/>; lista vazia de memórias/crenças é OK.</summary>
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
