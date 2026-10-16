namespace LivingWorld.Domain.Ecology;

/// <summary>Parâmetros de ciclo de vida por espécie vegetal — consumido só por
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
