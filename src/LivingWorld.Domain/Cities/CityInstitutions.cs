namespace LivingWorld.Domain.Cities;

// SPEC_DEVIATION (Fase 8, fix round 1, gap 1 — CITY-01 AC1): design.md Tech Decisions prometia
// estes 3 records vazios/stub para satisfazer "task 1 pede que existam", sem inventar
// comportamento de governo/cultura/tecnologia (isso é society.md/Fase 13+). Nenhum critério de
// verificação da Fase 8 testa comportamento deles — só existência.

/// <summary>Governo da cidade (Fase 8, CITY-01 AC1): stub sem campos comportamentais.</summary>
public sealed record CityGovernment
{
    public static readonly CityGovernment Empty = new();
}

/// <summary>Cultura da cidade (Fase 8, CITY-01 AC1): mesmo stub de <see cref="CityGovernment"/>.</summary>
public sealed record CityCulture
{
    public static readonly CityCulture Empty = new();
}

/// <summary>Tecnologia da cidade (Fase 8, CITY-01 AC1): mesmo stub de <see cref="CityGovernment"/>.</summary>
public sealed record CityTechnology
{
    public static readonly CityTechnology Empty = new();
}
