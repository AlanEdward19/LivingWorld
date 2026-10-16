using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Catálogo de ids de cultura/profissão/tipo-de-local válidos para um cenário
/// — mesmo padrão de <see cref="GeographyCatalog"/>. Conjunto vazio = sem restrição
/// declarada (id passa), igual à convenção já usada para bioma/recurso.</summary>
public sealed record PopulationCatalog(
    HashSet<int> CultureIds,
    HashSet<int> ProfessionIds,
    HashSet<int> LocationTypeIds,
    IReadOnlyDictionary<int, double>? ProfessionWeights = null)
{
    public bool IsValidCulture(CultureId culture) => CultureIds.Count == 0 || CultureIds.Contains(culture.Id);

    public bool IsValidProfession(ProfessionType profession) =>
        ProfessionIds.Count == 0 || ProfessionIds.Contains(profession.Id);

    public bool IsValidLocationType(LocationType type) =>
        LocationTypeIds.Count == 0 || LocationTypeIds.Contains(type.Id);

    /// <summary>Sorteio entre <see cref="ProfessionIds"/> (viés de
    /// período) — ordenado antes do sorteio, nunca por ordem de iteração do
    /// <see cref="HashSet{T}"/> (não determinístico entre processos). Catálogo vazio retorna
    /// <see cref="ProfessionType.None"/>, nunca lança exceção. Sem <see cref="ProfessionWeights"/>
    /// declarado (o caso comum, cenário sem viés de período) o sorteio é uniforme, byte a
    /// byte igual ao algoritmo original — profissão sem peso explícito em
    /// <see cref="ProfessionWeights"/> vale peso 1 (mesma chance de antes).</summary>
    public ProfessionType RollProfession(WorldRng rng)
    {
        if (ProfessionIds.Count == 0) return ProfessionType.None;

        var sorted = ProfessionIds.OrderBy(id => id).ToList();

        if (ProfessionWeights is null || ProfessionWeights.Count == 0)
        {
            int uniformIndex = Math.Min((int)(rng.NextDouble() * sorted.Count), sorted.Count - 1);
            return new ProfessionType(sorted[uniformIndex]);
        }

        double totalWeight = sorted.Sum(id => ProfessionWeights.GetValueOrDefault(id, 1.0));
        double target = rng.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var id in sorted)
        {
            cumulative += ProfessionWeights.GetValueOrDefault(id, 1.0);
            if (target < cumulative) return new ProfessionType(id);
        }
        return new ProfessionType(sorted[^1]);
    }
}
