namespace LivingWorld.Simulation.Observation;

/// <summary>Amostra de uma cidade num ano: <c>metric → valor</c>. Formato aberto (chaves documentadas em
/// <c>docs/domain/civilization-observatory.md</c>): sub-issue futura acrescenta chave, não migra esquema.</summary>
public sealed record CitySample(string Id, IReadOnlyDictionary<string, double> Metrics);

/// <summary>Amostra do mundo ao fim do ano <see cref="Year"/> (1 = primeiro ano completo): total do mundo
/// em <see cref="World"/> e uma <see cref="CitySample"/> por cidade ativa, ordenada por id.</summary>
public sealed record WorldYearSample(
    int Year, long Tick, IReadOnlyDictionary<string, double> World, IReadOnlyList<CitySample> Cities);

/// <summary>A série de um mundo (uma seed): uma <see cref="WorldYearSample"/> por ano.</summary>
public sealed record SeedRun(ulong Seed, IReadOnlyList<WorldYearSample> Years);
