using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api.Conversation;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Llm;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Llm;

/// <summary>Fase 11, T7 (LLM-01..03, story "Sessão de conversa segura", todas as ACs):
/// <c>POST /conversations/start|send|end</c> — mesmo padrão de <c>NpcEndpointTests</c>
/// (<c>WebApplicationFactory&lt;Program&gt;</c>). Cada teste usa um <c>NpcId</c> distinto (o
/// mundo é compartilhado entre os testes da classe, via <c>IClassFixture</c>) para não
/// interferir no estado de outro cenário. Factory própria: muta
/// <c>ConversationSessionStore</c> e ações de NPC no host — não pode compartilhar a
/// collection de endpoints de leitura.</summary>
public class ConversationEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;
    private readonly WorldState _world;
    private readonly ConversationSessionStore _sessions;

    public ConversationEndpointTests(LivingWorldApiFactory factory)
    {
        _factory = factory;
        _world = factory.Services.GetRequiredService<WorldState>();
        _sessions = factory.Services.GetRequiredService<ConversationSessionStore>();
    }

    private Npc NpcById(long id) =>
        _world.Npcs.First(n => n.Id == new NpcId(id));

    [Fact]
    public async Task Start_accepted_returns_200_with_a_session_id_and_keeps_the_npcs_current_action()
    {
        var client = _factory.CreateClient();
        var npc = NpcById(0);
        var actionBefore = npc.CurrentAction;

        var response = await client.PostAsJsonAsync("/conversations/start", new ConversationStartRequest(0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ConversationStartResponse>();
        Assert.Equal("Accepted", body!.Decision);
        Assert.NotNull(body.SessionId);
        Assert.Equal(actionBefore, npc.CurrentAction);
    }

    [Fact]
    public async Task Start_rejected_returns_200_with_a_reason_no_session_and_does_not_change_the_npcs_action()
    {
        var client = _factory.CreateClient();
        var npc = NpcById(1);
        npc.SetCurrentAction(ActionType.Sleep, tick: 0);

        var response = await client.PostAsJsonAsync("/conversations/start", new ConversationStartRequest(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ConversationStartResponse>();
        Assert.StartsWith("Rejected", body!.Decision);
        Assert.Null(body.SessionId);
        Assert.Equal(ActionType.Sleep, npc.CurrentAction);
    }

    [Fact]
    public async Task Start_for_an_unknown_npc_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/conversations/start", new ConversationStartRequest(999_999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_on_an_accepted_session_returns_200_with_a_validated_turn()
    {
        var client = _factory.CreateClient();
        var start = await client.PostAsJsonAsync("/conversations/start", new ConversationStartRequest(2));
        var session = (await start.Content.ReadFromJsonAsync<ConversationStartResponse>())!;

        var response = await client.PostAsJsonAsync("/conversations/send", new ConversationSendRequest(session.SessionId!.Value, "oi"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ConversationSendResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Dialogue);
    }

    [Fact]
    public async Task Send_on_a_session_that_does_not_exist_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/conversations/send", new ConversationSendRequest(999_999, "oi"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_on_an_ended_session_returns_409()
    {
        var client = _factory.CreateClient();
        var start = await client.PostAsJsonAsync("/conversations/start", new ConversationStartRequest(3));
        var session = (await start.Content.ReadFromJsonAsync<ConversationStartResponse>())!;
        await client.PostAsJsonAsync("/conversations/end", new ConversationEndRequest(session.SessionId!.Value));

        var response = await client.PostAsJsonAsync("/conversations/send", new ConversationSendRequest(session.SessionId.Value, "ainda aí?"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task End_returns_200_and_keeps_turn_history_in_the_store()
    {
        var client = _factory.CreateClient();
        var start = await client.PostAsJsonAsync("/conversations/start", new ConversationStartRequest(4));
        var session = (await start.Content.ReadFromJsonAsync<ConversationStartResponse>())!;
        await client.PostAsJsonAsync("/conversations/send", new ConversationSendRequest(session.SessionId!.Value, "oi"));

        var response = await client.PostAsJsonAsync("/conversations/end", new ConversationEndRequest(session.SessionId.Value));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(_sessions.Find(session.SessionId.Value)!.IsActive);
        Assert.Single(_sessions.TurnsOf(session.SessionId.Value));
    }

    [Fact]
    public async Task End_for_a_session_that_does_not_exist_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/conversations/end", new ConversationEndRequest(999_999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Npc_dying_during_an_open_session_ends_it_and_blocks_new_turns()
    {
        var client = _factory.CreateClient();
        var npc = NpcById(5);
        var start = await client.PostAsJsonAsync("/conversations/start", new ConversationStartRequest(5));
        var session = (await start.Content.ReadFromJsonAsync<ConversationStartResponse>())!;

        npc.Die(npc.BirthDate);

        var response = await client.PostAsJsonAsync("/conversations/send", new ConversationSendRequest(session.SessionId!.Value, "oi"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(_sessions.Find(session.SessionId.Value)!.IsActive);
    }
}
