using LivingWorld.AI;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Tests.Unit;

public class FakeLlmProviderTests
{
    private static LlmContext ContextFor(string utterance) =>
        new(NpcKnowledgeSummary: "npc conhece a vila e a família", PlayerUtterance: utterance, AllowedIntents: ["warn_player", "greet", "trade"]);

    [Fact]
    public async Task Twenty_distinct_inputs_produce_twenty_distinct_outputs()
    {
        var provider = new FakeLlmProvider();
        var outputs = new HashSet<string>();

        for (int i = 0; i < 20; i++)
        {
            var response = await provider.GetResponseAsync(ContextFor($"fala número {i}"));
            outputs.Add(response.Dialogue + "|" + response.Emotion + "|" + response.Intent);
        }

        Assert.Equal(20, outputs.Count);
    }

    [Fact]
    public async Task Changing_one_character_changes_the_output()
    {
        var provider = new FakeLlmProvider();

        var a = await provider.GetResponseAsync(ContextFor("bom dia, viajante"));
        var b = await provider.GetResponseAsync(ContextFor("bom dia, viajanta"));

        Assert.NotEqual(a.Dialogue, b.Dialogue);
    }

    [Fact]
    public async Task Same_input_produces_same_output()
    {
        var provider = new FakeLlmProvider();
        var context = ContextFor("a mesma pergunta de sempre");

        var a = await provider.GetResponseAsync(context);
        var b = await provider.GetResponseAsync(context);

        Assert.Equal(a, b);
    }

    [Fact]
    public async Task A_constant_provider_would_fail_both_distinctness_checks()
    {
        // Par de mutação: prova que os dois testes de cima medem alguma coisa.
        var constant = new NullLlmProvider();

        var outputs = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            var response = await constant.GetResponseAsync(ContextFor($"fala número {i}"));
            outputs.Add(response.Dialogue);
        }
        Assert.Single(outputs);

        var a = await constant.GetResponseAsync(ContextFor("bom dia, viajante"));
        var b = await constant.GetResponseAsync(ContextFor("bom dia, viajanta"));
        Assert.Equal(a.Dialogue, b.Dialogue);
    }
}
