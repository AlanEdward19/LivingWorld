namespace LivingWorld.AI;

/// <summary>Contexto somente-leitura montado a partir do conhecimento do NPC — nunca do
/// estado global (rules/llm-boundary.md). O provider não tem como escrever no mundo.</summary>
public sealed record LlmContext(
    string NpcKnowledgeSummary,
    string PlayerUtterance,
    IReadOnlyList<string> AllowedIntents);

public sealed record MemoryCandidate(string Event, int Importance);

/// <summary>DTO tipado e validável — nunca aplicado direto ao mundo (ADR-0004).</summary>
public sealed record LlmResponse(
    string Dialogue,
    string Emotion,
    string Intent,
    IReadOnlyList<string> ProposedActions,
    IReadOnlyList<MemoryCandidate> MemoryCandidates);

/// <summary>Fronteira única de saída da LLM. Toda chamada é opcional por design — quem
/// implementa nunca pode escrever no mundo, só propor.</summary>
public interface ILlmProvider
{
    Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default);
}
