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
}
