using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Emergence;

/// <summary>Regras da emergência aberta (emergence-first-slice, ADR-0013), declaradas no cenário: eixos da categoria
/// (mobilidade), limiar de novidade e o que a pressão, a habilidade e os recursos precisam ter para abrir um protótipo.
/// Desligado por padrão: nenhuma tecnologia emerge e o mundo antigo segue igual.</summary>
public sealed record EmergenceRules(
    bool Enabled,
    IReadOnlyList<string> AxisNames,
    int AxisMax,
    int NoveltyThreshold,
    int PressureTravelTicks,
    int MinAffectedHouseholds,
    double MinSkill,
    int PrototypeResourceId,
    long PrototypeResourceCost,
    long PrototypeTicks,
    int PaceBonusPctPerPoint,
    double SpreadAffinity)
{
    public const string MobilityCategory = "mobility";

    public static EmergenceRules Disabled { get; } = new(
        false, ["pace", "load", "reach"], 7, 3, 4, 3, 20, 4, 20, 720, 8, 10);

    /// <summary>Categoria de mobilidade ligada, com os valores que o cenário padrão usa nos testes.</summary>
    public static EmergenceRules Mobility { get; } = Disabled with { Enabled = true };

    public int AxisCount => AxisNames.Count;

    public Result<EmergenceRules> Validate()
    {
        if (AxisNames.Count == 0) return Result<EmergenceRules>.Fail("AxisNames: ao menos um eixo");
        if (AxisMax <= 0) return Result<EmergenceRules>.Fail("AxisMax: deve ser > 0");
        if (NoveltyThreshold <= 0) return Result<EmergenceRules>.Fail("NoveltyThreshold: deve ser > 0");
        if (PressureTravelTicks <= 0) return Result<EmergenceRules>.Fail("PressureTravelTicks: deve ser > 0");
        if (MinAffectedHouseholds <= 0) return Result<EmergenceRules>.Fail("MinAffectedHouseholds: deve ser > 0");
        if (PrototypeResourceCost < 0) return Result<EmergenceRules>.Fail("PrototypeResourceCost: deve ser >= 0");
        if (PrototypeTicks <= 0) return Result<EmergenceRules>.Fail("PrototypeTicks: deve ser > 0");
        if (PaceBonusPctPerPoint < 0) return Result<EmergenceRules>.Fail("PaceBonusPctPerPoint: deve ser >= 0");
        return Result<EmergenceRules>.Ok(this);
    }
}
