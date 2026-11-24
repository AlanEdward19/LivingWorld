using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Invention;

namespace LivingWorld.Api.World;

/// <summary><c>GET /designs</c> e <c>GET /designs/{id}</c> (AD-141): o que foi inventado, em partes, materiais e desempenho medido,
/// para o cliente desenhar e animar sem modelo por invenção (200), ou 404.</summary>
public static class DesignEndpoints
{
    public static void MapDesignEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapGet("/designs", () => Results.Ok(DesignQuery.All(host.Current)));
        app.MapGet("/designs/{id:int}", (int id) =>
        {
            var result = DesignQuery.Of(host.Current, id);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });
        // AD-143: o fato (e os relatos que nasceram dele) aponta para o design, vivo ou lápide
        app.MapGet("/designs/by-fact/{factId:long}", (long factId) =>
        {
            var result = DesignQuery.OfFact(host.Current, new LivingWorld.Domain.Shared.FactId(factId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });
    }
}
