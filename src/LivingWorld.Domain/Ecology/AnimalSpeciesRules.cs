namespace LivingWorld.Domain.Ecology;

/// <summary>Parâmetros de ciclo de vida por espécie animal (Fase 16.4) — consumido só por
/// <c>FaunaLifecycleSystem</c>.</summary>
public sealed record AnimalSpeciesRules(
    string Species,
    double HungerDecayPerTick,
    double ReproduceEnergyThreshold,
    double ReproduceRadius,
    double ReproduceProbability,
    string? PredatorOf,
    double PredationProbability);
