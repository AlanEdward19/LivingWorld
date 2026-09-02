using LivingWorld.Api.Realtime;
using LivingWorld.Api.Visual;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15.1, T4 (VTT2-26, viabilidade operacional): janela de retenção do log de
/// replay do gateway — descarta entradas abaixo do menor cursor que um assinante ativo ainda
/// possa precisar, e nunca acumula histórico em escopo sem nenhum assinante.</summary>
public class RealtimeGatewayRetentionTests
{
    private static readonly VisualScope WorldScope = new(VisualScopeKind.World, "");

    [Fact]
    public void Log_does_not_grow_past_the_retention_window_after_many_publishes()
    {
        var gateway = new RealtimeGateway(() => 0, retentionPerScope: 5);
        var (_, unsubscribe) = gateway.SubscribeChannel(WorldScope);

        for (int i = 0; i < 20; i++)
            gateway.Publish(WorldScope, payload: i);

        var everything = gateway.Replay(WorldScope, ViewerMode.Spectator, new VisualCursor(0, WorldScope.ScopeKey, 0));

        Assert.Equal(5, everything.Value!.Count);
        unsubscribe();
    }

    [Fact]
    public void Replay_of_an_active_subscriber_still_returns_everything_not_yet_seen_after_pruning()
    {
        var gateway = new RealtimeGateway(() => 0, retentionPerScope: 5);
        var (_, unsubscribe) = gateway.SubscribeChannel(WorldScope);

        for (int i = 0; i < 20; i++)
            gateway.Publish(WorldScope, payload: i);

        // As últimas 5 publicações (sequências 16..20) sobrevivem à poda; um assinante que já
        // viu até a sequência 17 ainda deve receber exatamente as 3 que faltam (18, 19, 20).
        var pending = gateway.Replay(WorldScope, ViewerMode.Spectator, new VisualCursor(0, WorldScope.ScopeKey, 17));

        Assert.Equal(3, pending.Value!.Count);
        Assert.All(pending.Value!, e => Assert.True(e.ToCursor.Sequence > 17));
        unsubscribe();
    }

    [Fact]
    public void Scope_without_any_subscriber_never_accumulates_history()
    {
        var gateway = new RealtimeGateway(() => 0);

        for (int i = 0; i < 10; i++)
            gateway.Publish(WorldScope, payload: i);

        var everything = gateway.Replay(WorldScope, ViewerMode.Spectator, new VisualCursor(0, WorldScope.ScopeKey, 0));

        Assert.Empty(everything.Value!);
    }
}
