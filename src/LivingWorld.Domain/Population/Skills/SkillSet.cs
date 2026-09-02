namespace LivingWorld.Domain;

/// <summary>Habilidades de um <c>Npc</c> (Fase 6, task 4; Fase 13, T11b: catálogo aberto por id
/// — mesmo padrão de <see cref="PopulationCatalog.ProfessionWeights"/>). Cada uma um <c>double</c>
/// em <c>[0, cap]</c>, habilidade nunca gerada vale 0 (nunca lança exceção, nunca precisa de lista
/// prévia de ids). Imutável: todo ganho passa por <see cref="WithGain"/>, que devolve uma nova
/// instância.</summary>
public sealed class SkillSet
{
    /// <summary>Público — permite ao <c>System.Text.Json</c> reidratar via construtor único
    /// (mesmo padrão de round-trip usado por <see cref="Npc"/>/<see cref="Personality"/>): o
    /// binding por construtor parametrizado do System.Text.Json exige uma propriedade pública
    /// com o mesmo nome do parâmetro.</summary>
    public IReadOnlyDictionary<int, double> Values { get; }

    public SkillSet(IReadOnlyDictionary<int, double> values) => Values = values;

    /// <summary>Nenhuma habilidade ainda ganhada — <see cref="Get"/> devolve 0 pra qualquer id
    /// (SKILL-01). Substitui o antigo <c>SkillSet.Initial(0)</c>: sem catálogo fechado, não há
    /// lista de ids pra pré-popular.</summary>
    public static readonly SkillSet Empty = new(new Dictionary<int, double>());

    public double Get(SkillType type) => Values.GetValueOrDefault(type.Id, 0.0);

    /// <summary>Aplica <paramref name="delta"/> à habilidade <paramref name="type"/>, clampado em
    /// <c>[0, cap]</c> — ganho no teto é absorvido sem exceção e sem efeito colateral em outra
    /// habilidade (SKILL-12). Devolve uma nova instância; as demais habilidades são
    /// preservadas.</summary>
    public SkillSet WithGain(SkillType type, double delta, double cap)
    {
        double newValue = Math.Clamp(Get(type) + delta, 0.0, cap);
        var copy = new Dictionary<int, double>(Values) { [type.Id] = newValue };
        return new SkillSet(copy);
    }
}
