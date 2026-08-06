using LivingWorld.Infrastructure;
using LivingWorld.Simulation;

namespace LivingWorld.Api;

public sealed record CreateWorldRequest(string ScenarioJson);

public sealed record CreateWorldResponse(int NpcCount);

/// <summary>Feature ad-hoc "criar mundo" (AD-001 em .specs/STATE.md): aceita um scenario JSON
/// completo (mesmo formato de <see cref="ScenarioLoaderV2.LoadWorld"/>), troca a instância
/// canônica em <see cref="WorldHost"/> e persiste imediatamente — sem isso o host nunca fica
/// sem lastro no repositório entre o create e o próximo snapshot automático.</summary>
public static class WorldCreateEndpoints
{
    public static void MapWorldCreateEndpoints(this WebApplication app, WorldHost host, PersistentWorldRunner runner, BufferingWorldEventSink sink)
    {
        app.MapPost("/worlds/create", (CreateWorldRequest request) =>
        {
            var result = ScenarioLoaderV2.LoadWorld(request.ScenarioJson);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);

            var (world, clock) = result.Value!;
            host.Replace(world, clock);
            runner.Snapshot(world, sink);

            return Results.Ok(new CreateWorldResponse(world.Npcs.Count));
        });
    }
}
