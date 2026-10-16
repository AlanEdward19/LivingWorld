using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Llm;

/// <summary>Fase 11, roadmap itens 1/2 (LLM-04/05): modelo de memória do NPC (5 categorias),
/// classificação canônico/volátil por limiar do cenário (ADR-0014) e <see cref="MemoryRecall"/>
/// ponderado por importância + recência + relevância, com desempate por Id — o critério de
/// verificação do spec.md ("Recall(npc, query, 5) devolve as mesmas 5 memórias, na mesma ordem,
/// em duas execuções do mesmo mundo semeado").</summary>
public class MemoryRecallTests
{
    private static readonly NpcId Owner = new(1);
    private static readonly NpcId Other = new(2);
    private static readonly CellCoord SomeLocation = new(0, 0);

    private static LlmRules RulesWithThreshold(int threshold) => LlmRules.Create(
        hostileTrustThreshold: 20,
        actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => ConversationCompatibility.Compatible),
        canonicalMemoryImportanceThreshold: threshold).Value!;

    private static WorldState BuildWorld(long nowTick = 100)
    {
        var (world, _) = ScenarioRunner.Create(seed: 1);
        world.CurrentDate = new WorldDate(world.Calendar, nowTick);
        return world;
    }

    // --- Classificação canônico/volátil (ADR-0014) ---

    [Fact]
    public void Memory_with_importance_at_or_above_threshold_is_canonical_not_volatile()
    {
        var world = BuildWorld();
        var rules = RulesWithThreshold(50);

        world.AddNpcMemory(Owner, MemoryCategory.Episodic, "colheita farta", importance: 50, originTick: 10,
            participants: [Owner], location: SomeLocation, canonicalImportanceThreshold: rules.CanonicalMemoryImportanceThreshold);

        Assert.Single(world.CanonicalMemories);
        Assert.Empty(world.VolatileMemories);
    }

    [Fact]
    public void Memory_below_threshold_is_volatile_not_canonical()
    {
        var world = BuildWorld();
        var rules = RulesWithThreshold(50);

        world.AddNpcMemory(Owner, MemoryCategory.Operational, "fome leve", importance: 49, originTick: 10,
            participants: [Owner], location: SomeLocation, canonicalImportanceThreshold: rules.CanonicalMemoryImportanceThreshold);

        Assert.Empty(world.CanonicalMemories);
        Assert.Single(world.VolatileMemories);
    }

    // --- Recall: cada dimensão isolada ---

    [Fact]
    public void Recall_orders_by_importance_when_recency_and_relevance_are_equal()
    {
        var world = BuildWorld(nowTick: 100);
        var rules = RulesWithThreshold(50);
        Add(world, rules, "evento neutro", importance: 30, originTick: 100);
        Add(world, rules, "evento neutro", importance: 90, originTick: 100);

        var result = MemoryRecall.Recall(world, Owner, query: "", n: 5, rules);

        Assert.Equal(90, result[0].Importance);
        Assert.Equal(30, result[1].Importance);
    }

    [Fact]
    public void Recall_orders_by_recency_when_importance_and_relevance_are_equal()
    {
        var world = BuildWorld(nowTick: 100);
        var rules = RulesWithThreshold(50);
        Add(world, rules, "evento neutro", importance: 60, originTick: 10);
        Add(world, rules, "evento neutro", importance: 60, originTick: 95);

        var result = MemoryRecall.Recall(world, Owner, query: "", n: 5, rules);

        Assert.Equal(95, result[0].OriginTick);
        Assert.Equal(10, result[1].OriginTick);
    }

    [Fact]
    public void Recall_orders_by_relevance_when_importance_and_recency_are_equal()
    {
        var world = BuildWorld(nowTick: 100);
        var rules = RulesWithThreshold(50);
        Add(world, rules, "colheita e festa na vila", importance: 60, originTick: 100);
        Add(world, rules, "assunto sem nenhuma relacao", importance: 60, originTick: 100);

        var result = MemoryRecall.Recall(world, Owner, query: "colheita festa", n: 5, rules);

        Assert.Equal("colheita e festa na vila", result[0].Content);
        Assert.Equal("assunto sem nenhuma relacao", result[1].Content);
    }

    [Fact]
    public void Recall_ties_break_by_ascending_memory_id()
    {
        var world = BuildWorld(nowTick: 100);
        var rules = RulesWithThreshold(50);
        Add(world, rules, "mesmo conteudo", importance: 60, originTick: 100);
        Add(world, rules, "mesmo conteudo", importance: 60, originTick: 100);
        Add(world, rules, "mesmo conteudo", importance: 60, originTick: 100);

        var result = MemoryRecall.Recall(world, Owner, query: "", n: 5, rules);

        Assert.Equal([0L, 1L, 2L], result.Select(m => m.Id).ToArray());
    }

    [Fact]
    public void Recall_only_returns_memories_owned_by_the_queried_npc()
    {
        var world = BuildWorld();
        var rules = RulesWithThreshold(50);
        world.AddNpcMemory(Owner, MemoryCategory.Semantic, "memoria do dono", importance: 60, originTick: 100,
            participants: [Owner], location: SomeLocation, canonicalImportanceThreshold: rules.CanonicalMemoryImportanceThreshold);
        world.AddNpcMemory(Other, MemoryCategory.Semantic, "memoria de outro npc", importance: 60, originTick: 100,
            participants: [Other], location: SomeLocation, canonicalImportanceThreshold: rules.CanonicalMemoryImportanceThreshold);

        var result = MemoryRecall.Recall(world, Owner, query: "", n: 5, rules);

        Assert.Single(result);
        Assert.Equal("memoria do dono", result[0].Content);
    }

    // --- Determinismo (critério de verificação do spec.md) ---

    [Fact]
    public void Recall_five_returns_same_memories_in_same_order_across_two_runs()
    {
        var world = BuildWorld(nowTick: 200);
        var rules = RulesWithThreshold(50);
        Add(world, rules, "chegada do inverno", importance: 80, originTick: 5);
        Add(world, rules, "casamento na vila", importance: 95, originTick: 150);
        Add(world, rules, "briga no mercado", importance: 40, originTick: 190);
        Add(world, rules, "boato sobre a colheita", importance: 60, originTick: 100);
        Add(world, rules, "festa da colheita", importance: 70, originTick: 180);
        Add(world, rules, "seca no verao", importance: 55, originTick: 20);
        Add(world, rules, "nascimento na familia", importance: 85, originTick: 199);

        var first = MemoryRecall.Recall(world, Owner, query: "colheita festa", n: 5, rules);
        var second = MemoryRecall.Recall(world, Owner, query: "colheita festa", n: 5, rules);

        Assert.Equal(5, first.Count);
        Assert.Equal(first.Select(m => m.Id), second.Select(m => m.Id));
        Assert.Equal(first, second);
    }

    private static void Add(WorldState world, LlmRules rules, string content, int importance, long originTick) =>
        world.AddNpcMemory(Owner, MemoryCategory.Episodic, content, importance, originTick,
            participants: [Owner], location: SomeLocation, canonicalImportanceThreshold: rules.CanonicalMemoryImportanceThreshold);
}
