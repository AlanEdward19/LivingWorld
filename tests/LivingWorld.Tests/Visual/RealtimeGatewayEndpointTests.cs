using System.Net;
using System.Net.Http.Json;
using System.Text;
using LivingWorld.Api.Visual;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T3 (VTT-02, VTT-10): subscribe/replay do gateway realtime — permissão
/// espectador-vs-personagem no escopo world (spec.md edge case "assina escopo sem permissão"),
/// replay por cursor após reconexão, e o invariante de não-escrita (hash canônico do mundo
/// inalterado por leituras realtime).</summary>
public class RealtimeGatewayEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RealtimeGatewayEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Subscribe_to_world_scope_as_spectator_returns_200_with_a_snapshot_envelope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/visual/subscribe?scope=World&mode=Spectator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<VisualSnapshotEnvelope<object?>>();
        Assert.NotNull(envelope);
        Assert.Equal(VisualScopeKind.World, envelope!.Scope.Kind);
        Assert.Equal("world", envelope.Cursor.ScopeKey);
    }

    [Fact]
    public async Task Subscribe_to_world_scope_as_player_is_denied_with_403_and_no_body()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/visual/subscribe?scope=World&mode=Player");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task Replay_with_no_prior_pushes_returns_an_empty_list_for_an_authorized_scope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/visual/replay?scope=City&refId=1&mode=Spectator&sinceTick=0&sinceSequence=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deltas = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(deltas);
        Assert.Empty(deltas!);
    }

    [Fact]
    public async Task Replay_for_an_unauthorized_scope_is_denied_with_403()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/visual/replay?scope=World&mode=Player&sinceTick=0&sinceSequence=0");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Subscribing_and_replaying_never_changes_the_canonical_world_hash()
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        var client = _factory.CreateClient();
        await client.GetAsync("/visual/subscribe?scope=World&mode=Spectator");
        await client.GetAsync("/visual/replay?scope=World&mode=Spectator&sinceTick=0&sinceSequence=0");

        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public async Task Websocket_subscribe_to_an_unauthorized_scope_is_rejected_with_403_before_upgrade()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/visual/ws?scope=World&mode=Player");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Websocket_subscribe_delivers_the_initial_snapshot_as_the_first_frame()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var wsUri = new UriBuilder(new Uri(_factory.Server.BaseAddress, "/visual/ws?scope=World&mode=Spectator"))
        {
            Scheme = "ws"
        }.Uri;

        using var socket = await wsClient.ConnectAsync(wsUri, CancellationToken.None);
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

        Assert.Contains("\"scopeKey\":\"world\"", json);
    }
}
