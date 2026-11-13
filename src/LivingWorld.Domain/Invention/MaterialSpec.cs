namespace LivingWorld.Domain.Invention;

/// <summary>Material bruto da natureza do cenário (ADR-0045): só propriedades físicas medidas, nunca o que se faz com ele. O id é
/// do material (espaço próprio, não o de recurso); o nome mora fora do motor. A peça natural (<see cref="NaturalShape"/> e os
/// limites de tamanho) é o que a natureza entrega antes de qualquer trabalho: um tronco, um feixe, uma pele, uma pedra.
/// <see cref="CoatingOnly"/>: só serve de revestimento (resina, piche). <see cref="Formable"/>: lâmina que se molda sobre uma
/// armação (pele), então a peça oca pode passar da espessura natural. <see cref="NeedsWaterNearby"/>: só existe à beira d'água.
/// Valores do mundo real com a marca "precisa de conferência" (registro AD-141).</summary>
public sealed record MaterialSpec(
    int Id,
    int DensityKgM3,
    int TensileMPa,
    int ImpermeabilityPm,
    PartShape NaturalShape,
    int MaxPieceMm,
    int MaxThicknessMm,
    int MinWallMm,
    int GatherMinutesPerKg,
    int WorkMinutesPerKg,
    int LifetimeMonths,
    bool NeedsWaterNearby = false,
    bool CoatingOnly = false,
    bool Formable = false,
    int ColorRgb = 0x808080,
    int FinishPm = 300,
    int GrainPm = 0,
    int ConductivityPm = 100)
{
    /// <summary>Boia sozinho em água de <paramref name="waterDensityKgM3"/>: o que quem vive à beira d'água vê acontecer.</summary>
    public bool FloatsIn(int waterDensityKgM3) => DensityKgM3 < waterDensityKgM3;

    public string? Validate()
    {
        if (Id <= 0 || Id > short.MaxValue) return $"Material {Id}: id fora de 1..{short.MaxValue}";
        if (DensityKgM3 <= 0) return $"Material {Id}: densidade deve ser > 0";
        if (TensileMPa < 0) return $"Material {Id}: resistência deve ser >= 0";
        if (ImpermeabilityPm is < 0 or > 1000) return $"Material {Id}: impermeabilidade fora de 0..1000";
        if (MaxPieceMm is <= 0 or > short.MaxValue || MaxThicknessMm <= 0 || MaxThicknessMm > MaxPieceMm)
            return $"Material {Id}: peça natural inválida";
        if (MinWallMm <= 0 || MinWallMm > MaxThicknessMm) return $"Material {Id}: parede mínima inválida";
        if (GatherMinutesPerKg < 0 || WorkMinutesPerKg < 0 || LifetimeMonths <= 0) return $"Material {Id}: custo ou vida inválidos";
        return null;
    }
}
