using LivingWorld.Domain.Llm;
using LivingWorld.Simulation.Llm;

namespace LivingWorld.Tests.Unit.Llm;

/// <summary>Fase 11, T5 (LLM-07/08), story "Validação estrita e aplicação controlada": <see
/// cref="LlmResponseValidator"/> — schema + <c>proposedActions</c> subconjunto de
/// <c>AllowedActions</c>; qualquer falha rejeita a resposta inteira, nunca autocorrige um campo
/// isolado (edge case: "DTO sem dialogue, com emotion desconhecida ou com JSON truncado ->
/// rejeitado inteiro").</summary>
public class LlmResponseValidatorTests
{
    private static readonly string[] KnownEmotions = ["neutral", "concerned", "happy"];

    private static LlmContext MakeContext(IReadOnlyList<string>? allowedActions = null) =>
        new(NpcKnowledgeSummary: "npc", PlayerUtterance: "oi", AllowedIntents: ["greet"], AllowedActions: allowedActions ?? ["greet"]);

    public LlmResponseValidatorTests() => LlmResponseValidator.EnforceValidation = true;

    [Fact]
    public void Valid_schema_and_allowed_action_is_accepted()
    {
        var response = new LlmResponse("oi, tudo bem?", "neutral", "greet", ["greet"], []);

        var result = LlmResponseValidator.Validate(response, MakeContext(), KnownEmotions);

        Assert.True(result.IsValid);
        Assert.Equal(LlmValidationFailure.None, result.Failure);
        Assert.NotNull(result.Turn);
        Assert.Equal("oi, tudo bem?", result.Turn!.Dialogue);
    }

    [Fact]
    public void Missing_dialogue_is_rejected_entirely()
    {
        var response = new LlmResponse("", "neutral", "greet", ["greet"], []);

        var result = LlmResponseValidator.Validate(response, MakeContext(), KnownEmotions);

        Assert.False(result.IsValid);
        Assert.Equal(LlmValidationFailure.MissingDialogue, result.Failure);
        Assert.Null(result.Turn);
    }

    [Fact]
    public void Unknown_emotion_is_rejected_entirely()
    {
        var response = new LlmResponse("oi", "furious", "greet", ["greet"], []);

        var result = LlmResponseValidator.Validate(response, MakeContext(), KnownEmotions);

        Assert.False(result.IsValid);
        Assert.Equal(LlmValidationFailure.UnknownEmotion, result.Failure);
        Assert.Null(result.Turn);
    }

    [Fact]
    public void Truncated_dto_with_null_proposed_actions_is_rejected_entirely()
    {
        var response = new LlmResponse("oi", "neutral", "greet", null!, []);

        var result = LlmResponseValidator.Validate(response, MakeContext(), KnownEmotions);

        Assert.False(result.IsValid);
        Assert.Equal(LlmValidationFailure.Truncated, result.Failure);
        Assert.Null(result.Turn);
    }

    [Fact]
    public void Action_outside_allowed_actions_is_rejected_entirely()
    {
        var response = new LlmResponse("oi", "neutral", "greet", ["attack_player"], []);

        var result = LlmResponseValidator.Validate(response, MakeContext(allowedActions: ["greet"]), KnownEmotions);

        Assert.False(result.IsValid);
        Assert.Equal(LlmValidationFailure.ActionNotAllowed, result.Failure);
        Assert.Null(result.Turn);
    }

    [Fact]
    public void Action_inside_allowed_actions_is_accepted()
    {
        var response = new LlmResponse("oi", "neutral", "greet", ["greet", "trade"], []);

        var result = LlmResponseValidator.Validate(response, MakeContext(allowedActions: ["greet", "trade"]), KnownEmotions);

        Assert.True(result.IsValid);
        Assert.Equal(LlmValidationFailure.None, result.Failure);
    }

    /// <summary>Par de mutação exigido pela spec (P2, "flag de teste desliga validador -> critério
    /// de segurança deve falhar"): com <see cref="LlmResponseValidator.EnforceValidation"/>
    /// desligada, uma resposta que deveria ser rejeitada (ação fora de AllowedActions) passa —
    /// prova que o mecanismo de desligar existe e realmente muda o resultado. A garantia real de
    /// segurança (rodar sempre ligado em produção) é critério de T8, fora desta task.</summary>
    [Fact]
    public void Disabling_the_validator_lets_an_otherwise_rejected_response_through()
    {
        var response = new LlmResponse("oi", "neutral", "greet", ["attack_player"], []);

        try
        {
            LlmResponseValidator.EnforceValidation = false;
            var result = LlmResponseValidator.Validate(response, MakeContext(allowedActions: ["greet"]), KnownEmotions);

            Assert.True(result.IsValid);
        }
        finally
        {
            LlmResponseValidator.EnforceValidation = true;
        }
    }
}
