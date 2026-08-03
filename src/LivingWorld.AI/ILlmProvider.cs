namespace LivingWorld.AI;

/// <summary>Contexto somente-leitura montado a partir do conhecimento do NPC — nunca do
/// estado global (rules/llm-boundary.md). O provider não tem como escrever no mundo.
/// <paramref name="NpcKnowledgeSummary"/>, <paramref name="PlayerUtterance"/> e <paramref
/// name="AllowedIntents"/> são o contrato original (ADR-0004); os demais são opcionais (Fase 11,
/// LLM-04/05) — todo campo novo tem default seguro, então nenhum código existente que constrói
/// <see cref="LlmContext"/> só com os 3 primeiros campos precisa mudar.</summary>
/// <param name="BeliefFacts">Relatos de crença do NPC (Fase 10) — nunca a versão de Verdade
/// quando as duas divergem (LLM-05). Quem monta o contexto (fora do escopo desta task) é
/// responsável por só chamar a consulta de Crença.</param>
/// <param name="RelevantMemories">Memórias recuperadas por relevância (Recall) já filtradas pelo
/// conhecimento do NPC — mesmo formato de <see cref="MemoryCandidate"/> usado na saída,
/// reaproveitado aqui como entrada.</param>
/// <param name="AllowedActions">Ações que este NPC pode legitimamente executar neste contexto —
/// o <c>proposedActions</c> da resposta é validado contra esta lista (fora do escopo desta task;
/// só o transporte é adicionado aqui).</param>
/// <param name="SessionId">Metadados da <c>ConversationSession</c> (Fase 11, LLM-03) —
/// <c>long?</c> em vez de referenciar o tipo de <c>LivingWorld.Simulation</c> diretamente:
/// <c>LivingWorld.AI</c> não tem (e não deve ganhar) referência a Simulation, só ao snapshot
/// somente-leitura que chega por aqui.</param>
public sealed record LlmContext(
    string NpcKnowledgeSummary,
    string PlayerUtterance,
    IReadOnlyList<string> AllowedIntents,
    IReadOnlyList<string>? BeliefFacts = null,
    IReadOnlyList<MemoryCandidate>? RelevantMemories = null,
    IReadOnlyList<string>? AllowedActions = null,
    long? SessionId = null,
    long? SessionOpenedAtTick = null);

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
