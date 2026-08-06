using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LivingWorld.Api.Visual;
using LivingWorld.Simulation;

namespace LivingWorld.Api.Realtime;

/// <summary>Fase 15, T3 (VTT-02, VTT-10): WebSocket primário (<c>/visual/ws</c>) + SSE fallback de
/// leitura (<c>/visual/sse</c>) para subscribe, mais <c>/visual/replay</c> HTTP para reconexão por
/// cursor (spec.md edge case: reidratar sem escrita de mundo). Autorização (<see
/// cref="RealtimeGateway.Authorize"/>) roda antes de qualquer upgrade/stream — escopo negado nunca
/// vaza payload (spec.md edge case: subscribe sem permissão).</summary>
public static class RealtimeEndpoints
{
    public static void MapRealtimeEndpoints(this WebApplication app)
    {
        app.UseWebSockets();

        app.MapGet("/visual/subscribe", (VisualScopeKind scope, string? refId, ViewerMode mode, RealtimeGateway gateway, WorldState world) =>
        {
            var result = gateway.Snapshot(new VisualScope(scope, refId ?? ""), mode);
            if (!result.IsSuccess) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(WithProjectedPayload(result.Value!, world));
        });

        app.MapGet("/visual/replay", (VisualScopeKind scope, string? refId, ViewerMode mode, long sinceTick, long sinceSequence, RealtimeGateway gateway) =>
        {
            var visualScope = new VisualScope(scope, refId ?? "");
            var since = new VisualCursor(sinceTick, visualScope.ScopeKey, sinceSequence);
            var result = gateway.Replay(visualScope, mode, since);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        app.MapGet("/visual/sse", async (HttpContext http, VisualScopeKind scope, string? refId, ViewerMode mode, RealtimeGateway gateway, WorldState world) =>
        {
            var visualScope = new VisualScope(scope, refId ?? "");
            var snapshot = gateway.Snapshot(visualScope, mode);
            if (!snapshot.IsSuccess)
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            http.Response.Headers.ContentType = "text/event-stream";
            await WriteEventAsync(http.Response, WithProjectedPayload(snapshot.Value!, world));

            var (reader, unsubscribe) = gateway.SubscribeChannel(visualScope);
            try
            {
                while (await reader.WaitToReadAsync(http.RequestAborted))
                    while (reader.TryRead(out var delta))
                        await WriteEventAsync(http.Response, delta);
            }
            finally
            {
                unsubscribe();
            }
        });

        app.Map("/visual/ws", async (HttpContext http, VisualScopeKind scope, string? refId, ViewerMode mode, RealtimeGateway gateway, WorldState world) =>
        {
            var visualScope = new VisualScope(scope, refId ?? "");
            var snapshot = gateway.Snapshot(visualScope, mode);
            if (!snapshot.IsSuccess)
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            if (!http.WebSockets.IsWebSocketRequest)
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await http.WebSockets.AcceptWebSocketAsync();
            await SendJsonAsync(socket, WithProjectedPayload(snapshot.Value!, world), http.RequestAborted);

            var (reader, unsubscribe) = gateway.SubscribeChannel(visualScope);
            try
            {
                while (await reader.WaitToReadAsync(http.RequestAborted))
                    while (reader.TryRead(out var delta))
                        await SendJsonAsync(socket, delta, http.RequestAborted);
            }
            finally
            {
                unsubscribe();
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
        });
    }

    /// <summary>Fase 15, T4 (VTT-01): o snapshot inicial do escopo world carrega a projeção global
    /// (cidades, NPCs externos, camadas) — montada sob demanda no subscribe, mesmo padrão de
    /// materialização sob demanda de <c>NpcInspectionQuery</c>. Outros escopos ainda não têm
    /// projector (T5) e mantêm <c>Payload</c> nulo.</summary>
    private static VisualSnapshotEnvelope<object?> WithProjectedPayload(VisualSnapshotEnvelope<object?> envelope, WorldState world) =>
        envelope.Scope.Kind == VisualScopeKind.World
            ? envelope with { Payload = GlobalProjector.Build(world) }
            : envelope;

    private static async Task WriteEventAsync(HttpResponse response, object payload)
    {
        await response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n");
        await response.Body.FlushAsync();
    }

    private static Task SendJsonAsync(WebSocket socket, object payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        return socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }
}
