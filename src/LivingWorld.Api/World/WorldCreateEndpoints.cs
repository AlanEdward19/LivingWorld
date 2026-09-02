using LivingWorld.Api.Visual;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;

namespace LivingWorld.Api;

public sealed record CreateWorldRequest(string ScenarioJson, string Name);

public sealed record CreateWorldResponse(int NpcCount, Guid WorldId, string Name, long Tick, string InitialScope);

/// <summary>Feature ad-hoc "criar mundo" (AD-001 em .specs/STATE.md; identidade em T42/ADR-0017):
/// aceita um scenario JSON completo (mesmo formato de <see cref="ScenarioLoaderV2.LoadWorld"/>)
/// mais o nome escolhido pelo usuário, troca a instância canônica em <see cref="WorldHost"/> e
/// persiste imediatamente — sem isso o host nunca fica sem lastro no repositório entre o create
/// e o próximo snapshot automático.</summary>
public static class WorldCreateEndpoints
{
    public static void MapWorldCreateEndpoints(this WebApplication app, WorldHost host, PersistentWorldRunner runner, BufferingWorldEventSink sink)
    {
        app.MapPost("/worlds/create", (CreateWorldRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Name é obrigatório.");

            var result = ScenarioLoaderV2.LoadWorld(request.ScenarioJson);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);

            var (world, clock) = result.Value!;
            world.Rename(request.Name);
            host.Replace(world, clock);
            runner.Snapshot(world, sink);

            var initialScope = new VisualScope(VisualScopeKind.World, "").ScopeKey;
            return Results.Ok(new CreateWorldResponse(
                world.Npcs.Count, WorldIdentity.WorldIdFor(world.Seed), world.Name, world.CurrentDate.TotalHours, initialScope));
        });
    }
}
