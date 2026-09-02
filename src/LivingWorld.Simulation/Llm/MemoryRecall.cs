using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Llm;

/// <summary>Recuperação ponderada de memória de um NPC (Fase 11, roadmap item 2, LLM-04/05) —
/// pontua por importância + recência + relevância (pesos em <see cref="LlmRules"/> do cenário) e
/// desempata por <see cref="NpcMemory.Id"/>, para <c>Recall(npc, query, n)</c> devolver sempre a
/// mesma ordem no mesmo mundo semeado (spec.md, critério de verificação).</summary>
public static class MemoryRecall
{
    public static IReadOnlyList<NpcMemory> Recall(WorldState world, NpcId npcId, string query, int n, LlmRules rules)
    {
        long now = world.CurrentDate.TotalHours;

        return world.CanonicalMemories.Concat(world.VolatileMemories)
            .Where(m => m.OwnerId == npcId)
            .Select(m => (Memory: m, Score: Score(m, query, now, rules)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Memory.Id)
            .Take(n)
            .Select(x => x.Memory)
            .ToList();
    }

    private static double Score(NpcMemory memory, string query, long now, LlmRules rules)
    {
        double importanceScore = memory.Importance / 100.0;
        double recencyScore = 1.0 / (1.0 + Math.Max(0, now - memory.OriginTick));
        double relevanceScore = Relevance(memory.Content, query);

        return rules.RecallImportanceWeight * importanceScore
            + rules.RecallRecencyWeight * recencyScore
            + rules.RecallRelevanceWeight * relevanceScore;
    }

    /// <summary>Sobreposição simples de termos (bag-of-words, case-insensitive) entre a
    /// consulta e o conteúdo da memória — sem stemming/semântica, determinístico e sem
    /// dependência externa.</summary>
    private static double Relevance(string content, string query)
    {
        var queryWords = Tokenize(query);
        if (queryWords.Count == 0) return 0.0;

        var contentWords = Tokenize(content);
        int shared = queryWords.Count(contentWords.Contains);
        return (double)shared / queryWords.Count;
    }

    private static HashSet<string> Tokenize(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', '!', '?', ';', ':').ToLowerInvariant())
            .Where(w => w.Length > 0)
            .ToHashSet();
}
