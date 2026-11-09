using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Seletor de área lido de um token: <see cref="Kind"/> <c>radius</c> ou <c>region</c>, ou vazio
/// para um token <c>area:...</c> que não tem forma de seletor (inerte).</summary>
public readonly record struct AreaSelector(string Kind, int Value)
{
    public bool IsSelector => !string.IsNullOrEmpty(Kind);
}

/// <summary>Marca o token de área no descritor; a expansão de alvos vive em <see cref="AreaTargetResolver"/>.</summary>
public sealed class AreaSelectorMechanic : ExtraordinaryMechanic
{
    public const string RadiusPrefix = "area:radius:";
    public const string RegionPrefix = "area:region:";

    public override string Prefix => "area:";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    /// <summary>
    /// Parser único do seletor (carga, invocação e <see cref="AreaTargetResolver"/>). Token com forma de
    /// seletor (<c>area:radius:</c>/<c>area:region:</c>) exige <c>area:&lt;kind&gt;:&lt;n&gt;</c> com n inteiro
    /// não negativo, lido cru (sem a redução tipada <c>chave:tipo:n</c>). Qualquer outro <c>area:...</c>
    /// a invocação aceita sem efeito (ADR-0040), então o parser devolve um seletor inerte.
    /// </summary>
    public static readonly ExtraordinaryPrimitive<AreaSelector> Selector = new(
        "area:",
        [
            new PrimitiveListing(RadiusPrefix, "area:radius:<n>", false, "area"),
            new PrimitiveListing(RegionPrefix, "area:region:<id>", false, "area"),
        ],
        static token =>
        {
            if (!IsSelector(token))
                return Result<AreaSelector>.Ok(default);
            var parts = token.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || parts[0] != "area"
                || parts[1] is not ("radius" or "region")
                || !int.TryParse(parts[2], out int value) || value < 0)
                return Result<AreaSelector>.Fail($"Effects: seletor de área inválido '{token}'");
            return Result<AreaSelector>.Ok(new AreaSelector(parts[1], value));
        },
        allowTyped: false);

    public static bool IsSelector(string token) =>
        token.StartsWith(RadiusPrefix, StringComparison.Ordinal)
        || token.StartsWith(RegionPrefix, StringComparison.Ordinal);

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Selector];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = Selector.Parse(declaration);
        return parsed.IsSuccess
            ? Result<PreparedMutation?>.Ok(null)
            : Result<PreparedMutation?>.Fail(parsed.Error!);
    }
}
