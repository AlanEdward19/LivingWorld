using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.World;

public static class SavedWorldEndpoints
{
    public static void MapSavedWorldEndpoints(this WebApplication app, WorldHost host, SavedWorldLibrary library,
        SimulationHost simulation, Func<WorldState, WorldClock> clockFor)
    {
        app.MapGet("/worlds", () => Results.Ok(library.List()));
        app.MapPost("/worlds/{worldId:guid}/load", (Guid worldId) =>
            library.Load(worldId, host, simulation, clockFor) ? Results.Ok() : Results.NotFound());
        app.MapDelete("/worlds/{worldId:guid}", (Guid worldId) => Results.StatusCode(library.Delete(worldId)));
    }
}
