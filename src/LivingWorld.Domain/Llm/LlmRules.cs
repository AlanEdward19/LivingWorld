namespace LivingWorld.Domain;

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

/// <summary>Parâmetros de disponibilidade social para conversa com LLM (Fase 11, LLM-01/02),
/// cenário-driven — nenhum literal em C#, mesmo padrão de <see cref="NeedsRules"/>.</summary>
public sealed record LlmRules(
    double HostileTrustThreshold,
    IReadOnlyDictionary<ActionType, ConversationCompatibility> ActionCompatibility)
{
    public static Result<LlmRules> Create(
        double hostileTrustThreshold, IReadOnlyDictionary<ActionType, ConversationCompatibility> actionCompatibility)
    {
        if (hostileTrustThreshold is < 0 or > 100)
            return Result<LlmRules>.Fail("HostileTrustThreshold: fora de [0,100]");

        foreach (var action in Enum.GetValues<ActionType>())
            if (!actionCompatibility.ContainsKey(action))
                return Result<LlmRules>.Fail($"ActionCompatibility: falta entrada declarada para {action}");

        return Result<LlmRules>.Ok(new LlmRules(hostileTrustThreshold, actionCompatibility));
    }
}
