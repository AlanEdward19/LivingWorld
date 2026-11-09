using System.Globalization;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Gravidade pessoal. <c>gravity.self:&lt;mult&gt;</c> (0 = sem peso, 1 = normal, &gt;1 = mais
/// pesado) e <c>gravity.target:&lt;mult&gt;</c> no alvo. <c>movement.flight</c> /
/// <c>movement.speed-multiplier</c> são sinônimos de <c>gravity.self</c>.
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

    /// <summary>Parser único de <c>gravity.self:&lt;m&gt;</c> (lido também por <c>ComposeWeight</c>).</summary>
    public static readonly ExtraordinaryPrimitive<double> Self = Multiplier("gravity.self", "carrier");

    /// <summary>Parser único de <c>gravity.target:&lt;m&gt;</c>.</summary>
    public static readonly ExtraordinaryPrimitive<double> TargetToken = Multiplier("gravity.target", "target");

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Self, TargetToken];

    private static ExtraordinaryPrimitive<double> Multiplier(string key, string target) => new(
        key, $"{key}:<multiplicador>", false, target,
        PrimitiveSyntax.From<double>(
            (string token, out double multiplier) => TryKeyMultiplier(token, key, out multiplier) && multiplier >= 0,
            static token => $"Effects: alvo não suportado '{token}'"));

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (Self.Accepts(declaration))
            return Result<PreparedMutation?>.Ok(null);

        var gravity = TargetToken.Parse(declaration);
        if (!gravity.IsSuccess)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        double multiplier = gravity.Value;

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
        var carrier = world.CarrierOf(npc.Id);
        bool canFly = false;
        double dash = 1;
        double weight = ComposeWeight(world, carrier, ref canFly, ref dash);
        double speed = canFly ? dash : dash / weight;
        bool hasModifier = canFly || speed != 1;
        return new(hasModifier, canFly, speed);
    }

    /// <summary>Produto de gravidade não-nula (peso) × alvo. Voo e speed-multiplier ficam
    /// eixos separados para <c>movement.flight</c>+<c>speed-multiplier</c>.</summary>
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

                    if (MovementEffectMechanic.Flight.TryParse(effect, out double flight))
                    {
                        canFly = true;
                        dash = Math.Max(dash, flight);
                    }
                    else if (MovementEffectMechanic.SpeedMultiplier.TryParse(effect, out double speed))
                        dash = Math.Max(dash, speed);
                }
            }
        }

        if (carrier is not null)
            weight *= carrier.GravityTargetMultiplier;
        return weight <= 0 ? 1 : weight;
    }

    internal static bool TryParseSelf(string declaration, out double multiplier) =>
        Self.TryParse(declaration, out multiplier);

    internal static bool TryParseTarget(string declaration, out double multiplier) =>
        TargetToken.TryParse(declaration, out multiplier);

    private static bool TryKeyMultiplier(string declaration, string key, out double multiplier)
    {
        multiplier = 0;
        string prefix = key + ":";
        if (!declaration.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return double.TryParse(
            declaration[prefix.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier);
    }
}
