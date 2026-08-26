namespace LivingWorld.Domain;

/// <summary>Parâmetros cenário-driven da herança de poder (Fase 16.2, EVO-10) — roll 1
/// (ocorre herança?) e pesos dos 3 caminhos (ambos / um só / mistura). Mesmo padrão de
/// <see cref="FamilyRules"/>/<see cref="PerfRules"/>: validação em <see cref="Create"/>,
/// default documentado quando o cenário omite a regra.</summary>
/// <remarks>
/// Defaults documentados (design 16.2):
/// <list type="bullet">
/// <item><see cref="InheritanceChance"/> = 1.0 — espelha <c>AcquisitionRules</c> genéricas
/// sem token <c>rate:</c> (aquisição sempre passa).</item>
/// <item><see cref="BothWeight"/>/<see cref="OneOfWeight"/>/<see cref="MixedWeight"/> =
/// 1/3 cada — distribuição uniforme sobrescrevível pelo cenário.</item>
/// </list>
/// </remarks>
public sealed record PowerInheritanceRules(
    double InheritanceChance,
    double BothWeight,
    double OneOfWeight,
    double MixedWeight)
{
    /// <summary>Peso uniforme documentado de cada caminho quando o cenário não declara
    /// pesos — 1/3.</summary>
    public const double UniformPathWeight = 1.0 / 3.0;

    /// <summary>Chance de herança documentada quando o cenário não declara — espelha
    /// aquisição sem <c>rate:</c> (sempre ocorre o roll de caminho).</summary>
    public const double DefaultInheritanceChance = 1.0;

    public static Result<PowerInheritanceRules> Create(
        double inheritanceChance,
        double bothWeight,
        double oneOfWeight,
        double mixedWeight)
    {
        if (inheritanceChance is < 0 or > 1)
            return Result<PowerInheritanceRules>.Fail("InheritanceChance: fora de [0,1]");
        if (bothWeight < 0)
            return Result<PowerInheritanceRules>.Fail("BothWeight: deve ser >= 0");
        if (oneOfWeight < 0)
            return Result<PowerInheritanceRules>.Fail("OneOfWeight: deve ser >= 0");
        if (mixedWeight < 0)
            return Result<PowerInheritanceRules>.Fail("MixedWeight: deve ser >= 0");

        double weightSum = bothWeight + oneOfWeight + mixedWeight;
        if (weightSum <= 0)
            return Result<PowerInheritanceRules>.Fail(
                "BothWeight + OneOfWeight + MixedWeight: soma deve ser > 0");

        return Result<PowerInheritanceRules>.Ok(new PowerInheritanceRules(
            inheritanceChance, bothWeight, oneOfWeight, mixedWeight));
    }

    /// <summary>Default documentado para cenário que ainda não declara
    /// <see cref="PowerInheritanceRules"/> — pesos uniformes 1/3 e
    /// <see cref="DefaultInheritanceChance"/>. Nunca falha por regra ausente; use
    /// <see cref="Resolve"/>.</summary>
    public static readonly PowerInheritanceRules Default = Create(
        inheritanceChance: DefaultInheritanceChance,
        bothWeight: UniformPathWeight,
        oneOfWeight: UniformPathWeight,
        mixedWeight: UniformPathWeight).Value
        ?? throw new InvalidOperationException(
            "PowerInheritanceRules.Default inválida — bug no cenário");

    /// <summary>Usa a regra declarada pelo cenário; se ausente, devolve
    /// <see cref="Default"/> — nunca falha por omissão.</summary>
    public static PowerInheritanceRules Resolve(PowerInheritanceRules? declared) =>
        declared ?? Default;
}
