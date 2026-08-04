using LivingWorld.Domain.Llm;

namespace LivingWorld.AI;

/// <summary>Provider determinístico e injetivo para testes e para o gate (ADR-0004): mesma
/// entrada sempre dá a mesma saída; entradas distintas dão saídas distintas; nunca chama rede.</summary>
public sealed class FakeLlmProvider : ILlmProvider
{
    private static readonly string[] Emotions = ["neutral", "concerned", "happy", "annoyed", "curious", "afraid"];

    public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default)
    {
        // Fase 11 (LLM-04/05): os campos novos de LlmContext (crença, memória, ações
        // permitidas, sessão) entram no hash também — senão dois contextos que só divergem
        // neles produziriam a mesma saída, quebrando a garantia "entradas distintas dão saídas
        // distintas" do resumo da classe.
        string extra = string.Concat(
            string.Join("", context.BeliefFacts ?? []),
            string.Join("", (context.RelevantMemories ?? []).Select(m => $"{m.Event}:{m.Importance}")),
            string.Join("", context.AllowedActions ?? []),
            context.SessionId?.ToString() ?? "",
            context.SessionOpenedAtTick?.ToString() ?? "");

        ulong hash = StableHash(context.NpcKnowledgeSummary + "" + context.PlayerUtterance + extra);

        var emotion = Emotions[(int)(hash % (ulong)Emotions.Length)];
        var intent = context.AllowedIntents.Count == 0
            ? "none"
            : context.AllowedIntents[(int)(hash % (ulong)context.AllowedIntents.Count)];
        var dialogue = $"[fake:{hash:x16}] {context.PlayerUtterance}";

        return Task.FromResult(new LlmResponse(dialogue, emotion, intent, [], []));
    }

    /// <summary>FNV-1a 64-bit. Nunca <c>string.GetHashCode()</c> — este varia entre processos
    /// no .NET (randomização de hash), o que quebraria "mesma entrada, mesma saída".</summary>
    private static ulong StableHash(string value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offsetBasis;
        foreach (var ch in value)
        {
            hash ^= ch;
            hash *= prime;
        }
        return hash;
    }
}
