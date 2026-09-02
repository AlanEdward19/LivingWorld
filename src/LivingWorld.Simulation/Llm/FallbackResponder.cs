using LivingWorld.Domain.Population;

namespace LivingWorld.Simulation.Llm;

/// <summary>Resposta determinística quando o pipeline de LLM falha (Fase 11, LLM-10/11) —
/// provider indisponível/timeout, orçamento excedido ou DTO inválido. Texto de tela apenas: nunca
/// grava fato canônico novo (não toca <c>WorldState</c>, então o hash canônico do mundo é
/// idêntico ao cenário sem conversa — rules/llm-boundary.md, edge case "fallback é texto de tela,
/// não fato do mundo").</summary>
public static class FallbackResponder
{
    public static ValidatedLlmTurn Respond(Npc npc) =>
        new(
            Dialogue: $"{npc.Name} não responde no momento.",
            Emotion: "neutral",
            Intent: "none",
            ProposedActions: [],
            MemoryCandidates: []);
}
