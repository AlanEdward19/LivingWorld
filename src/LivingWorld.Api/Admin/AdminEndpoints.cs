using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Admin;

public sealed record AdminPauseRequest(string? Reason, long? MaxTicks);
public sealed record AdminSpeedRequest(double TicksPerSecond);
public sealed record AdminPauseResponse(bool IsPaused, string? Actor, string? Reason, long? MaxTicks, long? PausedAtTick);

/// <summary>Console de admin (Fase 26, ADR-0015), fisicamente separado da API de jogo: toda rota exige o cabeçalho
/// <c>X-Admin-Token</c> igual a <c>ADMIN_TOKEN</c> (sem token configurado, 403) e toda ação, inclusive leitura, vai para a
/// auditoria append-only. Pausa de admin tem teto e retoma sozinha.</summary>
public static class AdminEndpoints
{
    public const string TokenHeader = "X-Admin-Token";
    public const string ActorHeader = "X-Admin-Actor";

    public static void MapAdminEndpoints(this WebApplication app, WorldHost host, SimulationHost simulation)
    {
        var admin = app.MapGroup("/admin");

        admin.MapPost("/world/pause", (HttpContext http, IConfiguration config, AdminAuditStore audit, AdminPauseRequest request) =>
        {
            if (Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { error = "reason: obrigatório" });

            long hoursPerDay = host.Current.Calendar.HoursPerDay;
            long max = request.MaxTicks ?? hoursPerDay * SimulationHost.DefaultAdminPauseDays;
            long ceiling = hoursPerDay * SimulationHost.MaxAdminPauseDays;
            if (max <= 0 || max > ceiling) return Results.BadRequest(new { error = $"maxTicks: entre 1 e {ceiling}" });

            long tick = host.Current.CurrentDate.TotalHours;
            simulation.PauseFor(access.Actor, request.Reason.Trim(), max, tick);
            audit.Append(BranchId.Root, tick, access.Actor, "world.pause", "world", $"reason={request.Reason.Trim()};maxTicks={max}");
            return Results.Ok(State(simulation));
        });

        admin.MapPost("/world/resume", (HttpContext http, IConfiguration config, AdminAuditStore audit) =>
        {
            if (Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            simulation.Resume();
            audit.Append(BranchId.Root, host.Current.CurrentDate.TotalHours, access.Actor, "world.resume", "world", "");
            return Results.Ok(State(simulation));
        });

        admin.MapPost("/world/speed", (HttpContext http, IConfiguration config, AdminAuditStore audit, AdminSpeedRequest request) =>
        {
            if (Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.TicksPerSecond <= 0) return Results.BadRequest(new { error = "ticksPerSecond: deve ser positivo" });

            simulation.SetSpeed(request.TicksPerSecond);
            audit.Append(BranchId.Root, host.Current.CurrentDate.TotalHours, access.Actor, "world.speed", "world", $"ticksPerSecond={request.TicksPerSecond}");
            return Results.Ok(State(simulation));
        });

        admin.MapGet("/search", (HttpContext http, IConfiguration config, AdminAuditStore audit, string? q, string? kind) =>
        {
            if (Authorize(http, config) is not { } access) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var hits = AdminSearch.Npcs(host.Current, kind, q);
            if (!hits.IsSuccess) return Results.BadRequest(new { error = hits.Error });

            audit.Append(BranchId.Root, host.Current.CurrentDate.TotalHours, access.Actor, "search", kind ?? "name", $"q={q!.Trim()};hits={hits.Value!.Count}");
            return Results.Ok(hits.Value);
        });

        admin.MapGet("/audit", (HttpContext http, IConfiguration config, AdminAuditStore audit, long? fromTick) =>
        {
            if (Authorize(http, config) is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(audit.Read(BranchId.Root, fromTick ?? 0));
        });
    }

    /// <summary>Credencial de admin da requisição, ou nulo (a rota responde 403). Também guarda as rotas de controle do relógio
    /// em <c>/simulation/*</c>: pausa e velocidade são globais e exclusivas do admin (ADR-0015).</summary>
    public static AdminAccess? Authorize(HttpContext http, IConfiguration config) =>
        AdminAccess.TryGrant(
            http.Request.Headers[TokenHeader].FirstOrDefault(), config["ADMIN_TOKEN"], http.Request.Headers[ActorHeader].FirstOrDefault());

    private static AdminPauseResponse State(SimulationHost simulation) =>
        new(simulation.IsPaused, simulation.AdminPause?.Actor, simulation.AdminPause?.Reason, simulation.AdminPause?.MaxTicks, simulation.AdminPause?.PausedAtTick);
}
