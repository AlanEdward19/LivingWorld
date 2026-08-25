namespace LivingWorld.Domain;

/// <summary>Parâmetros de ciclo de vida por espécie animal (Fase 16.3) — consumido só por
/// <c>FaunaLifecycleSystem</c>.</summary>
public sealed record AnimalSpeciesRules(
    string Species,
    double HungerDecayPerTick,
    double ReproduceEnergyThreshold,
    double ReproduceRadius,
    double ReproduceProbability,
    string? PredatorOf,
    double PredationProbability);
