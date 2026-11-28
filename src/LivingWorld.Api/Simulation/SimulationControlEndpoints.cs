using LivingWorld.Api.Admin;
using LivingWorld.Api.World;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Simulation;

public sealed record SetSpeedRequest(double TicksPerSecond);

/// <summary>Duração do tick do mundo em milissegundos de mundo (ADR-0050): divide a hora (1 a 3.600.000; 50 serve).</summary>
/// AD-197c: o Δ do admin fica fixo acima do ritmo automático; <c>0</c> devolve o Δ ao ritmo do mundo.
public sealed record SetTickDeltaRequest(int DeltaMs);

/// <summary>AD-197c: o admin acelera o mundo (uma hora por tick), com ou sem jogador.</summary>
public sealed record SetAcceleratedRequest(bool On);

/// <summary><see cref="AtMs"/> é o instante do mundo em milissegundos (ADR-0050); <see cref="TickDeltaMs"/>, a duração do tick em
/// vigor.</summary>
/// AD-197c: <see cref="Rhythm"/> é o modo do relógio (Fast, RealTime, Accelerated, Manual), <see cref="LoopTicksPerSecond"/> a
/// velocidade do laço nesse modo, <see cref="TimeScale"/> a escala do mundo com jogador e <see cref="PlayersPresent"/> quantos jogam.
public sealed record SimulationStatusResponse(
    bool IsPaused, double TicksPerSecond, long AtMs, long Year, int TickDeltaMs,
    string? PausedBy = null, string? PauseReason = null, long? PausedAtTick = null, long? PauseMaxTicks = null,
    string? Rhythm = null, double? LoopTicksPerSecond = null, double? TimeScale = null, int? PlayersPresent = null, bool? Accelerated = null, int? PlayerSpeed = null);

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
            LivingWorld.Api.Players.PlayerCommandGateway gateway, WorldHost worldHost, SimulationHost host) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.DeltaMs == 0) // AD-197c: volta ao ritmo do mundo
            {
                host.SetManualDelta(null);
                audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.tick-delta", "world", "deltaMs=auto");
                return Results.Ok(new { tickDeltaMs = 0 });
            }
            var reply = gateway.SetTickDelta(access.Actor, request.DeltaMs);
            if (!reply.Ok) return Results.BadRequest(reply.Error);
            host.SetManualDelta(request.DeltaMs);
            audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.tick-delta", "world",
                $"deltaMs={request.DeltaMs}");
            return Results.Ok(reply.Body);
        });

        app.MapPost("/simulation/step", (HttpContext http, IConfiguration config, AdminAuditStore audit, SimulationHost host, WorldHost worldHost, TickLoopService loop, SavedWorldLibrary library,
            LivingWorld.Api.Players.PlayerCommandGateway gateway) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            lock (library.Gate)
            {
                if (!host.IsPaused)
                    return Results.Conflict("Step só é permitido com a simulação pausada.");

                // AD-197c: o passo entra no registro do jogador (o replay o reproduz); uma hora, como sempre
                var reply = gateway.Advance(access.Actor, 1, LivingWorld.Domain.Shared.WorldMillis.HourTick);
                if (!reply.Ok) return Results.BadRequest(reply.Error);
                loop.PublishDeltas();
                audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.step", "world", "");
                return Results.Ok();
            }
        });

        app.MapPost("/simulation/advance-year", (HttpContext http, IConfiguration config, AdminAuditStore audit, SimulationHost host, WorldHost worldHost, TickLoopService loop, SavedWorldLibrary library,
            LivingWorld.Api.Players.PlayerCommandGateway gateway) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            lock (library.Gate)
            {
                if (!host.IsPaused)
                    return Results.Conflict("Avanço anual só é permitido com a simulação pausada.");

                var reply = gateway.Advance(access.Actor, worldHost.Current.Calendar.HoursPerYear, LivingWorld.Domain.Shared.WorldMillis.HourTick);
                if (!reply.Ok) return Results.BadRequest(reply.Error);
                loop.PublishDeltas();
                audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.advance-year", "world", "");
                return Results.Ok();
            }
        });

        // AD-197c: o admin acelera o mundo sempre (com ou sem jogador): uma hora por tick até desligar
        app.MapPost("/simulation/accelerate", (HttpContext http, IConfiguration config, AdminAuditStore audit, SetAcceleratedRequest request,
            SimulationHost host, WorldHost worldHost) =>
        {
            if (AdminEndpoints.Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            host.SetAccelerated(request.On);
            audit.Append(BranchId.Root, worldHost.Current.CurrentDate.TotalHours, access.Actor, "world.accelerate", "world", $"on={request.On}");
            return Results.Ok(new { accelerated = request.On });
        });

        app.MapGet("/simulation/status", (SimulationHost host, WorldHost worldHost, LivingWorld.Simulation.Players.PlayerService players) =>
        {
            // #256: o ritmo é o que o motor adotaria AGORA (quem possui, troca ou entra muda o ritmo antes do próximo ciclo do laço): o
            // cliente relê o status logo depois de possuir e não extrapola o relógio na velocidade do ritmo velho.
            var world = worldHost.Current;
            var plan = host.Plan(world, players);
            return Results.Ok(new SimulationStatusResponse(
                host.IsPaused,
                host.TicksPerSecond,
                world.CurrentDate.TotalMilliseconds,
                world.CurrentDate.Year,
                plan.DeltaMs,
                host.IsPaused ? host.AdminPause?.Actor : null,
                host.IsPaused ? host.AdminPause?.Reason : null,
                host.IsPaused ? host.AdminPause?.PausedAtTick : null,
                host.IsPaused ? host.AdminPause?.MaxTicks : null,
                plan.Mode.ToString(),
                plan.TicksPerSecond,
                world.Players.Time.TimeScale,
                LivingWorld.Simulation.Players.PlayerService.PlayersPresent(world),
                host.Accelerated,
                host.EffectiveSpeed(world)));
        });
    }
}
