namespace LivingWorld.Domain;

/// <summary>Parâmetros de ciclo de vida por espécie vegetal (Fase 16.3) — consumido só por
/// <c>FloraLifecycleSystem</c>.</summary>
public sealed record PlantSpeciesRules(
    string Species,
    float MinToleratedTemp,
    float MaxToleratedTemp,
    int MaturityStage,
    int CropResourceId,
    double YieldPerMaturePlant,
    double ReproduceRadius,
    double ReproduceProbability);
