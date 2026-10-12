using System.Text;
using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>godot-server-deltas C1–C4: o servidor empurra delta de interior (sem o cliente reler).</summary>
public class InteriorDeltaTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public InteriorDeltaTests(LivingWorldApiFactory factory) => _factory = factory;

    private (WorldState World, Building Building, IReadOnlyList<LivingWorld.Domain.Population.Npc> Npcs, VisualScope Scope) Setup()
    {
        var world = _factory.Services.GetRequiredService<WorldState>();
        var building = world.Buildings.First();
        foreach (var n in world.Npcs.Where(n => n.Interior is not null).ToArray()) n.ExitBuilding();
        var npcs = world.Npcs.Where(n => n.IsAlive).Take(2).ToArray();
        return (world, building, npcs, new VisualScope(VisualScopeKind.Interior, building.Id.Value.ToString()));
    }

    private static LivingWorld.Domain.Geography.CellCoord Door(Building building) =>
        BuildingLayoutGenerator.Generate(building).FloorLayouts[0].Cells.First(c => c.Material == BuildingMaterial.Door).Cell;

    [Fact]
    public void Enter_and_exit_are_published_as_upserts_and_removed()
    {
        var (world, building, npcs, scope) = Setup();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        var (reader, unsubscribe) = _factory.Services.GetRequiredService<RealtimeGateway>().SubscribeChannel(scope);
        try
        {
            loop.PublishDeltas(); // linha de base: ninguém dentro
            reader.TryRead(out _);

            Assert.True(InteriorEntry.TryEnter(world, npcs[0], building.Id, FloorLevel.Ground, Door(building)).IsSuccess);
            loop.PublishDeltas();
            Assert.True(reader.TryRead(out var entered));
            var enteredDelta = Assert.IsType<InteriorTickDelta>(entered!.Payload);
            Assert.Equal(npcs[0].Id, Assert.Single(enteredDelta.Upserts).Npc);
            Assert.Empty(enteredDelta.Removed);

            npcs[0].ExitBuilding();
            loop.PublishDeltas();
            Assert.True(reader.TryRead(out var exited));
            var exitedDelta = Assert.IsType<InteriorTickDelta>(exited!.Payload);
            Assert.Empty(exitedDelta.Upserts);
            Assert.Equal(npcs[0].Id, Assert.Single(exitedDelta.Removed));
        }
        finally { unsubscribe(); npcs[0].ExitBuilding(); }
    }

    [Fact]
    public void Action_change_is_an_upsert_and_no_change_is_empty()
    {
        var (world, building, npcs, scope) = Setup();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        var (reader, unsubscribe) = _factory.Services.GetRequiredService<RealtimeGateway>().SubscribeChannel(scope);
        try
        {
            npcs[0].SetCurrentAction(ActionType.Idle, 0);
            Assert.True(InteriorEntry.TryEnter(world, npcs[0], building.Id, FloorLevel.Ground, Door(building)).IsSuccess);
            loop.PublishDeltas();
            reader.TryRead(out _);

            loop.PublishDeltas();
            Assert.True(reader.TryRead(out var quiet));
            var quietDelta = Assert.IsType<InteriorTickDelta>(quiet!.Payload);
            Assert.Empty(quietDelta.Upserts);
            Assert.Empty(quietDelta.Removed);

            npcs[0].SetCurrentAction(ActionType.Work, 1);
            loop.PublishDeltas();
            Assert.True(reader.TryRead(out var changed));
            var changedDelta = Assert.IsType<InteriorTickDelta>(changed!.Payload);
            Assert.Equal(ActionType.Work, Assert.Single(changedDelta.Upserts).Action);
        }
        finally { unsubscribe(); npcs[0].ExitBuilding(); }
    }

    [Fact]
    public void Missing_building_publishes_nothing_and_loop_survives()
    {
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        var (missing, unsubMissing) = gateway.SubscribeChannel(new VisualScope(VisualScopeKind.Interior, "999999"));
        var (world, unsubWorld) = gateway.SubscribeChannel(new VisualScope(VisualScopeKind.World, ""));
        try
        {
            loop.PublishDeltas();

            Assert.False(missing.TryRead(out _), "prédio inexistente não publica");
            Assert.True(world.TryRead(out _), "o escopo Mundo continua sendo publicado");
        }
        finally { unsubMissing(); unsubWorld(); }
    }

    [Fact]
    public void Interior_delta_has_sequence_and_replays()
    {
        var (_, _, _, scope) = Setup();
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        var (reader, unsubscribe) = gateway.SubscribeChannel(scope);
        try
        {
            loop.PublishDeltas();
            loop.PublishDeltas();
            Assert.True(reader.TryRead(out var first));
            Assert.True(reader.TryRead(out var second));
            Assert.Equal(first!.ToCursor.Sequence + 1, second!.ToCursor.Sequence);

            var replay = gateway.Replay(scope, ViewerMode.Spectator, first.FromCursor);
            Assert.True(replay.IsSuccess);
            Assert.Contains(replay.Value!, e => e.ToCursor.Sequence == second.ToCursor.Sequence);
        }
        finally { unsubscribe(); }
    }

    [Fact]
    public async Task Websocket_subscriber_receives_the_interior_delta_frame()
    {
        var (world, building, npcs, scope) = Setup();
        var wsClient = _factory.Server.CreateWebSocketClient();
        var uri = new UriBuilder(new Uri(_factory.Server.BaseAddress, $"/visual/ws?scope=Interior&refId={building.Id.Value}&mode=Spectator")) { Scheme = "ws" }.Uri;
        using var socket = await wsClient.ConnectAsync(uri, CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[65536];
        await socket.ReceiveAsync(buffer, timeout.Token); // snapshot inicial
        Assert.True(InteriorEntry.TryEnter(world, npcs[0], building.Id, FloorLevel.Ground, Door(building)).IsSuccess);
        try
        {
            _factory.Services.GetRequiredService<TickLoopService>().PublishDeltas();
            var result = await socket.ReceiveAsync(buffer, timeout.Token);
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

            Assert.Contains("\"upserts\"", json);
            Assert.Contains("\"toCursor\"", json);
        }
        finally { npcs[0].ExitBuilding(); }
    }

    [Fact]
    public void Resubscribing_starts_from_every_occupant_again()
    {
        var (world, _, npcs, _) = Setup();
        // outro prédio: o teste do WebSocket pode deixar assinatura pendente no primeiro
        var building = world.Buildings.Skip(1).First();
        var scope = new VisualScope(VisualScopeKind.Interior, building.Id.Value.ToString());
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        Assert.True(InteriorEntry.TryEnter(world, npcs[0], building.Id, FloorLevel.Ground, Door(building)).IsSuccess);
        try
        {
            var (first, unsubscribeFirst) = gateway.SubscribeChannel(scope);
            loop.PublishDeltas();
            first.TryRead(out _);
            unsubscribeFirst();
            loop.PublishDeltas(); // sem assinante: o servidor esquece o estado anterior

            var (second, unsubscribeSecond) = gateway.SubscribeChannel(scope);
            try
            {
                loop.PublishDeltas();
                Assert.True(second.TryRead(out var envelope));
                var delta = Assert.IsType<InteriorTickDelta>(envelope!.Payload);
                Assert.Equal(npcs[0].Id, Assert.Single(delta.Upserts).Npc);
            }
            finally { unsubscribeSecond(); }
        }
        finally { npcs[0].ExitBuilding(); }
    }
}
