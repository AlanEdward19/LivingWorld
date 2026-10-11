using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Simulation;

public sealed record SetSpeedRequest(double TicksPerSecond);

public sealed record SimulationStatusResponse(bool IsPaused, double TicksPerSecond, long Tick, long Year);

/// <summary>Tradução fina de <see cref="SimulationHost"/> sobre HTTP
/// — nenhuma regra nova aqui, a validação de velocidade já existe em
/// <c>SimulationHost.SetSpeed</c>. <c>step</c> só faz sentido pausado (409 caso contrário) e
/// avança o mundo canônico chamando <see cref="WorldClock.Tick"/> diretamente, o mesmo relógio
/// que o loop de tempo real vai dirigir automaticamente.</summary>
public static class SimulationControlEndpoints
{
    public static void MapSimulationControlEndpoints(this WebApplication app)
    {
        app.MapPost("/simulation/pause", (SimulationHost host) =>
        {
            host.Pause();
            return Results.Ok();
        });

        app.MapPost("/simulation/resume", (SimulationHost host) =>
        {
            host.Resume();
            return Results.Ok();
        });

        app.MapPost("/simulation/speed", (SetSpeedRequest request, SimulationHost host) =>
        {
            if (request.TicksPerSecond <= 0)
                return Results.BadRequest("Velocidade deve ser positiva.");

            host.SetSpeed(request.TicksPerSecond);
            return Results.Ok();
        });

        app.MapPost("/simulation/step", (SimulationHost host, WorldHost worldHost, TickLoopService loop) =>
        {
            if (!host.IsPaused)
                return Results.Conflict("Step só é permitido com a simulação pausada.");

            worldHost.Clock.Tick(worldHost.Current);
            loop.PublishDeltas();
            return Results.Ok();
        });

        app.MapPost("/simulation/advance-year", (SimulationHost host, TickLoopService loop) =>
        {
            if (!host.IsPaused)
                return Results.Conflict("Avanço anual só é permitido com a simulação pausada.");

            host.FastForwardOneYear();
            loop.PublishDeltas();
            return Results.Ok();
        });

        app.MapGet("/simulation/status", (SimulationHost host, WorldHost worldHost) =>
            Results.Ok(new SimulationStatusResponse(
                host.IsPaused,
                host.TicksPerSecond,
                worldHost.Current.CurrentDate.TotalHours,
                worldHost.Current.CurrentDate.Year)));
    }
}
