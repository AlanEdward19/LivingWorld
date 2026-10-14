using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.World;

/// <summary><c>GET /cultures/{id}/technologies?fromTick=</c>: vetores e adoção das tecnologias emergidas da cultura (200) ou 404.</summary>
public static class CultureTechnologyEndpoints
{
    public static void MapCultureTechnologyEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapGet("/cultures/{id:int}/technologies", (int id, long? fromTick) =>
        {
            var result = CultureTechnologyQuery.Of(host.Current, new CultureId(id), fromTick ?? 0);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });
    }
}
