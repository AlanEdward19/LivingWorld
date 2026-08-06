namespace LivingWorld.Simulation.Periods;

/// <summary>Catálogo ativo de um período validado (Fase 13, T12): ids de profissão declarados no
/// bloco População (<see cref="LivingWorld.Domain.PopulationCatalog.ProfessionIds"/> — vazio
/// significa sem restrição, mesma convenção de sempre) + ids de habilidade referenciados no
/// bloco <c>Dynamics</c> (<see cref="SkillBias"/>). Nenhum nome aqui — o motor só conhece id
/// (AD-023/AD-025); nome é dado externo (documentação/IA), fora deste catálogo.</summary>
public sealed record PeriodCatalog(IReadOnlyList<int> ProfessionIds, IReadOnlyList<int> SkillIds)
{
    public static PeriodCatalog From(PeriodDefinition definition) => new(
        definition.Population.Catalog.ProfessionIds.OrderBy(id => id).ToList(),
        definition.Dynamics.SkillBiases.Select(b => b.SkillId).Distinct().OrderBy(id => id).ToList());
}
