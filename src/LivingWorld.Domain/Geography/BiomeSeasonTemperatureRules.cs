namespace LivingWorld.Domain;

/// <summary>Curva sazonal de delta de temperatura por bioma (Fase 16.3). <see
/// cref="SeasonDeltas"/> tem uma entrada por estação (4 estações = 12 meses / 3).</summary>
public sealed record BiomeSeasonTemperatureRules(int BiomeId, IReadOnlyList<float> SeasonDeltas);
