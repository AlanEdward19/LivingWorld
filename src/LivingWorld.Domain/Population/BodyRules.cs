namespace LivingWorld.Domain;

/// <summary>Parâmetros cenário-driven do corpo mínimo causal (Fase 16.3, COH-21) —
/// distribuição truncada de <c>Height</c>/<c>Weight</c>/<c>MuscleMass</c> na criação do NPC.
/// Mesmo template de <see cref="FamilyRules"/>/<see cref="PowerInheritanceRules"/>:
/// validação em <see cref="Create"/>, <see cref="Default"/> documentado.</summary>
/// <remarks>
/// Unidades: <see cref="HeightMean"/> em metros; <see cref="WeightMean"/> e
/// <see cref="MuscleMassMean"/> em kg. Defaults documentados (adulto medieval médio):
/// <list type="bullet">
/// <item>Height 1.70 ± 0.08 m</item>
/// <item>Weight 68 ± 10 kg</item>
/// <item>MuscleMass 28 ± 6 kg, clamp [8, 55]</item>
/// </list>
/// </remarks>
public sealed record BodyRules(
    double HeightMean,
    double HeightStdDev,
    double WeightMean,
    double WeightStdDev,
    double MuscleMassMean,
    double MuscleMassStdDev,
    double MuscleMassMin,
    double MuscleMassMax,
    bool Enabled)
{
    /// <summary>Faixa derivada de clamp para Height (mean ± 4σ, piso fisiológico 0.5 m) —
    /// BodyRules não declara HeightMin/Max explícitos; geração usa estes limites.</summary>
    public double HeightMin => Math.Max(0.5, HeightMean - 4.0 * HeightStdDev);

    /// <summary>Faixa derivada de clamp para Height (mean ± 4σ).</summary>
    public double HeightMax => HeightMean + 4.0 * HeightStdDev;

    /// <summary>Faixa derivada de clamp para Weight (mean ± 4σ, piso fisiológico 20 kg).</summary>
    public double WeightMin => Math.Max(20.0, WeightMean - 4.0 * WeightStdDev);

    /// <summary>Faixa derivada de clamp para Weight (mean ± 4σ).</summary>
    public double WeightMax => WeightMean + 4.0 * WeightStdDev;

    public static Result<BodyRules> Create(
        double heightMean,
        double heightStdDev,
        double weightMean,
        double weightStdDev,
        double muscleMassMean,
        double muscleMassStdDev,
        double muscleMassMin,
        double muscleMassMax,
        bool enabled)
    {
        if (heightStdDev < 0)
            return Result<BodyRules>.Fail("HeightStdDev: deve ser >= 0");
        if (weightStdDev < 0)
            return Result<BodyRules>.Fail("WeightStdDev: deve ser >= 0");
        if (muscleMassStdDev < 0)
            return Result<BodyRules>.Fail("MuscleMassStdDev: deve ser >= 0");
        if (muscleMassMin > muscleMassMax)
            return Result<BodyRules>.Fail("MuscleMassMin: não pode exceder MuscleMassMax");
        if (heightMean <= 0)
            return Result<BodyRules>.Fail("HeightMean: deve ser > 0");
        if (weightMean <= 0)
            return Result<BodyRules>.Fail("WeightMean: deve ser > 0");
        if (muscleMassMean < 0)
            return Result<BodyRules>.Fail("MuscleMassMean: deve ser >= 0");

        return Result<BodyRules>.Ok(new BodyRules(
            heightMean, heightStdDev, weightMean, weightStdDev,
            muscleMassMean, muscleMassStdDev, muscleMassMin, muscleMassMax, enabled));
    }

    /// <summary>Default documentado (adulto medieval médio) com <see cref="Enabled"/> verdadeiro
    /// — cenário que omite BodyRules ainda gera corpo plausível. Use
    /// <see cref="Disabled"/> quando o multiplicador corporal deve ser neutro 1.0.</summary>
    public static readonly BodyRules Default = Create(
        heightMean: 1.70,
        heightStdDev: 0.08,
        weightMean: 68.0,
        weightStdDev: 10.0,
        muscleMassMean: 28.0,
        muscleMassStdDev: 6.0,
        muscleMassMin: 8.0,
        muscleMassMax: 55.0,
        enabled: true).Value
        ?? throw new InvalidOperationException("BodyRules.Default inválida — bug no cenário");

    /// <summary>Corpo desligado — geração ainda pode preencher campos, mas
    /// <c>BodyMechanic</c> devolve multiplicadores neutros 1.0.</summary>
    public static readonly BodyRules Disabled = Default with { Enabled = false };

    /// <summary>Usa a regra declarada; se ausente, devolve <see cref="Default"/>.</summary>
    public static BodyRules Resolve(BodyRules? declared) => declared ?? Default;
}
