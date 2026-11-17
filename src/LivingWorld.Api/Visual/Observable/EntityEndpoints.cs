using LivingWorld.Api.World;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Visual.Observable;

/// <summary><c>GET /entities/{kind}/{id}</c> (AD-170, ADR-0047 etapa 2): ficha genérica de pessoa, prédio, lavoura, animal, objeto,
/// espólio, cidade, casa e oficina (aceita também os nomes em português). Só leitura, sob o mesmo lock do laço de tick; 404 para tipo
/// ou id desconhecido.</summary>
public static class EntityEndpoints
{
    public static void MapEntityEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapGet("/entities/{kind}/{id}", (string kind, string id, PresentationNameResolver names, SavedWorldLibrary library,
            BufferingWorldEventSink sink) =>
        {
            lock (library.Gate)
            {
                var world = host.Current;
                var result = EntityInspectionQuery.Inspect(world, kind, id, names.For(world.PeriodId), sink);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
        });
    }
}
