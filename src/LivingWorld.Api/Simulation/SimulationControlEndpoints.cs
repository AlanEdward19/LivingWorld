using LivingWorld.Simulation;

namespace LivingWorld.Api.Simulation;

public sealed record SetSpeedRequest(double TicksPerSecond);

public sealed record SimulationStatusResponse(bool IsPaused, double TicksPerSecond, long Tick, long Year);

public sealed record AdvanceYearsResponse(long Tick, long Year);

/// <summary>Fase 15.1, T1 (VTT2-27..30): tradução fina de <see cref="SimulationHost"/> sobre HTTP
/// — nenhuma regra nova aqui, a validação de velocidade já existe em
/// <c>SimulationHost.SetSpeed</c>. <c>step</c> só faz sentido pausado (409 caso contrário) e
/// avança o mundo canônico chamando <see cref="WorldClock.Tick"/> diretamente, o mesmo relógio
/// que o loop de tempo real (T3) vai dirigir automaticamente.</summary>
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

        app.MapPost("/simulation/step", (SimulationHost host, WorldHost worldHost) =>
        {
            if (!host.IsPaused)
                return Results.Conflict("Step só é permitido com a simulação pausada.");

            worldHost.Clock.Tick(worldHost.Current);
            return Results.Ok();
        });

        app.MapPost("/simulation/advance-year", (SimulationHost host) =>
        {
            if (!host.IsPaused)
                return Results.Conflict("Avanço anual só é permitido com a simulação pausada.");

            host.FastForwardOneYear();
            return Results.Ok();
        });

        app.MapPost("/simulation/advance-years", (int? count, SimulationHost host, WorldHost worldHost) =>
        {
            if (count is null or <= 0)
                return Results.BadRequest("count deve ser um inteiro positivo.");

            if (!host.IsPaused)
                return Results.Conflict("Avanço anual só é permitido com a simulação pausada.");

            long ticksPerYear = worldHost.Current.Calendar.HoursPerYear;
            host.FastForward(checked(count.Value * ticksPerYear));

            var world = worldHost.Current;
            return Results.Ok(new AdvanceYearsResponse(world.CurrentDate.TotalHours, world.CurrentDate.Year));
        });

        app.MapGet("/simulation/status", (SimulationHost host, WorldHost worldHost) =>
            Results.Ok(new SimulationStatusResponse(
                host.IsPaused,
                host.TicksPerSecond,
                worldHost.Current.CurrentDate.TotalHours,
                worldHost.Current.CurrentDate.Year)));
    }
}
