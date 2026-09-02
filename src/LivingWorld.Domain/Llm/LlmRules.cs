using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Llm;

/// <summary>Resultado de <c>StartConversation(npcId)</c> (Fase 11, LLM-01) — motivo determinístico
/// de recusa, nunca um bool solto de "aceitou/recusou".</summary>
public enum ConversationStartDecision
{
    Accepted,
    RejectedBusy,
    RejectedHostile,
    RejectedUnavailable,
}

/// <summary>Compatibilidade entre conversar e a <see cref="ActionType"/> corrente do NPC
/// (Fase 11, LLM-02) — política oportunista: só <see cref="Forbidden"/> impede aceitar a
/// conversa; <see cref="RequiresPause"/> aceita e pausa a ação, <see cref="Compatible"/> aceita
/// e mantém a ação rodando.</summary>
public enum ConversationCompatibility
{
    Compatible,
    RequiresPause,
    Forbidden,
}

/// <summary>Parâmetros de disponibilidade social para conversa com LLM (Fase 11, LLM-01/02) e
/// pesos/limiar de memória (Fase 11, roadmap itens 1/2, LLM-04/05) — cenário-driven, nenhum
/// literal em C#, mesmo padrão de <see cref="NeedsRules"/>. Os quatro últimos parâmetros têm
/// default para não quebrar chamadas existentes de <see cref="Create"/> anteriores a esta
/// task.</summary>
/// <remarks>Default usado quando o chamador (ex.: montagem de contexto de LLM em
/// <c>LivingWorld.Simulation</c>) não tem cenário próprio ainda: ver <see cref="Default"/>.</remarks>
/// <param name="RecallImportanceWeight">Peso da importância (0-100 normalizada) no score do
/// <c>Recall</c>.</param>
/// <param name="RecallRecencyWeight">Peso da recência (quão perto do tick atual) no score.</param>
/// <param name="RecallRelevanceWeight">Peso da sobreposição de termos com a consulta no
/// score.</param>
/// <param name="CanonicalMemoryImportanceThreshold">Memória com importância >= este limiar é
/// canônica (<see cref="CanonicalAttribute"/>, ADR-0014); abaixo é volátil e compactável
/// livremente.</param>
public sealed record LlmRules(
    double HostileTrustThreshold,
    IReadOnlyDictionary<ActionType, ConversationCompatibility> ActionCompatibility,
    double RecallImportanceWeight = 1.0,
    double RecallRecencyWeight = 1.0,
    double RecallRelevanceWeight = 1.0,
    int CanonicalMemoryImportanceThreshold = 50)
{
    public static Result<LlmRules> Create(
        double hostileTrustThreshold, IReadOnlyDictionary<ActionType, ConversationCompatibility> actionCompatibility,
        double recallImportanceWeight = 1.0, double recallRecencyWeight = 1.0, double recallRelevanceWeight = 1.0,
        int canonicalMemoryImportanceThreshold = 50)
    {
        if (hostileTrustThreshold is < 0 or > 100)
            return Result<LlmRules>.Fail("HostileTrustThreshold: fora de [0,100]");

        foreach (var action in Enum.GetValues<ActionType>())
            if (!actionCompatibility.ContainsKey(action))
                return Result<LlmRules>.Fail($"ActionCompatibility: falta entrada declarada para {action}");

        if (recallImportanceWeight < 0)
            return Result<LlmRules>.Fail("RecallImportanceWeight: deve ser >= 0");
        if (recallRecencyWeight < 0)
            return Result<LlmRules>.Fail("RecallRecencyWeight: deve ser >= 0");
        if (recallRelevanceWeight < 0)
            return Result<LlmRules>.Fail("RecallRelevanceWeight: deve ser >= 0");
        if (canonicalMemoryImportanceThreshold is < 0 or > 100)
            return Result<LlmRules>.Fail("CanonicalMemoryImportanceThreshold: fora de [0,100]");

        return Result<LlmRules>.Ok(new LlmRules(
            hostileTrustThreshold, actionCompatibility,
            recallImportanceWeight, recallRecencyWeight, recallRelevanceWeight, canonicalMemoryImportanceThreshold));
    }

    /// <summary>Default usado quando o chamador não tem cenário próprio ainda — todo-Compatible,
    /// pesos iguais, limiar canônico no meio da escala.</summary>
    public static readonly LlmRules Default = Create(
        hostileTrustThreshold: 20,
        actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => ConversationCompatibility.Compatible)).Value
        ?? throw new InvalidOperationException("LlmRules.Default inválida — bug no cenário");
}
