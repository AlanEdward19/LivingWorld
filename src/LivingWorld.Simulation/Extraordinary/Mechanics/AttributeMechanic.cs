using System.Globalization;
using LivingWorld.Domain.Extraordinary;
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

    // Parsers únicos dos valores (carga, invocação e leitores de efeito dos sistemas consumidores).
    public static readonly ExtraordinaryPrimitive<double> Fertility = Multiplier("attribute.fertility");
    public static readonly ExtraordinaryPrimitive<double> Strength = Multiplier("attribute.strength");
    public static readonly ExtraordinaryPrimitive<double> ReactionSpeed = Multiplier("attribute.reaction-speed");

    public static readonly ExtraordinaryPrimitive<int> Perception = new(
        "attribute.perception", "attribute.perception:<fator>", false, "carrier",
        PrimitiveSyntax.From<int>(ParseRadius, static token => $"Effects: alvo não suportado '{token}'"));

    private static readonly HashSet<string> Keys =
    [
        "attribute.fertility", "attribute.strength", "attribute.perception", "attribute.reaction-speed",
    ];

    /// <summary>A invocação aceita o token cuja chave (antes do último <c>:</c>) é um dos quatro atributos,
    /// mesmo com valor ausente ou inválido: o leitor do sistema consumidor ignora o lixo (ADR-0040).
    /// Fora do catálogo; os quatro parsers acima valem para quem lê o valor.</summary>
    public static readonly ExtraordinaryPrimitive<string> KeyOnly = new(
        "attribute.", [],
        static token =>
        {
            int separator = token.LastIndexOf(':');
            string key = separator > 0 ? token[..separator] : token;
            return Keys.Contains(key)
                ? Result<string>.Ok(key)
                : Result<string>.Fail($"Effects: alvo não suportado '{key}'");
        });

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
        [Fertility, Strength, Perception, ReactionSpeed, KeyOnly];

    private static ExtraordinaryPrimitive<double> Multiplier(string key)
    {
        string prefix = key + ":";
        return new(
            key, $"{key}:<fator>", false, "carrier",
            PrimitiveSyntax.From<double>(
                (string token, out double value) => ParseMultiplier(token, prefix, out value),
                static token => $"Effects: alvo não suportado '{token}'"));
    }

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var accepted = KeyOnly.Parse(declaration);
        return accepted.IsSuccess
            ? Result<PreparedMutation?>.Ok(null)
            : Result<PreparedMutation?>.Fail(accepted.Error!);
    }

    public static long EffectiveCarryCapacity(WorldState world, Npc npc)
    {
        double scaled = npc.CarryCapacity * StrengthMultiplier(world, npc);
        return Math.Max(0, (long)Math.Floor(scaled));
    }

    public static double StrengthMultiplier(WorldState world, Npc npc) =>
        ProductMultiplier(world, npc, Strength);

    public static double FertilityMultiplier(WorldState world, Npc npc) =>
        ProductMultiplier(world, npc, Fertility);

    /// <summary>Chebyshev em tiles. Sem poder manifesto o raio é 1 (adjacência, incluindo o
    /// próprio tile a distância 0). Com <c>attribute.perception</c>, o maior raio declarado.</summary>
    public static int PerceptionRadius(WorldState world, Npc npc)
    {
        if (world.ExtraordinaryCarriers.Count == 0)
            return 1;

        var carrier = world.CarrierOf(npc.Id);
        if (carrier is not { IsManifested: true })
            return 1;

        return DeclaredPerceptionRadius(world, carrier);
    }

    /// <summary>Fora de <see cref="PerceptionRadius"/> porque o lambda captura <paramref name="carrier"/> e o compilador
    /// aloca o fecho no início do método que o declara: todo NPC sem poder pagava o fecho a cada decisão.</summary>
    private static int DeclaredPerceptionRadius(WorldState world, ExtraordinaryCarrierState carrier)
    {
        int radius = 1;
        bool found = false;
        foreach (var effect in world.Extraordinary.Descriptors
                     .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal))
                     .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                     .SelectMany(descriptor => descriptor.Effects))
        {
            if (!Perception.TryParse(effect, out int declared))
                continue;
            radius = found ? Math.Max(radius, declared) : declared;
            found = true;
        }

        return found ? radius : 1;
    }

    public static double ReactionSpeedMultiplier(WorldState world, Npc npc) =>
        ProductMultiplier(world, npc, ReactionSpeed);

    private static double ProductMultiplier(WorldState world, Npc npc, ExtraordinaryPrimitive<double> primitive)
    {
        var carrier = world.CarrierOf(npc.Id);
        if (carrier is not { IsManifested: true })
            return 1;

        return DeclaredProduct(world, carrier, primitive);
    }

    /// <summary>Ver <see cref="DeclaredPerceptionRadius"/>: o fecho do lambda só nasce para quem tem poder manifesto.</summary>
    private static double DeclaredProduct(WorldState world, ExtraordinaryCarrierState carrier, ExtraordinaryPrimitive<double> primitive)
    {
        double product = 1;
        bool found = false;
        foreach (var effect in world.Extraordinary.Descriptors
                     .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal))
                     .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                     .SelectMany(descriptor => descriptor.Effects))
        {
            if (!primitive.TryParse(effect, out double multiplier))
                continue;
            product *= multiplier;
            found = true;
        }

        return found ? product : 1;
    }

    private static bool ParseMultiplier(string declaration, string token, out double multiplier)
    {
        multiplier = 1;
        if (!declaration.StartsWith(token, StringComparison.Ordinal))
            return false;
        return double.TryParse(
                   declaration[token.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier)
               && multiplier >= 0;
    }

    private static bool ParseRadius(string declaration, out int radius)
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
