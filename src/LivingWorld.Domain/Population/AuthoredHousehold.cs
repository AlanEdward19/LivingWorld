namespace LivingWorld.Domain.Population;

/// <summary>Pessoa definida pelo autor do cenário. <c>Spouse</c>/<c>Mother</c>/<c>Father</c> são índices
/// na lista de membros da MESMA família. Personalidade ausente = sorteada pelo mesmo stream do gerador.</summary>
public sealed record AuthoredPerson(
    string Name, Sex Sex, int AgeYears, int ProfessionId, Personality? Personality = null,
    int? Spouse = null, int? Mother = null, int? Father = null);

/// <summary><c>Settlement</c> nomeia o assentamento onde a família mora; nulo = vila inicial.</summary>
public sealed record AuthoredHousehold(string? Settlement, IReadOnlyList<AuthoredPerson> Members);
