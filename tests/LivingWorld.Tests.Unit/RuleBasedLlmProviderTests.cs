using LivingWorld.AI;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit;

/// <summary>#237: o jogo sem LLM. O NPC responde por regra a partir do estado do motor; nunca ecoa o jogador nem usa o prefixo do falso.</summary>
public class RuleBasedLlmProviderTests
{
    private static readonly string[] Intents = ["greet", "ask", "farewell"];

    private static LlmContext Ctx(string utterance, NpcDialogueState? state = null, IReadOnlyList<string>? beliefs = null) =>
        new("Ana, Farmer, Vila", utterance, Intents, BeliefFacts: beliefs, SessionId: 1, State: state);

    private static NpcDialogueState State(string? need = null, int health = 90, double? affection = null) =>
        new(health, need, affection, null, 50, 50, 50);

    private static async Task<LlmResponse> Reply(LlmContext context) => await new RuleBasedLlmProvider().GetResponseAsync(context);

    [Theory]
    [InlineData("Bom dia! Como vai?")]
    [InlineData("Tem alguma novidade por aqui?")]
    [InlineData("Preciso de ajuda.")]
    [InlineData("Posso ajudar em algo?")]
    [InlineData("Até logo.")]
    [InlineData("qualquer outra coisa")]
    public async Task Never_echoes_the_player_nor_uses_the_fake_prefix(string utterance)
    {
        var response = await Reply(Ctx(utterance, State()));
        Assert.DoesNotContain("[fake:", response.Dialogue);
        Assert.NotEqual(utterance, response.Dialogue);
        Assert.False(string.IsNullOrWhiteSpace(response.Dialogue));
        Assert.Contains(response.Intent, Intents);
    }

    [Fact]
    public async Task Same_context_same_answer()
    {
        var a = await Reply(Ctx("Bom dia! Como vai?", State("hunger")));
        var b = await Reply(Ctx("Bom dia! Como vai?", State("hunger")));
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Intent_follows_what_the_player_said()
    {
        Assert.Equal("greet", (await Reply(Ctx("Bom dia! Como vai?"))).Intent);
        Assert.Equal("ask", (await Reply(Ctx("Tem alguma novidade por aqui?"))).Intent);
        Assert.Equal("ask", (await Reply(Ctx("Preciso de ajuda."))).Intent);
        Assert.Equal("farewell", (await Reply(Ctx("Até logo."))).Intent);
    }

    [Fact]
    public async Task An_urgent_need_from_the_engine_shows_in_the_speech_and_mood()
    {
        var hungry = await Reply(Ctx("Bom dia! Como vai?", State("hunger")));
        Assert.Contains("fome", hungry.Dialogue);
        Assert.Equal("concerned", hungry.Emotion);
        var offered = await Reply(Ctx("Posso ajudar em algo?", State("hunger")));
        Assert.Contains("fome", offered.Dialogue);
        var fine = await Reply(Ctx("Bom dia! Como vai?", State()));
        Assert.DoesNotContain("fome", fine.Dialogue);
    }

    [Fact]
    public async Task Affection_changes_the_tone()
    {
        var friend = await Reply(Ctx("Bom dia! Como vai?", State(affection: 80)));
        var stranger = await Reply(Ctx("Bom dia! Como vai?", State(affection: 0)));
        Assert.Equal("friendly", friend.Emotion);
        Assert.Equal("neutral", stranger.Emotion);
        Assert.NotEqual(friend.Dialogue, stranger.Dialogue);
    }

    [Fact]
    public async Task News_uses_only_what_the_npc_believes()
    {
        var withBelief = await Reply(Ctx("Tem alguma novidade por aqui?", State(), ["o ferreiro casou"]));
        Assert.Contains("O ferreiro casou", withBelief.Dialogue);
        var none = await Reply(Ctx("Tem alguma novidade por aqui?", State()));
        Assert.DoesNotContain("casou", none.Dialogue);
    }

    [Fact]
    public async Task Respects_the_allowed_intents_and_proposes_nothing()
    {
        var restricted = new LlmContext("Ana", "Bom dia!", ["ask"], State: State());
        var response = await new RuleBasedLlmProvider().GetResponseAsync(restricted);
        Assert.Equal("ask", response.Intent);
        Assert.Empty(response.ProposedActions);
        Assert.Empty(response.MemoryCandidates);
    }

    [Fact]
    public void The_assembler_hands_over_the_npc_state_read_only()
    {
        var (world, _) = ScenarioRunner.Create(3);
        var npc = world.Npcs[0];
        var session = new ConversationSession(SessionId: 4, npc.Id, OpenedAtTick: 10, LastTurnTick: 10, IsActive: true);
        var context = LlmContextAssembler.Assemble(world, npc, session, "Bom dia!", Intents, [], tick: 10);
        Assert.NotNull(context.State);
        Assert.Equal(npc.Health, context.State!.Health);
        Assert.Null(context.State.Affection); // desconhecidos: ler o estado não cria relação
        Assert.Equal(npc.Personality.Agreeableness, context.State.Agreeableness);
    }

    private static NpcDialogueState At(int hour, string? relation, double? affection = null) =>
        new(90, null, affection, null, 50, 50, 50, relation, hour, 24);

    /// <summary>#260: o cônjuge não é tratado como desconhecido, e ninguém diz "Bom dia!" à meia-noite.</summary>
    [Fact]
    public async Task A_spouse_at_midnight_gets_an_intimate_late_greeting_not_good_morning_nor_a_stranger_offer_of_help()
    {
        var spouse = await Reply(Ctx("Oi! Como vai?", At(0, "spouse", 60)));

        Assert.DoesNotContain("Bom dia", spouse.Dialogue);
        Assert.DoesNotContain("Em que posso ajudar", spouse.Dialogue);
        Assert.Contains("meu bem", spouse.Dialogue);
        Assert.Equal("friendly", spouse.Emotion);
    }

    [Theory]
    [InlineData(8, "Bom dia")]
    [InlineData(15, "Boa tarde")]
    [InlineData(20, "Boa noite")]
    [InlineData(2, "Boa noite")]
    public async Task A_stranger_is_greeted_by_the_part_of_the_day_the_world_clock_says(int hour, string expected)
    {
        var response = await Reply(Ctx("Oi! Como vai?", At(hour, null)));

        Assert.Contains(expected, response.Dialogue);
    }

    [Fact]
    public async Task The_spouse_news_reply_stays_at_home_and_the_greeting_follows_the_time_for_the_spouse_too()
    {
        var morning = await Reply(Ctx("Bom dia! Como vai?", At(8, "spouse", 60)));
        var news = await Reply(Ctx("Tem alguma novidade por aqui?", At(14, "spouse", 60)));

        Assert.Contains("Bom dia", morning.Dialogue);
        Assert.DoesNotContain("Em que posso ajudar", morning.Dialogue);
        Assert.Contains(news.Dialogue, new[] { "Nada de novo que eu tenha ouvido, meu bem.", "Por aqui em casa, o dia segue como sempre." });
    }
}
