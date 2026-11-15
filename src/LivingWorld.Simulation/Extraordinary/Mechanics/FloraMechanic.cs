using System.Globalization;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// <c>flora.growth-rate:&lt;multiplicador&gt;</c> numa área (<c>area:radius</c>/<c>area:region</c>).
/// </summary>
public sealed class FloraMechanic : ExtraordinaryMechanic
{
    public const string GrowthRatePrefix = "flora.growth-rate:";

    public override string Prefix => "flora.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    /// <summary>Parser único de <c>flora.growth-rate:&lt;multiplicador&gt;</c>.</summary>
    public static readonly ExtraordinaryPrimitive<double> GrowthRate = new(
        GrowthRatePrefix, "flora.growth-rate:<multiplicador>", false, "area",
        PrimitiveSyntax.From<double>(ParseGrowthRate, static _ => "Effects: flora.growth-rate exige um multiplicador >= 0"));

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [GrowthRate];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = GrowthRate.Parse(declaration);
        return parsed.IsSuccess
            ? Result<PreparedMutation?>.Ok(null)
            : Result<PreparedMutation?>.Fail(parsed.Error!);
    }

    internal static bool TryParseGrowthRate(string declaration, out double multiplier) =>
        GrowthRate.TryParse(declaration, out multiplier);

    private static bool ParseGrowthRate(string declaration, out double multiplier)
    {
        multiplier = 1;
        if (!declaration.StartsWith(GrowthRatePrefix, StringComparison.Ordinal)) return false;
        string numeric = declaration[GrowthRatePrefix.Length..];
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier)
               && multiplier >= 0;
    }

    /// <summary>Um portador vivo e manifesto cujo poder mexe na flora nesta hora: os efeitos (para a área) e os multiplicadores
    /// de <c>flora.growth-rate</c> na ordem declarada.</summary>
    internal sealed record FloraSource(Npc Carrier, IReadOnlyList<string> Effects, double[] Multipliers);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<Domain.Extraordinary.PowerDescriptor>, object>
        FloraDescriptorCache = new();

    /// <summary>Algum portador vivo tem poder que mexe na flora? Sem isso a flora roda como com poderes desligados (inclusive
    /// o salto adiado de <c>FloraLifecycleSystem</c>), porque o multiplicador seria 1 em toda planta (AD-156, 08b item 106).
    /// Volta a falso quando o último desses portadores morre.</summary>
    internal static bool PowersCanTouchFlora(WorldState world)
    {
        if (!world.Extraordinary.Enabled) return false;
        var floraPowers = (HashSet<string>)FloraDescriptorCache.GetValue(world.Extraordinary.Descriptors, static list =>
            list.Where(d => d.Effects.Any(e => TryParseGrowthRate(e, out _))).Select(d => d.Id).ToHashSet(StringComparer.Ordinal));
        if (floraPowers.Count == 0) return false;
        foreach (var (state, npc) in world.LivingCarriers())
            if (npc.IsAlive)
                foreach (string id in state.PowerIds)
                    if (floraPowers.Contains(id)) return true;
        return false;
    }

    /// <summary>Fontes da hora, uma vez por tick em vez de uma vez por planta: mesma ordem (portador por id, descritor por id,
    /// efeito na ordem declarada) e mesmas condições do cálculo por planta. Nula quando nenhuma fonte age.</summary>
    internal static List<FloraSource>? ActiveSources(WorldState world)
    {
        if (!world.Extraordinary.Enabled) return null;
        List<FloraSource>? sources = null;
        foreach (var (carrierState, carrier) in world.LivingCarriers()) // só vivos, ordem de id
        {
            if (carrierState is not { IsManifested: true }) continue;
            if (!carrier.IsAlive) continue;

            foreach (var descriptor in world.Extraordinary.Descriptors
                         .Where(item => carrierState.PowerIds.Contains(item.Id, StringComparer.Ordinal))
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (!ExtraordinaryManifestationCondition.IsMet(descriptor.ManifestationCondition, world, carrier))
                    continue;
                var multipliers = new List<double>();
                foreach (var effect in descriptor.Effects)
                    if (TryParseGrowthRate(effect, out double multiplier))
                        multipliers.Add(multiplier);
                if (multipliers.Count == 0) continue;
                (sources ??= []).Add(new FloraSource(carrier, descriptor.Effects, multipliers.ToArray()));
            }
        }
        return sources;
    }

    /// <summary>Multiplicador de <c>flora.growth-rate</c> sobre a taxa de base.
    /// Sem poder ativo (ou extraordinário desligado) retorna 1 — nunca substitui o cálculo
    /// de temperatura/estação.</summary>
    internal static double GrowthRateMultiplier(WorldState world, Plant plant) =>
        Multiplier(ActiveSources(world), world, plant);

    internal static double Multiplier(List<FloraSource>? sources, WorldState world, Plant plant)
    {
        double rate = 1;
        if (sources is null) return rate;
        foreach (var source in sources)
        {
            if (!PlantInArea(world, source.Carrier, source.Effects, plant)) continue;
            foreach (double multiplier in source.Multipliers)
                rate *= multiplier;
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
            var parsed = AreaSelectorMechanic.Selector.Parse(selector);
            if (!parsed.IsSuccess)
                return false;
            int value = parsed.Value.Value;
            included = parsed.Value.Kind switch
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
[LivingCapability("EXTRAORDINARY")]
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
