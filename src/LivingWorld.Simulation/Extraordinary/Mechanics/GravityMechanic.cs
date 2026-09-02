using System.Globalization;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Gravidade pessoal. <c>gravity.self:&lt;mult&gt;</c> (0 = sem peso, 1 = normal, &gt;1 = mais
/// pesado) e <c>gravity.target:&lt;mult&gt;</c> no alvo. <c>movement.flight</c> /
/// <c>movement.speed-multiplier</c> são sinônimos de <c>gravity.self</c> (PWR-72).
/// </summary>
public sealed class GravityMechanic : ExtraordinaryMechanic
{
    /// <summary>
    /// Voo só com gravidade efetiva exatamente 0 (sem peso). Valores em (0, 1) aliviam
    /// (aumentam o orçamento); &gt;1 pesam (reduzem o orçamento). Não usar limiar &lt; 1.0:
    /// isso faria <c>movement.speed-multiplier</c> (mapeado para 1/N) habilitar voo.
    /// </summary>
    public const double FlightThreshold = 0;

    public override string Prefix => "gravity.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (TryParseSelf(declaration, out _))
            return Result<PreparedMutation?>.Ok(null);

        if (!TryParseTarget(declaration, out double multiplier))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var world = ctx.World;
        var target = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var existing = world.ExtraordinaryCarriers
                .FirstOrDefault(item => item.CarrierId == target.Id);
            var next = existing is null
                ? new ExtraordinaryCarrierState(
                    target.Id, [], false, "dormant",
                    new ExtraordinaryAppearanceState(1, "", ""), null, 1,
                    GravityTargetMultiplier: multiplier)
                : existing with { GravityTargetMultiplier = multiplier };
            world.UpsertExtraordinaryCarrier(next);
        }));
    }

    internal static ExtraordinaryLocomotionProfile ResolveProfile(WorldState world, Npc npc)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        bool canFly = false;
        double dash = 1;
        double weight = ComposeWeight(world, carrier, ref canFly, ref dash);
        double speed = canFly ? dash : dash / weight;
        bool hasModifier = canFly || speed != 1;
        return new(hasModifier, canFly, speed);
    }

    /// <summary>Produto de gravidade não-nula (peso) × alvo. Voo e speed-multiplier ficam
    /// eixos separados para <c>movement.flight</c>+<c>speed-multiplier</c> (PWR-72).</summary>
    internal static double Compose(WorldState world, ExtraordinaryCarrierState? carrier)
    {
        bool canFly = false;
        double dash = 1;
        return ComposeWeight(world, carrier, ref canFly, ref dash);
    }

    private static double ComposeWeight(
        WorldState world, ExtraordinaryCarrierState? carrier, ref bool canFly, ref double dash)
    {
        double weight = 1;
        if (carrier is { IsManifested: true })
        {
            foreach (var descriptor in world.Extraordinary.Descriptors
                         .Where(item => carrier.PowerIds.Contains(item.Id, StringComparer.Ordinal))
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                foreach (var effect in descriptor.Effects)
                {
                    if (TryParseSelf(effect, out double self))
                    {
                        if (self == FlightThreshold) canFly = true;
                        else weight *= self;
                        continue;
                    }

                    if (!TryPositiveAmount(effect, out string key, out double amount))
                        continue;
                    if (key == "movement.flight")
                    {
                        canFly = true;
                        dash = Math.Max(dash, amount);
                    }
                    else if (key == "movement.speed-multiplier") dash = Math.Max(dash, amount);
                }
            }
        }

        if (carrier is not null)
            weight *= carrier.GravityTargetMultiplier;
        return weight <= 0 ? 1 : weight;
    }

    internal static bool TryParseSelf(string declaration, out double multiplier) =>
        TryKeyMultiplier(declaration, "gravity.self", out multiplier) && multiplier >= 0;

    internal static bool TryParseTarget(string declaration, out double multiplier) =>
        TryKeyMultiplier(declaration, "gravity.target", out multiplier) && multiplier >= 0;

    private static bool TryKeyMultiplier(string declaration, string key, out double multiplier)
    {
        multiplier = 0;
        string prefix = key + ":";
        if (!declaration.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return double.TryParse(
            declaration[prefix.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier);
    }

    private static bool TryPositiveAmount(string declaration, out string key, out double amount)
    {
        int separator = declaration.LastIndexOf(':');
        key = separator > 0 ? declaration[..separator] : "";
        amount = 0;
        return separator > 0
            && double.TryParse(
                declaration[(separator + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out amount)
            && amount > 0;
    }
}
