namespace LivingWorld.Domain;

/// <summary>Catálogo de ids de cultura/profissão/tipo-de-local válidos para um cenário
/// (task 7) — mesmo padrão de <see cref="GeographyCatalog"/>. Conjunto vazio = sem restrição
/// declarada (id passa), igual à convenção já usada para bioma/recurso.</summary>
public sealed record PopulationCatalog(
    HashSet<int> CultureIds,
    HashSet<int> ProfessionIds,
    HashSet<int> LocationTypeIds)
{
    public bool IsValidCulture(CultureId culture) => CultureIds.Count == 0 || CultureIds.Contains(culture.Id);

    public bool IsValidProfession(ProfessionType profession) =>
        ProfessionIds.Count == 0 || ProfessionIds.Contains(profession.Id);

    public bool IsValidLocationType(LocationType type) =>
        LocationTypeIds.Count == 0 || LocationTypeIds.Contains(type.Id);

    /// <summary>Sorteio uniforme entre <see cref="ProfessionIds"/> (task 7) — ordenado antes do
    /// sorteio, nunca por ordem de iteração do <see cref="HashSet{T}"/> (não determinístico entre
    /// processos). Catálogo vazio retorna <see cref="ProfessionType.None"/>, nunca lança
    /// exceção.</summary>
    public ProfessionType RollProfession(WorldRng rng)
    {
        if (ProfessionIds.Count == 0) return ProfessionType.None;

        var sorted = ProfessionIds.OrderBy(id => id).ToList();
        int index = Math.Min((int)(rng.NextDouble() * sorted.Count), sorted.Count - 1);
        return new ProfessionType(sorted[index]);
    }
}
