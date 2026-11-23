using LivingWorld.Api.Admin;
using LivingWorld.Api.World;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Simulation;

public sealed record SetSpeedRequest(double TicksPerSecond);

/// <summary>Duração do tick do mundo em segundos de mundo (ADR-0050): divide a hora (1 a 3600).</summary>
public sealed record SetTickDeltaRequest(int DeltaSeconds);

/// <summary><see cref="Second"/> é o instante do mundo em segundos (ADR-0050); <see cref="TickDeltaSeconds"/>, a duração do tick em
/// vigor.</summary>
public sealed record SimulationStatusResponse(
    bool IsPaused, double TicksPerSecond, long Second, long Year, int TickDeltaSeconds,
    string? PausedBy = null, string? PauseReason = null, long? PausedAtTick = null, long? PauseMaxTicks = null);

/// <summary>Tradução fina de <see cref="SimulationHost"/> sobre HTTP. Pausa, retomada, velocidade, passo e avanço de ano
/// mexem no relógio de todo o branch, então são exclusivos do admin (ADR-0015): exigem <c>X-Admin-Token</c>, vão para a
/// auditoria e a pausa tem o teto padrão de <see cref="SimulationHost.DefaultAdminPauseDays"/> dias. Só <c>status</c> é
/// aberto. <c>step</c> só faz sentido pausado (409 caso contrário) e avança o mundo canônico chamando
/// <see cref="WorldClock.Tick"/> diretamente, o mesmo relógio que o loop de tempo real dirige.</summary>
public static class SimulationControlEndpoints
{
    public static void MapSimulationControlEndpoints(this WebApplication app)
    {
        app.MapPost("/simulation/pause", (HttpContext http, IConfiguration config, AdminAuditStore audit, SimulationHost host, WorldHost worldHost) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);

            long tick = worldHost.Current.CurrentDate.TotalHours;
            long cap = worldHost.Current.Calendar.HoursPerDay * SimulationHost.DefaultAdminPauseDays;
            host.PauseFor(access.Actor, "pausa pelo cliente", cap, tick);
            audit.Append(BranchId.Root, tick, access.Actor, "world.pause", "world", $"reason=pausa pelo cliente;maxTicks={cap}");
            return Results.Ok();
        });

        app.MapPost("/simulation/resume", (HttpContext http, IConfiguration config, AdminAuditStore audit, SimulationHost host, WorldHost worldHost) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);

            host.Resume();
            audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.resume", "world", "");
            return Results.Ok();
        });

        app.MapPost("/simulation/speed", (HttpContext http, IConfiguration config, AdminAuditStore audit, SetSpeedRequest request, SimulationHost host, WorldHost worldHost) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.TicksPerSecond <= 0)
                return Results.BadRequest("Velocidade deve ser positiva.");

            host.SetSpeed(request.TicksPerSecond);
            audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.speed", "world", $"ticksPerSecond={request.TicksPerSecond}");
            return Results.Ok();
        });

        // ADR-0050: troca a duração do tick (Δ) do mundo; vai para o registro do jogador para o replay
        app.MapPost("/simulation/tick-delta", (HttpContext http, IConfiguration config, AdminAuditStore audit, SetTickDeltaRequest request,
            LivingWorld.Api.Players.PlayerCommandGateway gateway, WorldHost worldHost) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var reply = gateway.SetTickDelta(access.Actor, request.DeltaSeconds);
            if (!reply.Ok) return Results.BadRequest(reply.Error);
            audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.tick-delta", "world",
                $"deltaSeconds={request.DeltaSeconds}");
            return Results.Ok(reply.Body);
        });

        app.MapPost("/simulation/step", (HttpContext http, IConfiguration config, AdminAuditStore audit, SimulationHost host, WorldHost worldHost, TickLoopService loop, SavedWorldLibrary library) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            lock (library.Gate)
            {
                if (!host.IsPaused)
                    return Results.Conflict("Step só é permitido com a simulação pausada.");

                worldHost.Clock.Tick(worldHost.Current);
                loop.PublishDeltas();
                audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.step", "world", "");
                return Results.Ok();
            }
        });

        app.MapPost("/simulation/advance-year", (HttpContext http, IConfiguration config, AdminAuditStore audit, SimulationHost host, WorldHost worldHost, TickLoopService loop, SavedWorldLibrary library) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            lock (library.Gate)
            {
                if (!host.IsPaused)
                    return Results.Conflict("Avanço anual só é permitido com a simulação pausada.");

                host.FastForwardOneYear();
                loop.PublishDeltas();
                audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.advance-year", "world", "");
                return Results.Ok();
            }
        });

        app.MapGet("/simulation/status", (SimulationHost host, WorldHost worldHost, LivingWorld.Simulation.Players.PlayerService players) =>
            Results.Ok(new SimulationStatusResponse(
                host.IsPaused,
                host.TicksPerSecond,
                worldHost.Current.CurrentDate.TotalSeconds,
                worldHost.Current.CurrentDate.Year,
                players.Log.TickDeltaSeconds,
                host.IsPaused ? host.AdminPause?.Actor : null,
                host.IsPaused ? host.AdminPause?.Reason : null,
                host.IsPaused ? host.AdminPause?.PausedAtTick : null,
                host.IsPaused ? host.AdminPause?.MaxTicks : null)));
    }
}
