namespace LivingWorld.Domain.Llm;

/// <summary>Contexto somente-leitura montado a partir do conhecimento do NPC — nunca do
/// estado global (rules/llm-boundary.md). O provider não tem como escrever no mundo.
/// <paramref name="NpcKnowledgeSummary"/>, <paramref name="PlayerUtterance"/> e <paramref
/// name="AllowedIntents"/> são o contrato original; os demais são opcionais —
/// todo campo novo tem default seguro, então nenhum código existente que constrói
/// <see cref="LlmContext"/> só com os 3 primeiros campos precisa mudar.</summary>
/// <param name="BeliefFacts">Relatos de crença do NPC — nunca a versão de Verdade
/// quando as duas divergem. Quem monta o contexto (fora do escopo desta task) é
/// responsável por só chamar a consulta de Crença.</param>
/// <param name="RelevantMemories">Memórias recuperadas por relevância (Recall) já filtradas pelo
/// conhecimento do NPC — mesmo formato de <see cref="MemoryCandidate"/> usado na saída,
/// reaproveitado aqui como entrada.</param>
/// <param name="AllowedActions">Ações que este NPC pode legitimamente executar neste contexto —
/// o <c>proposedActions</c> da resposta é validado contra esta lista (fora do escopo desta task;
/// só o transporte é adicionado aqui).</param>
/// <param name="SessionId">Metadados da <c>ConversationSession</c> —
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
    long? SessionOpenedAtTick = null,
    NpcDialogueState? State = null);

/// <summary>#237: o que o motor já sabe do NPC e da relação dele com quem fala, lido do estado (nunca inventado pelo
/// provider): é com isto que o provider "sem LLM" monta a fala. Opcional — quem não o preenche mantém o contrato antigo.</summary>
/// <param name="UrgentNeed">Necessidade mais urgente (<c>hunger</c>, <c>thirst</c>, <c>sleep</c> ou <c>social</c>); null se nenhuma aperta.</param>
/// <param name="Affection">Afeto do NPC por quem fala (0 a 100; 0 = desconhecidos); null se ainda não se conhecem.</param>
/// <param name="Relation">#260: o laço de quem fala com o NPC, lido da família do estado (<c>spouse</c>, <c>parent</c>, <c>child</c>); null para quem não é da família.</param>
/// <param name="HourOfDay">#260: a hora do dia do mundo agora (0 a <paramref name="HoursPerDay"/> menos um): a saudação não diz "Bom dia" à meia-noite.</param>
public sealed record NpcDialogueState(
    int Health,
    string? UrgentNeed,
    double? Affection,
    double? Trust,
    int Agreeableness,
    int Extroversion,
    int Altruism,
    string? Relation = null,
    int? HourOfDay = null,
    int HoursPerDay = 24);

public sealed record MemoryCandidate(string Event, int Importance);

/// <summary>DTO tipado e validável — nunca aplicado direto ao mundo.</summary>
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
