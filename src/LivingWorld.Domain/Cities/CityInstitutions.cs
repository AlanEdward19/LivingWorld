namespace LivingWorld.Domain.Cities;

// SPEC_DEVIATION: design.md Tech Decisions prometia estes 3 records vazios/stub, sem inventar
// comportamento de governo/cultura/tecnologia. Nenhum critério de
// verificação testa comportamento deles — só existência.

/// <summary>Governo da cidade: stub sem campos comportamentais.</summary>
public sealed record CityGovernment
{
    public static readonly CityGovernment Empty = new();
}

/// <summary>Cultura da cidade: mesmo stub de <see cref="CityGovernment"/>.</summary>
public sealed record CityCulture
{
    public static readonly CityCulture Empty = new();
}

/// <summary>Tecnologia da cidade: mesmo stub de <see cref="CityGovernment"/>.</summary>
public sealed record CityTechnology
{
    public static readonly CityTechnology Empty = new();
}
