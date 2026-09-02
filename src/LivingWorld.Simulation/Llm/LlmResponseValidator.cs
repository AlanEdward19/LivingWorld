using LivingWorld.Domain.Llm;

namespace LivingWorld.Simulation.Llm;

/// <summary>Motivo da rejeição total de uma <see cref="LlmResponse"/> (Fase 11, LLM-07/08) —
/// nunca um bool solto, mesmo espírito de <c>ConversationStartDecision</c>.</summary>
public enum LlmValidationFailure
{
    None,
    MissingDialogue,
    UnknownEmotion,
    Truncated,
    ActionNotAllowed,
}

/// <summary>Turno já validado, pronto para <c>ConversationEffectsApplier</c> (T6) — mesmo molde
/// do <c>ValidatedLlmTurn</c> do design.md.</summary>
public sealed record ValidatedLlmTurn(
    string Dialogue,
    string Emotion,
    string Intent,
    IReadOnlyList<string> ProposedActions,
    IReadOnlyList<MemoryCandidate> MemoryCandidates);

public sealed record LlmValidationResult(bool IsValid, LlmValidationFailure Failure, ValidatedLlmTurn? Turn);

/// <summary>Pipeline único de validação da saída da LLM (Fase 11, LLM-07/08, story "Validação
/// estrita e aplicação controlada" ACs 1-2; edge case "DTO sem dialogue, com emotion desconhecida
/// ou com JSON truncado -> rejeitado inteiro"): schema (dialogue/emotion/coleções presentes) e
/// <c>proposedActions</c> subconjunto de <c>AllowedActions(npc, ctx)</c> (<see
/// cref="LlmContext.AllowedActions"/>). Qualquer falha em qualquer etapa rejeita a resposta
/// inteira — nunca autocorrige um campo isolado (rules/llm-boundary.md).</summary>
public static class LlmResponseValidator
{
    /// <summary>Liga/desliga o validador — só para o par de mutação exigido pela spec (P2,
    /// "flag de teste desliga validador -> critério de segurança deve falhar", LLM-14 AC3/T8).
    /// Nunca desligado em uso normal: todo caminho de produção deixa o valor default (ligado).</summary>
    public static bool EnforceValidation { get; set; } = true;

    public static LlmValidationResult Validate(LlmResponse response, LlmContext context, IReadOnlyList<string> knownEmotions)
    {
        if (!EnforceValidation)
            return new LlmValidationResult(true, LlmValidationFailure.None, ToTurn(response));

        if (string.IsNullOrEmpty(response.Dialogue))
            return Reject(LlmValidationFailure.MissingDialogue);

        if (response.Emotion is null || !knownEmotions.Contains(response.Emotion))
            return Reject(LlmValidationFailure.UnknownEmotion);

        if (response.ProposedActions is null || response.MemoryCandidates is null)
            return Reject(LlmValidationFailure.Truncated);

        var allowedActions = context.AllowedActions ?? [];
        if (response.ProposedActions.Any(action => !allowedActions.Contains(action)))
            return Reject(LlmValidationFailure.ActionNotAllowed);

        return new LlmValidationResult(true, LlmValidationFailure.None, ToTurn(response));
    }

    private static LlmValidationResult Reject(LlmValidationFailure failure) =>
        new(false, failure, null);

    private static ValidatedLlmTurn ToTurn(LlmResponse response) =>
        new(response.Dialogue, response.Emotion, response.Intent, response.ProposedActions, response.MemoryCandidates);
}
