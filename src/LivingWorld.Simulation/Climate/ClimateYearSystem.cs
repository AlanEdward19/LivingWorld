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
        double shared = rules.StdDevMilli > 0 ? StandardShock(ctx.OneShot($"climate-shared-{year}")) : 0;
        foreach (var region in world.Map.Regions.OrderBy(r => r.Id.Value))
        {
            var rng = ctx.OneShot($"climate-{region.Id.Value}-{year}");
            int factor;
            if (rules.StdDevMilli <= 0)
                factor = min + (int)Math.Round(rng.NextDouble() * (max - min));
            else
            {
                // AR(1): o desvio do ano anterior persiste; o choque mistura a parte comum e a local com variância 1
                double w = Math.Clamp(rules.SharedShare, 0, 1), rho = Math.Clamp(rules.Persistence, 0, 0.99);
                double shock = Math.Sqrt(w) * shared + Math.Sqrt(1 - w) * StandardShock(rng);
                double previous = world.ClimateFactorMilli(region.Id, year - 1) - rules.MeanMilli;
                double value = rules.MeanMilli + rho * previous + rules.StdDevMilli * Math.Sqrt(1 - rho * rho) * shock;
                factor = (int)Math.Round(Math.Clamp(value, min, max));
            }

            world.SetClimateYear(region.Id, year, factor);
        }

        world.PurgeClimateYearsBefore(year - WorldState.ClimateYearsKept);
    }

    /// <summary>Choque padronizado (média 0, variância 1) pela soma de quatro uniformes: perto da normal, sem log nem cosseno.</summary>
    internal static double StandardShock(WorldRng rng) =>
        (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 2.0) * Math.Sqrt(3.0);
}
