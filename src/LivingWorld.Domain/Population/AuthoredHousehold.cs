namespace LivingWorld.Domain.Population;

/// <summary>Pessoa definida pelo autor do cenário. <c>Spouse</c>/<c>Mother</c>/<c>Father</c> são índices
/// na lista de membros da MESMA família. Personalidade, aparência e habilidades ausentes = sorteadas pelo mesmo
/// stream do gerador (ADR-0024). <c>Skills</c> mapeia id de habilidade → nível inicial em <c>[0,100]</c>. <c>Workplace</c> (#258) é o índice em <c>Workplaces[]</c> do cenário onde a
/// pessoa já começa empregada (o comerciante do mercado do bairro); ausente, ela procura emprego como qualquer outra.</summary>
public sealed record AuthoredPerson(
    string Name, Sex Sex, int AgeYears, int ProfessionId, Personality? Personality = null,
    int? Spouse = null, int? Mother = null, int? Father = null,
    Appearance? Appearance = null, IReadOnlyDictionary<int, int>? Skills = null, int? Workplace = null);

/// <summary><c>Settlement</c> nomeia o assentamento onde a família mora; nulo = vila inicial.</summary>
public sealed record AuthoredHousehold(string? Settlement, IReadOnlyList<AuthoredPerson> Members);
