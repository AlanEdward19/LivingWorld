using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LivingWorld.Api.Visual;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Core;

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

        app.MapGet("/visual/subscribe", (VisualScopeKind scope, string? refId, ViewerMode mode, long? playerNpcId, RealtimeGateway gateway, WorldState world) =>
        {
            var result = gateway.Snapshot(new VisualScope(scope, refId ?? ""), mode);
            if (!result.IsSuccess) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(WithProjectedPayload(result.Value!, world, playerNpcId));
        });

        app.MapGet("/visual/replay", (VisualScopeKind scope, string? refId, ViewerMode mode, long sinceTick, long sinceSequence, RealtimeGateway gateway) =>
        {
            var visualScope = new VisualScope(scope, refId ?? "");
            var since = new VisualCursor(sinceTick, visualScope.ScopeKey, sinceSequence);
            var result = gateway.Replay(visualScope, mode, since);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        app.MapGet("/visual/sse", async (HttpContext http, VisualScopeKind scope, string? refId, ViewerMode mode, long? playerNpcId, RealtimeGateway gateway, WorldState world) =>
        {
            var visualScope = new VisualScope(scope, refId ?? "");
            var snapshot = gateway.Snapshot(visualScope, mode);
            if (!snapshot.IsSuccess)
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            http.Response.Headers.ContentType = "text/event-stream";
            await WriteEventAsync(http.Response, WithProjectedPayload(snapshot.Value!, world, playerNpcId));

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

        // Fase 15, T9: MapGet (não Map genérico) — o handshake de WebSocket é uma requisição GET
        // com header Upgrade; MapGet é o verbo correto E o único que o gerador de OpenAPI
        // consegue documentar (Map genérico fica sem verbo, invisível pro doc).
        app.MapGet("/visual/ws", async (HttpContext http, VisualScopeKind scope, string? refId, ViewerMode mode, long? playerNpcId, RealtimeGateway gateway, WorldState world) =>
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
            await SendJsonAsync(socket, WithProjectedPayload(snapshot.Value!, world, playerNpcId), http.RequestAborted);

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

    /// <summary>Fase 15, T4/T5/T7 (VTT-01, VTT-03, VTT-08, VTT-09, VTT-11): o snapshot inicial
    /// carrega a projeção do escopo — montada sob demanda no subscribe, mesmo padrão de
    /// materialização sob demanda de <c>NpcInspectionQuery</c>. RefId inválido/inexistente (parse
    /// falho, cidade/prédio não encontrado) mantém <c>Payload</c> nulo em vez de forçar um novo
    /// código de erro — T5 não pede um contrato de 404 por refId, só a projeção quando o escopo
    /// resolve. Escopo city em modo Player aplica FOW (T7) centrado em <paramref
    /// name="playerNpcId"/>; Spectator/admin sempre vê sem filtro (VTT-09).</summary>
    private static VisualSnapshotEnvelope<object?> WithProjectedPayload(
        VisualSnapshotEnvelope<object?> envelope, WorldState world, long? playerNpcId)
    {
        object? payload = envelope.Scope.Kind switch
        {
            VisualScopeKind.World => GlobalProjector.Build(world),
            VisualScopeKind.City when Guid.TryParse(envelope.Scope.RefId, out var cityGuid) =>
                BuildCityPayload(world, new CityId(cityGuid), envelope.Mode, playerNpcId),
            VisualScopeKind.Interior when long.TryParse(envelope.Scope.RefId, out var buildingIdValue) =>
                InteriorProjector.Build(world, new BuildingId(buildingIdValue)) is { IsSuccess: true } result ? result.Value : null,
            _ => null,
        };

        return envelope with { Payload = payload };
    }

    /// <summary>Fase 15, T7 (VTT-08, VTT-09): espectador/admin sempre recebe o snapshot completo
    /// (override). Personagem sem <paramref name="playerNpcId"/> identificado não recebe nenhum
    /// morador — "área não descoberta" por padrão é mais seguro do que vazar tudo por engano.</summary>
    private static object? BuildCityPayload(WorldState world, CityId cityId, ViewerMode mode, long? playerNpcId)
    {
        var result = CityProjector.Build(world, cityId);
        if (!result.IsSuccess) return null;
        if (mode != ViewerMode.Player) return result.Value;

        var player = playerNpcId is { } id ? world.Npcs.FirstOrDefault(n => n.Id == new NpcId(id)) : null;
        if (player is null) return result.Value! with { Residents = [] };

        return CityVisibilityFilter.ApplyFog(result.Value!, player.CurrentLocation, adminOverride: false);
    }

    // Fase 15, T8: mesma convenção de naming do HTTP /visual/subscribe (ASP.NET Web defaults =
    // camelCase) — sem isso, WS/SSE serializavam PascalCase por padrão do JsonSerializer.Serialize
    // sem opções, e o cliente (que só entende camelCase) não conseguia ler os frames.
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    private static async Task WriteEventAsync(HttpResponse response, object payload)
    {
        await response.WriteAsync($"data: {JsonSerializer.Serialize(payload, WireOptions)}\n\n");
        await response.Body.FlushAsync();
    }

    private static Task SendJsonAsync(WebSocket socket, object payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, WireOptions));
        return socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }
}
