using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Llm;

/// <summary>Job batch periódico fora do caminho crítico do tick (Fase 11, roadmap item 10,
/// LLM-17..19) — reduz a contagem de <see cref="WorldState.VolatileMemories"/> de um NPC
/// resumindo grupos antigos de baixa importância em um único registro por
/// (dono, categoria). Nunca toca <see cref="WorldState.CanonicalMemories"/> (importância >=
/// <see cref="LlmRules.CanonicalMemoryImportanceThreshold"/>): só itera
/// <see cref="WorldState.VolatileMemories"/>, então o conjunto de ids canônicos e o hash
/// canônico do mundo (<see cref="Snapshot.IncrementalHasher"/> via <c>WorldSnapshot.CanonicalHash</c>)
/// permanecem idênticos antes/depois. O resumo nunca inventa fato novo: seu conteúdo é a
/// concatenação determinística (ordenada por id) do conteúdo das memórias que resume.</summary>
public static class MemoryCompactionJob
{
    /// <summary>Compacta, para cada (dono, categoria) com 2+ memórias voláteis, o grupo inteiro
    /// em um único registro resumo. Chame fora do tick crítico (worker/CLI batch), nunca dentro
    /// de <c>WorldClock</c>.</summary>
    public static void Compact(WorldState world)
    {
        var groups = world.VolatileMemories
            .GroupBy(m => (m.OwnerId, m.Category))
            .Where(g => g.Count() >= 2);

        foreach (var group in groups)
        {
            var memories = group.OrderBy(m => m.Id).ToList();

            var summary = new NpcMemory(
                Id: memories[0].Id,
                OwnerId: group.Key.OwnerId,
                Category: group.Key.Category,
                Content: string.Join(" | ", memories.Select(m => m.Content)),
                Importance: memories.Max(m => m.Importance),
                OriginTick: memories.Max(m => m.OriginTick),
                Participants: memories.SelectMany(m => m.Participants).Distinct().OrderBy(p => p.Value).ToList(),
                Location: memories[0].Location);

            world.ReplaceVolatileMemories(memories.Select(m => m.Id).ToList(), summary);
        }
    }
}
