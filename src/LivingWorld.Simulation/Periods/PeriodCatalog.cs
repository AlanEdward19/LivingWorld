namespace LivingWorld.Simulation.Periods;

/// <summary>Catálogo ativo de um período validado (Fase 13, T12): ids de profissão declarados no
/// bloco População (<see cref="LivingWorld.Domain.PopulationCatalog.ProfessionIds"/> — vazio
/// significa sem restrição, mesma convenção de sempre) + ids de habilidade referenciados no
/// bloco <c>Dynamics</c> (<see cref="SkillBias"/>). O motor só decide por id (AD-023/AD-025);
/// <see cref="ProfessionNames"/>/<see cref="SkillNames"/> (T14/PERIOD-22..23) só devolvem o nome
/// quando ele foi declarado num <see cref="ProfessionBias"/>/<see cref="SkillBias"/> do período —
/// id sem viés nomeado não aparece nesses dicionários.</summary>
public sealed record PeriodCatalog(
    IReadOnlyList<int> ProfessionIds,
    IReadOnlyList<int> SkillIds,
    IReadOnlyDictionary<int, string> ProfessionNames,
    IReadOnlyDictionary<int, string> SkillNames,
    PeriodDescriptors Descriptors)
{
    public static PeriodCatalog From(PeriodDefinition definition) => new(
        definition.Population.Catalog.ProfessionIds.OrderBy(id => id).ToList(),
        definition.Dynamics.SkillBiases.Select(b => b.SkillId).Distinct().OrderBy(id => id).ToList(),
        NamesById(definition.Dynamics.ProfessionBiases.Select(b => (b.ProfessionId, b.Name))),
        NamesById(definition.Dynamics.SkillBiases.Select(b => (b.SkillId, b.Name))),
        definition.Descriptors);

    private static IReadOnlyDictionary<int, string> NamesById(IEnumerable<(int Id, string? Name)> entries) =>
        entries.Where(e => e.Name is not null).ToDictionary(e => e.Id, e => e.Name!);
}
