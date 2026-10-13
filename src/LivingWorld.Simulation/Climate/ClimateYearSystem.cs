using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Climate;

/// <summary>Ano de safra (weather-drought), <c>Yearly</c>: no começo do ano sorteia o fator de cada região (em milésimos, entre o
/// mínimo e o máximo do cenário) com um stream nomeado por (região, ano), então o mesmo mundo dá os mesmos anos de seca e de
/// fartura. Sem clima declarado, não faz nada e todo ano vale 1000.</summary>
[LivingCapability("CLIMATE", Consumers = ["inspector.climate"])]
public sealed class ClimateYearSystem : ISimulationSystem
{
    public const string SystemName = "climate-year";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.ClimateRules;
        if (!rules.Enabled) return;

        // limites inválidos do cenário (negativo, máximo abaixo do mínimo) não podem virar colheita negativa
        int min = Math.Max(0, rules.MinMilli), max = Math.Max(min, rules.MaxMilli);
        long year = world.ClimateYearOf(ctx.CurrentTick);
        foreach (var region in world.Map.Regions.OrderBy(r => r.Id.Value))
        {
            double roll = ctx.Rng($"climate-{region.Id.Value}-{year}").NextDouble();
            world.SetClimateYear(region.Id, year, min + (int)Math.Round(roll * (max - min)));
        }

        world.PurgeClimateYearsBefore(year - WorldState.ClimateYearsKept);
    }
}
