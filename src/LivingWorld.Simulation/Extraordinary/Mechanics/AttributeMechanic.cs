using System.Globalization;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Modificadores de atributo são lidos pelos sistemas consumidores (ex.: natalidade),
/// não aplicados como mutação de invocação — mesmo padrão de <see cref="MovementEffectMechanic"/>.
/// </summary>
public sealed class AttributeMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "attribute.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        int separator = declaration.LastIndexOf(':');
        string key = separator > 0 ? declaration[..separator] : declaration;
        return key switch
        {
            "attribute.fertility" => Result<PreparedMutation?>.Ok(null),
            "attribute.strength" => Result<PreparedMutation?>.Ok(null),
            "attribute.perception" => Result<PreparedMutation?>.Ok(null),
            "attribute.reaction-speed" => Result<PreparedMutation?>.Ok(null),
            _ => Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{key}'"),
        };
    }

    public static long EffectiveCarryCapacity(WorldState world, Npc npc)
    {
        double scaled = npc.CarryCapacity * StrengthMultiplier(world, npc);
        return Math.Max(0, (long)Math.Floor(scaled));
    }

    public static double StrengthMultiplier(WorldState world, Npc npc) =>
        ProductMultiplier(world, npc, "attribute.strength:");

    public static double FertilityMultiplier(WorldState world, Npc npc) =>
        ProductMultiplier(world, npc, "attribute.fertility:");

    /// <summary>Chebyshev em tiles. Sem poder manifesto o raio é 1 (adjacência, incluindo o
    /// próprio tile a distância 0). Com <c>attribute.perception</c>, o maior raio declarado.</summary>
    public static int PerceptionRadius(WorldState world, Npc npc)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (carrier is not { IsManifested: true })
            return 1;

        int radius = 1;
        bool found = false;
        foreach (var effect in world.Extraordinary.Descriptors
                     .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal))
                     .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                     .SelectMany(descriptor => descriptor.Effects))
        {
            if (!TryParseRadius(effect, out int declared))
                continue;
            radius = found ? Math.Max(radius, declared) : declared;
            found = true;
        }

        return found ? radius : 1;
    }

    public static double ReactionSpeedMultiplier(WorldState world, Npc npc) =>
        ProductMultiplier(world, npc, "attribute.reaction-speed:");

    private static double ProductMultiplier(WorldState world, Npc npc, string token)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (carrier is not { IsManifested: true })
            return 1;

        double product = 1;
        bool found = false;
        foreach (var effect in world.Extraordinary.Descriptors
                     .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal))
                     .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                     .SelectMany(descriptor => descriptor.Effects))
        {
            if (!TryParseMultiplier(effect, token, out double multiplier))
                continue;
            product *= multiplier;
            found = true;
        }

        return found ? product : 1;
    }

    private static bool TryParseMultiplier(string declaration, string token, out double multiplier)
    {
        multiplier = 1;
        if (!declaration.StartsWith(token, StringComparison.Ordinal))
            return false;
        return double.TryParse(
                   declaration[token.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier)
               && multiplier >= 0;
    }

    private static bool TryParseRadius(string declaration, out int radius)
    {
        radius = 1;
        const string token = "attribute.perception:";
        if (!declaration.StartsWith(token, StringComparison.Ordinal))
            return false;
        return int.TryParse(
                   declaration[token.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out radius)
               && radius >= 0;
    }
}
