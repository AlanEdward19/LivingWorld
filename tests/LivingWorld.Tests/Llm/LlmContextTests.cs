using LivingWorld.AI;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Tests.Llm;

/// <summary>Fase 11, LLM-04/05: <see cref="LlmContext"/> ganha crença, memória relevante, ações
/// permitidas e metadados de sessão sem quebrar o contrato de leitura original (ADR-0004) — todo
/// código existente que só monta os 3 campos originais continua compilando e se comportando
/// igual.</summary>
public class LlmContextTests
{
    private static LlmContext BaseContext(string utterance) =>
        new(NpcKnowledgeSummary: "npc conhece a vila e a família", PlayerUtterance: utterance, AllowedIntents: ["warn_player", "greet", "trade"]);

    [Fact]
    public void New_fields_default_to_null_when_only_the_original_three_are_set()
    {
        var context = BaseContext("oi");

        Assert.Null(context.BeliefFacts);
        Assert.Null(context.RelevantMemories);
        Assert.Null(context.AllowedActions);
        Assert.Null(context.SessionId);
        Assert.Null(context.SessionOpenedAtTick);
    }

    [Fact]
    public async Task FakeLlmProvider_is_unaffected_by_omitting_the_new_fields()
    {
        // Par de mutação implícito com o teste abaixo: sem esta linha de base, não daria pra
        // provar que variar só os campos novos muda a saída (poderia já mudar por acaso).
        var provider = new FakeLlmProvider();
        var context = BaseContext("mesma pergunta");

        var a = await provider.GetResponseAsync(context);
        var b = await provider.GetResponseAsync(context);

        Assert.Equal(a, b);
    }

    [Fact]
    public async Task FakeLlmProvider_produces_a_different_response_when_only_belief_facts_differ()
    {
        var provider = new FakeLlmProvider();
        var withoutBelief = BaseContext("mesma pergunta");
        var withBelief = withoutBelief with { BeliefFacts = ["a colheita falhou este ano"] };

        var a = await provider.GetResponseAsync(withoutBelief);
        var b = await provider.GetResponseAsync(withBelief);

        Assert.NotEqual(a.Dialogue, b.Dialogue);
    }

    [Fact]
    public async Task FakeLlmProvider_produces_a_different_response_when_only_session_metadata_differs()
    {
        var provider = new FakeLlmProvider();
        var context = BaseContext("mesma pergunta");

        var a = await provider.GetResponseAsync(context with { SessionId = 1, SessionOpenedAtTick = 100 });
        var b = await provider.GetResponseAsync(context with { SessionId = 2, SessionOpenedAtTick = 100 });

        Assert.NotEqual(a.Dialogue, b.Dialogue);
    }

    [Fact]
    public async Task NullLlmProvider_still_ignores_context_including_the_new_fields()
    {
        var provider = new NullLlmProvider();
        var plain = BaseContext("oi");
        var full = plain with
        {
            BeliefFacts = ["segredo"],
            RelevantMemories = [new MemoryCandidate("evento", 50)],
            AllowedActions = ["greet"],
            SessionId = 7,
            SessionOpenedAtTick = 3,
        };

        var a = await provider.GetResponseAsync(plain);
        var b = await provider.GetResponseAsync(full);

        Assert.Equal(a, b);
    }
}
