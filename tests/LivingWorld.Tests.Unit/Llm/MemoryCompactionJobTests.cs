using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Llm;

/// <summary>Fase 11, roadmap item 10 (LLM-17..19, spec.md story "Compactação de memória em
/// lote"): <see cref="MemoryCompactionJob"/> reduz a contagem de memórias voláteis de um NPC sem
/// tocar memória canônica — hash canônico e conjunto de ids canônicos permanecem idênticos, e o
/// resumo nunca inventa conteúdo fora das memórias voláteis originais.</summary>
public class MemoryCompactionJobTests
{
    private static readonly NpcId Owner = new(1);
    private static readonly CellCoord SomeLocation = new(0, 0);
    private const int Threshold = 50;

    private static readonly LlmRules Rules = LlmRules.Create(
        hostileTrustThreshold: 20,
        actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => ConversationCompatibility.Compatible),
        canonicalMemoryImportanceThreshold: Threshold).Value!;

    /// <summary>1000 memórias do mesmo NPC: 900 abaixo do limiar (voláteis), 100 no limiar ou
    /// acima (canônicas), espalhadas pelas 5 categorias.</summary>
    private static WorldState BuildWorldWithManyMemories()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1);
        var categories = Enum.GetValues<MemoryCategory>();

        for (int i = 0; i < 900; i++)
        {
            world.AddNpcMemory(Owner, categories[i % categories.Length], $"memoria volatil {i}",
                importance: Threshold - 1 - (i % Threshold), originTick: i,
                participants: [Owner], location: SomeLocation, canonicalImportanceThreshold: Threshold);
        }

        for (int i = 0; i < 100; i++)
        {
            world.AddNpcMemory(Owner, categories[i % categories.Length], $"memoria canonica {i}",
                importance: Threshold + (i % (100 - Threshold)), originTick: 900 + i,
                participants: [Owner], location: SomeLocation, canonicalImportanceThreshold: Threshold);
        }

        return world;
    }

    [Fact]
    public void Compact_reduces_total_memory_count()
    {
        var world = BuildWorldWithManyMemories();
        int totalBefore = world.CanonicalMemories.Count + world.VolatileMemories.Count;

        MemoryCompactionJob.Compact(world);

        int totalAfter = world.CanonicalMemories.Count + world.VolatileMemories.Count;
        Assert.True(totalAfter < totalBefore);
    }

    [Fact]
    public void Compact_leaves_canonical_hash_unchanged()
    {
        var world = BuildWorldWithManyMemories();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        MemoryCompactionJob.Compact(world);

        var hashAfter = WorldSnapshot.CanonicalHash(world);
        Assert.Equal(hashBefore, hashAfter);
    }

    [Fact]
    public void Compact_preserves_exact_set_of_canonical_memory_ids()
    {
        var world = BuildWorldWithManyMemories();
        var idsBefore = world.CanonicalMemories.Select(m => m.Id).ToHashSet();

        MemoryCompactionJob.Compact(world);

        var idsAfter = world.CanonicalMemories.Select(m => m.Id).ToHashSet();
        Assert.Equal(idsBefore, idsAfter);
    }

    [Fact]
    public void Compact_never_touches_memories_at_or_above_threshold()
    {
        var world = BuildWorldWithManyMemories();
        var canonicalBefore = world.CanonicalMemories.ToList();

        MemoryCompactionJob.Compact(world);

        Assert.Equal(canonicalBefore, world.CanonicalMemories);
    }

    [Fact]
    public void Compacted_summary_content_is_a_concatenation_of_original_volatile_contents_only()
    {
        var world = BuildWorldWithManyMemories();
        var originalVolatileContents = world.VolatileMemories.Select(m => m.Content).ToHashSet();

        MemoryCompactionJob.Compact(world);

        foreach (var summary in world.VolatileMemories)
        {
            var parts = summary.Content.Split(" | ");
            foreach (var part in parts)
                Assert.Contains(part, originalVolatileContents);
        }
    }

    [Fact]
    public void Compact_does_not_introduce_content_absent_from_any_original_memory()
    {
        var world = BuildWorldWithManyMemories();
        var allOriginalContents = world.CanonicalMemories.Concat(world.VolatileMemories)
            .Select(m => m.Content).ToHashSet();

        MemoryCompactionJob.Compact(world);

        foreach (var memory in world.CanonicalMemories.Concat(world.VolatileMemories))
        {
            var parts = memory.Content.Split(" | ");
            foreach (var part in parts)
                Assert.Contains(part, allOriginalContents);
        }
    }
}
