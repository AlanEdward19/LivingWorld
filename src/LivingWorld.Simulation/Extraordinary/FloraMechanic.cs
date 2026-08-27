using System.Globalization;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// <c>flora.growth-rate:&lt;multiplicador&gt;</c> numa área (<c>area:radius</c>/<c>area:region</c>).
/// </summary>
public sealed class FloraMechanic : ExtraordinaryMechanic
{
    public const string GrowthRatePrefix = "flora.growth-rate:";

    public override string Prefix => "flora.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!TryParseGrowthRate(declaration, out _))
            return Result<PreparedMutation?>.Fail(
                "Effects: flora.growth-rate exige um multiplicador >= 0");
        return Result<PreparedMutation?>.Ok(null);
    }

    internal static bool TryParseGrowthRate(string declaration, out double multiplier)
    {
        multiplier = 1;
        if (!declaration.StartsWith(GrowthRatePrefix, StringComparison.Ordinal)) return false;
        string numeric = declaration[GrowthRatePrefix.Length..];
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier)
               && multiplier >= 0;
    }

    /// <summary>Multiplicador de <c>flora.growth-rate</c> sobre a taxa de base (REALISM-11).
    /// Sem poder ativo (ou extraordinário desligado) retorna 1 — nunca substitui o cálculo
    /// de temperatura/estação.</summary>
    internal static double GrowthRateMultiplier(WorldState world, Plant plant)
    {
        if (!world.Extraordinary.Enabled)
            return 1;

        double rate = 1;
        foreach (var carrierState in world.ExtraordinaryCarriers.OrderBy(item => item.CarrierId.Value))
        {
            if (carrierState is not { IsManifested: true }) continue;
            if (world.FindNpc(carrierState.CarrierId) is not { IsAlive: true } carrier) continue;

            foreach (var descriptor in world.Extraordinary.Descriptors
                         .Where(item => carrierState.PowerIds.Contains(item.Id, StringComparer.Ordinal))
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (!ExtraordinaryManifestationCondition.IsMet(descriptor.ManifestationCondition, world, carrier))
                    continue;
                if (!PlantInArea(world, carrier, descriptor.Effects, plant)) continue;

                foreach (var effect in descriptor.Effects)
                {
                    if (TryParseGrowthRate(effect, out double multiplier))
                        rate *= multiplier;
                }
            }
        }

        return rate;
    }

    internal static int GrowthIncrement(WorldState world, Plant plant) =>
        (int)Math.Floor(GrowthRateMultiplier(world, plant));

    internal static bool PlantInArea(
        WorldState world, Npc carrier, IReadOnlyList<string> effects, Plant plant)
    {
        var selectors = effects.Where(AreaTargetResolver.IsSelector).ToList();
        if (selectors.Count == 0) return true;

        bool included = true;
        foreach (var selector in selectors.OrderBy(item => item, StringComparer.Ordinal))
        {
            var parts = selector.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || parts[0] != "area" || !int.TryParse(parts[2], out int value) || value < 0)
                return false;
            included = parts[1] switch
            {
                "radius" => included && FaunaMechanic.Chebyshev(plant.Position, carrier.CurrentLocation) <= value,
                "region" => included && world.Map.TryGetCell(plant.Position, out _)
                    && world.Map.RegionOf(plant.Position).Value == value,
                _ => false,
            };
        }

        return included;
    }
}

/// <summary>Registro extraordinário legado. O avanço de estágio passou a
/// <see cref="FloraLifecycleSystem"/> (taxa base × multiplicador de poder); este sistema
/// não muta flora — evita dobro de avanço quando ambos estão no clock.</summary>
public sealed class FloraGrowthSystem : ISimulationSystem
{
    public const string SystemName = "ExtraordinaryFlora";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        // no-op: FloraLifecycleSystem.AdvanceStage aplica GrowthRateMultiplier sobre a taxa base.
    }
}
