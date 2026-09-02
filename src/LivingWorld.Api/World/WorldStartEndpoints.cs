using LivingWorld.Infrastructure;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Api;

public sealed record StartWorldRequest(string PeriodId, ulong Seed);

public sealed record StartWorldResponse(string PeriodId, ulong Seed, int NpcCount);

/// <summary>Fase 13, T6 (PERIOD-04..06, PERIOD-07..10): <c>POST /worlds/start</c> inicializa um
/// mundo a partir de um template já cadastrado (<see cref="IPeriodTemplateRepository"/>) pelo
/// mesmo pipeline de <see cref="WorldStartService"/> — nenhuma regra de bootstrap nova aqui, só
/// tradução request/response e o 404 quando o <c>PeriodId</c> não está registrado.</summary>
public static class WorldStartEndpoints
{
    public static void MapWorldStartEndpoints(this WebApplication app)
    {
        app.MapPost("/worlds/start", (StartWorldRequest request, IPeriodTemplateRepository repository) =>
        {
            var result = WorldStartService.Start(
                id => repository.FindLatestVersion(id)?.PayloadJson, request.PeriodId, request.Seed);

            if (!result.IsSuccess)
                return Results.NotFound(result.Error);

            var (world, _) = result.Value;
            return Results.Ok(new StartWorldResponse(request.PeriodId, request.Seed, world.Npcs.Count));
        });
    }
}
