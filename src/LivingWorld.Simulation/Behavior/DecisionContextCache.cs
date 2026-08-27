using System.Runtime.CompilerServices;
using LivingWorld.Domain;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Simulation;

/// <summary>Categorias de <see cref="DecisionContext"/> com dirty-flag independente
/// (Fase 16.3 P2a, COH-45 / doc#60) — mesmo espírito touch-on-mutate de PERF-12.</summary>
[Flags]
public enum DecisionContextCategory
{
    None = 0,
    Needs = 1 << 0,
    Body = 1 << 1,
    Location = 1 << 2,
    Economy = 1 << 3,
    Household = 1 << 4,
    Relationships = 1 << 5,
    Knowledge = 1 << 6,
    Beliefs = 1 << 7,
    Memory = 1 << 8,
    Threat = 1 << 9,
    Capabilities = 1 << 10,

    All = Needs | Body | Location | Economy | Household | Relationships
        | Knowledge | Beliefs | Memory | Threat | Capabilities,
}

/// <summary>Cache por-NPC de fatias de <see cref="DecisionContext"/> — reconstrói só
/// categorias dirty desde o último wake (COH-45).</summary>
public static class DecisionContextCache
{
    private sealed class Store
    {
        public readonly Dictionary<long, Entry> Entries = new();
    }

    private sealed class Entry
    {
        public DecisionContext? Context;
        public DecisionContextCategory Dirty = DecisionContextCategory.All;
    }

    private static readonly ConditionalWeakTable<WorldState, Store> Stores = new();

    /// <summary>Contadores de fatia (só testes) — resetar entre casos.</summary>
    public static int NeedsBuildCount { get; private set; }
    public static int MemoryBuildCount { get; private set; }
    public static int BeliefBuildCount { get; private set; }
    public static int RelationshipBuildCount { get; private set; }

    public static void ResetCounters()
    {
        NeedsBuildCount = 0;
        MemoryBuildCount = 0;
        BeliefBuildCount = 0;
        RelationshipBuildCount = 0;
    }

    /// <summary>Marca categoria dirty — chamar ao lado de <c>TouchCanonical</c> nos mutadores.</summary>
    public static void MarkDirty(WorldState world, NpcId npcId, DecisionContextCategory category)
    {
        var store = Stores.GetOrCreateValue(world);
        if (!store.Entries.TryGetValue(npcId.Value, out var entry))
        {
            entry = new Entry();
            store.Entries[npcId.Value] = entry;
        }

        entry.Dirty |= category;
    }

    public static DecisionContext BuildIncremental(
        WorldState world, Npc npc, long tick, LlmRules? llmRules = null)
    {
        var store = Stores.GetOrCreateValue(world);
        if (!store.Entries.TryGetValue(npc.Id.Value, out var entry) || entry.Context is null)
        {
            var fresh = DecisionContextBuilder.Build(world, npc, tick, llmRules);
            store.Entries[npc.Id.Value] = new Entry
            {
                Context = fresh,
                Dirty = DecisionContextCategory.None,
            };
            NeedsBuildCount++;
            MemoryBuildCount++;
            BeliefBuildCount++;
            RelationshipBuildCount++;
            return fresh;
        }

        var dirty = entry.Dirty;
        if (dirty == DecisionContextCategory.None)
            return entry.Context with { Tick = tick };

        var prev = entry.Context;
        var needs = (dirty & DecisionContextCategory.Needs) != 0
            ? BuildNeeds(npc, tick)
            : prev.Needs;
        var body = (dirty & (DecisionContextCategory.Body | DecisionContextCategory.Location)) != 0
            ? DecisionContextBuilder.BuildBodySlice(world, npc)
            : prev.Body;
        var household = (dirty & (DecisionContextCategory.Household | DecisionContextCategory.Economy)) != 0
            ? DecisionContextBuilder.BuildHouseholdSlice(world, npc)
            : prev.Household;
        var memories = (dirty & DecisionContextCategory.Memory) != 0
            ? BuildMemories(world, npc, needs, llmRules)
            : prev.RelevantMemories;
        var beliefs = (dirty & (DecisionContextCategory.Beliefs | DecisionContextCategory.Knowledge)) != 0
            ? BuildBeliefs(world, npc)
            : prev.RelevantBeliefs;
        var relationships = (dirty & DecisionContextCategory.Relationships) != 0
            ? BuildRelationships(world, npc)
            : prev.KnownRelationships;
        var powers = (dirty & (DecisionContextCategory.Capabilities | DecisionContextCategory.Threat)) != 0
            ? PowerOpportunityProvider.ApplicableTo(world, npc, tick)
            : prev.PowerOpportunities;

        var merged = new DecisionContext(
            npc.Id, tick, needs, body, household, memories, beliefs, relationships, powers,
            npc.Personality, npc.CurrentAction);

        entry.Context = merged;
        entry.Dirty = DecisionContextCategory.None;
        return merged;
    }

    private static NeedsSnapshot BuildNeeds(Npc npc, long tick)
    {
        NeedsBuildCount++;
        return new NeedsSnapshot(
            npc.HungerAt(tick), npc.ThirstAt(tick), npc.SleepAt(tick), npc.SocialAt(tick));
    }

    private static IReadOnlyList<NpcMemory> BuildMemories(
        WorldState world, Npc npc, NeedsSnapshot needs, LlmRules? llmRules)
    {
        MemoryBuildCount++;
        return DecisionContextBuilder.BuildMemorySlice(world, npc, needs, llmRules);
    }

    private static IReadOnlyList<string> BuildBeliefs(WorldState world, Npc npc)
    {
        BeliefBuildCount++;
        return DecisionContextBuilder.BuildBeliefSlice(world, npc);
    }

    private static IReadOnlyList<RelationshipFact> BuildRelationships(WorldState world, Npc npc)
    {
        RelationshipBuildCount++;
        return DecisionContextBuilder.BuildRelationshipSlice(world, npc);
    }
}
